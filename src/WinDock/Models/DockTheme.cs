namespace WinDock.Models;

public sealed record DockTheme(
    string Id,
    bool Dark,
    string BarTop,
    string BarBottom,
    string? Stroke,
    string Ink,
    double StrokeWidth = 1,
    double Corner = 0.34,
    string? Shadow = null,
    string? Indicator = null)
{
    public string Muted => "#B3" + Ink[^6..];
    public string Line => Dark ? "#38FFFFFF" : "#26000000";
    public string DotIdle => Dark ? "#66FFFFFF" : "#4D000000";
    public string TileFace => Dark ? "#24FFFFFF" : "#14000000";

    public static IReadOnlyList<DockTheme> All { get; } =
    [
        new(DockSettings.ThemeDark, true, "#E0292C37", "#E0111318", "#47FFFFFF", "#F4EFE7", Shadow: "#1A000000", Indicator: "#E6FFFFFF"),
        new(DockSettings.ThemeLight, false, "#EBF8F8FB", "#EBE1E4EB", "#1F000000", "#1B1D24", Shadow: "#12000000", Indicator: "#B3000000"),
        new("midnight", true, "#EB0C1022", "#F2030409", "#66FFFFFF", "#EEF1FF", Shadow: "#26000000", Indicator: "#E6FFFFFF"),
        new("neon", true, "#F2120A1C", "#F205040A", "#FFFF2BD6", "#F2FBFF", StrokeWidth: 1.6, Corner: 0.5, Shadow: "#3322E5FF", Indicator: "#FF22E5FF"),
        new("canarinho", false, "#F5FFDB1F", "#F51FA34A", "#FF0B2F8A", "#0B1F4D", StrokeWidth: 2, Shadow: "#1A002776", Indicator: "#FF0B2F8A"),
        new("aqua", false, "#F2F2F9FF", "#F29CCBFF", "#FF2F7DE1", "#0F2A55", StrokeWidth: 1.4, Shadow: "#1F1E5BB8", Indicator: "#FF1E5BB8"),
        new("sunset", true, "#EBFF7A45", "#EB5B2A86", "#59FFFFFF", "#FFF6EE", Shadow: "#1F3A0F40", Indicator: "#F2FFFFFF"),
        new("paper", false, "#FAFCFBF7", "#FAF1EEE7", "#17000000", "#2A2622", Corner: 0.16, Shadow: "#0D000000", Indicator: "#99000000"),
        new("floating", true, "#03000000", "#03000000", null, "#FFFFFF", Indicator: "#E6FFFFFF"),
    ];

    public static DockTheme Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
