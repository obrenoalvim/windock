using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Services;
using Forms = System.Windows.Forms;

namespace WinDock.Views;

public sealed class StackWindow : Window
{
    const int Columns = 4, Max = 16;
    const double Cell = 92;

    readonly FolderStack folder;
    readonly bool dark;
    readonly Border face;
    Point? pressed;
    bool closing;

    public StackWindow(FolderStack folder, bool dark)
    {
        this.folder = folder;
        this.dark = dark;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        FontFamily = (FontFamily)Application.Current.FindResource("Mono");

        face = new Border
        {
            Margin = new Thickness(14),
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(dark ? Color.FromArgb(0xF2, 0x1C, 0x1E, 0x26) : Color.FromArgb(0xF7, 0xF8, 0xF8, 0xFA)),
            BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x24, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.45 },
            RenderTransformOrigin = new Point(0.5, 1),
            RenderTransform = new ScaleTransform(1, 1),
            Child = Build()
        };
        Content = face;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = (long)Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, (nint)(ex | Native.WS_EX_TOOLWINDOW));
        };
        Deactivated += (_, _) => SafeClose();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) SafeClose();
        };
    }

    Brush Ink => new SolidColorBrush(dark ? Color.FromRgb(0xF4, 0xEF, 0xE7) : Color.FromRgb(0x1B, 0x1D, 0x24));
    Brush Muted => new SolidColorBrush(dark ? Color.FromArgb(0xB3, 0xF4, 0xEF, 0xE7) : Color.FromArgb(0x99, 0x1B, 0x1D, 0x24));

    UIElement Build()
    {
        var header = new DockPanel { Margin = new Thickness(4, 0, 2, 12), LastChildFill = true };
        var open = new TextBlock
        {
            Text = Loc.T("openInExplorer"),
            FontSize = 11,
            Foreground = Muted,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        open.MouseEnter += (_, _) => open.Foreground = Ink;
        open.MouseLeave += (_, _) => open.Foreground = Muted;
        open.MouseLeftButtonUp += (_, _) =>
        {
            Launcher.OpenFile(folder.Path);
            SafeClose();
        };
        DockPanel.SetDock(open, Dock.Right);
        header.Children.Add(open);
        header.Children.Add(new TextBlock
        {
            Text = folder.Name,
            FontFamily = (FontFamily)Application.Current.FindResource("Display"),
            FontSize = 13,
            Foreground = Ink,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        });

        var grid = new WrapPanel { Width = Columns * Cell };
        var entries = FolderStack.Entries(folder.Path, Max);
        foreach (var entry in entries) grid.Children.Add(Tile(entry));
        if (entries.Count == 0)
            grid.Children.Add(new TextBlock { Text = Loc.T("emptyFolder"), Foreground = Muted, FontSize = 11, Margin = new Thickness(6, 8, 6, 12) });

        return new StackPanel { Children = { header, grid } };
    }

    UIElement Tile(FolderEntry entry)
    {
        var icon = new Image { Source = ShellIcons.Thumbnail(entry.Path, 96) ?? ShellIcons.ForPath(entry.Path, 96), Width = 52, Height = 52 };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var hoverBrush = new SolidColorBrush(dark ? Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x14, 0, 0, 0));
        var cell = new Border
        {
            Width = Cell - 4,
            Margin = new Thickness(2),
            Padding = new Thickness(4, 8, 4, 6),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = entry.Name,
            Child = new StackPanel
            {
                Children =
                {
                    icon,
                    new TextBlock
                    {
                        Text = entry.Name,
                        FontSize = 10,
                        Foreground = Ink,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxHeight = 28,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };
        cell.MouseEnter += (_, _) => cell.Background = hoverBrush;
        cell.MouseLeave += (_, _) =>
        {
            cell.Background = Brushes.Transparent;
            pressed = null;
        };
        cell.MouseLeftButtonDown += (_, e) => pressed = e.GetPosition(this);
        cell.MouseMove += (_, e) =>
        {
            if (pressed is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            var now = e.GetPosition(this);
            if (Math.Abs(now.X - start.X) < 6 && Math.Abs(now.Y - start.Y) < 6) return;
            pressed = null;
            DragDrop.DoDragDrop(cell, new DataObject(DataFormats.FileDrop, new[] { entry.Path }), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        };
        cell.MouseLeftButtonUp += (_, _) =>
        {
            if (pressed is null) return;
            pressed = null;
            Launcher.OpenFile(entry.Path);
            SafeClose();
        };
        cell.MouseRightButtonUp += (_, _) =>
        {
            Launcher.Reveal(entry.Path);
            SafeClose();
        };
        return cell;
    }

    public void ShowAt(double centerX, double barTop, Forms.Screen screen)
    {
        Opacity = 0;
        Show();
        UpdateLayout();
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var w = (int)Math.Round(ActualWidth * scale);
        var h = (int)Math.Round(ActualHeight * scale);
        var b = screen.Bounds;
        var x = (int)Math.Clamp(centerX - w / 2.0, b.Left, Math.Max(b.Left, b.Right - w));
        var y = (int)Math.Max(b.Top, barTop - h + 6 * scale);
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE);
        Opacity = 1;
        Activate();

        var grow = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut } };
        var transform = (ScaleTransform)face.RenderTransform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        face.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
    }

    void SafeClose()
    {
        if (closing) return;
        closing = true;
        Dispatcher.BeginInvoke(Close);
    }
}
