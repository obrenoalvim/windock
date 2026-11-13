using System.Runtime.InteropServices;

namespace WinDock.Services;

public static class Brightness
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PhysicalMonitor
    {
        public nint Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    [DllImport("dxva2.dll")] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count);
    [DllImport("dxva2.dll")] static extern bool GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, [Out] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll")] static extern bool GetMonitorBrightness(nint monitor, out uint min, out uint current, out uint max);
    [DllImport("dxva2.dll")] static extern bool SetMonitorBrightness(nint monitor, uint value);
    [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitors(uint count, [In] PhysicalMonitor[] monitors);

    public static double? Read(nint monitor) => Use(monitor, (_, min, now, max) => (now - min) / (double)(max - min));

    public static void Set(nint monitor, double level) => Use(monitor, (handle, min, _, max) =>
    {
        SetMonitorBrightness(handle, (uint)Math.Round(min + Math.Clamp(level, 0, 1) * (max - min)));
        return level;
    });

    static double? Use(nint monitor, Func<nint, uint, uint, uint, double> action)
    {
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0) return null;
        var monitors = new PhysicalMonitor[count];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, monitors)) return null;
        try
        {
            var handle = monitors[0].Handle;
            return GetMonitorBrightness(handle, out var min, out var now, out var max) && max > min ? action(handle, min, now, max) : null;
        }
        finally { DestroyPhysicalMonitors(count, monitors); }
    }
}
