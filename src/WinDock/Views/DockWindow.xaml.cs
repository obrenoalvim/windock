using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;
using Forms = System.Windows.Forms;

namespace WinDock.Views;

public partial class DockWindow : Window
{
    const int BottomGap = 6;
    const int AbmNew = 0, AbmRemove = 1, AbmQueryPos = 2, AbmSetPos = 3, AbeBottom = 3, AbnPosChanged = 1;
    const int WmDisplayChange = 0x7E;

    static readonly Color Amber = Color.FromRgb(0xFF, 0xB5, 0x47);

    readonly DockViewModel vm;
    readonly Action openSettings;
    readonly string screenName;
    readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly DispatcherTimer hover = new() { Interval = TimeSpan.FromMilliseconds(450) };
    readonly DispatcherTimer closePreview = new() { Interval = TimeSpan.FromMilliseconds(350) };
    readonly SolidColorBrush accent = new(Amber);
    readonly GradientStop glowStop = new(Color.FromArgb(0x40, 0xFF, 0xB5, 0x47), 0);
    readonly RadialGradientBrush glow;

    LensPanel? panel;
    nint hwnd;
    int taskbarCreated, appBarMessage;
    bool appBar;
    Native.Rect? reserved;
    bool fullscreen, concealed, menuOpen;
    int away, peekAway, revealOrder;
    double fit = 1;
    DateTime revealBurst, lastWheel;
    FrameworkElement? hovered;
    PreviewWindow? preview;
    Flyout? flyout;
    (object Item, Type Kind)? flyoutKey, closedKey;
    DateTime closedAt, menuClosedAt;
    FrameworkElement? pressedElement;
    DockTile? pressed; // the pinned app or widget being dragged
    Point pressAt;
    bool dragging, dragOut;
    Action? undo;
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    public DockWindow(DockViewModel vm, Action openSettings, string screenName)
    {
        this.vm = vm;
        this.openSettings = openSettings;
        this.screenName = screenName;
        InitializeComponent();
        ((FaceSelector)Resources["Faces"]).On = vm.Options.CompactWidgets;
        DataContext = vm;

        glow = new RadialGradientBrush
        {
            Center = new Point(0.5, 1.25),
            GradientOrigin = new Point(0.5, 1.25),
            RadiusX = 0.55,
            RadiusY = 1.3,
            GradientStops = [glowStop, new GradientStop(Colors.Transparent, 1)]
        };

        vm.Layout += OnLayout;
        vm.Switched += OnSwitched;
        vm.Options.Changed += OnOption;
        vm.Tracker.Changed += CheckFullscreen;
        vm.Tracker.Reshaped += CheckFullscreen;
        vm.SetOffScreen(this, false);
        SystemParameters.StaticPropertyChanged += OnSystemParameter;
        poll.Tick += (_, _) => Poll();
        hover.Tick += (_, _) =>
        {
            hover.Stop();
            if (hovered is { DataContext: AppTile { IsRunning: true } app } element) ShowPreview(app, element);
        };
        closePreview.Tick += (_, _) =>
        {
            closePreview.Stop();
            preview?.Hide();
        };
        DragOver += OnDragOver;
        DragLeave += (_, _) => HideName();
        Drop += OnDrop;
        PreviewMouseUp += (_, e) =>
        {
            if (DateTime.Now - menuClosedAt > TimeSpan.FromMilliseconds(400) || dragging) return;
            e.Handled = true;
            if (pressedElement is { } element) element.Opacity = 1;
            pressed = null;
        };
        toastTimer.Tick += (_, _) => HideToast();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(Resize, DispatcherPriority.Loaded);
        ApplyAccent(false);
        poll.Start();
    }

    void OnLayout() => Dispatcher.BeginInvoke(Resize, DispatcherPriority.Loaded);

