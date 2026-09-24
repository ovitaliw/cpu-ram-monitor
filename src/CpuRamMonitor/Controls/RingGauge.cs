using System.Windows;
using System.Windows.Media;

namespace CpuRamMonitor.Controls;

/// <summary>Donut gauge: a track circle plus an arc from 12 o'clock, clockwise, for Value (0–100).</summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(RingGauge),
        new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double r = (size - Thickness) / 2;
        dc.DrawEllipse(null, new Pen(TrackBrush, Thickness), center, r, r);

        double fraction = Math.Clamp(Value, 0, 100) / 100;
        if (fraction <= 0) return;
        if (fraction >= 0.999)
        {
            dc.DrawEllipse(null, new Pen(Stroke, Thickness), center, r, r);
            return;
        }

        double angle = fraction * 360;
        double radians = (angle - 90) * Math.PI / 180;
        var start = new Point(center.X, center.Y - r);
        var end = new Point(center.X + r * Math.Cos(radians), center.Y + r * Math.Sin(radians));

        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(r, r), 0, angle > 180, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        arc.Freeze();

        dc.DrawGeometry(null, new Pen(Stroke, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, arc);
    }
}
