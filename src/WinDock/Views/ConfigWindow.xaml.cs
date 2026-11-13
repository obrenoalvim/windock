using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public partial class ConfigWindow : Window
{
    readonly MainViewModel vm;
    readonly System.Windows.Threading.DispatcherTimer usageTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly SelfUsage usage = new();

    public ConfigWindow(MainViewModel vm)
    {
        this.vm = vm;
        InitializeComponent();
        DataContext = vm;
        MaxHeight = SystemParameters.WorkArea.Height;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
        usageTimer.Tick += (_, _) => ShowUsage();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                usageTimer.Stop();
                return;
            }
            ShowUsage();
            usageTimer.Start();
        };
        Loc.Instance.PropertyChanged += (_, _) => ShowLanguage();
        ShowLanguage();
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Open();
    }

    public void Open()
    {
        FillScreens();
        Show();
        Topmost = true;
        Activate();
        Topmost = false;
    }

    void OnEnvironmentsTab(object sender, RoutedEventArgs e) => ShowTab(dock: false);

    void OnDockTab(object sender, RoutedEventArgs e) => ShowTab(dock: true);

    void ShowTab(bool dock)
    {
        EnvTab.IsChecked = !dock;
        DockTab.IsChecked = dock;
        EnvironmentsPage.Visibility = dock ? Visibility.Collapsed : Visibility.Visible;
        DockPage.Visibility = dock ? Visibility.Visible : Visibility.Collapsed;
    }

    public sealed record ThemeChoice(string Id, string Name, System.Windows.Media.Brush Swatch, System.Windows.Media.Brush Rim);

    bool fillingThemes;

    void FillThemes()
    {
        fillingThemes = true;
        ThemeBox.ItemsSource = WinDock.Models.DockTheme.All.Select(t => new ThemeChoice(
            t.Id,
            Loc.T("theme" + char.ToUpperInvariant(t.Id[0]) + t.Id[1..]),
            new System.Windows.Media.LinearGradientBrush(Color(t.BarTop), Color(t.BarBottom), 90),
            new System.Windows.Media.SolidColorBrush(Color(t.Stroke ?? "#59FFFFFF")))).ToList();
        ThemeBox.SelectedValue = vm.Dock.Theme;
        fillingThemes = false;
        FillIconStyles();
        FillWidgetLooks();
    }

    void OnThemePicked(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (fillingThemes || ThemeBox.SelectedValue is not string id || id == vm.Dock.Theme) return;
        vm.Dock.Theme = id;
        FillIconStyles();
        FillWidgetLooks();
    }

    public sealed record IconStyleChoice(string Id, string Name, IReadOnlyList<System.Windows.Media.ImageSource> Previews);

    bool fillingStyles;

    void FillIconStyles()
    {
        fillingStyles = true;
        var theme = WinDock.Models.DockTheme.Find(vm.Dock.Theme);
        var accent = vm.Active?.Model.Accent ?? "#FFB547";
        var samples = vm.Always.Model.Items.Concat(vm.Active?.Model.Items ?? [])
            .Select(p => ShellIcons.ForPin(p, 128)).OfType<System.Windows.Media.ImageSource>().Distinct().Take(4).ToList();
        if (samples.Count == 0 && ShellIcons.ForPath(Environment.ProcessPath ?? "", 128) is { } own) samples.Add(own);
        IconStyleBox.ItemsSource = IconStyles.All.Select(s => new IconStyleChoice(
            s,
            Loc.T("iconStyle_" + s),
            samples.Select(i => IconStyles.Apply(i, s, theme, accent)).OfType<System.Windows.Media.ImageSource>().ToList())).ToList();
        IconStyleBox.SelectedValue = vm.Dock.IconStyle;
        fillingStyles = false;
    }

    void OnIconStylePicked(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!fillingStyles && IconStyleBox.SelectedValue is string id && id != vm.Dock.IconStyle) vm.Dock.IconStyle = id;
    }

    public sealed record LookSample(System.Windows.Media.Brush Surface, System.Windows.Media.Brush Ink, System.Windows.Media.Brush Border);

    public sealed record WidgetLookChoice(string Id, string Name, IReadOnlyList<LookSample> Samples);

    bool fillingLooks;

    static LookSample Sample(WidgetPalette p) => new(
        new System.Windows.Media.LinearGradientBrush(p.Top, p.Bottom, 45),
        new System.Windows.Media.SolidColorBrush(p.Ink),
        new System.Windows.Media.SolidColorBrush(p.Border));

    void FillWidgetLooks()
    {
        fillingLooks = true;
        var theme = WinDock.Models.DockTheme.Find(vm.Dock.Theme);
        var accent = Color(vm.Active?.Model.Accent ?? "#FFB547");
        WidgetLookBox.ItemsSource = WidgetLooks.All.Select(l => new WidgetLookChoice(l, Loc.T("widgetLook_" + l), WidgetLooks.Samples(l, theme, accent).Select(Sample).ToList())).ToList();
        WidgetLookBox.SelectedValue = vm.Dock.WidgetLook;
        fillingLooks = false;
    }

    void OnWidgetLookPicked(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!fillingLooks && WidgetLookBox.SelectedValue is string id && id != vm.Dock.WidgetLook) vm.Dock.WidgetLook = id;
    }

    static System.Windows.Media.Color Color(string hex) => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

    void FillScreens()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        ScreenRow.Visibility = screens.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
        Screens.Children.Clear();
        if (screens.Length < 2) return;
        AddScreenChip(Loc.T("allScreens"), vm.Dock.AllScreens, () => vm.Dock.AllScreens = true);
        var current = screens.FirstOrDefault(s => s.DeviceName == vm.Dock.Screen) ?? System.Windows.Forms.Screen.PrimaryScreen;
        for (var i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            AddScreenChip($"{i + 1}  {screen.Bounds.Width}×{screen.Bounds.Height}", !vm.Dock.AllScreens && screen.DeviceName == current?.DeviceName, () =>
            {
                vm.Dock.Screen = screen.DeviceName;
                vm.Dock.AllScreens = false;
            });
        }
    }

    void AddScreenChip(string label, bool on, Action pick)
    {
        var chip = new System.Windows.Controls.Primitives.ToggleButton
        {
            Content = label,
            Style = (Style)FindResource("ChipToggle"),
            IsChecked = on
        };
        chip.Click += (_, _) =>
        {
            pick();
            FillScreens();
        };
        Screens.Children.Add(chip);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    void OnHeaderDrag(object sender, MouseButtonEventArgs e) => DragMove();

    void OnAddProgram(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not PresetViewModel preset) return;
        var picker = new ProgramPicker { Owner = this };
        if (picker.ShowDialog() == true) vm.AddPrograms(preset, picker.Chosen);
    }

    void OnDelete(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not PresetViewModel preset) return;
        var answer = MessageBox.Show(this, Loc.F("deleteAsk", preset.Model.Name), "WinDock",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) vm.DeleteCommand.Execute(preset);
    }

    void ShowLanguage()
    {
        FillThemes();
        EnChip.IsChecked = Loc.Instance.Language == Loc.English;
        PtChip.IsChecked = Loc.Instance.Language == Loc.Portuguese;
    }

    void ShowUsage()
    {
        var (mb, cpu) = usage.Sample(DateTime.Now);
        var culture = Loc.Instance.Culture;
        Usage.Text = Loc.F("usageNow", mb.ToString("0", culture), cpu.ToString("0.0", culture));
    }

    void OnEnglish(object sender, RoutedEventArgs e) => vm.SetLanguage(Loc.English);

    void OnPortuguese(object sender, RoutedEventArgs e) => vm.SetLanguage(Loc.Portuguese);

    void OnClose(object sender, RoutedEventArgs e) => Hide();

    void OnUpdateClick(object sender, RoutedEventArgs e) => Launcher.OpenFile(Updates.Page);
}
