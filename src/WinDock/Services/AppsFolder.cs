using System.Runtime.InteropServices;

namespace WinDock.Services;

public sealed record StoreApp(string Name, string AppId);

public static class AppsFolder
{
    static readonly Guid FolderId = new("1E87508D-89C2-42F0-8A7E-645A0F50CA58");
    static readonly Guid ItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    static readonly Guid EnumHandler = new("94F60519-2850-4924-AA5A-D15E84868039");
    static readonly Guid EnumId = new("70629033-E363-4A28-A567-0DB78006E6D7");

    const int DisplayName = 0;
    const int ParentRelativeParsing = unchecked((int)0x80018001);

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid iid, out IntPtr result);
        void GetParent(out IntPtr parent);
        void GetDisplayName(int form, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IntPtr other, uint hint, out int order);
    }

    [ComImport, Guid("70629033-E363-4A28-A567-0DB78006E6D7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IEnumShellItems
    {
        [PreserveSig] int Next(uint count, [MarshalAs(UnmanagedType.Interface)] out IShellItem item, out uint fetched);
        void Skip(uint count);
        void Reset();
        void Clone(out IntPtr copy);
    }

    [DllImport("shell32.dll", PreserveSig = false)]
    static extern void SHGetKnownFolderItem(ref Guid folder, uint flags, IntPtr token, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    public static List<StoreApp> Store()
    {
        var apps = new List<StoreApp>();
        try
        {
            var folderId = FolderId;
            var itemId = ItemId;
            SHGetKnownFolderItem(ref folderId, 0, IntPtr.Zero, ref itemId, out var folder);
            var handler = EnumHandler;
            var enumId = EnumId;
            folder.BindToHandler(IntPtr.Zero, ref handler, ref enumId, out var pointer);
            var items = (IEnumShellItems)Marshal.GetObjectForIUnknown(pointer);

            while (items.Next(1, out var item, out var fetched) == 0 && fetched == 1)
            {
                var name = Read(item, DisplayName);
                var parsing = Read(item, ParentRelativeParsing);
                if (name is { Length: > 0 } && parsing is { } id && id.Contains('!') && !id.Contains('\\'))
                    apps.Add(new StoreApp(name, id));
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException) { }
        return apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static string? Read(IShellItem item, int form)
    {
        item.GetDisplayName(form, out var pointer);
        var text = Marshal.PtrToStringUni(pointer);
        Marshal.FreeCoTaskMem(pointer);
        return text;
    }
}
