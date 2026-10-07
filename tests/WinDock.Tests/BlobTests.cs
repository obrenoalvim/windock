using WinDock.Services;

namespace WinDock.Tests;

public class BlobTests
{
    static (byte[] Fav, byte[] Res) Chunk(string name, string appId) =>
        TaskbarBlob.BuildClassic(name, @"C:\Users\x\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\" + name,
            2440, appId, new HashSet<string>(), new Random(1));

    [Fact]
    public void Favorite_entry_for_a_short_name_matches_the_size_windows_writes()
    {
        var (fav, _) = Chunk("Brave.lnk", "Brave");
        Assert.Equal(5 + 314, fav.Length);
    }

    [Theory]
    [InlineData("Brave.lnk", "Brave")]
    [InlineData("Visual Studio Code.lnk", "Microsoft.VisualStudioCode")]
    [InlineData("qBittorrent.lnk", @"{6D809377-6AF0-444B-8957-A3773F02200E}\qBittorrent\qbittorrent.exe")]
    public void Built_pin_parses_back_to_the_same_name_and_app_id(string name, string appId)
    {
        var (fav, res) = Chunk(name, appId);
        var pin = Assert.Single(TaskbarBlob.Parse(TaskbarBlob.JoinFavorites([fav]), TaskbarBlob.JoinResolve([res])));
        Assert.Equal(name, pin.LinkName);
        Assert.Equal(appId, pin.AppId);
    }

    [Fact]
    public void Order_is_kept_when_joining_several_pins()
    {
        var a = Chunk("Alpha.lnk", "Alpha");
        var b = Chunk("Beta.lnk", "Beta");
        var pins = TaskbarBlob.Parse(TaskbarBlob.JoinFavorites([a.Fav, b.Fav]), TaskbarBlob.JoinResolve([a.Res, b.Res]));
        Assert.Equal(["Alpha.lnk", "Beta.lnk"], pins.Select(p => p.LinkName));
    }

    [Fact]
    public void Built_snapshot_pins_the_programs_in_order_and_skips_missing_ones()
    {
        var preset = new WinDock.Models.Preset
        {
            Items =
            [
                new() { Name = "Notepad", Target = Path.Combine(Environment.SystemDirectory, "notepad.exe") },
                new() { Name = "Ghost", Target = @"C:\does\not\exist.exe" },
                new() { Name = "Calculator", Target = Path.Combine(Environment.SystemDirectory, "calc.exe") }
            ]
        };

        var snapshot = PinBuilder.Build(preset, new WinDock.Models.Snapshot());
        var pins = TaskbarBlob.Parse(
            Convert.FromBase64String(snapshot.Binary["Favorites"]),
            Convert.FromBase64String(snapshot.Binary["FavoritesResolve"]));

        Assert.Equal(["Notepad.lnk", "Calculator.lnk"], pins.Select(p => p.LinkName));
        Assert.Equal(["Notepad.lnk", "Calculator.lnk"], snapshot.Links.Keys);
        Assert.Equal(3, snapshot.Numbers["FavoritesVersion"]);
    }

    [Fact]
    public void Same_program_listed_twice_is_pinned_once_and_always_pinned_come_first()
    {
        var notepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        var calc = Path.Combine(Environment.SystemDirectory, "calc.exe");
        var preset = new WinDock.Models.Preset
        {
            Items =
            [
                new() { Name = "Calculator", Target = calc },
                new() { Name = "Notepad again", Target = notepad.Replace(Environment.SystemDirectory, "%SystemRoot%\\System32") },
                new() { Name = "Notepad", Target = notepad }
            ]
        };
        List<WinDock.Models.PinItem> always = [new() { Name = "Notepad", Target = notepad }];

        var snapshot = PinBuilder.Build(preset, new WinDock.Models.Snapshot(), always);

        Assert.Equal(["Notepad.lnk", "Calculator.lnk"], snapshot.Links.Keys);
    }

    [Fact]
    public void Importing_a_built_snapshot_gives_the_same_programs_back()
    {
        var preset = new WinDock.Models.Preset
        {
            Items = [new() { Name = "Notepad", Target = Path.Combine(Environment.SystemDirectory, "notepad.exe") }]
        };

        var imported = PinBuilder.Import(PinBuilder.Build(preset, new WinDock.Models.Snapshot()));

        Assert.Equal("Notepad", Assert.Single(imported).Name);
    }

    [Fact]
    public void Identical_pin_lists_are_detected_so_no_restart_is_needed()
    {
        var notepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        var calc = Path.Combine(Environment.SystemDirectory, "calc.exe");
        var one = new WinDock.Models.Preset { Items = [new() { Name = "Notepad", Target = notepad }, new() { Name = "Calc", Target = calc }] };
        var swapped = new WinDock.Models.Preset { Items = [new() { Name = "Calc", Target = calc }, new() { Name = "Notepad", Target = notepad }] };
        var empty = new WinDock.Models.Snapshot();

        var a = PinBuilder.Build(one, empty);

        Assert.True(TaskbarService.SamePins(a, PinBuilder.Build(one, empty)));
        Assert.False(TaskbarService.SamePins(a, PinBuilder.Build(swapped, empty)));
    }

    [Fact]
    public void Cache_keys_depend_on_the_pins_and_the_taskbar_geometry()
    {
        var strip = new System.Drawing.Rectangle(0, 1032, 1920, 48);
        var key = TaskbarImages.Key("brave|spotify", strip);

        Assert.Equal(key, TaskbarImages.Key("brave|spotify", strip));
        Assert.NotEqual(key, TaskbarImages.Key("spotify|brave", strip));
        Assert.NotEqual(key, TaskbarImages.Key("brave|spotify", new System.Drawing.Rectangle(0, 1032, 2560, 48)));
    }

    [Fact]
    public void Favorites_blob_ends_with_the_terminator()
    {
        var (fav, _) = Chunk("Brave.lnk", "Brave");
        Assert.Equal(0xFF, TaskbarBlob.JoinFavorites([fav])[^1]);
    }
}
