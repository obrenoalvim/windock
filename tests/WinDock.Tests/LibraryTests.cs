using WinDock.Models;
using WinDock.Services;

namespace WinDock.Tests;

public class LibraryTests
{
    const string Vdf = """
        "libraryfolders"
        {
            "0"
            {
                "path"		"C:\\Program Files (x86)\\Steam"
                "apps" { "1243130" "1091967644" }
            }
            "1"
            {
                "path"		"D:\\Games\\SteamLibrary"
            }
        }
        """;

    [Fact]
    public void Steam_library_folders_are_unescaped()
    {
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\Games\SteamLibrary"], GameLibraries.ParseSteamLibraries(Vdf));
    }

    [Fact]
    public void Installed_steam_game_manifest_is_read()
    {
        var acf = "\"AppState\"\n{\n\t\"appid\"\t\t\"1243130\"\n\t\"name\"\t\t\"Nimbatus - Drone Creator\"\n\t\"StateFlags\"\t\t\"4\"\n}";
        Assert.Equal(("1243130", "Nimbatus - Drone Creator"), GameLibraries.ParseSteamManifest(acf));
    }

    [Theory]
    [InlineData("\"appid\"\t\"1\"\n\"name\"\t\"Half Done\"\n\"StateFlags\"\t\"1026\"")]
    [InlineData("\"appid\"\t\"228980\"\n\"name\"\t\"Steamworks Common Redistributables\"\n\"StateFlags\"\t\"4\"")]
    public void Unfinished_downloads_and_steam_tools_are_skipped(string acf)
    {
        Assert.Null(GameLibraries.ParseSteamManifest(acf));
    }

    [Fact]
    public void Epic_manifest_is_read()
    {
        const string json = """
            {
              "DisplayName": "Rocket League®",
              "AppName": "Sugar",
              "CatalogNamespace": "9773aa1aa54f4f7b80e44bef04986cea",
              "CatalogItemId": "530145df28a24424923f5828cc9031a1",
              "InstallLocation": "C:/Program Files/Epic Games/rocketleague",
              "LaunchExecutable": "Binaries/Win64/Launcher.exe"
            }
            """;

        var game = GameLibraries.ParseEpicManifest(json);

        Assert.Equal("Rocket League®", game?.Name);
        Assert.Equal("Sugar", game?.AppName);
        Assert.Equal("530145df28a24424923f5828cc9031a1", game?.ItemId);
    }

    [Fact]
    public void Epic_manifest_without_an_executable_is_skipped()
    {
        Assert.Null(GameLibraries.ParseEpicManifest("""{ "DisplayName": "DLC", "AppName": "x", "LaunchExecutable": "" }"""));
    }

    [Fact]
    public void Launcher_items_with_different_ids_are_not_duplicates_of_each_other()
    {
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var seen = new HashSet<string>();

        var first = new PinItem { Name = "A", Target = explorer, Args = "steam://rungameid/1", AppId = "WinDock.Steam.1" };
        var second = new PinItem { Name = "B", Target = explorer, Args = "steam://rungameid/2", AppId = "WinDock.Steam.2" };

        Assert.True(PinBuilder.TryAdd(seen, first));
        Assert.True(PinBuilder.TryAdd(seen, second));
        Assert.False(PinBuilder.TryAdd(seen, first));
    }

    [Fact]
    public void Launcher_pin_carries_its_own_app_id_into_the_taskbar_entry()
    {
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var preset = new Preset
        {
            Items = [new PinItem { Name = "My Game", Target = explorer, Args = "steam://rungameid/42", AppId = "WinDock.Steam.42" }]
        };

        var snapshot = PinBuilder.Build(preset, new Snapshot());
        var pin = Assert.Single(TaskbarBlob.Parse(
            Convert.FromBase64String(snapshot.Binary["Favorites"]),
            Convert.FromBase64String(snapshot.Binary["FavoritesResolve"])));

        Assert.Equal("My Game.lnk", pin.LinkName);
        Assert.Equal("WinDock.Steam.42.open", pin.AppId);
        Assert.Equal("WinDock.Steam.42.open", ShellLinks.ReadAppId(WriteTemp(snapshot.Links["My Game.lnk"])));
    }

    static string WriteTemp(string base64)
    {
        var path = Path.Combine(Path.GetTempPath(), $"windock-test-{Guid.NewGuid():N}.lnk");
        File.WriteAllBytes(path, Convert.FromBase64String(base64));
        return path;
    }
}
