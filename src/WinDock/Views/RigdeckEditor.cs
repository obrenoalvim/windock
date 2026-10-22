using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WinDock.Services;
using Forms = System.Windows.Forms;

namespace WinDock.Views;

public sealed class RigdeckEditor : Window
{
    readonly RigAction action;
    readonly TextBox name;
    readonly StackPanel rows = new();
    readonly ScrollViewer scroll;

    public RigAction Result => action;

    static T Res<T>(string key) => (T)Application.Current.FindResource(key);

    public RigdeckEditor(RigAction source)
    {
        action = source.Clone();
        Width = 640;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        FontFamily = Res<FontFamily>("Mono");
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
        };

        var ink = Res<Brush>("Ink");
        var muted = Res<Brush>("Muted");

        var title = new TextBlock { Text = Loc.T("rigdeckEditTitle"), FontFamily = Res<FontFamily>("Display"), FontSize = 15, Foreground = ink };
        var sub = new TextBlock { Text = Loc.T("rigdeckStepsHint"), FontSize = 11, Foreground = muted, Margin = new Thickness(0, 6, 0, 0) };
        var header = new StackPanel { Background = Brushes.Transparent, Margin = new Thickness(0, 0, 0, 16), Children = { title, sub } };
        header.MouseLeftButtonDown += (_, _) => DragMove();

        name = new TextBox { Text = action.Name, Style = Res<Style>("FlatBox"), Foreground = ink, CaretBrush = ink, Margin = new Thickness(0, 4, 0, 14) };
        var nameLabel = new TextBlock { Text = Loc.T("rigdeckName"), FontSize = 10, Foreground = muted };

