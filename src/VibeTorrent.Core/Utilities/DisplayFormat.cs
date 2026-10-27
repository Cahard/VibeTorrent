using System.Globalization;

namespace VibeTorrent.Core.Utilities;

/// <summary>
/// Human readable values for the UI. Torrent clients conventionally use binary
/// units (1 KB = 1024 bytes), and Russian uses a comma as the decimal separator.
/// </summary>
public static class DisplayFormat
{
    private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ", "ТБ", "ПБ"];
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Bytes(long bytes, int decimals = 1, CultureInfo? culture = null)
    {
        culture ??= Russian;
        if (bytes < 0)
        {
            return "—";
        }

        if (bytes < 1024)
        {
            return $"{bytes.ToString("N0", culture)} Б";
        }

        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var digits = value >= 100 ? 0 : decimals;
        return $"{value.ToString($"N{digits}", culture)} {Units[unit]}";
    }

    public static string Speed(long bytesPerSecond, CultureInfo? culture = null) =>
        bytesPerSecond <= 0 ? "0 Б/с" : $"{Bytes(bytesPerSecond, 1, culture)}/с";

    public static string Percent(double fraction, int decimals = 1, CultureInfo? culture = null)
    {
        culture ??= Russian;
        var value = Math.Clamp(fraction, 0, 1) * 100;
        return $"{value.ToString($"N{decimals}", culture)}%";
    }

    public static string Eta(long? seconds, CultureInfo? culture = null)
    {
        culture ??= Russian;
        if (seconds is null || seconds < 0 || seconds > TimeSpan.FromDays(365).TotalSeconds)
        {
            return "—";
        }

        if (seconds < 60)
        {
            return seconds.Value <= 1 ? "меньше минуты" : $"{seconds.Value} сек";
        }

        var span = TimeSpan.FromSeconds(seconds.Value);
        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays} д {span.Hours} ч";
        }

        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours} ч {span.Minutes:D2} мин";
        }

        return $"{span.Minutes} мин {span.Seconds:D2} сек";
    }

    public static string Ratio(double ratio, CultureInfo? culture = null)
    {
        culture ??= Russian;
        if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0)
        {
            return "0,00";
        }

        return ratio.ToString("N2", culture);
    }

    public static string Count(int value, CultureInfo? culture = null) =>
        value.ToString("N0", culture ?? Russian);

    public static string Date(DateTime value, CultureInfo? culture = null) =>
        value.ToLocalTime().ToString("d MMMM, HH:mm", culture ?? Russian);

    public static string ShortDate(DateTime value, CultureInfo? culture = null) =>
        value.ToLocalTime().ToString("dd.MM.yy", culture ?? Russian);
}
