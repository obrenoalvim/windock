using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinDock.Views;

public sealed class RingArc : Shape
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(RingArc),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    protected override Geometry DefiningGeometry
    {
        get
        {
            var p = Math.Clamp(Progress, 0, 1);
            var size = Math.Min(ActualWidth, ActualHeight);
            var r = Math.Max(0, (size - StrokeThickness) / 2);
            if (p <= 0 || r <= 0) return Geometry.Empty;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            if (p >= 0.9999) return new EllipseGeometry(center, r, r);

            var angle = p * 2 * Math.PI;
            var start = new Point(center.X, center.Y - r);
            var end = new Point(center.X + r * Math.Sin(angle), center.Y - r * Math.Cos(angle));
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, p > 0.5, SweepDirection.Clockwise, true));
            var geometry = new PathGeometry([figure]);
            geometry.Freeze();
            return geometry;
        }
    }
}
