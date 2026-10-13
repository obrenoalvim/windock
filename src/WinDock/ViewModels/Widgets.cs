using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

// The square, minimal look of a widget: one value, an optional ring, glyph or picture.
public sealed record CompactFace(string Value, string Caption = "", string? Glyph = null, string? Symbol = null,
    double Ring = -1, System.Windows.Media.ImageSource? Image = null, bool Alert = false, string? Tip = null,
    double InnerRing = -1, System.Windows.Media.Brush? Tint = null, System.Windows.Media.Brush? InnerTint = null)
{
    public bool HasInnerRing => InnerRing >= 0;
    public bool HasTint => Tint is not null;
    public bool HasGlyph => !string.IsNullOrEmpty(Glyph);
    public bool HasSymbol => !string.IsNullOrEmpty(Symbol);
    public bool HasValue => Value.Length > 0;
    public bool HasCaption => Caption.Length > 0;
    public bool HasRing => Ring >= 0;
    public bool HasImage => Image is not null;
}

public abstract partial class WidgetTile(string key, WidgetSpec spec) : DockTile
{
    [ObservableProperty] CompactFace? face;

    protected abstract CompactFace Compact();

    public void RefreshFace() => Face = Compact();

    public override string Key { get; } = key;
    public WidgetSpec Spec { get; private set; } = spec;
    public string Kind => Spec.Kind;
    public virtual bool Wide => true;
    public virtual bool CanReset => false;
    public virtual bool HasPanel => false;
    // Ticks even off screen (another environment), because it has to ring on time.
    public virtual bool KeepsTime => false;

    public event Action<string>? Alert;
    public event Action<WidgetTile>? Edited;

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

    protected void Save() => Edited?.Invoke(this);

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
        WidgetSpec.Claude => new ClaudeWidget(key, spec),
        WidgetSpec.Music => new MusicWidget(key, spec),
        WidgetSpec.Weather => new WeatherWidget(key, spec),
        WidgetSpec.Mic => new MicWidget(key, spec),
        WidgetSpec.Network => new NetworkWidget(key, spec),
        WidgetSpec.Note => new NoteWidget(key, spec),
        _ => new ClockWidget(key, spec)
    };
}

public sealed partial class ClockWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    protected override CompactFace Compact() => new(Time, DateTime.Now.ToString("ddd d", Loc.Instance.Culture).Replace(".", ""), Tip: Date);

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
    protected override CompactFace Compact() => new(Text, Label, Ring: Progress, Tip: Done.Length > 0 ? $"{Label} · {Done}" : Label);

    public const int ShortBreak = 5, LongBreak = 15, Cycle = 4;

    DateTime endsAt;
    TimeSpan left;
    TimeSpan length;
    DateTime day;

    public PomodoroPhase Phase { get; private set; }
    public bool Running { get; private set; }
    public override bool KeepsTime => Running;
    public int Completed { get; private set; }
    public int Today { get; private set; }
    public override bool CanReset => Phase != PomodoroPhase.Idle;

    int Focus => Spec.Minutes is > 0 and <= 180 ? Spec.Minutes.Value : 25;

    [ObservableProperty] string text = "25:00";
    [ObservableProperty] string label = "";
    [ObservableProperty] string done = "";
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
                if (endsAt.Date != day) (day, Today) = (endsAt.Date, 0);
                Today++;
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
        Done = Today > 0 && now.Date == day ? Today.ToString(CultureInfo.InvariantCulture) : "";
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
    protected override CompactFace Compact() => new(Text.Split('.')[0], Label, Glyph: "\uE916", Tip: Label);

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
    protected override CompactFace Compact() => new(Text, Label, Ring: Progress, Tip: Label);

    DateTime endsAt;
    TimeSpan left;
    int minutes = Math.Clamp(spec.Minutes ?? 10, 1, 180);

    public bool Running { get; private set; }
    public override bool KeepsTime => Running;
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
        Save();
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
    protected override CompactFace Compact() => new(Text, Label, Tip: Label);

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
    protected override CompactFace Compact() => new(Text, Label, Ring: Value, Tip: Label);

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
        Save();
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
    protected override CompactFace Compact() => new(CpuText, $"RAM {RamText}", Ring: Cpu, Tip: $"CPU {CpuText} · RAM {RamText}");

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
    protected override CompactFace Compact() => new(Text, Ring: Level, Tip: Tip);

    DateTime last;

    public override bool Wide => false;

    [ObservableProperty] string text = "";
    [ObservableProperty] string glyph = "";
    [ObservableProperty] string tip = "";
    [ObservableProperty] double level;

    public override void Tick(DateTime now)
    {
        if (now - last < TimeSpan.FromSeconds(10) && Text.Length > 0) return;
        last = now;
        var battery = SystemStats.Battery();
        Text = battery.Present ? $"{battery.Percent}%" : Loc.T("plugged");
        Level = battery.Present ? battery.Percent / 100.0 : 1;
        Glyph = battery.Present ? SystemTile.BatteryGlyphFor(battery.Percent, battery.Charging) : "\uE945";
        Tip = SystemTile.DescribeBattery(battery);
    }
}

