using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;

namespace WinDock.Views;

public sealed class StackWindow : Flyout
{
    const int Columns = 4, Max = 16;
    const double Cell = 92;

    readonly FolderStack folder;
    Point? pressed;

    public StackWindow(FolderStack folder, bool dark) : base(dark)
    {
        this.folder = folder;
        SetBody(Build());
    }

    UIElement Build()
    {
        var header = new DockPanel { Margin = new Thickness(2, 0, 0, 12), LastChildFill = true };
        var open = Text(Loc.T("openInExplorer"), 11, muted: true);
        open.Cursor = Cursors.Hand;
        open.MouseEnter += (_, _) => open.Foreground = Ink;
        open.MouseLeave += (_, _) => open.Foreground = Muted;
        open.MouseLeftButtonUp += (_, _) =>
        {
            Launcher.OpenFile(folder.Path);
            SafeClose();
        };
        DockPanel.SetDock(open, Dock.Right);
        header.Children.Add(open);
        var title = Text(folder.Name, 13, font: Display);
        title.Margin = new Thickness(0, 0, 18, 0);
        header.Children.Add(title);

        var grid = new WrapPanel { Width = Columns * Cell };
        var entries = FolderStack.Entries(folder.Path, Max);
        foreach (var entry in entries) grid.Children.Add(Tile(entry));
        if (entries.Count == 0)
        {
            var empty = Text(Loc.T("emptyFolder"), 11, muted: true);
            empty.Margin = new Thickness(6, 8, 6, 12);
            grid.Children.Add(empty);
        }

        return new StackPanel { Children = { header, grid } };
    }

    UIElement Tile(FolderEntry entry)
    {
        var icon = new Image { Source = ShellIcons.Thumbnail(entry.Path, 96) ?? ShellIcons.ForPath(entry.Path, 96), Width = 52, Height = 52 };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var name = Text(entry.Name, 10);
        name.TextAlignment = TextAlignment.Center;
        name.TextWrapping = TextWrapping.Wrap;
        name.MaxHeight = 28;
        name.Margin = new Thickness(0, 6, 0, 0);
        var cell = new Border
        {
            Width = Cell - 4,
            Margin = new Thickness(2),
            Padding = new Thickness(4, 8, 4, 6),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = entry.Name,
            Child = new StackPanel { Children = { icon, name } }
        };
        cell.MouseEnter += (_, _) => cell.Background = Hover;
        cell.MouseLeave += (_, _) =>
        {
            cell.Background = Brushes.Transparent;
            pressed = null;
        };
        cell.MouseLeftButtonDown += (_, e) => pressed = e.GetPosition(this);
        cell.MouseMove += (_, e) =>
        {
            if (pressed is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            var now = e.GetPosition(this);
            if (Math.Abs(now.X - start.X) < 6 && Math.Abs(now.Y - start.Y) < 6) return;
            pressed = null;
            DragDrop.DoDragDrop(cell, new DataObject(DataFormats.FileDrop, new[] { entry.Path }), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        };
        cell.MouseLeftButtonUp += (_, _) =>
        {
            if (pressed is null) return;
            pressed = null;
            Launcher.OpenFile(entry.Path);
            SafeClose();
        };
        cell.MouseRightButtonUp += (_, _) =>
        {
            Launcher.Reveal(entry.Path);
            SafeClose();
        };
        return cell;
    }
}
