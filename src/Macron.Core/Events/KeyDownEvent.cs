namespace Macron.Core.Events;

public sealed record KeyDownEvent : MacronEvent
{
    public int Vk { get; init; }
    public int Scan { get; init; }
}
