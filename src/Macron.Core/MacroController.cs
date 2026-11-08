namespace Macron.Core;

using Macron.Core.Events;

/// <summary>
/// Единственный класс, через который проходят все переходы конечного автомата
/// Idle → Recording → Idle → Playing → Idle. Не зависит от WPF и WinAPI —
/// взаимодействие со вводом идёт через колбэки, передаваемые извне.
/// </summary>
public sealed class MacroController : IDisposable
{
    private readonly object _sync = new();
    private MacroState _state = MacroState.Idle;
    private readonly List<MacronEvent> _recordingBuffer = new();

    private CancellationTokenSource? _playbackCts;
    private Task? _playbackTask;

    public MacroState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public int RecordedEventCount
    {
        get
        {
            lock (_sync)
            {
                return _recordingBuffer.Count;
            }
        }
    }

    public event EventHandler<MacroState>? StateChanged;
    public event EventHandler<MacronEvent>? EventRecorded;
    public event EventHandler<MacronEvent>? PlaybackTick;
    public event EventHandler<PlaybackCompletedEventArgs>? PlaybackCompleted;

    /// <summary>Начать запись. Возвращает false, если приложение не в состоянии Idle.</summary>
    public bool TryBeginRecording()
    {
        lock (_sync)
        {
            if (_state != MacroState.Idle || _playbackTask != null && !_playbackTask.IsCompleted)
            {
                return false;
            }

            _state = MacroState.Recording;
            _recordingBuffer.Clear();
        }

        RaiseStateChanged(MacroState.Recording);
        return true;
    }

    /// <summary>Закончить запись и вернуть собранный макрос. null, если не шла запись.</summary>
    public Macro? EndRecording()
    {
        lock (_sync)
        {
            if (_state != MacroState.Recording)
            {
                return null;
            }

            _state = MacroState.Idle;
            var macro = BuildMacro(_recordingBuffer);
            _recordingBuffer.Clear();
            RaiseStateChanged(MacroState.Idle);
            return macro;
        }
    }

    /// <summary>Добавить событие в буфер записи. Безопасно вызывать из потока хуков.</summary>
    public void AddRecordedEvent(MacronEvent evt)
    {
        lock (_sync)
        {
            if (_state != MacroState.Recording)
            {
                return;
            }

            _recordingBuffer.Add(evt);
        }

        EventRecorded?.Invoke(this, evt);
    }

    /// <summary>
    /// Запустить воспроизведение на фоновом потоке. playEvent вызывается для каждого события,
    /// которое нужно отослать системе (SendInput). Возвращает задачу завершения.
    /// </summary>
    public Task StartPlayingAsync(Macro macro, PlayOptions options, Action<MacronEvent> playEvent,
        CancellationToken external = default)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(playEvent);

        CancellationTokenSource linkedCts;
        lock (_sync)
        {
            if (_state != MacroState.Idle)
            {
                throw new InvalidOperationException("Воспроизведение возможно только из состояния Idle.");
            }

            if (_playbackTask != null && !_playbackTask.IsCompleted)
            {
                throw new InvalidOperationException("Воспроизведение уже идёт.");
            }

            _state = MacroState.Playing;
        }

        RaiseStateChanged(MacroState.Playing);

        _playbackCts = new CancellationTokenSource();
        linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_playbackCts.Token, external);

        _playbackTask = Task.Run(() => PlayLoop(macro, options, playEvent, linkedCts.Token));
        return _playbackTask;
    }

    /// <summary>Остановить воспроизведение (или запись) максимально быстро.</summary>
    public void RequestStop()
    {
        lock (_sync)
        {
            _playbackCts?.Cancel();
        }
    }

    private void PlayLoop(Macro macro, PlayOptions options, Action<MacronEvent> playEvent, CancellationToken ct)
    {
        var iterations = 0;
        bool cancelled = false;

        try
        {
            var delay = options.DelayBeforeStart;
            if (delay > TimeSpan.Zero && !TimingScheduler.Wait(delay, 1.0, ct))
            {
                cancelled = true;
                return;
            }

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    cancelled = true;
                    return;
                }

                var events = macro.Events;
                for (var i = 0; i < events.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    playEvent(events[i]);

                    if (i + 1 < events.Count)
                    {
                        if (!TimingScheduler.Wait(TimeSpan.FromMilliseconds(events[i + 1].DtMs), options.ClampedSpeed, ct))
                        {
                            cancelled = true;
                            return;
                        }
                    }
                }

                iterations++;

                if (options.RepeatMode == RepeatMode.Once)
                {
                    return;
                }

                if (options.RepeatMode == RepeatMode.Count)
                {
                    if (iterations >= options.RepeatCount)
                    {
                        return;
                    }
                }

                // RepeatMode.Infinite — продолжаем цикл до RequestStop.
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        finally
        {
            lock (_sync)
            {
                _state = MacroState.Idle;
            }

            RaiseStateChanged(MacroState.Idle);
            PlaybackCompleted?.Invoke(this, new PlaybackCompletedEventArgs
            {
                Cancelled = cancelled,
                IterationsCompleted = iterations
            });
        }
    }

    private static Macro BuildMacro(List<MacronEvent> events)
    {
        return new Macro
        {
            Name = "",
            CreatedUtc = DateTimeOffset.UtcNow,
            Events = new List<MacronEvent>(events)
        };
    }

    private void RaiseStateChanged(MacroState state)
    {
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        RequestStop();

        var task = _playbackTask;
        if (task != null && !task.IsCompleted)
        {
            try
            {
                task.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Игнорируем при завершении приложения.
            }
        }
    }
}
