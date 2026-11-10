using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDock.Models;
using WinDock.Services;

namespace WinDock.ViewModels;

public sealed record CompactFace(string Value, string Caption = "", string? Glyph = null, string? Symbol = null,
    double Ring = -1, System.Windows.Media.ImageSource? Image = null, bool Alert = false, string? Tip = null,
    double InnerRing = -1, System.Windows.Media.Brush? Tint = null, System.Windows.Media.Brush? InnerTint = null, double Bar = -1,
    string? Paper = null, bool Dim = false)
{
    public bool HasPaper => Paper is not null;
    public bool HasBar => Bar >= 0;
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

    public bool Square(bool byDefault) => Spec.Size is { } size ? size == WidgetSpec.SquareSize : byDefault && this is not NoteWidget;

    string? applied = spec.Size;

    public void Resize(string size)
    {
        Spec.Size = applied = size;
        Save();
    }

    public bool TakeResize()
    {
        if (applied == Spec.Size) return false;
        applied = Spec.Size;
        return true;
    }

    public override string Key { get; } = key;
    public WidgetSpec Spec { get; private set; } = spec;
    public string Kind => Spec.Kind;
    public bool Wide => !WidgetSpec.SingleSize.Contains(Kind);
    public virtual bool CanReset => false;
    public virtual bool HasPanel => false;
    public virtual bool KeepsTime => false;

    public virtual DateTime Due(DateTime now) => now.AddSeconds(30);

    public DateTime DueAt { get; set; }
    public event Action<WidgetTile>? Woke;

    public void Poke()
    {
        DueAt = DateTime.MinValue;
        Woke?.Invoke(this);
    }

    public static DateTime NextMinute(DateTime now) => now.Date.AddHours(now.Hour).AddMinutes(now.Minute + 1);

    public static DateTime NextSecond(DateTime now, TimeSpan remaining)
    {
        var part = remaining.Ticks % TimeSpan.TicksPerSecond;
        return now.AddTicks(part > 0 ? part : TimeSpan.TicksPerSecond);
    }

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

    protected static readonly TimeSpan Second = TimeSpan.FromMilliseconds(900);

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
        WidgetSpec.Todo => new TodoWidget(key, spec),
        WidgetSpec.Rigdeck => new RigdeckWidget(key, spec),
        WidgetSpec.Steam => new SteamWidget(key, spec),
        WidgetSpec.Epic => new EpicWidget(key, spec),
        WidgetSpec.Uninstall => new UninstallWidget(key, spec),
        WidgetSpec.Convert => new ConvertWidget(key, spec),
        WidgetSpec.Shelf => new ShelfWidget(key, spec),
        WidgetSpec.Cleanup => new CleanupWidget(key, spec),
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

    public override DateTime Due(DateTime now) => NextMinute(now);
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
    public override DateTime Due(DateTime now) => Running ? NextSecond(now, endsAt - now) : DateTime.MaxValue;
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
    public override DateTime Due(DateTime now) => Running ? now.AddMilliseconds(200) : DateTime.MaxValue;
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
    public override DateTime Due(DateTime now) => Running ? NextSecond(now, endsAt - now) : DateTime.MaxValue;
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

    public override DateTime Due(DateTime now) => now.Date.AddDays(1);

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

    public override DateTime Due(DateTime now)
    {
        var days = Periods[period] switch { "week" => 7, "month" => 30, "year" => 365, _ => 1 };
        return now.AddMinutes(Math.Max(1, days * 24 * 60 / 200.0));
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

    public override DateTime Due(DateTime now) => now.AddSeconds(1);

    [ObservableProperty] double cpu;
    [ObservableProperty] double ram;
    [ObservableProperty] string cpuText = "";
    [ObservableProperty] string ramText = "";

    public override void Tick(DateTime now)
    {
        if (now - last < Second) return;
        last = now;
        var c = stats.Cpu();
        var r = SystemStats.Ram();
        Cpu = c / 100;
        Ram = r / 100;
        CpuText = $"{c:0}%";
        RamText = $"{r:0}%";
    }
}
public sealed partial class BatteryWidget : WidgetTile
{
    protected override CompactFace Compact() => new(Text, Ring: Level, Tip: Tip);

    public BatteryWidget(string key, WidgetSpec spec) : base(key, spec) =>
        Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) =>
        {
            if (e.Mode == Microsoft.Win32.PowerModes.StatusChange) System.Windows.Application.Current?.Dispatcher.BeginInvoke(Poke);
        };

    public override DateTime Due(DateTime now) => Present ? now.AddMinutes(1) : DateTime.MaxValue;

    public bool Present { get; private set; }


    [ObservableProperty] string text = "";
    [ObservableProperty] string glyph = "";
    [ObservableProperty] string tip = "";
    [ObservableProperty] double level;

    public override void Tick(DateTime now)
    {
        var battery = SystemStats.Battery();
        Present = battery.Present;
        Text = battery.Present ? $"{battery.Percent}%" : Loc.T("plugged");
        Level = battery.Present ? battery.Percent / 100.0 : 1;
        Glyph = battery.Present ? SystemTile.BatteryGlyphFor(battery.Percent, battery.Charging) : "\uE945";
        Tip = SystemTile.DescribeBattery(battery);
    }
}

