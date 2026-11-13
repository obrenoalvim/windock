using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WinDock.Services;

public sealed record SteamGame(string Id, string Name, string? IconPath, bool IconIsReady);

public sealed record EpicGame(string Name, string AppName, string Namespace, string ItemId, string? Executable);

public sealed record LibraryGame(string Id, string Name, string? Executable, string? Folder);

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

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"")]
    private static partial Regex InstallDir();

    public static string? ParseSteamInstallDir(string acf) =>
        InstallDir().Match(acf) is { Success: true } m ? m.Groups[1].Value.Replace(@"\\", @"\") : null;

    public static string? SteamInstallPath(string id)
    {
        if (SteamRoot() is not { } root) return null;
        var libraries = new List<string> { root };
        var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        try
        {
            if (File.Exists(vdf)) libraries.AddRange(ParseSteamLibraries(File.ReadAllText(vdf)));
            foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var manifest = Path.Combine(library, "steamapps", $"appmanifest_{id}.acf");
                if (File.Exists(manifest) && ParseSteamInstallDir(File.ReadAllText(manifest)) is { } dir)
                    return Path.Combine(library, "steamapps", "common", dir);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return null;
    }

    public static string? EpicInstallPath(string appName) =>
        Epic().FirstOrDefault(g => g.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase))?.Executable is { } exe
            ? Path.GetDirectoryName(exe)
            : null;

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

    static readonly string[] LauncherNames =
        ["EA app", "EA Desktop", "Origin", "Battle.net", "Blizzard Battle.net", "EA Anticheat", "EA Javelin", "Redistributable", "Visual C++", "Ubisoft Connect"];

    public static LibraryGame? ParseUninstall(string? name, string? publisher, string? icon, string? location, string wantedPublisher)
    {
        if (string.IsNullOrWhiteSpace(name) || publisher is null || !publisher.Contains(wantedPublisher, StringComparison.OrdinalIgnoreCase)) return null;
        if (LauncherNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase))) return null;
        var exe = IconExecutable(icon);
        return exe is null ? null : new LibraryGame(name, name, exe, string.IsNullOrWhiteSpace(location) ? Path.GetDirectoryName(exe) : location);
    }

    public static string? IconExecutable(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        var path = icon.Trim().Trim('"');
        var comma = path.LastIndexOf(',');
        if (comma > 0 && int.TryParse(path[(comma + 1)..], out _)) path = path[..comma].Trim().Trim('"');
        return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
    }

    public static LibraryGame? ParseGog(string id, string? name, string? exe, string? folder) =>
        string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exe) || !File.Exists(exe) ? null : new LibraryGame(id, name, exe, folder);

    public static LibraryGame? ParseUbisoft(string id, string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir)) return null;
        var folder = installDir.Replace('/', '\\').TrimEnd('\\');
        var name = Path.GetFileName(folder);
        if (name.Length == 0 || !Directory.Exists(folder)) return null;
        var exe = Directory.EnumerateFiles(folder, "*.exe").FirstOrDefault(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase));
        return new LibraryGame(id, name, exe, folder);
    }

    public static List<LibraryGame> Gog() => Registered(@"SOFTWARE\WOW6432Node\GOG.com\Games", (id, key) =>
        ParseGog(id, key.GetValue("gameName") as string, key.GetValue("exe") as string, key.GetValue("path") as string));

    public static List<LibraryGame> Ubisoft() => Registered(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", (id, key) =>
        ParseUbisoft(id, key.GetValue("InstallDir") as string));

    public static string? UbisoftInstallPath(string id) => Ubisoft().FirstOrDefault(g => g.Id == id)?.Folder;

    public static List<LibraryGame> Published(string publisher)
    {
        var games = new List<LibraryGame>();
        foreach (var root in new[] { @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" })
            games.AddRange(Registered(root, (_, key) => ParseUninstall(
                key.GetValue("DisplayName") as string, key.GetValue("Publisher") as string,
                key.GetValue("DisplayIcon") as string, key.GetValue("InstallLocation") as string, publisher)));
        return [.. games.DistinctBy(g => g.Name, StringComparer.OrdinalIgnoreCase)];
    }

    static List<LibraryGame> Registered(string path, Func<string, RegistryKey, LibraryGame?> read)
    {
        var games = new List<LibraryGame>();
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(path);
            foreach (var id in root?.GetSubKeyNames() ?? [])
            {
                using var key = root!.OpenSubKey(id);
                if (key is not null && read(id, key) is { } game) games.Add(game);
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return games;
    }
}
