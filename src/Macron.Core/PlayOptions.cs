namespace Macron.Core;

/// <summary>
/// Параметры воспроизведения: скорость (0.1×–10×), повтор и пауза перед стартом.
/// </summary>
public sealed class PlayOptions
{
    public double SpeedMultiplier { get; set; } = 1.0;
    public RepeatMode RepeatMode { get; set; } = RepeatMode.Once;
    public int RepeatCount { get; set; } = 1;
    public TimeSpan DelayBeforeStart { get; set; }

    public double ClampedSpeed => Math.Clamp(SpeedMultiplier, 0.1, 10.0);
}
