using System.Windows;
using System.Windows.Media;
using WinDock.Models;

namespace WinDock.Services;

public sealed record WidgetPalette(Color Top, Color Bottom, Color HotTop, Color HotBottom)
{
    public Color Ink => WidgetLooks.InkFor([Top, Bottom]);
    public Color Soft => WidgetLooks.WithAlpha(Ink, WidgetLooks.SoftAlpha);
    public Color Border => WidgetLooks.WithAlpha(Ink, 0x30);
}

public static class WidgetLooks
{
    public const string Colorful = "colorful";
    public const string Dock = "dock";
    public const string Accent = "accent";
    public const string Graphite = "graphite";
    public static readonly string[] All = [Colorful, Dock, Accent, Graphite];

    public const double MinContrast = 4.5;
    public const byte SoftAlpha = 0xE0;
    const byte TrackAlpha = 0x40;
    const double Step = 0.04;
    const int MaxSteps = 80;

    public static readonly Color DarkInk = Color.FromRgb(0x1B, 0x1D, 0x24);
    static readonly Color Red = Color.FromRgb(0xE5, 0x48, 0x3B);

    static readonly string[] Surfaces =
    [
        "WClock", "WPomodoro", "WBreak", "WStopwatch", "WTimer", "WCountdown", "WProgress", "WActivity", "WBattery",
        "WPeripherals", "WMusic", "WWeather", "WWeatherNight", "WNetwork", "WNote", "WTodo", "WRigdeck", "WClaude", "WSteam", "WEpic",
    ];
    static readonly string[] Hots = ["WClaudeHot"];
    static readonly string[] Borders = ["WBorder", "WRigdeckEdge", "WSteamEdge", "WEpicEdge"];
    static readonly string[] BrandInks = ["WRigdeckInk", "WGamesInk"];
    static readonly string[] BrandSofts = ["WRigdeckAccent", "WSteamAccent", "WEpicAccent"];

    public static IEnumerable<string> Keys => Surfaces.Concat(Hots).Concat(Borders).Concat(BrandInks).Concat(BrandSofts);

    static readonly (string Top, string Bottom)[] ColorfulSamples = [("#3965B8", "#2245A1"), ("#FFB547", "#E8890C"), ("#3DDC97", "#13A86B")];

    static readonly Dictionary<(string, string?), Brush> RoleCache = [];

    public static WidgetPalette? Palette(string look, DockTheme dock, Color accent) => look switch
    {
        Dock => FromDock(dock),
        Accent => Build(Mix(accent, Colors.White, 0.08), Mix(accent, Colors.Black, 0.18)),
        Graphite => Build(Hex("#3A3D46"), Hex("#1E2026")),
        _ => null
    };

    public static IReadOnlyList<WidgetPalette> Samples(string look, DockTheme dock, Color accent) =>
        Palette(look, dock, accent) is { } palette
            ? [palette, palette, palette]
            : ColorfulSamples.Select(s => Build(Hex(s.Top), Hex(s.Bottom))).ToList();

    public static void Apply(ResourceDictionary resources, IReadOnlyDictionary<string, object> colorful, string look, DockTheme dock, Color accent)
    {
        if (Palette(look, dock, accent) is not { } palette)
        {
            foreach (var (key, brush) in colorful) resources[key] = brush;
            return;
        }
        Set(resources, Surfaces, Gradient(palette.Top, palette.Bottom));
        Set(resources, Hots, Gradient(palette.HotTop, palette.HotBottom));
        Set(resources, Borders, Solid(palette.Border));
        Set(resources, BrandInks, Solid(palette.Ink));
        Set(resources, BrandSofts, Solid(palette.Soft));
    }

    static void Set(ResourceDictionary resources, IEnumerable<string> keys, Brush brush)
    {
        foreach (var key in keys) resources[key] = brush;
    }

    static WidgetPalette FromDock(DockTheme dock)
    {
        var mid = Mix(Opaque(Hex(dock.BarTop)), Opaque(Hex(dock.BarBottom)), 0.5);
        return dock.Dark
            ? Build(Mix(mid, Colors.White, 0.12), Mix(mid, Colors.White, 0.05))
            : Build(Mix(mid, Colors.Black, 0.03), Mix(mid, Colors.Black, 0.08));
    }

