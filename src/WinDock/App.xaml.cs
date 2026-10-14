using System.Windows;
using System.Windows.Threading;
using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;
using WinDock.Views;

namespace WinDock;

public partial class App : System.Windows.Application
{
    const string ShowSignal = "WinDock.Show";
    const string ExitSignal = "WinDock.Exit";

    Mutex? instance;
    EventWaitHandle? showEvent;
    EventWaitHandle? exitEvent;
    TrayService? tray;
    ConfigWindow? window;
    ShellGuard? shell;
    DockViewModel? dock;
    DockHost? docks;
    MainViewModel? main;

    protected override void OnStartup(StartupEventArgs e)
    {
        exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitSignal);
        if (e.Args.Contains("--exit"))
        {
            exitEvent.Set();
            Shutdown();
            return;
        }

        instance = new Mutex(true, "WinDock.SingleInstance", out var first);
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignal);
        if (!first)
        {
            showEvent.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += OnCrash;
        ShellGuard.RecoverStale();

        var firstRun = !Settings.Exists;
        var settings = Settings.Load();
        if (firstRun) Autostart.Enabled = true;
        Loc.Instance.Set(settings.Language);
        main = new MainViewModel(new PresetStore(), new TaskbarService(), settings);
        window = new ConfigWindow(main);
        main.Start();

        shell = new ShellGuard();
        dock = new DockViewModel(main, shell);
        docks = new DockHost(dock, () => window.Open());
        WinKeys.Pressed = (n, fresh) => docks.OpenNth(n, fresh);
        main.Dock.Changed += _ => SyncKeys();
        SyncKeys();

        tray = new TrayService(main, window, dock);
        if (!e.Args.Contains("--tray")) window.Open();

        new Thread(() =>
        {
            while (showEvent.WaitOne()) Dispatcher.BeginInvoke(() => window.Open());
        }) { IsBackground = true }.Start();
        new Thread(() =>
        {
            if (exitEvent.WaitOne()) Dispatcher.BeginInvoke(Shutdown);
        }) { IsBackground = true }.Start();
    }

    // The hook runs only while the dock stands in for the taskbar.
    void SyncKeys() => WinKeys.Enable(main!.Dock.Enabled && main.Dock.ReplaceTaskbar);

    void OnCrash(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.AppendAllText(Path.Combine(Settings.Dir, "error.log"), $"{DateTime.Now:O}  {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception log) when (log is IOException or UnauthorizedAccessException) { }
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WinKeys.Enable(false);
        main?.Dock.Flush();
        docks?.Dispose();
        dock?.Dispose();
        shell?.Dispose();
        window?.Release();
        tray?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
