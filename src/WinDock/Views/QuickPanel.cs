using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.Devices.Radios;
using WinDock.Services;

namespace WinDock.Views;

public sealed class QuickPanel : Flyout
{
    const byte KeyA = 0x41, KeyP = 0x50;

    static readonly TimeSpan LetGo = TimeSpan.FromMilliseconds(180);

    readonly nint monitor;
    readonly WrapPanel radios = new() { Width = 300, Margin = new Thickness(0, 10, 0, 0) };
    readonly StackPanel brightness = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
    readonly Slider slider;
    readonly DispatcherTimer apply = new() { Interval = TimeSpan.FromMilliseconds(150) };

    public QuickPanel(bool dark, nint monitor) : base(dark)
    {
        this.monitor = monitor;
        slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Width = 240,
            Style = (Style)Application.Current.FindResource("FlatSlider"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        System.Windows.Automation.AutomationProperties.SetName(slider, Loc.T("quickBrightness"));
        var sun = Glyph("", 16);
        sun.ToolTip = Loc.T("quickBrightness");
        brightness.Children.Add(sun);
        brightness.Children.Add(slider);
        apply.Tick += (_, _) =>
        {
            apply.Stop();
            var level = slider.Value / 100;
            Task.Run(() => Brightness.Set(monitor, level));
        };

        var actions = new WrapPanel
        {
            Width = 300,
            Margin = new Thickness(0, 14, 0, 0),
            Children =
            {
                Spaced(Action(Loc.T("quickNetworks"), () => Open("ms-availablenetworks:"), "")),
                Spaced(Action(Loc.T("quickNightLight"), () => Open("ms-settings:nightlight"), "")),
                Spaced(Action(Loc.T("quickFocus"), () => Open("ms-settings:quiethours"), "")),
                Spaced(Action(Loc.T("quickProject"), () => Press(KeyP), "")),
                Spaced(Action(Loc.T("quickAll"), () => Press(KeyA), ""))
            }
        };
        SetBody(new StackPanel { Width = 300, Children = { Text(Loc.T("quickTitle"), 13, font: Display), radios, brightness, actions } });
        Loaded += async (_, _) =>
        {
            await FillRadios();
            await ShowBrightness();
        };
        Closed += (_, _) => apply.Stop();
    }

    static Border Spaced(Border button)
    {
        button.Margin = new Thickness(0, 0, 6, 6);
        return button;
    }

    void Open(string target)
    {
        Launcher.OpenFile(target);
        SafeClose();
    }

    async void Press(byte key)
    {
        SafeClose();
        await Task.Delay(LetGo);
        Native.Keys(Native.VK_LWIN, key);
    }

    async Task ShowBrightness()
    {
        if (await Task.Run(() => Brightness.Read(monitor)) is not { } level || !IsLoaded) return;
        slider.Value = Math.Round(level * 100);
        slider.ValueChanged += (_, _) =>
        {
            apply.Stop();
            apply.Start();
        };
        brightness.Visibility = Visibility.Visible;
    }

    async Task FillRadios()
    {
        IReadOnlyList<Radio> found;
        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return;
            found = await Radio.GetRadiosAsync();
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException) { return; }
        if (!IsLoaded) return;
        foreach (var radio in found.Where(r => r.Kind is RadioKind.Bluetooth or RadioKind.WiFi)) radios.Children.Add(Toggle(radio));
    }

    static async Task<bool> Switch(Radio radio)
    {
        try { return await radio.SetStateAsync(radio.State == RadioState.On ? RadioState.Off : RadioState.On) == RadioAccessStatus.Allowed; }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException) { return false; }
    }

    Border Toggle(Radio radio)
    {
        var wifi = radio.Kind == RadioKind.WiFi;
        var state = Text("", 9.5, muted: true);
        var name = Text(Loc.T(wifi ? "quickWifi" : "quickBluetooth"), 11);
        var icon = Glyph(wifi ? "" : "", 15);
        icon.Margin = new Thickness(0, 0, 10, 0);
        var chip = new Border
        {
            Width = 144,
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(12, 9, 12, 9),
            CornerRadius = new CornerRadius(12),
            BorderBrush = Track,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, new StackPanel { Children = { name, state } } } }
        };
        void Show()
        {
            var on = radio.State == RadioState.On;
            chip.Background = on ? Paint("#33FFB547") : System.Windows.Media.Brushes.Transparent;
            state.Text = Loc.T(on ? "quickOn" : "quickOff");
            System.Windows.Automation.AutomationProperties.SetName(chip, $"{name.Text} · {state.Text}");
        }
        chip.MouseLeftButtonUp += async (_, e) =>
        {
            e.Handled = true;
            var switched = await Switch(radio);
            if (!IsLoaded) return;
            Show();
            if (!switched) state.Text = Loc.T("quickBlocked");
        };
        Show();
        return chip;
    }
}
