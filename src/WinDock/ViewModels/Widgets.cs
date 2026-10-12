using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public abstract class WidgetTile(string key, WidgetSpec spec) : DockTile
{
    public override string Key { get; } = key;
    public WidgetSpec Spec { get; private set; } = spec;
    public string Kind => Spec.Kind;
    public virtual bool Wide => true;
    public virtual bool CanReset => false;

    public event Action<string>? Alert;

    public abstract void Tick(DateTime now);
    public virtual void Click(DateTime now) { }
    public virtual void Reset() { }
    public virtual bool Wheel(int steps) => false;

    public void Apply(WidgetSpec spec)
    {
        Spec = spec;
        OnApply();
    }

    protected virtual void OnApply() { }

    protected void Raise(string text) => Alert?.Invoke(text);

    public static string Clock(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";

    public static WidgetTile Create(string key, WidgetSpec spec) => spec.Kind switch
    {
        WidgetSpec.Pomodoro => new PomodoroWidget(key, spec),
        WidgetSpec.Stopwatch => new StopwatchWidget(key, spec),
        WidgetSpec.Timer => new TimerWidget(key, spec),
        WidgetSpec.Countdown => new CountdownWidget(key, spec),
        WidgetSpec.Progress => new ProgressWidget(key, spec),
        WidgetSpec.Activity => new ActivityWidget(key, spec),
        WidgetSpec.Battery => new BatteryWidget(key, spec),
        _ => new ClockWidget(key, spec)
    };
}

public sealed partial class ClockWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    [ObservableProperty] string time = "";
    [ObservableProperty] string date = "";

    public override void Tick(DateTime now)
    {
        var culture = Loc.Instance.Culture;
        var pt = Loc.Instance.Language == Loc.Portuguese;
        Time = now.ToString(pt ? "HH:mm" : "h:mm", culture);
        Date = now.ToString(pt ? "ddd, d MMM" : "ddd, MMM d", culture).Replace(".", "");
    }

    public override void Click(DateTime now) => ShellGuard.Notifications();
}

public enum PomodoroPhase { Idle, Focus, Break }

public sealed partial class PomodoroWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    public const int ShortBreak = 5, LongBreak = 15, Cycle = 4;

    DateTime endsAt;
    TimeSpan left;
    TimeSpan length;

    public PomodoroPhase Phase { get; private set; }
    public bool Running { get; private set; }
    public int Completed { get; private set; }
    public override bool CanReset => Phase != PomodoroPhase.Idle;

    int Focus => Spec.Minutes is > 0 and <= 180 ? Spec.Minutes.Value : 25;

    [ObservableProperty] string text = "25:00";
    [ObservableProperty] string label = "";
    [ObservableProperty] double progress;
    [ObservableProperty] bool isBreak;

    public override void Click(DateTime now)
    {
        if (Phase == PomodoroPhase.Idle) Begin(PomodoroPhase.Focus, TimeSpan.FromMinutes(Focus), now);
        else if (Running)
        {
            left = endsAt - now;
            Running = false;
        }
        else
        {
            endsAt = now + left;
            Running = true;
        }
        Tick(now);
    }

    public override void Reset()
    {
        Phase = PomodoroPhase.Idle;
        Running = false;
        Tick(DateTime.Now);
    }

    void Begin(PomodoroPhase phase, TimeSpan duration, DateTime now)
    {
        Phase = phase;
        length = duration;
        endsAt = now + duration;
        left = duration;
        Running = true;
    }

    public override void Tick(DateTime now)
    {
        if (Running && now >= endsAt)
        {
            if (Phase == PomodoroPhase.Focus)
            {
                Completed++;
                var pause = Completed % Cycle == 0 ? LongBreak : ShortBreak;
                Begin(PomodoroPhase.Break, TimeSpan.FromMinutes(pause), endsAt);
                Raise(Loc.F("pomodoroBreak", pause));
            }
            else
            {
                Phase = PomodoroPhase.Idle;
                Running = false;
                Raise(Loc.T("pomodoroFocus"));
            }
        }

        var remaining = Phase == PomodoroPhase.Idle ? TimeSpan.FromMinutes(Focus) : Running ? endsAt - now : left;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        Text = Clock(remaining + TimeSpan.FromMilliseconds(999));
        Progress = Phase == PomodoroPhase.Idle || length <= TimeSpan.Zero ? 0 : 1 - remaining / length;
        IsBreak = Phase == PomodoroPhase.Break;
        Label = Phase switch
        {
            PomodoroPhase.Focus => Running ? Loc.T("focus") : Loc.T("paused"),
            PomodoroPhase.Break => Running ? Loc.T("break") : Loc.T("paused"),
            _ => Loc.T("pomodoro")
        };
    }
}

