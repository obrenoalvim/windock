using System.Threading.Channels;
using Windows.Devices.Enumeration;

namespace WinDock.Services;

public static class LogitechHid
{
    const string HidInterfaces = "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\" AND " +
        "System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True AND System.DeviceInterface.Hid.VendorId:=1133 AND " +
        "(System.DeviceInterface.Hid.UsagePage:=65280 OR System.DeviceInterface.Hid.UsagePage:=65347)";
    const string UsageId = "System.DeviceInterface.Hid.UsageId";
    const string Container = "System.Devices.ContainerId";
    const ushort ShortUsage = 0x01, ShortUsageBle = 0x0201;

    const ushort UnifiedBattery = 0x1004, BatteryVoltage = 0x1001, BatteryStatus = 0x1000, DeviceName = 0x0005;
    static readonly byte[] DeviceSlots = [0xFF, 1, 2, 3, 4, 5, 6];

    public static async Task<IReadOnlyList<Peripheral>> ReadAsync()
    {
        var found = await DeviceInformation.FindAllAsync(HidInterfaces, [UsageId, Container]).AsTask().ConfigureAwait(false);
        var receivers = found.GroupBy(d => d.Properties.TryGetValue(Container, out var c) ? c?.ToString() ?? d.Id : d.Id);
        var all = new List<Peripheral>();
        foreach (var group in receivers)
        {
            var shortPath = group.FirstOrDefault(d => IsShort(d))?.Id;
            var longPath = group.FirstOrDefault(d => !IsShort(d))?.Id;
            using var link = HidLink.Open(shortPath, longPath);
            if (link is null) continue;
            foreach (var slot in DeviceSlots)
                if (await ReadDevice(link, slot).ConfigureAwait(false) is { } device) all.Add(device);
        }
        return all;
    }

    static bool IsShort(DeviceInformation d) => d.Properties.TryGetValue(UsageId, out var u) && u is ushort id && id is ShortUsage or ShortUsageBle;

    static async Task<Peripheral?> ReadDevice(HidLink link, byte slot)
    {
        try { return await Probe(link, slot).ConfigureAwait(false); }
        catch (Exception e) when (e is IOException or ObjectDisposedException or UnauthorizedAccessException) { return null; }
    }

    static async Task<Peripheral?> Probe(HidLink link, byte slot)
    {
        var level = await Battery(link, slot).ConfigureAwait(false);
        if (level is not { } battery) return null;
        var (name, kind) = await Identity(link, slot).ConfigureAwait(false);
        return new Peripheral(name, kind, battery.Percent, battery.Charging);
    }

    static async Task<(int Percent, bool Charging)?> Battery(HidLink link, byte slot)
    {
        if (await Feature(link, slot, UnifiedBattery).ConfigureAwait(false) is { } unified
            && await link.Ask(slot, unified, 1).ConfigureAwait(false) is { } status)
            return (status[0] > 0 ? status[0] : LevelPercent(status[1]), status[2] is 1 or 2);
        if (await Feature(link, slot, BatteryVoltage).ConfigureAwait(false) is { } voltage
            && await link.Ask(slot, voltage, 0).ConfigureAwait(false) is { } reading)
            return (VoltagePercent(reading[0] << 8 | reading[1]), (reading[2] & 0x80) != 0 && (reading[2] & 0x03) == 0);
        if (await Feature(link, slot, BatteryStatus).ConfigureAwait(false) is { } legacy
            && await link.Ask(slot, legacy, 0).ConfigureAwait(false) is { } old)
            return (old[0], old[2] is 1 or 2 or 4);
        return null;
    }

    static async Task<byte?> Feature(HidLink link, byte slot, ushort feature)
    {
        var reply = await link.Ask(slot, 0, 0, (byte)(feature >> 8), (byte)feature).ConfigureAwait(false);
        return reply is { } r && r[0] != 0 ? r[0] : null;
    }

    static async Task<(string Name, PeripheralKind Kind)> Identity(HidLink link, byte slot)
    {
        if (await Feature(link, slot, DeviceName).ConfigureAwait(false) is not { } index) return ("Logitech", PeripheralKind.Other);
        var length = (await link.Ask(slot, index, 0).ConfigureAwait(false))?[0] ?? 0;
        var name = new List<byte>();
        while (name.Count < length && await link.Ask(slot, index, 1, (byte)name.Count).ConfigureAwait(false) is { } part)
            name.AddRange(part.Take(Math.Min(part.Length, length - name.Count)));
        var type = (await link.Ask(slot, index, 2).ConfigureAwait(false))?[0];
        var text = System.Text.Encoding.UTF8.GetString([.. name]).TrimEnd('\0').Trim();
        return (text.Length > 0 ? ShortName(text) : "Logitech", KindOf(type));
    }

