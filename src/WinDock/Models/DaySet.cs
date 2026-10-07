namespace WinDock.Models;

public static class DaySet
{
    static readonly string[] Names = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    public static HashSet<DayOfWeek> Parse(string text)
    {
        var set = new HashSet<DayOfWeek>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var ends = part.Split('-', StringSplitOptions.TrimEntries);
            var from = Index(ends[0]);
            var to = ends.Length > 1 ? Index(ends[1]) : from;
            for (var d = from; ; d = (d + 1) % 7)
            {
                set.Add((DayOfWeek)d);
                if (d == to) break;
            }
        }
        return set;
    }

    public static string Format(IEnumerable<DayOfWeek> days) =>
        string.Join(',', days.Order().Select(d => Names[(int)d]).Select(n => char.ToUpperInvariant(n[0]) + n[1..]));

    static int Index(string name)
    {
        var i = name.Length >= 3 ? Array.IndexOf(Names, name[..3].ToLowerInvariant()) : -1;
        return i >= 0 ? i : throw new FormatException($"Unknown day '{name}'");
    }
}
