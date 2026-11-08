namespace Macron.Core.Events;

public sealed record MouseMoveEvent : MacronEvent
{
    public int X { get; init; }
    public int Y { get; init; }
}
