namespace Macron.Core;

/// <summary>
/// Гибридное ожидание с отменой.
/// Thread.Sleep до отметки «минус ~2 мс», затем SpinWait. Сон режется на кусочки
/// не более 50 мс, поэтому отмену через CancellationToken можно обнаружить
/// быстрее чем за 100 мс — даже в середине 30-секундной паузы.
/// </summary>
public static class TimingScheduler
{
    private const int MaxSleepMs = 50;
    private const int NearThresholdMs = 2;

    /// <returns>true — интерoyal отждидан отработал полностью; false — отменено по CancellationToken.</returns>
    public static bool Wait(TimeSpan delay, double speedMultiplier, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        var effective = TimeSpan.FromTicks((long)(delay.Ticks / speedMultiplier));
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var remaining = effective - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return true;
            }

            if (remaining > TimeSpan.FromMilliseconds(NearThresholdMs))
            {
                var sleepFor = remaining - TimeSpan.FromMilliseconds(NearThresholdMs);
                if (sleepFor > TimeSpan.FromMilliseconds(MaxSleepMs))
                {
                    sleepFor = TimeSpan.FromMilliseconds(MaxSleepMs);
                }

                Thread.Sleep(sleepFor);
            }
            else
            {
                SpinWait.SpinUntil(
                    () => cancellationToken.IsCancellationRequested || stopwatch.Elapsed >= effective,
                    1);
            }
        }
    }
}
