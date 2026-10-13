using System.Diagnostics;
using System.Text.Json;
using WinDock.Models;

namespace WinDock.Services;

public sealed class ShellState
{
    public int Pid { get; set; }
    public DateTime Started { get; set; }
    public bool TaskbarHidden { get; set; }
    public bool RestoreAutoHide { get; set; }
    public List<long> HiddenWindows { get; set; } = [];

    public static string FilePath { get; set; } = Path.Combine(Settings.Dir, "shell-state.json");

    public static ShellState? Load()
    {
        try { return JsonSerializer.Deserialize<ShellState>(File.ReadAllText(FilePath), Json.Options); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json.Options));
    }

    public static void Delete()
    {
        try { File.Delete(FilePath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public bool IsOwnerAlive()
    {
        try
        {
            using var p = Process.GetProcessById(Pid);
            return Math.Abs((p.StartTime - Started).TotalSeconds) < 2 && !p.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
}

public sealed class ShellGuard : IDisposable
{
    const int AbmGetState = 4, AbmSetState = 10, AbsAutoHide = 1;

    readonly ShellState state;
    bool watchdog;
    bool peeking;

    public ShellGuard()
    {
        using var self = Process.GetCurrentProcess();
        state = new ShellState { Pid = self.Id, Started = self.StartTime };
    }

    public bool TaskbarHidden => state.TaskbarHidden;
    public bool Peeking => peeking;

    public static void Watch(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.WaitForExit();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }

        if (ShellState.Load() is { } s && s.Pid == pid)
        {
            Restore(s);
            ShellState.Delete();
        }
    }

    public static void RecoverStale()
    {
        if (ShellState.Load() is not { } s || s.IsOwnerAlive()) return;
        Restore(s);
        ShellState.Delete();
    }

    public static void Restore(ShellState s)
    {
        if (s.RestoreAutoHide && IsAutoHide()) SetState(GetState() & ~AbsAutoHide);
        foreach (var bar in Bars()) Native.ShowWindow(bar, Native.SW_SHOWNA);
        foreach (var h in s.HiddenWindows)
            if (Native.IsWindow((nint)h)) Native.ShowWindow((nint)h, Native.SW_SHOWNA);
    }

    public void HideTaskbar()
    {
        if (!state.TaskbarHidden)
        {
            state.RestoreAutoHide = !IsAutoHide();
            state.TaskbarHidden = true;
            Persist();
        }
        peeking = false;
        Enforce();
    }

    public void ShowTaskbar()
    {
        if (!state.TaskbarHidden) return;
        if (state.RestoreAutoHide && IsAutoHide()) SetState(GetState() & ~AbsAutoHide);
        foreach (var bar in Bars()) Native.ShowWindow(bar, Native.SW_SHOWNA);
        state.TaskbarHidden = false;
        state.RestoreAutoHide = false;
        peeking = false;
        Persist();
    }

    public void Peek(bool show)
    {
        if (!state.TaskbarHidden) return;
        peeking = show;
        foreach (var bar in Bars()) Native.ShowWindow(bar, show ? Native.SW_SHOWNA : Native.SW_HIDE);
    }

    public void Enforce()
    {
        if (!state.TaskbarHidden || peeking) return;
        if (!IsAutoHide()) SetState(GetState() | AbsAutoHide);
        foreach (var bar in Bars())
            if (Native.IsWindowVisible(bar)) Native.ShowWindow(bar, Native.SW_HIDE);
    }

    public void SetHiddenWindows(IEnumerable<nint> handles)
    {
        var list = handles.Select(h => (long)h).ToList();
        if (list.SequenceEqual(state.HiddenWindows)) return;
        state.HiddenWindows = list;
        Persist();
    }

    public static void Start() => Native.Keys(Native.VK_LWIN);

    public static void QuickSettings() => Native.Keys(Native.VK_LWIN, 0x41);

    public static void Notifications() => Native.Keys(Native.VK_LWIN, 0x4E);

    public static void PowerMenu() => Native.Keys(Native.VK_LWIN, 0x58);

    public static void SoundOutput() => Native.Keys(Native.VK_LWIN, 0x11, 0x56);

    public static void ShowDesktop() => Native.Keys(Native.VK_LWIN, 0x44);

    public static bool TaskbarAutoHides() => IsAutoHide();

    void Persist()
    {
        if (!state.TaskbarHidden && state.HiddenWindows.Count == 0)
        {
            ShellState.Delete();
            return;
        }
        state.Save();
        StartWatchdog();
    }

    void StartWatchdog()
    {
        if (watchdog || Environment.ProcessPath is not { } exe) return;
        try
        {
            Process.Start(new ProcessStartInfo(exe, $"--watchdog {state.Pid}") { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
            watchdog = true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    static IEnumerable<nint> Bars()
    {
        var main = Native.FindWindow("Shell_TrayWnd", null);
        if (main != 0) yield return main;
        for (var h = Native.FindWindowEx(0, 0, "Shell_SecondaryTrayWnd", null); h != 0; h = Native.FindWindowEx(0, h, "Shell_SecondaryTrayWnd", null))
            yield return h;
    }

    static Native.AppBarData Data() => new()
    {
        Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.AppBarData>(),
        Window = Native.FindWindow("Shell_TrayWnd", null)
    };

    static int GetState()
    {
        var data = Data();
        return (int)Native.SHAppBarMessage(AbmGetState, ref data);
    }

    static bool IsAutoHide() => (GetState() & AbsAutoHide) != 0;

    static void SetState(int value)
    {
        var data = Data();
        data.Param = value;
        Native.SHAppBarMessage(AbmSetState, ref data);
    }

    public void Dispose()
    {
        Restore(state);
        state.TaskbarHidden = false;
        state.HiddenWindows.Clear();
        ShellState.Delete();
    }
}
