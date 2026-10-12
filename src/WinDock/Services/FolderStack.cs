using System.Windows.Threading;
using WinDock.Models;

namespace WinDock.Services;

public sealed record FolderEntry(string Path, string Name, DateTime When, bool IsFolder);

public sealed class FolderStack : IDisposable
{
    static readonly string[] Partial = [".crdownload", ".part", ".partial", ".tmp", ".download", ".opdownload"];

    readonly Settings settings;
    readonly string key;
    readonly FileSystemWatcher? watcher;
    readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public FolderStack(string path, Settings settings)
    {
        Path = path;
        this.settings = settings;
        key = path.ToLowerInvariant();
        if (!settings.FolderSeen.ContainsKey(key))
        {
            settings.FolderSeen[key] = DateTime.Now;
            settings.Save();
        }

        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            Recount();
        };
        try
        {
            watcher = new FileSystemWatcher(path) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
            var dispatcher = Dispatcher.CurrentDispatcher;
            FileSystemEventHandler later = (_, _) => dispatcher.BeginInvoke(() => debounce.Start());
            watcher.Created += later;
            watcher.Deleted += later;
            watcher.Renamed += (_, _) => dispatcher.BeginInvoke(() => debounce.Start());
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is ArgumentException or IOException or PlatformNotSupportedException) { watcher = null; }
        Recount();
    }

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\')) is { Length: > 0 } n ? n : Path;
    public int NewCount { get; private set; }

    public event Action? Changed;

    public static List<FolderEntry> Entries(string path, int max)
    {
        try
        {
            return new DirectoryInfo(path).EnumerateFileSystemInfos()
                .Where(i => (i.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .Where(i => !Partial.Contains(i.Extension.ToLowerInvariant()))
                .Select(i => new FolderEntry(i.FullName, i.Name, i.CreationTime > i.LastWriteTime ? i.CreationTime : i.LastWriteTime, i is DirectoryInfo))
                .OrderByDescending(e => e.When)
                .Take(max)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return []; }
    }

    public void MarkSeen()
    {
        settings.FolderSeen[key] = DateTime.Now;
        settings.Save();
        Recount();
    }

    void Recount()
    {
        var seen = settings.FolderSeen.GetValueOrDefault(key, DateTime.Now);
        var count = Math.Min(99, Entries(Path, 200).Count(e => e.When > seen));
        if (count == NewCount) return;
        NewCount = count;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        debounce.Stop();
        watcher?.Dispose();
    }
}
