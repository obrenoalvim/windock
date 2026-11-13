using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public sealed class WelcomeWindow : Window
{
    static readonly Brush Ink = Paint("#F4EFE7");
    static readonly Brush Muted = Paint("#B3F4EFE7");
    static readonly Brush Line = Paint("#33FFFFFF");
    static readonly Brush Soft = Paint("#1AFFFFFF");
    static readonly Brush Amber = Paint("#FFB547");
    static readonly FontFamily Glyphs = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    static readonly string[] TryWidgets = [WidgetSpec.Clock, WidgetSpec.Weather, WidgetSpec.Pomodoro, WidgetSpec.Todo, WidgetSpec.Music, WidgetSpec.Claude];

    readonly MainViewModel main;
    readonly IReadOnlyList<WelcomeStep> steps;
    readonly IReadOnlyList<string> launchers;
    readonly Action openSettings;
    readonly List<PinItem> taken;
    readonly StackPanel rail = new() { Margin = new Thickness(0, 6, 0, 0) };
    readonly Border page = new();
    readonly StackPanel footer = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    int index;
    bool switched, widgetAdded;

    public WelcomeWindow(MainViewModel main, IReadOnlyList<WelcomeStep> steps, IReadOnlyList<string> launchers, Action openSettings)
    {
        this.main = main;
        this.steps = steps;
        this.launchers = launchers;
        this.openSettings = openSettings;
        taken = [.. main.Always.Model.Items.Where(i => i.Exists)];

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Width = 760;
        Height = 520;
        Topmost = true;
        ShowInTaskbar = true;
        Title = "WinDock";
        UseLayoutRounding = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = (FontFamily)Application.Current.FindResource("Mono");
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/app.ico"));

        var railBox = new Border { Padding = new Thickness(22, 24, 12, 24), Background = Paint("#10FFFFFF"), Child = new StackPanel { Children = { Brand(), rail } } };
        railBox.MouseLeftButtonDown += (_, _) => DragMove();
        var body = new Grid { Margin = new Thickness(30, 26, 30, 20) };
        body.RowDefinitions.Add(new RowDefinition());
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(page);
        Grid.SetRow(footer, 1);
        body.Children.Add(footer);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(railBox);
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);

        Content = new Border
        {
            Margin = new Thickness(14),
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
            Background = Paint("#F71C1E26"),
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Direction = 270, Opacity = 0.5 },
            Child = grid
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key is Key.Right or Key.Enter && index < steps.Count - 1) Go(1);
            else if (e.Key == Key.Left && index > 0) Go(-1);
        };
        main.PropertyChanged += OnMain;
        Closed += OnClosed;
        Render();
    }

    WelcomeStep Current => steps[index];

    void OnMain(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Active) || Current != WelcomeStep.Try) return;
        switched = true;
        Dispatcher.BeginInvoke(Render);
    }

    void OnClosed(object? sender, EventArgs e)
    {
        main.PropertyChanged -= OnMain;
        main.Settings.TourSeen = true;
        main.Settings.Save();
    }

    void Go(int delta)
    {
        index = Math.Clamp(index + delta, 0, steps.Count - 1);
        Render();
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    void Render()
    {
        FillRail();
        page.Child = Current switch
        {
            WelcomeStep.Hello => HelloPage(),
            WelcomeStep.Apps => AppsPage(),
            WelcomeStep.Games => GamesPage(),
            WelcomeStep.Look => LookPage(),
            WelcomeStep.Try => TryPage(),
            _ => DonePage()
        };
        FillFooter();
    }

    UIElement Brand() => new TextBlock
    {
        Text = "WinDock",
        FontFamily = (FontFamily)Application.Current.FindResource("Display"),
        FontSize = 19,
        Foreground = Amber
    };

    void FillRail()
    {
        rail.Children.Clear();
        rail.Margin = new Thickness(0, 28, 0, 0);
        for (var i = 0; i < steps.Count; i++)
        {
            var done = i < index;
            var now = i == index;
            var mark = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = done || now ? Amber : Brushes.Transparent,
                BorderBrush = done || now ? Amber : Line,
                BorderThickness = new Thickness(1.5),
                Child = done ? Glyph("", 10, Night) : null
            };
            var label = Label(Loc.T(StepKey(steps[i])), 12, now ? Ink : Muted);
            label.FontWeight = now ? FontWeights.SemiBold : FontWeights.Normal;
            label.Margin = new Thickness(12, 0, 0, 0);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16), Children = { mark, label } };
            var target = i;
            if (i < index)
            {
                row.Cursor = Cursors.Hand;
                row.MouseLeftButtonUp += (_, _) => Go(target - index);
            }
            rail.Children.Add(row);
        }
    }

    static Brush Night => Paint("#1C1E26");

    static string StepKey(WelcomeStep step) => step switch
    {
        WelcomeStep.Hello => "welHello",
        WelcomeStep.Apps => "welApps",
        WelcomeStep.Games => "welGames",
        WelcomeStep.Look => "welLook",
        WelcomeStep.Try => "welTry",
        _ => "welReady"
    };

    void FillFooter()
    {
        footer.Children.Clear();
        var last = index == steps.Count - 1;
        var skip = Label(Loc.T("welSkip"), 11, Muted);
        skip.Cursor = Cursors.Hand;
        skip.VerticalAlignment = VerticalAlignment.Center;
        skip.Margin = new Thickness(0, 0, 18, 0);
        skip.MouseLeftButtonUp += (_, _) => Close();
        if (!last) footer.Children.Add(skip);
        if (index > 0) footer.Children.Add(Button(Loc.T("welBack"), () => Go(-1), primary: false));
        footer.Children.Add(Button(Loc.T(last ? "welFinish" : "welNext"), last ? Close : () => Go(1), primary: true));
    }

    UIElement Frame(string title, string text, UIElement body)
    {
        var heading = Label(title, 24, Ink, display: true);
        var sub = Label(text, 12, Muted);
        sub.TextWrapping = TextWrapping.Wrap;
        sub.Margin = new Thickness(0, 8, 0, 20);
        sub.TextTrimming = TextTrimming.None;
        return new StackPanel { Children = { heading, sub, body } };
    }

    UIElement HelloPage()
    {
        var hint = Label(Loc.T("welHover"), 11, Muted);
        hint.Margin = new Thickness(0, 14, 0, 22);
        var language = new StackPanel { Orientation = Orientation.Horizontal, Children = { Label(Loc.T("trayLanguage"), 11, Muted) } };
        language.Children[0].SetValue(MarginProperty, new Thickness(0, 0, 12, 0));
        foreach (var (code, name) in new[] { (Loc.English, "English"), (Loc.Portuguese, "Português") })
            language.Children.Add(Chip(name, Loc.Instance.Language == code, () =>
            {
                main.SetLanguage(code);
                Render();
            }));
        return Frame(Loc.T("welHelloTitle"), Loc.T("welHelloText"), new StackPanel { Children = { MiniDock(), hint, language } });
    }

    UIElement MiniDock()
    {
        var pins = (main.Always.Pins.Count >= 3 ? main.Always.Pins : main.Active?.Pins ?? main.Always.Pins).Select(p => p.Icon).OfType<ImageSource>().Take(7).ToList();
        var strip = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        var icons = new List<FrameworkElement>();
        for (var i = 0; i < Math.Max(pins.Count, 5); i++)
        {
            FrameworkElement icon = i < pins.Count
                ? new Image { Source = pins[i], Width = 44, Height = 44 }
                : new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), Background = Soft };
            icon.Margin = new Thickness(5, 0, 5, 0);
            icon.RenderTransformOrigin = new Point(0.5, 1);
            icon.RenderTransform = new ScaleTransform(1, 1);
            icons.Add(icon);
            strip.Children.Add(icon);
        }
        var glass = new Border
        {
            Height = 84,
            Padding = new Thickness(10, 0, 10, 10),
            CornerRadius = new CornerRadius(22),
            Background = Paint("#22FFFFFF"),
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = strip
        };
        glass.MouseMove += (_, e) =>
        {
            var x = e.GetPosition(strip).X;
            foreach (var icon in icons)
            {
                var distance = Math.Abs(icon.TranslatePoint(new Point(icon.ActualWidth / 2, 0), strip).X - x);
                Magnify(icon, 1 + 0.75 * Math.Exp(-distance * distance / (2 * 48 * 48)));
            }
        };
        glass.MouseLeave += (_, _) => icons.ForEach(i => Magnify(i, 1));
        return glass;
    }

    static void Magnify(FrameworkElement icon, double scale)
    {
        var transform = (ScaleTransform)icon.RenderTransform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(90)));
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(90)));
    }

    UIElement AppsPage()
    {
        var count = taken.Count;
        var grid = new WrapPanel { Margin = new Thickness(0, 0, 0, 22) };
        foreach (var item in taken.Take(24))
        {
            var icon = ShellIcons.ForPin(item, 48);
            grid.Children.Add(icon is null
                ? new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Soft, Margin = new Thickness(0, 0, 10, 10) }
                : new Image { Source = icon, Width = 36, Height = 36, Margin = new Thickness(0, 0, 10, 10), ToolTip = item.Name });
        }
        var everywhere = main.Always.Model.Items.Any(taken.Contains);
        var env = main.Active?.Name ?? "";
        var row = Setting(Loc.T("welEverywhere"), Loc.F("welEverywhereHint", env), everywhere, on => Spread(on));
        return Frame(Loc.T("welAppsTitle"), Loc.F(count == 1 ? "welAppsOne" : "welAppsText", count), new StackPanel { Children = { grid, row } });
    }

    void Spread(bool everywhere)
    {
        var (from, to) = everywhere ? (main.Active, main.Always) : (main.Always, main.Active);
        if (from is null || to is null) return;
        from.ReplaceItems(from.Model.Items.Where(i => !taken.Contains(i)).ToList());
        to.AddItems(taken.Where(i => !to.Model.Items.Contains(i)).ToList());
    }

    UIElement GamesPage()
    {
        var target = main.Presets.FirstOrDefault(p => p.Id == "jogos") ?? main.Active;
        var list = new StackPanel();
        foreach (var platform in launchers)
        {
            var kind = platform.Replace(".", "").ToLowerInvariant();
            var choice = target?.Widgets.FirstOrDefault(w => w.Kind == kind);
            var logo = Games.Launcher(platform) is { } exe ? ShellIcons.ForPath(exe, 48) : null;
            var count = Games.Installed(platform).Count;
            var name = Label(platform, 13, Ink);
            var detail = Label(Loc.F(count == 1 ? "gamesCountOne" : "gamesCount", count), 10.5, Muted);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Children = { name, detail } };
            var picture = logo is null ? (UIElement)Glyph("", 22, Ink) : new Image { Source = logo, Width = 30, Height = 30 };
            var on = choice?.IsOn == true;
            var add = on
                ? Added()
                : (UIElement)Button(Loc.F("welAddTo", target?.Name ?? ""), () =>
                {
                    if (choice is not null) choice.IsOn = true;
                    Render();
                }, primary: false);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(add, Dock.Right);
            row.Children.Add(add);
            row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { picture, text } });
            list.Children.Add(row);
        }
        return Frame(Loc.T("welGamesTitle"), Loc.T("welGamesText"), list);
    }

    static UIElement Added()
    {
        var mark = Glyph("", 12, Amber);
        mark.Margin = new Thickness(0, 0, 8, 0);
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, Label(Loc.T("welAdded"), 11, Amber) } };
    }

    UIElement LookPage()
    {
        var options = main.Dock;
        var themes = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        foreach (var theme in DockTheme.All)
            themes.Children.Add(Chip(Loc.T("theme" + char.ToUpperInvariant(theme.Id[0]) + theme.Id[1..]), options.Theme == theme.Id, () =>
            {
                options.Theme = theme.Id;
                Render();
            }));
        var lens = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        foreach (var (id, key) in new[] { (DockSettings.LensNone, "lensNone"), (DockSettings.LensScale, "lensScale"), (DockSettings.LensMagnify, "lensMagnify") })
            lens.Children.Add(Chip(Loc.T(key), options.Lens == id, () =>
            {
                options.Lens = id;
                Render();
            }));
        var size = new Slider { Minimum = 32, Maximum = 72, Value = options.IconSize, Width = 220, VerticalAlignment = VerticalAlignment.Center, Style = (Style)Application.Current.FindResource("FlatSlider") };
        size.ValueChanged += (_, e) => options.IconSize = e.NewValue;
        var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16), Children = { Label(Loc.T("iconSize"), 11, Muted, width: 150), size } };
        var replace = Setting(Loc.T("replaceTaskbar"), Loc.T("welReplaceHint"), options.ReplaceTaskbar, on => options.ReplaceTaskbar = on);
        return Frame(Loc.T("welLookTitle"), Loc.T("welLookText"), new StackPanel
        {
            Children = { Section(Loc.T("theme")), themes, Section(Loc.T("lens")), lens, sizeRow, replace }
        });
    }

    UIElement TryPage()
    {
        var chips = new WrapPanel { Margin = new Thickness(0, 4, 0, 18) };
        foreach (var kind in TryWidgets)
        {
            var choice = main.Active?.Widgets.FirstOrDefault(w => w.Kind == kind);
            if (choice is null) continue;
            chips.Children.Add(Chip(choice.Label, choice.IsOn, () =>
            {
                choice.IsOn = !choice.IsOn;
                widgetAdded |= choice.IsOn;
                Render();
            }));
        }
        var tips = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        foreach (var (glyph, key) in new[] { ("", "welTip1"), ("", "welTip2"), ("", "welTip3"), ("", "welTip4") })
        {
            var icon = Glyph(glyph, 13, Amber);
            icon.Margin = new Thickness(0, 0, 10, 0);
            tips.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7), Children = { icon, Label(Loc.T(key), 11, Muted) } });
        }
        return Frame(Loc.T("welTryTitle"), Loc.T("welTryText"), new StackPanel
        {
            Children = { Check(Loc.T("welTrySwitch"), switched), Check(Loc.T("welTryWidget"), widgetAdded), chips, tips }
        });
    }

    UIElement Check(string text, bool done)
    {
        var mark = Glyph(done ? "" : "", 15, done ? Amber : Muted);
        mark.Margin = new Thickness(0, 0, 10, 0);
        return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 9), Children = { mark, Label(text, 12.5, done ? Ink : Muted) } };
    }

    UIElement DonePage()
    {
        var startup = Setting(Loc.T("trayStartup"), "", main.Dock.StartWithWindows, on => main.Dock.StartWithWindows = on);
        var settings = Button(Loc.T("welSettings"), () =>
        {
            Close();
            openSettings();
        }, primary: false);
        settings.HorizontalAlignment = HorizontalAlignment.Left;
        settings.Margin = new Thickness(0, 14, 0, 0);
        return Frame(Loc.T("welDoneTitle"), Loc.T("welDoneText"), new StackPanel { Children = { startup, settings } });
    }

    UIElement Setting(string title, string hint, bool on, Action<bool> changed)
    {
        var text = new StackPanel { Margin = new Thickness(0, 0, 18, 0), Children = { Label(title, 12.5, Ink) } };
        if (hint.Length > 0)
        {
            var detail = Label(hint, 10.5, Muted);
            detail.TextWrapping = TextWrapping.Wrap;
            detail.TextTrimming = TextTrimming.None;
            detail.Margin = new Thickness(0, 3, 0, 0);
            text.Children.Add(detail);
        }
        var toggle = Switch(on, changed);
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(toggle, Dock.Right);
        row.Children.Add(toggle);
        row.Children.Add(text);
        return row;
    }

    static UIElement Switch(bool on, Action<bool> changed)
    {
        var knob = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Brushes.White, Margin = new Thickness(on ? 20 : 2, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        var track = new Border
        {
            Width = 38,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = on ? Amber : Paint("#40FFFFFF"),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = knob
        };
        track.MouseLeftButtonUp += (_, _) =>
        {
            on = !on;
            track.Background = on ? Amber : Paint("#40FFFFFF");
            knob.Margin = new Thickness(on ? 20 : 2, 0, 0, 0);
            changed(on);
        };
        return track;
    }

    static ToggleButton Chip(string label, bool on, Action click)
    {
        var chip = new ToggleButton { Content = label, Style = (Style)Application.Current.FindResource("ChipToggle"), IsChecked = on, Margin = new Thickness(0, 0, 6, 6) };
        chip.Click += (_, _) => click();
        return chip;
    }

    static Border Button(string text, Action click, bool primary)
    {
        var button = new Border
        {
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(8, 0, 0, 0),
            CornerRadius = new CornerRadius(11),
            Background = primary ? Amber : Brushes.Transparent,
            BorderBrush = primary ? Amber : Line,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = Label(text, 12, primary ? Night : Ink)
        };
        ((TextBlock)button.Child).FontWeight = FontWeights.SemiBold;
        System.Windows.Automation.AutomationProperties.SetName(button, text);
        button.MouseEnter += (_, _) => button.Opacity = 0.85;
        button.MouseLeave += (_, _) => button.Opacity = 1;
        button.MouseLeftButtonUp += (_, _) => click();
        return button;
    }

    static UIElement Section(string text)
    {
        var label = Label(text, 10.5, Muted);
        label.Margin = new Thickness(0, 0, 0, 7);
        return label;
    }

    static TextBlock Label(string text, double size, Brush color, bool display = false, double width = 0)
    {
        var label = new TextBlock { Text = text, FontSize = size, Foreground = color, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        if (display) label.FontFamily = (FontFamily)Application.Current.FindResource("Display");
        if (width > 0) label.Width = width;
        return label;
    }

    static TextBlock Glyph(string glyph, double size, Brush color) => new()
    {
        Text = glyph,
        FontFamily = Glyphs,
        FontSize = size,
        Foreground = color,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    static SolidColorBrush Paint(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
