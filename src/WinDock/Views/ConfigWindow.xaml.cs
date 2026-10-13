using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public partial class ConfigWindow : Window
{
    const int ToggleId = 1;
    const int PresetBaseId = 10;

    readonly MainViewModel vm;
    readonly HwndSource source;

    public ConfigWindow(MainViewModel vm)
    {
        this.vm = vm;
        InitializeComponent();
        DataContext = vm;
        MaxHeight = SystemParameters.WorkArea.Height;

        var handle = new WindowInteropHelper(this).EnsureHandle();
        source = HwndSource.FromHwnd(handle);
        source.AddHook(WndProc);
        const uint mods = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT;
        Native.RegisterHotKey(handle, ToggleId, mods, 0x57);
        for (var i = 0; i < 9; i++) Native.RegisterHotKey(handle, PresetBaseId + i, mods, (uint)(0x31 + i));

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
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
    }

    void OnThemePicked(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!fillingThemes && ThemeBox.SelectedValue is string id && id != vm.Dock.Theme) vm.Dock.Theme = id;
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

    public void Release()
    {
        Native.UnregisterHotKey(source.Handle, ToggleId);
        for (var i = 0; i < 9; i++) Native.UnregisterHotKey(source.Handle, PresetBaseId + i);
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

    void OnEnglish(object sender, RoutedEventArgs e) => vm.SetLanguage(Loc.English);

    void OnPortuguese(object sender, RoutedEventArgs e) => vm.SetLanguage(Loc.Portuguese);

    void OnClose(object sender, RoutedEventArgs e) => Hide();

    nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != Native.WM_HOTKEY) return 0;
        handled = true;
        var id = (int)wParam;
        if (id == ToggleId) Toggle();
        else _ = vm.ApplyIndexAsync(id - PresetBaseId);
        return 0;
    }
}
