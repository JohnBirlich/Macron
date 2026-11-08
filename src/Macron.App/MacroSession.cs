namespace Macron.App;

using Macron.Core;
using Macron.Core.Events;
using Macron.Input.Windows;
using System.Windows.Threading;

/// <summary>
/// Оркестратор сессии: контроллер состояний, рекордер, проигрыватель,
/// глобальные горячие клавиши и репозиторий макросов.
/// WPF-зависимости изолированы здесь — через Dispatcher события приводятся к UI-потоку.
/// </summary>
public sealed class MacroSession : IDisposable
{
    private readonly MacroController _controller = new();
    private readonly InputRecorder _recorder = new();
    private readonly InputPlayer _player = new();
    private readonly Dispatcher _dispatcher;
    private readonly HotkeyManager _hotkeys;
    private readonly MacroRepository _repository = new();
    private readonly object _sync = new();
    private Macro? _currentMacro;

    public MacroState State => _controller.State;
    public int RecordedEventCount => _controller.RecordedEventCount;
    public IReadOnlyList<MacroSummary> Macros => _repository.Macros;
    public MacroRepository Repository => _repository;
    public Macro? SelectedMacro { get; set; }
    public Macro? CurrentMacro => _currentMacro;

    public event EventHandler<MacroState>? StateChanged;
    public event EventHandler<Macro>? MacroRecorded;
    public event EventHandler<MacroSummary>? MacroAdded;
    public event EventHandler<MacroSummary>? MacroRemoved;
    public event EventHandler<PlaybackCompletedEventArgs>? PlaybackCompleted;

    public MacroSession()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        _controller.StateChanged += (_, s) => _dispatcher.Invoke(() => StateChanged?.Invoke(this, s));
        _controller.PlaybackTick += (_, e) =>
        {
            switch (e)
            {
                case MouseMoveEvent m: _player.Play(m); break;
                case MouseDownEvent m: _player.Play(m); break;
                case MouseUpEvent m: _player.Play(m); break;
                case MouseWheelEvent m: _player.Play(m); break;
                case KeyDownEvent k: _player.Play(k); break;
                case KeyUpEvent k: _player.Play(k); break;
            }
        };
        _controller.PlaybackCompleted += (_, e) => _dispatcher.Invoke(() => PlaybackCompleted?.Invoke(this, e));
        _recorder.EventsCaptured += (_, events) =>
        {
            foreach (var evt in events)
            {
                _controller.AddRecordedEvent(evt);
            }
        };

        _repository.MacroRemoved += (_, summary) => MacroRemoved?.Invoke(this, summary);

        _hotkeys = new HotkeyManager(
            new[]
            {
                new HotkeyManager.Hotkey(1, 0, NativeMethods.VK_F6),     // Record
                new HotkeyManager.Hotkey(2, 0, NativeMethods.VK_F8),     // Play
                new HotkeyManager.Hotkey(3, 0, NativeMethods.VK_F7),     // Stop
                new HotkeyManager.Hotkey(4, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_F12), // Panic
            },
            new Dictionary<int, Action>
            {
                [1] = TryRecord,
                [2] = TryPlaySelectedDefault,
                [3] = Stop,
                [4] = () => _controller.RequestStop(),
            });
    }

    public bool TryRecord(bool recordMouseMoves = true)
    {
        if (!_controller.TryBeginRecording())
        {
            return false;
        }

        lock (_sync)
        {
            _currentMacro = null;
        }

        _recorder.Start(recordMouseMoves);
        _dispatcher.Invoke(() => StateChanged?.Invoke(this, State));
        return true;
    }

    public void Stop()
    {
        var state = State;

        if (state == MacroState.Recording)
        {
            var macro = _controller.EndRecording();
            _recorder.Stop();

            lock (_sync)
            {
                _currentMacro = macro;
            }

            if (macro is not null)
            {
                _repository.Add(macro);
                _dispatcher.Invoke(() =>
                {
                    MacroRecorded?.Invoke(this, macro);
                    MacroAdded?.Invoke(this, _repository.Macros[^1]);
                });
            }
        }
        else if (state == MacroState.Playing)
        {
            _controller.RequestStop();
        }

        _dispatcher.Invoke(() => StateChanged?.Invoke(this, State));
    }

    public Task Play(Macro macro, PlayOptions options)
    {
        if (_controller.State != MacroState.Idle)
        {
            return Task.CompletedTask;
        }

        return _controller.StartPlayingAsync(macro, options, e => _player.Play(e));
    }

    public Task<Macro> LoadMacro(MacroSummary summary) => _repository.LoadBySummary(summary);

    public bool TryPlaySelectedDefault()
    {
        if (SelectedMacro == null || State != MacroState.Idle)
        {
            return false;
        }

        _ = LoadMacro(SelectedMacro).ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                _ = Play(task.Result, new PlayOptions());
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        return true;
    }

    public void Dispose()
    {
        _controller.Dispose();
        _recorder.Dispose();
        _hotkeys.Dispose();
    }
}
