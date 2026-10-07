using System.Windows;
using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;
using WinDock.Views;

namespace WinDock;

public partial class App : System.Windows.Application
{
    Mutex? instance;
    TrayService? tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, "WinDock.SingleInstance", out var first);
        if (!first)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var settings = Settings.Load();
        var vm = new MainViewModel(new PresetStore(), settings, new IconService());
        var window = new DockWindow(vm);
        tray = new TrayService(vm, window);
        vm.Start();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
