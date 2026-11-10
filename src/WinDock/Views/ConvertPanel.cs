using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class ConvertPanel : Flyout
{
    const double PanelWidth = 320;
    static readonly Brush Accent = Paint("#4C8DFF");
    static readonly Brush Green = Paint("#2FB37A");
    static readonly Brush Red = Paint("#E5483B");

    readonly ConvertWidget widget;
    readonly StackPanel body = new() { Width = PanelWidth };
    readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };
    string shape = "";
    TextBlock? elapsed, percent;
    Grid? meter;

    public ConvertPanel(ConvertWidget widget, bool dark) : base(dark)
    {
        this.widget = widget;
        SetBody(body);
        Render();
        widget.Changed += OnChanged;
        clock.Tick += (_, _) => UpdateProgress();
        clock.Start();
        Closed += (_, _) =>
        {
            clock.Stop();
            widget.Changed -= OnChanged;
        };
    }

    string Shape() => $"{widget.Current?.Source}|{widget.Queue.Count}|{widget.History.Count}|{widget.BatchTotal}";

    void OnChanged() => Dispatcher.BeginInvoke(() =>
    {
        if (Shape() == shape) UpdateProgress();
        else Render();
    });

    void UpdateProgress()
    {
        if (widget.Current is not { } job) return;
        if (elapsed is not null) elapsed.Text = WidgetTile.Clock(DateTime.Now - (job.Started ?? DateTime.Now));
        if (percent is not null) percent.Text = job.Duration > 0 || job.Progress >= 1 ? $"{(int)(job.Progress * 100)}%" : "…";
        if (meter is not null) SetMeter(meter, job.Progress);
    }

    void Render()
    {
        shape = Shape();
        elapsed = percent = null;
        meter = null;
        body.Children.Clear();
        body.Children.Add(Header());
        if (widget.Current is { } current) body.Children.Add(CurrentCard(current));
        else if (widget.Queue.Count == 0) body.Children.Add(Note(Loc.T("convertIdle")));

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
        if (widget.Current is not null && widget.BatchTotal > 1)
        {
            var count = Text(Loc.F("uninstallOf", widget.BatchDone + 1, widget.BatchTotal), 11, muted: true);
            DockPanel.SetDock(count, Dock.Right);
            header.Children.Add(count);
        }
        var icon = Glyph(ConvertWidget.ConvertGlyph, 14);
        icon.Margin = new Thickness(0, 0, 8, 0);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, Text(Loc.T("widget_convert"), 14, font: Display) } });
        return header;
    }

    Border CurrentCard(ConvertJob job)
    {
        elapsed = Text("", 11, muted: true);
        DockPanel.SetDock(elapsed, Dock.Right);
        var top = new DockPanel { Children = { elapsed, Text(job.Name, 13) } };

        percent = Text("", 18, font: Display);
        DockPanel.SetDock(percent, Dock.Right);
        var stage = new DockPanel { Margin = new Thickness(0, 2, 0, 8), Children = { percent, Text("→ " + job.Target.Label, 10.5, muted: true) } };

        meter = Meter(job.Progress, Accent);
        var cancel = Action(Loc.T("convertCancel"), () => widget.Cancel(job), "");
        cancel.Margin = new Thickness(0, 10, 0, 0);
        cancel.HorizontalAlignment = HorizontalAlignment.Left;
        UpdateProgress();
        return new Border
        {
            Padding = new Thickness(12, 10, 12, 12),
            Margin = new Thickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(12),
            Background = Hover,
            Child = new StackPanel { Children = { top, stage, meter, cancel } }
        };
    }

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

    Border WaitingRow(ConvertJob job)
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
        var row = Row("", Muted, job.Name, "→ " + job.Target.Label, remove);
        row.MouseEnter += (_, _) => remove.Opacity = 1;
        row.MouseLeave += (_, _) => remove.Opacity = 0;
        return row;
    }

    Border DoneRow(ConvertJob job)
    {
        var done = job.Stage == ConvertStage.Done;
        var time = Text(job.Finished?.ToString("HH:mm") ?? "", 10, muted: true);
        var row = Row(done ? "" : "", done ? Green : Red, done ? Path.GetFileName(job.Output!) : job.Name, done ? job.Name : job.Error ?? "", time);
        row.ToolTip = ConvertWidget.Message(job);
        if (done && job.Output is { } output)
        {
            row.Cursor = Cursors.Hand;
            row.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Launcher.Reveal(output);
                SafeClose();
            };
        }
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
        foreach (var engine in new[] { ConvertEngine.Ffmpeg, ConvertEngine.Office }.Where(e => !Converter.Has(e)))
            footer.Children.Add(Button(Loc.T("convertInstall_" + engine), "", () => Converter.Install(engine)));
        if (widget.History is [{ Output: { } last, Stage: ConvertStage.Done }, ..])
            footer.Children.Add(Button(Loc.T("convertOpenFolder"), "", () => Launcher.Reveal(last)));
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
