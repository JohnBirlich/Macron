namespace Macron.Core.Events;

public sealed record MouseWheelEvent : MacronEvent
{
    public int Delta { get; init; }
}
