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
    static readonly CultureInfo Pt = new("pt-BR");
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
        Always = new PresetViewModel(new Preset { Id = "always", Name = "Sempre fixos", Items = settings.AlwaysPinned }, 0, Save);
        tick.Tick += (_, _) => Tick();
    }

    public Settings Settings { get; }
    public PresetStore Store => store;
    public ObservableCollection<PresetViewModel> Presets { get; } = [];
    public PresetViewModel Always { get; }

    public event Action<string>? Notice;

    [ObservableProperty] string status = "";
    [ObservableProperty] string nextText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    bool isBusy;

    public bool IsIdle => !IsBusy;

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
    }

    public void Reload()
    {
        Presets.Clear();
        var loaded = store.Load();
        for (var i = 0; i < loaded.Count; i++) Presets.Add(new PresetViewModel(loaded[i], i + 1, Save));
        if (store.Seeded) AdoptCurrentTaskbar();
        foreach (var p in Presets) p.RemoveDuplicates();
        Always.RemoveDuplicates();
        MarkActive();
        scheduleStarted = false;
        lastMinute = -1;
    }

    void AdoptCurrentTaskbar()
    {
        var scheduled = ScheduleResolver.At(Presets.Select(p => p.Model), DateTime.Now);
        var target = Presets.FirstOrDefault(p => p.Model == scheduled) ?? Presets.FirstOrDefault();
        if (target is null) return;
        target.ReplaceItems(PinBuilder.Import(taskbar.Capture()));
        SetActive(target.Id);
    }

    void MarkActive()
    {
        foreach (var p in Presets) p.IsActive = p.Id == Settings.ActiveId;
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
    }

    [RelayCommand]
    async Task Apply(PresetViewModel preset) => await ApplyAsync(preset, auto: false);

    public async Task ApplyAsync(PresetViewModel preset, bool auto)
    {
        if (IsBusy)
        {
            pending = (preset, auto);
            return;
        }
        if (!preset.HasItems)
        {
            Notice?.Invoke($"{preset.Model.Name}: nenhum programa, adicione alguns primeiro");
            return;
        }

        IsBusy = true;
        Status = $"Trocando a taskbar para {preset.Model.Name}...";
        try
        {
            taskbar.BackupOnce();
            var current = taskbar.Capture();
            var built = PinBuilder.Build(preset.Model, current, Settings.AlwaysPinned);
            if (!TaskbarService.SamePins(current, built)) await Task.Run(() => taskbar.Apply(built));
            SetActive(preset.Id);
            Status = $"Taskbar: {preset.Model.Name}";
            if (!auto || Settings.Notify) Notice?.Invoke($"{preset.Glyph}  {preset.Model.Name}");
        }
        catch (Exception e)
        {
            Status = $"Falhou: {e.Message}";
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
            Notice?.Invoke("Nenhum backup da taskbar original ainda");
            return;
        }
        IsBusy = true;
        Status = "Restaurando a taskbar original...";
        try
        {
            await Task.Run(() => taskbar.Apply(original));
            SetActive(null);
            Status = "Taskbar original restaurada";
        }
        catch (Exception e) { Status = $"Falhou: {e.Message}"; }
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
        SetActive(preset.Id);
        Status = $"Programas fixados agora copiados para {preset.Model.Name}";
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
        if (skipped > 0) Status = skipped == 1 ? "1 programa já estava na lista e foi ignorado" : $"{skipped} programas já estavam na lista e foram ignorados";
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
        Status = "Preset criado. Adicione programas nele.";
    }

    [RelayCommand]
    void Delete(PresetViewModel preset)
    {
        store.Delete(preset.Model);
        Presets.Remove(preset);
        if (Settings.ActiveId == preset.Id) SetActive(null);
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
        if (target.Id != Settings.ActiveId && target.HasItems) _ = ApplyAsync(target, auto: true);
    }

    void RefreshNext(DateTime now)
    {
        var next = ScheduleResolver.Next(Presets.Select(p => p.Model).ToList(), now);
        NextText = next is { } n
            ? $"Próxima troca: {n.At.ToString("ddd HH:mm", Pt).TrimEnd('.')} → {n.To?.Name ?? "livre"}"
            : "Sem agenda";
    }
}