public sealed partial class ClaudeWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    static readonly System.Windows.Media.Brush SessionTint = Frozen("#E58A68"), WeekTint = Frozen("#F2C3A6"), HotTint = Frozen("#FFFFFF");

    static System.Windows.Media.Brush Frozen(string hex)
    {
        var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    // Two rings, one inside the other: the 5-hour session outside, the week inside. The panel has the numbers.
    protected override CompactFace Compact() => new("", Ring: Session, InnerRing: Week, Alert: Hot, Tip: Tip,
        Tint: Hot ? HotTint : SessionTint, InnerTint: Hot ? HotTint : WeekTint);

    static readonly HashSet<string> Alerted = [];
    static readonly int[] Thresholds = [80, 95];
    static bool primed;

    public override bool HasPanel => true;

    [ObservableProperty] double session;
    [ObservableProperty] double week;
    [ObservableProperty] string sessionText = "—";
    [ObservableProperty] string weekText = "—";
    [ObservableProperty] bool hot;
    [ObservableProperty] bool stale = true;
    [ObservableProperty] string tip = "Claude";

    public override void Tick(DateTime now)
    {
        var usage = ClaudeUsage.Shared;
        usage.Tick(now);
        var limits = usage.Last;
        Session = (limits?.Session ?? 0) / 100;
        Week = (limits?.Week ?? 0) / 100;
        SessionText = limits?.Session is { } s ? $"{s:0}%" : "—";
        WeekText = limits?.Week is { } w ? $"{w:0}%" : "—";
        Hot = Math.Max(limits?.Session ?? 0, limits?.Week ?? 0) >= 90;
        Stale = usage.Status != ClaudeStatus.Ok;
        Tip = limits is null
            ? $"Claude · {Loc.T(StatusKey(usage.Status))}"
            : $"Claude · {Loc.T("claudeSession")} {SessionText} · {Loc.T("claudeWeek")} {WeekText}";
        if (limits is not null && usage.Status == ClaudeStatus.Ok) Warn(limits);
    }

    public static IEnumerable<(string Name, int Threshold, string Key)> Crossings(ClaudeLimits limits)
    {
        foreach (var (name, used, reset) in new[] { ("claudeSession", limits.Session, limits.SessionReset), ("claudeWeek", limits.Week, limits.WeekReset) })
            foreach (var t in Thresholds)
                if (used >= t) yield return (name, t, $"{name}:{t}:{reset:O}");
    }

    void Warn(ClaudeLimits limits)
    {
        foreach (var (name, threshold, key) in Crossings(limits))
            if (Alerted.Add(key) && primed) Raise(Loc.F("claudeAlert", Loc.T(name), threshold));
        primed = true;
    }

    static string StatusKey(ClaudeStatus status) => status switch
    {
        ClaudeStatus.NoLogin => "claudeNoLoginShort",
        ClaudeStatus.Expired => "claudeExpiredShort",
        ClaudeStatus.Offline => "claudeOfflineShort",
        ClaudeStatus.RateLimited => "claudeRateLimitedShort",
        ClaudeStatus.Failed => "claudeFailedShort",
        _ => "claudeLoadingShort"
    };
}

public sealed partial class MusicWidget : WidgetTile
{
    protected override CompactFace Compact() => new("", HasCover ? "" : Title, Glyph: HasCover ? null : "\uE8D6", Image: Cover, Tip: Tip);

    public MusicWidget(string key, WidgetSpec spec) : base(key, spec)
    {
        MediaSessions.Shared.Changed += Show;
        MediaSessions.Shared.Start();
        Show();
    }

    public override bool HasPanel => true;

    [ObservableProperty] string title = "";
    [ObservableProperty] string artist = "";
    [ObservableProperty] System.Windows.Media.ImageSource? cover;
    [ObservableProperty] bool hasCover;
    [ObservableProperty] bool playing;
    [ObservableProperty] bool idle = true;
    [ObservableProperty] string toggleGlyph = "\uE768";
    [ObservableProperty] string tip = "";

