using System.Text.Json.Serialization;

namespace WinDock.Models;

public sealed class Slot
{
    HashSet<DayOfWeek>? days;

    public string Days { get; set; } = "Mon-Sun";
    public string Start { get; set; } = "00:00";
    public string End { get; set; } = "24:00";

    [JsonIgnore]
    HashSet<DayOfWeek> ParsedDays => days ??= DaySet.Parse(Days);

    public bool Contains(DateTime t)
    {
        if (!ParsedDays.Contains(t.DayOfWeek)) return false;
        var minute = t.Hour * 60 + t.Minute;
        return minute >= Minutes(Start) && minute < Minutes(End);
    }

    static int Minutes(string hhmm)
    {
        var p = hhmm.Split(':');
        return int.Parse(p[0]) * 60 + int.Parse(p[1]);
    }
}
