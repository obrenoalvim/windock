using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class ShelfPanel : Flyout
{
    const double PanelWidth = 320;
    const double DragDistance = 6;
    static readonly Brush Accent = Paint("#4C8DFF");

    readonly ShelfWidget shelf;
    readonly StackPanel body = new() { Width = PanelWidth };
    readonly HashSet<string> unselected = new(StringComparer.OrdinalIgnoreCase);
    readonly Action<string> notify;

    public ShelfPanel(ShelfWidget shelf, bool dark, Action<string> notify) : base(dark)
    {
        this.shelf = shelf;
        this.notify = notify;
        shelf.Prune();
        SetBody(body);
        AllowDrop = true;
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
            e.Handled = true;
        };
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files) shelf.Add(files);
        };
        Render();
        shelf.Changed += OnChanged;
        Closed += (_, _) => shelf.Changed -= OnChanged;
    }

    void OnChanged() => Dispatcher.BeginInvoke(Render);

    List<string> Chosen() => shelf.Files.Where(f => !unselected.Contains(f)).ToList();

    public static DataObject Data(IReadOnlyList<string> files) => new(DataFormats.FileDrop, files.ToArray());

    void Render()
    {
        body.Children.Clear();
        var files = shelf.Files;
        unselected.IntersectWith(files);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        if (files.Count > 0)
        {
            var count = Text(Loc.F(files.Count == 1 ? "shelfOne" : "shelfMany", files.Count), 11, muted: true);
            DockPanel.SetDock(count, Dock.Right);
            header.Children.Add(count);
        }
        var icon = Glyph(ShelfWidget.ShelfGlyph, 14);
        icon.Margin = new Thickness(0, 0, 8, 0);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, Text(Loc.T("widget_shelf"), 14, font: Display) } });
        body.Children.Add(header);

        if (files.Count == 0)
        {
            var note = Text(Loc.T("shelfEmpty"), 11, muted: true);
            note.TextWrapping = TextWrapping.Wrap;
            note.TextTrimming = TextTrimming.None;
            note.Margin = new Thickness(0, 0, 0, 12);
            body.Children.Add(note);
            return;
        }

        body.Children.Add(DragAll());
        var list = new StackPanel { Margin = new Thickness(-6, 0, -6, 0) };
        foreach (var file in files) list.Children.Add(Row(file));
        body.Children.Add(new ScrollViewer { Content = list, MaxHeight = 260, Margin = new Thickness(0, 0, 0, 10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        var footer = new WrapPanel();
        footer.Children.Add(Button(Loc.T("shelfCopy"), "", Copy));
        footer.Children.Add(Button(Loc.T("shelfClear"), "", shelf.Clear));
        body.Children.Add(footer);
    }

    Border DragAll()
    {
        var chosen = Chosen();
        var label = Text(Loc.F("shelfDragAll", chosen.Count), 12);
        label.Foreground = Brushes.White;
        var arrow = Glyph("", 16);
        arrow.Foreground = Brushes.White;
        arrow.Margin = new Thickness(0, 0, 10, 0);
        var area = new Border
        {
            Padding = new Thickness(12, 12, 12, 12),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = new CornerRadius(12),
            Background = Accent,
            Cursor = Cursors.SizeAll,
            Opacity = chosen.Count == 0 ? 0.5 : 1,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { arrow, label } }
        };
        Draggable(area, Chosen);
        return area;
    }

    void Draggable(UIElement element, Func<List<string>> files)
    {
        Point? start = null;
        element.PreviewMouseLeftButtonDown += (_, e) => start = e.GetPosition(this);
        element.PreviewMouseLeftButtonUp += (_, _) => start = null;
        element.PreviewMouseMove += (_, e) =>
        {
            if (start is not { } from || e.LeftButton != MouseButtonState.Pressed) return;
            var now = e.GetPosition(this);
            if (Math.Abs(now.X - from.X) < DragDistance && Math.Abs(now.Y - from.Y) < DragDistance) return;
            start = null;
            var chosen = files().Where(f => File.Exists(f) || Directory.Exists(f)).ToList();
            if (chosen.Count > 0) DragDrop.DoDragDrop(element, Data(chosen), DragDropEffects.Copy | DragDropEffects.Link);
        };
    }

    Border Row(string file)
    {
        var selected = !unselected.Contains(file);
        var check = new CheckBox { IsChecked = selected, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        check.Click += (_, _) =>
        {
            if (!unselected.Remove(file)) unselected.Add(file);
            Render();
        };
        var image = new Image { Source = ShellIcons.ForPath(file, 32), Width = 24, Height = 24, Margin = new Thickness(0, 0, 8, 0) };
        var remove = Glyph("", 10);
        remove.Opacity = 0;
        remove.Cursor = Cursors.Hand;
        remove.Margin = new Thickness(8, 0, 0, 0);
        remove.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            shelf.Remove([file]);
        };
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text(Path.GetFileName(file.TrimEnd('\\')), 11.5), Text(Path.GetDirectoryName(file) ?? "", 9.5, muted: true) } };
        var line = new DockPanel();
        DockPanel.SetDock(check, Dock.Left);
        DockPanel.SetDock(image, Dock.Left);
        DockPanel.SetDock(remove, Dock.Right);
        line.Children.Add(check);
        line.Children.Add(image);
        line.Children.Add(remove);
        line.Children.Add(texts);
        var row = new Border { Padding = new Thickness(6, 4, 6, 4), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Child = line, ToolTip = file };
        row.MouseEnter += (_, _) =>
        {
            row.Background = Hover;
            remove.Opacity = 1;
        };
        row.MouseLeave += (_, _) =>
        {
            row.Background = Brushes.Transparent;
            remove.Opacity = 0;
        };
        row.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            Launcher.Reveal(file);
        };
        Draggable(texts, () => [file]);
        Draggable(image, () => [file]);
        return row;
    }

    void Copy()
    {
        var chosen = Chosen().Where(f => File.Exists(f) || Directory.Exists(f)).ToList();
        if (chosen.Count == 0) return;
        Clipboard.SetDataObject(Data(chosen), true);
        notify(Loc.F("shelfCopied", chosen.Count));
    }

    Border Button(string label, string glyph, Action click)
    {
        var button = Action(label, click, glyph);
        button.Margin = new Thickness(0, 0, 6, 6);
        return button;
    }
}
