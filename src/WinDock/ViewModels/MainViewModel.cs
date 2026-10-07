using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    static readonly CultureInfo Pt = new("pt-BR");

    readonly PresetStore store;
    readonly IconService icons;
    readonly SystemStats stats = new();
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer scan = new() { Interval = TimeSpan.FromSeconds(3) };
    string? lastScheduledId;
    bool scheduleStarted;
    int lastMinute = -1;

    public MainViewModel(PresetStore store, Settings settings, IconService icons)
    {
        this.store = store;
        this.icons = icons;
        Settings = settings;
        tick.Tick += (_, _) => Tick();
        scan.Tick += (_, _) => Scan();
    }

    public Settings Settings { get; }
    public PresetStore Store => store;
    public ObservableCollection<PresetViewModel> Presets { get; } = [];
    public ObservableCollection<DockItemViewModel> Items { get; } = [];
    public bool IsEmpty => Items.Count == 0;
    public SolidColorBrush Accent { get; } = new(Color.FromRgb(0xFF, 0xB5, 0x47));
    public SolidColorBrush AccentSoft { get; } = new(Color.FromArgb(0x2A, 0xFF, 0xB5, 0x47));
    public SolidColorBrush AccentLine { get; } = new(Color.FromArgb(0x66, 0xFF, 0xB5, 0x47));

    public event Action<string>? Notice;
    public event Action? ItemsRebuilt;

    [ObservableProperty] PresetViewModel? active;
    [ObservableProperty] string time = "";
    [ObservableProperty] string date = "";
    [ObservableProperty] string nextText = "";
    [ObservableProperty] double cpu;
    [ObservableProperty] double ram;
    [ObservableProperty] bool showClock;
    [ObservableProperty] bool showCpu;
    [ObservableProperty] bool showRam;
    [ObservableProperty] bool showNext;

    public string CpuText => $"{Cpu:0}%";
    public string RamText => $"{Ram:0}%";

    public void Start()
    {
        Reload();
        tick.Start();
        scan.Start();
        Scan();
    }

    public void Reload()
    {
        var keep = Active?.Id;
        var loaded = store.Load();
        Presets.Clear();
        for (var i = 0; i < loaded.Count; i++) Presets.Add(new PresetViewModel(loaded[i], i + 1));
        lastScheduledId = null;
        scheduleStarted = false;
        var target = Presets.FirstOrDefault(p => p.Id == keep) ?? Presets.FirstOrDefault();
        if (target is not null) Activate(target, byUser: true);
        lastMinute = -1;
        Tick();
    }

    [RelayCommand]
    void Select(PresetViewModel preset) => Activate(preset, byUser: true);

    public void SelectIndex(int index)
    {
        if (index >= 0 && index < Presets.Count) Activate(Presets[index], byUser: true);
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        if (Active is null) return;
        foreach (var path in paths)
        {
            Active.Model.Items.Add(new DockItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Target = path
            });
        }
        store.Save(Active.Model);
        Activate(Active, byUser: true);
    }

    void Activate(PresetViewModel preset, bool byUser)
    {
        foreach (var p in Presets) p.IsActive = p == preset;
        Active = preset;

        Items.Clear();
        foreach (var item in preset.Model.Items.Where(i => Target.IsAvailable(i.Target) && (i.Icon is null || Target.ResolvePath(i.Icon) is not null)))
        {
            var captured = item;
            Items.Add(new DockItemViewModel(
                captured,
                icons.Get(captured),
                () => Launch(captured),
                () => Remove(preset, captured)));
        }

        var widgets = preset.Model.Widgets;
        ShowClock = widgets.Contains("clock");
        ShowCpu = widgets.Contains("cpu");
        ShowRam = widgets.Contains("ram");
        ShowNext = widgets.Contains("next");

        OnPropertyChanged(nameof(IsEmpty));
        ItemsRebuilt?.Invoke();
        RefreshNext(DateTime.Now);
        Scan();

        if (byUser) return;
        if (preset.Model.LaunchOnActivate)
            foreach (var item in preset.Model.Items) Launch(item);
        if (Settings.Notify) Notice?.Invoke($"{preset.Glyph}  {preset.Model.Name}");
    }

    void Launch(DockItem item)
    {
        if (!Target.Launch(item.Target, item.Args)) Notice?.Invoke($"Não foi possível abrir {item.Name}");
    }

    void Remove(PresetViewModel preset, DockItem item)
    {
        preset.Model.Items.Remove(item);
        store.Save(preset.Model);
        Activate(preset, byUser: true);
    }

    void Tick()
    {
        var now = DateTime.Now;
        Time = now.ToString("HH:mm", Pt);
        Date = $"{now.ToString("ddd", Pt).TrimEnd('.').ToUpperInvariant()} · {now:dd} {now.ToString("MMM", Pt).TrimEnd('.').ToUpperInvariant()}";
        Cpu = stats.Cpu();
        Ram = stats.Ram();
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(RamText));

        if (now.Minute != lastMinute)
        {
            lastMinute = now.Minute;
            RefreshNext(now);
        }

        var scheduled = ScheduleResolver.At(Presets.Select(p => p.Model), now);
        if (scheduled?.Id == lastScheduledId && scheduleStarted) return;
        var first = !scheduleStarted;
        scheduleStarted = true;
        lastScheduledId = scheduled?.Id;
        if (!Settings.AutoSwitch || scheduled is null) return;
        var target = Presets.First(p => p.Model == scheduled);
        if (target != Active || !first) Activate(target, byUser: first);
    }

    void RefreshNext(DateTime now)
    {
        var next = ScheduleResolver.Next(Presets.Select(p => p.Model).ToList(), now);
        NextText = next is { } n
            ? $"{n.At:HH:mm} → {n.To?.Name ?? "livre"}"
            : "sem agenda";
    }

    void Scan()
    {
        var all = Process.GetProcesses();
        var names = all.Select(p => p.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in all) p.Dispose();
        foreach (var item in Items)
            item.IsRunning = item.ProcessName is { } n && names.Contains(n);
    }
}
