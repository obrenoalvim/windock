using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

// The to-do list: type and Enter to add, the circle checks a task off, the cross removes it.
public sealed class TodoPanel : Flyout
{
    static readonly Brush Green = Paint("#2FB37A");

    readonly TodoWidget todo;
    readonly StackPanel list = new();
    readonly Border clear;

    public TodoPanel(TodoWidget todo, bool dark) : base(dark)
    {
        this.todo = todo;
        var title = Text(Loc.T("widget_todo"), 13, font: Display);
        title.Margin = new Thickness(0, 0, 0, 10);
        var box = new TextBox
        {
            Width = 280,
            Style = (Style)Application.Current.FindResource("FlatBox"),
            Foreground = Ink,
            CaretBrush = Ink
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || box.Text.Trim() is not { Length: > 0 } text) return;
            e.Handled = true;
            Change(items => items.Add(new TodoItem(text, false)));
            box.Clear();
        };
        var hint = Text(Loc.T("todoHint"), 10, muted: true);
        hint.Margin = new Thickness(0, 8, 0, 0);
        clear = Action(Loc.T("todoClear"), () => Change(items => items.RemoveAll(i => i.Done)), "");
        clear.Margin = new Thickness(0, 10, 0, 0);
        clear.HorizontalAlignment = HorizontalAlignment.Left;
        var scroll = new ScrollViewer { Content = list, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        SetBody(new StackPanel { Width = 280, Children = { title, scroll, box, hint, clear } });
        Fill();
        Loaded += (_, _) => box.Focus();
    }

    void Change(Action<List<TodoItem>> edit)
    {
        var items = todo.Items.ToList();
        edit(items);
        todo.Set(items);
        Fill();
    }

    void Fill()
    {
        list.Children.Clear();
        var items = todo.Items;
        for (var i = 0; i < items.Count; i++) list.Children.Add(Row(i, items[i]));
        list.Margin = new Thickness(-4, 0, -4, items.Count > 0 ? 8 : 0);
        clear.Visibility = items.Any(i => i.Done) ? Visibility.Visible : Visibility.Collapsed;
    }

    Border Row(int index, TodoItem item)
    {
        var mark = Glyph(item.Done ? "" : "", 9);
        mark.Foreground = Brushes.White;
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        var check = new Border
        {
            Width = 17,
            Height = 17,
            CornerRadius = new CornerRadius(8.5),
            BorderThickness = new Thickness(1.5),
            BorderBrush = item.Done ? Green : Muted,
            Background = item.Done ? Green : Brushes.Transparent,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = mark
        };
        var text = Text(item.Text, 11.5, muted: item.Done);
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        if (item.Done) text.TextDecorations = TextDecorations.Strikethrough;
        var remove = Glyph("", 10);
        remove.Opacity = 0;
        remove.Margin = new Thickness(8, 0, 0, 0);
        remove.ToolTip = Loc.T("todoRemove");

        var line = new DockPanel();
        DockPanel.SetDock(check, Dock.Left);
        DockPanel.SetDock(remove, Dock.Right);
        line.Children.Add(check);
        line.Children.Add(remove);
        line.Children.Add(text);
        var row = new Border
        {
            Padding = new Thickness(4, 5, 4, 5),
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = line
        };
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
        remove.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Change(items => items.RemoveAt(index));
        };
        row.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Change(items => items[index] = items[index] with { Done = !items[index].Done });
        };
        return row;
    }
}
