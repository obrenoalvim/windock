using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace WinDock.Services;

public sealed class SystemStats
{
    long lastIdle, lastBusy;

    public double Cpu()
    {
        if (!Native.GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var busy = kernel + user;
        var dIdle = idle - lastIdle;
        var dBusy = busy - lastBusy;
        lastIdle = idle;
        lastBusy = busy;
        return dBusy <= 0 ? 0 : Math.Clamp((1.0 - (double)dIdle / dBusy) * 100, 0, 100);
    }

    public static double Ram()
    {
        var s = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        return Native.GlobalMemoryStatusEx(ref s) ? s.MemoryLoad : 0;
    }

    public static (bool Present, int Percent, bool Charging) Battery()
    {
        var p = Forms.SystemInformation.PowerStatus;
        if (p.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery) || p.BatteryLifePercent > 1)
            return (false, 0, false);
        return (true, (int)Math.Round(p.BatteryLifePercent * 100), p.PowerLineStatus == Forms.PowerLineStatus.Online);
    }

    public static NetworkKind Network()
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
}

public enum NetworkKind { None, Wired, Wifi }
