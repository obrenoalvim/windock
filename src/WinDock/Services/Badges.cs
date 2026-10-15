using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace WinDock.Services;

public static partial class Badges
{
    public const int Dot = -1;

    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Notifications");
    static readonly string Database = Path.Combine(Folder, "wpndatabase.db");
    static FileSystemWatcher? watcher;
    static DispatcherTimer? debounce;

    public static IReadOnlyDictionary<string, int> Current { get; private set; } = new Dictionary<string, int>();

    public static event Action? Changed;

    public static void Start()
    {
        if (watcher is not null || !File.Exists(Database)) return;
        Current = Read();
        debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            var next = Read();
            if (next.Count == Current.Count && next.All(p => Current.TryGetValue(p.Key, out var v) && v == p.Value)) return;
            Current = next;
            Changed?.Invoke();
        };
        var dispatcher = Dispatcher.CurrentDispatcher;
        watcher = new FileSystemWatcher(Folder, "wpndatabase.db*") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size };
        watcher.Changed += (_, _) => dispatcher.BeginInvoke(() =>
        {
            debounce.Stop();
            debounce.Start();
        });
        watcher.EnableRaisingEvents = true;
    }

    public static int For(IEnumerable<string?> appIds)
    {
        var best = 0;
        foreach (var id in appIds)
            if (id is not null && Current.TryGetValue(id, out var n) && (n > best || (best == 0 && n == Dot))) best = n;
        return best;
    }

    public static int FromTitle(string title) =>
        TitleCount().Match(title) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;

    [GeneratedRegex(@"^\((\d{1,4})\+?\)\s")]
    private static partial Regex TitleCount();

    public static int Parse(string payload)
    {
        var value = BadgeValue().Match(payload);
        if (!value.Success) return 0;
        var text = value.Groups[1].Value;
        if (int.TryParse(text, out var n)) return Math.Max(0, n);
        return text is "none" or "" ? 0 : Dot;
    }

    [GeneratedRegex("value=\"([^\"]*)\"")]
    private static partial Regex BadgeValue();

    static Dictionary<string, int> Read()
    {
        var badges = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        nint db = 0, statement = 0;
        try
        {
            if (sqlite3_open_v2(Utf8(Database), out db, OpenReadOnly, 0) != 0) return badges;
            sqlite3_busy_timeout(db, 300);
            const string sql = "select h.PrimaryId, n.Payload from Notification n join NotificationHandler h on h.RecordId = n.HandlerId where n.Type = 'badge'";
            if (sqlite3_prepare_v2(db, Utf8(sql), -1, out statement, 0) != 0) return badges;
            while (sqlite3_step(statement) == Row)
            {
                var id = Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 0));
                var size = sqlite3_column_bytes(statement, 1);
                var blob = sqlite3_column_blob(statement, 1);
                if (id is null || blob == 0 || size <= 0) continue;
                var bytes = new byte[size];
                Marshal.Copy(blob, bytes, 0, size);
                var count = Parse(Encoding.UTF8.GetString(bytes));
                if (count != 0) badges[id] = count;
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        finally
        {
            if (statement != 0) sqlite3_finalize(statement);
            if (db != 0) sqlite3_close(db);
        }
        return badges;
    }

    static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + "\0");

    const int OpenReadOnly = 0x1, Row = 100;

    [DllImport("winsqlite3.dll")] static extern int sqlite3_open_v2(byte[] filename, out nint db, int flags, nint vfs);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_busy_timeout(nint db, int ms);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_prepare_v2(nint db, byte[] sql, int bytes, out nint statement, nint tail);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_step(nint statement);
    [DllImport("winsqlite3.dll")] static extern nint sqlite3_column_text(nint statement, int column);
    [DllImport("winsqlite3.dll")] static extern nint sqlite3_column_blob(nint statement, int column);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_column_bytes(nint statement, int column);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_finalize(nint statement);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_close(nint db);
}
