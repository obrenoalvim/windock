using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using WinDock.Models;
using Forms = System.Windows.Forms;

namespace WinDock.Services;

public static class RigStep
{
    public const string Open = "open", Command = "cmd", Keys = "keys", System = "system", Sound = "sound", Wait = "wait";
}

public sealed class RigStepSpec
{
    public string Type { get; set; } = RigStep.Open;
    public string Target { get; set; } = "";
    public int? Monitor { get; set; }
    public bool Fullscreen { get; set; }
    public string? Icon { get; set; }
}

public sealed class RigAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public bool Parallel { get; set; }
    public List<RigStepSpec> Steps { get; set; } = [];

    [JsonIgnore]
    public RigStepSpec? FirstOpen => Steps.FirstOrDefault(s => s.Type == RigStep.Open && s.Target.Length > 0);

    // A shortcut that is just a folder lists its contents instead of opening Explorer.
    [JsonIgnore]
    public string? Folder => Steps is [{ Type: RigStep.Open } step] && Directory.Exists(Environment.ExpandEnvironmentVariables(step.Target))
        ? Environment.ExpandEnvironmentVariables(step.Target) : null;

    [JsonIgnore]
    public string Tip => Steps.Count == 1 ? Steps[0].Target : $"{Name} · {Steps.Count}";

    public static RigAction Single(string name, string target, string? icon = null) =>
        new() { Name = name, Icon = icon, Steps = [new RigStepSpec { Target = target }] };

    public RigAction Clone() => JsonSerializer.Deserialize<RigAction>(JsonSerializer.Serialize(this))!;
}

public sealed record RigSystem(string Id, string Label, string Glyph);

// RigDeck shortcuts live as JSON in the widget text. The first version stored one "name|icon|target" line per shortcut; those still load.
public static class RigStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault
    };

    public static List<RigAction> Parse(string? text)
    {
        text = (text ?? "").Trim();
        if (text.StartsWith('['))
        {
            try
            {
                return (JsonSerializer.Deserialize<List<RigAction>>(text, Options) ?? [])
                    .Where(a => a.Name.Length > 0 && a.Steps.Count > 0)
                    .ToList();
            }
            catch (JsonException) { return []; }
        }
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 3, StringSplitOptions.TrimEntries))
            .Select(p => p.Length switch
            {
                3 => RigAction.Single(p[0], p[2], p[1].Length > 0 ? p[1] : null),
                2 => RigAction.Single(p[0], p[1]),
                _ => null
            })
            .OfType<RigAction>()
            .Where(a => a.Name.Length > 0 && a.Steps[0].Target.Length > 0)
            .ToList();
    }

    public static string? Format(IEnumerable<RigAction> actions)
    {
        var list = actions.ToList();
        return list.Count == 0 ? null : JsonSerializer.Serialize(list, Options);
    }

    // Store, Steam and Epic pins launch through Args (shell:AppsFolder, steam://, ...); plain programs, folders and files through Target.
    public static RigAction FromPin(PinItem pin) =>
        RigAction.Single(pin.Name, (pin.IsLauncher ? pin.Args : pin.ResolvedTarget) ?? pin.Target,
            pin.ResolvedIcon is { } icon && File.Exists(icon) ? icon
            : pin.IsLauncher && pin.Args!.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase) ? pin.Args
            : pin.ResolvedTarget);

    // "ctrl+shift+m" -> virtual keys, null when a part is unknown
    public static byte[]? Chord(string text)
    {
        var keys = new List<byte>();
        foreach (var part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = part.ToLowerInvariant();
            byte? vk = p switch
            {
                "ctrl" or "control" => 0x11,
                "shift" => 0x10,
                "alt" => 0x12,
                "win" or "windows" => Native.VK_LWIN,
                "enter" => 0x0D, "esc" or "escape" => 0x1B, "tab" => 0x09, "space" => 0x20, "backspace" => 0x08,
                "delete" or "del" => 0x2E, "insert" => 0x2D, "home" => 0x24, "end" => 0x23, "pageup" => 0x21, "pagedown" => 0x22,
                "left" => 0x25, "up" => 0x26, "right" => 0x27, "down" => 0x28, "printscreen" => 0x2C,
                "playpause" => 0xB3, "nexttrack" => 0xB0, "prevtrack" => 0xB1, "volumeup" => 0xAF, "volumedown" => 0xAE, "volumemute" => 0xAD,
                _ when p.Length == 1 && (char.IsAsciiLetterOrDigit(p[0])) => (byte)char.ToUpperInvariant(p[0]),
                _ when p.Length is 2 or 3 && p[0] == 'f' && int.TryParse(p[1..], out var n) && n is >= 1 and <= 24 => (byte)(0x6F + n),
                _ => null
            };
            if (vk is null) return null;
            keys.Add(vk.Value);
        }
        return keys.Count == 0 ? null : keys.ToArray();
    }
}