public sealed partial class ClaudeWidget : WidgetTile
{
    public ClaudeWidget(string key, WidgetSpec spec) : base(key, spec) => ClaudeUsage.Shared.Updated += Poke;

    public override DateTime Due(DateTime now) => now.AddMinutes(1);

    static readonly System.Windows.Media.Brush SessionTint = Frozen("#E58A68"), WeekTint = Frozen("#F2C3A6"), HotTint = Frozen("#FFFFFF");

    static System.Windows.Media.Brush Frozen(string hex)
    {
        var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    protected override CompactFace Compact() => new(ClaudeUsage.Shared.Last is null ? "—" : "", Ring: Session, InnerRing: Week, Alert: Hot, Tip: Tip, Dim: Stale,
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
                if (used >= t) yield return (name, t, $"{name}:{t}:{Window(reset):O}");
    }

    // resets_at carries sub-second jitter between fetches; round so one window is one alert key
    static DateTime? Window(DateTime? reset) =>
        reset is { } r ? new DateTime((r.Ticks + TimeSpan.TicksPerMinute * 30) / TimeSpan.TicksPerHour * TimeSpan.TicksPerHour, r.Kind) : null;

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
    protected override CompactFace Compact() =>
        new("", HasCover ? "" : Title, Glyph: HasCover ? null : "\uE8D6", Image: Cover, Tip: Tip, Bar: Idle ? -1 : Progress);

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
    [ObservableProperty] System.Windows.Media.ImageSource? backdrop;
    [ObservableProperty] bool hasCover;
    [ObservableProperty] double progress = -1;
    [ObservableProperty] bool hasProgress;
    [ObservableProperty] bool playing;
    [ObservableProperty] bool idle = true;
    [ObservableProperty] string toggleGlyph = "\uE768";
    [ObservableProperty] string tip = "";

    public override DateTime Due(DateTime now) => Playing && HasProgress ? now.AddSeconds(1) : DateTime.MaxValue;

    void Show()
    {
        var media = MediaSessions.Shared.Current;
        Idle = media is null;
        Title = media?.Title ?? Loc.T("musicIdle");
        Artist = media?.Artist ?? "";
        if (!ReferenceEquals(Cover, media?.Cover)) Backdrop = Smear(media?.Cover);
        Cover = media?.Cover;
        HasCover = Cover is not null;
        Track(media);
        Playing = media?.Playing ?? false;
        ToggleGlyph = Playing ? "\uE769" : "\uE768";
        Tip = media is null ? Loc.T("musicIdle") : media.Artist.Length > 0 ? $"{media.Title} · {media.Artist}" : media.Title;
        Poke();
    }

    public override void Tick(DateTime now)
    {
        if (Idle) Title = Loc.T("musicIdle");
        Track(MediaSessions.Shared.Current);
    }

    void Track(MediaInfo? media)
    {
        var p = MediaSessions.Progress(media);
        Progress = p < 0 ? p : Math.Round(p * 200) / 200;
        HasProgress = Progress >= 0;
    }

    static readonly System.Windows.Media.Brush Plate = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2F, 0x3A));

    public static System.Windows.Media.ImageSource? Smear(System.Windows.Media.ImageSource? cover)
    {
        if (cover is null) return null;
        var visual = new System.Windows.Media.DrawingVisual();
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(visual, System.Windows.Media.BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Plate, null, new System.Windows.Rect(0, 0, 8, 3));
            dc.DrawImage(cover, new System.Windows.Rect(-4, -6.5, 16, 16));
        }
        var small = new System.Windows.Media.Imaging.RenderTargetBitmap(8, 3, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        small.Render(visual);
        small.Freeze();
        return small;
    }
}

