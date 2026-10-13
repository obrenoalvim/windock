using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Services;
using Forms = System.Windows.Forms;

namespace WinDock.Views;

public abstract class Flyout : Window
{
    readonly Border face;
    bool closing;
    (double CenterX, double BarTop, Forms.Screen Screen)? anchor;

    protected Flyout(bool dark)
    {
        Dark = dark;
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
            Padding = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(18),
            Background = Paint(dark ? "#F21C1E26" : "#F7F8F8FA"),
            BorderBrush = Paint(dark ? "#33FFFFFF" : "#24000000"),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.45 },
            RenderTransformOrigin = new Point(0.5, 1),
            RenderTransform = new ScaleTransform(1, 1)
        };
        Content = face;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = (long)Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, (nint)(ex | Native.WS_EX_TOOLWINDOW));
        };
        // Content that grows while open (a new player in the music panel) stays centered on its tile.
        SizeChanged += (_, _) => Place();
        Deactivated += (_, _) => SafeClose();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) SafeClose();
        };
    }

    protected bool Dark { get; }
    protected Brush Ink => Paint(Dark ? "#F4EFE7" : "#1B1D24");
    protected Brush Muted => Paint(Dark ? "#B3F4EFE7" : "#991B1D24");
    protected Brush Hover => Paint(Dark ? "#24FFFFFF" : "#14000000");
    protected Brush Track => Paint(Dark ? "#26FFFFFF" : "#1A000000");
    protected static FontFamily Display => (FontFamily)Application.Current.FindResource("Display");
    protected static FontFamily Glyphs { get; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    protected void SetBody(UIElement body) => face.Child = body;

    public void ShowAt(double centerX, double barTop, Forms.Screen screen)
    {
        Opacity = 0;
        Show();
        UpdateLayout();
        anchor = (centerX, barTop, screen);
        Place();
        Opacity = 1;
        Activate();

        var grow = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut } };
        var transform = (ScaleTransform)face.RenderTransform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        face.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
    }

    void Place()
    {
        if (anchor is not var (centerX, barTop, screen)) return;
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var w = (int)Math.Round(ActualWidth * scale);
        var h = (int)Math.Round(ActualHeight * scale);
        var b = screen.Bounds;
        var x = (int)Math.Clamp(centerX - w / 2.0, b.Left, Math.Max(b.Left, b.Right - w));
        var y = (int)Math.Max(b.Top, barTop - h + 6 * scale);
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE);
    }

    protected void SafeClose()
    {
        if (closing) return;
        closing = true;
        Dispatcher.BeginInvoke(Close);
    }

    protected TextBlock Text(string text, double size = 11, bool muted = false, FontFamily? font = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = muted ? Muted : Ink,
        FontFamily = font ?? FontFamily,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };

    protected TextBlock Glyph(string glyph, double size = 14) => new()
    {
        Text = glyph,
        FontFamily = Glyphs,
        FontSize = size,
        Foreground = Ink,
        VerticalAlignment = VerticalAlignment.Center
    };

    protected Border Action(string label, Action click, string? glyph = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph is not null)
        {
            var icon = Glyph(glyph, 12);
            icon.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(icon);
        }
        row.Children.Add(Text(label, 11));
        var button = new Border
        {
            Padding = new Thickness(10, 7, 12, 7),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            BorderBrush = Track,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = row
        };
        button.MouseEnter += (_, _) => button.Background = Hover;
        button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
        button.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            click();
        };
        return button;
    }

    protected Grid Meter(double value, Brush fill, double height = 6)
    {
        var bar = new Border
        {
            CornerRadius = new CornerRadius(height / 2),
            Background = fill,
            RenderTransformOrigin = new Point(0, 0.5),
            RenderTransform = new ScaleTransform(Math.Clamp(value, 0, 1), 1)
        };
        return new Grid
        {
            Height = height,
            Children = { new Border { CornerRadius = new CornerRadius(height / 2), Background = Track }, bar }
        };
    }

    protected static void SetMeter(Grid meter, double value) =>
        ((ScaleTransform)((Border)meter.Children[1]).RenderTransform).ScaleX = Math.Clamp(value, 0, 1);

    protected static SolidColorBrush Paint(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
