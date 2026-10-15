using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinDock.Services;

public static class ShellIcons
{
    static readonly Guid ImageFactoryId = new("BCC18B79-BA16-442F-80C4-8A59C30C463B");
    static readonly Guid ItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string?> Names = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential)]
    struct Size
    {
        public int Width, Height;
    }

    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        void GetImage(Size size, int flags, out IntPtr bitmap);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr bc, ref Guid handler, ref Guid iid, out IntPtr result);
        void GetParent(out IntPtr parent);
        void GetDisplayName(int form, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IntPtr other, uint hint, out int order);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out object item);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr handle);

    public static ImageSource? ForPath(string path, int size = 96) => Get(path, size);

    public static ImageSource? ForAppId(string appId, int size = 96) => Get(@"shell:AppsFolder\" + appId, size);

    public static BitmapSource? Render(string parsingName, int size) => Get(parsingName, size) as BitmapSource;

    public static ImageSource? Thumbnail(string path, int size) => Get(path, size, ThumbnailFlags);

    public static ImageSource? ForPin(Models.PinItem pin, int size = 96)
    {
        if (pin.IsRaw) return pin.AppId is { Length: > 0 } id ? ForAppId(id, size) : null;
        if (pin.ResolvedIcon is { } icon && File.Exists(icon)) return ForPath(icon, size);
        if (pin.IsLauncher && pin.Args!.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
            return ForPath(pin.Args, size);
        return pin.ResolvedTarget is { } path ? ForPath(path, size) : null;
    }

    static readonly Dictionary<string, string?> Prints = new(StringComparer.OrdinalIgnoreCase);

    public static string? Fingerprint(string path)
    {
        if (Prints.TryGetValue(path, out var hit)) return hit;
        if (ForPath(path, 32) is not BitmapSource icon) return Prints[path] = null;
        var bgra = new FormatConvertedBitmap(icon, PixelFormats.Bgra32, null, 0);
        var px = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(px, bgra.PixelWidth * 4, 0);
        return Prints[path] = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(px));
    }

    public static string? NameForAppId(string appId)
    {
        var key = @"shell:AppsFolder\" + appId;
        if (Names.TryGetValue(key, out var hit)) return hit;
        try
        {
            var iid = ItemId;
            SHCreateItemFromParsingName(key, IntPtr.Zero, ref iid, out var obj);
            ((IShellItem)obj).GetDisplayName(0, out var ptr);
            var name = Marshal.PtrToStringUni(ptr);
            Marshal.FreeCoTaskMem(ptr);
            return Names[key] = name;
        }
        catch (Exception e) when (e is COMException or ArgumentException or IOException) { return Names[key] = null; }
    }

    const int IconFlags = 0x4 | 0x100;
    const int ThumbnailFlags = 0x0;

    static ImageSource? Get(string parsingName, int size, int flags = IconFlags)
    {
        var key = $"{size}|{flags}|{parsingName}";
        if (Cache.TryGetValue(key, out var hit)) return hit;
        return Cache[key] = Load(parsingName, size, flags);
    }

    static ImageSource? LoadImage(string path, int size)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = size;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UriFormatException) { return null; }
    }

    public static BitmapSource Trim(BitmapSource source)
    {
        var bgra = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight;
        if (w < 64 || h < 64) return source;
        var px = new byte[w * h * 4];
        bgra.CopyPixels(px, w * 4, 0);
        const int skip = 4;
        int left = w, top = h, right = -1, bottom = -1;
        for (var y = skip; y < h - skip; y++)
            for (var x = skip; x < w - skip; x++)
                if (px[(y * w + x) * 4 + 3] >= 0x60)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
        if (right < 0) return source;
        var side = Math.Max(right - left + 1, bottom - top + 1);
        if (side >= w * 0.6) return source;
        side = Math.Min(w, (int)Math.Ceiling(side * 1.1));
        var x0 = Math.Clamp((left + right + 1 - side) / 2, 0, w - side);
        var y0 = Math.Clamp((top + bottom + 1 - side) / 2, 0, h - side);
        var cropped = new CroppedBitmap(bgra, new Int32Rect(x0, y0, side, side));
        cropped.Freeze();
        return cropped;
    }

    static ImageSource? Load(string parsingName, int size, int flags)
    {
        if (flags == IconFlags && Path.GetExtension(parsingName).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" && File.Exists(parsingName))
            return LoadImage(parsingName, size);

        try
        {
            var iid = ImageFactoryId;
            SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var obj);
            ((IShellItemImageFactory)obj).GetImage(new Size { Width = size, Height = size }, flags, out var bitmap);
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return flags == IconFlags ? Trim(source) : source;
            }
            finally { DeleteObject(bitmap); }
        }
        catch (Exception e) when (e is COMException or ArgumentException or IOException) { return null; }
    }
}
