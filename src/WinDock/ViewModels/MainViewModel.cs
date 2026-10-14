using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    static readonly string[] Palette = ["#FFB547", "#7C5CFF", "#3DDC97", "#FF6B8A", "#3B9EFF", "#FF8A3D"];

    readonly PresetStore store;
    readonly TaskbarService taskbar;
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(5) };
    (PresetViewModel Preset, bool Auto)? pending;
    string? lastScheduledId;
    bool scheduleStarted;
    int lastMinute = -1;

    public MainViewModel(PresetStore store, TaskbarService taskbar, Settings settings)
    {
        this.store = store;
        this.taskbar = taskbar;
        Settings = settings;
        Dock = new DockOptions(settings);
        Always = new PresetViewModel(new Preset { Id = "always", Name = "Always pinned", Items = settings.AlwaysPinned, Widgets = [] }, 0, Save);
        tick.Tick += (_, _) => Tick();
    }

    public Settings Settings { get; }
    public DockOptions Dock { get; }
    public PresetStore Store => store;
    public ObservableCollection<PresetViewModel> Presets { get; } = [];
    public PresetViewModel Always { get; }

    public event Action<string>? Notice;
    public event Action<PresetViewModel>? PresetChanged;

    [ObservableProperty] string status = "";
    [ObservableProperty] string nextText = "";
    [ObservableProperty] PresetViewModel? active;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    bool isBusy;

    public bool IsIdle => !IsBusy;

    public void Notify(string text) => Notice?.Invoke(text);

    public void SaveQuietly(PresetViewModel p) => store.Save(p.Model);

    public bool AutoSwitch
    {
        get => Settings.AutoSwitch;
        set { Settings.AutoSwitch = value; Settings.Save(); OnPropertyChanged(); }
    }

    public bool NotifySwitch
    {
        get => Settings.Notify;
        set { Settings.Notify = value; Settings.Save(); OnPropertyChanged(); }
    }

    public void Start()
    {
        Reload();
        tick.Start();
        Tick();
        updates.Tick += async (_, _) => await CheckUpdatesAsync();
        updates.Start();
        _ = FirstUpdateCheckAsync();
    }

    readonly System.Windows.Threading.DispatcherTimer updates = new() { Interval = TimeSpan.FromHours(6) };

    async Task FirstUpdateCheckAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(20));
        await CheckUpdatesAsync();
    }

    public async Task CheckUpdatesAsync()
    {
        if (!Settings.CheckUpdates || await Updates.LatestAsync() is not { } latest) return;
        if (latest <= Updates.Current)
        {
            Dock.NewVersion = null;
            return;
        }
        var text = latest.ToString(3);
        Dock.NewVersion = text;
        if (Settings.UpdateSeen == text) return;
        Settings.UpdateSeen = text;
        Settings.Save();
        Notify(Loc.F("updateAvailable", text));
    }

    public void Reload()
    {
        Presets.Clear();
        var loaded = store.Load();
        for (var i = 0; i < loaded.Count; i++) Presets.Add(new PresetViewModel(loaded[i], i + 1, Save));
        if (store.Seeded) AdoptCurrentTaskbar();
        foreach (var p in Presets) p.RemoveDuplicates();
        Always.RemoveDuplicates();
        if (!Settings.SyncTaskbar && Presets.All(p => p.Id != Settings.ActiveId))
            Settings.ActiveId = (ScheduledNow() ?? Presets.FirstOrDefault())?.Id;
        MarkActive();
        scheduleStarted = false;
        lastMinute = -1;
    }

    PresetViewModel? ScheduledNow()
    {
        var scheduled = ScheduleResolver.At(Presets.Select(p => p.Model), DateTime.Now);
        return Presets.FirstOrDefault(p => p.Model == scheduled);
    }

    void AdoptCurrentTaskbar()
    {
        var target = ScheduledNow() ?? Presets.FirstOrDefault();
        if (target is null) return;
        target.ReplaceItems(PinBuilder.Import(taskbar.Capture()));
        SetActive(target.Id);
    }

    void MarkActive()
    {
        foreach (var p in Presets) p.IsActive = p.Id == Settings.ActiveId;
        Active = Presets.FirstOrDefault(p => p.IsActive);
    }

    void SetActive(string? id)
    {
        Settings.ActiveId = id;
        Settings.Save();
        MarkActive();
    }

    void Save(PresetViewModel p)
    {
        if (p == Always) Settings.Save();
        else store.Save(p.Model);
        RefreshNext(DateTime.Now);
        PresetChanged?.Invoke(p);
    }

    [RelayCommand]
    async Task Apply(PresetViewModel preset) => await ApplyAsync(preset, auto: false);

    bool CanApply(PresetViewModel preset) => !Settings.SyncTaskbar || preset.HasItems;

    public async Task ApplyAsync(PresetViewModel preset, bool auto)
    {
        if (!Settings.SyncTaskbar)
        {
            if (preset.Id == Settings.ActiveId) return;
            SetActive(preset.Id);
            Status = Loc.F("environmentNow", preset.Model.Name);
            if (auto && Settings.Notify) Notice?.Invoke($"{preset.Glyph}  {preset.Model.Name}");
            return;
        }
        if (IsBusy)
        {
            pending = (preset, auto);
            return;
        }
        if (!preset.HasItems)
        {
            Notice?.Invoke(Loc.F("noPrograms", preset.Model.Name));
            return;
        }

        IsBusy = true;
        Status = Loc.F("switching", preset.Model.Name);
        try
        {
            taskbar.BackupOnce();
            var current = taskbar.Capture();
            var built = PinBuilder.Build(preset.Model, current, Settings.AlwaysPinned);
            if (!TaskbarService.SamePins(current, built)) await Task.Run(() => taskbar.Apply(built));
            SetActive(preset.Id);
            Status = Loc.F("taskbarNow", preset.Model.Name);
            if (!auto || Settings.Notify) Notice?.Invoke($"{preset.Glyph}  {preset.Model.Name}");
        }
        catch (Exception e)
        {
            Status = Loc.F("failed", e.Message);
            Notice?.Invoke(Status);
        }
        finally { IsBusy = false; }

        if (pending is var (next, nextAuto))
        {
            pending = null;
            await ApplyAsync(next, nextAuto);
        }
    }

    public async Task ApplyIndexAsync(int index)
    {
        if (index >= 0 && index < Presets.Count) await ApplyAsync(Presets[index], auto: false);
    }

    public async Task RestoreOriginalAsync()
    {
        if (IsBusy || taskbar.LoadBackup() is not { } original)
        {
            Notice?.Invoke(Loc.T("noBackup"));
            return;
        }
        IsBusy = true;
        Status = Loc.T("restoring");
        try
        {
            await Task.Run(() => taskbar.Apply(original));
            if (Settings.SyncTaskbar) SetActive(null);
            Status = Loc.T("restored");
        }
        catch (Exception e) { Status = Loc.F("failed", e.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task RestoreOriginal() => await RestoreOriginalAsync();

    [RelayCommand]
    void OpenFolder() => System.Diagnostics.Process.Start("explorer.exe", store.Dir);

    [RelayCommand]
    void UseCurrent(PresetViewModel preset)
    {
        preset.ReplaceItems(PinBuilder.Import(taskbar.Capture()));
        if (Settings.SyncTaskbar) SetActive(preset.Id);
        Status = Loc.F("copiedCurrent", preset.Model.Name);
    }

    public async Task SwitchByAsync(int delta)
    {
        if (Presets.Count == 0) return;
        var index = Active is { } a ? Presets.IndexOf(a) : -1;
        var next = ((index + delta) % Presets.Count + Presets.Count) % Presets.Count;
        await ApplyAsync(Presets[next], auto: false);
    }

    public void AddPrograms(PresetViewModel preset, IEnumerable<PinItem> items)
    {
        var known = new HashSet<string>();
        foreach (var existing in preset.Model.Items) PinBuilder.TryAdd(known, existing);
        var fresh = new List<PinItem>();
        var skipped = 0;
        foreach (var item in items)
        {
            if (PinBuilder.TryAdd(known, item)) fresh.Add(item);
            else skipped++;
        }
        preset.AddItems(fresh);
        if (skipped > 0) Status = skipped == 1 ? Loc.T("dupOne") : Loc.F("dupMany", skipped);
    }

    [RelayCommand]
    void Add()
    {
        var n = Presets.Count + 1;
        var id = $"preset-{DateTime.Now:HHmmss}";
        var model = new Preset
        {
            Id = id,
            Name = $"Preset {n}",
            Glyph = "⭐",
            Accent = Palette[(n - 1) % Palette.Length],
            FilePath = store.NewPath(id)
        };
        store.Save(model);
        Presets.Add(new PresetViewModel(model, n, Save));
        Status = Loc.T("presetCreated");
    }

    [RelayCommand]
    void Delete(PresetViewModel preset)
    {
        store.Delete(preset.Model);
        Presets.Remove(preset);
        if (Settings.ActiveId == preset.Id) SetActive(Settings.SyncTaskbar ? null : (ScheduledNow() ?? Presets.FirstOrDefault())?.Id);
    }

    void Tick()
    {
        var now = DateTime.Now;
        if (now.Minute != lastMinute)
        {
            lastMinute = now.Minute;
            RefreshNext(now);
        }

        if (IsBusy) return;
        var scheduled = ScheduleResolver.At(Presets.Select(p => p.Model), now);
        if (scheduleStarted && scheduled?.Id == lastScheduledId) return;
        scheduleStarted = true;
        lastScheduledId = scheduled?.Id;
        if (!Settings.AutoSwitch || scheduled is null) return;
        var target = Presets.First(p => p.Model == scheduled);
        if (target.Id != Settings.ActiveId && CanApply(target)) _ = ApplyAsync(target, auto: true);
    }

    public void SetLanguage(string code)
    {
        Loc.Instance.Set(code);
        Settings.Language = Loc.Instance.Language;
        Settings.Save();
        Always.RefreshText();
        foreach (var p in Presets) p.RefreshText();
        RefreshNext(DateTime.Now);
        Status = "";
    }

    void RefreshNext(DateTime now)
    {
        var next = ScheduleResolver.Next(Presets.Select(p => p.Model).ToList(), now);
        NextText = next is { } n
            ? Loc.F("nextSwitch", n.At.ToString("ddd HH:mm", Loc.Instance.Culture).TrimEnd('.'), n.To?.Name ?? Loc.T("free"))
            : Loc.T("noSchedule");
    }
}