public sealed partial class WeatherWidget : WidgetTile
{
    protected override CompactFace Compact() => new(Temp, Symbol: Glyph, Tip: Tip, Dim: Stale);

    public WeatherWidget(string key, WidgetSpec spec) : base(key, spec) => Service.Updated += Poke;

    public override DateTime Due(DateTime now) => Service.Due;

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
        WeatherStatus.Limited => "weatherLimited",
        _ => "weatherLoading"
    };
}

public sealed partial class MicWidget : WidgetTile
{
    protected override CompactFace Compact() => new("", Glyph: Glyph, Alert: Muted, Tip: Tip);

    public MicWidget(string key, WidgetSpec spec) : base(key, spec)
    {
        Audio.Watch();
        Audio.Changed += Poke;
    }

    public override DateTime Due(DateTime now) => DateTime.MaxValue;


    [ObservableProperty] string glyph = "";
    [ObservableProperty] bool muted;
    [ObservableProperty] bool present;
    [ObservableProperty] string tip = "";

    public override void Tick(DateTime now) => Refresh(now);

    public void Refresh(DateTime now)
    {
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

    public override DateTime Due(DateTime now) => now.AddSeconds(1);

    [ObservableProperty] string down = "—";
    [ObservableProperty] string up = "—";

    public override void Tick(DateTime now)
    {
        if (now - last < Second) return;
        last = now;
        var (d, u) = meter.Sample(now);
        Down = NetworkMeter.Rate(d);
        Up = NetworkMeter.Rate(u);
    }
}

public sealed partial class NoteWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    protected override CompactFace Compact() => new("", Paper: Text, Tip: Text);

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
            Poke();
        }
    }

    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    public override void Tick(DateTime now)
    {
        Empty = string.IsNullOrWhiteSpace(Spec.Text);
        Text = Empty ? Loc.T("noteEmpty") : Spec.Text!;
    }
}

public sealed record TodoItem(string Text, bool Done);

public sealed partial class TodoWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    const string DoneMark = "[x] ", OpenMark = "[ ] ";

    protected override CompactFace Compact()
    {
        var items = Items;
        var value = Left > 0 ? Left.ToString(CultureInfo.InvariantCulture) : items.Count > 0 ? "\u2713" : "+";
        return new(value, Loc.T("todoShort"), Ring: items.Count > 0 ? Progress : -1, Tip: Tip);
    }

    public override bool HasPanel => true;
    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    [ObservableProperty] int left;
    [ObservableProperty] string count = "";
    [ObservableProperty] string next = "";
    [ObservableProperty] double progress;
    [ObservableProperty] string tip = "";

    public IReadOnlyList<TodoItem> Items => Parse(Spec.Text);

    public static List<TodoItem> Parse(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.StartsWith(DoneMark.Trim(), StringComparison.OrdinalIgnoreCase) ? new TodoItem(line[3..].Trim(), true)
                : new TodoItem(line.StartsWith(OpenMark.Trim()) ? line[3..].Trim() : line, false))
            .Where(i => i.Text.Length > 0)
            .ToList();

    public static string? Format(IEnumerable<TodoItem> items)
    {
        var text = string.Join('\n', items.Select(i => (i.Done ? DoneMark : OpenMark) + i.Text.ReplaceLineEndings(" ").Trim()));
        return text.Length == 0 ? null : text;
    }

    public void Set(IEnumerable<TodoItem> items)
    {
        Spec.Text = Format(items);
        Save();
        Tick(DateTime.Now);
        RefreshFace();
    }

    public override void Tick(DateTime now)
    {
        var items = Items;
        Left = items.Count(i => !i.Done);
        Next = items.FirstOrDefault(i => !i.Done)?.Text ?? "";
        Progress = items.Count == 0 ? 0 : (double)(items.Count - Left) / items.Count;
        Count = items.Count == 0 ? Loc.T("todoEmpty") : Left == 0 ? Loc.T("todoAllDone") : Loc.F(Left == 1 ? "todoLeftOne" : "todoLeft", Left);
        Tip = Next.Length > 0 ? $"{Count} · {Next}" : Count;
    }
}

// The games one store has installed. The Steam widget can also list the rest of the library: Spec.Text "all" (anything else is installed only).
public abstract partial class GamesWidget(string key, WidgetSpec spec, string platform) : WidgetTile(key, spec)
{
    protected override CompactFace Compact() => Logo is null ? new(Number, Glyph: "", Tip: Tip) : new("", Image: Logo, Tip: Tip);

