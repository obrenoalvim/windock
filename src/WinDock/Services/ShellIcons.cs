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

    public static ImageSource? ForPath(string path) => Get(path);

    public static ImageSource? ForAppId(string appId) => Get(@"shell:AppsFolder\" + appId);

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

    static ImageSource? Get(string parsingName)
    {
        if (Cache.TryGetValue(parsingName, out var hit)) return hit;
        return Cache[parsingName] = Load(parsingName);
    }

    static ImageSource? Load(string parsingName)
    {
        try
        {
            var iid = ImageFactoryId;
            SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var obj);
            ((IShellItemImageFactory)obj).GetImage(new Size { Width = 96, Height = 96 }, 0x4 | 0x100, out var bitmap);
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

