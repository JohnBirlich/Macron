namespace Macron.Core.Events;

public sealed record MouseDownEvent : MacronEvent
{
    public MouseButton Button { get; init; }
}