    void OnSystemParameter(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea)) Dispatcher.BeginInvoke(Place, DispatcherPriority.Background);
    }

    double IconSize => Math.Round(vm.Options.IconSize * fit);
    double Gap => Math.Round(IconSize * 0.17);
    Thickness Inset => new(Math.Round(IconSize * 0.26), Math.Round(IconSize * 0.2), Math.Round(IconSize * 0.26), Math.Round(IconSize * 0.27));
    double BarHeight => IconSize + Inset.Top + Inset.Bottom;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwnd = new WindowInteropHelper(this).Handle;
        var ex = (long)Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, (nint)(ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE));
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        appBarMessage = Native.RegisterWindowMessage("WinDock.AppBar");
    }

    void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        panel = (LensPanel)sender;
        panel.Arranged += (_, _) => PlaceName();
        ApplyOptions();
    }

    void ApplySizes()
    {
        if (panel is null) return;
        Resources["IconSize"] = IconSize;
        Resources["WideSize"] = IconSize * 2 + Gap;
        Resources["WiderSize"] = IconSize * 3 + Gap * 2;
        panel.Gap = Gap;
        panel.Inset = Inset;
        panel.Corner = Math.Round(BarHeight * Theme.Corner);
        panel.Radius = vm.Options.LensWidth * (IconSize + Gap);
    }

    public void ApplyOptions()
    {
        if (panel is null) return;
        var o = vm.Options;
        ApplySizes();
        panel.Magnification = o.Magnification;
        panel.Mode = o.Lens switch
        {
            DockSettings.LensNone => LensMode.None,
            DockSettings.LensScale => LensMode.Scale,
            _ => LensMode.Magnify
        };
        ApplyTheme();
        Reserve(o.Reserve && !o.AutoHide);
        ApplyShell();
        if (!o.AutoHide && concealed) Reveal();
        if (o.AutoHide) poll.Start();
        Resize();
    }

    void ApplyShell()
    {
        if (vm.Options.ReplaceTaskbar && vm.Options.Enabled) vm.Shell.HideTaskbar();
        else vm.Shell.ShowTaskbar();
    }

    void ApplyTheme()
    {
        if (panel is null) return;
        var theme = Theme;
        var dark = theme.Dark;
        panel.BarBrush = Gradient(theme.BarTop, theme.BarBottom);
        panel.BarStroke = theme.Stroke is { } stroke ? Brush(stroke) : null;
        panel.StrokeWidth = theme.StrokeWidth;
        panel.ShadowBrush = theme.Shadow is { } shadow ? Brush(shadow) : null;
        panel.IndicatorBrush = Brush(theme.Indicator ?? (dark ? "#E6FFFFFF" : "#B3000000"));
        panel.FocusBrush = accent;
        panel.GlowBrush = glow;
        Resources["DockLine"] = Brush(theme.Line);
        Resources["DotIdle"] = Brush(theme.DotIdle);
        Resources["TileFace"] = Brush(theme.TileFace);
        Resources["DockInk"] = Brush(theme.Ink);
        Resources["DockMuted"] = Brush(theme.Muted);
        NameTag.Background = Brush(dark ? "#EB1E2029" : "#F5FFFFFF");
        NameTag.BorderBrush = Brush(dark ? "#26FFFFFF" : "#1F000000");
        NameText.Foreground = Brush(dark ? "#F4EFE7" : "#1B1D24");
    }

    void ApplyAccent(bool animate)
    {
        var color = vm.Active?.Accent.Color ?? Amber;
        var soft = Color.FromArgb(0x48, color.R, color.G, color.B);
        if (!animate)
        {
            accent.Color = color;
            glowStop.Color = soft;
            return;
        }
        var time = TimeSpan.FromMilliseconds(450);
        accent.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, time));
        glowStop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(soft, time));
    }

    void OnOption(string name)
    {
        if (name == nameof(DockOptions.CompactWidgets))
        {
            var faces = (FaceSelector)Resources["Faces"];
            faces.On = vm.Options.CompactWidgets;
            Strip.ItemTemplateSelector = null;
            Strip.ItemTemplateSelector = faces;
            Dispatcher.BeginInvoke(Resize, DispatcherPriority.Loaded);
            return;
        }
        if (name is nameof(DockOptions.Enabled) or nameof(DockOptions.AllScreens) or nameof(DockOptions.Screen)
            or nameof(DockOptions.FollowWindows) or nameof(DockOptions.SyncTaskbar) or nameof(DockOptions.CheckUpdates)) return;
        ApplyOptions();
    }

    void Resize()
    {
        if (panel is null) return;
        var screen = TargetScreen();
        var room = screen.Bounds.Width / ScaleOf(screen);
        var allowance = LensMath.Allowance(vm.Options.Magnification, panel.Radius, IconSize);
        var target = Math.Clamp(fit * (room - 2 * allowance - 60) / Math.Max(1, panel.BaseWidth), 0.45, 1);
        if (Math.Abs(target - fit) > 0.03)
        {
            fit = target;
            ApplySizes();
            Dispatcher.BeginInvoke(Resize, DispatcherPriority.Loaded);
            return;
        }

        var headroom = IconSize * (vm.Options.Magnification - 1) + 48;
        Height = Math.Ceiling(headroom + BarHeight + BottomGap);
        Width = Math.Ceiling(Math.Max(360, panel.BaseWidth + 2 * allowance + 60));
        if (appBar) SetBarPos();
        Place();
    }

    Forms.Screen TargetScreen() =>
        Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == screenName) ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];

    static double ScaleOf(Forms.Screen screen) =>
        Native.DpiScale(Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + screen.Bounds.Width / 2, Y = screen.Bounds.Top + screen.Bounds.Height / 2 }, 2));

    void Place()
    {
        if (hwnd == 0) return;
        var screen = TargetScreen();
        var scale = ScaleOf(screen);
        var w = (int)Math.Round(Width * scale);
        var h = (int)Math.Round(Height * scale);
        var bottom = reserved?.Bottom ?? screen.WorkingArea.Bottom;
        var x = screen.Bounds.Left + (screen.Bounds.Width - w) / 2;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, bottom - h, w, h, Native.SWP_NOACTIVATE);
    }

    void Reserve(bool on)
    {
        if (hwnd == 0) return;
        if (on && !appBar)
        {
            var data = BarData();
            data.CallbackMessage = appBarMessage;
            Native.SHAppBarMessage(AbmNew, ref data);
            appBar = true;
        }
        else if (!on && appBar)
        {
            var data = BarData();
            Native.SHAppBarMessage(AbmRemove, ref data);
            appBar = false;
            reserved = null;
            return;
        }
        if (on) SetBarPos();
    }

    void SetBarPos(bool force = false)
    {
        var screen = TargetScreen();
        var height = (int)Math.Ceiling((BarHeight + BottomGap) * ScaleOf(screen));
        if (!force && reserved is { } r && r.Height == height && r.Left == screen.Bounds.Left && r.Right == screen.Bounds.Right) return;
        var data = BarData();
        data.Edge = AbeBottom;
        data.Bounds = new Native.Rect { Left = screen.Bounds.Left, Right = screen.Bounds.Right, Top = screen.Bounds.Bottom - height, Bottom = screen.Bounds.Bottom };
        Native.SHAppBarMessage(AbmQueryPos, ref data);
        data.Bounds.Top = data.Bounds.Bottom - height;
        Native.SHAppBarMessage(AbmSetPos, ref data);
        reserved = data.Bounds;
    }

    Native.AppBarData BarData() => new() { Size = Marshal.SizeOf<Native.AppBarData>(), Window = hwnd };

    nint WndProc(nint h, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == taskbarCreated)
        {
            appBar = false;
            reserved = null;
            Dispatcher.BeginInvoke(() =>
            {
                if (vm.Options.Enabled) ApplyOptions();
            }, DispatcherPriority.Background);
        }
        else if (msg == appBarMessage && (int)wParam == AbnPosChanged && appBar)
        {
            SetBarPos(force: true);
            Place();
        }
        else if (msg == WmDisplayChange) Dispatcher.BeginInvoke(Resize, DispatcherPriority.Background);
        return 0;
    }

    void OnPanelMove(object sender, MouseEventArgs e)
    {
        if (panel is null) return;
        if (!dragging) panel.Pointer = e.GetPosition(panel).X;
        if (pressed is null || pressedElement is null || e.LeftButton != MouseButtonState.Pressed) return;
        var at = e.GetPosition(Root);
        if (!dragging)
        {
            if (Math.Abs(at.X - pressAt.X) < 7 && Math.Abs(at.Y - pressAt.Y) < 7) return;
            StartDrag();
        }
        var shift = Shift(pressedElement);
        shift.X = at.X - pressAt.X;
        shift.Y = Math.Min(0, at.Y - pressAt.Y);
        var barTop = panel.TranslatePoint(new Point(0, panel.Bar.Top), Root).Y;
        var outside = pressed is AppTile && at.Y < barTop - IconSize * 0.9; // only apps can be dropped off the dock
        if (outside != dragOut)
        {
            dragOut = outside;
            pressedElement.Opacity = outside ? 0.45 : 0.9;
            if (outside) ShowName(Loc.T("dropToUnpin"));
            else HideName();
        }
        if (dragOut) PlaceName();
    }

    void StartDrag()
    {
        dragging = true;
        dragOut = false;
        hover.Stop();
        preview?.Hide();
        HideName();
        hovered = pressedElement;
        pressedElement!.Opacity = 0.9;
        Panel.SetZIndex(Container(pressedElement), 10);
        pressedElement.CaptureMouse();
        pressedElement.LostMouseCapture += OnDragLost;
        panel?.BeginAnimation(LensPanel.AmplitudeProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
    }

    void OnDragLost(object sender, MouseEventArgs e)
    {
        if (dragging) EndDrag(null);
    }

    void EndDrag(Point? at)
    {
        var tile = pressed!;
        var element = pressedElement!;
        dragging = false;
        pressed = null;
        element.LostMouseCapture -= OnDragLost;
        element.ReleaseMouseCapture();
        var shift = Shift(element);
        shift.X = 0;
        shift.Y = 0;
        element.Opacity = 1;
        Panel.SetZIndex(Container(element), 0);
        HideName();
        if (panel is { IsMouseOver: true }) panel.BeginAnimation(LensPanel.AmplitudeProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
        if (at is not { } point) return;
        if (tile is WidgetTile widget)
        {
            var siblings = vm.Tiles.OfType<WidgetTile>().Where(t => t != widget);
            vm.PlaceWidget(widget, siblings.FirstOrDefault(t => Strip.ItemContainerGenerator.ContainerFromItem(t) is FrameworkElement box
                                                              && box.TranslatePoint(new Point(box.ActualWidth / 2, 0), Root).X > point.X));
            return;
        }
        var app = (AppTile)tile;
        if (dragOut)
        {
            Unpinned(app);
            return;
        }
        var pins = vm.Tiles.OfType<AppTile>().Where(t => t.IsPinned).ToList();
        var others = pins.Where(t => t != app).ToList();
        var before = others.FirstOrDefault(t => Strip.ItemContainerGenerator.ContainerFromItem(t) is FrameworkElement box
                                                && box.TranslatePoint(new Point(box.ActualWidth / 2, 0), Root).X > point.X);
        var index = pins.IndexOf(app);
        var currentNext = index + 1 < pins.Count ? pins[index + 1] : null;
        if (before != currentNext) vm.Move(app, before);
    }

    UIElement Container(FrameworkElement element) => Strip.ItemContainerGenerator.ContainerFromItem(element.DataContext) as UIElement ?? element;

    void Unpinned(AppTile app)
    {
        if (vm.Unpin(app, vm.IsAlwaysPinned(app)) is { } back) ShowToast(Loc.F("unpinned", app.Name), back);
    }

    void ShowToast(string text, Action back)
    {
        undo = back;
        ToastText.Text = text;
        UndoText.Text = Loc.T("undo");
        Toast.Visibility = Visibility.Visible;
        Toast.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var barTop = panel is null ? Height - BarHeight : panel.TranslatePoint(new Point(0, panel.Bar.Top), Root).Y;
        Canvas.SetLeft(Toast, Math.Round((ActualWidth - Toast.DesiredSize.Width) / 2));
        Canvas.SetTop(Toast, Math.Round(barTop - Toast.DesiredSize.Height - 10));
        Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        toastTimer.Stop();
        toastTimer.Start();
    }

    void HideToast()
    {
        toastTimer.Stop();
        undo = null;
        Toast.Visibility = Visibility.Collapsed;
    }

    void OnUndo(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var back = undo;
        HideToast();
        back?.Invoke();
    }

    void OnPanelEnter(object sender, MouseEventArgs e)
    {
        if (panel is null) return;
        panel.Pointer = e.GetPosition(panel).X;
        panel.BeginAnimation(LensPanel.AmplitudeProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }

    void OnPanelLeave(object sender, MouseEventArgs e)
    {
        panel?.BeginAnimation(LensPanel.AmplitudeProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
        HideName();
    }

    void OnPanelWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        if (DateTime.Now - lastWheel < TimeSpan.FromMilliseconds(220)) return;
        lastWheel = DateTime.Now;
        _ = vm.SwitchByAsync(e.Delta > 0 ? -1 : 1);
    }

    void OnTileWheel(object sender, MouseWheelEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not WidgetTile w || !w.Wheel(e.Delta > 0 ? 1 : -1)) return;
        e.Handled = true;
        w.Poke();
    }

    void OnTileLoaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var now = DateTime.Now;
        if (now - revealBurst > TimeSpan.FromMilliseconds(500))
        {
            revealBurst = now;
            revealOrder = 0;
        }
        var delay = TimeSpan.FromMilliseconds(Math.Min(revealOrder++, 14) * 26);
        var shift = Shift(element);
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { BeginTime = delay, FillBehavior = FillBehavior.Stop });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(380))
        {
            BeginTime = delay,
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    static TranslateTransform Shift(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform { IsFrozen: false } t) return t;
        var fresh = new TranslateTransform();
        element.RenderTransform = fresh;
        return fresh;
    }

    void OnTileEnter(object sender, MouseEventArgs e)
    {
        var element = (FrameworkElement)sender;
        hovered = element;
        var name = element.DataContext switch
        {
            AppTile app => app.Name,
            StartTile s => s.Tip,
            _ => null
        };
        if (preview is { IsVisible: true } p)
        {
            if (element.DataContext is AppTile { IsRunning: true, IsFolder: false } app)
            {
                closePreview.Stop();
                if (p.Tile != app) ShowPreview(app, element);
                return;
            }
            closePreview.Start();
        }
        if (name is null) HideName();
        else ShowName(name);
        if (element.DataContext is AppTile { IsRunning: true, IsFolder: false }) hover.Start();
    }

    void OnTileLeave(object sender, MouseEventArgs e)
    {
        var element = (FrameworkElement)sender;
        element.Opacity = 1;
        if (hovered != element) return;
        hovered = null;
        hover.Stop();
        HideName();
        if (preview is { IsVisible: true }) closePreview.Start();
    }

    void ShowName(string text)
    {
        NameText.Text = text;
        NameTag.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        PlaceName();
    }

    void HideName() => NameTag.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(90)));

    void PlaceName()
    {
        if (hovered is null || !hovered.IsLoaded || PresentationSource.FromVisual(hovered) is null) return;
        var bounds = hovered.TransformToAncestor(Root).TransformBounds(new Rect(hovered.RenderSize));
        NameTag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = NameTag.DesiredSize;
        Canvas.SetLeft(NameTag, Math.Round(bounds.Left + bounds.Width / 2 - size.Width / 2));
        Canvas.SetTop(NameTag, Math.Round(bounds.Top - size.Height - 8));
    }

    void OnTileDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var element = (FrameworkElement)sender;
        element.Opacity = 0.72;
        pressedElement = element;
        pressed = element.DataContext switch { AppTile { IsPinned: true } app => app, WidgetTile widget => widget, _ => null };
        pressAt = e.GetPosition(Root);
    }

    void OnTileUp(object sender, MouseButtonEventArgs e)
    {
        var element = (FrameworkElement)sender;
        e.Handled = true;
        if (dragging)
        {
            EndDrag(e.GetPosition(Root));
            return;
        }
        pressed = null;
        element.Opacity = 1;
        switch (element.DataContext)
        {
            case AppTile app:
                OnApp(app, element, e.ChangedButton);
                break;
            case StartTile:
                if (e.ChangedButton == MouseButton.Left) ShellGuard.Start();
                else if (e.ChangedButton == MouseButton.Right) ShellGuard.PowerMenu();
                break;
            case WidgetTile widget:
                if (e.ChangedButton == MouseButton.Left && widget.HasPanel) OpenPanel(widget, element);
                else if (e.ChangedButton == MouseButton.Left)
                {
                    widget.Click(DateTime.Now);
                    widget.Poke();
                }
                else if (e.ChangedButton == MouseButton.Right) WidgetMenu(widget);
                break;
        }
    }

    void OnApp(AppTile app, FrameworkElement element, MouseButton button)
    {
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (button == MouseButton.Right)
        {
            AppMenu(app, element);
            return;
        }
        if (button == MouseButton.Middle || (button == MouseButton.Left && shift && !app.IsFolder))
        {
            Launch(app, element);
            return;
        }
        if (button != MouseButton.Left) return;
        if (ctrl)
        {
            if (app.RevealPath is { } path) Launcher.Reveal(path);
            return;
        }
        if (app.IsFolder)
        {
            OpenStack(app, element);
            return;
        }

        hover.Stop();
        var windows = app.Windows;
        if (windows.Count == 0)
        {
            Launch(app, element);
            return;
        }
        var foreground = Native.GetAncestor(Native.GetForegroundWindow(), Native.GA_ROOTOWNER);
        if (windows.Count == 1)
        {
            var w = windows[0];
            if (w.Handle == foreground && !Native.IsIconic(w.Handle)) WindowTracker.Minimize(w.Handle);
            else WindowTracker.Activate(w.Handle);
            preview?.Hide();
            return;
        }
        if (windows.Any(w => w.Handle == foreground)) ShowPreview(app, element);
        else
        {
            WindowTracker.Activate(windows[0].Handle);
            preview?.Hide();
        }
    }

    void Launch(AppTile app, FrameworkElement element)
    {
        if (!app.Launch())
        {
            vm.Main.Notify(Loc.F("launchFailed", app.Name));
            return;
        }
        Bounce(element);
    }

    void Bounce(FrameworkElement element)
    {
        var bounce = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(760), FillBehavior = FillBehavior.Stop };
        var up = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var down = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        var lift = -IconSize * 0.32;
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(lift, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)), up));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340)), down));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(lift * 0.45, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480)), up));
        bounce.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620)), down));
        Shift(element).BeginAnimation(TranslateTransform.YProperty, bounce);
    }

    void ShowPreview(AppTile app, FrameworkElement element)
    {
        if (!app.IsRunning) return;
        HideName();
        closePreview.Stop();
        if (preview is null)
        {
            preview = new PreviewWindow();
            preview.PointerEntered += () => closePreview.Stop();
            preview.PointerLeft += () => closePreview.Start();
            preview.Picked += h =>
            {
                WindowTracker.Activate(h);
                preview.Hide();
            };
        }
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        var anchorTop = Math.Min(topLeft.Y, bottomRight.Y);
        var barTop = panel is null ? anchorTop : panel.PointToScreen(new Point(0, panel.Bar.Top)).Y;
        preview.ShowFor(app, (topLeft.X + bottomRight.X) / 2, Math.Min(anchorTop, barTop), TargetScreen(), Dark);
    }

    DockTheme Theme => DockTheme.Find(vm.Options.Theme);

    bool Dark => Theme.Dark;

    void OpenStack(AppTile app, FrameworkElement element)
    {
        if (app.Folder is not { } folder) return;
        ShowFlyout(new StackWindow(folder, Dark), element);
        folder.MarkSeen();
    }

    void OpenPanel(WidgetTile widget, FrameworkElement element)
    {
        Flyout? next = widget switch
        {
            ClaudeWidget => new ClaudePanel(ClaudeUsage.Shared, Dark),
            MusicWidget => new MusicPanel(MediaSessions.Shared, Dark),
            WeatherWidget weather => new WeatherPanel(weather.Service, Dark),
            NoteWidget note => new NotePanel(note, Dark),
            TodoWidget todo => new TodoPanel(todo, Dark),
            RigdeckWidget rigdeck => Rigdeck(rigdeck, element),
            GamesWidget games => new GamesPanel(games, Dark),
            _ => null
        };
        if (next is not null) ShowFlyout(next, element);
    }

    RigdeckPanel Rigdeck(RigdeckWidget widget, FrameworkElement element)
    {
        var rigdeck = new RigdeckPanel(widget);
        rigdeck.Browse += path => ShowFlyout(new StackWindow(path, Dark), element);
        return rigdeck;
    }

    void OnMediaToggle(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ChangedButton == MouseButton.Left) _ = MediaSessions.Shared.Toggle();
    }

    void OnMediaPrevious(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ChangedButton == MouseButton.Left) _ = MediaSessions.Shared.Previous();
    }

    void OnMediaNext(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ChangedButton == MouseButton.Left) _ = MediaSessions.Shared.Next();
    }

    void ShowFlyout(Flyout next, FrameworkElement element)
    {
        var key = (element.DataContext ?? element, next.GetType());
        if ((flyout is not null && flyoutKey == key) || (closedKey == key && DateTime.Now - closedAt < TimeSpan.FromMilliseconds(400)))
        {
            closedKey = null;
            flyout?.Close();
            next.Close();
            return;
        }
        HideName();
        preview?.Hide();
        flyout?.Close();
        flyout = next;
        flyoutKey = key;
        next.Closed += (_, _) =>
        {
            if (flyout != next) return;
            flyout = null;
            closedKey = key;
            closedAt = DateTime.Now;
        };
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        var barTop = panel is null ? topLeft.Y : panel.PointToScreen(new Point(0, panel.Bar.Top)).Y;
        next.ShowAt((topLeft.X + bottomRight.X) / 2, barTop, TargetScreen());
    }

    public bool OpenNth(int n, bool fresh)
    {
        if (vm.Tiles.OfType<AppTile>().Skip(n - 1).FirstOrDefault() is not { } app) return false;
        var element = Strip.ItemContainerGenerator.ContainerFromItem(app) as FrameworkElement ?? Root;
        Dispatcher.BeginInvoke(() => OnApp(app, element, fresh ? MouseButton.Middle : MouseButton.Left));
        return true;
    }

    public bool ShowTour()
    {
        if (panel is null || !IsVisible || fullscreen) return false;
        ShowFlyout(new TourPanel(Dark), panel);
        return true;
    }

    void OnTrayClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (vm.Shell.Peeking) PeekTaskbar();
        else ShowFlyout(new TrayPanel(Dark, PeekTaskbar), (FrameworkElement)sender);
    }

    void PeekTaskbar()
    {
        var show = !vm.Shell.Peeking;
        vm.Shell.Peek(show);
        if (show) poll.Start();
        peekAway = 0;
        if (show && Native.FindWindow("Shell_TrayWnd", null) is var bar && bar != 0) Native.SetForegroundWindow(bar);
    }

    void OnStatusClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShellGuard.QuickSettings();
    }

    void OnVolumeClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (Audio.Read() is { } now) Audio.SetMute(!now.Muted);
            vm.System.RefreshVolume();
            return;
        }
        ShowFlyout(new SoundPanel(Dark), (FrameworkElement)sender);
    }

    void OnVolumeWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        Audio.Nudge(e.Delta > 0 ? 1 : -1);
        vm.System.RefreshVolume();
    }

    void OnMicClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Audio.ToggleMic();
        vm.System.RefreshVolume();
        vm.RefreshMic();
    }

    void OnClockClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShellGuard.Notifications();
    }

    void OnDesktopClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShellGuard.ShowDesktop();
    }

    void OnSwitched()
    {
        ApplyAccent(true);
        flyout?.Close();
        preview?.Hide();
        if (vm.Active is not { } env || panel is null) return;
        BannerText.Text = $"{env.Glyph}  {env.Name}";
        Banner.Background = accent;
        Banner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(Banner, Math.Round((ActualWidth - Banner.DesiredSize.Width) / 2));
        Canvas.SetTop(Banner, Math.Round(panel.Bar.Top - Banner.DesiredSize.Height - 14));
        var show = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1700) };
        show.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
        show.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1250))));
        show.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700))));
        Banner.BeginAnimation(OpacityProperty, show);
        if (vm.Options.AutoHide)
        {
            Reveal();
            away = -18;
        }
    }

    void Poll()
    {
        if (!(vm.Options.AutoHide || vm.Shell.Peeking))
        {
            poll.Stop();
            return;
        }
        if (!IsVisible && !fullscreen) return;
        Native.GetCursorPos(out var p);
        var screen = TargetScreen().Bounds;

        if (vm.Shell.Peeking)
        {
            var bar = Native.FindWindow("Shell_TrayWnd", null);
            var over = Native.GetWindowRect(bar, out var r) && p.X >= r.Left && p.X < r.Right && p.Y >= r.Top - 4 && p.Y <= r.Bottom;
            peekAway = over ? 0 : peekAway + 1;
            if (peekAway > 30 && !Native.GetForegroundWindow().Equals(bar)) vm.Shell.Peek(false);
        }

        if (!vm.Options.AutoHide || fullscreen) return;
        var edge = p.Y >= screen.Bottom - 2 && p.X >= screen.Left && p.X < screen.Right;
        if (concealed)
        {
            if (edge) Reveal();
            return;
        }
        var busy = menuOpen || dragging || undo is not null || (panel?.IsMouseOver ?? false) || preview is { IsVisible: true } || flyout is not null;
        away = busy || edge ? Math.Min(away, 0) : away + 1;
        if (away > 7) Conceal();
    }

    void Conceal()
    {
        concealed = true;
        vm.SetOffScreen(this, true);
        preview?.Hide();
        var distance = BarHeight + BottomGap + 16;
        Slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(distance, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
    }

    void Reveal()
    {
        concealed = false;
        vm.SetOffScreen(this, fullscreen);
        away = 0;
        Slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    void CheckFullscreen()
    {
        var now = IsFullscreen(Native.GetForegroundWindow());
        if (now == fullscreen) return;
        fullscreen = now;
        vm.SetOffScreen(this, fullscreen || concealed);
        if (!vm.Options.Enabled) return;
        if (fullscreen)
        {
            preview?.Hide();
            Hide();
        }
        else
        {
            Show();
            ApplyOptions();
        }
    }

    bool IsFullscreen(nint window)
    {
        if (window == 0 || hwnd == 0) return false;
        if (Native.ClassOf(window) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        Native.GetWindowThreadProcessId(window, out var pid);
        if (pid == Environment.ProcessId) return false;
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        var monitor = Native.MonitorFromWindow(window, 2);
        if (!Native.GetMonitorInfo(monitor, ref info) || !Native.GetWindowRect(window, out var r)) return false;
        var screen = TargetScreen().Bounds;
        var m = info.Monitor;
        var ours = m.Left == screen.Left && m.Top == screen.Top;
        if (!ours || r.Left > m.Left || r.Top > m.Top || r.Right < m.Right || r.Bottom < m.Bottom) return false;
        return !IsZoomed(window) || (r.Left == m.Left && r.Top == m.Top && r.Right == m.Right && r.Bottom == m.Bottom);
    }

    [DllImport("user32.dll")]
    static extern bool IsZoomed(nint hwnd);

    void OnPanelMenu(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        var menu = new ContextMenu();
        foreach (var p in vm.Main.Presets)
        {
            var preset = p;
            menu.Items.Add(Item($"{p.Glyph}  {p.Name}", () => vm.Pick(preset), p == vm.Active));
        }
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("replaceTaskbar"), () => vm.Options.ReplaceTaskbar = !vm.Options.ReplaceTaskbar, vm.Options.ReplaceTaskbar));
        menu.Items.Add(Item(Loc.T("autoHide"), () => vm.Options.AutoHide = !vm.Options.AutoHide, vm.Options.AutoHide));
        menu.Items.Add(Item(Loc.T("followWindows"), () => vm.Options.FollowWindows = !vm.Options.FollowWindows, vm.Options.FollowWindows));
        menu.Items.Add(Item(Loc.T("trayStartup"), () => vm.Options.StartWithWindows = !vm.Options.StartWithWindows, vm.Options.StartWithWindows));
        menu.Items.Add(Item(Loc.T("compactWidgets"), () => vm.Options.CompactWidgets = !vm.Options.CompactWidgets, vm.Options.CompactWidgets));
        if (global::System.Windows.Forms.Screen.AllScreens.Length > 1)
            menu.Items.Add(Item(Loc.T("perScreen"), () => vm.Options.PerScreen = !vm.Options.PerScreen, vm.Options.PerScreen));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("dockSettings"), openSettings));
        menu.Items.Add(Item(Loc.T("trayExit"), () => Application.Current.Shutdown()));
        Open(menu);
    }

    void AppMenu(AppTile app, FrameworkElement element)
    {
        HideName();
        hover.Stop();
        preview?.Hide();
        var menu = new ContextMenu();
        if (app.IsFolder) menu.Items.Add(Item(Loc.T("openFolder"), () => Launcher.OpenFile(app.Folder!.Path)));
        else menu.Items.Add(Item(app.IsRunning ? Loc.T("newWindow") : Loc.T("open"), () => Launch(app, element)));
        menu.Items.Add(new Separator());
        if (app.IsPinned)
        {
            var everywhere = vm.IsAlwaysPinned(app);
            menu.Items.Add(Item(Loc.T(everywhere ? "unpinEverywhere" : "unpinHere"), () => Unpinned(app)));
        }
        else
        {
            menu.Items.Add(Item(Loc.T("pinHere"), () => vm.Pin(app, false), enabled: vm.Active is not null));
            menu.Items.Add(Item(Loc.T("pinEverywhere"), () => vm.Pin(app, true)));
        }
        if (app.RevealPath is { } path) menu.Items.Add(Item(Loc.T("showInExplorer"), () => Launcher.Reveal(path)));
        if (app.IsRunning)
        {
            menu.Items.Add(new Separator());
            var windows = app.Windows.ToList();
            menu.Items.Add(Item(windows.Count == 1 ? Loc.T("closeWindow") : Loc.F("closeWindows", windows.Count),
                () => windows.ForEach(w => WindowTracker.Close(w.Handle))));
        }
        Open(menu);
    }

    void WidgetMenu(WidgetTile widget)
    {
        var menu = new ContextMenu();
        if (widget.CanReset) menu.Items.Add(Item(Loc.T("reset"), () =>
        {
            widget.Reset();
            widget.Poke();
        }));
        var square = widget.Square(vm.Options.CompactWidgets);
        menu.Items.Add(Item(Loc.T("widgetSquare"), () => Resize(widget, WidgetSpec.SquareSize), square));
        if (widget is MusicWidget)
        {
            var size = widget.Spec.Size;
            menu.Items.Add(Item(Loc.T("musicPause"), () => Resize(widget, WidgetSpec.WideSize),
                !square && size is not (WidgetSpec.FullSize or WidgetSpec.BackdropSize)));
            menu.Items.Add(Item(Loc.T("musicFull"), () => Resize(widget, WidgetSpec.FullSize), size == WidgetSpec.FullSize));
            menu.Items.Add(Item(Loc.T("musicBackdrop"), () => Resize(widget, WidgetSpec.BackdropSize), size == WidgetSpec.BackdropSize));
        }
        else menu.Items.Add(Item(Loc.T("widgetWide"), () => Resize(widget, WidgetSpec.WideSize), !square));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("widgetLeft"), () => vm.MoveWidget(widget, -1), enabled: vm.Neighbor(widget, -1) is not null));
        menu.Items.Add(Item(Loc.T("widgetRight"), () => vm.MoveWidget(widget, 1), enabled: vm.Neighbor(widget, 1) is not null));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("editWidgets"), openSettings));
        Open(menu);
    }

    void Resize(WidgetTile widget, string size)
    {
        widget.Resize(size);
        vm.Options.Retemplate();
    }

    static MenuItem Item(string header, Action click, bool check = false, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsChecked = check, IsEnabled = enabled };
        item.Click += (_, _) => click();
        return item;
    }

    void Open(ContextMenu menu)
    {
        menuOpen = true;
        menu.Placement = PlacementMode.MousePoint;
        Native.SetForegroundWindow(hwnd);
        menu.Closed += (_, _) =>
        {
            menuOpen = false;
            menuClosedAt = DateTime.Now;
        };
        menu.IsOpen = true;
    }

    void OnDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var files = e.Data.GetDataPresent(DataFormats.FileDrop);
        if (files && DropTarget(e) is (var app, var element))
        {
            var opens = app.Opener() is not null;
            e.Effects = opens ? DragDropEffects.Copy : DragDropEffects.None;
            if (hovered != element)
            {
                hovered = element;
                ShowName(Loc.F(opens ? "openWith" : "cantOpen", app.Name));
            }
            return;
        }
        if (hovered is not null && hovered.DataContext is AppTile) HideName();
        hovered = null;
        e.Effects = files && vm.Active is not null ? DragDropEffects.Link : DragDropEffects.None;
    }

    (AppTile App, FrameworkElement Element)? DropTarget(DragEventArgs e)
    {
        (AppTile, FrameworkElement)? found = null;
        for (var d = e.OriginalSource as DependencyObject; d is Visual; d = VisualTreeHelper.GetParent(d))
        {
            if (d is not FrameworkElement { DataContext: AppTile { IsFolder: false } app } element) break;
            found = (app, element);
        }
        return found;
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        HideName();
        hovered = null;
        if (DropTarget(e) is (var app, var element))
        {
            if (app.Opener() is not (var exe, var args)) return;
            if (Launcher.OpenWith(exe, args, files)) Bounce(element);
            else vm.Main.Notify(Loc.F("launchFailed", app.Name));
            return;
        }
        if (vm.Active is not { } env) return;
        var items = files
            .Where(f => File.Exists(f) || Directory.Exists(f))
            .Select(f => new PinItem { Name = Directory.Exists(f) ? new DirectoryInfo(f).Name : Path.GetFileNameWithoutExtension(f), Target = f })
            .ToList();
        if (items.Count > 0) vm.Main.AddPrograms(env, items);
    }

    static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    static LinearGradientBrush Gradient(string top, string bottom)
    {
        var b = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top), (Color)ColorConverter.ConvertFromString(bottom), 90);
        b.Freeze();
        return b;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Reserve(false);
        poll.Stop();
        hover.Stop();
        closePreview.Stop();
        toastTimer.Stop();
        preview?.Close();
        flyout?.Close();
        vm.Layout -= OnLayout;
        vm.Switched -= OnSwitched;
        vm.Options.Changed -= OnOption;
        vm.Tracker.Changed -= CheckFullscreen;
        vm.Tracker.Reshaped -= CheckFullscreen;
        vm.ForgetWindow(this);
        SystemParameters.StaticPropertyChanged -= OnSystemParameter;
        base.OnClosing(e);
    }
}
