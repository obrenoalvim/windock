using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace WinDock.Services;

public sealed class TaskbarCover
{
    const int ExTransparent = 0x20, ExToolWindow = 0x80, ExNoActivate = 0x8000000;
    const uint ExcludeFromCapture = 0x11;
    const int TrayWidth = 380;
    static readonly nint Topmost = new(-1);
    static TaskbarCover? current;

    sealed record Slot(Rectangle Area, Window Window, Grid Layers, Bitmap Shot);

    readonly List<Slot> slots = [];
    readonly DispatcherTimer keepOnTop = new() { Interval = TimeSpan.FromMilliseconds(40) };
    readonly string? outgoing;
    string? incoming;
    int baseline;
    bool canProbe = true;
    volatile bool closed;
    volatile int generation;

    TaskbarCover(string? outgoing, string? incoming)
    {
        this.outgoing = outgoing;
        this.incoming = incoming;
    }

    public int Generation => generation;

    [DllImport("user32.dll")] static extern int GetWindowLong(nint hWnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(nint hWnd, int index, int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(nint hWnd, uint affinity);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint handle);

    public static TaskbarCover? Begin(string? outgoingSignature, string? incomingSignature)
    {
        try
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                if (current is { closed: false } running)
                {
                    running.Retarget(incomingSignature);
                    return running;
                }

                var cover = new TaskbarCover(outgoingSignature, incomingSignature);
                if (!cover.Show()) return null;
                current = cover;
                return cover;
            });
        }
        catch (Exception) { return null; }
    }

    public void Release(int ticket)
    {
        if (!WaitForTaskbar(ticket, TimeSpan.FromSeconds(12))) return;
        try { Application.Current.Dispatcher.Invoke(() => Fade(ticket)); }
        catch (Exception e) when (e is InvalidOperationException or TaskCanceledException) { return; }
        Thread.Sleep(380);
        if (generation == ticket) RememberIncoming();
    }

    bool Show()
    {
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var strip = Strip(screen);
            if (!strip.IsEmpty) slots.Add(Create(strip));
        }
        if (slots.Count == 0) return false;
        keepOnTop.Tick += (_, _) => Raise();
        keepOnTop.Start();
        return true;
    }

    void Retarget(string? signature)
    {
        incoming = signature;
        generation++;
        foreach (var slot in slots)
        {
            slot.Window.BeginAnimation(UIElement.OpacityProperty, null);
            slot.Window.Opacity = 1;
            if (Preview(slot) is { } layer) slot.Layers.Children.Add(layer);
        }
    }

    static Rectangle Strip(Forms.Screen screen)
    {
        var b = screen.Bounds;
        var w = screen.WorkingArea;
        if (w.Bottom < b.Bottom) return new Rectangle(b.Left, w.Bottom, b.Width, b.Bottom - w.Bottom);
        if (w.Top > b.Top) return new Rectangle(b.Left, b.Top, b.Width, w.Top - b.Top);
        if (w.Left > b.Left) return new Rectangle(b.Left, b.Top, w.Left - b.Left, b.Height);
        if (w.Right < b.Right) return new Rectangle(w.Right, b.Top, b.Right - w.Right, b.Height);
        return Rectangle.Empty;
    }

    static Bitmap Capture(Rectangle area)
    {
        var shot = new Bitmap(area.Width, area.Height);
        using var g = Graphics.FromImage(shot);
        g.CopyFromScreen(area.Location, System.Drawing.Point.Empty, area.Size);
        return shot;
    }

    Slot Create(Rectangle area)
    {
        var shot = Capture(area);
        baseline += Bright(shot);
        if (outgoing is not null) TaskbarImages.Save(TaskbarImages.Key(outgoing, area), shot);

        var layers = new Grid();
        layers.Children.Add(new System.Windows.Controls.Image { Source = ToSource(shot), Stretch = System.Windows.Media.Stretch.Fill });

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Content = layers
        };
        window.Show();

        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | ExTransparent | ExToolWindow | ExNoActivate);
        SetWindowPos(hwnd, Topmost, area.X, area.Y, area.Width, area.Height, 0x10 | 0x40);
        if (Environment.GetEnvironmentVariable("WINDOCK_DEBUG_COVER") == "1" || !SetWindowDisplayAffinity(hwnd, ExcludeFromCapture)) canProbe = false;

        var slot = new Slot(area, window, layers, shot);
        if (Preview(slot) is { } layer) layers.Children.Add(layer);
        return slot;
    }

    System.Windows.Controls.Image? Preview(Slot slot)
    {
        if (incoming is null || TaskbarImages.Load(TaskbarImages.Key(incoming, slot.Area)) is not { } cached) return null;

        using (cached)
        using (var blend = (Bitmap)slot.Shot.Clone())
        {
            var keep = Math.Min(TrayWidth, blend.Width / 4);
            var left = new Rectangle(0, 0, blend.Width - keep, blend.Height);
            using (var g = Graphics.FromImage(blend)) g.DrawImage(cached, left, left, GraphicsUnit.Pixel);

            var layer = new System.Windows.Controls.Image { Source = ToSource(blend), Stretch = System.Windows.Media.Stretch.Fill, Opacity = 0 };
            layer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
            {
                BeginTime = TimeSpan.FromMilliseconds(40),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            });
            return layer;
        }
    }

    static BitmapSource ToSource(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally { DeleteObject(handle); }
    }

    static int Bright(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var count = 0;
            var row = new byte[data.Stride];
            for (var y = 0; y < data.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);
                for (var x = 0; x < data.Width * 4; x += 4)
                    if (Math.Max(row[x], Math.Max(row[x + 1], row[x + 2])) > 110) count++;
            }
            return count;
        }
        finally { bitmap.UnlockBits(data); }
    }

    void Raise()
    {
        foreach (var slot in slots)
            SetWindowPos(new WindowInteropHelper(slot.Window).Handle, Topmost, 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
    }

    bool WaitForTaskbar(int ticket, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        if (!canProbe || baseline == 0)
        {
            for (var i = 0; i < 70 && generation == ticket; i++) Thread.Sleep(50);
            return generation == ticket;
        }

        var stable = 0;
        var last = -1;
        while (DateTime.UtcNow < until)
        {
            if (generation != ticket) return false;

            var now = 0;
            foreach (var slot in slots)
            {
                using var shot = Capture(slot.Area);
                now += Bright(shot);
            }

            var populated = now > baseline * 0.25;
            stable = populated && last >= 0 && Math.Abs(now - last) <= Math.Max(8, last * 0.03) ? stable + 1 : 0;
            last = now;
            if (stable >= 3) return generation == ticket;
            Thread.Sleep(40);
        }
        return generation == ticket;
    }

    void RememberIncoming()
    {
        if (incoming is null) return;
        foreach (var slot in slots)
        {
            using var shot = Capture(slot.Area);
            TaskbarImages.Save(TaskbarImages.Key(incoming, slot.Area), shot);
        }
    }

    void Fade(int ticket)
    {
        if (generation != ticket || closed) return;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
        fade.Completed += (_, _) => Close(ticket);
        foreach (var slot in slots) slot.Window.BeginAnimation(UIElement.OpacityProperty, fade);
        if (slots.Count == 0) Close(ticket);
    }

    void Close(int ticket)
    {
        if (generation != ticket || closed) return;
        closed = true;
        current = null;
        keepOnTop.Stop();
        foreach (var slot in slots)
        {
            slot.Window.Close();
            slot.Shot.Dispose();
        }
        slots.Clear();
    }
}
