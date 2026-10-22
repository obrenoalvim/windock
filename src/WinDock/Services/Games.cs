using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WinDock.Services;

public sealed record GameEntry(string Platform, string Id, string Name, bool Installed, string Uri, string? Icon);

public static partial class Games
{
    public const string Steam = "Steam", Epic = "Epic";

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

    public static string? Launcher(string platform)
    {
        if (platform == Steam) return GameLibraries.SteamRoot() is { } root && Path.Combine(root, "steam.exe") is var steam && File.Exists(steam) ? steam : null;
        return new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }
            .Select(f => Path.Combine(Environment.GetFolderPath(f), @"Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe"))
            .FirstOrDefault(File.Exists);
    }

    public static List<GameEntry> Installed(string? platform = null)
    {
        var games = new List<GameEntry>();
        try
        {
            if (platform != Epic) games.AddRange(GameLibraries.Steam().Select(g => new GameEntry(Steam, g.Id, g.Name, true, $"steam://rungameid/{g.Id}", g.IconPath)));
            if (platform != Steam) games.AddRange(GameLibraries.Epic().Select(g => new GameEntry(Epic, g.AppName, g.Name, true,
                $"com.epicgames.launcher://apps/{g.Namespace}%3A{g.ItemId}%3A{g.AppName}?action=launch&silent=true", g.Executable)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

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
