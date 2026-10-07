namespace WinDock.Models;

public sealed class PinItem
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? AppId { get; set; }
    public string? Favorites { get; set; }
    public string? Resolve { get; set; }

    public bool IsRaw => Favorites is not null && Resolve is not null;

    public string? ResolvedTarget =>
        string.IsNullOrWhiteSpace(Target) ? null : Environment.ExpandEnvironmentVariables(Target);

    public bool Exists => IsRaw || (ResolvedTarget is { } t && File.Exists(t));
}
