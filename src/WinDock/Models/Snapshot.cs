namespace WinDock.Models;

public sealed class Snapshot
{
    public DateTime CapturedAt { get; set; }
    public Dictionary<string, string> Binary { get; set; } = [];
    public Dictionary<string, int> Numbers { get; set; } = [];
    public Dictionary<string, string> Links { get; set; } = [];

    public IEnumerable<string> LinkNames =>
        Links.Keys.Select(k => Path.GetFileNameWithoutExtension(k)).Order(StringComparer.CurrentCultureIgnoreCase);
}
