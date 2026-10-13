using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace WinDock.Services;

public sealed record TrayApp(string Name, string Exe, IReadOnlyList<int> Pids, string? AppId);

// Apps with an icon in the notification area. Windows 11 lists every program that ever showed a tray icon under
// NotifyIconSettings; the ones running now are the ones in the tray. Windows 10 keeps no such list, so it shows none.
public static class TrayApps
{
    const string SettingsKey = @"Control Panel\NotifyIconSettings";
    const uint QueryLimited = 0x1000;

    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase) { "explorer.exe", "rundll32.exe", "wlrmdr.exe" };

    [DllImport("shell32.dll")]
    static extern int SHGetKnownFolderPath(ref Guid id, uint flags, nint token, out nint path);

    public static List<TrayApp> Running()
    {
        var known = Registered();
        if (known.Count == 0) return [];
        // A tray icon talks to a window of its process, so console shells (PowerShell, Python) that once showed an
        // icon do not count unless they own a window now.
        var windowed = new HashSet<int>();
        Native.EnumWindows((h, _) =>
        {
            if (Native.ClassOf(h) is "ConsoleWindowClass" or "PseudoConsoleWindow" or "IME" or "MSCTFIME UI") return true;
            Native.GetWindowThreadProcessId(h, out var pid);
            windowed.Add((int)pid);
            return true;
        }, 0);
        var ownExe = Environment.ProcessPath;
        var byExe = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!windowed.Contains(process.Id) || Path(process.Id) is not { } exe || !known.Contains(exe)) continue;
                if (string.Equals(exe, ownExe, StringComparison.OrdinalIgnoreCase)) continue;
                if (!byExe.TryGetValue(exe, out var pids)) byExe[exe] = pids = [];
                pids.Add(process.Id);
            }
        }
        return byExe
            .Select(p => new TrayApp(NameOf(p.Key, p.Value), p.Key, p.Value, AppIdOf(p.Value[0])))
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static HashSet<string> Registered()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var root = Registry.CurrentUser.OpenSubKey(SettingsKey);
        if (root is null) return paths;
        foreach (var name in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(name);
            if (key?.GetValue("ExecutablePath") is string raw && Resolve(raw) is { } path && !Skip.Contains(System.IO.Path.GetFileName(path)))
                paths.Add(path);
        }
        return paths;
    }

    // "{F38BF404-...}\explorer.exe" starts with a known folder id.
    public static string? Resolve(string raw)
    {
        if (!raw.StartsWith('{')) return raw;
        var close = raw.IndexOf('}');
        if (close < 0 || !Guid.TryParse(raw[1..close], out var id)) return null;
        if (SHGetKnownFolderPath(ref id, 0, 0, out var ptr) != 0) return null;
        try { return Marshal.PtrToStringUni(ptr) + raw[(close + 1)..]; }
        finally { Marshal.FreeCoTaskMem(ptr); }
    }

    // Brings a window of the app forward, or starts it again, which wakes most tray apps up.
    public static void Open(TrayApp app)
    {
        nint found = 0;
        var pids = app.Pids.ToHashSet();
        Native.EnumWindows((h, _) =>
        {
            Native.GetWindowThreadProcessId(h, out var pid);
            if (!pids.Contains(pid) || !Native.IsWindowVisible(h) || Native.TitleOf(h).Length == 0) return true;
            found = h;
            return false;
        }, 0);
        if (found != 0) WindowTracker.Activate(found);
        else if (app.AppId is { } id && Launcher.OpenAppId(id)) { }
        else Launcher.OpenFile(app.Exe);
    }

    // Like "End task" in Task Manager. Returns false when Windows refuses (an app running as administrator).
    public static bool End(TrayApp app)
    {
        var ok = true;
        foreach (var pid in app.Pids)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
            }
            catch (ArgumentException) { }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException) { ok = false; }
        }
        return ok;
    }

    static string NameOf(string exe, IReadOnlyList<int> pids)
    {
        if (AppIdOf(pids[0]) is { } id && ShellIcons.NameForAppId(id) is { Length: > 0 } packaged) return packaged;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription.Trim();
            if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName.Trim();
        }
        catch (FileNotFoundException) { }
        return System.IO.Path.GetFileNameWithoutExtension(exe);
    }

    static string? Path(int pid) => With(pid, h =>
    {
        var size = 1024;
        var name = new StringBuilder(size);
        return Native.QueryFullProcessImageName(h, 0, name, ref size) ? name.ToString() : null;
    });

    static string? AppIdOf(int pid) => With(pid, h =>
    {
        var length = 512;
        var id = new StringBuilder(length);
        return Native.GetApplicationUserModelId(h, ref length, id) == 0 ? id.ToString() : null;
    });

    static string? With(int pid, Func<nint, string?> read)
    {
        var handle = Native.OpenProcess(QueryLimited, false, pid);
        if (handle == 0) return null;
        try { return read(handle); }
        finally { Native.CloseHandle(handle); }
    }
}
