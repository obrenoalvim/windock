using System.Windows.Media;
using Microsoft.Win32;
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
    public bool HasBadge => Badge != 0;
    public string BadgeText => Badge < 0 ? "" : Badge > 99 ? "99+" : Badge.ToString();

    // Every app id this tile stands for, to find its unread badge.
    public IEnumerable<string?> BadgeIds => match.Ids.Append(AppId).Append(Pin?.AppId).Concat(Windows.Select(w => w.AppId));

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

    public bool Hosts(AppWindow w) => match.Hosts(w);

    public bool Launch()
    {
        if (Pin is not null) return Launcher.Open(Pin);
        if (AppId is { } id && Launcher.OpenAppId(id)) return true;
        return Exe is { } exe && Launcher.OpenFile(exe);
    }

    public string? RevealPath => Folder?.Path ?? (Pin is not null ? Launcher.RevealTarget(Pin) : Exe);

    (string Exe, string? Args)? opener;
    bool openerKnown;

    // The exe (and the shortcut's own arguments) that opens files dropped on this icon, or null when it takes none.
    public (string Exe, string? Args)? Opener()
    {
        if (!openerKnown)
        {
            opener = FindOpener();
            openerKnown = true;
        }
        return opener;
    }

    (string Exe, string? Args)? FindOpener()
    {
        if (IsFolder) return null;
        if (Pin is null)
            return Exe is { } exe && AppId?.Contains('!') != true && !exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) ? (exe, null) : null;
        if (Pin.IsLauncher || Pin.IsRaw || Pin.IsUriLauncher || Pin.ResolvedTarget is not { } target) return null;
        if (!target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? (target, null) : null;
        if (ShellLinks.ReadTarget(target) is not { } linked || !linked.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        var args = ShellLinks.ReadArguments(target);
        // ponytail: Squirrel stubs (Discord) would read the files as their own switches, so they take none.
        return AppMatch.UpdaterFolder(linked, args) is null ? (linked, args) : null;
    }

    public void Dispose() => Folder?.Dispose();
}

public sealed record AppMatch(HashSet<string> Ids, HashSet<string> Exes, List<string> Folders)
{
    static readonly Dictionary<string, string> KnownIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["explorer.exe"] = WindowTracker.ExplorerId,
        ["steam.exe"] = "Valve.Steam.Client"
    };

    // A shortcut to a link runs inside the program that opens the link: its windows titled like the shortcut are this pin.
    public string? Host { get; init; }
    public string? Title { get; init; }

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
            if (exe is not null && UpdaterFolder(exe, isLink ? ShellLinks.ReadArguments(target) : pin.Args) is { } updater) match.Folders.Add(updater);
            if (exe is null && isLink && ShellLinks.ReadLink(target) is { } link && LinkHandlerFolder(link) is { } host)
                match = match with { Host = host, Title = Path.GetFileNameWithoutExtension(target) };
            if ((pin.AppId ?? (isLink ? ShellLinks.ReadAppId(target) : null)) is { } id) match.Ids.Add(id);
        }
        return match;
    }

    // Squirrel apps (Discord, Slack, Figma...) pin a stub with "--processStart X.exe" and run X from app-1.2.3\.
    public static string? UpdaterFolder(string exe, string? args) =>
        args?.Contains("--processStart", StringComparison.OrdinalIgnoreCase) == true && Path.GetDirectoryName(exe) is { Length: > 3 } dir
            ? dir.TrimEnd('\\') + "\\"
            : null;

    // Google Play Games: googleplaygames://launch/?id=... opens in C:\Program Files\Google\Play Games\.
    public static string? LinkHandlerFolder(string link)
    {
        var colon = link.IndexOf(':');
        if (colon < 2) return null;
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(link[..colon] + @"\shell\open\command");
            if (key?.GetValue(null) is not string command) return null;
            var exe = command.StartsWith('"') ? command.Split('"')[1] : command.Split(' ')[0];
            return Path.GetDirectoryName(exe) is { Length: > 3 } dir && !IsSharedRoot(dir) ? dir.TrimEnd('\\') + "\\" : null;
        }
        catch (Exception e) when (e is ArgumentException or System.Security.SecurityException or IOException) { return null; }
    }

    static readonly string[] SharedRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps")
    ];

    // Two exes of one program live under a folder of their own (not Program Files or AppData itself).
    public static bool SameProgramFolder(string a, string b)
    {
        var pa = Path.GetDirectoryName(a)?.Split('\\') ?? [];
        var pb = Path.GetDirectoryName(b)?.Split('\\') ?? [];
        var n = 0;
        while (n < pa.Length && n < pb.Length && pa[n].Equals(pb[n], StringComparison.OrdinalIgnoreCase)) n++;
        if (n < 2) return false;
        return !IsSharedRoot(string.Join('\\', pa.Take(n)));
    }

    static bool IsSharedRoot(string dir) =>
        SharedRoots.Any(r => r.Length > 0 && dir.TrimEnd('\\').Equals(r.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));

    void AddExe(string exe)
    {
        Exes.Add(exe);
        if (KnownIds.TryGetValue(Path.GetFileName(exe), out var id)) Ids.Add(id);
    }

    public bool Matches(AppWindow w)
    {
        if (Title is { } name) return Hosts(w) && w.Title.StartsWith(name, StringComparison.OrdinalIgnoreCase);
        if (w.AppId is { } id && Ids.Count > 0) return Ids.Contains(id);
        if (w.Exe is not { } exe) return false;
        return Exes.Contains(exe) || Folders.Any(f => exe.StartsWith(f, StringComparison.OrdinalIgnoreCase));
    }

    public bool Hosts(AppWindow w) => Host is { } host && w.Exe?.StartsWith(host, StringComparison.OrdinalIgnoreCase) == true;
}

