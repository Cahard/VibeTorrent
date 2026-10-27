using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VibeTorrent.Core.Localization;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.App.Converters;

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is not null;
        if (parameter is string text && text.Equals("inverse", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = !string.IsNullOrWhiteSpace(value as string);
        if (parameter is string text && text.Equals("inverse", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double number;
        try
        {
            number = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            number = 0;
        }

        var visible = number > 0;
        if (parameter is string text && text.Equals("inverse", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BytesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bytes = value is null ? 0L : System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
        return DisplayFormat.Bytes(bytes, 1, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class SpeedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bytes = value is null ? 0L : System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
        return DisplayFormat.Speed(bytes, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EtaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var seconds = value is null ? (long?)null : System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
        return DisplayFormat.Eta(seconds, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class PercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is null ? 0d : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        var decimals = 0;
        if (parameter is string text && int.TryParse(text, out var parsed))
        {
            decimals = parsed;
        }

        return DisplayFormat.Percent(fraction, decimals, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class RatioConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ratio = value is null ? 0d : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        return DisplayFormat.Ratio(ratio, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class CountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is null ? 0 : System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
        return DisplayFormat.Count(count, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusBrushConverter : IValueConverter
{
    private static readonly Dictionary<TorrentStatus, SolidColorBrush> Cache = new()
    {
        [TorrentStatus.Metadata] = Frozen("#A855F7"),
        [TorrentStatus.Queued] = Frozen("#93C5FD"),
        [TorrentStatus.Checking] = Frozen("#FBBF24"),
        [TorrentStatus.Downloading] = Frozen("#38BDF8"),
        [TorrentStatus.Seeding] = Frozen("#34D399"),
        [TorrentStatus.Completed] = Frozen("#A78BFA"),
        [TorrentStatus.Paused] = Frozen("#FBBF24"),
        [TorrentStatus.Stopped] = Frozen("#6D7693"),
        [TorrentStatus.Error] = Frozen("#FB7185")
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TorrentStatus status && Cache.TryGetValue(status, out var brush)
            ? brush
            : Cache[TorrentStatus.Stopped];

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}

public sealed class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TorrentStatus status
            ? StatusText(status)
            : L.T("Waiting");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    public static string StatusText(TorrentStatus status) => status switch
    {
        TorrentStatus.Metadata => L.T("Metadata"),
        TorrentStatus.Queued => L.T("Queued"),
        TorrentStatus.Checking => L.T("Checking"),
        TorrentStatus.Downloading => L.T("Downloading"),
        TorrentStatus.Seeding => L.T("Seeding"),
        TorrentStatus.Completed => L.T("Completed"),
        TorrentStatus.Paused => L.T("Paused"),
        TorrentStatus.Stopped => L.T("Stopped"),
        TorrentStatus.Error => L.T("Error"),
        _ => L.T("Waiting")
    };
}

public sealed class StatusGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TorrentStatus status
            ? status switch
            {
                TorrentStatus.Metadata => "\uE896",
                TorrentStatus.Queued => "\uE823",
                TorrentStatus.Checking => "\uE9D5",
                TorrentStatus.Downloading => "\uE896",
                TorrentStatus.Seeding => "\uE898",
                TorrentStatus.Completed => "\uE73E",
                TorrentStatus.Paused => "\uE769",
                TorrentStatus.Stopped => "\uE71A",
                TorrentStatus.Error => "\uE7BA",
                _ => "\uE946"
            }
            : "\uE946";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusGradientConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is TorrentStatus state ? state : TorrentStatus.Stopped;
        var (from, to) = status switch
        {
            TorrentStatus.Downloading => ("#0E7490", "#4338CA"),
            TorrentStatus.Seeding => ("#065F46", "#0F766E"),
            TorrentStatus.Completed => ("#4C1D95", "#7C3AED"),
            TorrentStatus.Paused => ("#78350F", "#B45309"),
            TorrentStatus.Error => ("#881337", "#BE123C"),
            TorrentStatus.Metadata => ("#581C87", "#7E22CE"),
            _ => ("#1E293B", "#334155")
        };

        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(from),
            (Color)ColorConverter.ConvertFromString(to),
            new Point(0, 0),
            new Point(1, 1));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class FileIconConverter : IValueConverter
{
    private static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase)
        { ".mkv", ".mp4", ".avi", ".mov", ".webm", ".m4v", ".ts", ".wmv", ".flv" };
    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
        { ".flac", ".mp3", ".wav", ".aac", ".ogg", ".m4a", ".opus", ".wma", ".ape" };
    private static readonly HashSet<string> Image = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".tiff", ".psd" };
    private static readonly HashSet<string> Archive = new(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso" };
    private static readonly HashSet<string> Code = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".js", ".ts", ".py", ".rs", ".go", ".json", ".xml", ".html", ".css", ".sql" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string ?? string.Empty;
        var extension = Path.GetExtension(path);
        if (Video.Contains(extension)) return "\uE714";
        if (Audio.Contains(extension)) return "\uE8D6";
        if (Image.Contains(extension)) return "\uEB9F";
        if (Archive.Contains(extension)) return "\uE7B8";
        if (Code.Contains(extension)) return "\uE943";
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".msi", StringComparison.OrdinalIgnoreCase)) return "\uE7EF";
        if (extension.Equals(".torrent", StringComparison.OrdinalIgnoreCase)) return "\uE896";
        return "\uE8A5";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class FileAccentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var extension = Path.GetExtension(value as string ?? string.Empty).ToLowerInvariant();
        var color = extension switch
        {
            ".mkv" or ".mp4" or ".avi" or ".mov" or ".webm" => "#38BDF8",
            ".flac" or ".mp3" or ".wav" or ".opus" => "#F472B6",
            ".jpg" or ".png" or ".webp" => "#A78BFA",
            ".zip" or ".rar" or ".7z" or ".iso" => "#FBBF24",
            ".exe" or ".msi" => "#34D399",
            _ => "#94A3B8"
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class FirstLetterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string ?? string.Empty).Trim();
        return text.Length == 0 ? "V" : text[..1].ToUpper(culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class DateTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
        {
            return "—";
        }

        return parameter as string == "short"
            ? DisplayFormat.ShortDate(dateTime, culture)
            : DisplayFormat.Date(dateTime, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class FilePriorityTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is FilePriority priority
            ? priority switch
            {
                FilePriority.DoNotDownload => L.T("Do not download"),
                FilePriority.Low => L.T("Low"),
                FilePriority.High => L.T("High"),
                FilePriority.Maximum => L.T("Maximum"),
                _ => L.T("Normal")
            }
            : L.T("Normal");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
