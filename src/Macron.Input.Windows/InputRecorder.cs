namespace Macron.Input.Windows;

using Macron.Core.Events;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Низкоуровневые хуки мыши и клавиатуры на отдельном потоке с очередью сообщений.
/// Колбэк хука только копирует структуру в безблокирующую очередь и выходит —
/// иначе Windows отключит хук по таймауту.
/// События, порождённые SendInput (включая наш собственный воспроизводитель),
/// отфильтровываются по флагам INJECTED и метке dwExtraInfo, чтобы макрос не
/// записал сам себя.
/// </summary>
public sealed class InputRecorder : IDisposable
{
    private const int MoveThrottleMs = 16; // около 60 событий движения мыши в секунду
    private const uint WM_QUIT = 0x0012;

    private readonly object _sync = new();
    private Thread? _hookThread;
    private uint _hookThreadId;
    private IntPtr _hHookKeyboard;
    private IntPtr _hHookMouse;
    private readonly ConcurrentQueue<MacronEvent> _queue = new();
    private long _lastTicks;
    private long _lastMoveEmitTicks;
    private bool _recordMouseMoves;
    private bool _disposed;

    public event EventHandler<MacronEvent[]>? EventsCaptured;

    public bool Start(bool recordMouseMoves)
    {
        lock (_sync)
        {
            if (_disposed || _hookThread != null)
            {
                return false;
            }

            _queue.Clear();
            _lastTicks = Stopwatch.GetTimestamp();
            _lastMoveEmitTicks = _lastTicks;
            _recordMouseMoves = recordMouseMoves;
        }

        _hookThread = new Thread(HookLoop)
        {
            IsBackground = true,
            Name = "Macron Input Hook"
        };
        _hookThread.Start();

        // Даём потоку время вызвать SetWindowsHookEx и запомнить свой OS-ID.
        for (var i = 0; i < 100 && _hookThreadId == 0; i++)
        {
            Thread.Sleep(10);
        }

        return _hookThreadId != 0;
    }

    public void Stop()
    {
        Thread? thread;
        uint threadId;

        lock (_sync)
        {
            if (_hookThread == null)
            {
                return;
            }

            thread = _hookThread;
            threadId = _hookThreadId;
        }

        // WM_QUIT выводит поток хуков из GetMessage.
        NativeMethods.PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);

        if (!thread.Join(TimeSpan.FromSeconds(3)))
        {
            // Страховка: если поток повис в хуке, завершаем жестко.
            try
            {
                thread.Abort();
            }
            catch
            {
                // Игнорируем.
            }
        }

        lock (_sync)
        {
            if (_hHookKeyboard != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hHookKeyboard);
                _hHookKeyboard = IntPtr.Zero;
            }

