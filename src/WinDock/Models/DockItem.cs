namespace WinDock.Models;

public sealed class DockItem
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Args { get; set; }
    public string? Icon { get; set; }
    public string? Process { get; set; }
}
