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
    public const string Claude = "claude";
    public const string Music = "music";
    public const string Network = "network";
    public const string Note = "note";
    public const string Weather = "weather";
    public const string Mic = "mic";
    public const string Todo = "todo";
    public const string SquareSize = "square";
    public const string WideSize = "wide";
    // Music only: the PrimoDock look with every control, and the cover blurred behind everything.
    public const string FullSize = "full";
    public const string BackdropSize = "backdrop";

    public static readonly string[] Kinds = [Clock, Weather, Music, Claude, Pomodoro, Stopwatch, Timer, Countdown, Progress, Activity, Network, Battery, Mic, Note, Todo];

    public string Kind { get; set; } = Clock;
    public string? Label { get; set; }
    public string? Date { get; set; }
    public int? Minutes { get; set; }
    // The note's text; for the to-do list, one task per line, "[x] " in front of the done ones.
    public string? Text { get; set; }
    // "square" or "wide" ("full" and "backdrop" for music); empty follows the default of the Dock tab.
    public string? Size { get; set; }
}
