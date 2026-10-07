using System.Windows;
using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;
using WinDock.Views;

namespace WinDock;

public partial class App : System.Windows.Application
{
    const string ShowSignal = "WinDock.Show";

    Mutex? instance;
    EventWaitHandle? showEvent;
    TrayService? tray;
    ConfigWindow? window;

    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, "WinDock.SingleInstance", out var first);
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignal);
        if (!first)
        {
            showEvent.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var vm = new MainViewModel(new PresetStore(), new TaskbarService(), Settings.Load());
        window = new ConfigWindow(vm);
        tray = new TrayService(vm, window);
        vm.Start();
        if (!e.Args.Contains("--tray")) window.Open();

        new Thread(() =>
        {
            while (showEvent.WaitOne()) Dispatcher.BeginInvoke(() => window.Open());
        }) { IsBackground = true }.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        window?.Release();
        tray?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
