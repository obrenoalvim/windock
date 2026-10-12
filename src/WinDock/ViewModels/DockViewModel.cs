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
    readonly WindowTracker tracker = new();
    readonly Dictionary<string, DockTile> cache = [];
    readonly Dictionary<string, WidgetTile> widgets = [];
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly StartTile start = new();
    readonly SystemTile system = new();
    readonly EnvTile envs = new();
    List<AppMatch> always = [];
    string? activeId;
    int enforce;

    public DockViewModel(MainViewModel main, ShellGuard shell)
    {
        this.main = main;
        this.shell = shell;
        activeId = main.Active?.Id;
        tracker.ActiveEnvironment = activeId;
        tracker.Changed += Rebuild;
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Active)) OnActiveChanged();
        };
        main.PresetChanged += p =>
        {
            if (p == main.Always) always = [];
            if (p == main.Active || p == main.Always) Rebuild();
        };
        main.Presets.CollectionChanged += (_, _) => Rebuild();
        main.Dock.Changed += OnOption;
        Loc.Instance.PropertyChanged += (_, _) => Tick();
        tick.Tick += (_, _) => Tick();
        tick.Start();
        Rebuild();
        Tick();
    }

    public ObservableCollection<DockTile> Tiles { get; } = [];
    public MainViewModel Main => main;
    public DockOptions Options => main.Dock;
    public WindowTracker Tracker => tracker;
    public ShellGuard Shell => shell;
    public PresetViewModel? Active => main.Active;

    public event Action? Switched;
    public event Action? Layout;

    void OnOption(string name)
    {
        if (name == nameof(DockOptions.FollowWindows) && !Options.FollowWindows)
        {
            tracker.ShowAll();
            shell.SetHiddenWindows([]);
        }
        if (name == nameof(DockOptions.ReplaceTaskbar)) Rebuild();
    }

    void OnActiveChanged()
    {
        var to = main.Active?.Id;
        if (to == activeId) return;
        var from = activeId;
        activeId = to;
        if (Options.FollowWindows)
        {
            tracker.SwitchEnvironment(from, to, IsShared);
            shell.SetHiddenWindows(tracker.HiddenHandles);
        }
        else tracker.ActiveEnvironment = to;
        OnPropertyChanged(nameof(Active));
        Rebuild();
        Switched?.Invoke();
    }

    bool IsShared(AppWindow w)
    {
        if (always.Count != main.Always.Model.Items.Count) always = main.Always.Model.Items.Select(p => AppMatch.For(p)).ToList();
        return always.Any(m => m.Matches(w));
    }

    public void Rebuild()
    {
        var desired = new List<DockTile>();
        if (Options.ReplaceTaskbar) desired.Add(start);

        var pinned = new List<AppTile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in main.Always.Model.Items.Concat(main.Active?.Model.Items ?? []))
        {
            if (!pin.Exists) continue;
            var key = KeyOf(pin);
            if (!seen.Add(key)) continue;
            pinned.Add(Pinned(key, pin));
        }
        desired.AddRange(pinned);

        var assigned = pinned.ToDictionary(t => t, _ => new List<AppWindow>());
        var loose = new List<(string Key, List<AppWindow> Windows)>();
        foreach (var w in tracker.Windows)
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

        var running = loose.Select(g => (AppTile)Get(g.Key, () => RunningTile(g.Key, g.Windows[0]))).ToList();
        if (running.Count > 0) desired.Add(Get("sep:run", () => new SeparatorTile("sep:run")));
        desired.AddRange(running);

        var foreground = tracker.Foreground;
        foreach (var (tile, list) in assigned.Select(p => (p.Key, p.Value)).Concat(running.Select((t, i) => (t, loose[i].Windows))))
        {
            if (!tile.Windows.SequenceEqual(list)) tile.Windows = list;
            tile.IsFocused = list.Any(w => w.Handle == foreground);
        }

        var env = main.Active;
        var specs = env?.Model.ShownWidgets ?? [new WidgetSpec()];
        if (specs.Count > 0) desired.Add(Get("sep:widgets", () => new SeparatorTile("sep:widgets")));
        for (var i = 0; i < specs.Count; i++) desired.Add(Widget(env?.Id ?? "", i, specs[i]));

        envs.Sync(main.Presets, env);
        if (main.Presets.Count > 1)
        {
            desired.Add(Get("sep:envs", () => new SeparatorTile("sep:envs")));
            desired.Add(envs);
        }
        if (Options.ReplaceTaskbar)
        {
            desired.Add(Get("sep:system", () => new SeparatorTile("sep:system")));
            desired.Add(system);
        }

        Sync(desired);
    }

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
            w.Tick(DateTime.Now);
        }
        return w;
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
        foreach (var w in widgets.Values) w.Tick(now);
        if (Options.ReplaceTaskbar) system.Tick(now);
        if (shell.TaskbarHidden && ++enforce % 5 == 0) shell.Enforce();
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
        var target = everywhere ? main.Always : main.Active;
        if (target is not null) main.AddPrograms(target, [item]);
    }

    public void Unpin(AppTile tile, bool everywhere)
    {
        var target = everywhere ? main.Always : main.Active;
        if (target?.Pins.FirstOrDefault(p => KeyOf(p.Model) == tile.Key) is { } pin) pin.RemoveCommand.Execute(null);
    }

    public bool IsAlwaysPinned(AppTile tile) => main.Always.Model.Items.Any(p => KeyOf(p) == tile.Key);

    public void Dispose()
    {
        tick.Stop();
        tracker.Dispose();
        foreach (var d in cache.Values.OfType<IDisposable>()) d.Dispose();
    }
}
