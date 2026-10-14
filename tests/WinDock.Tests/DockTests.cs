using System.Text.Json;
using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;
using WinDock.Views;

namespace WinDock.Tests;

public class DockTests
{
    static readonly double[] Widths = [48, 48, 48, 48, 48];
    static readonly bool[] AllLens = [true, true, true, true, true];

    [Fact]
    public void Lens_at_rest_keeps_every_icon_at_its_base_place()
    {
        var layout = LensMath.Layout(Widths, AllLens, 8, 100, 0, 1.8, 140);
        Assert.All(layout.Scale, s => Assert.Equal(1, s));
        Assert.Equal([0, 56, 112, 168, 224], layout.Left);
        Assert.Equal(272, layout.Total);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(250)]
    public void Icon_under_the_pointer_stays_under_the_pointer(double pointer)
    {
        var layout = LensMath.Layout(Widths, AllLens, 8, pointer, 1, 1.8, 140);
        var i = Enumerable.Range(0, 5).Last(k => k * 56 <= pointer);
        var inside = Math.Min(pointer - i * 56, 48);
        var scaled = layout.Left[i] + inside * layout.Scale[i];
        if (pointer - i * 56 <= 48) Assert.Equal(pointer, scaled, 6);
        Assert.True(layout.Scale[i] >= layout.Scale.Where((_, k) => Math.Abs(k - i) > 1).DefaultIfEmpty(1).Max());
    }

    [Fact]
    public void Icon_centered_under_the_pointer_reaches_full_magnification_and_neighbors_shrink_symmetrically()
    {
        var layout = LensMath.Layout(Widths, AllLens, 8, 112 + 24, 1, 1.8, 140);
        Assert.Equal(1.8, layout.Scale[2], 6);
        Assert.Equal(layout.Scale[1], layout.Scale[3], 6);
        Assert.Equal(layout.Scale[0], layout.Scale[4], 6);
        Assert.True(layout.Scale[1] > layout.Scale[0]);
        Assert.True(layout.Total > 272);
    }

    [Fact]
    public void Widgets_and_separators_never_magnify()
    {
        bool[] lens = [true, false, true, false, true];
        var layout = LensMath.Layout(Widths, lens, 8, 80, 1, 2, 200);
        Assert.Equal(1, layout.Scale[1]);
        Assert.Equal(1, layout.Scale[3]);
        Assert.True(layout.Scale[0] > 1);
    }

    [Fact]
    public void Lens_influence_fades_to_zero_at_the_radius()
    {
        Assert.Equal(1, LensMath.Influence(0, 100), 6);
        Assert.Equal(0.5, LensMath.Influence(50, 100), 6);
        Assert.Equal(0, LensMath.Influence(100, 100));
        Assert.Equal(0, LensMath.Influence(140, 100));
    }

    [Theory]
    [InlineData(null, "2026-10-12", 74)]
    [InlineData("12-25", "2026-12-26", 364)]
    [InlineData("25/12", "2026-10-12", 74)]
    [InlineData("2026-11-01", "2026-10-12", 20)]
    [InlineData("01/11/2026", "2026-10-12", 20)]
    [InlineData("2026-01-01", "2026-10-12", 0)]
    [InlineData("02-29", "2027-02-01", 27)]
    public void Countdown_counts_days_and_repeats_yearly_dates(string? date, string today, int expected) =>
        Assert.Equal(expected, CountdownWidget.DaysLeft(date, DateTime.Parse(today)));

    [Fact]
    public void Countdown_rejects_garbage()
    {
        Assert.Null(CountdownWidget.DaysLeft("tomorrow", DateTime.Parse("2026-10-12")));
    }

    [Theory]
    [InlineData("day", "2026-10-12 12:00", 0.5)]
    [InlineData("week", "2026-10-12 00:00", 0.0)]
    [InlineData("week", "2026-10-15 12:00", 0.5)]
    [InlineData("month", "2026-11-16 00:00", 0.5)]
    [InlineData("year", "2026-01-01 00:00", 0.0)]
    public void Progress_covers_day_week_month_and_year(string period, string now, double expected) =>
        Assert.Equal(expected, ProgressWidget.Fraction(period, DateTime.Parse(now)), 3);

