using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class GamesPanel : Flyout
{
    readonly GamesWidget widget;
    readonly StackPanel list = new();
    readonly TextBox search;
    readonly TextBlock note;
    List<GameEntry> games = [];
    int loaded;

    public GamesPanel(GamesWidget widget, bool dark) : base(dark)
    {
        this.widget = widget;
        var title = Text(widget.Name, 13, font: Display);
        title.Margin = new Thickness(0, 0, 18, 0);

        var chips = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (widget.IsSteam)
        {
            chips.Children.Add(Chip(Loc.T("gamesInstalled"), false));
            chips.Children.Add(Chip(Loc.T("gamesAll"), true));
        }
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(chips, Dock.Right);
        header.Children.Add(chips);
        header.Children.Add(title);

        search = new TextBox { Style = (Style)Application.Current.FindResource("FlatBox"), Foreground = Ink, CaretBrush = Ink, ToolTip = Loc.T("gamesSearch") };
        search.TextChanged += (_, _) => Fill();
        note = Text("", 10, muted: true);
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(0, 8, 0, 0);
        note.Visibility = Visibility.Collapsed;
        list.Margin = new Thickness(-4, 8, -4, 0);

        var scroll = new ScrollViewer { Content = list, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        SetBody(new StackPanel { Width = 300, Children = { header, search, scroll, note } });
        Load();
        Loaded += (_, _) => search.Focus();
    }

    ToggleButton Chip(string label, bool all)
    {
        var chip = new ToggleButton
        {
            Content = label,
            Style = (Style)Application.Current.FindResource("ChipToggle"),
            IsChecked = widget.ShowAll == all,
            Margin = new Thickness(4, 0, 0, 0)
        };
        chip.Click += (_, _) =>
        {
            widget.ShowAll = all;
            foreach (var other in ((Panel)chip.Parent).Children.OfType<ToggleButton>()) other.IsChecked = other == chip;
            Load();
        };
        return chip;
    }

    async void Load()
    {
        var mine = ++loaded;
        note.Visibility = Visibility.Collapsed;
        games = Games.Installed(widget.Platform);
        Fill();
        if (!widget.ShowAll) return;
        note.Text = Loc.T("gamesLoading");
        note.Visibility = Visibility.Visible;
        var (all, complete) = await Games.All();
        if (mine != loaded || !IsLoaded) return;
        games = all;
        note.Text = Loc.T("gamesPrivate");
        note.Visibility = complete ? Visibility.Collapsed : Visibility.Visible;
        Fill();
    }

    void Fill()
    {
        list.Children.Clear();
        var text = search.Text.Trim();
        var shown = games.Where(g => text.Length == 0 || g.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)).ToList();
        foreach (var game in shown) list.Children.Add(Row(game));
        if (shown.Count == 0)
        {
            var none = Text(Loc.T("gamesNone"), 11, muted: true);
            none.Margin = new Thickness(6, 6, 6, 6);
            list.Children.Add(none);
        }
    }

    Border Row(GameEntry game)
    {
        UIElement icon = game.Icon is { } source && ShellIcons.ForPath(source, 32) is { } image
            ? new Image { Source = image, Width = 24, Height = 24 }
            : Glyph("", 16);
        if (icon is TextBlock g) g.Width = 24;
        var name = Text(game.Name, 11.5);
        var platform = Text(game.Platform, 9, muted: true);
        platform.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(platform, Dock.Right);
        DockPanel.SetDock((FrameworkElement)icon, Dock.Left);
        ((FrameworkElement)icon).Margin = new Thickness(0, 0, 10, 0);

        var row = new Border
        {
            Padding = new Thickness(6, 5, 6, 5),
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Opacity = game.Installed ? 1 : 0.55,
            ToolTip = game.Installed ? game.Name : Loc.T("gamesInstall"),
            Child = new DockPanel { Children = { icon, platform, name } }
        };
        System.Windows.Automation.AutomationProperties.SetName(row, game.Name);
        row.MouseEnter += (_, _) => row.Background = Hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Launcher.OpenFile(game.Uri);
            SafeClose();
        };
        return row;
    }
}
