namespace Macron.App;

/// <summary>
/// Пользовательские настройки, хранящиеся в %APPDATA%\Macron\settings.json.
/// </summary>
public sealed class Settings
{
    public double SpeedMultiplier { get; set; } = 1.0;
    public RepeatMode RepeatMode { get; set; } = RepeatMode.Once;
    public int RepeatCount { get; set; } = 1;
    public int DelaySeconds { get; set; } = 0;
    public bool RecordMouseMoves { get; set; } = true;
}
