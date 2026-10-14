using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;
using WinDock.Views;

namespace WinDock.Tests;

public class SystemTests
{
    const string UsageJson = """
        {
          "five_hour": { "utilization": 20, "resets_at": "2026-10-12T23:19:59.544027+00:00" },
          "seven_day": { "utilization": 89.4, "resets_at": "2026-10-13T12:59:59.544048+00:00" },
          "seven_day_opus": null,
          "limits": [ { "kind": "session", "percent": 20 } ]
        }
        """;

    [Fact]
    public void Claude_usage_reads_session_and_week()
    {
        var now = new DateTime(2026, 10, 12, 18, 0, 0);
        var limits = ClaudeUsage.Parse(UsageJson, now);
        Assert.NotNull(limits);
        Assert.Equal(20, limits!.Session);
        Assert.Equal(89.4, limits.Week);
        Assert.Equal(DateTimeOffset.Parse("2026-10-12T23:19:59.544027+00:00").LocalDateTime, limits.SessionReset);
        Assert.Equal(now, limits.FetchedAt);
    }

    [Theory]
    [InlineData("""{ "five_hour": { "utilization": 140 } }""", 100.0)]
    [InlineData("""{ "five_hour": { "utilization": -3 } }""", 0.0)]
    public void Claude_usage_is_clamped(string json, double expected) =>
        Assert.Equal(expected, ClaudeUsage.Parse(json, DateTime.Now)!.Session);

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{ "five_hour": null, "seven_day": null }""")]
    public void Claude_usage_without_limits_is_null(string json) =>
        Assert.Null(ClaudeUsage.Parse(json, DateTime.Now));

    [Fact]
    public void Claude_login_reads_token_expiry_plan_and_renewal()
    {
        var login = ClaudeUsage.ParseLogin("""
            { "claudeAiOauth": { "accessToken": "sk-ant-test", "refreshToken": "rt-old", "expiresAt": 1791840058143, "scopes": ["user:inference", "user:profile"], "subscriptionType": "pro" } }
            """);
        Assert.NotNull(login);
        Assert.Equal("sk-ant-test", login!.Token);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1791840058143).UtcDateTime, login.Expires);
        Assert.Equal("Pro", login.Plan);
        Assert.Equal("rt-old", login.RefreshToken);
        Assert.Equal("user:inference user:profile", login.Scopes);
    }

    [Fact]
    public void Claude_renewal_rewrites_only_the_renewed_fields()
    {
        var now = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
        var updated = ClaudeUsage.ApplyRenewal("""
            { "claudeAiOauth": { "accessToken": "old", "refreshToken": "rt-old", "expiresAt": 1, "scopes": ["user:inference"], "subscriptionType": "max", "rateLimitTier": "t" }, "other": 7 }
            """, """{ "access_token": "new", "refresh_token": "rt-new", "expires_in": 28800, "refresh_token_expires_in": 60 }""", now);
        var login = ClaudeUsage.ParseLogin(updated!);
        Assert.Equal("new", login!.Token);
        Assert.Equal("rt-new", login.RefreshToken);
        Assert.Equal(now.AddHours(8), login.Expires);
        Assert.Equal("user:inference", login.Scopes);
        Assert.Equal("Max", login.Plan);
        Assert.Contains("\"rateLimitTier\":\"t\"", updated);
        Assert.Contains("\"other\":7", updated);
        Assert.Null(ClaudeUsage.ApplyRenewal(updated!, """{ "error": "invalid_grant" }""", now));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": "" } }""")]
    [InlineData("garbage")]
    public void Claude_login_needs_a_token(string json) => Assert.Null(ClaudeUsage.ParseLogin(json));

    [Theory]
    [InlineData("max_20x", "Max")]
    [InlineData("pro", "Pro")]
    [InlineData("team", "Team")]
    [InlineData("api", null)]
    [InlineData("", null)]
    public void Claude_plan_names(string subscription, string? expected) =>
        Assert.Equal(expected, ClaudeUsage.PlanName(subscription));

    [Fact]
    public void Claude_pace_projects_the_week_to_its_reset()
    {
        var now = new DateTime(2026, 10, 12, 12, 0, 0);
        var reset = now.AddDays(3.5);
        Assert.Equal(80, ClaudeUsage.Projected(40, reset, ClaudeUsage.WeekWindow, now)!.Value, 6);
        Assert.Null(ClaudeUsage.Projected(40, now.AddDays(7).AddMinutes(-10), ClaudeUsage.WeekWindow, now));
        Assert.Null(ClaudeUsage.Projected(40, null, ClaudeUsage.WeekWindow, now));
    }

    [Fact]
    public void Claude_alerts_once_per_threshold_and_reset_window()
    {
        var reset = new DateTime(2026, 10, 13, 9, 0, 0);
        var limits = new ClaudeLimits(85, reset, 96, reset.AddDays(3), DateTime.Now);
        var crossings = ClaudeWidget.Crossings(limits).ToList();
        Assert.Equal(3, crossings.Count);
        Assert.Contains(crossings, c => c is { Name: "claudeSession", Threshold: 80 });
        Assert.DoesNotContain(crossings, c => c is { Name: "claudeSession", Threshold: 95 });
        Assert.Equal(crossings.Count, crossings.Select(c => c.Key).Distinct().Count());
        var nextWindow = ClaudeWidget.Crossings(limits with { SessionReset = reset.AddHours(5) }).ToList();
        Assert.NotEqual(crossings.First().Key, nextWindow.First().Key);
    }

    [Theory]
    [InlineData(NetworkKind.None, false, 0, "\uF384")]
    [InlineData(NetworkKind.Wired, true, 0, "\uE839")]
    [InlineData(NetworkKind.Wired, false, 0, "\uF384")]
    [InlineData(NetworkKind.Wifi, false, 90, "\uEB5E")]
    [InlineData(NetworkKind.Wifi, true, 10, "\uE872")]
    [InlineData(NetworkKind.Wifi, true, 60, "\uE874")]
    [InlineData(NetworkKind.Wifi, true, 95, "\uE701")]
    public void Network_glyph_shows_kind_signal_and_internet(NetworkKind kind, bool internet, int signal, string glyph) =>
        Assert.Equal(glyph, NetworkStatus.Glyph(new NetworkState(kind, internet, "Casa", signal)));

    [Theory]
    [InlineData(0.5, true, "\uE74F")]
    [InlineData(0.0, false, "\uE992")]
    [InlineData(0.2, false, "\uE993")]
    [InlineData(0.5, false, "\uE994")]
    [InlineData(0.9, false, "\uE995")]
    public void Volume_glyph_follows_level_and_mute(double volume, bool muted, string glyph) =>
        Assert.Equal(glyph, Audio.Glyph(new AudioState(volume, muted, "Speakers")));

    [Fact]
    public void Network_text_names_the_wifi_and_flags_missing_internet()
    {
        Assert.Contains("Casa", SystemTile.NetworkText(new NetworkState(NetworkKind.Wifi, true, "Casa", 80)));
        Assert.Contains("80", SystemTile.NetworkText(new NetworkState(NetworkKind.Wifi, true, "Casa", 80)));
        Assert.NotEqual(SystemTile.NetworkText(new NetworkState(NetworkKind.Wired, true, null, 0)),
            SystemTile.NetworkText(new NetworkState(NetworkKind.Wired, false, null, 0)));
    }

    [Fact]
    public void Battery_text_has_time_left_when_unplugged()
    {
        var text = SystemTile.DescribeBattery(new BatteryState(true, 64, false, TimeSpan.FromMinutes(135)));
        Assert.Contains("64", text);
        Assert.Contains("2", text);
        Assert.Contains("15", text);
        Assert.Equal(Loc.T("plugged"), SystemTile.DescribeBattery(new BatteryState(false, 0, false, null)));
    }

    [Fact]
    public void Clock_widget_steps_aside_when_the_dock_shows_the_windows_clock_and_keeps_widget_slots()
    {
        WidgetSpec[] specs = [new() { Kind = WidgetSpec.Clock }, new() { Kind = WidgetSpec.Claude }, new() { Kind = WidgetSpec.Pomodoro }];
        var withTaskbar = DockViewModel.VisibleWidgets(specs, systemClock: false);
        var withSystem = DockViewModel.VisibleWidgets(specs, systemClock: true);
        Assert.Equal(3, withTaskbar.Count);
        Assert.Equal([WidgetSpec.Claude, WidgetSpec.Pomodoro], withSystem.Select(w => w.Spec.Kind));
        Assert.Equal([1, 2], withSystem.Select(w => w.Index));
    }

    static readonly string[] Two = [@"\\.\DISPLAY1", @"\\.\DISPLAY2"];

    [Fact]
    public void Dock_goes_to_every_screen_by_default()
    {
        Assert.True(new DockSettings().AllScreens);
        Assert.Equal(Two, DockHost.Targets(true, true, null, Two, Two[0]));
    }

    [Fact]
    public void Dock_can_live_on_one_chosen_screen_and_falls_back_to_the_primary()
    {
        Assert.Equal([Two[1]], DockHost.Targets(true, false, Two[1], Two, Two[0]));
        Assert.Equal([Two[0]], DockHost.Targets(true, false, @"\\.\DISPLAY9", Two, Two[0]));
        Assert.Empty(DockHost.Targets(false, true, null, Two, Two[0]));
    }
}
