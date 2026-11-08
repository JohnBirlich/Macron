namespace Macron.Core.Events;

public sealed record MouseUpEvent : MacronEvent
{
    public MouseButton Button { get; init; }
}
