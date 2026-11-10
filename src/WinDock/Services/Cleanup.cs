using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDock.Services;

public enum CleanupMethod { Delete, Recycle, EmptyBin }

public sealed record CleanupCategory(string Id, CleanupMethod Method, bool Suggested, IReadOnlyList<string> Paths, long Size, int Count, IReadOnlyList<string> Busy)
{
    public string Label => Loc.T("cleanup_" + Id);
    public bool IsEmpty => Size == 0 && Count == 0;
}

public sealed record CleanupResult(long Freed, int Recycled, long Left);

public static class Cleanup
{
    const uint EmptyQuietly = 0x1 | 0x2 | 0x4;
    static readonly TimeSpan TempAge = TimeSpan.FromDays(1);
    static readonly TimeSpan InstallerAge = TimeSpan.FromDays(30);
    static readonly string[] InstallerExtensions = [".exe", ".msi", ".msix", ".msixbundle", ".appx"];
    static readonly string[] ChromiumCaches = ["Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage"];

    static readonly (string Process, string Name, string UserData)[] Chromium =
    [
        ("chrome", "Chrome", @"Google\Chrome\User Data"),
        ("msedge", "Edge", @"Microsoft\Edge\User Data"),
        ("brave", "Brave", @"BraveSoftware\Brave-Browser\User Data"),
        ("vivaldi", "Vivaldi", @"Vivaldi\User Data"),
        ("opera", "Opera", @"Opera Software\Opera Stable")
    ];

