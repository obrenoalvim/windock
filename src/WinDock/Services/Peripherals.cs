using Windows.Devices.Enumeration;

namespace WinDock.Services;

public enum PeripheralKind { Keyboard, Mouse, Headset, Gamepad, Other }

public sealed record Peripheral(string Name, PeripheralKind Kind, int Percent, bool Charging)
{
    public bool Low => !Charging && Percent <= Peripherals.LowPercent;
}

public static class Peripherals
{
    public const int LowPercent = 15;

    const string BluetoothDevices = "System.Devices.ClassGuid:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"";
    const string BatteryLevel = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    const string DeviceClass = "{2BD67D8B-8BEB-48D5-87E0-6CDA3428040A} 4";

    public static async Task<IReadOnlyList<Peripheral>> ReadAsync()
    {
        var logitech = await Safely(LogitechHid.ReadAsync).ConfigureAwait(false);
        var bluetooth = await Safely(Bluetooth).ConfigureAwait(false);
        return [.. logitech.Concat(bluetooth).DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }

    static async Task<IReadOnlyList<Peripheral>> Safely(Func<Task<IReadOnlyList<Peripheral>>> read)
    {
        try { return await read().ConfigureAwait(false); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { return []; }
    }

    static async Task<IReadOnlyList<Peripheral>> Bluetooth()
    {
        var devices = await DeviceInformation.FindAllAsync(BluetoothDevices, [BatteryLevel, DeviceClass], DeviceInformationKind.Device).AsTask().ConfigureAwait(false);
        return [.. devices
            .Where(d => d.Properties.TryGetValue(BatteryLevel, out var level) && level is byte)
            .Select(d => new Peripheral(d.Name, KindOfClass(d.Properties.TryGetValue(DeviceClass, out var c) && c is uint cod ? cod : 0),
                (byte)d.Properties[BatteryLevel], false))];
    }

    public static PeripheralKind KindOfClass(uint classOfDevice) => ((classOfDevice >> 8) & 0x1F, (classOfDevice >> 6) & 0x03) switch
    {
        (0x04, _) => PeripheralKind.Headset,
        (0x05, 0x01) => PeripheralKind.Keyboard,
        (0x05, 0x02 or 0x03) => PeripheralKind.Mouse,
        (0x05, _) => PeripheralKind.Gamepad,
        _ => PeripheralKind.Other
    };

    public static string Glyph(PeripheralKind kind) => kind switch
    {
        PeripheralKind.Keyboard => "",
        PeripheralKind.Mouse => "",
        PeripheralKind.Headset => "",
        PeripheralKind.Gamepad => "",
        _ => ""
    };
}
