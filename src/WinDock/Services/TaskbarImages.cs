using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using WinDock.Models;

namespace WinDock.Services;

public static class TaskbarImages
{
    static readonly string Dir = Path.Combine(Settings.Dir, "cache");

    public static string Key(string signature, Rectangle strip) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{signature}|{strip.X},{strip.Y},{strip.Width},{strip.Height}")));

    public static void Save(string key, Bitmap image)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            image.Save(Path.Combine(Dir, key + ".png"), ImageFormat.Png);
            Prune();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException) { }
    }

    public static Bitmap? Load(string key)
    {
        var path = Path.Combine(Dir, key + ".png");
        if (!File.Exists(path)) return null;
        try
        {
            using var file = new Bitmap(path);
            return new Bitmap(file);
        }
        catch (Exception e) when (e is IOException or ArgumentException or System.Runtime.InteropServices.ExternalException) { return null; }
    }

    static void Prune()
    {
        foreach (var old in new DirectoryInfo(Dir).GetFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).Skip(60))
            old.Delete();
    }
}