    public string Platform { get; } = platform;
    public string Name => Platform;
    public bool IsSteam => Platform == Games.Steam;
    public System.Windows.Media.ImageSource? Logo { get; } = Games.Launcher(platform) is { } exe ? ShellIcons.ForPath(exe, 64) : null;
    public override bool HasPanel => true;
    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    public System.Windows.Media.Brush Back { get; } = Paint(platform == Games.Steam ? "#1B2838" : "#2A2D33");
    public System.Windows.Media.Brush Accent { get; } = Paint(platform == Games.Steam ? "#66C0F4" : "#E6E8EB");

    static System.Windows.Media.Brush Paint(string hex)
    {
        var brush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public bool ShowAll
    {
        get => IsSteam && Spec.Text == "all";
        set
        {
            Spec.Text = value ? "all" : null;
            Save();
        }
    }

    [ObservableProperty] string tip = "";
    [ObservableProperty] string count = "";
    [ObservableProperty] string number = "";

    public override void Tick(DateTime now)
    {
        var n = Games.Installed(Platform).Count;
        Number = n.ToString();
        Count = Loc.F(n == 1 ? "gamesCountOne" : "gamesCount", n);
        Tip = $"{Name} · {Count}";
    }
}

public sealed class SteamWidget(string key, WidgetSpec spec) : GamesWidget(key, spec, Games.Steam);

public sealed class EpicWidget(string key, WidgetSpec spec) : GamesWidget(key, spec, Games.Epic);

public sealed record UninstallRequest(string? Path, string? AppId, string? Name, Action? Removed = null, bool CanTrash = false)
{
    public string Label => Name ?? System.IO.Path.GetFileName(Path?.TrimEnd('\\')) ?? "";
}

public enum UninstallStage { Waiting, Running, Cleaning, Done, Partial, Failed }

public sealed class UninstallJob
{
    UninstallJob(InstalledApp? app, string? trash, IReadOnlyList<string> leftovers, Action? removed)
    {
        App = app;
        Trash = trash;
        Leftovers = leftovers;
        Removed = removed;
        Silent = app is not null && Uninstaller.Silent(app);
    }

    public static UninstallJob Program(InstalledApp app, IReadOnlyList<string> leftovers, Action? removed, string? dropped = null) =>
        new(app, null, leftovers, removed) { Dropped = dropped };

    public string? Dropped { get; private init; }
    public static UninstallJob Recycle(string path, string? folder = null) =>
        folder is null ? new(null, path, [path], null) : new(null, folder, Uninstaller.Remnants(folder, path), null);

    public InstalledApp? App { get; }
    public string? Trash { get; }
    public IReadOnlyList<string> Leftovers { get; }
    public Action? Removed { get; }
    public bool Silent { get; }
    public bool IsTrash => App is null;
    public string Name => App?.Name ?? Path.GetFileName(Trash!.TrimEnd('\\'));
    public UninstallStage Stage { get; set; }
    public DateTime? Started { get; set; }
    public DateTime? Finished { get; set; }
    public int Stuck { get; set; }
    public string? Error { get; set; }

    public bool Same(UninstallJob other) => App is not null
        ? App == other.App
        : Trash is not null && Trash.Equals(other.Trash, StringComparison.OrdinalIgnoreCase);
}

public sealed partial class UninstallWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    public const string TrashGlyph = "";
    const int HistoryLimit = 20;

    protected override CompactFace Compact() => new(queue.Count > 0 ? queue.Count.ToString(CultureInfo.InvariantCulture) : "", Glyph: TrashGlyph, Alert: Busy, Tip: Tip);

    public override bool HasPanel => true;
    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    [ObservableProperty] bool busy;
    [ObservableProperty] int waiting;
    [ObservableProperty] string tip = "";

    readonly List<UninstallJob> queue = [];
    readonly List<UninstallJob> history = [];
    readonly List<UninstallJob> batch = [];
    int searching;
    bool draining;

    public event Action? Changed;

    public IReadOnlyList<UninstallJob> Queue => queue;
    public IReadOnlyList<UninstallJob> History => history;
    public UninstallJob? Current => draining ? queue.FirstOrDefault() : null;
    public bool Searching => searching > 0;
    public int BatchDone => batch.Count;
    public int BatchTotal => batch.Count + queue.Count;

