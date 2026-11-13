using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinDock.Models;

namespace WinDock.Services;

public static class IconStyles
{
    public const string Original = "original", Plate = "plate", Squircle = "squircle", Glass = "glass", Mono = "mono", Duotone = "duotone", Gray = "gray";

    public static readonly string[] All = [Original, Plate, Squircle, Glass, Mono, Duotone, Gray];

    const int Size = 128;

    static readonly Dictionary<(ImageSource, string), ImageSource> Cache = [];

    public static ImageSource? Apply(ImageSource? icon, string style, DockTheme theme, string accent)
    {
        if (icon is not BitmapSource source || style == Original || !All.Contains(style)) return icon;
        var key = (icon, $"{style}|{theme.Id}|{accent}");
        if (Cache.TryGetValue(key, out var hit)) return hit;

        BitmapSource made = style switch
        {
            Plate => OnPlate(source, theme, 0.62, glass: false),
            Glass => OnPlate(source, theme, 0.66, glass: true),
            Squircle => Squircled(source),
            Mono => Recolor(source, MonoOf(Paint(theme.Ink)), tintAlpha: true),
            Duotone => Recolor(source, DuotoneOf(theme.Dark ? Paint("#0E1017") : Paint("#23202B"), Paint(accent)), tintAlpha: false),
            _ => Recolor(source, l => Color.FromRgb((byte)(l * 255), (byte)(l * 255), (byte)(l * 255)), tintAlpha: false)
        };
        made.Freeze();
        return Cache[key] = made;
    }

    static Color Paint(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    static Func<double, Color> MonoOf(Color ink) =>
        l => Color.FromArgb((byte)(Math.Clamp((l - 0.12) / 0.7, 0, 1) * 255), ink.R, ink.G, ink.B);

    static Func<double, Color> DuotoneOf(Color shadow, Color light) => l => Lerp(shadow, light, Math.Clamp((l - 0.08) / 0.8, 0, 1));

    static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    static BitmapSource Recolor(BitmapSource source, Func<double, Color> map, bool tintAlpha)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight;
        var px = new byte[w * h * 4];
        bgra.CopyPixels(px, w * 4, 0);
        for (var i = 0; i < px.Length; i += 4)
        {
            double b = px[i], g = px[i + 1], r = px[i + 2];
            var alpha = px[i + 3];
            var luma = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
            var c = map(luma);
            px[i] = c.B;
            px[i + 1] = c.G;
            px[i + 2] = c.R;
            px[i + 3] = tintAlpha ? (byte)(alpha * c.A / 255) : alpha;
        }
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
    }

    static BitmapSource OnPlate(BitmapSource source, DockTheme theme, double scale, bool glass)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var plate = new Rect(2, 2, Size - 4, Size - 4);
            var radius = Size * 0.24;
            Brush fill;
            Pen? rim;
            if (glass)
            {
                var tint = theme.Dark ? Colors.White : Colors.Black;
                fill = new LinearGradientBrush(Color.FromArgb(theme.Dark ? (byte)0x4A : (byte)0x26, tint.R, tint.G, tint.B),
                    Color.FromArgb(theme.Dark ? (byte)0x14 : (byte)0x0C, tint.R, tint.G, tint.B), 90);
                rim = new Pen(new SolidColorBrush(Color.FromArgb(theme.Dark ? (byte)0x80 : (byte)0x40, tint.R, tint.G, tint.B)), 2);
            }
            else
            {
                fill = new SolidColorBrush(theme.Dark ? Paint("#2A2E39") : Paint("#F4F5F8"));
                rim = new Pen(new SolidColorBrush(theme.Dark ? Color.FromArgb(0x3A, 255, 255, 255) : Color.FromArgb(0x26, 0, 0, 0)), 2);
            }
            dc.DrawRoundedRectangle(fill, rim, plate, radius, radius);
            var side = Size * scale;
            dc.DrawImage(source, new Rect((Size - side) / 2, (Size - side) / 2, side, side));
        }
        return Render(visual);
    }

    static BitmapSource Squircled(BitmapSource source)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var box = new Rect(4, 4, Size - 8, Size - 8);
            dc.PushClip(new RectangleGeometry(box, Size * 0.22, Size * 0.22));
            dc.DrawImage(source, box);
            dc.Pop();
        }
        return Render(visual);
    }

    static BitmapSource Render(DrawingVisual visual)
    {
        var target = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return target;
    }
}
