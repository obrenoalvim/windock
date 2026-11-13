using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace WinDock.Services;

public static partial class Badges
{
    public const int Dot = -1;

    static readonly string Database = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Notifications\wpndatabase.db");
    static DispatcherTimer? poll;

    public static IReadOnlyDictionary<string, int> Current { get; private set; } = new Dictionary<string, int>();

    public static event Action? Changed;

    public static void Start()
    {
        if (poll is not null || !File.Exists(Database)) return;
        Current = Read() ?? Current;
        poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        poll.Tick += (_, _) =>
        {
            if (Read() is not { } next || Same(next, Current)) return;
            Current = next;
            Changed?.Invoke();
        };
        poll.Start();
    }

    static bool Same(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);

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

    static Dictionary<string, int>? Read()
    {
        var badges = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        nint db = 0, statement = 0;
        try
        {
            if (sqlite3_open_v2(Utf8(Database), out db, OpenReadOnly, 0) != 0) return null;
            sqlite3_busy_timeout(db, 300);
            const string sql = "select h.PrimaryId, n.Payload from Notification n join NotificationHandler h on h.RecordId = n.HandlerId where n.Type = 'badge'";
            if (sqlite3_prepare_v2(db, Utf8(sql), -1, out statement, 0) != 0) return null;
            int step;
            while ((step = sqlite3_step(statement)) == Row)
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
            if (step != Done) return null;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return null; }
        finally
        {
            if (statement != 0) sqlite3_finalize(statement);
            if (db != 0) sqlite3_close(db);
        }
        return badges;
    }

    static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + "\0");

    const int OpenReadOnly = 0x1, Row = 100, Done = 101;

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
