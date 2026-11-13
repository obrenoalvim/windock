using System.Text;
using Forms = System.Windows.Forms;

namespace WinDock.Services;

public static class ProblemReport
{
    public const string NewIssue = "https://github.com/obrenoalvim/windock/issues/new";

    public static string Body(Version version, Version windows, IEnumerable<string> screens, string language, bool replacesTaskbar) =>
        new StringBuilder()
            .AppendLine("**What happened?**")
            .AppendLine()
            .AppendLine()
            .AppendLine("**What did you expect?**")
            .AppendLine()
            .AppendLine()
            .AppendLine("---")
            .AppendLine($"WinDock {version}")
            .AppendLine($"Windows {windows}")
            .AppendLine($"Screens: {string.Join(", ", screens)}")
            .AppendLine($"Language: {language}")
            .AppendLine($"Replaces the taskbar: {(replacesTaskbar ? "yes" : "no")}")
            .ToString();

    public static string Url(string body) => $"{NewIssue}?body={Uri.EscapeDataString(body)}";

    public static void Open(bool replacesTaskbar)
    {
        var screens = Forms.Screen.AllScreens.Select(s =>
        {
            var monitor = Native.MonitorFromPoint(new Native.Point { X = s.Bounds.Left + s.Bounds.Width / 2, Y = s.Bounds.Top + s.Bounds.Height / 2 }, 2);
            return $"{s.Bounds.Width}x{s.Bounds.Height} at {Native.DpiScale(monitor):P0}";
        });
        Launcher.OpenFile(Url(Body(Updates.Current, Environment.OSVersion.Version, screens, Loc.Instance.Language, replacesTaskbar)));
    }
}
