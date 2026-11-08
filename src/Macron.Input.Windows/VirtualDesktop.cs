namespace Macron.Input.Windows;

/// <summary>
/// Нормализация координат относительно виртуального рабочего стола
/// (все мониторы, в том числе с отрицательными координатами) в диапазон 0–65535,
/// необходимый для MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK.
/// </summary>
internal static class VirtualDesktop
{
    public static int Left => NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
    public static int Top => NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
    public static int Width => NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
    public static int Height => NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

    public static uint NormalizeX(int x) => Normalize(x, Left, Width);
    public static uint NormalizeY(int y) => Normalize(y, Top, Height);

    private static uint Normalize(int coord, int origin, int size)
    {
        if (size <= 1)
        {
            return 0;
        }

        var value = (long)(coord - origin) * 65535 / (size - 1);
        if (value < 0)
        {
            return 0;
        }

        if (value > 65535)
        {
            return 65535;
        }

        return (uint)value;
    }
}
