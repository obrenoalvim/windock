using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinDock.Services;

namespace WinDock.Views;

public sealed class TrayPanel : Flyout
{
    readonly Action showWindowsTray;
    readonly StackPanel body = new() { Width = 300 };

    public TrayPanel(bool dark, Action showWindowsTray) : base(dark)
    {
        this.showWindowsTray = showWindowsTray;
        SetBody(body);
        Render();
    }

    void Render()
    {
        body.Children.Clear();
        body.Children.Add(Text(Loc.T("trayTitle"), 13, font: Display));
        var apps = TrayApps.Running();
        var list = new StackPanel { Margin = new Thickness(-6, 12, -6, 10) };
        foreach (var app in apps) list.Children.Add(Row(app));
        if (apps.Count == 0)
        {
            var empty = Text(Loc.T("trayEmpty"), 11, muted: true);
            empty.TextWrapping = TextWrapping.Wrap;
            empty.Margin = new Thickness(6, 0, 6, 0);
            list.Children.Add(empty);
        }
        body.Children.Add(new ScrollViewer { Content = list, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        body.Children.Add(Action(Loc.T("trayShowWindows"), () =>
        {
            SafeClose();
            showWindowsTray();
        }, ""));
    }

    Border Row(TrayApp app)
    {
        var icon = new Image { Width = 22, Height = 22, Source = ShellIcons.ForPath(app.Exe, 64), Margin = new Thickness(0, 0, 10, 0) };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var quit = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = Loc.T("trayEnd"),
            Child = Centered(Glyph("", 11))
        };
        quit.MouseEnter += (_, _) => quit.Background = Paint("#40E5483B");
        quit.MouseLeave += (_, _) => quit.Background = Brushes.Transparent;
        quit.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (!TrayApps.End(app)) quit.ToolTip = Loc.T("trayEndFailed");
            var later = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            later.Tick += (_, _) =>
            {
                later.Stop();
                Render();
            };
            later.Start();
        };

        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(quit, Dock.Right);
        line.Children.Add(icon);
        line.Children.Add(quit);
        line.Children.Add(Text(app.Name, 11.5));

        var row = new Border
        {
            Padding = new Thickness(6, 4, 4, 4),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = app.Exe,
            Child = line
        };
        row.MouseEnter += (_, _) => row.Background = Hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            SafeClose();
            TrayApps.Open(app);
        };
        return row;
    }

    static TextBlock Centered(TextBlock glyph)
    {
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        return glyph;
    }
}
