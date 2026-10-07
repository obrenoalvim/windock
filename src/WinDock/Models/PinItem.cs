using System.Text.Json.Serialization;

namespace WinDock.Models;

public sealed class PinItem
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Args { get; set; }
    public string? Icon { get; set; }
    public string? AppId { get; set; }
    public string? Favorites { get; set; }
    public string? Resolve { get; set; }

    [JsonIgnore]
    public bool IsRaw => Favorites is not null && Resolve is not null;

    [JsonIgnore]
    public bool IsLauncher => !IsRaw && Args is not null && AppId is not null;

    [JsonIgnore]
    public string? ResolvedTarget =>
        string.IsNullOrWhiteSpace(Target) ? null : Environment.ExpandEnvironmentVariables(Target);

    [JsonIgnore]
    public string? ResolvedIcon =>
        string.IsNullOrWhiteSpace(Icon) ? null : Environment.ExpandEnvironmentVariables(Icon);

    [JsonIgnore]
    public bool Exists => IsRaw || (ResolvedTarget is { } t && File.Exists(t));
}
