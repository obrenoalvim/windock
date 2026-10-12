using System.Text.Json.Serialization;

namespace WinDock.Models;

public sealed class Preset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Glyph { get; set; } = "●";
    public string Accent { get; set; } = "#FFB547";
    public List<Slot> Schedule { get; set; } = [];
    public List<PinItem> Items { get; set; } = [];
    public List<WidgetSpec>? Widgets { get; set; }

    [JsonIgnore]
    public string FilePath { get; set; } = "";

    [JsonIgnore]
    public IReadOnlyList<WidgetSpec> ShownWidgets => Widgets ?? [new WidgetSpec { Kind = WidgetSpec.Clock }];
}

public sealed class WidgetSpec
{
    public const string Clock = "clock";
    public const string Pomodoro = "pomodoro";
    public const string Stopwatch = "stopwatch";
    public const string Timer = "timer";
    public const string Countdown = "countdown";
    public const string Progress = "progress";
    public const string Activity = "activity";
    public const string Battery = "battery";

    public static readonly string[] Kinds = [Clock, Pomodoro, Stopwatch, Timer, Countdown, Progress, Activity, Battery];

    public string Kind { get; set; } = Clock;
    public string? Label { get; set; }
    public string? Date { get; set; }
    public int? Minutes { get; set; }
}
