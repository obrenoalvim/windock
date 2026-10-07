using System.Text.Json.Serialization;

namespace WinDock.Models;

public sealed class Preset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Glyph { get; set; } = "●";
    public string Accent { get; set; } = "#FFB547";
    public bool LaunchOnActivate { get; set; }
    public List<Slot> Schedule { get; set; } = [];
    public List<string> Widgets { get; set; } = [];
    public List<DockItem> Items { get; set; } = [];

    [JsonIgnore]
    public string FilePath { get; set; } = "";
}
