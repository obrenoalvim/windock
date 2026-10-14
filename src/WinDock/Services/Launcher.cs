using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinDock.Models;

namespace WinDock.Services;

public static class Launcher
{
    static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", PreserveSig = false)]
    static extern nint SHGetKnownFolderPath(ref Guid id, uint flags, nint token);

    public static bool Open(PinItem item)
    {
        if (item.IsUriLauncher) return Shell(item.Args!);
        if (item.IsLauncher) return Shell(item.Args!) || Shell(item.ResolvedTarget!, item.Args);
        if (item.IsRaw) return item.AppId is { Length: > 0 } id && OpenAppId(id);
        return item.ResolvedTarget is { } target && Shell(target);
    }

    public static bool OpenAppId(string appId) =>
        appId.Contains('!') ? Shell(@"shell:AppsFolder\" + appId) : File.Exists(TaskbarBlob.ExpandAppId(appId)) && Shell(TaskbarBlob.ExpandAppId(appId));

    public static bool OpenFile(string path) => Shell(path);

    // Files dropped on a dock icon open in that program, after the shortcut's own arguments.
    public static bool OpenWith(string exe, string? args, IEnumerable<string> files) =>
        Shell(exe, string.Join(' ', files.Select(f => $"\"{f}\"").Prepend(args ?? "").Where(a => a.Length > 0)));

    public static void Reveal(string path)
    {
        if (File.Exists(path)) Shell("explorer.exe", $"/select,\"{path}\"");
        else if (Directory.Exists(path)) Shell(path);
    }

    public static string? RevealTarget(PinItem item)
    {
        if (item.IsLauncher || item.IsRaw || item.ResolvedTarget is not { } target) return null;
        return target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ShellLinks.ReadTarget(target) ?? target : target;
    }

    public static string Downloads()
    {
        try
        {
            var id = DownloadsId;
            var ptr = SHGetKnownFolderPath(ref id, 0, 0);
            var path = Marshal.PtrToStringUni(ptr);
            Marshal.FreeCoTaskMem(ptr);
            if (path is { Length: > 0 }) return path;
        }
        catch (COMException) { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    static bool Shell(string file, string? args = null)
    {
        try
        {
            var info = new ProcessStartInfo(file) { UseShellExecute = true, Arguments = args ?? "" };
            if (Path.IsPathRooted(file) && Path.GetDirectoryName(file) is { } dir && Directory.Exists(dir)) info.WorkingDirectory = dir;
            Process.Start(info)?.Dispose();
            return true;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or FileNotFoundException) { return false; }
    }
}
