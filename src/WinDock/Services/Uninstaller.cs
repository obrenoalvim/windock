using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace WinDock.Services;

public sealed record InstalledApp(string Name, string? Publisher, string? Location, string? Command = null, string? QuietCommand = null,
    RegistryHive Hive = RegistryHive.CurrentUser, RegistryView View = RegistryView.Default, string? Id = null, string? Package = null, string? IconFolder = null)
{
    public bool IsStore => Package is not null;
}

public static partial class Uninstaller
{
    const string UninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    const int ElevationRequired = 740;
    const int UserCancelled = 1223;
    static readonly TimeSpan HandoffWindow = TimeSpan.FromSeconds(10);
    static readonly TimeSpan HandoffLimit = TimeSpan.FromMinutes(10);
    static readonly TimeSpan RunLimit = TimeSpan.FromMinutes(30);
    static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(15);
    static readonly TimeSpan Poll = TimeSpan.FromSeconds(2);

    [GeneratedRegex(@"\{[0-9A-Fa-f\-]{36}\}")]
    private static partial Regex ProductCode();

    [GeneratedRegex(@"steam://uninstall/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SteamUninstall();

    [GeneratedRegex(@"^unins\d{3}\.exe$", RegexOptions.IgnoreCase)]
    private static partial Regex InnoUninstaller();

    public static List<InstalledApp> Installed()
    {
        var apps = new List<InstalledApp>();
        Read(RegistryHive.LocalMachine, RegistryView.Registry64, apps);
        Read(RegistryHive.LocalMachine, RegistryView.Registry32, apps);
        Read(RegistryHive.CurrentUser, RegistryView.Default, apps);
        return apps;
    }

    static void Read(RegistryHive hive, RegistryView view, List<InstalledApp> apps)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var list = root.OpenSubKey(UninstallPath);
        if (list is null) return;
        foreach (var id in list.GetSubKeyNames())
        {
            using var key = list.OpenSubKey(id);
            if (key?.GetValue("DisplayName") is not string name || name.Trim().Length == 0) continue;
            if (key.GetValue("SystemComponent") is 1 || key.GetValue("ParentKeyName") is string) continue;
            var command = key.GetValue("UninstallString") as string;
            var quiet = key.GetValue("QuietUninstallString") as string;
            if (string.IsNullOrWhiteSpace(command) && string.IsNullOrWhiteSpace(quiet)) continue;
            if (apps.Any(a => a.Id == id && a.Hive == hive)) continue;
            var location = Specific(key.GetValue("InstallLocation") as string);
            var iconFolder = Specific(Path.GetDirectoryName(IconFile(key.GetValue("DisplayIcon") as string)));
            apps.Add(new InstalledApp(name.Trim(), (key.GetValue("Publisher") as string)?.Trim(), location, command, quiet, hive, view, id, IconFolder: iconFolder));
        }
    }

    static string? IconFile(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        var file = icon.Split(',')[0].Trim().Trim('"');
        return file.Length == 0 ? null : Environment.ExpandEnvironmentVariables(file);
    }

    public static InstalledApp? Find(string? path, string? appId, string? name, IReadOnlyList<InstalledApp> apps)
    {
        if (path is not null && Is(path, ".lnk"))
        {
            name ??= Path.GetFileNameWithoutExtension(path);
            appId ??= ShellLinks.ReadAppId(path);
            path = ShellLinks.ReadTarget(path);
        }
        else if (path is not null && Is(path, ".url"))
        {
            name ??= Path.GetFileNameWithoutExtension(path);
            if (SteamGame(path) is { } game && apps.FirstOrDefault(a => a.Id == "Steam App " + game) is { } steam) return steam;
            path = null;
        }
        else if (path is not null && Directory.Exists(path))
            return apps.FirstOrDefault(a => a.Location is { } location && location.Equals(Full(path), StringComparison.OrdinalIgnoreCase));
        else if (path is not null && File.Exists(path) && !Is(path, ".exe")) return null;
        if (StoreFullName(path) is { } fullName) return StorePackage(new PackageManager().FindPackageForUser("", fullName));
        if (appId?.Contains('!') == true)
            return StorePackage(new PackageManager().FindPackagesForUser("", appId.Split('!')[0]).FirstOrDefault());
        return Match(apps, path, name);
    }

    static bool Is(string path, string extension) => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);

