using System.Windows.Threading;
using Microsoft.Win32;
using WinDock.ViewModels;
using Forms = System.Windows.Forms;

namespace WinDock.Views;

public sealed class DockHost : IDisposable
{
    readonly DockViewModel vm;
    readonly Action openSettings;
    readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    readonly Dictionary<string, (DockWindow Window, DockViewModel? Own)> windows = [];

    public DockHost(DockViewModel vm, Action openSettings)
    {
        this.vm = vm;
        this.openSettings = openSettings;
        vm.Options.Changed += OnOption;
        SystemEvents.DisplaySettingsChanged += OnDisplays;
        Sync();
    }

    public static IReadOnlyList<string> Targets(bool enabled, bool all, string? chosen, IReadOnlyList<string> screens, string primary)
    {
        if (!enabled || screens.Count == 0) return [];
        if (all) return screens;
        return [screens.Contains(chosen ?? "") ? chosen! : primary];
    }

    void OnOption(string name)
    {
        if (name is nameof(DockOptions.Enabled) or nameof(DockOptions.AllScreens) or nameof(DockOptions.Screen) or nameof(DockOptions.PerScreen)) Sync();
    }

    void OnDisplays(object? sender, EventArgs e) => dispatcher.BeginInvoke(Sync, DispatcherPriority.Background);

    void Sync()
    {
        var screens = Forms.Screen.AllScreens.Select(s => s.DeviceName).ToList();
        var primary = Forms.Screen.PrimaryScreen?.DeviceName ?? screens.FirstOrDefault() ?? "";
        var wanted = Targets(vm.Options.Enabled, vm.Options.AllScreens, vm.Options.Screen, screens, primary);
        // With "each screen its own dock", the main dock stays on the main screen and the others get docks of their own.
        var perScreen = vm.Options.PerScreen && wanted.Count > 1;
        var home = wanted.Contains(primary) ? primary : wanted.FirstOrDefault();
        vm.OnlyScreen = perScreen ? home : null;

        foreach (var gone in windows.Keys.Where(k => !wanted.Contains(k) || windows[k].Own is not null != (perScreen && k != home)).ToList())
        {
            windows[gone].Window.Close();
            windows[gone].Own?.Dispose();
            windows.Remove(gone);
        }
        foreach (var name in wanted.Where(n => !windows.ContainsKey(n)))
        {
            var own = perScreen && name != home ? new DockViewModel(vm.Main, vm.Shell, name, vm.Tracker) : null;
            var window = new DockWindow(own ?? vm, openSettings, name);
            windows[name] = (window, own);
            window.Show();
        }
        if (windows.Count == 0) vm.Shell.ShowTaskbar();
    }

    public void Dispose()
    {
        vm.Options.Changed -= OnOption;
        SystemEvents.DisplaySettingsChanged -= OnDisplays;
        foreach (var (window, own) in windows.Values)
        {
            window.Close();
            own?.Dispose();
        }
        windows.Clear();
    }
}
