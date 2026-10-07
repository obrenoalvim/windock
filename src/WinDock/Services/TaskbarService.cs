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

    static readonly string LinksDir = Path.Combine(
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
        StopExplorer();

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

        StopExplorer();
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }

    static void StopExplorer()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); p.WaitForExit(3000); }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                finally { p.Dispose(); }
            }
            Thread.Sleep(700);
        }
    }
}