public sealed partial class StopwatchWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    DateTime startedAt;
    TimeSpan stored;

    public bool Running { get; private set; }
    public override bool CanReset => Running || stored > TimeSpan.Zero;

    [ObservableProperty] string text = "00:00.0";
    [ObservableProperty] string label = "";

    public TimeSpan Elapsed(DateTime now) => stored + (Running ? now - startedAt : TimeSpan.Zero);

    public override void Click(DateTime now)
    {
        if (Running) stored += now - startedAt;
        else startedAt = now;
        Running = !Running;
        Tick(now);
    }

    public override void Reset()
    {
        Running = false;
        stored = TimeSpan.Zero;
        Tick(DateTime.Now);
    }

    public override void Tick(DateTime now)
    {
        var t = Elapsed(now);
        Text = t.TotalHours >= 1 ? Clock(t) : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";
        Label = Running ? Loc.T("stopwatch") : t > TimeSpan.Zero ? Loc.T("paused") : Loc.T("stopwatchStart");
    }
}

public sealed partial class TimerWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    DateTime endsAt;
    TimeSpan left;
    int minutes = Math.Clamp(spec.Minutes ?? 10, 1, 180);

    public bool Running { get; private set; }
    public bool Started { get; private set; }
    public override bool CanReset => Started;

    [ObservableProperty] string text = "";
    [ObservableProperty] string label = "";
    [ObservableProperty] double progress;

    protected override void OnApply()
    {
        if (!Started) minutes = Math.Clamp(Spec.Minutes ?? minutes, 1, 180);
    }

    public override void Click(DateTime now)
    {
        if (!Started)
        {
            Started = Running = true;
            endsAt = now.AddMinutes(minutes);
        }
        else if (Running)
        {
            left = endsAt - now;
            Running = false;
        }
        else
        {
            endsAt = now + left;
            Running = true;
        }
        Tick(now);
    }

    public override void Reset()
    {
        Started = Running = false;
        Tick(DateTime.Now);
    }

    public override bool Wheel(int steps)
    {
        if (Started) return false;
        minutes = Math.Clamp(minutes + steps, 1, 180);
        Spec.Minutes = minutes;
        Tick(DateTime.Now);
        return true;
    }

    public override void Tick(DateTime now)
    {
        if (Running && now >= endsAt)
        {
            Started = Running = false;
            Raise(Loc.F("timerDone", minutes));
        }
        var total = TimeSpan.FromMinutes(minutes);
        var remaining = !Started ? total : Running ? endsAt - now : left;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        Text = Clock(remaining + TimeSpan.FromMilliseconds(999));
        Progress = Started ? 1 - remaining / total : 0;
        Label = !Started ? Loc.T("timerHint") : Running ? Loc.T("timer") : Loc.T("paused");
    }
}

