namespace Macron.Core.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Одно зарегистрированное действие пользователя.
/// DtMs — delta времени в миллисекундах от предыдущего события.
/// </summary>
[JsonConverter(typeof(MacronEventJsonConverter))]
public abstract record MacronEvent
{
    public long DtMs { get; init; }
}
