using System.Windows;
using System.Windows.Controls;
using WinDock.Services;

namespace WinDock.Views;

public sealed class TourPanel : Flyout
{
    static readonly (string Glyph, string Key)[] Steps =
    [
        ("", "tour1"),
        ("", "tour2"),
        ("", "tour3"),
        ("", "tour4")
    ];

    readonly TextBlock glyph, title, text, count;
    readonly TextBlock nextLabel;
    int step;

    public TourPanel(bool dark) : base(dark)
    {
        glyph = Glyph("", 18);
        glyph.Margin = new Thickness(0, 0, 10, 0);
        title = Text("", 13, font: Display);
        text = Text("", 11.5, muted: true);
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        text.Width = 300;
        text.Margin = new Thickness(0, 10, 0, 14);
        count = Text("", 10, muted: true);

        var next = Action("", Next);
        nextLabel = (TextBlock)((StackPanel)next.Child).Children[0];
        var skip = Action(Loc.T("tourSkip"), SafeClose);
        skip.Margin = new Thickness(0, 0, 6, 0);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { skip, next } };
        var footer = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        footer.Children.Add(count);

        SetBody(new StackPanel
        {
            Width = 300,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Children = { glyph, title } },
                text,
                footer
            }
        });
        Show(0);
    }

    void Show(int index)
    {
        step = index;
        var (icon, key) = Steps[index];
        glyph.Text = icon;
        title.Text = Loc.T(key + "Title");
        text.Text = Loc.T(key + "Text");
        count.Text = Loc.F("tourStep", index + 1, Steps.Length);
        nextLabel.Text = Loc.T(index == Steps.Length - 1 ? "tourDone" : "tourNext");
    }

    void Next()
    {
        if (step == Steps.Length - 1) SafeClose();
        else Show(step + 1);
    }
}
