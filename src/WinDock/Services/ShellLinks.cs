using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WinDock.Services;

public static class ShellLinks
{
    static readonly Guid AppUserModelFormat = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    const uint AppUserModelId = 5;

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLinkCoClass;

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int max, IntPtr data, uint flags);
        void GetIDList(out IntPtr pidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int max);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int max);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out ushort hotkey);
        void SetHotkey(ushort hotkey);
        void GetShowCmd(out int show);
        void SetShowCmd(int show);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHGetPropertyStoreFromParsingName(string path, IntPtr bindContext, int flags, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("shell32.dll")]
    static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    public static byte[] CreateLauncher(string target, string args, string? iconPath, string appId)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        link.SetPath(target);
        link.SetArguments(args);
        link.SetWorkingDirectory(Path.GetDirectoryName(target) ?? "");
        if (iconPath is not null) link.SetIconLocation(iconPath, 0);
        PropertyStores.WriteString((IPropertyStore)link, AppUserModelFormat, AppUserModelId, appId);

        var temp = Path.Combine(Path.GetTempPath(), $"windock-{Guid.NewGuid():N}.lnk");
        try
        {
            ((IPersistFile)link).Save(temp, true);
            return File.ReadAllBytes(temp);
        }
        finally { File.Delete(temp); }
    }

    public static byte[] CreateForExe(string exePath)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        link.SetPath(exePath);
        link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? "");
        link.SetIconLocation(exePath, 0);
        var temp = Path.Combine(Path.GetTempPath(), $"windock-{Guid.NewGuid():N}.lnk");
        try
        {
            ((IPersistFile)link).Save(temp, true);
            return File.ReadAllBytes(temp);
        }
        finally { File.Delete(temp); }
    }

    public static string? ReadTarget(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLinkCoClass();
            ((IPersistFile)link).Load(lnkPath, 0);
            var sb = new StringBuilder(520);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch (Exception e) when (e is COMException or IOException) { return null; }
    }

    public static string? ReadArguments(string lnkPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLinkCoClass();
            ((IPersistFile)link).Load(lnkPath, 0);
            var sb = new StringBuilder(1024);
            link.GetArguments(sb, sb.Capacity);
            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch (Exception e) when (e is COMException or IOException) { return null; }
    }

    public static string? ReadAppId(string path)
    {
        try
        {
            var iid = PropertyStores.Iid;
            SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref iid, out var store);
            return AppIdFrom(store);
        }
        catch (Exception e) when (e is COMException or FileNotFoundException) { return null; }
    }

    public static string? WindowAppId(IntPtr hwnd)
    {
        try
        {
            var iid = PropertyStores.Iid;
            return SHGetPropertyStoreForWindow(hwnd, ref iid, out var store) == 0 ? AppIdFrom(store) : null;
        }
        catch (Exception e) when (e is COMException or InvalidCastException) { return null; }
    }

    static string? AppIdFrom(IPropertyStore store)
    {
        try { return PropertyStores.ReadString(store, AppUserModelFormat, AppUserModelId); }
        finally { Marshal.FinalReleaseComObject(store); }
    }
}
