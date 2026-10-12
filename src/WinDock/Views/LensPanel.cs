using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinDock.Views;

public enum LensMode { None, Scale, Magnify }

public sealed class LensPanel : Panel
{
    const double ScaleCap = 0.3;

    public static readonly DependencyProperty PointerProperty = Value(nameof(Pointer), double.NaN, FrameworkPropertyMetadataOptions.AffectsArrange);
    public static readonly DependencyProperty AmplitudeProperty = Value(nameof(Amplitude), 0.0, FrameworkPropertyMetadataOptions.AffectsArrange);
    public static readonly DependencyProperty MagnificationProperty = Value(nameof(Magnification), 1.75, FrameworkPropertyMetadataOptions.AffectsArrange);
    public static readonly DependencyProperty RadiusProperty = Value(nameof(Radius), 130.0, FrameworkPropertyMetadataOptions.AffectsArrange);
    public static readonly DependencyProperty GapProperty = Value(nameof(Gap), 8.0, FrameworkPropertyMetadataOptions.AffectsMeasure);
    public static readonly DependencyProperty CornerProperty = Value(nameof(Corner), 22.0, FrameworkPropertyMetadataOptions.AffectsRender);

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(LensMode), typeof(LensPanel),
        new FrameworkPropertyMetadata(LensMode.Magnify, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty InsetProperty = DependencyProperty.Register(nameof(Inset), typeof(Thickness), typeof(LensPanel),
        new FrameworkPropertyMetadata(new Thickness(12, 10, 12, 12), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty BarBrushProperty = Paint(nameof(BarBrush));
    public static readonly DependencyProperty BarStrokeProperty = Paint(nameof(BarStroke));
    public static readonly DependencyProperty GlowBrushProperty = Paint(nameof(GlowBrush));
    public static readonly DependencyProperty IndicatorBrushProperty = Paint(nameof(IndicatorBrush));
    public static readonly DependencyProperty FocusBrushProperty = Paint(nameof(FocusBrush));
    public static readonly DependencyProperty ShadowBrushProperty = Paint(nameof(ShadowBrush));

    public static readonly DependencyProperty LensProperty = DependencyProperty.RegisterAttached("Lens", typeof(bool), typeof(LensPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static readonly DependencyProperty IndicatorProperty = DependencyProperty.RegisterAttached("Indicator", typeof(int), typeof(LensPanel),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    static readonly Brush Catcher = Frozen(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)));

    readonly Dictionary<UIElement, ScaleTransform> transforms = [];
    readonly List<(Point Center, int Kind)> indicators = [];

    public double Pointer { get => (double)GetValue(PointerProperty); set => SetValue(PointerProperty, value); }
    public double Amplitude { get => (double)GetValue(AmplitudeProperty); set => SetValue(AmplitudeProperty, value); }
    public double Magnification { get => (double)GetValue(MagnificationProperty); set => SetValue(MagnificationProperty, value); }
    public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public double Corner { get => (double)GetValue(CornerProperty); set => SetValue(CornerProperty, value); }
    public LensMode Mode { get => (LensMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public Thickness Inset { get => (Thickness)GetValue(InsetProperty); set => SetValue(InsetProperty, value); }
    public Brush? BarBrush { get => (Brush?)GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public Brush? BarStroke { get => (Brush?)GetValue(BarStrokeProperty); set => SetValue(BarStrokeProperty, value); }
    public Brush? GlowBrush { get => (Brush?)GetValue(GlowBrushProperty); set => SetValue(GlowBrushProperty, value); }
    public Brush? IndicatorBrush { get => (Brush?)GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }
    public Brush? FocusBrush { get => (Brush?)GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public Brush? ShadowBrush { get => (Brush?)GetValue(ShadowBrushProperty); set => SetValue(ShadowBrushProperty, value); }

    public static bool GetLens(DependencyObject o) => (bool)o.GetValue(LensProperty);
    public static void SetLens(DependencyObject o, bool value) => o.SetValue(LensProperty, value);
    public static int GetIndicator(DependencyObject o) => (int)o.GetValue(IndicatorProperty);
    public static void SetIndicator(DependencyObject o, int value) => o.SetValue(IndicatorProperty, value);

    public Rect Bar { get; private set; }
    public double BaseWidth { get; private set; }

    public event EventHandler? Arranged;

    protected override Size MeasureOverride(Size available)
    {
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double width = 0, height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(free);
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var inset = Inset;
        width += Gap * Math.Max(0, InternalChildren.Count - 1) + inset.Left + inset.Right;
        BaseWidth = width;
        return new Size(width, height + inset.Top + inset.Bottom);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var children = InternalChildren.Cast<UIElement>().ToList();
        var n = children.Count;
        indicators.Clear();
        if (n == 0)
        {
            Bar = Rect.Empty;
            InvalidateVisual();
            return final;
        }

        var inset = Inset;
        var widths = children.Select(c => c.DesiredSize.Width).ToArray();
        var lens = children.Select(GetLens).ToArray();
        var tileHeight = children.Max(c => c.DesiredSize.Height);
        var baseTotal = widths.Sum() + Gap * (n - 1);
        var x0 = Math.Round((final.Width - baseTotal) / 2);
        var bottom = final.Height - inset.Bottom;
        var amplitude = Mode == LensMode.None ? 0 : Amplitude;
        var pointer = Pointer - x0;

        var layout = LensMath.Layout(widths, lens, Gap, pointer, Mode == LensMode.Magnify ? amplitude : 0, Magnification, Radius);
        var hovered = -1;
        if (Mode == LensMode.Scale && amplitude > 0)
            for (var i = 0; i < n; i++)
                if (lens[i] && pointer >= layout.Left[i] && pointer < layout.Left[i] + widths[i] + Gap) hovered = i;

        for (var i = 0; i < n; i++)
        {
            var child = children[i];
            var w = widths[i];
            var h = child.DesiredSize.Height;
            var scale = i == hovered ? 1 + Math.Min(Magnification - 1, ScaleCap) * amplitude : layout.Scale[i];
            var slot = Mode == LensMode.Magnify ? w * layout.Scale[i] : w;
            var left = x0 + layout.Left[i];
            child.Arrange(new Rect(left + (slot - w) / 2, bottom - h, w, h));
            Transform(child, scale);

            var kind = GetIndicator(child);
            if (kind > 0) indicators.Add((new Point(left + slot / 2, final.Height - inset.Bottom / 2), kind));
        }

        var last = n - 1;
        var right = x0 + layout.Left[last] + (Mode == LensMode.Magnify ? widths[last] * layout.Scale[last] : widths[last]);
        Bar = new Rect(x0 + layout.Left[0] - inset.Left, bottom - tileHeight - inset.Top,
            right - (x0 + layout.Left[0]) + inset.Left + inset.Right, tileHeight + inset.Top + inset.Bottom);
        InvalidateVisual();
        Arranged?.Invoke(this, EventArgs.Empty);
        return final;
    }

    void Transform(UIElement child, double scale)
    {
        if (!transforms.TryGetValue(child, out var t))
        {
            t = new ScaleTransform();
            transforms[child] = t;
            child.RenderTransformOrigin = new Point(0.5, 1);
            child.RenderTransform = t;
        }
        if (t.ScaleX != scale) t.ScaleX = t.ScaleY = scale;
    }

    protected override void OnVisualChildrenChanged(DependencyObject? added, DependencyObject? removed)
    {
        base.OnVisualChildrenChanged(added, removed);
        if (removed is UIElement gone) transforms.Remove(gone);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Bar.IsEmpty) return;
        var bar = Bar;
        var corner = Math.Min(Corner, bar.Height / 2);

        if (Amplitude > 0.01 && Mode != LensMode.None)
            dc.DrawRectangle(Catcher, null, new Rect(bar.Left, 0, bar.Width, Math.Max(0, bar.Top)));

        if (ShadowBrush is { } shadow)
            for (var k = 1; k <= 3; k++)
            {
                var r = bar;
                r.Inflate(k * 1.5, k * 1.5);
                r.Offset(0, k);
                dc.DrawRoundedRectangle(shadow, null, r, corner + k * 1.5, corner + k * 1.5);
            }

        var pen = BarStroke is { } stroke ? new Pen(stroke, 1) : null;
        var inner = bar;
        inner.Inflate(-0.5, -0.5);
        dc.DrawRoundedRectangle(BarBrush, pen, inner, corner, corner);
        if (GlowBrush is { } glow) dc.DrawRoundedRectangle(glow, null, inner, corner, corner);

        foreach (var (center, kind) in indicators)
        {
            if (kind == 2 && FocusBrush is { } focus)
                dc.DrawRoundedRectangle(focus, null, new Rect(center.X - 7, center.Y - 2, 14, 4), 2, 2);
            else if (IndicatorBrush is { } dot)
                dc.DrawEllipse(dot, null, center, 2.2, 2.2);
        }
    }

    static DependencyProperty Value(string name, double value, FrameworkPropertyMetadataOptions options) =>
        DependencyProperty.Register(name, typeof(double), typeof(LensPanel), new FrameworkPropertyMetadata(value, options));

    static DependencyProperty Paint(string name) =>
        DependencyProperty.Register(name, typeof(Brush), typeof(LensPanel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static Brush Frozen(Brush b)
    {
        b.Freeze();
        return b;
    }
}
