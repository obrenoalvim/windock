using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinDock.Models;

namespace WinDock.Services;

public sealed class IconService
{
    static readonly string[] ImageExts = [".png", ".jpg", ".jpeg", ".ico", ".bmp"];
    readonly Dictionary<string, ImageSource?> cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? Get(DockItem item)
    {
        var source = item.Icon ?? item.Target;
        if (cache.TryGetValue(source, out var hit)) return hit;
        return cache[source] = Load(source);
    }

    static ImageSource? Load(string source)
    {
        var path = Target.ResolvePath(source);
        if (path is null) return null;
        try
        {
            if (ImageExts.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(path);
                img.DecodePixelWidth = 96;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            var bmp = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