public static class RigRunner
{
    public static readonly RigSystem[] Controls =
    [
        new("mic", "rigMic", ""), new("mute", "rigMute", ""), new("voldown", "rigVolDown", ""), new("volup", "rigVolUp", ""),
        new("prev", "rigPrev", ""), new("play", "rigPlay", ""), new("next", "rigNext", ""),
        new("lock", "rigLock", ""), new("desktop", "rigDesktop", ""), new("shot", "rigShot", ""), new("tasks", "rigTasks", "")
    ];

    static readonly Dictionary<string, List<(int Pid, nint Hwnd)>> Opened = [];
    static readonly List<MediaPlayer> Players = [];

    public static async Task Run(RigAction action)
    {
        var steps = action.Steps.ToList();
        if (action.Parallel) await Task.WhenAll(steps.Select(s => Safe(action, s)));
        else
            foreach (var step in steps) await Safe(action, step);
    }

    static async Task Safe(RigAction action, RigStepSpec step)
    {
        try { await Step(action, step); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UriFormatException or ArgumentException) { }
    }

    // Closes the windows (or processes) this shortcut opened, not other windows of the same app that were already there.
    public static int Close(RigAction action)
    {
        List<(int Pid, nint Hwnd)> list;
        lock (Opened)
        {
            if (!Opened.Remove(action.Id, out list!)) return 0;
        }
        foreach (var (pid, hwnd) in list)
        {
            if (hwnd != 0 && Native.IsWindow(hwnd)) Native.PostMessage(hwnd, 0x10, 0, 0);
            else
            {
                try { using var p = Process.GetProcessById(pid); p.CloseMainWindow(); }
                catch (ArgumentException) { }
            }
        }
        return list.Count;
    }

    public static Task Press(string id) => Step(null, new RigStepSpec { Type = RigStep.System, Target = id });