            if (_hHookMouse != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hHookMouse);
                _hHookMouse = IntPtr.Zero;
            }

            _hookThread = null;
            _hookThreadId = 0;
        }
    }

    /// <summary>Забрать все накопленные события из очереди.</summary>
    public List<MacronEvent> Drain()
    {
        var list = new List<MacronEvent>();
        while (_queue.TryDequeue(out var evt))
        {
            list.Add(evt);
        }

        return list;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private void HookLoop()
    {
        // OS-ID потока нужен, чтобы извне выдать ему WM_QUIT.
        _hookThreadId = GetCurrentThreadIdWin32();

        IntPtr hMod = NativeMethods.GetModuleHandle(null);

        NativeMethods.HookProc kbdCallback = KbdCallback;
        NativeMethods.HookProc mouseCallback = MouseCallback;
        var kbdHandle = GCHandle.Alloc(kbdCallback);
        var mouseHandle = GCHandle.Alloc(mouseCallback);

        try
        {
        _hHookKeyboard = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, kbdCallback, hMod, 0);
        _hHookMouse = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, mouseCallback, hMod, 0);

        if (_hHookKeyboard == IntPtr.Zero || _hHookMouse == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0);
        }
        finally
        {
            if (_hHookKeyboard != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hHookKeyboard);
            }

            if (_hHookMouse != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hHookMouse);
            }

            kbdHandle.Free();
            mouseHandle.Free();
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadIdWin32();

    private IntPtr KbdCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        var msg = (int)(long)wParam;
        var isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
        var isUp = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;

        if (isDown || isUp)
        {
            var s = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            if ((s.Flags & NativeMethods.LLKHF_INJECTED) == 0 && IsNotOwnExtra(s.ExtraInfo))
            {
                var evt = isDown
                    ? (MacronEvent)new KeyDownEvent { Vk = (int)s.VkCode, Scan = (int)s.ScanCode }
                    : new KeyUpEvent { Vk = (int)s.VkCode, Scan = (int)s.ScanCode };

                Enqueue(evt);
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        var msg = (int)(long)wParam;
        var s = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

        if ((s.Flags & NativeMethods.LLMHF_INJECTED) != 0 || !IsNotOwnExtra(s.ExtraInfo))
        {
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        MacronEvent? evt = msg switch
        {
            NativeMethods.WM_MOUSEMOVE => new MouseMoveEvent { X = s.Pt.X, Y = s.Pt.Y },
            NativeMethods.WM_LBUTTONDOWN => new MouseDownEvent { Button = MouseButton.Left },
            NativeMethods.WM_LBUTTONUP => new MouseUpEvent { Button = MouseButton.Left },
            NativeMethods.WM_RBUTTONDOWN => new MouseDownEvent { Button = MouseButton.Right },
            NativeMethods.WM_RBUTTONUP => new MouseUpEvent { Button = MouseButton.Right },
            NativeMethods.WM_MBUTTONDOWN => new MouseDownEvent { Button = MouseButton.Middle },
            NativeMethods.WM_MBUTTONUP => new MouseUpEvent { Button = MouseButton.Middle },
            NativeMethods.WM_XBUTTONDOWN => new MouseDownEvent
            {
                Button = ((s.MouseData >> 16) & 0xFFFF) switch
                {
                    1 => MouseButton.XButton1,
                    2 => MouseButton.XButton2,
                    _ => MouseButton.Left
                }
            },
            NativeMethods.WM_XBUTTONUP => new MouseUpEvent
            {
                Button = ((s.MouseData >> 16) & 0xFFFF) switch
                {
                    1 => MouseButton.XButton1,
                    2 => MouseButton.XButton2,
                    _ => MouseButton.Left
                }
            },
            NativeMethods.WM_MOUSEWHEEL => new MouseWheelEvent
            {
                Delta = (short)((s.MouseData >> 16) & 0xFFFF)
            },
            _ => null
        };

        if (evt is not null)
        {
            Enqueue(evt);
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void Enqueue(MacronEvent evt)
    {
        lock (_sync)
        {
            if (!_recordMouseMoves && evt is MouseMoveEvent)
            {
                return;
            }
        }

        var now = Stopwatch.GetTimestamp();
        long dtMs = (long)((now - _lastTicks) * 1000 / Stopwatch.Frequency);
        _lastTicks = now;
        if (dtMs < 0)
        {
            dtMs = 0;
        }

        evt = evt with { DtMs = dtMs };

        if (evt is MouseMoveEvent)
        {
            var sinceEmitTicks = now - _lastMoveEmitTicks;
            var sinceEmitMs = (long)(sinceEmitTicks * 1000 / Stopwatch.Frequency);
            if (sinceEmitMs < MoveThrottleMs)
            {
                return;
            }

            _lastMoveEmitTicks = now;
        }

        _queue.Enqueue(evt);
        EventsCaptured?.Invoke(this, new[] { evt });
    }

    private static bool IsNotOwnExtra(UIntPtr extra)
    {
        return extra != InputPlayer.MagicExtraInfo;
    }
}
