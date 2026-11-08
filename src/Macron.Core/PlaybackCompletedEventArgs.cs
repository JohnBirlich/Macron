namespace Macron.Core;

public sealed class PlaybackCompletedEventArgs : EventArgs
{
    public bool Cancelled { get; init; }
    public int IterationsCompleted { get; init; }
}