    public double Progress
    {
        get
        {
            if (BatchTotal == 0) return 0;
            var partial = Current?.Stage switch { UninstallStage.Cleaning => 0.75, UninstallStage.Running => 0.35, _ => 0 };
            return (batch.Count + partial) / BatchTotal;
        }
    }

    public override void Tick(DateTime now)
    {
        Busy = draining || searching > 0;
        Waiting = Math.Max(0, queue.Count - (draining ? 1 : 0));
        var status = Current is { } job ? Loc.F("uninstalling", job.Name) : Loc.T(Busy ? "uninstallSearching" : "uninstallHint");
        Tip = Waiting > 0 ? $"{status} · {Loc.F("uninstallQueued", Waiting)}" : status;
    }

    public async Task Add(IReadOnlyList<UninstallRequest> requests, Func<IReadOnlyList<UninstallJob>, bool> confirm)
    {
        if (requests.Count == 0) return;
        searching++;
        Refresh();
        List<UninstallJob> found = [];
        List<string> missing = [];
        try
        {
            await Task.Run(() =>
            {
                var apps = Uninstaller.Installed();
                foreach (var request in requests)
                {
                    var trashable = request.CanTrash && Uninstaller.Trashable(request.Path);
                    var job = Uninstaller.Find(request.Path, request.AppId, request.Name, apps) is { } app
                        ? UninstallJob.Program(app, WithDropped(Uninstaller.Leftovers(app, apps), trashable ? request.Path : null), request.Removed, trashable ? request.Path : null)
                        : trashable ? UninstallJob.Recycle(request.Path!, Uninstaller.PortableFolder(request.Path!, apps)) : null;
                    if (job is null) missing.Add(request.Label);
                    else if (!found.Any(job.Same)) found.Add(job);
                }
            });
        }
        finally
        {
            searching--;
            Refresh();
        }
        if (missing.Count > 0) Raise(Loc.F("uninstallNotFound", string.Join(", ", missing)));
        found.RemoveAll(j => queue.Any(j.Same));
        if (found.Count == 0 || !confirm(found)) return;
        found.RemoveAll(j => queue.Any(j.Same));
        queue.AddRange(found);
        Refresh();
        if (!draining) await Drain();
    }

    static IReadOnlyList<string> WithDropped(List<string> leftovers, string? dropped)
    {
        if (dropped is not null && !leftovers.Any(l => dropped.StartsWith(l.TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase) || dropped.Equals(l, StringComparison.OrdinalIgnoreCase)))
            leftovers.Add(dropped);
        return leftovers;
    }

    public void Cancel(UninstallJob job)
    {
        if (job == Current) return;
        queue.Remove(job);
        Refresh();
    }

    public void ClearHistory()
    {
        history.Clear();
        Refresh();
    }

    async Task Drain()
    {
        draining = true;
        batch.Clear();
        try
        {
            while (queue.Count > 0)
            {
                var job = queue[0];
                await Run(job);
                queue.Remove(job);
                batch.Add(job);
                history.Insert(0, job);
                if (history.Count > HistoryLimit) history.RemoveAt(history.Count - 1);
                Refresh();
            }
        }
        finally
        {
            draining = false;
            Refresh();
        }
        Raise(Summary(batch));
    }

    static string Summary(IReadOnlyList<UninstallJob> jobs)
    {
        if (jobs is [var job]) return Message(job);
        var failed = jobs.Count(j => j.Stage == UninstallStage.Failed);
        return failed == 0 ? Loc.F("uninstallBatch", jobs.Count) : Loc.F("uninstallBatchFailed", jobs.Count - failed, failed);
    }

    public static string Message(UninstallJob job) => (job.IsTrash, job.Stage) switch
    {
        (true, UninstallStage.Done) => Loc.F("uninstallTrashed", job.Name),
        (true, _) => Loc.F("uninstallTrashFailed", job.Name),
        (false, UninstallStage.Done) => Loc.F("uninstallDone", job.Name),
        (false, UninstallStage.Partial) => Loc.F("uninstallDoneLeft", job.Name, job.Stuck),
        _ => job.Error is { } error ? $"{Loc.F("uninstallFailed", job.Name)}: {error}" : Loc.F("uninstallFailed", job.Name)
    };

