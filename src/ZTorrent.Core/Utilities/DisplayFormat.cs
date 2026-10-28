using System.Globalization;
using ZTorrent.Core.Localization;

namespace ZTorrent.Core.Utilities;

/// <summary>
/// Human readable values for the UI. Torrent clients conventionally use binary
/// units (1 KB = 1024 bytes). Unit names and date patterns follow the active
/// application language, not the OS locale.
/// </summary>
public static class DisplayFormat
{
    public static string Bytes(long bytes, int decimals = 1, CultureInfo? culture = null)
    {
        culture ??= L.Culture;
        if (bytes < 0)
        {
            return "—";
        }

        if (bytes < 1024)
        {
            return $"{bytes.ToString("N0", culture)} {L.T("B")}";
        }

        var value = (double)bytes;
        var unit = 0;
        var units = new[] { L.T("B"), L.T("KB"), L.T("MB"), L.T("GB"), L.T("TB"), L.T("PB") };
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var digits = value >= 100 ? 0 : decimals;
        return $"{value.ToString($"N{digits}", culture)} {units[unit]}";
    }

    public static string Speed(long bytesPerSecond, CultureInfo? culture = null) =>
        bytesPerSecond <= 0
            ? $"0 {L.T("B")}{L.T("/s")}"
            : $"{Bytes(bytesPerSecond, 1, culture)}{L.T("/s")}";

    public static string Percent(double fraction, int decimals = 1, CultureInfo? culture = null)
    {
        culture ??= L.Culture;
        var value = Math.Clamp(fraction, 0, 1) * 100;
        return $"{value.ToString($"N{decimals}", culture)}%";
    }

    public static string Eta(long? seconds, CultureInfo? culture = null)
    {
        culture ??= L.Culture;
        if (seconds is null || seconds < 0 || seconds > TimeSpan.FromDays(365).TotalSeconds)
        {
            return "—";
        }

        if (seconds < 60)
        {
            return seconds.Value <= 1
                ? L.T("less than a minute")
                : L.F("{0} sec", seconds.Value);
        }

        var span = TimeSpan.FromSeconds(seconds.Value);
        if (span.TotalDays >= 1)
        {
            return L.F("{0} d {1} h", (int)span.TotalDays, span.Hours);
        }

        if (span.TotalHours >= 1)
        {
            return L.F("{0} h {1:D2} min", (int)span.TotalHours, span.Minutes);
        }

        return L.F("{0} min {1:D2} sec", span.Minutes, span.Seconds);
    }

    public static string Ratio(double ratio, CultureInfo? culture = null)
    {
        culture ??= L.Culture;
        if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0)
        {
            return 0d.ToString("N2", culture);
        }

        return ratio.ToString("N2", culture);
    }

    public static string Count(int value, CultureInfo? culture = null) =>
        value.ToString("N0", culture ?? L.Culture);

    public static string Date(DateTime value, CultureInfo? culture = null) =>
        value.ToLocalTime().ToString(DatePattern, culture ?? L.Culture);

    public static string ShortDate(DateTime value, CultureInfo? culture = null) =>
        value.ToLocalTime().ToString(ShortDatePattern, culture ?? L.Culture);

    private static string DatePattern => LocalizationManager.Instance.Language switch
    {
        AppLanguage.Russian => "d MMMM, HH:mm",
        AppLanguage.Korean => "yyyy년 M월 d일 HH:mm",
        _ => "MMM d, HH:mm"
    };

    private static string ShortDatePattern => LocalizationManager.Instance.Language switch
    {
        AppLanguage.Russian => "dd.MM.yy",
        AppLanguage.Korean => "yyyy.MM.dd",
        _ => "MM/dd/yy"
    };
}
