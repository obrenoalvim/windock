using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WinDock.Services;

public sealed record GameEntry(string Platform, string Id, string Name, bool Installed, string Uri, string? Icon);

public static partial class Games
{
    public const string Steam = "Steam", Epic = "Epic", Gog = "GOG", Ea = "EA", Ubisoft = "Ubisoft", BattleNet = "Battle.net";

    public static readonly string[] Platforms = [Steam, Epic, Gog, Ea, Ubisoft, BattleNet];

    public static string ForKind(string kind) => Platforms.First(p => p.Replace(".", "").Equals(kind, StringComparison.OrdinalIgnoreCase));

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    [GeneratedRegex("\"(\\d{17})\"\\s*\\{[^{}]*\"MostRecent\"\\s*\"1\"")]
    private static partial Regex RecentUser();

    public static string? ParseRecentSteamUser(string loginUsers) =>
        RecentUser().Match(loginUsers) is { Success: true } m ? m.Groups[1].Value : null;

    public static List<(string Id, string Name)> ParseSteamLibrary(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Descendants("game")
                .Select(g => (Id: (string?)g.Element("appID") ?? "", Name: (string?)g.Element("name") ?? ""))
                .Where(g => g.Id.Length > 0 && g.Name.Length > 0)
                .ToList();
        }
        catch (System.Xml.XmlException) { return []; }
    }

    static readonly Dictionary<string, string> LauncherFiles = new()
    {
        [Epic] = @"Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
        [Gog] = @"GOG Galaxy\GalaxyClient.exe",
        [Ea] = @"Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
        [Ubisoft] = @"Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe",
        [BattleNet] = @"Battle.net\Battle.net Launcher.exe"
    };

    public static string? Launcher(string platform)
    {
        if (platform == Steam) return GameLibraries.SteamRoot() is { } root && Path.Combine(root, "steam.exe") is var steam && File.Exists(steam) ? steam : null;
        return new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }
            .Select(f => Path.Combine(Environment.GetFolderPath(f), LauncherFiles[platform]))
            .FirstOrDefault(File.Exists);
    }

    public static List<GameEntry> Installed(string? platform = null)
    {
        var games = new List<GameEntry>();
        foreach (var name in Platforms.Where(p => platform is null || p == platform))
        {
            try { games.AddRange(Read(name)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static IEnumerable<GameEntry> Read(string platform) => platform switch
    {
        Steam => GameLibraries.Steam().Select(g => new GameEntry(Steam, g.Id, g.Name, true, $"steam://rungameid/{g.Id}", g.IconPath)),
        Epic => GameLibraries.Epic().Select(g => new GameEntry(Epic, g.AppName, g.Name, true,
            $"com.epicgames.launcher://apps/{g.Namespace}%3A{g.ItemId}%3A{g.AppName}?action=launch&silent=true", g.Executable)),
        Gog => GameLibraries.Gog().Select(g => new GameEntry(Gog, g.Id, g.Name, true, g.Executable!, g.Executable)),
        Ubisoft => GameLibraries.Ubisoft().Select(g => new GameEntry(Ubisoft, g.Id, g.Name, true, $"uplay://launch/{g.Id}/0", g.Executable)),
        Ea => GameLibraries.Published("Electronic Arts").Select(g => new GameEntry(Ea, g.Id, g.Name, true, g.Executable!, g.Executable)),
        _ => GameLibraries.Published("Blizzard Entertainment").Select(g => new GameEntry(BattleNet, g.Id, g.Name, true, g.Executable!, g.Executable))
    };

    public static async Task<(List<GameEntry> Games, bool Complete)> All()
    {
        var games = Installed(Steam);
        var library = await SteamLibrary();
        if (library is null) return (games, false);
        var have = games.Where(g => g.Platform == Steam).Select(g => g.Id).ToHashSet();
        games.AddRange(library.Where(g => !have.Contains(g.Id)).Select(g => new GameEntry(Steam, g.Id, g.Name, false, $"steam://install/{g.Id}", null)));
        return (games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), true);
    }

    static async Task<List<(string Id, string Name)>?> SteamLibrary()
    {
        try
        {
            if (GameLibraries.SteamRoot() is not { } root) return null;
            var file = Path.Combine(root, "config", "loginusers.vdf");
            if (!File.Exists(file) || ParseRecentSteamUser(await File.ReadAllTextAsync(file)) is not { } user) return null;
            var xml = await Http.GetStringAsync($"https://steamcommunity.com/profiles/{user}/games?tab=all&xml=1");
            var games = ParseSteamLibrary(xml);
            return games.Count > 0 ? games : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException) { return null; }
    }
}
