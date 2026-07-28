using System.Globalization;

namespace Square.Platform.X11;

internal static class X11DisplayMetrics
{
    internal const double DefaultDpi = 96d;
    internal const double DefaultRefreshRate = 60d;

    internal static bool TryParseXftDpi(string? resources, out double dpi)
    {
        dpi = 0;
        if (string.IsNullOrWhiteSpace(resources)) return false;

        foreach (var line in resources.Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator < 0) continue;
            var name = line[..separator].Trim();
            if (!name.Equals("Xft.dpi", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("Xft*dpi", StringComparison.OrdinalIgnoreCase))
                continue;

            if (double.TryParse(line[(separator + 1)..].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var parsed)
                && IsUsableDpi(parsed))
            {
                dpi = parsed;
                return true;
            }
        }

        return false;
    }

    internal static double CalculatePhysicalDpi(
        int pixelWidth, int pixelHeight, int millimeterWidth, int millimeterHeight)
    {
        var horizontal = millimeterWidth > 0 ? pixelWidth * 25.4 / millimeterWidth : double.NaN;
        var vertical = millimeterHeight > 0 ? pixelHeight * 25.4 / millimeterHeight : double.NaN;
        var horizontalValid = IsUsableDpi(horizontal);
        var verticalValid = IsUsableDpi(vertical);
        if (horizontalValid && verticalValid) return (horizontal + vertical) / 2d;
        if (horizontalValid) return horizontal;
        if (verticalValid) return vertical;
        return DefaultDpi;
    }

    internal static double ResolveDpi(
        string? resources, int pixelWidth, int pixelHeight, int millimeterWidth, int millimeterHeight)
        => TryParseXftDpi(resources, out var dpi)
            ? dpi
            : CalculatePhysicalDpi(pixelWidth, pixelHeight, millimeterWidth, millimeterHeight);

    internal static float DpiToScale(double dpi)
        => IsUsableDpi(dpi) ? (float)(dpi / DefaultDpi) : 1f;

    internal static double NormalizeRefreshRate(double refreshRate)
        => double.IsFinite(refreshRate) && refreshRate is >= 24d and <= 360d
            ? refreshRate
            : DefaultRefreshRate;

    internal static long FrameIntervalTicks(double refreshRate, long stopwatchFrequency)
    {
        if (stopwatchFrequency <= 0) throw new ArgumentOutOfRangeException(nameof(stopwatchFrequency));
        refreshRate = NormalizeRefreshRate(refreshRate);
        return Math.Max(1, (long)Math.Round(stopwatchFrequency / refreshRate));
    }

    internal static long NextFrameDeadline(long currentDeadline, long now, long interval)
    {
        if (interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
        if (currentDeadline <= 0) return checked(now + interval);
        if (currentDeadline > now) return currentDeadline;

        var elapsedIntervals = (now - currentDeadline) / interval + 1;
        return checked(currentDeadline + elapsedIntervals * interval);
    }

    private static bool IsUsableDpi(double dpi)
        => double.IsFinite(dpi) && dpi is >= 48d and <= 768d;
}
