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
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Open();
    }

    public void Open()
    {
        Show();
        Topmost = true;
        Activate();
        Topmost = false;
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
