using System.Windows;
using System.Windows.Media;

namespace ZTorrent.App.Controls;

/// <summary>
/// A deterministic rounded progress strip. WPF's stock ProgressBar template
/// depends on internal part sizing; drawing the tiny bar ourselves guarantees
/// pixel-perfect gradients at every DPI and avoids two-way binding surprises.
/// </summary>
public sealed class LinearProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(LinearProgress),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceValue));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(LinearProgress),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(
        nameof(ProgressBrush), typeof(Brush), typeof(LinearProgress),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush? ProgressBrush
    {
        get => (Brush?)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    private static object CoerceValue(DependencyObject d, object baseValue) =>
        Math.Clamp((double)baseValue, 0, 1);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var radius = height / 2;
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, width, height), radius, radius));
        drawingContext.DrawRectangle(TrackBrush, null, new Rect(0, 0, width, height));

        var progressWidth = width * Value;
        if (progressWidth > 0.25)
        {
            drawingContext.DrawRectangle(ProgressBrush, null, new Rect(0, 0, progressWidth, height));
        }

        drawingContext.Pop();
    }
}
