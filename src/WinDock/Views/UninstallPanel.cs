using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class UninstallPanel : Flyout
{
    const double PanelWidth = 320;
    static readonly Brush Red = Paint("#E5483B");
    static readonly Brush Green = Paint("#2FB37A");
    static readonly Brush Amber = Paint("#FFB547");

    readonly UninstallWidget widget;
    readonly StackPanel body = new() { Width = PanelWidth };
    readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };
    TextBlock? elapsed;

    public UninstallPanel(UninstallWidget widget, bool dark) : base(dark)
    {
        this.widget = widget;
        SetBody(body);
        Render();
        widget.Changed += OnChanged;
        clock.Tick += (_, _) =>
        {
            if (elapsed is not null && widget.Current is { } job) elapsed.Text = Elapsed(job);
        };
        clock.Start();
        Closed += (_, _) =>
        {
            clock.Stop();
            widget.Changed -= OnChanged;
        };
    }

    void OnChanged() => Dispatcher.BeginInvoke(Render);

    void Render()
    {
        body.Children.Clear();
        elapsed = null;
        body.Children.Add(Header());
        if (widget.Current is { } current) body.Children.Add(CurrentCard(current));
        else if (widget.Searching) body.Children.Add(Note(Loc.T("uninstallSearching")));
        else if (widget.Queue.Count == 0) body.Children.Add(Note(Loc.T("uninstallIdle")));

        var waiting = widget.Queue.Where(j => j != widget.Current).ToList();
        if (waiting.Count > 0)
        {
            body.Children.Add(Section(Loc.T("uninstallQueueTitle"), waiting.Count.ToString()));
            body.Children.Add(List(waiting.Select(WaitingRow)));
        }

        if (widget.History.Count > 0)
        {
            body.Children.Add(Section(Loc.T("uninstallRecent"), null));
            body.Children.Add(List(widget.History.Select(DoneRow)));
        }

        body.Children.Add(Footer());
    }

    DockPanel Header()
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        if (widget.BatchTotal > 0 && widget.Current is not null)
        {
            var count = Text(Loc.F("uninstallOf", Math.Min(widget.BatchDone + 1, widget.BatchTotal), widget.BatchTotal), 11, muted: true);
            DockPanel.SetDock(count, Dock.Right);
            header.Children.Add(count);
        }
        var icon = Glyph(UninstallWidget.TrashGlyph, 14);
        icon.Margin = new Thickness(0, 0, 8, 0);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, Text(Loc.T("widget_uninstall"), 14, font: Display) } });
        return header;
    }

    Border CurrentCard(UninstallJob job)
    {
        elapsed = Text(Elapsed(job), 11, muted: true);
        var top = new DockPanel();
        DockPanel.SetDock(elapsed, Dock.Right);
        top.Children.Add(elapsed);
        top.Children.Add(Text(job.Name, 13));

        var stage = Text(StageText(job), 10.5, muted: true);
        stage.Margin = new Thickness(0, 3, 0, 10);
        var meter = Meter(widget.Progress, Red);

        var card = new Border
        {
            Padding = new Thickness(12, 10, 12, 12),
            Margin = new Thickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(12),
            Background = Hover,
            Child = new StackPanel { Children = { top, stage, meter } }
        };
        if (widget.CanSkip)
        {
            var skip = Action(Loc.T("uninstallSkip"), widget.Skip, "");
            skip.Margin = new Thickness(0, 10, 0, 0);
            skip.HorizontalAlignment = HorizontalAlignment.Left;
            ((StackPanel)card.Child).Children.Add(skip);
        }
        return card;
    }

    static string Elapsed(UninstallJob job) => WidgetTile.Clock(DateTime.Now - (job.Started ?? DateTime.Now));

    static string StageText(UninstallJob job) => job.Stage switch
    {
        UninstallStage.Cleaning when job.IsTrash => Loc.T("uninstallStageTrash"),
        UninstallStage.Cleaning => Loc.F("uninstallStageCleaning", job.Leftovers.Count),
        _ => Loc.T(job.Silent ? "uninstallStageSilent" : "uninstallStageWindow")
    };

    TextBlock Note(string text)
    {
        var note = Text(text, 11, muted: true);
        note.TextWrapping = TextWrapping.Wrap;
        note.TextTrimming = TextTrimming.None;
        note.Margin = new Thickness(0, 0, 0, 12);
        return note;
    }

    DockPanel Section(string title, string? count)
    {
        var section = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        if (count is not null)
        {
            var badge = Text(count, 10, muted: true);
            DockPanel.SetDock(badge, Dock.Right);
            section.Children.Add(badge);
        }
        section.Children.Add(Text(title.ToUpperInvariant(), 9.5, muted: true));
        return section;
    }

    static ScrollViewer List(IEnumerable<UIElement> rows)
    {
        var list = new StackPanel { Margin = new Thickness(-6, 0, -6, 0) };
        foreach (var row in rows) list.Children.Add(row);
        return new ScrollViewer { Content = list, MaxHeight = 190, Margin = new Thickness(0, 0, 0, 10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    Border WaitingRow(UninstallJob job)
    {
        var remove = Glyph("", 10);
        remove.Opacity = 0;
        remove.Cursor = Cursors.Hand;
        remove.ToolTip = Loc.T("uninstallRemoveQueued");
        remove.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            widget.Cancel(job);
        };
        var detail = job.IsTrash ? Loc.T("uninstallKindTrash")
            : job.Leftovers.Count > 0 ? $"{Loc.T("uninstallKindApp")} · {Loc.F("uninstallLeftoverCount", job.Leftovers.Count)}"
            : Loc.T("uninstallKindApp");
        var row = Row(job.IsTrash ? "" : "", Muted, job.Name, detail, remove);
        row.MouseEnter += (_, _) => remove.Opacity = 1;
        row.MouseLeave += (_, _) => remove.Opacity = 0;
        return row;
    }

    Border DoneRow(UninstallJob job)
    {
        var (glyph, color) = job.Stage switch
        {
            UninstallStage.Done => ("", Green),
            UninstallStage.Partial => ("", Amber),
            _ => ("", Red)
        };
        var detail = job.Stage switch
        {
            UninstallStage.Done => Loc.T(job.IsTrash ? "uninstallKindTrash" : "uninstallKindApp"),
            UninstallStage.Partial => Loc.F("uninstallLeftStayed", job.Stuck),
            _ => job.Error ?? Loc.T("uninstallNotRemoved")
        };
        var time = Text(job.Finished?.ToString("HH:mm") ?? "", 10, muted: true);
        var row = Row(glyph, color, job.Name, detail, time);
        row.ToolTip = UninstallWidget.Message(job);
        return row;
    }

    Border Row(string glyph, Brush color, string name, string detail, UIElement right)
    {
        var icon = Glyph(glyph, 12);
        icon.Foreground = color;
        icon.Width = 18;
        icon.Margin = new Thickness(0, 0, 8, 0);
        var texts = new StackPanel { Children = { Text(name, 11.5), Text(detail, 9.5, muted: true) } };
        var line = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        if (right is FrameworkElement element)
        {
            element.Margin = new Thickness(8, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
        }
        line.Children.Add(icon);
        line.Children.Add(right);
        line.Children.Add(texts);
        var row = new Border { Padding = new Thickness(6, 5, 6, 5), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Child = line };
        row.MouseEnter += (_, _) => row.Background = Hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        return row;
    }

    WrapPanel Footer()
    {
        var footer = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        footer.Children.Add(Button(Loc.T("uninstallApps"), "", () => Launcher.OpenFile("ms-settings:appsfeatures")));
        footer.Children.Add(Button(Loc.T("uninstallOpenBin"), UninstallWidget.TrashGlyph, () => Launcher.OpenFile("shell:RecycleBinFolder")));
        if (widget.History.Count > 0) footer.Children.Add(Button(Loc.T("uninstallClear"), "", widget.ClearHistory, close: false));
        return footer;
    }

    Border Button(string label, string glyph, Action click, bool close = true)
    {
        var button = Action(label, () =>
        {
            click();
            if (close) SafeClose();
        }, glyph);
        button.Margin = new Thickness(0, 0, 6, 6);
        return button;
    }
}