    [Fact]
    public void Pomodoro_runs_focus_then_break_then_stops()
    {
        var alerts = new List<string>();
        var p = new PomodoroWidget("p", new WidgetSpec { Kind = WidgetSpec.Pomodoro, Minutes = 25 });
        p.Alert += alerts.Add;
        var t0 = new DateTime(2026, 10, 12, 9, 0, 0);

        p.Click(t0);
        Assert.Equal(PomodoroPhase.Focus, p.Phase);
        p.Tick(t0.AddMinutes(10));
        Assert.Equal("15:00", p.Text);

        p.Tick(t0.AddMinutes(25));
        Assert.Equal(PomodoroPhase.Break, p.Phase);
        Assert.Single(alerts);
        Assert.True(p.IsBreak);

        p.Tick(t0.AddMinutes(30));
        Assert.Equal(PomodoroPhase.Idle, p.Phase);
        Assert.Equal(2, alerts.Count);
        Assert.Equal(1, p.Completed);
    }

    [Fact]
    public void Pomodoro_pause_freezes_the_remaining_time()
    {
        var p = new PomodoroWidget("p", new WidgetSpec { Kind = WidgetSpec.Pomodoro, Minutes = 25 });
        var t0 = new DateTime(2026, 10, 12, 9, 0, 0);
        p.Click(t0);
        p.Click(t0.AddMinutes(5));
        p.Tick(t0.AddMinutes(50));
        Assert.Equal("20:00", p.Text);
        Assert.Equal(PomodoroPhase.Focus, p.Phase);
        p.Click(t0.AddMinutes(50));
        p.Tick(t0.AddMinutes(70));
        Assert.Equal(PomodoroPhase.Break, p.Phase);
    }

    [Fact]
    public void Every_fourth_pomodoro_earns_a_long_break()
    {
        var p = new PomodoroWidget("p", new WidgetSpec { Kind = WidgetSpec.Pomodoro, Minutes = 1 });
        var t = new DateTime(2026, 10, 12, 9, 0, 0);
        for (var i = 0; i < 4; i++)
        {
            p.Click(t);
            t = t.AddMinutes(1);
            p.Tick(t);
            if (i < 3)
            {
                t = t.AddMinutes(PomodoroWidget.ShortBreak);
                p.Tick(t);
            }
        }
        Assert.Equal(PomodoroPhase.Break, p.Phase);
        p.Tick(t.AddMinutes(PomodoroWidget.ShortBreak));
        Assert.Equal(PomodoroPhase.Break, p.Phase);
        p.Tick(t.AddMinutes(PomodoroWidget.LongBreak));
        Assert.Equal(PomodoroPhase.Idle, p.Phase);
    }

    [Fact]
    public void Timer_counts_down_and_wheel_sets_minutes_only_when_idle()
    {
        var alerts = new List<string>();
        var timer = new TimerWidget("t", new WidgetSpec { Kind = WidgetSpec.Timer, Minutes = 10 });
        timer.Alert += alerts.Add;
        Assert.True(timer.Wheel(5));
        timer.Tick(DateTime.Now);
        Assert.Equal("15:00", timer.Text);

        var t0 = new DateTime(2026, 10, 12, 9, 0, 0);
        timer.Click(t0);
        Assert.False(timer.Wheel(1));
        timer.Tick(t0.AddMinutes(15));
        Assert.Single(alerts);
        Assert.False(timer.Started);
    }

    [Fact]
    public void Stopwatch_adds_up_running_spans()
    {
        var s = new StopwatchWidget("s", new WidgetSpec { Kind = WidgetSpec.Stopwatch });
        var t0 = new DateTime(2026, 10, 12, 9, 0, 0);
        s.Click(t0);
        s.Click(t0.AddSeconds(30));
        s.Click(t0.AddMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(40), s.Elapsed(t0.AddMinutes(5).AddSeconds(10)));
    }

    static AppWindow Window(string? exe, string? appId) => new(1, "x", exe, appId, false);

    [Fact]
    public void Window_with_an_app_id_matches_by_app_id_when_the_pin_has_one()
    {
        var match = AppMatch.Empty();
        match.Ids.Add("Chrome");
        match.Exes.Add(@"C:\Program Files\Google\Chrome\Application\chrome.exe");

        Assert.True(match.Matches(Window(@"C:\Program Files\Google\Chrome\Application\chrome.exe", "chrome")));
        Assert.False(match.Matches(Window(@"C:\Program Files\Google\Chrome\Application\chrome.exe", "Chrome._crx_abc")));
        Assert.True(match.Matches(Window(@"c:\program files\google\chrome\application\chrome.exe", null)));
    }

