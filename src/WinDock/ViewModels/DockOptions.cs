using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed class DockOptions : ObservableObject
{
    readonly Settings settings;
    readonly DispatcherTimer save = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public DockOptions(Settings settings)
    {
        this.settings = settings;
        save.Tick += (_, _) =>
        {
            save.Stop();
            settings.Save();
        };
    }

    DockSettings D => settings.Dock;

    public event Action<string>? Changed;

    public bool Enabled { get => D.Enabled; set => Set(() => D.Enabled = value); }
    public bool ReplaceTaskbar { get => D.ReplaceTaskbar; set => Set(() => D.ReplaceTaskbar = value); }
    public bool AutoHide { get => D.AutoHide; set => Set(() => D.AutoHide = value, nameof(CanReserve)); }
    public bool Reserve { get => D.Reserve; set => Set(() => D.Reserve = value); }
    public bool FollowWindows { get => D.FollowWindows; set => Set(() => D.FollowWindows = value); }
    public bool SyncTaskbar { get => settings.SyncTaskbar; set => Set(() => settings.SyncTaskbar = value); }
    public double Magnification { get => D.Magnification; set => Set(() => D.Magnification = Math.Round(value, 2)); }
    public double LensWidth { get => D.LensWidth; set => Set(() => D.LensWidth = Math.Round(value, 2)); }
    public double IconSize { get => D.IconSize; set => Set(() => D.IconSize = Math.Round(value)); }
    public string Lens { get => D.Lens; set => Set(() => D.Lens = value, nameof(LensNone), nameof(LensScale), nameof(LensMagnify)); }
    public string Theme { get => D.Theme; set => Set(() => D.Theme = value); }
    public string? Screen { get => D.Screen; set => Set(() => D.Screen = value); }
    public bool AllScreens { get => D.AllScreens; set => Set(() => D.AllScreens = value); }
    public bool CompactWidgets { get => D.CompactWidgets; set => Set(() => D.CompactWidgets = value); }
    public bool PerScreen { get => D.PerScreen; set => Set(() => D.PerScreen = value); }
    public Dictionary<string, string> ScreenEnvironments => D.ScreenEnvironments;

    public void SetScreenEnvironment(string screen, string id) => Set(() => D.ScreenEnvironments[screen] = id, name: nameof(ScreenEnvironments));

    public bool CanReserve => !AutoHide;

    public bool StartWithWindows
    {
        get => Autostart.Enabled;
        set
        {
            Autostart.Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool LensNone { get => Lens == DockSettings.LensNone; set { if (value) Lens = DockSettings.LensNone; else OnPropertyChanged(); } }
    public bool LensScale { get => Lens == DockSettings.LensScale; set { if (value) Lens = DockSettings.LensScale; else OnPropertyChanged(); } }
    public bool LensMagnify { get => Lens == DockSettings.LensMagnify; set { if (value) Lens = DockSettings.LensMagnify; else OnPropertyChanged(); } }

    void Set(Action apply, string also1 = "", string also2 = "", string also3 = "", [CallerMemberName] string name = "")
    {
        apply();
        D.Clamp();
        save.Stop();
        save.Start();
        OnPropertyChanged(name);
        foreach (var other in new[] { also1, also2, also3 }.Where(o => o.Length > 0)) OnPropertyChanged(other);
        Changed?.Invoke(name);
    }

    public void Flush()
    {
        if (!save.IsEnabled) return;
        save.Stop();
        settings.Save();
    }
}
