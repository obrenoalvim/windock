using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public abstract class DockTile : ObservableObject
{
    public abstract string Key { get; }
    public virtual bool Lens => false;
    public virtual int Indicator => 0;
}

public sealed class SeparatorTile(string key) : DockTile
{
    public override string Key { get; } = key;
}

public sealed class StartTile : DockTile
{
    public override string Key => "start";
    public override bool Lens => true;
    public string Tip => Loc.T("start");
}

public sealed partial class AppTile : DockTile, IDisposable
{
    readonly AppMatch match;

    public AppTile(string key, string name, ImageSource? icon, PinItem? pin, AppMatch match, FolderStack? folder = null)
    {
        Key = key;
        Name = name;
        Icon = icon;
        Pin = pin;
        this.match = match;
        Folder = folder;
        Initial = name.Length > 0 ? name[..1].ToUpperInvariant() : "?";
        if (folder is not null)
        {
            Badge = folder.NewCount;
            folder.Changed += () => Badge = folder.NewCount;
        }
    }

    public override string Key { get; }
    public override bool Lens => true;
    public override int Indicator => IsFocused ? 2 : Windows.Count > 0 ? 1 : 0;

    public string Name { get; }
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;
    public string Initial { get; }
    public PinItem? Pin { get; }
    public FolderStack? Folder { get; }
    public bool IsPinned => Pin is not null;
    public bool IsFolder => Folder is not null;
    public string? Exe { get; init; }
    public string? AppId { get; init; }
    public bool HasBadge => Badge > 0;
    public string BadgeText => Badge > 9 ? "9+" : Badge.ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Indicator), nameof(IsRunning))]
    IReadOnlyList<AppWindow> windows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Indicator))]
    bool isFocused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge), nameof(BadgeText))]
    int badge;

    public bool IsRunning => Windows.Count > 0;

    public bool Matches(AppWindow w) => match.Matches(w);

    public bool Launch()
    {
        if (Pin is not null) return Launcher.Open(Pin);
        if (AppId is { } id && Launcher.OpenAppId(id)) return true;
        return Exe is { } exe && Launcher.OpenFile(exe);
    }

    public string? RevealPath => Folder?.Path ?? (Pin is not null ? Launcher.RevealTarget(Pin) : Exe);

    public void Dispose() => Folder?.Dispose();
}

public sealed record AppMatch(HashSet<string> Ids, HashSet<string> Exes, List<string> Folders)
{
    static readonly Dictionary<string, string> KnownIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["explorer.exe"] = WindowTracker.ExplorerId,
        ["steam.exe"] = "Valve.Steam.Client"
    };

    public static AppMatch Empty() => new(new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), []);

    public static AppMatch For(PinItem pin, Func<string, string?>? steamPath = null, Func<string, string?>? epicPath = null)
    {
        var match = Empty();
        if (pin.IsRaw)
        {
            if (pin.AppId is not { Length: > 0 } raw) return match;
            if (raw.Contains('\\')) match.AddExe(TaskbarBlob.ExpandAppId(raw));
            else match.Ids.Add(raw);
        }
        else if (pin.IsUriLauncher)
        {
            var folder = pin.AppId switch
            {
                { } id when id.StartsWith("WinDock.Steam.") => (steamPath ?? GameLibraries.SteamInstallPath)(id["WinDock.Steam.".Length..]),
                { } id when id.StartsWith("WinDock.Epic.") => (epicPath ?? GameLibraries.EpicInstallPath)(id["WinDock.Epic.".Length..]),
                _ => null
            };
            if (folder is { Length: > 0 }) match.Folders.Add(folder.TrimEnd('\\') + "\\");
        }
        else if (pin.IsLauncher) match.Ids.Add(pin.AppId!);
        else if (pin.ResolvedTarget is { } target && !Directory.Exists(target))
        {
            var isLink = target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
            var exe = isLink ? ShellLinks.ReadTarget(target) : target;
            if (exe is not null) match.AddExe(exe);
            if ((pin.AppId ?? (isLink ? ShellLinks.ReadAppId(target) : null)) is { } id) match.Ids.Add(id);
        }
        return match;
    }

    void AddExe(string exe)
    {
        Exes.Add(exe);
        if (KnownIds.TryGetValue(Path.GetFileName(exe), out var id)) Ids.Add(id);
    }

    public bool Matches(AppWindow w)
    {
        if (w.AppId is { } id && Ids.Count > 0) return Ids.Contains(id);
        if (w.Exe is not { } exe) return false;
        return Exes.Contains(exe) || Folders.Any(f => exe.StartsWith(f, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed partial class SystemTile : DockTile
{
    int lastSlow = -1;

    public override string Key => "system";

    [ObservableProperty] string time = "";
    [ObservableProperty] string date = "";
    [ObservableProperty] string networkGlyph = "";
    [ObservableProperty] string batteryGlyph = "";
    [ObservableProperty] bool hasBattery;

    public string TrayTip => Loc.T("trayIcons");
    public string StatusTip => Loc.T("quickSettings");
    public string ClockTip => Loc.T("calendar");
    public string VolumeGlyph => "";

    public void Tick(DateTime now)
    {
        var culture = Loc.Instance.Culture;
        Time = now.ToString("t", culture);
        Date = now.ToString(Loc.Instance.Language == Loc.Portuguese ? "dd/MM/yyyy" : "M/d/yyyy", culture);
        if (now.Second / 5 == lastSlow) return;
        lastSlow = now.Second / 5;

        NetworkGlyph = SystemStats.Network() switch
        {
            NetworkKind.Wifi => "",
            NetworkKind.Wired => "",
            _ => ""
        };
        var (present, percent, charging) = SystemStats.Battery();
        HasBattery = present;
        BatteryGlyph = present ? BatteryGlyphFor(percent, charging) : "";
        OnPropertyChanged(nameof(TrayTip));
        OnPropertyChanged(nameof(StatusTip));
        OnPropertyChanged(nameof(ClockTip));
    }

    public static string BatteryGlyphFor(int percent, bool charging) =>
        ((char)((charging ? 0xEBAB : 0xEBA0) + Math.Clamp((int)Math.Round(percent / 10.0), 0, 10))).ToString();
}

public sealed partial class EnvDot(PresetViewModel preset) : ObservableObject
{
    public PresetViewModel Preset { get; } = preset;
    public Brush Accent => Preset.Accent;
    public string Tip => $"{Preset.Glyph}  {Preset.Name}";

    [ObservableProperty] bool isActive;

    public void RefreshText() => OnPropertyChanged(nameof(Tip));
}

public sealed class EnvTile : DockTile
{
    public override string Key => "envs";
    public ObservableCollection<EnvDot> Dots { get; } = [];

    public void Sync(IEnumerable<PresetViewModel> presets, PresetViewModel? active)
    {
        var list = presets.ToList();
        if (!Dots.Select(d => d.Preset).SequenceEqual(list))
        {
            Dots.Clear();
            foreach (var p in list) Dots.Add(new EnvDot(p));
        }
        foreach (var d in Dots)
        {
            d.IsActive = d.Preset == active;
            d.RefreshText();
        }
    }
}
