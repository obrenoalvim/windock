using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed partial class DockViewModel : ObservableObject, IDisposable
{
    public const int IconPixels = 128;

    readonly MainViewModel main;
    readonly ShellGuard shell;
    readonly WindowTracker tracker;
    // The main dock owns the tracker and the shell; the dock of another screen (with "each screen its own dock") only shows.
    readonly bool owner;
    readonly string? screen;
    readonly Dictionary<string, DockTile> cache = [];
    readonly Dictionary<string, WidgetTile> widgets = [];
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly DispatcherTimer persist = new() { Interval = TimeSpan.FromMilliseconds(700) };
    // Grace before the music tile hides, so a track change does not make it blink.
    readonly DispatcherTimer musicGrace = new() { Interval = TimeSpan.FromSeconds(3) };
    readonly HashSet<PresetViewModel> dirty = [];
    readonly StartTile start = new();
    readonly SystemTile system = new();
    List<AppMatch> always = [];
    string? activeId;
    string? onlyScreen;
    int enforce;
    bool taskbarAutoHides = ShellGuard.TaskbarAutoHides();
    DateTime nextTaskbarCheck;

    public DockViewModel(MainViewModel main, ShellGuard shell, string? screen = null, WindowTracker? shared = null)
    {
        this.main = main;
        this.shell = shell;
        this.screen = screen;
        owner = shared is null;
        tracker = shared ?? new WindowTracker();
        onlyScreen = screen;
        activeId = Active?.Id;
        if (owner) tracker.ActiveEnvironment = activeId;
        tracker.Changed += Rebuild;
        tracker.Reshaped += OnReshaped;
        main.PropertyChanged += OnMain;
        main.PresetChanged += OnPreset;
        main.Presets.CollectionChanged += OnPresets;
        main.Dock.Changed += OnOption;
        Loc.Instance.PropertyChanged += OnLanguage;
        tick.Tick += (_, _) => Tick();
        tick.Start();
        persist.Tick += (_, _) => Persist();
        musicGrace.Tick += (_, _) =>
        {
            musicGrace.Stop();
            Rebuild();
        };
        MediaSessions.Shared.Changed += OnMedia;
        Badges.Changed += Rebuild;
        Badges.Start();
        Rebuild();
        Tick();
    }

    public ObservableCollection<DockTile> Tiles { get; } = [];
    public MainViewModel Main => main;
    public DockOptions Options => main.Dock;
    public WindowTracker Tracker => tracker;
    public ShellGuard Shell => shell;
    public SystemTile System => system;
    public bool SystemVisible => Options.ReplaceTaskbar || taskbarAutoHides;

    // The dock of another screen can sit on an environment of its own; otherwise every dock follows the main one.
    public PresetViewModel? Active =>
        screen is not null && Options.ScreenEnvironments.TryGetValue(screen, out var id) && main.Presets.FirstOrDefault(p => p.Id == id) is { } own
            ? own
            : main.Active;

    // With "each screen its own dock", the main dock lists only the windows on its own screen.
    public string? OnlyScreen
    {
        get => onlyScreen;
        set
        {
            if (onlyScreen == value) return;
            onlyScreen = value;
            Rebuild();
        }
    }

    public event Action? Switched;
    public event Action? Layout;

    void OnMain(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Active)) OnActiveChanged();
    }

    void OnPreset(PresetViewModel p)
    {
        if (p == main.Always) always = [];
        if (p == Active || p == main.Always) Rebuild();
    }

    void OnPresets(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild();

    void OnLanguage(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Tick();

    void OnReshaped()
    {
        if (onlyScreen is not null) Rebuild();
    }

    void OnOption(string name)
    {
        if (owner && name == nameof(DockOptions.FollowWindows) && !Options.FollowWindows)
        {
            tracker.ShowAll();
            shell.SetHiddenWindows([]);
        }
        if (name == nameof(DockOptions.ReplaceTaskbar)) Rebuild();
    }

    void OnActiveChanged()
    {
        if (owner && main.Active?.Id is var mainId && mainId != tracker.ActiveEnvironment)
        {
            if (Options.FollowWindows) tracker.SwitchEnvironment(tracker.ActiveEnvironment, mainId, IsShared);
            else tracker.ActiveEnvironment = mainId;
            if (Options.FollowWindows) shell.SetHiddenWindows(tracker.HiddenHandles);
        }
        Followed();
    }

    // Rebuilds and shows the name when the environment of this dock really changed.
    void Followed()
    {
        var to = Active?.Id;
        if (to == activeId) return;
        activeId = to;
        OnPropertyChanged(nameof(Active));
        Rebuild();
        Switched?.Invoke();
    }

    // The wheel and the dock menu: the main dock switches the environment, the dock of another screen only its own.
    public async Task SwitchByAsync(int delta)
    {
        if (screen is null)
        {
            await main.SwitchByAsync(delta);
            return;
        }
        if (main.Presets.Count == 0) return;
        var index = Math.Max(0, main.Presets.IndexOf(Active!));
        Pick(main.Presets[((index + delta) % main.Presets.Count + main.Presets.Count) % main.Presets.Count]);
    }

    public void Pick(PresetViewModel preset)
    {
        if (screen is null)
        {
            _ = main.ApplyAsync(preset, auto: false);
            return;
        }
        Options.SetScreenEnvironment(screen, preset.Id);
        Followed();
    }

    void OnMedia()
    {
        if (MediaSessions.Shared.Current is not null) Rebuild();
        else
        {
            musicGrace.Stop();
            musicGrace.Start();
        }
    }

    public void RefreshMic()
    {
        foreach (var mic in widgets.Values.OfType<MicWidget>()) mic.Refresh(DateTime.Now);
    }

    bool MusicHidden => MediaSessions.Shared.Current is null && !musicGrace.IsEnabled;

    bool IsShared(AppWindow w)
    {
        if (always.Count != main.Always.Model.Items.Count) always = main.Always.Model.Items.Select(p => AppMatch.For(p)).ToList();
        return always.Any(m => m.Matches(w));
    }

    IEnumerable<AppWindow> WindowsHere()
    {
        if (onlyScreen is null || global::System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == onlyScreen) is not { } target)
            return tracker.Windows;
        var b = target.Bounds;
        var monitor = Native.MonitorFromPoint(new Native.Point { X = b.Left + b.Width / 2, Y = b.Top + b.Height / 2 }, 2);
        return tracker.Windows.Where(w => Native.MonitorFromWindow(w.Handle, 2) == monitor);
    }

    public void Rebuild()
    {
        var desired = new List<DockTile>();
        if (SystemVisible) desired.Add(start);

        var pinned = new List<AppTile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in main.Always.Model.Items.Concat(Active?.Model.Items ?? []))
        {
            if (!pin.Exists) continue;
            var key = KeyOf(pin);
            if (!seen.Add(key)) continue;
            pinned.Add(Pinned(key, pin));
        }
        desired.AddRange(pinned);

        var assigned = pinned.ToDictionary(t => t, _ => new List<AppWindow>());
        var loose = new List<(string Key, List<AppWindow> Windows)>();
        foreach (var w in WindowsHere())
        {
            if (pinned.FirstOrDefault(t => t.Matches(w)) is { } owner)
            {
                assigned[owner].Add(w);
                continue;
            }
            var key = "run:" + (w.AppId ?? w.Exe ?? w.Handle.ToString()).ToLowerInvariant();
            if (loose.FirstOrDefault(g => g.Key == key) is { Windows: { } group }) group.Add(w);
            else loose.Add((key, [w]));
        }

        StackAlike(loose);
        var running = loose.Select(g => (AppTile)Get(g.Key, () => RunningTile(g.Key, g.Windows[0]))).ToList();
        if (running.Count > 0) desired.Add(Get("sep:run", () => new SeparatorTile("sep:run")));
        desired.AddRange(running);

        var foreground = tracker.Foreground;
        foreach (var (tile, list) in assigned.Select(p => (p.Key, p.Value)).Concat(running.Select((t, i) => (t, loose[i].Windows))))
        {
            if (!tile.Windows.SequenceEqual(list)) tile.Windows = list;
            tile.IsFocused = list.Any(w => w.Handle == foreground);
            if (!tile.IsFolder) tile.Badge = BadgeOf(tile);
        }

        var env = Active;
        // Music hides while nothing plays; its tile is still made so it keeps listening.
        var shown = VisibleWidgets(env?.Model.ShownWidgets ?? [new WidgetSpec()], SystemVisible)
            .Select(w => Widget(env?.Id ?? "", w.Index, w.Spec))
            .Where(w => !(w is MusicWidget && MusicHidden))
            .ToList();
        if (shown.Count > 0) desired.Add(Get("sep:widgets", () => new SeparatorTile("sep:widgets")));
        desired.AddRange(shown);

        if (SystemVisible)
        {
            // Start stays at the left end like on Windows; tray, quick settings and clock sit together at the right.
            desired.Add(Get("sep:system", () => new SeparatorTile("sep:system")));
            desired.Add(system);
        }

        Sync(desired);
    }

    // The Windows badge (WhatsApp, Teams) or a "(3) ..." count in a window title, whichever is higher.
    static int BadgeOf(AppTile tile)
    {
        var stored = Badges.For(tile.BadgeIds);
        var titled = tile.Windows.Select(w => Badges.FromTitle(w.Title)).DefaultIfEmpty(0).Max();
        return Math.Max(stored, titled) is var count and > 0 ? count : stored;
    }

    // One program split over several exes with the same icon (Google Play Games: client.exe and crosvm.exe) takes one slot.
    static void StackAlike(List<(string Key, List<AppWindow> Windows)> groups)
    {
        for (var i = groups.Count - 1; i > 0; i--)
        {
            var w = groups[i].Windows[0];
            if (w.Exe is not { } exe || IsPackaged(w)) continue;
            for (var j = 0; j < i; j++)
            {
                var other = groups[j].Windows[0];
                if (other.Exe is not { } otherExe || IsPackaged(other) || exe.Equals(otherExe, StringComparison.OrdinalIgnoreCase)) continue;
                if (!AppMatch.SameProgramFolder(exe, otherExe) || ShellIcons.Fingerprint(exe) is not { } print || print != ShellIcons.Fingerprint(otherExe)) continue;
                groups[j].Windows.AddRange(groups[i].Windows);
                groups.RemoveAt(i);
                break;
            }
        }
    }

    static bool IsPackaged(AppWindow w) => w.AppId?.Contains('!') == true;

    public static List<(WidgetSpec Spec, int Index)> VisibleWidgets(IReadOnlyList<WidgetSpec> specs, bool systemClock) =>
        specs.Select((spec, index) => (spec, index)).Where(w => !(systemClock && w.spec.Kind == WidgetSpec.Clock)).ToList();

    void Sync(List<DockTile> desired)
    {
        var changed = false;
        for (var i = 0; i < desired.Count; i++)
        {
            if (i < Tiles.Count && ReferenceEquals(Tiles[i], desired[i])) continue;
            var j = -1;
            for (var k = i + 1; k < Tiles.Count; k++)
                if (ReferenceEquals(Tiles[k], desired[i])) { j = k; break; }
            if (j >= 0) Tiles.Move(j, i);
            else Tiles.Insert(i, desired[i]);
            changed = true;
        }
        while (Tiles.Count > desired.Count)
        {
            Tiles.RemoveAt(Tiles.Count - 1);
            changed = true;
        }

        var keep = desired.Select(t => t.Key).ToHashSet();
        var live = main.Always.Model.Items.Concat(main.Presets.SelectMany(p => p.Model.Items)).ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var gone in cache.Where(p => !keep.Contains(p.Key) && (p.Key.StartsWith("run:") || p.Value is AppTile { Pin: { } pin } && !live.Contains(pin))).ToList())
        {
            cache.Remove(gone.Key);
            (gone.Value as IDisposable)?.Dispose();
        }
        if (changed) Layout?.Invoke();
    }

    DockTile Get(string key, Func<DockTile> make)
    {
        if (!cache.TryGetValue(key, out var tile)) cache[key] = tile = make();
        return tile;
    }

    AppTile Pinned(string key, PinItem pin)
    {
        if (cache.TryGetValue(key, out var hit) && hit is AppTile { Pin: var known } tile && ReferenceEquals(known, pin)) return tile;
        (hit as IDisposable)?.Dispose();
        var fresh = PinTile(key, pin);
        cache[key] = fresh;
        return fresh;
    }

    WidgetTile Widget(string env, int index, WidgetSpec spec)
    {
        var key = $"widget:{env}:{index}:{spec.Kind}";
        if (widgets.TryGetValue(key, out var w)) w.Apply(spec);
        else
        {
            widgets[key] = w = WidgetTile.Create(key, spec);
            w.Alert += Alert;
            w.Edited += OnEdited;
            w.Tick(DateTime.Now);
            w.RefreshFace();
        }
        return w;
    }

    void OnEdited(WidgetTile widget)
    {
        if (main.Presets.FirstOrDefault(p => p.Model.Widgets?.Contains(widget.Spec) == true) is not { } owner) return;
        dirty.Add(owner);
        persist.Stop();
        persist.Start();
    }

    void Persist()
    {
        persist.Stop();
        foreach (var p in dirty) main.SaveQuietly(p);
        dirty.Clear();
    }

    void Alert(string text)
    {
        SystemSounds.Exclamation.Play();
        main.Notify(text);
    }

    AppTile PinTile(string key, PinItem pin)
    {
        var folder = pin.IsFolder ? new FolderStack(pin.ResolvedTarget!, main.Settings) : null;
        return new AppTile(key, pin.Name, ShellIcons.ForPin(pin, IconPixels), pin, AppMatch.For(pin), folder);
    }

    static AppTile RunningTile(string key, AppWindow w)
    {
        var packaged = w.AppId is { } id && id.Contains('!');
        var icon = (packaged ? ShellIcons.ForAppId(w.AppId!, IconPixels) : null)
                   ?? (w.Exe is { } exe && !exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) ? ShellIcons.ForPath(exe, IconPixels) : null);
        var name = (packaged ? ShellIcons.NameForAppId(w.AppId!) : null) ?? Describe(w.Exe) ?? w.Title;
        var match = AppMatch.Empty();
        if (w.AppId is { } appId) match.Ids.Add(appId);
        if (w.Exe is { } path) match.Exes.Add(path);
        return new AppTile(key, name, icon, null, match) { Exe = w.Exe, AppId = w.AppId };
    }

    static string? Describe(string? exe)
    {
        if (exe is null || !File.Exists(exe)) return null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            return string.IsNullOrWhiteSpace(info.FileDescription) ? Path.GetFileNameWithoutExtension(exe) : info.FileDescription.Trim();
        }
        catch (FileNotFoundException) { return Path.GetFileNameWithoutExtension(exe); }
    }

    static string KeyOf(PinItem pin) =>
        "pin:" + (pin.IsRaw || pin.IsLauncher ? pin.AppId ?? pin.Name : pin.ResolvedTarget ?? pin.Name).ToLowerInvariant();

    void Tick()
    {
        var now = DateTime.Now;
        // Only widgets on the dock refresh (Claude, weather, activity and network cost work); hidden timers still ring.
        var shown = Options.Enabled ? Tiles.OfType<WidgetTile>().ToHashSet() : [];
        foreach (var w in widgets.Values.ToList())
            if (w.KeepsTime || shown.Contains(w)) w.Tick(now);
        foreach (var w in shown) w.RefreshFace();
        if (SystemVisible) system.Tick(now);
        if (!owner) return;
        if (shell.TaskbarHidden && ++enforce % 5 == 0) shell.Enforce();
        if (now < nextTaskbarCheck) return;
        nextTaskbarCheck = now.AddSeconds(2);
        var hides = ShellGuard.TaskbarAutoHides();
        if (hides == taskbarAutoHides) return;
        taskbarAutoHides = hides;
        Rebuild();
    }

    public void Pin(AppTile tile, bool everywhere)
    {
        var item = tile.AppId is { } id && id.Contains('!')
            ? new PinItem
            {
                Name = tile.Name,
                Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                Args = @"shell:AppsFolder\" + id,
                AppId = id,
                Icon = IconFiles.FromParsingName("store-" + id, @"shell:AppsFolder\" + id)
            }
            : tile.Exe is { } exe ? new PinItem { Name = tile.Name, Target = exe } : null;
        if (item is null) return;
        var target = everywhere ? main.Always : Active;
        if (target is not null) main.AddPrograms(target, [item]);
    }

    public void Unpin(AppTile tile, bool everywhere)
    {
        var target = everywhere ? main.Always : Active;
        if (target?.Pins.FirstOrDefault(p => KeyOf(p.Model) == tile.Key) is { } pin) pin.RemoveCommand.Execute(null);
    }

    public bool IsAlwaysPinned(AppTile tile) => main.Always.Model.Items.Any(p => KeyOf(p) == tile.Key);

    public void Dispose()
    {
        Persist();
        tick.Stop();
        musicGrace.Stop();
        MediaSessions.Shared.Changed -= OnMedia;
        Badges.Changed -= Rebuild;
        tracker.Changed -= Rebuild;
        tracker.Reshaped -= OnReshaped;
        main.PropertyChanged -= OnMain;
        main.PresetChanged -= OnPreset;
        main.Presets.CollectionChanged -= OnPresets;
        main.Dock.Changed -= OnOption;
        Loc.Instance.PropertyChanged -= OnLanguage;
        if (owner) tracker.Dispose();
        foreach (var d in cache.Values.OfType<IDisposable>()) d.Dispose();
    }
}
