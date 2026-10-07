using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WinDock.Services;

public sealed record SteamGame(string Id, string Name, string? IconPath, bool IconIsReady);

public sealed record EpicGame(string Name, string AppName, string Namespace, string ItemId, string? Executable);

public static partial class GameLibraries
{
    static readonly string[] SteamTools =
        ["Steamworks Common Redistributables", "Proton", "Steam Linux Runtime", "Steam Client", "Steamworks Shared"];

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPath();

    [GeneratedRegex("\"appid\"\\s+\"(\\d+)\"")]
    private static partial Regex AppId();

    [GeneratedRegex("\"name\"\\s+\"([^\"]*)\"")]
    private static partial Regex GameName();

    [GeneratedRegex("\"StateFlags\"\\s+\"(\\d+)\"")]
    private static partial Regex StateFlags();

    [GeneratedRegex("^[0-9a-f]{40}\\.jpg$")]
    private static partial Regex IconHash();

    public static IEnumerable<string> ParseSteamLibraries(string vdf) =>
        LibraryPath().Matches(vdf).Select(m => m.Groups[1].Value.Replace(@"\\", @"\"));

    public static (string Id, string Name)? ParseSteamManifest(string acf)
    {
        var id = AppId().Match(acf);
        var name = GameName().Match(acf);
        var flags = StateFlags().Match(acf);
        if (!id.Success || !name.Success) return null;
        if (flags.Success && (int.Parse(flags.Groups[1].Value) & 4) == 0) return null;
        if (SteamTools.Any(t => name.Groups[1].Value.StartsWith(t, StringComparison.OrdinalIgnoreCase))) return null;
        return (id.Groups[1].Value, name.Groups[1].Value);
    }

    public static string? SteamRoot()
    {
        var path = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
        return path is null ? null : Path.GetFullPath(path);
    }

    public static List<SteamGame> Steam()
    {
        var games = new Dictionary<string, SteamGame>();
        if (SteamRoot() is not { } root) return [];

        var libraries = new List<string> { root };
        var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf)) libraries.AddRange(ParseSteamLibraries(File.ReadAllText(vdf)));

        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var apps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var file in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                if (ParseSteamManifest(File.ReadAllText(file)) is not { } game) continue;
                var (icon, ready) = SteamIcon(root, game.Id);
                games[game.Id] = new SteamGame(game.Id, game.Name, icon, ready);
            }
        }
        return games.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static (string? Path, bool Ready) SteamIcon(string root, string id)
    {
        var folder = Path.Combine(root, "appcache", "librarycache", id);
        if (!Directory.Exists(folder)) return (null, false);

        var jpg = Directory.EnumerateFiles(folder, "*.jpg").FirstOrDefault(f => IconHash().IsMatch(Path.GetFileName(f)));
        if (jpg is null) return (null, false);

        var ico = Path.Combine(root, "steam", "games", Path.GetFileNameWithoutExtension(jpg) + ".ico");
        return File.Exists(ico) ? (ico, true) : (jpg, false);
    }

    public static EpicGame? ParseEpicManifest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            string Text(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

            if (r.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) return null;
            var name = Text("DisplayName");
            var app = Text("AppName");
            if (name.Length == 0 || app.Length == 0 || Text("LaunchExecutable").Length == 0) return null;

            var exe = Path.Combine(Text("InstallLocation"), Text("LaunchExecutable")).Replace('/', '\\');
            return new EpicGame(name, app, Text("CatalogNamespace"), Text("CatalogItemId"), File.Exists(exe) ? exe : null);
        }
        catch (JsonException) { return null; }
    }

    public static List<EpicGame> Epic()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(folder)) return [];

        return Directory.EnumerateFiles(folder, "*.item")
            .Select(f => ParseEpicManifest(File.ReadAllText(f)))
            .OfType<EpicGame>()
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
