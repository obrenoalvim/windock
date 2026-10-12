using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinDock.Services;
using WinDock.ViewModels;
using Forms = System.Windows.Forms;

namespace WinDock.Views;

public sealed class PreviewWindow : Window
{
    const double BoxWidth = 216, BoxHeight = 128;
    const int DwmCorner = 33, DwmRound = 2;
    const uint ThumbDestination = 0x1, ThumbVisible = 0x8, ThumbOpacity = 0x4, ThumbClientOnly = 0x10;

    readonly StackPanel cards = new() { Orientation = Orientation.Horizontal };
    readonly List<(nint Thumb, Border Box)> thumbs = [];
    nint hwnd;
    bool dark = true;

    public PreviewWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        FontFamily = (FontFamily)Application.Current.FindResource("Mono");
        Content = new Border { Padding = new Thickness(6), Child = cards };

        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            var ex = (long)Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, (nint)(ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE));
            var round = DwmRound;
            Native.DwmSetWindowAttribute(hwnd, DwmCorner, ref round, sizeof(int));
        };
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) Release();
        };
        MouseEnter += (_, _) => PointerEntered?.Invoke();
        MouseLeave += (_, _) => PointerLeft?.Invoke();
    }

    public AppTile? Tile { get; private set; }

    public event Action? PointerEntered;
    public event Action? PointerLeft;
    public event Action<nint>? Picked;

    Brush Ink => dark ? Brushes.WhiteSmoke : new SolidColorBrush(Color.FromRgb(0x1B, 0x1D, 0x24));

    public void ShowFor(AppTile tile, double centerX, double top, Forms.Screen screen, bool darkTheme)
    {
        dark = darkTheme;
        Tile = tile;
        Release();
        Background = new SolidColorBrush(dark ? Color.FromRgb(0x1C, 0x1E, 0x26) : Color.FromRgb(0xF6, 0xF6, 0xF8));
        cards.Children.Clear();
        foreach (var window in tile.Windows.Take(6)) cards.Children.Add(Card(tile, window));
        if (cards.Children.Count == 0)
        {
            Hide();
            return;
        }

        if (!IsVisible) Show();
        UpdateLayout();
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var w = (int)Math.Round(ActualWidth * scale);
        var h = (int)Math.Round(ActualHeight * scale);
        var bounds = screen.Bounds;
        var x =(int)Math.Clamp(centerX - w / 2.0, bounds.Left + 8, Math.Max(bounds.Left + 8, bounds.Right - w - 8));
        var y = (int)Math.Max(bounds.Top + 8, top - h - 10 * scale);
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        Dispatcher.BeginInvoke(Register, DispatcherPriority.Loaded);
    }

    Border Card(AppTile tile, AppWindow window)
    {
        var title = new TextBlock
        {
            Text = Native.TitleOf(window.Handle) is { Length: > 0 } live ? live : window.Title,
            FontSize = 11,
            Foreground = Ink,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 9,
                Foreground = Ink,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        close.MouseEnter += (_, _) => close.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
        close.MouseLeave += (_, _) => close.Background = Brushes.Transparent;

        var header = new DockPanel { Margin = new Thickness(2, 0, 0, 6), Width = BoxWidth };
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        if (tile.Icon is { } icon)
        {
            var small = new Image { Source = icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 7, 0) };
            DockPanel.SetDock(small, Dock.Left);
            header.Children.Add(small);
        }
        header.Children.Add(title);

        var box = new Border
        {
            Width = BoxWidth,
            Height = BoxHeight,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(dark ? Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x0F, 0, 0, 0)),
            Tag = window.Handle,
            Child = tile.Icon is { } big ? new Image { Source = big, Width = 40, Height = 40, Opacity = 0.85 } : null
        };

        var card = new Border
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(9),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = new StackPanel { Children = { header, box } }
        };
        var hoverBrush = new SolidColorBrush(dark ? Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x12, 0, 0, 0));
        card.MouseEnter += (_, _) => card.Background = hoverBrush;
        card.MouseLeave += (_, _) => card.Background = Brushes.Transparent;
        card.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Picked?.Invoke(window.Handle);
        };
        close.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            WindowTracker.Close(window.Handle);
            Unregister(box);
            cards.Children.Remove(card);
            if (cards.Children.Count == 0) Hide();
            else Dispatcher.BeginInvoke(Reposition, DispatcherPriority.Loaded);
        };
        return card;
    }

    void Register()
    {
        if (!IsVisible || hwnd == 0) return;
        foreach (var card in cards.Children.OfType<Border>())
        {
            if (card.Child is not StackPanel { Children.Count: 2 } stackPanel || stackPanel.Children[1] is not Border box || box.Tag is not nint source) continue;
            if (Native.DwmRegisterThumbnail(hwnd, source, out var thumb) != 0) continue;
            thumbs.Add((thumb, box));
        }
        Reposition();
    }

    void Reposition()
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        foreach (var (thumb, box) in thumbs)
        {
            if (!box.IsLoaded) continue;
            var origin = box.TransformToAncestor(this).Transform(new Point(0, 0));
            var left = origin.X * scale;
            var topPx = origin.Y * scale;
            var width = box.ActualWidth * scale;
            var height = box.ActualHeight * scale;
            Native.DwmQueryThumbnailSourceSize(thumb, out var source);
            if (source.Width > 0 && source.Height > 0)
            {
                var fit = Math.Min(width / source.Width, height / source.Height);
                var fw = source.Width * fit;
                var fh = source.Height * fit;
                left += (width - fw) / 2;
                topPx += (height - fh) / 2;
                width = fw;
                height = fh;
            }
            var props = new Native.ThumbnailProperties
            {
                Flags = ThumbDestination | ThumbVisible | ThumbOpacity | ThumbClientOnly,
                Destination = new Native.Rect { Left = (int)left, Top = (int)topPx, Right = (int)(left + width), Bottom = (int)(topPx + height) },
                Opacity = 255,
                Visible = 1,
                SourceClientAreaOnly = 0
            };
            Native.DwmUpdateThumbnailProperties(thumb, ref props);
        }
    }

    void Unregister(Border box)
    {
        var index = thumbs.FindIndex(t => t.Box == box);
        if (index < 0) return;
        Native.DwmUnregisterThumbnail(thumbs[index].Thumb);
        thumbs.RemoveAt(index);
    }

    void Release()
    {
        foreach (var (thumb, _) in thumbs) Native.DwmUnregisterThumbnail(thumb);
        thumbs.Clear();
    }
}
