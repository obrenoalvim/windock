using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace WinDock.Services;

public sealed record BatteryState(bool Present, int Percent, bool Charging, TimeSpan? Left);

public sealed class NetworkMeter
{
    long lastDown = -1, lastUp = -1;
    DateTime lastAt;

    public (double Down, double Up) Sample(DateTime now)
    {
        long down = 0, up = 0;
        try
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                if (n.GetIPProperties().GatewayAddresses.Count == 0) continue;
                var stats = n.GetIPStatistics();
                down += stats.BytesReceived;
                up += stats.BytesSent;
            }
        }
        catch (NetworkInformationException) { return (0, 0); }

        var seconds = (now - lastAt).TotalSeconds;
        var rates = lastDown < 0 || seconds <= 0 || seconds > 5 ? (0.0, 0.0) : (Math.Max(0, down - lastDown) / seconds, Math.Max(0, up - lastUp) / seconds);
        lastDown = down;
        lastUp = up;
        lastAt = now;
        return rates;
    }

    public static string Rate(double bytesPerSecond)
    {
        var culture = Loc.Instance.Culture;
        return bytesPerSecond switch
        {
            >= 1024 * 1024 * 100 => $"{(bytesPerSecond / 1024 / 1024).ToString("0", culture)} MB/s",
            >= 1024 * 1024 => $"{(bytesPerSecond / 1024 / 1024).ToString("0.0", culture)} MB/s",
            >= 1024 => $"{(bytesPerSecond / 1024).ToString("0", culture)} KB/s",
            _ => $"{bytesPerSecond.ToString("0", CultureInfo.InvariantCulture)} B/s"
        };
    }
}

public sealed class SelfUsage
{
    readonly System.Diagnostics.Process self = System.Diagnostics.Process.GetCurrentProcess();
    TimeSpan lastCpu;
    DateTime lastAt;

    public (double Megabytes, double Cpu) Sample(DateTime now)
    {
        var counters = new Native.MemoryCounters { Size = (uint)Marshal.SizeOf<Native.MemoryCounters>() };
        var bytes = Native.GetProcessMemoryInfo(self.Handle, ref counters, counters.Size) ? (double)counters.PrivateWorkingSet : 0;
        self.Refresh();
        var cpu = self.TotalProcessorTime;
        var seconds = (now - lastAt).TotalSeconds;
        var percent = lastAt == default || seconds <= 0 ? 0 : (cpu - lastCpu).TotalSeconds / seconds / Environment.ProcessorCount * 100;
        (lastCpu, lastAt) = (cpu, now);
        return (bytes / 1024 / 1024, Math.Clamp(percent, 0, 100));
    }
}

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

    public static BatteryState Battery()
    {
        var p = Forms.SystemInformation.PowerStatus;
        if (p.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery) || p.BatteryLifePercent > 1)
            return new BatteryState(false, 0, false, null);
        var charging = p.PowerLineStatus == Forms.PowerLineStatus.Online;
        var left = !charging && p.BatteryLifeRemaining > 0 ? TimeSpan.FromSeconds(p.BatteryLifeRemaining) : (TimeSpan?)null;
        return new BatteryState(true, (int)Math.Round(p.BatteryLifePercent * 100), charging, left);
    }
}
