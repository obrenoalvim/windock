using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinDock.Services;

namespace WinDock.Views;

public sealed class ClaudePanel : Flyout
{
    readonly ClaudeUsage usage;
    readonly StackPanel body = new() { Width = 300 };

    public ClaudePanel(ClaudeUsage usage, bool dark) : base(dark)
    {
        this.usage = usage;
        SetBody(body);
        Render();
        usage.Updated += Render;
        Closed += (_, _) => usage.Updated -= Render;
    }

    public static Brush LevelBrush(double percent) => percent switch
    {
        >= 90 => Paint("#FF5A4E"),
        >= 75 => Paint("#FFB547"),
        _ => Paint("#D97757")
    };

    void Render()
    {
        body.Children.Clear();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        if (usage.Plan is { } plan)
        {
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 8, 3),
                Background = Paint("#33D97757"),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Text(plan, 10)
            };
            DockPanel.SetDock(badge, Dock.Right);
            header.Children.Add(badge);
        }
        header.Children.Add(Text("Claude", 14, font: Display));
        body.Children.Add(header);

        var now = DateTime.Now;
        if (usage.Last is { } limits)
        {
            body.Children.Add(Section(Loc.T("claudeSession"), limits.Session, limits.SessionReset, null, now));
            body.Children.Add(Section(Loc.T("claudeWeek"), limits.Week, limits.WeekReset,
                limits.Week is { } week ? ClaudeUsage.Projected(week, limits.WeekReset, ClaudeUsage.WeekWindow, now) : null, now));
        }

        if (Message() is { } message)
        {
            var note = Text(message, 11, muted: true);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 0, 0, 12);
            body.Children.Add(note);
        }

        var footer = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Action(Loc.T("refresh"), () =>
        {
            if (!usage.Refresh(DateTime.Now)) Render();
        }, "\uE895"));
        var site = Action(Loc.T("claudeSite"), () =>
        {
            Launcher.OpenFile("https://claude.ai/settings/usage");
            SafeClose();
        }, "\uE8A7");
        site.Margin = new Thickness(6, 0, 0, 0);
        actions.Children.Add(site);
        DockPanel.SetDock(actions, Dock.Right);
        footer.Children.Add(actions);
        if (usage.Last is { } last) footer.Children.Add(Text(Ago(last.FetchedAt, now), 10, muted: true));
        body.Children.Add(footer);
    }

    StackPanel Section(string title, double? used, DateTime? reset, double? projected, DateTime now)
    {
        var top = new DockPanel();
        var value = Text(used is { } u ? $"{u:0}%" : "—", 20, font: Display);
        DockPanel.SetDock(value, Dock.Right);
        top.Children.Add(value);
        top.Children.Add(Text(title, 11, muted: true));

        var section = new StackPanel { Margin = new Thickness(0, 0, 0, 14), Children = { top } };
        var meter = Meter((used ?? 0) / 100, LevelBrush(used ?? 0));
        meter.Margin = new Thickness(0, 8, 0, 6);
        section.Children.Add(meter);
        if (reset is { } at) section.Children.Add(Text(Loc.F("claudeResets", ResetText(at, now)), 10, muted: true));
        if (projected is { } p)
        {
            var pace = Text(p >= 100 ? Loc.T("claudePaceOver") : Loc.F("claudePace", Math.Round(p)), 10, muted: p < 100);
            if (p >= 100) pace.Foreground = Paint("#FF8A7A");
            pace.TextWrapping = TextWrapping.Wrap;
            pace.Margin = new Thickness(0, 3, 0, 0);
            section.Children.Add(pace);
        }
        return section;
    }

    string? Message() => usage.Status switch
    {
        ClaudeStatus.Loading => Loc.T("claudeLoading"),
        ClaudeStatus.NoLogin => Loc.T("claudeNoLogin"),
        ClaudeStatus.Expired => Loc.T("claudeExpired"),
        ClaudeStatus.Offline => Loc.T("claudeOffline"),
        ClaudeStatus.RateLimited => Loc.T("claudeRateLimited"),
        ClaudeStatus.Failed => Loc.T("claudeFailed"),
        _ => null
    };

    public static string ResetText(DateTime reset, DateTime now)
    {
        var left = reset - now;
        if (left <= TimeSpan.Zero) return Loc.T("claudeNow");
        if (left < TimeSpan.FromHours(1)) return Loc.F("inMinutes", Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));
        if (left < TimeSpan.FromHours(24)) return Loc.F("inHours", (int)left.TotalHours, left.Minutes);
        return reset.ToString(Loc.Instance.Language == Loc.Portuguese ? "ddd HH:mm" : "ddd h:mm tt", Loc.Instance.Culture).Replace(".", "");
    }

    static string Ago(DateTime at, DateTime now)
    {
        var minutes = (int)(now - at).TotalMinutes;
        return minutes < 1 ? Loc.T("updatedNow") : Loc.F("updatedAgo", minutes);
    }
}
