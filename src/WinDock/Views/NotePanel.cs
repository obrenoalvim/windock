using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class NotePanel : Flyout
{
    public NotePanel(NoteWidget note, bool dark) : base(dark)
    {
        var box = new TextBox
        {
            Text = note.Note,
            Width = 280,
            Height = 120,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Style = (Style)Application.Current.FindResource("FlatBox"),
            Foreground = Ink,
            CaretBrush = Ink
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                SafeClose();
            }
        };
        var hint = Text(Loc.T("noteHint"), 10, muted: true);
        hint.Margin = new Thickness(0, 8, 0, 0);
        var title = Text(Loc.T("widget_note"), 13, font: Display);
        title.Margin = new Thickness(0, 0, 0, 10);
        SetBody(new StackPanel { Children = { title, box, hint } });

        Loaded += (_, _) =>
        {
            box.Focus();
            box.CaretIndex = box.Text.Length;
        };
        Closed += (_, _) => note.Note = box.Text;
    }
}
