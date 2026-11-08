namespace Macron.Core;

using System.Collections.ObjectModel;
using Macron.Core.Events;

/// <summary>
/// Записанный макрос: имя, дата создания и список событий с дельта-временами.
/// </summary>
public sealed class Macro
{
    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<MacronEvent> Events { get; set; } = new();

    public long TotalDurationMs => Events.Count > 0 ? Events.Sum(e => (long)e.DtMs) : 0;
    public TimeSpan Duration => TimeSpan.FromMilliseconds(TotalDurationMs);
    public int EventCount => Events.Count;
}
