using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WinDock.Services;

public static class ShellLinks
{
    static readonly Guid AppUserModelFormat = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    static readonly Guid PropertyStoreId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

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

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, IntPtr value);
        void SetValue(ref PropertyKey key, IntPtr value);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHGetPropertyStoreFromParsingName(string path, IntPtr bindContext, int flags, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(IntPtr value);

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

    public static string? ReadAppId(string path)
    {
        var value = Marshal.AllocCoTaskMem(24);
        try
        {
            for (var i = 0; i < 24; i++) Marshal.WriteByte(value, i, 0);
            var iid = PropertyStoreId;
            SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref iid, out var store);
            var key = new PropertyKey { Format = AppUserModelFormat, Id = 5 };
            store.GetValue(ref key, value);
            var vt = Marshal.ReadInt16(value);
            return vt == 31 ? Marshal.PtrToStringUni(Marshal.ReadIntPtr(value, 8)) : null;
        }
        catch (Exception e) when (e is COMException or FileNotFoundException) { return null; }
        finally
        {
            PropVariantClear(value);
            Marshal.FreeCoTaskMem(value);
        }
    }
}