public sealed partial class CountdownWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    static readonly string[] Fixed = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"];
    static readonly string[] Yearly = ["MM-dd", "dd/MM", "d/M"];

    [ObservableProperty] string text = "";
    [ObservableProperty] string label = "";

    public static DateTime? Target(string? date, DateTime today)
    {
        var day = today.Date;
        var input = string.IsNullOrWhiteSpace(date) ? "12-25" : date.Trim();
        if (DateTime.TryParseExact(input, Fixed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact.Date;
        if (!DateTime.TryParseExact("2000-" + Normalize(input), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var yearly)) return null;
        var next = OnYear(yearly, day.Year);
        return next < day ? OnYear(yearly, day.Year + 1) : next;
    }

    static string Normalize(string input)
    {
        foreach (var f in Yearly)
            if (DateTime.TryParseExact("2000|" + input, "yyyy|" + f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.ToString("MM-dd", CultureInfo.InvariantCulture);
        return input;
    }

    static DateTime OnYear(DateTime monthDay, int year) =>
        new(year, monthDay.Month, Math.Min(monthDay.Day, DateTime.DaysInMonth(year, monthDay.Month)));

    public static int? DaysLeft(string? date, DateTime today) =>
        Target(date, today) is { } t ? Math.Max(0, (t - today.Date).Days) : null;

    public override void Tick(DateTime now)
    {
        var name = string.IsNullOrWhiteSpace(Spec.Label) ? (string.IsNullOrWhiteSpace(Spec.Date) ? Loc.T("christmas") : "") : Spec.Label!;
        if (DaysLeft(Spec.Date, now) is not { } days)
        {
            Text = "?";
            Label = Loc.T("countdownBad");
            return;
        }
        Text = days.ToString(CultureInfo.InvariantCulture);
        Label = days switch
        {
            0 => name.Length > 0 ? Loc.F("todayIs", name) : Loc.T("today"),
            1 => name.Length > 0 ? Loc.F("dayTo", name) : Loc.T("dayLeft"),
            _ => name.Length > 0 ? Loc.F("daysTo", name) : Loc.T("daysLeft")
        };
    }
}

public sealed partial class ProgressWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    public static readonly string[] Periods = ["day", "week", "month", "year"];

    int period = Math.Max(0, Array.IndexOf(Periods, spec.Label ?? "day"));

    [ObservableProperty] string text = "";
    [ObservableProperty] string label = "";
    [ObservableProperty] double value;

    public static double Fraction(string period, DateTime now)
    {
        var (start, end) = period switch
        {
            "week" => (now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)), now.Date.AddDays(7 - (((int)now.DayOfWeek + 6) % 7))),
            "month" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
            "year" => (new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1)),
            _ => (now.Date, now.Date.AddDays(1))
        };
        return Math.Clamp((now - start) / (end - start), 0, 1);
    }

    public override void Click(DateTime now)
    {
        period = (period + 1) % Periods.Length;
        Spec.Label = Periods[period];
        Tick(now);
    }

    public override void Tick(DateTime now)
    {
        var f = Fraction(Periods[period], now);
        Value = f;
        Text = $"{Math.Floor(f * 100):0}%";
        Label = Loc.T("progress_" + Periods[period]);
    }
}

public sealed partial class ActivityWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    readonly SystemStats stats = new();
    DateTime last;

    [ObservableProperty] double cpu;
    [ObservableProperty] double ram;
    [ObservableProperty] string cpuText = "";
    [ObservableProperty] string ramText = "";

    public override void Tick(DateTime now)
    {
        if (now - last < TimeSpan.FromSeconds(1)) return;
        last = now;
        var c = stats.Cpu();
        var r = SystemStats.Ram();
        Cpu = c / 100;
        Ram = r / 100;
        CpuText = $"{c:0}%";
        RamText = $"{r:0}%";
    }
}

public sealed partial class BatteryWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    DateTime last;

    public override bool Wide => false;

    [ObservableProperty] string text = "";
    [ObservableProperty] string glyph = "";
    [ObservableProperty] double level;

    public override void Tick(DateTime now)
    {
        if (now - last < TimeSpan.FromSeconds(10) && Text.Length > 0) return;
        last = now;
        var (present, percent, charging) = SystemStats.Battery();
        Text = present ? $"{percent}%" : Loc.T("plugged");
        Level = present ? percent / 100.0 : 1;
        Glyph = present ? SystemTile.BatteryGlyphFor(percent, charging) : "";
    }
}