    [Fact]
    public void Window_falls_back_to_the_executable_when_the_pin_has_no_app_id()
    {
        var match = AppMatch.Empty();
        match.Exes.Add(@"C:\Users\x\AppData\Roaming\Spotify\Spotify.exe");
        Assert.True(match.Matches(Window(@"C:\Users\x\AppData\Roaming\Spotify\Spotify.exe", "Spotify.Spotify")));
        Assert.False(match.Matches(Window(@"C:\Windows\notepad.exe", null)));
    }

    [Fact]
    public void Store_pin_matches_its_package_window()
    {
        var pin = new PinItem
        {
            Name = "Calc",
            Target = @"C:\Windows\explorer.exe",
            Args = @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            AppId = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"
        };
        var match = AppMatch.For(pin);
        Assert.True(match.Matches(Window(@"C:\Windows\System32\ApplicationFrameHost.exe", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")));
        Assert.False(match.Matches(Window(@"C:\Windows\explorer.exe", null)));
    }

    [Fact]
    public void Steam_game_pin_claims_windows_from_its_install_folder_only()
    {
        var pin = new PinItem { Name = "Elden Ring", Target = @"C:\Windows\explorer.exe", Args = "steam://rungameid/1245620", AppId = "WinDock.Steam.1245620" };
        var match = AppMatch.For(pin, id => id == "1245620" ? @"D:\Steam\steamapps\common\ELDEN RING" : null);

        Assert.True(match.Matches(Window(@"D:\Steam\steamapps\common\ELDEN RING\Game\eldenring.exe", null)));
        Assert.False(match.Matches(Window(@"D:\Steam\steamapps\common\ELDEN RING NIGHTREIGN\Game\nightreign.exe", null)));
        Assert.False(match.Matches(Window(@"C:\Windows\explorer.exe", null)));
    }

    [Fact]
    public void Epic_game_pin_uses_the_launch_executable_folder()
    {
        var pin = new PinItem { Name = "Fortnite", Target = @"C:\Windows\explorer.exe", Args = "com.epicgames.launcher://apps/x", AppId = "WinDock.Epic.Fortnite" };
        var match = AppMatch.For(pin, epicPath: name => name == "Fortnite" ? @"C:\Epic\Fortnite\FortniteGame\Binaries\Win64" : null);
        Assert.True(match.Matches(Window(@"C:\Epic\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe", null)));
    }

    [Fact]
    public void Link_shortcut_claims_the_windows_titled_like_it_in_its_handler_folder()
    {
        var match = AppMatch.Empty() with { Host = @"C:\Program Files\Google\Play Games\", Title = "Clash of Clans" };
        AppWindow Titled(string title, string exe) => new(1, title, exe, "CROSVM-1", false);

        Assert.True(match.Matches(Titled("Clash of Clans - Alvim", @"C:\Program Files\Google\Play Games\current\emulator\crosvm.exe")));
        Assert.False(match.Matches(Titled("Brawl Stars - Alvim", @"C:\Program Files\Google\Play Games\current\emulator\crosvm.exe")));
        Assert.False(match.Matches(Titled("Clash of Clans", @"C:\Games\clash.exe")));
        Assert.True(match.Hosts(Titled("Google Play Games", @"C:\Program Files\Google\Play Games\current\client\client.exe")));
    }

    [Fact]
    public void Music_progress_moves_on_while_playing_and_stays_while_paused()
    {
        var updated = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
        var now = updated.AddSeconds(30);
        var length = TimeSpan.FromSeconds(200);
        Assert.Equal(0.25, MediaSessions.Fraction(TimeSpan.Zero, length, TimeSpan.FromSeconds(20), updated, true, now), 3);
        Assert.Equal(0.1, MediaSessions.Fraction(TimeSpan.Zero, length, TimeSpan.FromSeconds(20), updated, false, now), 3);
        Assert.Equal(1, MediaSessions.Fraction(TimeSpan.Zero, length, TimeSpan.FromSeconds(190), updated, true, now), 3);
        Assert.Equal(-1, MediaSessions.Fraction(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, updated, true, now));
        Assert.Equal(0.1, MediaSessions.Fraction(TimeSpan.Zero, length, TimeSpan.FromSeconds(20), default, true, now), 3);
    }

    [Fact]
    public void Release_tags_compare_with_the_running_version()
    {
        Assert.Equal(new Version(0, 4, 0), Updates.Parse("v0.4.0"));
        Assert.Equal(new Version(0, 5, 0), Updates.Parse("0.5.0-beta"));
        Assert.Null(Updates.Parse("latest"));
        Assert.True(Updates.Parse("v0.4.1") > Updates.Parse("v0.4.0"));
        Assert.False(Updates.Parse("v0.4.0") > new Version(0, 4, 0));
    }

    [Fact]
    public void Dragged_pin_lands_before_the_tile_it_was_dropped_on_and_undo_puts_it_back()
    {
        PinItem a = new() { Name = "A" }, b = new() { Name = "B" }, c = new() { Name = "C" };
        var saves = 0;
        var env = new PresetViewModel(new Preset { Items = [a, b, c] }, 1, _ => saves++);
        string Order() => string.Concat(env.Model.Items.Select(i => i.Name));

        env.Place(c, a, atStart: false);
        Assert.Equal("CAB", Order());
        env.Place(c, null, atStart: false);
        Assert.Equal("ABC", Order());
        env.Place(b, null, atStart: true);
        Assert.Equal("BAC", Order());
        Assert.Equal(env.Model.Items, env.Pins.Select(p => p.Model));

        env.Pins[1].RemoveCommand.Execute(null);
        env.InsertItem(a, 1);
        Assert.Equal("BAC", Order());
        Assert.Equal(5, saves);
    }

    [Fact]
    public void Widget_size_picked_on_the_card_reaches_the_spec_and_the_dock()
    {
        var saves = 0;
        var env = new PresetViewModel(new Preset { Widgets = [new WidgetSpec { Kind = WidgetSpec.Music }] }, 1, _ => saves++);
        var music = env.Widgets.First(w => w.Kind == WidgetSpec.Music);
        var tile = new MusicWidget("m", env.Model.Widgets![0]);

        Assert.Equal(5, music.Sizes.Count);
        music.Size = WidgetSpec.FullSize;
        Assert.Equal(WidgetSpec.FullSize, env.Model.Widgets[0].Size);
        Assert.True(tile.TakeResize());
        Assert.False(tile.TakeResize());
        music.Size = "";
        Assert.Null(env.Model.Widgets[0].Size);
        Assert.Equal(2, saves);
    }

    [Fact]
    public void Todo_list_round_trips_through_the_widget_text()
    {
        var items = TodoWidget.Parse("[ ] Ligar pro dentista\n[x] Pagar luz\nsem marca\n\n[x]   ");
        Assert.Equal([new TodoItem("Ligar pro dentista", false), new TodoItem("Pagar luz", true), new TodoItem("sem marca", false)], items);
        Assert.Equal("[ ] Ligar pro dentista\n[x] Pagar luz\n[ ] sem marca", TodoWidget.Format(items));
        Assert.Equal("[ ] duas linhas", TodoWidget.Format([new TodoItem("duas\nlinhas", false)]));
        Assert.Null(TodoWidget.Format([]));
    }

    [Fact]
    public void Unknown_game_folder_claims_nothing()
    {
        var pin = new PinItem { Name = "Game", Target = @"C:\Windows\explorer.exe", Args = "steam://rungameid/1", AppId = "WinDock.Steam.1" };
        var match = AppMatch.For(pin, _ => null);
        Assert.Empty(match.Ids);
        Assert.Empty(match.Exes);
        Assert.Empty(match.Folders);
    }

    [Fact]
    public void Steam_and_explorer_pins_know_the_app_id_their_windows_report()
    {
        var raw =AppMatch.For(new PinItem { Name = "Steam", AppId = @"{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}\Steam\steam.exe", Favorites = "", Resolve = "" });
        Assert.Contains("Valve.Steam.Client", raw.Ids);
        Assert.True(raw.Matches(Window(@"C:\Program Files (x86)\Steam\bin\cef\cef.win64\steamwebhelper.exe", "Valve.Steam.Client")));

        var explorer = AppMatch.For(new PinItem { Name = "Explorer", Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") });
        Assert.True(explorer.Matches(Window(@"C:\Windows\explorer.exe", WindowTracker.ExplorerId)));
    }

    [Fact]
    public void Settings_from_an_older_version_get_dock_defaults()
    {
        var settings = Settings.Parse("""{ "autoSwitch": true, "language": "pt", "activeId": "jogos" }""");
        Assert.True(settings.Dock.Enabled);
        Assert.False(settings.Dock.ReplaceTaskbar);
        Assert.True(settings.Dock.Reserve);
        Assert.Equal(DockSettings.LensMagnify, settings.Dock.Lens);
        Assert.False(settings.SyncTaskbar);
        Assert.Equal("jogos", settings.ActiveId);
    }

    [Fact]
    public void Dock_settings_are_clamped_and_round_trip()
    {
        var settings = Settings.Parse("""{ "dock": { "magnification": 9, "iconSize": 4, "lens": "warp", "theme": "plaid" } }""");
        Assert.Equal(2.4, settings.Dock.Magnification);
        Assert.Equal(32, settings.Dock.IconSize);
        Assert.Equal(DockSettings.LensMagnify, settings.Dock.Lens);
        Assert.Equal(DockSettings.ThemeDark, settings.Dock.Theme);

        settings.Dock.ReplaceTaskbar = true;
        var back = Settings.Parse(JsonSerializer.Serialize(settings, Json.Options));
        Assert.True(back.Dock.ReplaceTaskbar);
    }

    [Fact]
    public void Every_dock_theme_has_valid_colors_and_a_name()
    {
        var hex = new System.Text.RegularExpressions.Regex("^#([0-9A-F]{6}|[0-9A-F]{8})$");
        Assert.Equal(DockTheme.All.Count, DockTheme.All.Select(t => t.Id).Distinct().Count());
        foreach (var t in DockTheme.All)
        {
            foreach (var color in new[] { t.BarTop, t.BarBottom, t.Stroke, t.Ink, t.Muted, t.Shadow, t.Indicator }.OfType<string>())
                Assert.Matches(hex, color);
            var key = "theme" + char.ToUpperInvariant(t.Id[0]) + t.Id[1..];
            Assert.NotEqual(key, Loc.T(key));
            Assert.Equal(t.Id, Settings.Parse($$"""{ "dock": { "theme": "{{t.Id}}" } }""").Dock.Theme);
        }
        Assert.Same(DockTheme.All[0], DockTheme.Find("plaid"));
    }

    [Fact]
    public void Broken_settings_file_falls_back_to_defaults()
    {
        Assert.True(Settings.Parse("{ not json").Dock.Enabled);
    }

    [Fact]
    public void Preset_without_widgets_shows_a_clock_but_an_empty_list_shows_nothing()
    {
        Assert.Equal(WidgetSpec.Clock, Assert.Single(new Preset().ShownWidgets).Kind);
        Assert.Empty(new Preset { Widgets = [] }.ShownWidgets);
    }

    [Fact]
    public void Bundled_presets_bring_widgets()
    {
        foreach (var name in PresetStore.BundledNames())
        {
            var p = JsonSerializer.Deserialize<Preset>(PresetStore.ReadBundled(name), Json.Options)!;
            Assert.NotNull(p.Widgets);
            Assert.NotEmpty(p.Widgets!);
            Assert.All(p.Widgets!, w => Assert.Contains(w.Kind, WidgetSpec.Kinds));
        }
    }

    [Fact]
    public void A_folder_is_a_pin_that_exists_and_never_reaches_the_native_taskbar()
    {
        var dir = Directory.CreateTempSubdirectory("windock-test-").FullName;
        try
        {
            var pin = new PinItem { Name = "Stack", Target = dir };
            Assert.True(pin.Exists);
            Assert.True(pin.IsFolder);
            var snapshot = PinBuilder.Build(new Preset { Items = [pin] }, new Snapshot());
            Assert.Empty(snapshot.Links);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Folder_entries_skip_partial_downloads_and_list_newest_first()
    {
        var dir = Directory.CreateTempSubdirectory("windock-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "old.txt"), "a");
            File.SetLastWriteTime(Path.Combine(dir, "old.txt"), DateTime.Now.AddDays(-2));
            File.SetCreationTime(Path.Combine(dir, "old.txt"), DateTime.Now.AddDays(-2));
            File.WriteAllText(Path.Combine(dir, "new.pdf"), "b");
            File.WriteAllText(Path.Combine(dir, "movie.mp4.crdownload"), "c");

            var entries = FolderStack.Entries(dir, 10);
            Assert.Equal(["new.pdf", "old.txt"], entries.Select(e => e.Name));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Shell_state_owned_by_a_dead_process_is_stale()
    {
        var state = new ShellState { Pid = int.MaxValue - 7, Started = DateTime.Now.AddHours(-1) };
        Assert.False(state.IsOwnerAlive());

        using var self = System.Diagnostics.Process.GetCurrentProcess();
        Assert.True(new ShellState { Pid = self.Id, Started = self.StartTime }.IsOwnerAlive());
    }

    [Fact]
    public void Battery_glyphs_step_through_the_charge_levels()
    {
        Assert.Equal("\uEBA0", SystemTile.BatteryGlyphFor(0, false));
        Assert.Equal("\uEBAA", SystemTile.BatteryGlyphFor(100, false));
        Assert.Equal("\uEBB0", SystemTile.BatteryGlyphFor(52, true));
    }
}
