using WinDock.Models;
using WinDock.Services;

namespace WinDock.Tests;

public class LauncherTests
{
    static readonly string Explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    [Theory]
    [InlineData("steam://rungameid/1243130", true)]
    [InlineData("com.epicgames.launcher://apps/fn%3Aabc%3AFortnite?action=launch&silent=true", true)]
    [InlineData(@"shell:AppsFolder\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", false)]
    public void Only_protocol_links_use_the_protocol_handler(string args, bool expected)
    {
        var item = new PinItem { Name = "x", Target = Explorer, Args = args, AppId = "WinDock.Test" };

        Assert.Equal(expected, item.IsUriLauncher);
    }

    [Fact]
    public void A_pinned_game_shortcut_opens_the_link_through_the_protocol_handler_not_explorer()
    {
        const string uri = "com.epicgames.launcher://apps/fn%3Aabc%3AFortnite?action=launch&silent=true";
        var preset = new Preset { Items = [new PinItem { Name = "Fortnite", Target = Explorer, Args = uri, AppId = "WinDock.Epic.Fortnite" }] };

        var snapshot = PinBuilder.Build(preset, new Snapshot());
        var path = Path.Combine(Path.GetTempPath(), $"windock-test-{Guid.NewGuid():N}.lnk");
        File.WriteAllBytes(path, Convert.FromBase64String(snapshot.Links["Fortnite.lnk"]));
        try
        {
            var shortcut = (dynamic)Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            var link = shortcut.CreateShortcut(path);

            Assert.EndsWith("rundll32.exe", (string)link.TargetPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("url.dll,FileProtocolHandler " + uri, (string)link.Arguments);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Store_apps_keep_launching_through_the_shell_namespace()
    {
        const string args = @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        var preset = new Preset { Items = [new PinItem { Name = "Calc", Target = Explorer, Args = args, AppId = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" }] };

        var snapshot = PinBuilder.Build(preset, new Snapshot());
        var path = Path.Combine(Path.GetTempPath(), $"windock-test-{Guid.NewGuid():N}.lnk");
        File.WriteAllBytes(path, Convert.FromBase64String(snapshot.Links["Calc.lnk"]));
        try
        {
            var shortcut = (dynamic)Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            var link = shortcut.CreateShortcut(path);

            Assert.EndsWith("explorer.exe", (string)link.TargetPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(args, (string)link.Arguments);
        }
        finally { File.Delete(path); }
    }
}
