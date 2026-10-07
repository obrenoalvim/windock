namespace WinDock.Services;

public sealed record StartApp(string Name, string Path);

public static class StartMenu
{
    static readonly string[] Noise = ["uninstall", "desinstal", "readme", "license", "documentation", "documenta", "manual", "help", "ajuda", "website", "release notes"];

    public static List<StartApp> Scan()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        };

        var apps = new Dictionary<string, StartApp>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (Noise.Any(n => name.Contains(n, StringComparison.CurrentCultureIgnoreCase))) continue;
                apps[name] = new StartApp(name, file);
            }
        }
        return apps.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
