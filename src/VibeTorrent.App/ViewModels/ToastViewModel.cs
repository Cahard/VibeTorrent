using System.Windows.Media;

namespace VibeTorrent.App.ViewModels;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class ToastViewModel
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public ToastKind Kind { get; init; } = ToastKind.Info;
    public DateTime CreatedAt { get; } = DateTime.Now;

    public string Glyph => Kind switch
    {
        ToastKind.Success => "\uE73E",
        ToastKind.Warning => "\uE7BA",
        ToastKind.Error => "\uEA39",
        _ => "\uE946"
    };

    public Brush AccentBrush => Kind switch
    {
        ToastKind.Success => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        ToastKind.Warning => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
        ToastKind.Error => new SolidColorBrush(Color.FromRgb(251, 113, 133)),
        _ => new SolidColorBrush(Color.FromRgb(34, 211, 238))
    };
}