    static string? SteamGame(string url)
    {
        const string prefix = "URL=steam://rungameid/";
        try
        {
            var line = File.ReadLines(url).FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            var id = line?[prefix.Length..].Trim();
            return id is { Length: > 0 } && id.All(char.IsDigit) ? id : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public static string? PortableFolder(string path, IReadOnlyList<InstalledApp> apps)
    {
        var exe = Is(path, ".lnk") ? ShellLinks.ReadTarget(path) : path;
        if (exe is null || !Is(exe, ".exe") || !File.Exists(exe)) return null;
        InstalledApp[] names = [new(Path.GetFileNameWithoutExtension(path), null, null), new(Path.GetFileNameWithoutExtension(exe), null, null)];
        string? best = null;
        for (var dir = Path.GetDirectoryName(Full(exe)); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var folder = Path.GetFileName(dir);
            if (folder.Length == 0 || folder.StartsWith('.') || Specific(dir) is null) break;
            if (apps.Any(a => a.Location is { } location && Under(location, dir))) break;
            if (names.Any(n => Related(folder, n))) best = dir;
        }
        return best;
    }

    public static List<string> Remnants(string folder, string dropped)
    {
        var found = new List<string> { folder };
        if (!Under(Full(dropped), folder)) found.Add(dropped);
        found.AddRange(Shortcuts(folder).Where(s => !found.Contains(s, StringComparer.OrdinalIgnoreCase)));
        return found;
    }

    public static bool Trashable(string? path) => path is not null && (File.Exists(path) || Directory.Exists(path)) && Specific(path) is not null;

    public static InstalledApp? Match(IReadOnlyList<InstalledApp> apps, string? path, string? name)
    {
        if (path is { Length: > 0 })
        {
            var full = Full(path);
            var byFolder = apps.Where(a => a.Location is { } location && Under(full, location)).MaxBy(a => a.Location!.Length);
            if (byFolder is not null) return byFolder;
            var folder = Path.GetDirectoryName(full);
            var byIcon = apps.FirstOrDefault(a => a.IconFolder is { } icon && icon.Equals(folder, StringComparison.OrdinalIgnoreCase));
            if (byIcon is not null) return byIcon;
        }
        var key = Normalize(name);
        if (key.Length < 3) return null;
        return apps.FirstOrDefault(a => Normalize(a.Name) == key)
            ?? apps.Where(a => Normalize(a.Name).StartsWith(key, StringComparison.Ordinal)).MinBy(a => a.Name.Length);
    }

    static string? StoreFullName(string? path)
    {
        if (path is null) return null;
        var marker = Path.DirectorySeparatorChar + "WindowsApps" + Path.DirectorySeparatorChar;
        var at = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;
        var rest = path[(at + marker.Length)..];
        var end = rest.IndexOf(Path.DirectorySeparatorChar);
        return end > 0 ? rest[..end] : rest;
    }

    static InstalledApp? StorePackage(Windows.ApplicationModel.Package? package)
    {
        if (package is null || package.IsFramework || package.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.System) return null;
        return new InstalledApp(package.DisplayName, package.PublisherDisplayName, null, Package: package.Id.FullName);
    }

    public static List<string> Leftovers(InstalledApp app, IReadOnlyList<InstalledApp> apps)
    {
        if (app.IsStore) return [];
        var others = apps.Where(a => a != app && a.Location is not null).Select(a => a.Location!).ToList();
        var found = new List<string>();

        void Add(string candidate)
        {
            if (Specific(candidate) is not { } dir || !Directory.Exists(dir)) return;
            if (found.Any(f => Under(dir, f)) || others.Any(o => Under(o, dir))) return;
            found.RemoveAll(f => Under(f, dir));
            found.Add(dir);
        }

        if (app.Location is { } location && Related(Path.GetFileName(location), app)) Add(location);
        foreach (var root in DataRoots())
        foreach (var name in Names(app))
            Add(Path.Combine(root, name));
        if (app.Location is { } target) found.AddRange(Shortcuts(target).Where(s => !found.Any(f => Under(s, f))));
        return found;
    }

    static IEnumerable<string> DataRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return [
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            local,
            Path.Combine(local, "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        ];
    }

    static IEnumerable<string> Names(InstalledApp app)
    {
        var names = new List<string> { app.Name };
        if (app.Location is { } location && Related(Path.GetFileName(location), app)) names.Add(Path.GetFileName(location));
        if (app.Publisher is { Length: > 0 } publisher) names.AddRange(names.ToList().Select(n => Path.Combine(publisher, n)));
        var bad = Path.GetInvalidPathChars().Append(':').ToArray();
        return names.Where(n => n.Length >= 3 && n.IndexOfAny(bad) < 0).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    static IEnumerable<string> Shortcuts(string location)
    {
        var menus = new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms }
            .Select(Environment.GetFolderPath).Where(Directory.Exists)
            .SelectMany(d => SafeFiles(d, true));
        var desktops = new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory }
            .Select(Environment.GetFolderPath).Where(Directory.Exists)
            .SelectMany(d => SafeFiles(d, false));
        return menus.Concat(desktops).Where(lnk => ShellLinks.ReadTarget(lnk) is { } target && Under(Full(target), location));
    }

    static IEnumerable<string> SafeFiles(string dir, bool recursive)
    {
        try { return Directory.GetFiles(dir, "*.lnk", new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true }); }
        catch (IOException) { return []; }
    }

    public static bool Related(string folder, InstalledApp app)
    {
        var dir = Normalize(folder);
        if (dir.Length < 3) return false;
        var name = Normalize(app.Name);
        var first = Normalize(app.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        return name.Contains(dir) || dir.Contains(name) || first.Length >= 3 && dir.Contains(first)
               || app.Publisher is { } publisher && Normalize(publisher) is { Length: >= 3 } p && dir.Contains(p);
    }

    public static string? Specific(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string full;
        try { full = Full(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
        if (!Path.IsPathFullyQualified(full) || Path.GetPathRoot(full)?.TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase) != false) return null;
        if (Under(full, Environment.GetFolderPath(Environment.SpecialFolder.Windows))) return null;
        if (full.Contains(@"\Package Cache\", StringComparison.OrdinalIgnoreCase) || full.Contains(@"\WindowsApps", StringComparison.OrdinalIgnoreCase)) return null;
        return Protected().Any(p => Under(p, full)) ? null : full;
    }

    static IEnumerable<string> Protected()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[]
        {
            Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonProgramFiles,
            Environment.SpecialFolder.CommonProgramFilesX86, Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.Programs,
            Environment.SpecialFolder.CommonPrograms, Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu,
            Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos
        }.Select(Environment.GetFolderPath)
            .Append(Path.Combine(local, "Programs"))
            .Append(Launcher.Downloads())
            .Append(Path.GetTempPath())
            .Where(p => p.Length > 0)
            .Select(Full);
    }

    static string Full(string path) => Path.GetFullPath(path).TrimEnd('\\');

    static bool Under(string path, string folder) =>
        path.Equals(folder, StringComparison.OrdinalIgnoreCase) || path.StartsWith(folder.TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase);

    static string Normalize(string? text) => new((text ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static (string File, string Args, bool Silent)? Command(InstalledApp app)
    {
        var raw = app.Command ?? "";
        if (raw.Contains("msiexec", StringComparison.OrdinalIgnoreCase) && ProductCode().Match(raw) is { Success: true } code)
            return ("msiexec.exe", $"/x {code.Value} /passive /norestart", true);
        if (!string.IsNullOrWhiteSpace(app.QuietCommand) && Split(app.QuietCommand) is (var quietFile, var quietArgs)) return (quietFile, quietArgs, true);
        if (Split(raw) is not (var file, var args)) return null;
        if (InnoUninstaller().IsMatch(Path.GetFileName(file)) && !args.Contains("SILENT", StringComparison.OrdinalIgnoreCase))
            return (file, (args + " /SILENT /SUPPRESSMSGBOXES /NORESTART").Trim(), true);
        return (file, args, args.Contains("silent", StringComparison.OrdinalIgnoreCase) || args.Contains("/S", StringComparison.Ordinal));
    }

    public static bool Silent(InstalledApp app) => app.IsStore || Command(app) is { Silent: true };

    public static (string File, string Args)? Split(string command)
    {
        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        if (text.Length == 0) return null;
        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            return close < 0 ? (text.Trim('"'), "") : (text[1..close], text[(close + 1)..].Trim());
        }
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe < 0)
        {
            var space = text.IndexOf(' ');
            return space < 0 ? (text, "") : (text[..space], text[(space + 1)..].Trim());
        }
        return (text[..(exe + 4)], text[(exe + 4)..].Trim());
    }

    public static async Task<bool> Remove(InstalledApp app, CancellationToken token = default)
    {
        if (app.Package is { } fullName)
        {
            try
            {
                await new PackageManager().RemovePackageAsync(fullName).AsTask(token);
                return true;
            }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException) { return false; }
        }
        if (Command(app) is not (var file, var args, _)) return false;
        if (Broken(file)) return true;
        var hadFolder = app.Location is { } location && Directory.Exists(location);
        var started = DateTime.Now;
        using var process = Start(file, args);
        if (process is null) return false;
        while (!Gone(app, hadFolder))
        {
            if (DateTime.Now - started > WaitLimit(process, started)) return false;
            await Task.Delay(Poll, token);
        }
        if (!process.HasExited)
        {
            try { await process.WaitForExitAsync(token).WaitAsync(ExitGrace, token); }
            catch (TimeoutException) { }
        }
        return true;
    }

    public static bool Broken(string uninstaller) =>
        Path.IsPathFullyQualified(uninstaller)
        && Path.GetPathRoot(uninstaller) is { } drive && Directory.Exists(drive)
        && !File.Exists(uninstaller);

    static TimeSpan WaitLimit(Process process, DateTime started)
    {
        if (!process.HasExited) return RunLimit;
        var ran = process.ExitTime - started;
        return ran < HandoffWindow ? HandoffLimit : ran + ExitGrace;
    }

    public static bool Gone(InstalledApp app, bool hadFolder)
    {
        if (!StillInstalled(app)) return true;
        if (SteamId(app) is { } id) return GameLibraries.SteamInstallPath(id) is null;
        return hadFolder && !Directory.Exists(app.Location!);
    }

    static string? SteamId(InstalledApp app) =>
        SteamUninstall().Match(app.Command ?? "") is { Success: true } match ? match.Groups[1].Value : null;

    static Process? Start(string file, string args)
    {
        var info = new ProcessStartInfo(file, args) { UseShellExecute = true };
        try { return Process.Start(info); }
        catch (Win32Exception e) when (e.NativeErrorCode == ElevationRequired)
        {
            info.Verb = "runas";
            try { return Process.Start(info); }
            catch (Win32Exception) { return null; }
        }
        catch (Win32Exception) { return null; }
    }

    public static bool StillInstalled(InstalledApp app)
    {
        if (app.Id is null) return false;
        using var root = RegistryKey.OpenBaseKey(app.Hive, app.View);
        using var key = root.OpenSubKey(UninstallPath + "\\" + app.Id);
        return key?.GetValue("DisplayName") is string name && name.Trim().Length > 0;
    }

    public static async Task<int> Clean(IReadOnlyList<string> paths, InstalledApp? app = null)
    {
        var stuck = paths.Where(p => !Recycle(p)).ToList();
        var orphan = app is not null && !Forget(app) ? app : null;
        if (stuck.Count > 0 || orphan is not null) await Elevated(stuck, orphan);
        return stuck.Count(Exists) + (orphan is not null && StillInstalled(orphan) ? 1 : 0);
    }

    static bool Forget(InstalledApp app)
    {
        if (!StillInstalled(app)) return true;
        try
        {
            using var root = RegistryKey.OpenBaseKey(app.Hive, app.View);
            root.DeleteSubKeyTree(UninstallPath + "\\" + app.Id, false);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        return !StillInstalled(app);
    }

    static string RegistryPath(InstalledApp app)
    {
        var machine = app.Hive == RegistryHive.LocalMachine;
        var root = machine ? @"HKEY_LOCAL_MACHINE\SOFTWARE\" : @"HKEY_CURRENT_USER\Software\";
        var node = machine && app.View == RegistryView.Registry32 ? @"WOW6432Node\" : "";
        return root + node + @"Microsoft\Windows\CurrentVersion\Uninstall\" + app.Id;
    }

    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    static bool Recycle(string path)
    {
        try
        {
            if (Directory.Exists(path)) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
            else if (File.Exists(path)) FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return !Exists(path);
    }

    static async Task Elevated(IEnumerable<string> paths, InstalledApp? orphan)
    {
        var script = new StringBuilder("Add-Type -AssemblyName Microsoft.VisualBasic;");
        if (orphan is not null)
            script.Append($"Remove-Item -LiteralPath '{("Registry::" + RegistryPath(orphan)).Replace("'", "''")}' -Recurse -Force -ErrorAction SilentlyContinue;");
        foreach (var path in paths)
        {
            var quoted = "'" + path.Replace("'", "''") + "'";
            script.Append($"if (Test-Path -LiteralPath {quoted} -PathType Container) {{ [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory({quoted}, 'OnlyErrorDialogs', 'SendToRecycleBin') }} ");
            script.Append($"elseif (Test-Path -LiteralPath {quoted}) {{ [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile({quoted}, 'OnlyErrorDialogs', 'SendToRecycleBin') }};");
        }
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script.ToString()));
        var info = new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -EncodedCommand {encoded}") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        try
        {
            using var process = Process.Start(info);
            if (process is not null) await process.WaitForExitAsync();
        }
        catch (Win32Exception e) when (e.NativeErrorCode == UserCancelled) { }
    }
}
