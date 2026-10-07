using System.Runtime.InteropServices;

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

    public double Ram()
    {
        var s = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        return Native.GlobalMemoryStatusEx(ref s) ? s.MemoryLoad : 0;
    }
}
