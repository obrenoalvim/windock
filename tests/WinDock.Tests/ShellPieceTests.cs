using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Tests;

public class ShellPieceTests
{
    [Theory]
    [InlineData("<badge value=\"3\"/>", 3)]
    [InlineData("<badge value=\"newMessage\"/>", Badges.Dot)]
    [InlineData("<badge value=\"none\"/>", 0)]
    [InlineData("<badge value=\"0\"/>", 0)]
    [InlineData("<tile/>", 0)]
    public void Badge_payloads_become_counts(string payload, int expected) => Assert.Equal(expected, Badges.Parse(payload));

    [Theory]
    [InlineData("(3) WhatsApp - Brave", 3)]
    [InlineData("(12) Discord | #general", 12)]
    [InlineData("WhatsApp", 0)]
    [InlineData("Report (3) final.docx - Word", 0)]
    public void Window_titles_can_carry_a_count(string title, int expected) => Assert.Equal(expected, Badges.FromTitle(title));

    [Fact]
    public void Squirrel_shortcuts_match_the_versioned_app_folder()
    {
        Assert.Equal(@"C:\Users\x\AppData\Local\Discord\",
            AppMatch.UpdaterFolder(@"C:\Users\x\AppData\Local\Discord\Update.exe", "--processStart Discord.exe"));
        Assert.Null(AppMatch.UpdaterFolder(@"C:\Program Files\App\app.exe", "--new-window"));
        Assert.Null(AppMatch.UpdaterFolder(@"C:\Program Files\App\app.exe", null));
    }

    [Fact]
    public void Only_exes_of_one_program_folder_stack()
    {
        Assert.True(AppMatch.SameProgramFolder(@"C:\Program Files\Google\Play Games\current\client\client.exe",
            @"C:\Program Files\Google\Play Games\current\emulator\crosvm.exe"));
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.False(AppMatch.SameProgramFolder(Path.Combine(programs, @"Alpha\a.exe"), Path.Combine(programs, @"Beta\b.exe")));
        Assert.False(AppMatch.SameProgramFolder(@"C:\a.exe", @"D:\a.exe"));
    }

    [Fact]
    public void Known_folder_paths_of_the_tray_list_resolve()
    {
        var windows = TrayApps.Resolve(@"{F38BF404-1D43-42F2-9305-67DE0B28FC23}\explorer.exe");
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), windows, ignoreCase: true);
        Assert.Equal(@"C:\Tools\app.exe", TrayApps.Resolve(@"C:\Tools\app.exe"));
    }

    [Fact]
    public void Compact_faces_show_the_main_value()
    {
        var claude = (ClaudeWidget)WidgetTile.Create("k", new Models.WidgetSpec { Kind = Models.WidgetSpec.Claude });
        claude.RefreshFace();
        Assert.True(claude.Face!.HasRing && claude.Face.HasInnerRing);
        Assert.Equal(ClaudeUsage.Shared.Last is null ? "—" : "", claude.Face.Value);
        var progress = WidgetTile.Create("p", new Models.WidgetSpec { Kind = Models.WidgetSpec.Progress });
        progress.Tick(new DateTime(2026, 10, 13, 12, 0, 0));
        progress.RefreshFace();
        Assert.Equal("50%", progress.Face!.Value);
        Assert.True(progress.Face.HasRing);
    }
}
