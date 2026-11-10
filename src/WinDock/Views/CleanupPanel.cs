using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class CleanupPanel : Flyout
{
    const double PanelWidth = 340;
    const int TipLines = 15;
    static readonly Brush Accent = Paint("#2FB37A");
    static readonly Brush Danger = Paint("#E5483B");

    readonly CleanupWidget widget;
    readonly StackPanel body = new() { Width = PanelWidth };
    bool confirming;

    public CleanupPanel(CleanupWidget widget, bool dark) : base(dark)
    {
        this.widget = widget;
        SetBody(body);
        Render();
        widget.Changed += OnChanged;
        Closed += (_, _) => widget.Changed -= OnChanged;
        Loaded += (_, _) => _ = widget.Scan();
    }

    void OnChanged() => Dispatcher.BeginInvoke(Render);

    void Render()
    {
        body.Children.Clear();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        if (!widget.Busy)
        {
            var rescan = Glyph("", 12);
            rescan.Cursor = Cursors.Hand;
            rescan.ToolTip = Loc.T("cleanupRescan");
            rescan.MouseLeftButtonUp += (_, _) => _ = widget.Scan(force: true);
            DockPanel.SetDock(rescan, Dock.Right);
            header.Children.Add(rescan);
        }
        var icon = Glyph(CleanupWidget.BroomGlyph, 14);
        icon.Margin = new Thickness(0, 0, 8, 0);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, Text(Loc.T("widget_cleanup"), 14, font: Display) } });
        body.Children.Add(header);

        if (widget.Last is { } last && !widget.Busy) body.Children.Add(Note(CleanupWidget.Summary(last), Accent));
        if (widget.Busy)
        {
            body.Children.Add(Note(Loc.T(widget.Scanning ? "cleanupScanning" : "cleanupCleaning"), null));
            return;
        }

        var list = new StackPanel { Margin = new Thickness(-6, 0, -6, 8) };
        foreach (var category in widget.Categories) list.Children.Add(Row(category));
        body.Children.Add(list);

        var footer = new WrapPanel();
        var size = widget.SelectedSize;
        var any = widget.Categories.Any(c => widget.Selected.Contains(c.Id) && !c.IsEmpty);
        if (any)
        {
            var clean = Action(confirming ? Loc.T("cleanupConfirm") : Loc.F("cleanupRun", Cleanup.Bytes(size)), () =>
            {
                if (!confirming)
                {
                    confirming = true;
                    Render();
                    return;
                }
                confirming = false;
                _ = widget.Clean();
            }, CleanupWidget.BroomGlyph);
            if (confirming) clean.BorderBrush = Danger;
            clean.Margin = new Thickness(0, 0, 6, 6);
            footer.Children.Add(clean);
        }
        var windows = Action(Loc.T("cleanupWindows"), () =>
        {
            Launcher.OpenFile("ms-settings:storagerecommendations");
            SafeClose();
        }, "");
        windows.Margin = new Thickness(0, 0, 6, 6);
        footer.Children.Add(windows);
        body.Children.Add(footer);
    }

    TextBlock Note(string text, Brush? color)
    {
        var note = Text(text, 11, muted: color is null);
        if (color is not null) note.Foreground = color;
        note.TextWrapping = TextWrapping.Wrap;
        note.TextTrimming = TextTrimming.None;
        note.Margin = new Thickness(0, 0, 0, 12);
        return note;
    }

    Border Row(CleanupCategory category)
    {
        var check = new CheckBox
        {
            IsChecked = widget.Selected.Contains(category.Id),
            IsEnabled = !category.IsEmpty,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 8, 0)
        };
        check.Click += (_, _) =>
        {
            confirming = false;
            widget.Toggle(category.Id);
        };
        var size = Text(category.IsEmpty ? "—" : Cleanup.Bytes(category.Size), 11, muted: category.IsEmpty);
        size.VerticalAlignment = VerticalAlignment.Top;
        size.Margin = new Thickness(8, 0, 0, 0);
        var detail = Text(Detail(category), 9.5, muted: true);
        detail.TextWrapping = TextWrapping.Wrap;
        detail.TextTrimming = TextTrimming.None;
        var texts = new StackPanel { Children = { Text(category.Label, 11.5, muted: category.IsEmpty), detail } };
        var line = new DockPanel();
        DockPanel.SetDock(check, Dock.Left);
        DockPanel.SetDock(size, Dock.Right);
        line.Children.Add(check);
        line.Children.Add(size);
        line.Children.Add(texts);
        var row = new Border { Padding = new Thickness(6, 5, 6, 5), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Child = line };
        if (category.Paths.Count > 0)
            row.ToolTip = string.Join('\n', category.Paths.Take(TipLines)) + (category.Paths.Count > TipLines ? "\n" + Loc.F("uninstallMore", category.Paths.Count - TipLines) : "");
        row.MouseEnter += (_, _) => row.Background = Hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        return row;
    }

    static string Detail(CleanupCategory category)
    {
        var parts = new List<string> { Loc.T("cleanupWhy_" + category.Id) };
        if (category.Count > 0 && category.Method != CleanupMethod.Delete) parts.Add(Loc.F("cleanupItems", category.Count));
        if (category.Busy.Count > 0) parts.Add(Loc.F("cleanupBusy", string.Join(", ", category.Busy)));
        return string.Join(" · ", parts);
    }
}
