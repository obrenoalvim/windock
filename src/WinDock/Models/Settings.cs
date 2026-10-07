using System.Text.Json;

namespace WinDock.Models;

public sealed class Settings
{
    public bool AutoSwitch { get; set; } = true;
    public bool Notify { get; set; } = true;
    public string? ActiveId { get; set; }
    public List<PinItem> AlwaysPinned { get; set; } = [];

    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinDock");

    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json.Options) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json.Options));
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
