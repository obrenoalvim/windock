using System.Windows;
using System.Windows.Interop;
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
    const int ToggleId = 1;
    const int PresetBaseId = 10;
    static readonly nint MessageOnly = new(-3);

    Mutex? instance;
    EventWaitHandle? showEvent;
    EventWaitHandle? exitEvent;
    TrayService? tray;
    ConfigWindow? window;
    WelcomeWindow? welcome;
    HwndSource? hotkeys;
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
        main.Start();
        RegisterHotkeys();

        shell = new ShellGuard();
        dock = new DockViewModel(main, shell);
        docks = new DockHost(dock, OpenConfig);
        WinKeys.Pressed = (n, fresh) => docks.OpenNth(n, fresh);
        main.Dock.Changed += _ => SyncKeys();
        SyncKeys();

        tray = new TrayService(main, OpenConfig, () => Config.Toggle(), dock, ShowWelcome);
        if (e.Args.Contains("--welcome") || (!settings.TourSeen && !e.Args.Contains("--tray")))
            Dispatcher.BeginInvoke(ShowWelcome, DispatcherPriority.Background);

        new Thread(() =>
        {
            while (showEvent.WaitOne()) Dispatcher.BeginInvoke(OpenConfig);
        }) { IsBackground = true }.Start();
        new Thread(() =>
        {
            if (exitEvent.WaitOne()) Dispatcher.BeginInvoke(Shutdown);
        }) { IsBackground = true }.Start();
    }

    void SyncKeys() => WinKeys.Enable(main!.Dock.Enabled && main.Dock.ReplaceTaskbar);

    ConfigWindow Config => window ??= new ConfigWindow(main!);

    void OpenConfig() => Config.Open();

    void ShowWelcome()
    {
        if (welcome is { IsVisible: true })
        {
            welcome.Activate();
            return;
        }
        var launchers = Onboarding.Launchers();
        var steps = Onboarding.Plan(main!.Always.Model.Items.Count(i => i.Exists), launchers.Count);
        welcome = new WelcomeWindow(main, steps, launchers, OpenConfig);
        welcome.Show();
    }

    void RegisterHotkeys()
    {
        hotkeys = new HwndSource(new HwndSourceParameters("WinDock.Hotkeys") { ParentWindow = MessageOnly, WindowStyle = 0 });
        hotkeys.AddHook(OnHotkey);
        const uint mods = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT;
        Native.RegisterHotKey(hotkeys.Handle, ToggleId, mods, 0x57);
        for (var i = 0; i < 9; i++) Native.RegisterHotKey(hotkeys.Handle, PresetBaseId + i, mods, (uint)(0x31 + i));
    }

    nint OnHotkey(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != Native.WM_HOTKEY) return 0;
        handled = true;
        var id = (int)wParam;
        if (id == ToggleId) Config.Toggle();
        else _ = main!.ApplyIndexAsync(id - PresetBaseId);
        return 0;
    }

    void ReleaseHotkeys()
    {
        if (hotkeys is null) return;
        Native.UnregisterHotKey(hotkeys.Handle, ToggleId);
        for (var i = 0; i < 9; i++) Native.UnregisterHotKey(hotkeys.Handle, PresetBaseId + i);
        hotkeys.Dispose();
    }

    void OnCrash(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        e.Handled = true;
    }

    public static void Log(Exception error)
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.AppendAllText(Path.Combine(Settings.Dir, "error.log"), $"{DateTime.Now:O}  {error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception log) when (log is IOException or UnauthorizedAccessException) { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WinKeys.Enable(false);
        main?.Dock.Flush();
        docks?.Dispose();
        dock?.Dispose();
        shell?.Dispose();
        ReleaseHotkeys();
        tray?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
