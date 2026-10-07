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

    public void Delete(Preset p) => File.Delete(p.FilePath);

    public string NewPath(string id)
    {
        var next = Directory.GetFiles(Dir, "*.json")
            .Select(f => Path.GetFileName(f).Split('-')[0])
            .Select(n => int.TryParse(n, out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return Path.Combine(Dir, $"{next:00}-{id}.json");
    }

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