    async Task Run(UninstallJob job)
    {
        job.Started = DateTime.Now;
        job.Stage = job.IsTrash ? UninstallStage.Cleaning : UninstallStage.Running;
        using var skip = new CancellationTokenSource();
        skipping = skip;
        Refresh();
        try
        {
            if (job.App is { } app && !await Uninstaller.Remove(app, skip.Token))
            {
                job.Stage = UninstallStage.Failed;
                if (job.Dropped is { } dropped && await Task.Run(() => Uninstaller.Clean([dropped])) == 0)
                    job.Error = Loc.T("uninstallOnlyDropped");
                return;
            }
            skipping = null;
            job.Stage = UninstallStage.Cleaning;
            Refresh();
            job.Stuck = await Task.Run(() => Uninstaller.Clean(job.Leftovers, job.App));
            if (!job.IsTrash) job.Removed?.Invoke();
            job.Stage = job.Stuck == 0 ? UninstallStage.Done : job.IsTrash ? UninstallStage.Failed : UninstallStage.Partial;
        }
        catch (OperationCanceledException)
        {
            job.Error = Loc.T("uninstallSkipped");
            job.Stage = UninstallStage.Failed;
        }
        catch (Exception e)
        {
            App.Log(e);
            job.Error = e.Message;
            job.Stage = UninstallStage.Failed;
        }
        finally
        {
            skipping = null;
            job.Finished = DateTime.Now;
        }
    }

    CancellationTokenSource? skipping;

    public bool CanSkip => skipping is not null;

    public void Skip() => skipping?.Cancel();

    void Refresh()
    {
        Tick(DateTime.Now);
        RefreshFace();
        Changed?.Invoke();
    }
}

public enum ConvertStage { Waiting, Running, Done, Failed }

public sealed class ConvertJob(string source, ConvertTarget target, double duration)
{
    public string Source { get; } = source;
    public ConvertTarget Target { get; } = target;
    public double Duration { get; } = duration;
    public string? Output { get; set; }
    public string Name => Path.GetFileName(Source);
    public ConvertStage Stage { get; set; }
    public double Progress { get; set; }
    public DateTime? Started { get; set; }
    public DateTime? Finished { get; set; }
    public string? Error { get; set; }
}

public sealed partial class ConvertWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    public const string ConvertGlyph = "";
    const int HistoryLimit = 20;

    protected override CompactFace Compact() => new(Waiting > 0 ? Waiting.ToString(CultureInfo.InvariantCulture) : "", Glyph: ConvertGlyph, Ring: Busy ? Percent : -1, Tip: Tip);

    public override bool HasPanel => true;
    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    [ObservableProperty] bool busy;
    [ObservableProperty] int waiting;
    [ObservableProperty] double percent;
    [ObservableProperty] string tip = "";

    readonly List<ConvertJob> queue = [];
    readonly List<ConvertJob> history = [];
    int batchDone;
    bool draining;
    CancellationTokenSource? cancel;

    public event Action? Changed;

    public IReadOnlyList<ConvertJob> Queue => queue;
    public IReadOnlyList<ConvertJob> History => history;
    public ConvertJob? Current => draining ? queue.FirstOrDefault() : null;
    public int BatchDone => batchDone;
    public int BatchTotal => batchDone + queue.Count;

    public override void Tick(DateTime now)
    {
        Busy = draining;
        Waiting = Math.Max(0, queue.Count - (draining ? 1 : 0));
        Percent = Current?.Progress ?? 0;
        var status = Current is { } job ? Loc.F("converting", job.Name, (int)(job.Progress * 100)) : Loc.T("convertHint");
        Tip = Waiting > 0 ? $"{status} · {Loc.F("uninstallQueued", Waiting)}" : status;
    }

    public async Task Add(IEnumerable<ConvertJob> jobs)
    {
        var fresh = jobs.Where(j => !queue.Any(q => q.Source == j.Source && q.Target == j.Target)).ToList();
        if (fresh.Count == 0) return;
        queue.AddRange(fresh);
        Refresh();
        if (!draining) await Drain();
    }

    public void Cancel(ConvertJob job)
    {
        if (job == Current) cancel?.Cancel();
        else queue.Remove(job);
        Refresh();
    }

    public void ClearHistory()
    {
        history.Clear();
        Refresh();
    }

    async Task Drain()
    {
        draining = true;
        batchDone = 0;
        var done = new List<ConvertJob>();
        try
        {
            while (queue.Count > 0)
            {
                var job = queue[0];
                await Run(job);
                queue.Remove(job);
                batchDone++;
                done.Add(job);
                history.Insert(0, job);
                if (history.Count > HistoryLimit) history.RemoveAt(history.Count - 1);
                Refresh();
            }
        }
        finally
        {
            draining = false;
            Refresh();
        }
        var failed = done.Count(j => j.Stage == ConvertStage.Failed);
        Raise(done is [var only] ? Message(only) : failed == 0 ? Loc.F("convertBatch", done.Count) : Loc.F("uninstallBatchFailed", done.Count - failed, failed));
    }

    public static string Message(ConvertJob job) => job.Stage == ConvertStage.Done
        ? Loc.F("convertDone", Path.GetFileName(job.Output ?? job.Source))
        : $"{Loc.F("convertFailed", job.Name)}: {job.Error}";

    async Task Run(ConvertJob job)
    {
        job.Started = DateTime.Now;
        job.Stage = ConvertStage.Running;
        using var token = new CancellationTokenSource();
        cancel = token;
        Refresh();
        var progress = new Progress<double>(p =>
        {
            job.Progress = p;
            Refresh();
        });
        try
        {
            job.Output = Converter.OutputFor(job.Source, job.Target);
            job.Error = await Converter.Run(job.Source, job.Output, job.Target, job.Duration, progress, token.Token);
            job.Stage = job.Error is null ? ConvertStage.Done : ConvertStage.Failed;
        }
        catch (OperationCanceledException)
        {
            job.Error = Loc.T("convertCancelled");
            job.Stage = ConvertStage.Failed;
        }
        catch (Exception e)
        {
            App.Log(e);
            job.Error = e.Message;
            job.Stage = ConvertStage.Failed;
        }
        finally
        {
            cancel = null;
            job.Finished = DateTime.Now;
        }
    }

    void Refresh()
    {
        Tick(DateTime.Now);
        RefreshFace();
        Changed?.Invoke();
    }
}

