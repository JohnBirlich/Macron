namespace Macron.Core.Tests;

public class MacroControllerTests
{
    [Fact]
    public void Transitions_idle_to_recording_and_back()
    {
        var ctl = new MacroController();
        Assert.Equal(MacroState.Idle, ctl.State);

        Assert.True(ctl.TryBeginRecording());
        Assert.Equal(MacroState.Recording, ctl.State);

        // Повторная запись без остановки запрещена.
        Assert.False(ctl.TryBeginRecording());

        ctl.AddRecordedEvent(new MouseMoveEvent { DtMs = 10, X = 1, Y = 2 });
        var macro = ctl.EndRecording();
        Assert.NotNull(macro);
        Assert.Single(macro.Events);
        Assert.Equal(MacroState.Idle, ctl.State);

        // Завершение записи вне состояния записи возвращает null.
        Assert.Null(ctl.EndRecording());
    }

    [Fact]
    public void Events_recorded_only_while_recording()
    {
        var ctl = new MacroController();
        ctl.AddRecordedEvent(new MouseMoveEvent { DtMs = 1 });
        Assert.Null(ctl.EndRecording());
    }

    [Fact]
    public void Play_forbidden_while_recording()
    {
        var ctl = new MacroController();
        ctl.TryBeginRecording();

        var ex = Assert.Throws<InvalidOperationException>(() => ctl.StartPlayingAsync(
            new Macro(), new PlayOptions(), _ => { }));
        Assert.Equal(MacroState.Recording, ctl.State);
        ctl.EndRecording();
    }

    [Fact]
    public async Task Play_transitions_idle_to_playing_to_idle()
    {
        var ctl = new MacroController();
        var macro = new Macro { Events = new List<MacronEvent> { new MouseMoveEvent { DtMs = 0, X = 1, Y = 2 } } };

        var played = new List<MacronEvent>();
        var task = ctl.StartPlayingAsync(macro, new PlayOptions(), played.Add);

        Assert.Equal(MacroState.Playing, ctl.State);
        await task;
        Assert.Equal(MacroState.Idle, ctl.State);
        Assert.Single(played);
    }

    [Fact]
    public async Task Request_stop_cancels_long_playback_quickly()
    {
        var ctl = new MacroController();
        // 100 событий по 1 секунде — без отмены playback шёл бы долго.
        var macro = new Macro
        {
            Events = Enumerable.Range(0, 100)
                .Select(i => new MouseMoveEvent { DtMs = 1000, X = i, Y = 0 })
                .ToList()
        };

        var task = ctl.StartPlayingAsync(macro, new PlayOptions(), _ => { });

        Assert.Equal(MacroState.Playing, ctl.State);
        ctl.RequestStop();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await task.WaitAsync(cts.Token);
        Assert.Equal(MacroState.Idle, ctl.State);
    }

    [Fact]
    public void Timing_wait_respects_cancellation()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var ok = TimingScheduler.Wait(TimeSpan.FromMilliseconds(25), 1.0, token);
        Assert.True(ok);

        cts.Cancel();
        var cancelled = TimingScheduler.Wait(TimeSpan.FromHours(1), 1.0, token);
        Assert.False(cancelled);
    }
}