    void Show()
    {
        var media = MediaSessions.Shared.Current;
        Idle = media is null;
        Title = media?.Title ?? Loc.T("musicIdle");
        Artist = media?.Artist ?? "";
        Cover = media?.Cover;
        HasCover = Cover is not null;
        Playing = media?.Playing ?? false;
        ToggleGlyph = Playing ? "\uE769" : "\uE768";
        Tip = media is null ? Loc.T("musicIdle") : media.Artist.Length > 0 ? $"{media.Title} · {media.Artist}" : media.Title;
    }

    public override void Tick(DateTime now)
    {
        if (Idle) Title = Loc.T("musicIdle");
    }
}

public sealed partial class WeatherWidget : WidgetTile
{
    protected override CompactFace Compact() => new(Temp, Symbol: Glyph, Tip: Tip);

    public WeatherWidget(string key, WidgetSpec spec) : base(key, spec) => Service.Updated += Show;

    public Weather Service { get; } = new();
    public override bool HasPanel => true;

    [ObservableProperty] string temp = "—";
    [ObservableProperty] string glyph = "\u2601";
    [ObservableProperty] string condition = "";
    [ObservableProperty] string place = "";
    [ObservableProperty] bool day = true;
    [ObservableProperty] bool stale = true;
    [ObservableProperty] string tip = "";

    public override void Tick(DateTime now)
    {
        Service.Tick(now, Spec.Label);
        Show();
    }

    void Show()
    {
        if (Service.Last is not { } report)
        {
            Temp = "—";
            Glyph = "\u2601";
            Condition = Loc.T(StatusKey(Service.Status));
            Place = Service.Place ?? "";
            Day = true;
            Stale = true;
            Tip = Condition;
            return;
        }
        var (glyph, key) = Weather.Describe(report.Code, report.Day);
        Temp = Weather.Degrees(report.Temp);
        Glyph = glyph;
        Condition = Loc.T(key);
        Place = report.Place;
        Day = report.Day;
        Stale = Service.Status != WeatherStatus.Ok;
        Tip = $"{report.Place} · {Condition} · {Temp}";
    }

    public static string StatusKey(WeatherStatus status) => status switch
    {
        WeatherStatus.NoPlace => "weatherNoPlace",
        WeatherStatus.NotFound => "weatherNotFound",
        WeatherStatus.Offline => "weatherOffline",
        _ => "weatherLoading"
    };
}

public sealed partial class MicWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    protected override CompactFace Compact() => new("", Glyph: Glyph, Alert: Muted, Tip: Tip);

    DateTime last;

    public override bool Wide => false;

    [ObservableProperty] string glyph = "";
    [ObservableProperty] bool muted;
    [ObservableProperty] bool present;
    [ObservableProperty] string tip = "";

    public override void Tick(DateTime now)
    {
        if (now - last < TimeSpan.FromSeconds(1)) return;
        Refresh(now);
    }

    public void Refresh(DateTime now)
    {
        last = now;
        var mic = Audio.ReadMic();
        Present = mic is not null;
        Muted = mic?.Muted ?? false;
        Glyph = Audio.MicGlyph(mic);
        Tip = mic is null ? Loc.T("noMic") : $"{mic.Device ?? Loc.T("microphone")} · {Loc.T(mic.Muted ? "micMuted" : "micOn")}";
    }

    public override void Click(DateTime now)
    {
        Audio.ToggleMic();
        Refresh(now);
    }
}

public sealed partial class NetworkWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    protected override CompactFace Compact() => new(Down, $"\u2191 {Up}", Tip: $"\u2193 {Down} \u00B7 \u2191 {Up}");

    readonly NetworkMeter meter = new();
    DateTime last;

    [ObservableProperty] string down = "—";
    [ObservableProperty] string up = "—";

    public override void Tick(DateTime now)
    {
        if (now - last < TimeSpan.FromSeconds(1)) return;
        last = now;
        var (d, u) = meter.Sample(now);
        Down = NetworkMeter.Rate(d);
        Up = NetworkMeter.Rate(u);
    }
}

public sealed partial class NoteWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    protected override CompactFace Compact() => new("", Text.Split('\n')[0], Glyph: "\uE70B", Tip: Text);

    public override bool HasPanel => true;

    [ObservableProperty] string text = "";
    [ObservableProperty] bool empty = true;

    public string Note
    {
        get => Spec.Text ?? "";
        set
        {
            var clean = value.Trim();
            if (clean == Note) return;
            Spec.Text = clean.Length == 0 ? null : clean;
            Save();
            Tick(DateTime.Now);
        }
    }

    public override void Tick(DateTime now)
    {
        Empty = string.IsNullOrWhiteSpace(Spec.Text);
        Text = Empty ? Loc.T("noteEmpty") : Spec.Text!;
    }
}
