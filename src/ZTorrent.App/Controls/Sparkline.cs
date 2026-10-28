using System.Windows;
using System.Windows.Media;

namespace ZTorrent.App.Controls;

/// <summary>
/// A tiny area chart used by the traffic cards. Values are normalized against
/// their own maximum, and a smooth polyline keeps it readable at 120 px wide.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(1.6, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var values = Values?.Where(double.IsFinite).ToArray();
        if (values is null || values.Length == 0 || ActualWidth <= 2 || ActualHeight <= 2)
        {
            return;
        }

        if (values.All(value => value < 1))
        {
            return;
        }

        var max = Math.Max(1d, values.Max());
        var min = Math.Max(0d, values.Min());
        var range = Math.Max(1d, max - min);
        var width = ActualWidth;
        var height = ActualHeight;
        var points = new Point[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            var x = values.Length == 1 ? width : i * width / (values.Length - 1);
            var normalized = (values[i] - min) / range;
            var y = height - 2 - normalized * (height - 4);
            points[i] = new Point(x, y);
        }

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(points[0], false, false);
            if (points.Length == 1)
            {
                context.LineTo(points[0], true, false);
            }
            else
            {
                context.PolyLineTo(points[1..], true, false);
            }
        }

        line.Freeze();

        if (Fill is not null && points.Length > 1)
        {
            var area = new StreamGeometry();
            using (var context = area.Open())
            {
                context.BeginFigure(new Point(0, height), true, true);
                context.LineTo(points[0], true, false);
                context.PolyLineTo(points[1..], true, false);
                context.LineTo(new Point(width, height), true, false);
            }

            area.Freeze();
            drawingContext.DrawGeometry(Fill, null, area);
        }

        var pen = new Pen(Stroke ?? Brushes.DeepSkyBlue, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        drawingContext.DrawGeometry(null, pen, line);

        if (points.Length > 0)
        {
            drawingContext.DrawEllipse(Stroke, null, points[^1], 2.2, 2.2);
        }
    }
}
