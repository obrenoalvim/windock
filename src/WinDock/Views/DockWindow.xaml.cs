using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Views;

public partial class DockWindow : Window
{
    const int ToggleId = 1;
    const int PresetBaseId = 10;

    readonly MainViewModel vm;
    HwndSource? source;

    public DockWindow(MainViewModel vm)
    {
        this.vm = vm;
        InitializeComponent();
        DataContext = vm;

        SizeChanged += (_, _) => Place();
        Deactivated += (_, _) => { if (vm.Settings.AutoHide) Hide(); };
        Drop += OnDrop;
        vm.ItemsRebuilt += () => Dispatcher.BeginInvoke(RevealTiles, System.Windows.Threading.DispatcherPriority.Loaded);
        vm.PropertyChanged += OnVmChanged;
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Reveal();
    }

    public void Reveal()
    {
        Show();
        Activate();
        var slide = new DoubleAnimation(26, 0, TimeSpan.FromMilliseconds(380)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        ShellShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
        Shell.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
        RevealTiles();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        source = (HwndSource)PresentationSource.FromVisual(this);
        var handle = source.Handle;
        Native.SetWindowLong(handle, Native.GWL_EXSTYLE, Native.GetWindowLong(handle, Native.GWL_EXSTYLE) | Native.WS_EX_TOOLWINDOW);
        source.AddHook(WndProc);

        const uint mods = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT;
        Native.RegisterHotKey(handle, ToggleId, mods, 0x20);
        for (var i = 0; i < 9; i++) Native.RegisterHotKey(handle, PresetBaseId + i, mods, (uint)(0x31 + i));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (source is not null)
        {
            Native.UnregisterHotKey(source.Handle, ToggleId);
            for (var i = 0; i < 9; i++) Native.UnregisterHotKey(source.Handle, PresetBaseId + i);
        }
        base.OnClosing(e);
    }

    nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != Native.WM_HOTKEY) return 0;
        var id = (int)wParam;
        handled = true;
        if (id == ToggleId) Toggle();
        else
        {
            vm.SelectIndex(id - PresetBaseId);
            if (!IsVisible) Reveal();
        }
        return 0;
    }

    void Place()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Bottom - ActualHeight + 14;
    }

    void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) vm.AddFiles(files);
    }

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Active) || vm.Active is null) return;
        var accent = (System.Windows.Media.Color)ColorConverter.ConvertFromString(vm.Active.Model.Accent);
        Tint(vm.Accent, accent);
        Tint(vm.AccentSoft, System.Windows.Media.Color.FromArgb(0x2A, accent.R, accent.G, accent.B));
        Tint(vm.AccentLine, System.Windows.Media.Color.FromArgb(0x66, accent.R, accent.G, accent.B));
    }

    static void Tint(SolidColorBrush brush, System.Windows.Media.Color color) =>
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(450)));

    void RevealTiles()
    {
        for (var i = 0; i < Tiles.Items.Count; i++)
        {
            if (Tiles.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement tile) continue;
            var shift = new TranslateTransform(0, 18);
            tile.RenderTransform = shift;
            tile.Opacity = 0;
            var delay = TimeSpan.FromMilliseconds(40 * i);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            shift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = delay, EasingFunction = ease });
            tile.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = delay });
        }
    }
}
