using WinDock.Models;

namespace WinDock.Services;

public sealed record CatalogEntry(string Name, string Category, string? IconSource, Func<PinItem> Make);

public static class Catalog
{
    public const string Programs = "Programas";
    public const string Store = "Loja";
    public const string Steam = "Steam";
    public const string Epic = "Epic";

    static readonly string Explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    public static List<CatalogEntry> Load()
    {
        var entries = new List<CatalogEntry>();
        Safe(() => entries.AddRange(StartMenu.Scan().Select(a =>
            new CatalogEntry(a.Name, Programs, a.Path, () => new PinItem { Name = a.Name, Target = a.Path }))));
        Safe(() => entries.AddRange(AppsFolder.Store().Select(a =>
            new CatalogEntry(a.Name, Store, @"shell:AppsFolder\" + a.AppId, () => StoreItem(a)))));
        Safe(() => entries.AddRange(GameLibraries.Steam().Select(g =>
            new CatalogEntry(g.Name, Steam, g.IconPath ?? SteamExe(), () => SteamItem(g)))));
        Safe(() => entries.AddRange(GameLibraries.Epic().Select(g =>
            new CatalogEntry(g.Name, Epic, g.Executable ?? Explorer, () => EpicItem(g)))));
        return entries;
    }

    static PinItem StoreItem(StoreApp app) => new()
    {
        Name = app.Name,
        Target = Explorer,
        Args = @"shell:AppsFolder\" + app.AppId,
        AppId = app.AppId,
        Icon = IconFiles.FromParsingName("store-" + app.AppId, @"shell:AppsFolder\" + app.AppId)
    };

    static PinItem SteamItem(SteamGame game)
    {
        var icon = game.IconPath is null ? SteamExe()
            : game.IconIsReady ? game.IconPath
            : IconFiles.FromImageFile("steam-" + game.Id, game.IconPath);
        return new PinItem
        {
            Name = game.Name,
            Target = Explorer,
            Args = $"steam://rungameid/{game.Id}",
            AppId = $"WinDock.Steam.{game.Id}",
            Icon = icon
        };
    }

    static PinItem EpicItem(EpicGame game) => new()
    {
        Name = game.Name,
        Target = Explorer,
        Args = $"com.epicgames.launcher://apps/{game.Namespace}%3A{game.ItemId}%3A{game.AppName}?action=launch&silent=true",
        AppId = $"WinDock.Epic.{game.AppName}",
        Icon = game.Executable is null ? null : IconFiles.FromParsingName("epic-" + game.AppName, game.Executable)
    };

    static string? SteamExe() =>
        GameLibraries.SteamRoot() is { } root && File.Exists(Path.Combine(root, "steam.exe")) ? Path.Combine(root, "steam.exe") : null;

    static void Safe(Action load)
    {
        try { load(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
