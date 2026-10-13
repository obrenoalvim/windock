using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDock.Services;

public enum NetworkKind { None, Wired, Wifi }

public sealed record NetworkState(NetworkKind Kind, bool Internet, string? Ssid, int Signal);

public static class NetworkStatus
{
    const int CurrentConnection = 7;
    const int SsidLengthOffset = 520, SsidOffset = 524, SignalOffset = 576;
    const int InterfaceListHeader = 8, InterfaceInfoSize = 532;

    static readonly Guid ListManager = new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

    [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr handle, ref Guid iface, int opcode, IntPtr reserved, out int size, out IntPtr data, out int type);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr memory);

    public static NetworkState Read()
    {
        var kind = Kind();
        var internet = kind != NetworkKind.None && HasInternet();
        var (ssid, signal) = kind == NetworkKind.Wifi ? Wifi() : (null, 0);
        return new NetworkState(kind, internet, ssid, signal);
    }

    public static string Glyph(NetworkState state) => state switch
    {
        { Kind: NetworkKind.None } => "\uF384",
        { Kind: NetworkKind.Wifi, Internet: false } => "\uEB5E",
        { Kind: NetworkKind.Wifi, Signal: < 25 } => "\uE872",
        { Kind: NetworkKind.Wifi, Signal: < 50 } => "\uE873",
        { Kind: NetworkKind.Wifi, Signal: < 75 } => "\uE874",
        { Kind: NetworkKind.Wifi } => "\uE701",
        { Internet: false } => "\uF384",
        _ => "\uE839"
    };

    static NetworkKind Kind()
    {
        try
        {
            var up = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Where(n => n.GetIPProperties().GatewayAddresses.Count > 0)
                .ToList();
            if (up.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)) return NetworkKind.Wifi;
            return up.Count > 0 ? NetworkKind.Wired : NetworkKind.None;
        }
        catch (NetworkInformationException) { return NetworkKind.None; }
    }

    static bool HasInternet()
    {
        try
        {
            if (Type.GetTypeFromCLSID(ListManager) is not { } type) return true;
            dynamic manager = Activator.CreateInstance(type)!;
            try { return (bool)manager.IsConnectedToInternet; }
            finally { Marshal.ReleaseComObject(manager); }
        }
        catch (Exception e) when (e is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return true; }
    }

    static (string? Ssid, int Signal) Wifi()
    {
        try { return QueryWifi(); }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return (null, 0); }
    }

    static (string? Ssid, int Signal) QueryWifi()
    {
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out var handle) != 0) return (null, 0);
        var list = IntPtr.Zero;
        try
        {
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out list) != 0) return (null, 0);
            var count = Marshal.ReadInt32(list);
            for (var i = 0; i < count; i++)
            {
                var iface = Marshal.PtrToStructure<Guid>(list + InterfaceListHeader + i * InterfaceInfoSize);
                if (WlanQueryInterface(handle, ref iface, CurrentConnection, IntPtr.Zero, out var size, out var data, out _) != 0) continue;
                try
                {
                    if (size < SignalOffset + 4) continue;
                    var length = Math.Clamp(Marshal.ReadInt32(data, SsidLengthOffset), 0, 32);
                    var bytes = new byte[length];
                    Marshal.Copy(data + SsidOffset, bytes, 0, length);
                    return (Encoding.UTF8.GetString(bytes), Math.Clamp(Marshal.ReadInt32(data, SignalOffset), 0, 100));
                }
                finally { WlanFreeMemory(data); }
            }
            return (null, 0);
        }
        finally
        {
            if (list != IntPtr.Zero) WlanFreeMemory(list);
            WlanCloseHandle(handle, IntPtr.Zero);
        }
    }
}
