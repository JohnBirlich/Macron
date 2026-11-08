namespace Macron.Core.Events;

public sealed record KeyUpEvent : MacronEvent
{
    public int Vk { get; init; }
    public int Scan { get; init; }
}
