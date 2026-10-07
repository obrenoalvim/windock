using WinDock.Models;

namespace WinDock.Services;

public readonly record struct ScheduleChange(Preset? To, DateTime At);

public static class ScheduleResolver
{
    public static Preset? At(IEnumerable<Preset> presets, DateTime t) =>
        presets.FirstOrDefault(p => p.Schedule.Any(s => s.Contains(t)));

    public static ScheduleChange? Next(IReadOnlyList<Preset> presets, DateTime now)
    {
        var current = At(presets, now);
        var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        for (var i = 1; i <= 8 * 24 * 60; i++)
        {
            var t = start.AddMinutes(i);
            var p = At(presets, t);
            if (p?.Id != current?.Id) return new ScheduleChange(p, t);
        }
        return null;
    }
}
