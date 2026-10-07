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

    public static ImageSource? ForPath(string path) => Get(path, 96);

    public static ImageSource? ForAppId(string appId) => Get(@"shell:AppsFolder\" + appId, 96);

    public static BitmapSource? Render(string parsingName, int size) => Get(parsingName, size) as BitmapSource;

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

    static ImageSource? Get(string parsingName, int size)
    {
        var key = $"{size}|{parsingName}";
        if (Cache.TryGetValue(key, out var hit)) return hit;
        return Cache[key] = Load(parsingName, size);
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

    static ImageSource? Load(string parsingName, int size)
    {
        if (Path.GetExtension(parsingName).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" && File.Exists(parsingName))
            return LoadImage(parsingName, size);

        try
        {
            var iid = ImageFactoryId;
            SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var obj);
            ((IShellItemImageFactory)obj).GetImage(new Size { Width = size, Height = size }, 0x4 | 0x100, out var bitmap);
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally { DeleteObject(bitmap); }
        }
        catch (Exception e) when (e is COMException or ArgumentException or IOException) { return null; }
    }
}