    static readonly EnumerationOptions Everything = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    [StructLayout(LayoutKind.Sequential)]
    struct RecycleBinInfo
    {
        public int Size;
        public long Bytes;
        public long Items;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHQueryRecycleBin(string? root, ref RecycleBinInfo info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHEmptyRecycleBin(nint window, string? root, uint flags);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern nint StrFormatByteSizeW(long size, StringBuilder buffer, int length);

    public static string Bytes(long size)
    {
        var buffer = new StringBuilder(32);
        StrFormatByteSizeW(size, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static List<CleanupCategory> Scan() =>
    [
        RecycleBin(),
        Category("temp", CleanupMethod.Delete, true, Old(Path.GetTempPath(), TempAge, null)),
        Category("shortcuts", CleanupMethod.Recycle, true, DeadShortcuts().ToList()),
        Category("crashes", CleanupMethod.Delete, true, Existing(
            Path.Combine(Local, "CrashDumps"),
            Path.Combine(Local, @"Microsoft\Windows\WER\ReportArchive"),
            Path.Combine(Local, @"Microsoft\Windows\WER\ReportQueue"))),
        Browsers(),
        Category("dev", CleanupMethod.Delete, false, Existing(
            Path.Combine(Local, "npm-cache"),
            Path.Combine(Local, @"Yarn\Cache"),
            Path.Combine(Local, @"pip\Cache"),
            Path.Combine(Home, @".nuget\packages"),
            Path.Combine(Home, @".gradle\caches"))),
        Category("installers", CleanupMethod.Recycle, false, Old(Launcher.Downloads(), InstallerAge, InstallerExtensions))
    ];

    static CleanupCategory RecycleBin()
    {
        var info = new RecycleBinInfo { Size = Marshal.SizeOf<RecycleBinInfo>() };
        var ok = SHQueryRecycleBin(null, ref info) == 0;
        return new("bin", CleanupMethod.EmptyBin, false, [], ok ? info.Bytes : 0, ok ? (int)info.Items : 0, []);
    }

    static CleanupCategory Category(string id, CleanupMethod method, bool suggested, List<string> paths, IReadOnlyList<string>? busy = null) =>
        new(id, method, suggested, paths, paths.Sum(SizeOf), paths.Count, busy ?? []);

    static List<string> Existing(params string[] folders) =>
        folders.Where(Directory.Exists).SelectMany(Children).ToList();

    static IEnumerable<string> Children(string folder)
    {
        try { return Directory.EnumerateFileSystemEntries(folder).ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    static List<string> Old(string folder, TimeSpan age, string[]? extensions)
    {
        var limit = DateTime.Now - age;
        return Children(folder)
            .Where(p => extensions is null || extensions.Contains(Path.GetExtension(p).ToLowerInvariant()) && File.Exists(p))
            .Where(p => LastWrite(p) < limit)
            .ToList();
    }

    static DateTime LastWrite(string path)
    {
        try { return Directory.Exists(path) ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return DateTime.MaxValue; }
    }

    public static long SizeOf(string path)
    {
        try
        {
            if (File.Exists(path)) return new FileInfo(path).Length;
            if (!Directory.Exists(path)) return 0;
            return new DirectoryInfo(path).EnumerateFiles("*", Everything).Sum(f => f.Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    static IEnumerable<string> DeadShortcuts()
    {
        var roots = new[]
        {
            (Environment.SpecialFolder.DesktopDirectory, false),
            (Environment.SpecialFolder.CommonDesktopDirectory, false),
            (Environment.SpecialFolder.Programs, true),
            (Environment.SpecialFolder.CommonPrograms, true)
        };
        foreach (var (folder, deep) in roots)
        {
            var dir = Environment.GetFolderPath(folder);
            if (!Directory.Exists(dir)) continue;
            var options = new EnumerationOptions { RecurseSubdirectories = deep, IgnoreInaccessible = true };
            foreach (var link in Directory.EnumerateFiles(dir, "*.lnk", options))
                if (Dead(ShellLinks.ReadTarget(link))) yield return link;
        }
    }

    public static bool Dead(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || !Path.IsPathFullyQualified(target) || target.StartsWith(@"\\")) return false;
        var drive = new DriveInfo(Path.GetPathRoot(target)!);
        if (!drive.IsReady || drive.DriveType != DriveType.Fixed) return false;
        return !File.Exists(target) && !Directory.Exists(target);
    }

    static CleanupCategory Browsers()
    {
        var running = Process.GetProcesses().Select(p => p.ProcessName.ToLowerInvariant()).ToHashSet();
        var busy = new List<string>();
        var paths = new List<string>();
        foreach (var (process, name, userData) in Chromium)
        {
            var root = Path.Combine(Local, userData);
            if (!Directory.Exists(root)) continue;
            if (running.Contains(process))
            {
                busy.Add(name);
                continue;
            }
            var profiles = Directory.EnumerateDirectories(root).Where(d => Path.GetFileName(d) is "Default" or "Guest Profile" || Path.GetFileName(d).StartsWith("Profile ")).Append(root);
            paths.AddRange(profiles.SelectMany(p => ChromiumCaches.Select(c => Path.Combine(p, c))).Where(Directory.Exists));
        }
        var firefox = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefox))
        {
            if (running.Contains("firefox")) busy.Add("Firefox");
            else paths.AddRange(Directory.EnumerateDirectories(firefox).Select(p => Path.Combine(p, "cache2")).Where(Directory.Exists));
        }
        return Category("browsers", CleanupMethod.Delete, true, paths, busy);
    }

    public static async Task<CleanupResult> Clean(CleanupCategory category)
    {
        switch (category.Method)
        {
            case CleanupMethod.EmptyBin:
                var hr = SHEmptyRecycleBin(0, null, EmptyQuietly);
                return new(hr == 0 ? category.Size : 0, 0, hr == 0 ? 0 : category.Size);
            case CleanupMethod.Recycle:
                var stuck = await Uninstaller.Clean(category.Paths);
                return new(0, category.Paths.Count - stuck, 0);
            default:
                long freed = 0, left = 0;
                foreach (var path in category.Paths)
                {
                    var before = SizeOf(path);
                    Delete(path);
                    var after = SizeOf(path);
                    freed += before - after;
                    left += after;
                }
                return new(freed, 0, left);
        }
    }

    static void Delete(string path)
    {
        if (File.Exists(path))
        {
            TryDelete(() => File.Delete(path));
            return;
        }
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.EnumerateFiles(path, "*", Everything).ToList()) TryDelete(() => File.Delete(file));
        foreach (var dir in Directory.EnumerateDirectories(path, "*", Everything).OrderByDescending(d => d.Length).ToList()) TryDelete(() => Directory.Delete(dir));
        TryDelete(() => Directory.Delete(path));
    }

    static void TryDelete(Action delete)
    {
        try { delete(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
