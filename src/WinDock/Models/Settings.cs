using System.Text.Json;

namespace WinDock.Models;

public sealed class Settings
{
    public bool AutoSwitch { get; set; } = true;
    public bool Notify { get; set; } = true;
    public string Language { get; set; } = "en";
    public string? ActiveId { get; set; }
    public List<PinItem> AlwaysPinned { get; set; } = [];
    public DockSettings Dock { get; set; } = new();
    public bool SyncTaskbar { get; set; }
    public Dictionary<string, DateTime> FolderSeen { get; set; } = [];

    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinDock");

    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try { return Parse(File.ReadAllText(FilePath)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(); }
    }

    public static Settings Parse(string json)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(json, Json.Options) ?? new();
            settings.Dock ??= new();
            settings.Dock.Clamp();
            return settings;
        }
        catch (JsonException) { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json.Options));
    }
}

public sealed class DockSettings
{
    public const string LensNone = "none";
    public const string LensScale = "scale";
    public const string LensMagnify = "magnify";
    public const string ThemeDark = "dark";
    public const string ThemeLight = "light";

    public bool Enabled { get; set; } = true;
    public bool ReplaceTaskbar { get; set; }
    public bool AutoHide { get; set; }
    public bool Reserve { get; set; } = true;
    public bool FollowWindows { get; set; }
    public string Lens { get; set; } = LensMagnify;
    public double Magnification { get; set; } = 1.75;
    public double LensWidth { get; set; } = 2.5;
    public double IconSize { get; set; } = 46;
    public string Theme { get; set; } = ThemeDark;
    public string? Screen { get; set; }

    public void Clamp()
    {
        Magnification = Math.Clamp(Magnification, 1.1, 2.4);
        LensWidth = Math.Clamp(LensWidth, 1, 4.5);
        IconSize = Math.Clamp(IconSize, 32, 72);
        if (Lens is not (LensNone or LensScale or LensMagnify)) Lens = LensMagnify;
        if (Theme is not (ThemeDark or ThemeLight)) Theme = ThemeDark;
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
