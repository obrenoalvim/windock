using System.Text.Json;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.Tests;

public class ScheduleTests
{
    static Preset P(string id, params (string days, string start, string end)[] slots) => new()
    {
        Id = id,
        Schedule = slots.Select(s => new Slot { Days = s.days, Start = s.start, End = s.end }).ToList()
    };

    static readonly List<Preset> Presets =
    [
        P("work", ("Mon-Fri", "09:00", "18:00")),
        P("games", ("Mon-Fri", "18:00", "24:00"), ("Mon-Fri", "00:00", "09:00")),
        P("weekend", ("Sat,Sun", "00:00", "24:00")),
        P("manual")
    ];

    [Theory]
    [InlineData("2026-10-07 11:30", "work")]
    [InlineData("2026-10-07 09:00", "work")]
    [InlineData("2026-10-07 17:59", "work")]
    [InlineData("2026-10-07 18:00", "games")]
    [InlineData("2026-10-07 23:59", "games")]
    [InlineData("2026-10-08 00:00", "games")]
    [InlineData("2026-10-08 08:59", "games")]
    [InlineData("2026-10-10 14:00", "weekend")]
    [InlineData("2026-10-11 23:59", "weekend")]
    public void At_picks_the_slot_covering_the_instant(string when, string expected) =>
        Assert.Equal(expected, ScheduleResolver.At(Presets, DateTime.Parse(when))?.Id);

    [Fact]
    public void Next_finds_the_following_boundary()
    {
        var next = ScheduleResolver.Next(Presets, DateTime.Parse("2026-10-07 11:30"));
        Assert.Equal("games", next?.To?.Id);
        Assert.Equal(DateTime.Parse("2026-10-07 18:00"), next?.At);
    }

    [Fact]
    public void Next_crosses_into_the_weekend()
    {
        var next = ScheduleResolver.Next(Presets, DateTime.Parse("2026-10-09 20:00"));
        Assert.Equal("weekend", next?.To?.Id);
        Assert.Equal(DateTime.Parse("2026-10-10 00:00"), next?.At);
    }

    [Theory]
    [InlineData("Mon-Fri", 5)]
    [InlineData("Sat,Sun", 2)]
    [InlineData("Fri-Mon", 4)]
    [InlineData("Mon-Sun", 7)]
    public void DaySet_parses_ranges_and_lists(string text, int count) =>
        Assert.Equal(count, DaySet.Parse(text).Count);

    [Theory]
    [InlineData("Mon-Fri")]
    [InlineData("Sat,Sun")]
    [InlineData("Mon,Wed,Sun")]
    public void DaySet_format_round_trips(string text)
    {
        var parsed = DaySet.Parse(text);
        Assert.Equal(parsed, DaySet.Parse(DaySet.Format(parsed)));
    }

    [Theory]
    [InlineData("9:5", true, "09:05")]
    [InlineData("09:30", true, "09:30")]
    [InlineData("24:00", true, "24:00")]
    [InlineData("24:30", false, "")]
    [InlineData("abc", false, "")]
    public void Slot_time_input_is_validated(string text, bool ok, string expected)
    {
        Assert.Equal(ok, Slot.TryNormalize(text, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Bundled_presets_deserialize()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Presets");
        var files = Directory.GetFiles(dir, "*.json");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var p = JsonSerializer.Deserialize<Preset>(File.ReadAllText(file), Json.Options);
            Assert.NotNull(p);
            Assert.NotEmpty(p!.Name);
        }
    }
}

