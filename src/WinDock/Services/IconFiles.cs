using System.Windows.Media.Imaging;
using WinDock.Models;

namespace WinDock.Services;

public static class IconFiles
{
    static readonly string Dir = Path.Combine(Settings.Dir, "icons");

    public static string? FromParsingName(string key, string parsingName) =>
        Ensure(key, () => ShellIcons.Render(parsingName, 256));

    public static string? FromImageFile(string key, string imagePath) =>
        Ensure(key, () =>
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(imagePath);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception e) when (e is IOException or NotSupportedException or UriFormatException) { return null; }
        });

    static string? Ensure(string key, Func<BitmapSource?> source)
    {
        var path = Path.Combine(Dir, Sanitize(key) + ".ico");
        if (File.Exists(path)) return path;
        if (source() is not { } bitmap) return null;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);

        Directory.CreateDirectory(Dir);
        using var ico = new MemoryStream();
        using (var w = new BinaryWriter(ico, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)1);
            w.Write((byte)(bitmap.PixelWidth >= 256 ? 0 : bitmap.PixelWidth));
            w.Write((byte)(bitmap.PixelHeight >= 256 ? 0 : bitmap.PixelHeight));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write((uint)png.Length);
            w.Write(22u);
            w.Write(png.ToArray());
        }
        File.WriteAllBytes(path, ico.ToArray());
        return path;
    }

    static string Sanitize(string key) =>
        string.Concat(key.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '!' ? '_' : c));
}
