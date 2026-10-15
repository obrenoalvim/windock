using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinDock.Services;

namespace WinDock.Views;

public sealed class SoundPanel : Flyout
{
    readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(300) };
    readonly TextBlock device;
    readonly TextBlock level;
    readonly Slider slider;
    readonly TextBlock muteGlyph;
    readonly StackPanel outputs = new();
    bool syncing;

    public SoundPanel(bool dark) : base(dark)
    {
        device = Text("", 12);
        level = Text("", 11, muted: true);
        level.Width = 40;
        level.TextAlignment = TextAlignment.Right;
        muteGlyph = Glyph("\uE767", 16);
        slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Width = 230,
            Style = (Style)Application.Current.FindResource("FlatSlider"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0)
        };
        slider.ValueChanged += (_, e) =>
        {
            if (!syncing) Audio.SetVolume(e.NewValue / 100);
        };
        slider.PreviewMouseWheel += (_, e) =>
        {
            e.Handled = true;
            Audio.Nudge(e.Delta > 0 ? 1 : -1);
            Sync();
        };

        var mute = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(10),
            Background = Hover,
            Cursor = Cursors.Hand,
            Child = muteGlyph,
            ToolTip = Loc.T("mute")
        };
        muteGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        mute.MouseLeftButtonUp += (_, _) =>
        {
            if (Audio.Read() is { } now) Audio.SetMute(!now.Muted);
            Sync();
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 14), Children = { mute, slider, level } };
        var title = Text(Loc.T("sound"), 13, font: Display);
        var actions = new WrapPanel
        {
            Width = 330,
            Children =
            {
                Spaced(Action(Loc.T("volumeMixer"), () => Run(() => Launcher.OpenFile("ms-settings:apps-volume")), "\uE9E9")),
                Spaced(Action(Loc.T("soundSettings"), () => Run(() => Launcher.OpenFile("ms-settings:sound")), "\uE713"))
            }
        };
        device.Margin = new Thickness(0, 6, 0, 0);
        outputs.Margin = new Thickness(-6, 0, -6, 10);
        SetBody(new StackPanel { Width = 330, Children = { title, device, row, outputs, actions } });
        FillOutputs();

        poll.Tick += (_, _) => Sync();
        Loaded += (_, _) =>
        {
            Sync();
            poll.Start();
        };
        Closed += (_, _) => poll.Stop();
    }

    void FillOutputs()
    {
        outputs.Children.Clear();
        var all = Audio.Outputs();
        if (all.Count < 2) return;
        outputs.Children.Add(Spacer(Text(Loc.T("soundOutput"), 10, muted: true)));
        foreach (var output in all)
        {
            var check = Glyph(output.IsDefault ? "\uE73E" : "", 11);
            check.Width = 18;
            var icon = Glyph("\uE7F5", 13);
            icon.Margin = new Thickness(0, 0, 10, 0);
            var name = Text(output.Name, 11.5);
            var line = new DockPanel();
            DockPanel.SetDock(check, Dock.Right);
            DockPanel.SetDock(icon, Dock.Left);
            line.Children.Add(check);
            line.Children.Add(icon);
            line.Children.Add(name);
            var rowBorder = new Border
            {
                Padding = new Thickness(6, 6, 6, 6),
                CornerRadius = new CornerRadius(9),
                Background = output.IsDefault ? Track : System.Windows.Media.Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = line
            };
            rowBorder.MouseEnter += (_, _) => rowBorder.Background = Hover;
            rowBorder.MouseLeave += (_, _) => rowBorder.Background = output.IsDefault ? Track : System.Windows.Media.Brushes.Transparent;
            rowBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (!output.IsDefault) Audio.SetOutput(output.Id);
                FillOutputs();
                Sync();
            };
            outputs.Children.Add(rowBorder);
        }
    }

    static TextBlock Spacer(TextBlock text)
    {
        text.Margin = new Thickness(6, 0, 0, 4);
        return text;
    }

    static Border Spaced(Border button)
    {
        button.Margin = new Thickness(0, 0, 6, 6);
        return button;
    }

    void Run(Action open)
    {
        open();
        SafeClose();
    }

    void Sync()
    {
        var state = Audio.Read();
        syncing = true;
        try
        {
            device.Text = state?.Device ?? Loc.T("noAudio");
            if (state is null) return;
            if (!slider.IsMouseCaptureWithin) slider.Value = Math.Round(state.Volume * 100);
            level.Text = state.Muted ? Loc.T("muted") : $"{Math.Round(state.Volume * 100)}%";
            muteGlyph.Text = Audio.Glyph(state);
        }
        finally { syncing = false; }
    }
}
