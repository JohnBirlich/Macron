namespace Macron.Input.Windows;

/// <summary>
/// Глобальные горячие клавиши через RegisterHotKey в окне-only-сообщениях
/// на собственном потоке с очередью сообщений. Работает независимо от состояния
/// главного окна WPF, поэтому Panic (Ctrl+Alt+F12) останавливает воспроизведение
/// даже в бесконечном цикле.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    public record Hotkey(int Id, uint Modifiers, uint Vk);

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly Thread _thread;
    private IntPtr _hwnd;
    private uint _threadId;
    private readonly Dictionary<int, Action> _handlers = new();
    private readonly ManualResetEventSlim _started = new();
    private bool _disposed;

    public HotkeyManager(IReadOnlyList<Hotkey> hotkeys, Dictionary<int, Action> handlers)
    {
        foreach (var (id, action) in handlers)
        {
            _handlers[id] = action;
        }

        _thread = new Thread(() => ThreadLoop(hotkeys))
        {
            IsBackground = true,
            Name = "Macron Hotkeys"
        };
        _thread.Start();
        _started.Wait();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        NativeMethods.PostThreadMessage(_threadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(3));
    }

    private void ThreadLoop(IReadOnlyList<Hotkey> hotkeys)
    {
        var hInstance = NativeMethods.GetModuleHandle(null);
        WndProc wndProc = WndProcHandler;
        var gc = GCHandle.Alloc(wndProc);

        try
        {
            _hwnd = NativeMethods.CreateWindowEx(0, "static", "MacronHotkeyWindow", 0, 0, 0, 0, 0,
                NativeMethods.HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                _started.Set();
                return;
            }

            _ = NativeMethods.SetWindowLongPtrPtr(_hwnd, NativeMethods.GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(wndProc));

            foreach (var hotkey in hotkeys)
            {
                _ = NativeMethods.RegisterHotKey(_hwnd, hotkey.Id, hotkey.Modifiers, hotkey.Vk);
            }

            _threadId = GetCurrentThreadIdWin32();
            _started.Set();

            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                if (msg.Message == NativeMethods.WM_HOTKEY)
                {
                    var id = (int)(long)msg.WParam;
                    if (_handlers.TryGetValue(id, out var handler))
                    {
                        handler();
                    }
                }
                else
                {
                    NativeMethods.TranslateMessage(ref msg);
                    NativeMethods.DispatchMessage(ref msg);
                }
            }

            foreach (var hotkey in hotkeys)
            {
                _ = NativeMethods.UnregisterHotKey(_hwnd, hotkey.Id);
            }
        }
        finally
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_hwnd);
            }

            gc.Free();
        }
    }

    private IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadIdWin32();
}
