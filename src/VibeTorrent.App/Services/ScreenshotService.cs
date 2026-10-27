using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VibeTorrent.App.Services;

/// <summary>
/// Development/preview helper used by --screenshot(s). It renders the live WPF
/// visual tree, so the result is pixel-identical to what the user sees without
/// depending on desktop capture APIs or window focus.
/// </summary>
public static class ScreenshotService
{
    public static void Capture(Window window, string path, int? width = null, int? height = null)
    {
        window.UpdateLayout();
        var pixelWidth = Math.Max(1, width ?? (int)Math.Ceiling(window.ActualWidth));
        var pixelHeight = Math.Max(1, height ?? (int)Math.Ceiling(window.ActualHeight));

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(window);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        encoder.Save(stream);
    }
}
