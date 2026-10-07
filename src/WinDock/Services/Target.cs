using System.Diagnostics;

namespace WinDock.Services;

public static class Target
{
    public static bool IsUri(string target) =>
        !Path.IsPathRooted(target)
        && Uri.TryCreate(target, UriKind.Absolute, out var u)
        && u.Scheme.Length > 1
        && !u.IsFile;

    public static string? ResolvePath(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(target);
        if (Path.IsPathRooted(expanded)) return File.Exists(expanded) || Directory.Exists(expanded) ? expanded : null;
        var exts = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';');
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var direct = Path.Combine(dir, expanded);
            if (File.Exists(direct)) return direct;
            foreach (var ext in exts)
            {
                var withExt = direct + ext;
                if (File.Exists(withExt)) return withExt;
            }
        }
        return null;
    }

    public static bool IsAvailable(string target) => IsUri(target) || ResolvePath(target) is not null;

    public static bool Launch(string target, string? args)
    {
        try
        {
            var path = IsUri(target) ? target : ResolvePath(target);
            if (path is null) return false;
            Process.Start(new ProcessStartInfo(path) { Arguments = args ?? "", UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }
}
