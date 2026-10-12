using System.Text;
using System.Windows.Threading;

namespace WinDock.Services;

public sealed record AppWindow(nint Handle, string Title, string? Exe, string? AppId, bool Minimized);

public sealed class WindowTracker : IDisposable
{
    public const string ExplorerId = "Microsoft.Windows.Explorer";

    const uint ProcessQueryLimited = 0x1000;
    const uint EventForeground = 0x0003, EventNameChange = 0x800C;
    const int DwmCloaked = 14;
    const string FrameHost = "applicationframehost.exe";

    static readonly TimeSpan HideGrace = TimeSpan.FromSeconds(1.5);

    static readonly HashSet<string> SkipClasses =
    [
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland", "Shell_InputSwitchTopLevelWindow"
    ];

    static readonly (uint Min, uint Max)[] Events =
    [
        (EventForeground, EventForeground), (0x0016, 0x0017), (0x8000, 0x8003), (EventNameChange, EventNameChange), (0x8017, 0x8018)
    ];

    readonly Native.WinEventProc proc;
    readonly List<nint> hooks = [];
    readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(70) };
    readonly DispatcherTimer safety = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly Dictionary<int, string?> exeByPid = [];
    readonly Dictionary<nint, string?> appIdByWindow = [];
    readonly Dictionary<nint, string> envByWindow = [];
    readonly Dictionary<nint, (string Env, DateTime At)> hidden = [];
    readonly int ownPid = Environment.ProcessId;
    nint foreground;

    public WindowTracker()
    {
        proc = OnEvent;
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            Refresh();
        };
        safety.Tick += (_, _) => Refresh();
        foreach (var (min, max) in Events) hooks.Add(Native.SetWinEventHook(min, max, 0, proc, 0, 0, 0x2));
        safety.Start();
        Refresh();
    }

    public IReadOnlyList<AppWindow> Windows { get; private set; } = [];
    public nint Foreground => foreground;
    public string? ActiveEnvironment { get; set; }
    public IEnumerable<nint> HiddenHandles => hidden.Keys;

    public event Action? Changed;

    void OnEvent(nint hook, uint ev, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || idChild != 0) return;
        if (ev == EventNameChange && Windows.Any(w => w.Handle == hwnd)) return;
        if (ev != EventForeground)
        {
            var root = Native.GetAncestor(hwnd, Native.GA_ROOT);
            if (root != 0 && root != hwnd) return;
        }
        if (!debounce.IsEnabled) debounce.Start();
    }

    public void Refresh()
    {
        var now = DateTime.UtcNow;
        var list = new List<AppWindow>();
        var pids = new HashSet<int>();
        Native.EnumWindows((h, _) =>
        {
            if (Describe(h, pids) is { } w && !(hidden.TryGetValue(h, out var hid) && now - hid.At < HideGrace)) list.Add(w);
            return true;
        }, 0);

        foreach (var pid in exeByPid.Keys.Where(p => !pids.Contains(p)).ToList()) exeByPid.Remove(pid);
        var alive = list.Select(w => w.Handle).ToHashSet();
        foreach (var h in appIdByWindow.Keys.Where(h => !alive.Contains(h)).ToList()) appIdByWindow.Remove(h);
        foreach (var h in envByWindow.Keys.Where(h => !alive.Contains(h) && !hidden.ContainsKey(h) && !Native.IsWindow(h)).ToList()) envByWindow.Remove(h);
        foreach (var h in hidden.Keys.Where(h => !Native.IsWindow(h)).ToList()) hidden.Remove(h);

        foreach (var w in list)
        {
            if (hidden.Remove(w.Handle) || !envByWindow.ContainsKey(w.Handle))
                if (ActiveEnvironment is { } env) envByWindow[w.Handle] = env;
        }

        var top = Native.GetAncestor(Native.GetForegroundWindow(), Native.GA_ROOTOWNER);
        var same = top == foreground && list.Count == Windows.Count
                   && list.Zip(Windows).All(p => p.First with { Title = "" } == p.Second with { Title = "" });
        Windows = list;
        foreground = top;
        if (!same) Changed?.Invoke();
    }

    public void SwitchEnvironment(string? from, string? to, Func<AppWindow, bool> stays)
    {
        ActiveEnvironment = to;
        if (from is not null)
        {
            foreach (var w in Windows)
            {
                if (stays(w) || !envByWindow.TryGetValue(w.Handle, out var env) || env != from) continue;
                if (Native.ShowWindowAsync(w.Handle, Native.SW_HIDE)) hidden[w.Handle] = (from, DateTime.UtcNow);
                else envByWindow[w.Handle] = to ?? from;
            }
        }
        if (to is not null)
        {
            foreach (var h in hidden.Where(p => p.Value.Env == to).Select(p => p.Key).ToList())
            {
                hidden.Remove(h);
                if (Native.IsWindow(h)) Native.ShowWindowAsync(h, Native.SW_SHOWNA);
            }
        }
        Refresh();
    }

    public void ShowAll()
    {
        foreach (var h in hidden.Keys.ToList())
            if (Native.IsWindow(h)) Native.ShowWindowAsync(h, Native.SW_SHOWNA);
        hidden.Clear();
    }

    public static void Activate(nint h)
    {
        if (Native.IsIconic(h)) Native.ShowWindowAsync(h, Native.SW_RESTORE);
        if (Native.SetForegroundWindow(h) && Native.GetForegroundWindow() == h) return;
        Native.keybd_event(Native.VK_MENU, 0, Native.KEYEVENTF_EXTENDEDKEY, 0);
        Native.keybd_event(Native.VK_MENU, 0, Native.KEYEVENTF_EXTENDEDKEY | Native.KEYEVENTF_KEYUP, 0);
        if (!Native.SetForegroundWindow(h)) Native.SwitchToThisWindow(h, true);
    }

    public static void Minimize(nint h) => Native.ShowWindowAsync(h, Native.SW_MINIMIZE);

    public static void Close(nint h) => Native.PostMessage(h, Native.WM_CLOSE, 0, 0);

    AppWindow? Describe(nint h, HashSet<int> pids)
    {
        if (!Native.IsWindowVisible(h)) return null;
        var ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
        var appWindow = (ex & Native.WS_EX_APPWINDOW) != 0;
        var hiddenStyle = Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT;
        if (!appWindow && (Native.GetWindow(h, Native.GW_OWNER) != 0 || (ex & hiddenStyle) != 0)) return null;
        var cls = Native.ClassOf(h);
        if (SkipClasses.Contains(cls)) return null;
        Native.GetWindowThreadProcessId(h, out var pid);
        if (pid == ownPid) return null;
        if (Native.DwmGetWindowAttribute(h, DwmCloaked, out var cloaked, 4) == 0 && cloaked != 0) return null;
        var title = Native.TitleOf(h);
        if (title.Length == 0) return null;
        pids.Add(pid);

        var exe = ExeOf(pid);
        var appId = cls == "CabinetWClass" ? ExplorerId : AppIdOf(h, pid, exe);
        return new AppWindow(h, title, exe, appId, Native.IsIconic(h));
    }

    string? ExeOf(int pid)
    {
        if (exeByPid.TryGetValue(pid, out var hit)) return hit;
        return exeByPid[pid] = WithProcess(pid, p =>
        {
            var size = 1024;
            var name = new StringBuilder(size);
            return Native.QueryFullProcessImageName(p, 0, name, ref size) ? name.ToString() : null;
        });
    }

    string? AppIdOf(nint h, int pid, string? exe)
    {
        if (appIdByWindow.TryGetValue(h, out var hit)) return hit;
        var frameHost = exe is not null && Path.GetFileName(exe).Equals(FrameHost, StringComparison.OrdinalIgnoreCase);
        var id = ShellLinks.WindowAppId(h) ?? (frameHost ? HostedAppId(h) : PackageAppId(pid));
        if (id is not null || !frameHost) appIdByWindow[h] = id;
        return id;
    }

    static string? HostedAppId(nint frame)
    {
        string? id = null;
        Native.EnumChildWindows(frame, (child, _) =>
        {
            if (Native.ClassOf(child) != "Windows.UI.Core.CoreWindow") return true;
            Native.GetWindowThreadProcessId(child, out var pid);
            id = PackageAppId(pid);
            return id is null;
        }, 0);
        return id;
    }

    static string? PackageAppId(int pid) => WithProcess(pid, p =>
    {
        var length = 512;
        var id = new StringBuilder(length);
        return Native.GetApplicationUserModelId(p, ref length, id) == 0 ? id.ToString() : null;
    });

    static string? WithProcess(int pid, Func<nint, string?> read)
    {
        var handle = Native.OpenProcess(ProcessQueryLimited, false, pid);
        if (handle == 0) return null;
        try { return read(handle); }
        finally { Native.CloseHandle(handle); }
    }

    public void Dispose()
    {
        foreach (var hook in hooks) Native.UnhookWinEvent(hook);
        hooks.Clear();
        debounce.Stop();
        safety.Stop();
        ShowAll();
    }
}
