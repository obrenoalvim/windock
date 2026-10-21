using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class RigdeckPanel : Flyout
{
    const int Columns = 4;

    // These steal the foreground or need it back (Win key combos, the lock screen): leave the flyout first.
    static readonly HashSet<string> NeedFocus = ["lock", "desktop", "shot", "tasks"];

    // RigDeck palette (public/style.css of the rigdeck repo)
    static readonly Brush Raised = Paint("#1D2126"), Lit = Paint("#33FF8A1E"), Edge = Paint("#2B3038"), Orange = Paint("#FF8A1E");

    readonly RigdeckWidget deck;
    bool opening;

    // Raised for folder shortcuts: the dock swaps this panel for the folder's contents.
    public event Action<string>? Browse;
    readonly WrapPanel grid = new() { Width = Columns * 84 };
    readonly Dictionary<string, Border> buttons = [];

    public RigdeckPanel(RigdeckWidget deck) : base(true)
    {
        this.deck = deck;
        Brand("#F20A0B0D", "#55FF8A1E");
        var logo = new Image { Source = RigLogo.Image, Width = 30, Height = 30, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = Text("RigDeck", 14, font: Display);
        var tag = Text(Loc.T("rigdeckTag"), 9, muted: true);
        tag.Margin = new Thickness(0, 2, 0, 0);
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12),
            Children = { logo, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, tag } } }
        };

        var controls = new WrapPanel { Width = Columns * 84, Margin = new Thickness(-1, 0, 0, 8) };
        foreach (var control in RigRunner.Controls) controls.Children.Add(Control(control));

        var rule = new Border { Height = 1, Background = Edge, Margin = new Thickness(0, 0, 0, 8) };
        var scroll = new ScrollViewer { Content = grid, MaxHeight = 290, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(-3, 0, -3, 0) };
        var hint = Text(Loc.T("rigdeckHint"), 9.5, muted: true);
        hint.TextWrapping = TextWrapping.Wrap;
        hint.Margin = new Thickness(0, 8, 0, 6);
        var links = new StackPanel { Orientation = Orientation.Horizontal, Children = { Link("GitHub", "https://github.com/obrenoalvim/rigdeck") } };

        SetBody(new StackPanel { Width = Columns * 84, Children = { header, controls, rule, scroll, hint, links } });
        Fill();
        States();
    }

    Border Frame(UIElement content, string tip, double width, double height, double radius)
    {
        var tile = new Border
        {
            Width = width,
            Height = height,
            Margin = new Thickness(width < 40 ? 1 : 3),
            CornerRadius = new CornerRadius(radius),
            Background = Raised,
            Tag = Raised,
            BorderBrush = Edge,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = tip,
            Child = content
        };
        System.Windows.Automation.AutomationProperties.SetName(tile, tip);
        tile.MouseEnter += (_, _) => tile.Background = Lit;
        tile.MouseLeave += (_, _) => tile.Background = (Brush)tile.Tag;
        return tile;
    }

    Border Control(RigSystem control)
    {
        var glyph = Glyph(control.Glyph, 13);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        var button = Frame(glyph, Loc.T(control.Label), 28, 28, 9);
        button.MouseLeftButtonUp += async (_, e) =>
        {
            e.Handled = true;
            if (NeedFocus.Contains(control.Id))
            {
                SafeClose();
                await Task.Delay(180);
            }
            await RigRunner.Press(control.Id);
            if (IsLoaded) States();
        };
        buttons[control.Id] = button;
        return button;
    }

    // microphone and sound turn red while muted
    void States()
    {
        Tint("mic", Audio.ReadMic()?.Muted == true);
        Tint("mute", Audio.Read()?.Muted == true);
    }

    void Tint(string id, bool on)
    {
        if (!buttons.TryGetValue(id, out var button)) return;
        button.Tag = on ? Paint("#B3FF5C5C") : Raised;
        button.Background = (Brush)button.Tag;
    }

    void Fill()
    {
        grid.Children.Clear();
        var actions = deck.Actions;
        for (var i = 0; i < actions.Count; i++) grid.Children.Add(Tile(i, actions[i]));
        grid.Children.Add(AddTile("", Loc.T("rigdeckApps"), AddPrograms));
        grid.Children.Add(AddTile("", Loc.T("rigdeckScene"), () => Edit(null)));
    }

    Border AddTile(string glyph, string label, Action click)
    {
        var icon = Glyph(glyph, 18);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = Text(label, 9, muted: true);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.Margin = new Thickness(0, 6, 0, 0);
        var tile = Frame(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { icon, caption } }, label, 78, 70, 12);
        tile.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            click();
        };
        return tile;
    }

    // The windows below steal focus, which closes this flyout: close first, show them, then save.
    void AfterClose(Action show)
    {
        if (opening) return;
        opening = true;
        Closed += (_, _) => Application.Current.Dispatcher.BeginInvoke(show);
        SafeClose();
    }

    void AddPrograms() => AfterClose(() =>
    {
        var picker = new ProgramPicker { WindowStartupLocation = WindowStartupLocation.CenterScreen };
        if (picker.ShowDialog() != true) return;
        deck.Set(deck.Actions.Concat(picker.Chosen.Select(RigStore.FromPin)));
    });

    void Edit(int? index) => AfterClose(() =>
    {
        var actions = deck.Actions.ToList();
        var editor = new RigdeckEditor(index is { } i ? actions[i] : new RigAction { Parallel = true });
        if (editor.ShowDialog() != true) return;
        if (index is { } at) actions[at] = editor.Result;
        else actions.Add(editor.Result);
        deck.Set(actions);
    });

    void Remove(int index)
    {
        var actions = deck.Actions.ToList();
        actions.RemoveAt(index);
        deck.Set(actions);
        Fill();
    }

    TextBlock Link(string label, string url)
    {
        var link = Text(label, 9.5);
        link.Foreground = Orange;
        link.Cursor = Cursors.Hand;
        link.Margin = new Thickness(0, 0, 14, 0);
        link.MouseEnter += (_, _) => link.TextDecorations = TextDecorations.Underline;
        link.MouseLeave += (_, _) => link.TextDecorations = null;
        link.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Launcher.OpenFile(url);
            SafeClose();
        };
        return link;
    }

    Border Tile(int index, RigAction action)
    {
        UIElement icon = RigdeckWidget.IconOf(action) is { } image
            ? new Image { Source = image, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Center }
            : Glyph("", 20);
        if (icon is TextBlock glyph) glyph.HorizontalAlignment = HorizontalAlignment.Center;
        var label = Text(action.Name, 10);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Margin = new Thickness(0, 6, 0, 0);

        var edit = Glyph("", 9);
        edit.Opacity = 0;
        edit.HorizontalAlignment = HorizontalAlignment.Left;
        edit.VerticalAlignment = VerticalAlignment.Top;
        edit.Margin = new Thickness(6, 5, 0, 0);
        edit.ToolTip = Loc.T("rigdeckEdit");
        edit.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Edit(index);
        };
        var remove = Glyph("", 9);
        remove.Opacity = 0;
        remove.HorizontalAlignment = HorizontalAlignment.Right;
        remove.VerticalAlignment = VerticalAlignment.Top;
        remove.Margin = new Thickness(0, 5, 6, 0);
        remove.ToolTip = Loc.T("todoRemove");
        var armed = false;
        remove.MouseLeftButtonUp += async (_, e) =>
        {
            e.Handled = true;
            if (armed)
            {
                Remove(index);
                return;
            }
            armed = true;
            remove.Foreground = Paint("#FF5C5C");
            remove.ToolTip = Loc.T("rigdeckConfirm");
            await Task.Delay(2000);
            armed = false;
            remove.Foreground = Ink;
            remove.ToolTip = Loc.T("todoRemove");
        };

        var tile = Frame(new Grid
        {
            Children =
            {
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), Children = { icon, label } },
                edit,
                remove
            }
        }, action.Tip, 78, 70, 12);
        tile.MouseEnter += (_, _) => edit.Opacity = remove.Opacity = 1;
        tile.MouseLeave += (_, _) => edit.Opacity = remove.Opacity = 0;
        tile.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (action.Folder is { } folder && Browse is not null)
            {
                Browse(folder);
                return;
            }
            _ = RigRunner.Run(action);
            SafeClose();
        };
        tile.MouseRightButtonUp += async (_, e) =>
        {
            e.Handled = true;
            tile.Background = Paint(RigRunner.Close(action) > 0 ? "#664ADE80" : "#66FF5C5C");
            await Task.Delay(500);
            tile.Background = tile.IsMouseOver ? Lit : (Brush)tile.Tag;
        };
        return tile;
    }
}