    public static WidgetPalette Build(Color top, Color bottom)
    {
        var surface = Fit(top, bottom);
        var hot = Fit(Mix(Red, Colors.White, 0.06), Mix(Red, Colors.Black, 0.18));
        return new WidgetPalette(surface.Top, surface.Bottom, hot.Top, hot.Bottom);
    }

    public static (Color Top, Color Bottom) Fit(Color top, Color bottom) => Fit(top, bottom, InkFor([top, bottom]));

    public static (Color Top, Color Bottom) Fit(Color top, Color bottom, Color ink)
    {
        var away = Light(ink) ? Colors.Black : Colors.White;
        for (var i = 0; i < MaxSteps && !Readable([top, bottom], ink); i++)
        {
            top = Mix(top, away, Step);
            bottom = Mix(bottom, away, Step);
        }
        return (top, bottom);
    }

    public static Color AlertPlate(Color ink) => Fit(Red, Red, ink).Top;

    public static Color InkFor(IReadOnlyList<Color> stops) =>
        stops.Min(s => Contrast(Colors.White, s)) >= stops.Min(s => Contrast(DarkInk, s)) ? Colors.White : DarkInk;

    public static Color AlertFor(IReadOnlyList<Color> stops)
    {
        var toward = Light(InkFor(stops)) ? Colors.White : Colors.Black;
        var color = Red;
        for (var i = 0; i < MaxSteps && stops.Any(s => Contrast(color, s) < MinContrast); i++) color = Mix(color, toward, Step);
        return color;
    }

    public static bool Readable(IReadOnlyList<Color> stops, Color ink) =>
        stops.All(s => Contrast(ink, s) >= MinContrast && Contrast(Over(WithAlpha(ink, SoftAlpha), s), s) >= MinContrast);

    public static bool Readable(IReadOnlyList<Color> stops) => Readable(stops, InkFor(stops));

    public static bool AlertReadable(IReadOnlyList<Color> stops) => stops.All(s => Contrast(AlertFor(stops), s) >= MinContrast);

    public static bool Readable(WidgetPalette p) =>
        Readable([p.Top, p.Bottom]) && AlertReadable([p.Top, p.Bottom]) && Readable([p.HotTop, p.HotBottom]);

    public static Brush Role(Brush? surface, string? role)
    {
        var stops = Stops(surface);
        var key = (string.Join(',', stops), role);
        if (RoleCache.TryGetValue(key, out var cached)) return cached;
        var ink = InkFor(stops);
        var color = role switch
        {
            "soft" => WithAlpha(ink, SoftAlpha),
            "track" => WithAlpha(ink, TrackAlpha),
            "alert" => AlertFor(stops),
            _ => ink
        };
        return RoleCache[key] = Solid(color);
    }

    public static IReadOnlyList<Color> Stops(Brush? surface) => surface switch
    {
        GradientBrush gradient when gradient.GradientStops.Count > 0 => gradient.GradientStops.Select(g => g.Color).ToList(),
        SolidColorBrush solid => [solid.Color],
        _ => [Colors.Black]
    };

    static bool Light(Color c) => Contrast(c, Colors.Black) > Contrast(c, Colors.White);

    public static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    public static double Contrast(Color a, Color b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static Color Over(Color top, Color bottom)
    {
        var a = top.A / 255.0;
        return Color.FromRgb(
            (byte)Math.Round(top.R * a + bottom.R * (1 - a)),
            (byte)Math.Round(top.G * a + bottom.G * (1 - a)),
            (byte)Math.Round(top.B * a + bottom.B * (1 - a)));
    }

    public static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round(from.R + (to.R - from.R) * amount),
        (byte)Math.Round(from.G + (to.G - from.G) * amount),
        (byte)Math.Round(from.B + (to.B - from.B) * amount));

    static Color Opaque(Color c) => Color.FromRgb(c.R, c.G, c.B);

    public static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    static Brush Gradient(Color top, Color bottom) => Frozen(new LinearGradientBrush(top, bottom, new Point(0, 0), new Point(1, 1)));

    static Brush Solid(Color color) => Frozen(new SolidColorBrush(color));

    static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