        scroll = new ScrollViewer { Content = rows, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        var add = new Button { Content = "+  " + Loc.T("rigdeckAddStep"), Style = Res<Style>("ActionButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        add.Click += (_, _) => AddMenu(add);

        var cancel = new Button { Content = Loc.T("cancel"), Style = Res<Style>("GhostButton"), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = Loc.T("rigdeckSave"), Style = Res<Style>("ActionButton"), Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x1E)), Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0x11, 0x14)), FontWeight = FontWeights.Bold };
        save.Click += (_, _) => Save();
        var together = new ToggleButton { Style = Res<Style>("ChipToggle"), Content = Loc.T("rigdeckParallel"), IsChecked = action.Parallel, Margin = new Thickness(10, 12, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        together.Click += (_, _) => action.Parallel = together.IsChecked == true;
        var footer = new DockPanel { Margin = new Thickness(0, 18, 0, 0) };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Children = { cancel, save } };
        DockPanel.SetDock(right, Dock.Right);
        footer.Children.Add(right);
        footer.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { add, together } });

        var card = new Border
        {
            CornerRadius = new CornerRadius(28),
            Background = Res<Brush>("ShellBrush"),
            BorderBrush = Res<Brush>("RimBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 22, 24, 22),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 46, ShadowDepth = 12, Direction = 270, Opacity = 0.6, Color = Colors.Black },
            Child = new StackPanel { Children = { header, nameLabel, name, scroll, footer } }
        };
        Content = new Grid { Margin = new Thickness(26), Children = { card } };
        Fill();
        Loaded += (_, _) =>
        {
            Activate();
            name.Focus();
        };
    }

    void Save()
    {
        action.Steps.RemoveAll(s => s.Target.Trim().Length == 0);
        if (action.Steps.Count == 0) return;
        action.Name = name.Text.Trim() is { Length: > 0 } typed ? typed : Fallback();
        DialogResult = true;
    }

    string Fallback()
    {
        var target = action.Steps[0].Target;
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Host.Length > 0) return uri.Host;
        return Path.GetFileNameWithoutExtension(target) is { Length: > 0 } file ? file : Loc.T("rigdeckScene");
    }

    void AddMenu(Button anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Top };
        void Item(string label, Action click, ItemsControl? into = null)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => click();
            (into ?? menu).Items.Add(item);
        }

        Item(Loc.T("rigStepProgram"), AddPrograms);
        Item(Loc.T("rigStepSite"), () => Add(RigStep.Open, "https://"));
        Item(Loc.T("rigStepCommand"), () => Add(RigStep.Command, ""));
        Item(Loc.T("rigStepKeys"), () => Add(RigStep.Keys, ""));
        var system = new MenuItem { Header = Loc.T("rigStepSystem") };
        foreach (var control in RigRunner.Controls) Item(Loc.T(control.Label), () => Add(RigStep.System, control.Id), system);
        menu.Items.Add(system);
        Item(Loc.T("rigStepSound"), AddSound);
        Item(Loc.T("rigStepWait"), () => Add(RigStep.Wait, "1"));
        menu.IsOpen = true;
    }

    void Add(string type, string target)
    {
        action.Steps.Add(new RigStepSpec { Type = type, Target = target });
        Fill();
        scroll.ScrollToEnd();
    }

    void AddPrograms()
    {
        var picker = new ProgramPicker { Owner = this };
        if (picker.ShowDialog() != true) return;
        foreach (var pin in picker.Chosen.Select(RigStore.FromPin))
        {
            action.Steps.Add(new RigStepSpec { Target = pin.Steps[0].Target, Icon = pin.Icon });
            if (action.Icon is null && action.Steps.Count == 1) action.Icon = pin.Icon;
            if (name.Text.Trim().Length == 0) name.Text = pin.Name;
        }
        Fill();
        scroll.ScrollToEnd();
    }

    void AddSound()
    {
        var dialog = new OpenFileDialog { Filter = Loc.T("rigSoundFilter") };
        if (dialog.ShowDialog(this) == true) Add(RigStep.Sound, dialog.FileName);
    }

    void Fill()
    {
        rows.Children.Clear();
        for (var i = 0; i < action.Steps.Count; i++) rows.Children.Add(Row(i, action.Steps[i]));
    }

    static string TypeLabel(RigStepSpec step) => Loc.T(step.Type switch
    {
        RigStep.Open => step.Target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "rigStepSite" : "rigStepProgram",
        RigStep.Command => "rigStepCommand",
        RigStep.Keys => "rigStepKeys",
        RigStep.System => "rigStepSystem",
        RigStep.Sound => "rigStepSound",
        _ => "rigStepWait"
    });

    FrameworkElement StepIcon(int index, RigStepSpec step)
    {
        var control = step.Type == RigStep.System ? RigRunner.Controls.FirstOrDefault(c => c.Id == step.Target) : null;
        var source = step.Icon ?? (action.FirstOpen == step ? action.Icon : null) ?? step.Target;
        var image = step.Type == RigStep.Open && !step.Target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? ShellIcons.ForPath(Environment.ExpandEnvironmentVariables(source))
            : null;
        FrameworkElement icon = image is not null
            ? new Image { Source = image, Width = 26, Height = 26 }
            : new TextBlock
            {
                Text = step.Type switch
                {
                    RigStep.Open => step.Target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "" : "",
                    RigStep.Command => "",
                    RigStep.Keys => "",
                    RigStep.System => control?.Glyph ?? "",
                    RigStep.Sound => "",
                    _ => ""
                },
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = Res<Brush>("Muted"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
        icon.ToolTip = TypeLabel(step);
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Width = 36;
        return icon;
    }

    Border Tool(string glyph, string tip, Action click)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 11,
            Foreground = Res<Brush>("Muted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var tool = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tip, Child = icon };
        tool.MouseEnter += (_, _) => tool.Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
        tool.MouseLeave += (_, _) => tool.Background = Brushes.Transparent;
        tool.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            click();
        };
        return tool;
    }

    Grid Row(int index, RigStepSpec step)
    {
        var ink = Res<Brush>("Ink");
        var kind = StepIcon(index, step);

        var options = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        UIElement middle;
        if (step.Type == RigStep.System)
        {
            var control = RigRunner.Controls.FirstOrDefault(c => c.Id == step.Target);
            middle = new TextBlock { Text = control is null ? step.Target : Loc.T(control.Label), FontSize = 12, Foreground = ink, VerticalAlignment = VerticalAlignment.Center };
        }
        else
        {
            var box = new TextBox { Text = step.Target, Style = Res<Style>("FlatBox"), Foreground = ink, CaretBrush = ink, VerticalAlignment = VerticalAlignment.Center };
            if (step.Type == RigStep.Keys) box.ToolTip = "ctrl+shift+m, win+d, f13, volumemute";
            void Check()
            {
                box.Foreground = Valid(step) ? ink : new SolidColorBrush(Color.FromRgb(0xFF, 0x5C, 0x5C));
                options.Visibility = Placeable(step) ? Visibility.Visible : Visibility.Collapsed;
            }
            box.TextChanged += (_, _) =>
            {
                step.Target = box.Text;
                Check();
            };
            Check();
            middle = box;
        }

        if (step.Type == RigStep.Open)
        {
            var monitor = new Button { Style = Res<Style>("GhostButton"), Content = MonitorText(step) };
            monitor.Click += (_, _) =>
            {
                var count = Forms.Screen.AllScreens.Length;
                step.Monitor = step.Monitor is null ? 0 : step.Monitor + 1 >= count ? null : step.Monitor + 1;
                monitor.Content = MonitorText(step);
            };
            var full = new ToggleButton { Style = Res<Style>("ChipToggle"), Content = Loc.T("rigFullscreen"), IsChecked = step.Fullscreen, Margin = new Thickness(6, 0, 0, 0) };
            full.Click += (_, _) => step.Fullscreen = full.IsChecked == true;
            options.Children.Add(monitor);
            options.Children.Add(full);
        }

        var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        if (index > 0) tools.Children.Add(Tool("", "", () => Move(index, -1)));
        if (index < action.Steps.Count - 1) tools.Children.Add(Tool("", "", () => Move(index, 1)));
        tools.Children.Add(Tool("", Loc.T("todoRemove"), () =>
        {
            action.Steps.RemoveAt(index);
            Fill();
        }));

        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foreach (var (child, column) in new[] { ((UIElement)kind, 0), (middle, 1), (options, 2), (tools, 3) })
        {
            Grid.SetColumn(child, column);
            row.Children.Add(child);
        }
        return row;
    }

    static bool Valid(RigStepSpec step) => step.Type switch
    {
        RigStep.Keys => RigStore.Chord(step.Target) is not null,
        RigStep.Wait => double.TryParse(step.Target, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 0,
        _ => true
    };

    static bool Placeable(RigStepSpec step) =>
        step.Type == RigStep.Open && !step.Target.Contains("://") && !step.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);

    static string MonitorText(RigStepSpec step) => step.Monitor is { } m ? Loc.F("rigMonitor", m + 1) : Loc.T("rigMonitorAny");

    void Move(int index, int by)
    {
        (action.Steps[index], action.Steps[index + by]) = (action.Steps[index + by], action.Steps[index]);
        Fill();
    }
}
