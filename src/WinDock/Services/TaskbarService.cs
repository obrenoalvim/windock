using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using WinDock.Models;

namespace WinDock.Services;

public sealed class TaskbarService
{
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband";
    static readonly string[] BinaryValues = ["Favorites", "FavoritesResolve"];
    static readonly string[] NumberValues = ["FavoritesChanges", "FavoritesRemovedChanges", "FavoritesVersion"];

    public static readonly string LinksDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

    public static string BackupPath { get; } = Path.Combine(Settings.Dir, "backup", "original.json");

    public Snapshot Capture()
    {
        var snapshot = new Snapshot { CapturedAt = DateTime.Now };
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        foreach (var name in BinaryValues)
            if (key?.GetValue(name) is byte[] bytes) snapshot.Binary[name] = Convert.ToBase64String(bytes);
        foreach (var name in NumberValues)
            if (key?.GetValue(name) is int number) snapshot.Numbers[name] = number;
        if (Directory.Exists(LinksDir))
            foreach (var file in Directory.GetFiles(LinksDir, "*.lnk"))
                snapshot.Links[Path.GetFileName(file)] = Convert.ToBase64String(File.ReadAllBytes(file));
        return snapshot;
    }

    public void BackupOnce()
    {
        if (File.Exists(BackupPath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(BackupPath)!);
        File.WriteAllText(BackupPath, JsonSerializer.Serialize(Capture(), Json.Options));
    }

    public Snapshot? LoadBackup() =>
        File.Exists(BackupPath) ? JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(BackupPath), Json.Options) : null;

    public void Apply(Snapshot snapshot)
    {
        var previous = Capture();
        var cover = TaskbarCover.Begin(Signature(previous), Signature(snapshot));
        var ticket = cover?.Generation ?? 0;
        try
        {
            try
            {
                Write(snapshot);
            }
            catch
            {
                try { Write(previous); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                throw;
            }
            KillShell();
            EnsureShell();
        }
        finally
        {
            if (cover is not null) _ = Task.Run(() => cover.Release(ticket));
        }
    }

    static List<string> Ids(Snapshot s) =>
        s.Binary.TryGetValue("Favorites", out var fav) && s.Binary.TryGetValue("FavoritesResolve", out var res)
            ? TaskbarBlob.Parse(Convert.FromBase64String(fav), Convert.FromBase64String(res))
                .Select(p => (p.AppId ?? p.LinkName ?? p.Name).ToLowerInvariant()).ToList()
            : [];

    public static bool SamePins(Snapshot a, Snapshot b)
    {
        var left = Ids(a);
        return left.Count > 0 && left.SequenceEqual(Ids(b));
    }

    static string? Signature(Snapshot s)
    {
        var ids = Ids(s);
        return ids.Count == 0 ? null : string.Join('|', ids);
    }

    static void EnsureShell()
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(500);
        while (DateTime.UtcNow < deadline)
        {
            var shells = Process.GetProcessesByName("explorer");
            foreach (var p in shells) p.Dispose();
            if (shells.Length > 0) return;
            Thread.Sleep(25);
        }
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    static void Write(Snapshot snapshot)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
        {
            foreach (var name in BinaryValues)
            {
                if (snapshot.Binary.TryGetValue(name, out var b64))
                    key.SetValue(name, Convert.FromBase64String(b64), RegistryValueKind.Binary);
                else
                    key.DeleteValue(name, throwOnMissingValue: false);
            }
            foreach (var name in NumberValues)
            {
                if (snapshot.Numbers.TryGetValue(name, out var number))
                    key.SetValue(name, number, RegistryValueKind.DWord);
                else
                    key.DeleteValue(name, throwOnMissingValue: false);
            }
        }

        Directory.CreateDirectory(LinksDir);
        foreach (var file in Directory.GetFiles(LinksDir, "*.lnk")) File.Delete(file);
        foreach (var (name, b64) in snapshot.Links)
            File.WriteAllBytes(Path.Combine(LinksDir, Path.GetFileName(name)), Convert.FromBase64String(b64));
    }

    static void KillShell()
    {
        foreach (var p in Process.GetProcessesByName("explorer"))
        {
            Kill(p);
            p.Dispose();
        }
    }

    static void Kill(Process p)
    {
        try { p.Kill(); p.WaitForExit(3000); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }
}