public sealed partial class ShelfWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    public const string ShelfGlyph = "";

    protected override CompactFace Compact() => new(Count > 0 ? Count.ToString(CultureInfo.InvariantCulture) : "", Glyph: ShelfGlyph, Tip: Tip);

    public override bool HasPanel => true;
    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    [ObservableProperty] int count;
    [ObservableProperty] string tip = "";

    public event Action? Changed;

    public IReadOnlyList<string> Files => Parse(Spec.Text);

    public static List<string> Parse(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public int Add(IEnumerable<string> paths)
    {
        var files = Files.ToList();
        var fresh = paths.Where(p => Exists(p) && !files.Contains(p, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (fresh.Count > 0) Set(files.Concat(fresh));
        return fresh.Count;
    }

    public void Remove(IEnumerable<string> paths) => Set(Files.Except(paths, StringComparer.OrdinalIgnoreCase));

    public void Clear() => Set([]);

    public void Prune()
    {
        var files = Files;
        if (files.Any(f => !Exists(f))) Set(files.Where(Exists));
    }

    void Set(IEnumerable<string> files)
    {
        var list = files.ToList();
        Spec.Text = list.Count == 0 ? null : string.Join('\n', list);
        Save();
        Tick(DateTime.Now);
        RefreshFace();
        Changed?.Invoke();
    }

    public override void Tick(DateTime now)
    {
        Count = Files.Count;
        Tip = Count == 0 ? Loc.T("shelfHint") : Loc.F(Count == 1 ? "shelfOne" : "shelfMany", Count);
    }
}

public sealed partial class CleanupWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    public const string BroomGlyph = "";
    static readonly TimeSpan Fresh = TimeSpan.FromMinutes(5);

    protected override CompactFace Compact() => new("", Glyph: BroomGlyph, Alert: Busy, Tip: Tip);

    public override bool HasPanel => true;
    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    [ObservableProperty] bool busy;
    [ObservableProperty] string tip = "";

    public event Action? Changed;

    public IReadOnlyList<CleanupCategory> Categories { get; private set; } = [];
    public HashSet<string> Selected { get; } = [];
    public bool Scanning { get; private set; }
    public bool Cleaning { get; private set; }
    public CleanupResult? Last { get; private set; }
    DateTime scannedAt;

    public long SelectedSize => Categories.Where(c => Selected.Contains(c.Id) && c.Method != CleanupMethod.Recycle).Sum(c => c.Size);

    public override void Tick(DateTime now)
    {
        Busy = Scanning || Cleaning;
        var total = Categories.Sum(c => c.Size);
        Tip = Busy ? Loc.T(Scanning ? "cleanupScanning" : "cleanupCleaning")
            : Categories.Count == 0 ? Loc.T("cleanupHint")
            : Loc.F("cleanupFound", Cleanup.Bytes(total));
    }

    public async Task Scan(bool force = false)
    {
        if (Busy || !force && Categories.Count > 0 && DateTime.Now - scannedAt < Fresh) return;
        Scanning = true;
        Refresh();
        try
        {
            Categories = await Task.Run(Cleanup.Scan);
            scannedAt = DateTime.Now;
            Selected.Clear();
            Selected.UnionWith(Categories.Where(c => c.Suggested && !c.IsEmpty).Select(c => c.Id));
        }
        catch (Exception e)
        {
            App.Log(e);
            Raise($"{Loc.T("cleanupFailed")}: {e.Message}");
        }
        finally
        {
            Scanning = false;
            Refresh();
        }
    }

    public void Toggle(string id)
    {
        if (!Selected.Remove(id)) Selected.Add(id);
        Refresh();
    }

    public async Task Clean()
    {
        if (Busy) return;
        var chosen = Categories.Where(c => Selected.Contains(c.Id) && !c.IsEmpty).ToList();
        if (chosen.Count == 0) return;
        Cleaning = true;
        Refresh();
        long freed = 0, left = 0;
        var recycled = 0;
        try
        {
            foreach (var category in chosen)
            {
                var result = await Task.Run(() => Cleanup.Clean(category));
                freed += result.Freed;
                left += result.Left;
                recycled += result.Recycled;
            }
            Last = new CleanupResult(freed, recycled, left);
            Raise(Summary(Last));
        }
        catch (Exception e)
        {
            App.Log(e);
            Raise($"{Loc.T("cleanupFailed")}: {e.Message}");
        }
        finally
        {
            Cleaning = false;
            Categories = [];
            Refresh();
        }
        await Scan(force: true);
    }

    public static string Summary(CleanupResult result)
    {
        var parts = new List<string> { Loc.F("cleanupFreed", Cleanup.Bytes(result.Freed)) };
        if (result.Recycled > 0) parts.Add(Loc.F("cleanupRecycled", result.Recycled));
        if (result.Left > 0) parts.Add(Loc.F("cleanupLeft", Cleanup.Bytes(result.Left)));
        return string.Join(" · ", parts);
    }

    void Refresh()
    {
        Tick(DateTime.Now);
        RefreshFace();
        Changed?.Invoke();
    }
}

// RigDeck inside the dock: shortcuts that run one or more steps (open apps on a monitor, commands, key combos, PC controls...).
// The tile is the brand mark; clicking it opens the panel with every shortcut.
public sealed partial class RigdeckWidget(string key, WidgetSpec spec) : WidgetTile(key, spec)
{
    protected override CompactFace Compact() => new("", Image: Logo, Tip: Tip);

    public override bool HasPanel => true;
    public override DateTime Due(DateTime now) => DateTime.MaxValue;

    public System.Windows.Media.ImageSource Logo => RigLogo.Image;

    [ObservableProperty] string tip = "RigDeck";
    [ObservableProperty] string count = "";

    string? parsedFrom;
    IReadOnlyList<RigAction> parsed = [];

    // parsed once per saved text: the panel asks for the list several times each time it opens
    public IReadOnlyList<RigAction> Actions
    {
        get
        {
            if (parsedFrom != Spec.Text || parsed.Count == 0 && Spec.Text is not null)
            {
                parsed = RigStore.Parse(Spec.Text);
                parsedFrom = Spec.Text;
            }
            return parsed;
        }
    }

    public void Set(IEnumerable<RigAction> actions)
    {
        Spec.Text = RigStore.Format(actions);
        Save();
        Tick(DateTime.Now);
        RefreshFace();
    }

    static readonly Dictionary<string, System.Windows.Media.ImageSource> Icons = [];

    // shell icons are slow to pull; keep the ones that resolved
    public static System.Windows.Media.ImageSource? IconOf(RigAction action)
    {
        if ((action.Icon ?? action.FirstOpen?.Icon ?? action.FirstOpen?.Target) is not { } source) return null;
        source = Environment.ExpandEnvironmentVariables(source);
        if (Icons.TryGetValue(source, out var known)) return known;
        var icon = ShellIcons.ForPath(source);
        if (icon is not null) Icons[source] = icon;
        return icon;
    }

    public override void Tick(DateTime now)
    {
        var n = Actions.Count;
        Count = n == 0 ? Loc.T("rigdeckFirst") : Loc.F(n == 1 ? "rigdeckOne" : "rigdeckMany", n);
        Tip = n == 0 ? "RigDeck" : $"RigDeck · {Count}";
    }
}
