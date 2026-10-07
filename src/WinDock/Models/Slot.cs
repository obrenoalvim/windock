using System.Text.Json.Serialization;

namespace WinDock.Models;

public sealed class Slot
{
    HashSet<DayOfWeek>? parsed;
    string days = "Mon-Sun";

    public string Days
    {
        get => days;
        set { days = value; parsed = null; }
    }

    public string Start { get; set; } = "00:00";
    public string End { get; set; } = "24:00";

    [JsonIgnore]
    HashSet<DayOfWeek> ParsedDays => parsed ??= DaySet.Parse(Days);

    public bool Contains(DateTime t)
    {
        if (!ParsedDays.Contains(t.DayOfWeek)) return false;
        var minute = t.Hour * 60 + t.Minute;
        return minute >= Minutes(Start) && minute < Minutes(End);
    }

    public static bool TryNormalize(string text, out string normalized)
    {
        normalized = "";
        var p = text.Trim().Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) return false;
        if (m is < 0 or > 59 || h < 0 || h > 24 || (h == 24 && m != 0)) return false;
        normalized = $"{h:00}:{m:00}";
        return true;
    }

    static int Minutes(string hhmm)
    {
        var p = hhmm.Split(':');
        return int.Parse(p[0]) * 60 + int.Parse(p[1]);
    }
}
