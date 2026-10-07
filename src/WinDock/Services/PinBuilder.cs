using WinDock.Models;

namespace WinDock.Services;

public static class PinBuilder
{
    public static readonly string LibraryDir = Path.Combine(Settings.Dir, "links");

    static readonly string UriHandler = Path.Combine(Environment.SystemDirectory, "rundll32.exe");
    const string UriHandlerSuffix = ".open";

    public static bool TryAdd(HashSet<string> seen, PinItem item)
    {
        var keys = Keys(item);
        if (keys.Any(seen.Contains)) return false;
        foreach (var k in keys) seen.Add(k);
        return true;
    }

    static List<string> Keys(PinItem item)
    {
        if (item.IsRaw) return [(item.AppId is { Length: > 0 } id ? id : item.Name).ToLowerInvariant()];
        if (item.IsLauncher) return [item.AppId!.ToLowerInvariant()];
        if (item.ResolvedTarget is not { } target) return [item.Name.ToLowerInvariant()];

        var isLink = target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
        var exe = LinkTarget(target);
        var appId = item.AppId ?? (isLink ? ShellLinks.ReadAppId(target) : null) ?? TaskbarBlob.AppIdForTarget(exe);
        return [appId.ToLowerInvariant(), exe.ToLowerInvariant()];
    }

    public static Snapshot Build(Preset preset, Snapshot current, IEnumerable<PinItem>? always = null)
    {
        var snapshot = new Snapshot { CapturedAt = DateTime.Now };
        var favorites = new List<byte[]>();
        var resolves = new List<byte[]>();
        var usedShort = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rng = new Random();

        var seen = new HashSet<string>();
        foreach (var item in (always ?? []).Concat(preset.Items))
        {
            if (!item.Exists || !TryAdd(seen, item)) continue;

            if (item.IsRaw)
            {
                favorites.Add(Convert.FromBase64String(item.Favorites!));
                resolves.Add(Convert.FromBase64String(item.Resolve!));
                continue;
            }

            if (item.ResolvedTarget is not { } target || !File.Exists(target)) continue;

            var isLink = target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
            var icon = item.ResolvedIcon is { } i && File.Exists(i) ? i : null;
            var bytes = item.IsUriLauncher
                ? ShellLinks.CreateLauncher(UriHandler, $"url.dll,FileProtocolHandler {item.Args}", icon, item.AppId + UriHandlerSuffix)
                : item.IsLauncher
                    ? ShellLinks.CreateLauncher(target, item.Args!, icon, item.AppId!)
                    : isLink ? File.ReadAllBytes(target) : ShellLinks.CreateForExe(target);
            var linkName = UniqueName(item.Name, usedNames);
            var linkPath = Path.Combine(TaskbarService.LinksDir, linkName);
            var appId = (item.IsUriLauncher ? item.AppId + UriHandlerSuffix : item.AppId)
                ?? (isLink ? ShellLinks.ReadAppId(target) : null)
                ?? TaskbarBlob.AppIdForTarget(LinkTarget(target));

            var (fav, res) = TaskbarBlob.BuildClassic(linkName, linkPath, bytes.Length, appId, usedShort, rng);
            favorites.Add(fav);
            resolves.Add(res);
            snapshot.Links[linkName] = Convert.ToBase64String(bytes);
        }

        snapshot.Binary["Favorites"] = Convert.ToBase64String(TaskbarBlob.JoinFavorites(favorites));
        snapshot.Binary["FavoritesResolve"] = Convert.ToBase64String(TaskbarBlob.JoinResolve(resolves));
        snapshot.Numbers["FavoritesVersion"] = 3;
        snapshot.Numbers["FavoritesChanges"] = current.Numbers.GetValueOrDefault("FavoritesChanges") + 1;
        snapshot.Numbers["FavoritesRemovedChanges"] = current.Numbers.GetValueOrDefault("FavoritesRemovedChanges");
        return snapshot;
    }

    public static List<PinItem> Import(Snapshot current)
    {
        if (!current.Binary.TryGetValue("Favorites", out var fav) || !current.Binary.TryGetValue("FavoritesResolve", out var res))
            return [];

        Directory.CreateDirectory(LibraryDir);
        var items = new List<PinItem>();
        foreach (var pin in TaskbarBlob.Parse(Convert.FromBase64String(fav), Convert.FromBase64String(res)))
        {
            if (pin.LinkName is { } name && current.Links.TryGetValue(name, out var lnk))
            {
                var path = Path.Combine(LibraryDir, name);
                File.WriteAllBytes(path, Convert.FromBase64String(lnk));
                items.Add(new PinItem { Name = pin.Name, Target = path, AppId = pin.AppId });
            }
            else
            {
                items.Add(new PinItem
                {
                    Name = pin.Name,
                    AppId = pin.AppId,
                    Favorites = Convert.ToBase64String(pin.Favorites),
                    Resolve = Convert.ToBase64String(pin.Resolve)
                });
            }
        }
        return items;
    }

    static string LinkTarget(string path) =>
        path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ShellLinks.ReadTarget(path) ?? path : path;

    static string UniqueName(string name, HashSet<string> used)
    {
        var clean = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        if (clean.Length == 0) clean = "App";
        var candidate = clean + ".lnk";
        for (var n = 2; !used.Add(candidate); n++) candidate = $"{clean} ({n}).lnk";
        return candidate;
    }
}
