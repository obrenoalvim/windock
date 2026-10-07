using System.Text.Json;
using WinDock.Models;

namespace WinDock.Services;

public sealed class PresetStore
{
    public string Dir { get; } = Path.Combine(Settings.Dir, "presets");

    public List<Preset> Load()
    {
        Seed();
        var list = new List<Preset>();
        foreach (var file in Directory.GetFiles(Dir, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var p = JsonSerializer.Deserialize<Preset>(File.ReadAllText(file), Json.Options);
                if (p is null) continue;
                p.FilePath = file;
                if (p.Id == "") p.Id = Path.GetFileNameWithoutExtension(file);
                if (p.Name == "") p.Name = p.Id;
                list.Add(p);
            }
            catch (Exception e) when (e is JsonException or IOException or FormatException) { }
        }
        return list;
    }

    public void Save(Preset p) =>
        File.WriteAllText(p.FilePath, JsonSerializer.Serialize(p, Json.Options));

    void Seed()
    {
        if (Directory.Exists(Dir) && Directory.EnumerateFiles(Dir, "*.json").Any()) return;
        Directory.CreateDirectory(Dir);
        var bundled = Path.Combine(AppContext.BaseDirectory, "Presets");
        if (!Directory.Exists(bundled)) return;
        foreach (var f in Directory.GetFiles(bundled, "*.json"))
            File.Copy(f, Path.Combine(Dir, Path.GetFileName(f)), overwrite: false);
    }
}