    static async Task Step(RigAction? owner, RigStepSpec step)
    {
        var target = Environment.ExpandEnvironmentVariables(step.Target.Trim());
        switch (step.Type)
        {
            case RigStep.Open:
                await Open(owner, step, target);
                break;
            case RigStep.Command:
                Process.Start(new ProcessStartInfo("cmd.exe", "/c " + target) { CreateNoWindow = true, UseShellExecute = false })?.Dispose();
                break;
            case RigStep.Keys:
                if (RigStore.Chord(target) is { } keys) Native.Keys(keys);
                break;
            case RigStep.Wait:
                if (double.TryParse(target, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                    await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 120)));
                break;
            case RigStep.Sound:
                await Application.Current.Dispatcher.InvokeAsync(() => Play(target));
                break;
            case RigStep.System:
                await Application.Current.Dispatcher.InvokeAsync(() => Control(target)).Task.Unwrap();
                break;
        }
    }

    static void Play(string path)
    {
        if (!File.Exists(path)) return;
        var player = new MediaPlayer();
        void Done(object? s, EventArgs e) { player.Close(); Players.Remove(player); }
        player.MediaEnded += Done;
        player.MediaFailed += Done;
        Players.Add(player);
        player.Open(new Uri(path));
        player.Play();
    }

    static async Task Control(string id)
    {
        switch (id)
        {
            case "mic": Audio.ToggleMic(); break;
            case "mute": Audio.SetMute(!(Audio.Read()?.Muted ?? false)); break;
            case "volup": Audio.Nudge(5); break;
            case "voldown": Audio.Nudge(-5); break;
            case "play" or "prev" or "next":
                var media = MediaSessions.Shared;
                media.Start();
                if (media.Current is null) await Task.Delay(600);
                await (id == "play" ? media.Toggle() : id == "next" ? media.Next() : media.Previous());
                break;
            case "lock": Process.Start("rundll32.exe", "user32.dll,LockWorkStation")?.Dispose(); break;
            case "desktop": Native.Keys(Native.VK_LWIN, 0x44); break;
            case "shot": Native.Keys(Native.VK_LWIN, 0x10, 0x53); break;
            case "tasks": Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true })?.Dispose(); break;
        }
    }

    static async Task Open(RigAction? owner, RigStepSpec step, string target)
    {
        if (!Launcher.OpenFile(target)) return;
        var exe = ExeOf(target);
        if (exe is null) return;

        var found = await Task.Run(() => WaitWindow(Path.GetFileNameWithoutExtension(exe), TimeSpan.FromSeconds(30)));
        if (found is null) return;
        var (pid, hwnd) = found.Value;
        if (owner is not null)
            lock (Opened)
            {
                if (!Opened.TryGetValue(owner.Id, out var list)) Opened[owner.Id] = list = [];
                list.RemoveAll(e => e.Pid == pid && e.Hwnd == hwnd);
                list.Add((pid, hwnd));
            }
        if (step.Monitor is not null || step.Fullscreen) Place(hwnd, step);
    }

    // Only programs have a process we can follow; sites, steam:// links and Store apps open and that is all.
    static string? ExeOf(string target)
    {
        if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) target = ShellLinks.ReadTarget(target) ?? "";
        return target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target : null;
    }

    static (int Pid, nint Hwnd)? WaitWindow(string name, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        do
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                using (p)
                {
                    var hwnd = p.MainWindowHandle;
                    if (hwnd != 0 && Native.IsWindowVisible(hwnd)) return (p.Id, hwnd);
                }
            }
            Thread.Sleep(400);
        } while (DateTime.UtcNow < until);
        return null;
    }

    static void Place(nint hwnd, RigStepSpec step)
    {
        const int Restore = 9, Maximize = 3;
        Native.ShowWindow(hwnd, Restore);
        if (step.Monitor is { } m && Forms.Screen.AllScreens is { Length: > 0 } screens)
        {
            var b = screens[Math.Clamp(m, 0, screens.Length - 1)].Bounds;
            Native.SetWindowPos(hwnd, 0, b.X, b.Y, 0, 0, Native.SWP_NOSIZE);
            if (!step.Fullscreen) Native.SetWindowPos(hwnd, 0, b.X, b.Y, b.Width, b.Height, 0);
        }
        if (step.Fullscreen) Native.ShowWindow(hwnd, Maximize);
        Native.SetForegroundWindow(hwnd);
    }
}

// The RigDeck mark (public/icon.svg of the rigdeck repo): four keys on a near-black tile.
public static class RigLogo
{
    public static readonly ImageSource Image = Build();

    static ImageSource Build()
    {
        static SolidColorBrush Fill(string hex, double opacity = 1)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }
        static GeometryDrawing Key(double x, double y, Brush fill) =>
            new(fill, null, new RectangleGeometry(new Rect(x, y, 148, 148), 30, 30));

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Fill("#0A0B0D"), null, new RectangleGeometry(new Rect(0, 0, 512, 512), 100, 100)));
        group.Children.Add(Key(92, 92, Fill("#FF8A1E")));
        group.Children.Add(Key(272, 92, Fill("#3A2410")));
        group.Children.Add(Key(92, 272, Fill("#3A2410")));
        group.Children.Add(Key(272, 272, Fill("#FF8A1E", 0.55)));
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