    public static PeripheralKind KindOf(byte? type) => type switch
    {
        0 or 2 => PeripheralKind.Keyboard,
        3 or 4 or 5 => PeripheralKind.Mouse,
        8 => PeripheralKind.Headset,
        10 or 11 or 12 => PeripheralKind.Gamepad,
        _ => PeripheralKind.Other
    };

    public static int LevelPercent(byte flags) =>
        (flags & 0x08) != 0 ? 100 : (flags & 0x04) != 0 ? 50 : (flags & 0x02) != 0 ? 20 : (flags & 0x01) != 0 ? 5 : 0;

    static readonly (int Millivolts, int Percent)[] Discharge =
        [(4186, 100), (4067, 90), (3989, 80), (3922, 70), (3859, 60), (3811, 50), (3778, 40), (3751, 30), (3717, 20), (3671, 10), (3646, 5), (3579, 2)];

    public static int VoltagePercent(int millivolts)
    {
        if (millivolts >= Discharge[0].Millivolts) return 100;
        for (var i = 1; i < Discharge.Length; i++)
        {
            var (mv, percent) = Discharge[i];
            if (millivolts < mv) continue;
            var (upperMv, upperPercent) = Discharge[i - 1];
            return percent + (upperPercent - percent) * (millivolts - mv) / (upperMv - mv);
        }
        return 0;
    }

    static readonly string[] Filler = ["LIGHTSPEED", "Wireless", "Bluetooth", "Mechanical", "Gaming", "RGB"];

    public static string ShortName(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kept = words.TakeWhile(w => !Filler.Contains(w, StringComparer.OrdinalIgnoreCase)).ToArray();
        return kept.Length > 0 ? string.Join(' ', kept) : name;
    }

    sealed class HidLink : IDisposable
    {
        const byte ShortReport = 0x10, LongReport = 0x11, SoftwareId = 0x0B;
        const byte V2Error = 0xFF, V1Error = 0x8F;
        static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(700);

        readonly FileStream? shortStream, longStream;
        readonly Channel<byte[]> replies = Channel.CreateUnbounded<byte[]>();
        readonly CancellationTokenSource stop = new();

        HidLink(FileStream? shortStream, FileStream? longStream)
        {
            this.shortStream = shortStream;
            this.longStream = longStream;
            foreach (var stream in new[] { shortStream, longStream })
                if (stream is not null) _ = Pump(stream);
        }

        public static HidLink? Open(string? shortPath, string? longPath)
        {
            var longStream = OpenStream(longPath);
            var shortStream = OpenStream(shortPath);
            return shortStream is null && longStream is null ? null : new HidLink(shortStream, longStream);
        }

        static FileStream? OpenStream(string? path)
        {
            if (path is null) return null;
            try { return new FileStream(File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous), FileAccess.ReadWrite, 0, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        }

        async Task Pump(FileStream stream)
        {
            var buffer = new byte[64];
            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, stop.Token).ConfigureAwait(false);
                    if (read == 0) return;
                    replies.Writer.TryWrite(buffer[..read]);
                }
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException) { }
        }

        public async Task<byte[]?> Ask(byte slot, byte feature, byte function, params byte[] args)
        {
            var functionId = (byte)(function << 4 | SoftwareId);
            var stream = longStream ?? shortStream!;
            var report = new byte[longStream is null ? 7 : 20];
            report[0] = longStream is null ? ShortReport : LongReport;
            report[1] = slot;
            report[2] = feature;
            report[3] = functionId;
            args.AsSpan(0, Math.Min(args.Length, report.Length - 4)).CopyTo(report.AsSpan(4));
            while (replies.Reader.TryRead(out _)) { }
            await stream.WriteAsync(report).ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            timeout.CancelAfter(Wait);
            try
            {
                while (true)
                {
                    var r = await replies.Reader.ReadAsync(timeout.Token).ConfigureAwait(false);
                    if (r.Length < 5 || r[0] is not (ShortReport or LongReport) || r[1] != slot) continue;
                    if (r[2] == feature && r[3] == functionId) return r[4..];
                    if (r[2] is V2Error or V1Error && r[3] == feature && r[4] == functionId) return null;
                }
            }
            catch (OperationCanceledException) { return null; }
        }

        public void Dispose()
        {
            stop.Cancel();
            shortStream?.Dispose();
            longStream?.Dispose();
            stop.Dispose();
        }
    }
}