public sealed partial class SystemTile : DockTile
{
    int volumeTick;
    DateTime nextNetwork, nextBattery;
    volatile bool networkChanged = true;
    NetworkKind kind;

    public SystemTile()
    {
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += (_, _) => networkChanged = true;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (_, _) => networkChanged = true;
    }

    public override string Key => "system";

    [ObservableProperty] string time = "";
    [ObservableProperty] string date = "";
    [ObservableProperty] string clockTip = "";
    [ObservableProperty] string networkGlyph = "\uE839";
    [ObservableProperty] string networkTip = "";
    [ObservableProperty] string volumeGlyph = "\uE767";
    [ObservableProperty] string volumeTip = "";
    [ObservableProperty] string batteryGlyph = "";
    [ObservableProperty] string batteryTip = "";
    [ObservableProperty] bool hasBattery;
    [ObservableProperty] string micGlyph = "";
    [ObservableProperty] string micTip = "";
    [ObservableProperty] bool hasMic;
    [ObservableProperty] bool micMuted;

    public string TrayTip => Loc.T("trayIcons");
    public string DesktopTip => Loc.T("showDesktop");

    public void Tick(DateTime now)
    {
        var culture = Loc.Instance.Culture;
        var pt = Loc.Instance.Language == Loc.Portuguese;
        Time = now.ToString("t", culture);
        Date = now.ToString(pt ? "dd/MM/yyyy" : "M/d/yyyy", culture);
        ClockTip = $"{now.ToString(pt ? "dddd, d 'de' MMMM 'de' yyyy" : "dddd, MMMM d, yyyy", culture)} · {Loc.T("calendar")}";

        if (++volumeTick % 5 == 0) RefreshVolume();
        if (networkChanged || now >= nextNetwork)
        {
            networkChanged = false;
            var net = NetworkStatus.Read();
            kind = net.Kind;
            nextNetwork = now.AddSeconds(kind == NetworkKind.Wifi ? 10 : 60);
            NetworkGlyph = NetworkStatus.Glyph(net);
            NetworkTip = NetworkText(net);
            OnPropertyChanged(nameof(TrayTip));
            OnPropertyChanged(nameof(DesktopTip));
        }
        if (now >= nextBattery)
        {
            nextBattery = now.AddSeconds(10);
            var battery = SystemStats.Battery();
            HasBattery = battery.Present;
            BatteryGlyph = battery.Present ? BatteryGlyphFor(battery.Percent, battery.Charging) : "";
            BatteryTip = DescribeBattery(battery);
        }
    }

    public void RefreshVolume()
    {
        var audio = Audio.Read();
        VolumeGlyph = Audio.Glyph(audio);
        VolumeTip = audio is null ? Loc.T("noAudio")
            : $"{audio.Device ?? Loc.T("sound")} · {(audio.Muted ? Loc.T("muted") : $"{Math.Round(audio.Volume * 100)}%")}";
        var mic = Audio.ReadMic();
        HasMic = mic is not null;
        MicMuted = mic?.Muted ?? false;
        MicGlyph = Audio.MicGlyph(mic);
        MicTip = mic is null ? "" : $"{mic.Device ?? Loc.T("microphone")} · {Loc.T(mic.Muted ? "micMuted" : "micOn")}";
    }

    public static string NetworkText(NetworkState net) => net switch
    {
        { Kind: NetworkKind.None } => Loc.T("netNone"),
        { Internet: false } => Loc.F("netNoInternet", net.Ssid ?? (net.Kind == NetworkKind.Wifi ? "Wi-Fi" : "Ethernet")),
        { Kind: NetworkKind.Wifi } => Loc.F("netWifi", net.Ssid ?? "Wi-Fi", net.Signal),
        _ => Loc.T("netWired")
    };

    public static string DescribeBattery(BatteryState battery)
    {
        if (!battery.Present) return Loc.T("plugged");
        if (battery.Charging) return Loc.F("batteryCharging", battery.Percent);
        return battery.Left is { } left
            ? Loc.F("batteryLeft", battery.Percent, (int)left.TotalHours, left.Minutes)
            : Loc.F("batteryLevel", battery.Percent);
    }

    public static string BatteryGlyphFor(int percent, bool charging) =>
        ((char)((charging ? 0xEBAB : 0xEBA0) + Math.Clamp((int)Math.Round(percent / 10.0), 0, 10))).ToString();
}
