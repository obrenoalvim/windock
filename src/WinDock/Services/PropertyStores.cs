using System.Runtime.InteropServices;

namespace WinDock.Services;

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

static class PropertyStores
{
    public static readonly Guid Iid = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    const short StringType = 31;
    const int VariantSize = 24;

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(IntPtr value);

    public static string? ReadString(IPropertyStore store, Guid format, uint id)
    {
        var value = Blank();
        try
        {
            var key = new PropertyKey { Format = format, Id = id };
            store.GetValue(ref key, value);
            var text = Marshal.ReadInt16(value) == StringType ? Marshal.PtrToStringUni(Marshal.ReadIntPtr(value, 8)) : null;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        finally
        {
            PropVariantClear(value);
            Marshal.FreeCoTaskMem(value);
        }
    }

    public static void WriteString(IPropertyStore store, Guid format, uint id, string text)
    {
        var value = Blank();
        try
        {
            Marshal.WriteInt16(value, StringType);
            Marshal.WriteIntPtr(value, 8, Marshal.StringToCoTaskMemUni(text));
            var key = new PropertyKey { Format = format, Id = id };
            store.SetValue(ref key, value);
            store.Commit();
        }
        finally
        {
            PropVariantClear(value);
            Marshal.FreeCoTaskMem(value);
        }
    }

    static IntPtr Blank()
    {
        var value = Marshal.AllocCoTaskMem(VariantSize);
        for (var i = 0; i < VariantSize; i++) Marshal.WriteByte(value, i, 0);
        return value;
    }
}
