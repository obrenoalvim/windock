using System.Runtime.InteropServices;

namespace WinDock.Services;

public sealed record AudioState(double Volume, bool Muted, string? Device);

public sealed record AudioDevice(string Id, string Name, bool IsDefault);

public static class Audio
{
    const int Render = 0, Capture = 1, Console = 0, Multimedia = 1, InProcess = 0x1;
    const uint FriendlyName = 14;

    static readonly Guid VolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    static readonly Guid DeviceFormat = new("A45C254E-DF1C-4EFD-8020-67D146A850E0");
    static Guid context = Guid.Empty;

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class DeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, int state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid Format;
        public int Id;
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr data);
    }

    sealed class Listener : IMMNotificationClient, IAudioEndpointVolumeCallback
    {
        public int OnDeviceStateChanged(string id, int state) => Devices();
        public int OnDeviceAdded(string id) => 0;
        public int OnDeviceRemoved(string id) => 0;
        public int OnDefaultDeviceChanged(int flow, int role, string? id) => Devices();
        public int OnPropertyValueChanged(string id, PropertyKey key) => 0;
        public int OnNotify(IntPtr data) => Raise();

        static int Devices()
        {
            dispatcher?.BeginInvoke(() =>
            {
                Speaker.Reset();
                Microphone.Reset();
            });
            return Raise();
        }

        static int Raise()
        {
            dispatcher?.BeginInvoke(() => Changed?.Invoke());
            return 0;
        }
    }

    static System.Windows.Threading.Dispatcher? dispatcher;
    static Listener? listener;
    static IMMDeviceEnumerator? watching;

    public static event Action? Changed;

    public static void Watch()
    {
        if (listener is not null) return;
        dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        listener = new Listener();
        try
        {
            watching = (IMMDeviceEnumerator)new DeviceEnumerator();
            watching.RegisterEndpointNotificationCallback(listener);
        }
        catch (Exception e) when (e is COMException or InvalidCastException) { watching = null; }
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    class PolicyConfigClient;

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
        [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
        [PreserveSig] int ResetDeviceFormat(IntPtr a);
        [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
        [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int GetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int SetPropertyValue(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float db, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    static readonly TimeSpan Reconnect = TimeSpan.FromSeconds(3);
    static readonly Endpoint Speaker = new(Render, Multimedia);
    static readonly Endpoint Microphone = new(Capture, Console);

    public static AudioState? Read() => Speaker.Use(volume =>
    {
        volume.GetMasterVolumeLevelScalar(out var level);
        volume.GetMute(out var muted);
        return new AudioState(Math.Round(level, 2), muted, Speaker.Name);
    });

    public static void SetVolume(double level) => Speaker.Use(volume =>
    {
        volume.SetMasterVolumeLevelScalar((float)Math.Clamp(level, 0, 1), ref context);
        if (level > 0) volume.SetMute(false, ref context);
        return null;
    });

    public static void SetMute(bool mute) => Speaker.Use(volume =>
    {
        volume.SetMute(mute, ref context);
        return null;
    });

    public static void Nudge(int steps)
    {
        if (Read() is { } now) SetVolume(Math.Round(now.Volume + steps * 0.02, 2));
    }

    public static AudioState? ReadMic() => Microphone.Use(volume =>
    {
        volume.GetMasterVolumeLevelScalar(out var level);
        volume.GetMute(out var muted);
        return new AudioState(Math.Round(level, 2), muted, Microphone.Name);
    });

    public static bool? ToggleMic() => Microphone.Use(volume =>
    {
        volume.GetMute(out var muted);
        volume.SetMute(!muted, ref context);
        return new AudioState(0, !muted, null);
    })?.Muted;

    public static List<AudioDevice> Outputs()
    {
        var list = new List<AudioDevice>();
        IMMDeviceEnumerator? enumerator = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new DeviceEnumerator();
            string? current = null;
            if (enumerator.GetDefaultAudioEndpoint(Render, Multimedia, out var main) == 0 && main is not null)
            {
                main.GetId(out current);
                Marshal.ReleaseComObject(main);
            }
            if (enumerator.EnumAudioEndpoints(Render, 1, out var devices) != 0 || devices is null) return list;
            try
            {
                devices.GetCount(out var count);
                for (uint i = 0; i < count; i++)
                {
                    if (devices.Item(i, out var device) != 0 || device is null) continue;
                    try
                    {
                        if (device.GetId(out var id) != 0 || device.OpenPropertyStore(0, out var store) != 0) continue;
                        string? name;
                        try { name = PropertyStores.ReadString(store, DeviceFormat, FriendlyName); }
                        finally { Marshal.ReleaseComObject(store); }
                        list.Add(new AudioDevice(id, name ?? id, id == current));
                    }
                    finally { Marshal.ReleaseComObject(device); }
                }
            }
            finally { Marshal.ReleaseComObject(devices); }
        }
        catch (Exception e) when (e is COMException or InvalidCastException) { }
        finally
        {
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
        return list;
    }

    public static bool SetOutput(string id)
    {
        try
        {
            var policy = (IPolicyConfig)new PolicyConfigClient();
            try
            {
                for (var role = 0; role <= 2; role++)
                    if (policy.SetDefaultEndpoint(id, role) != 0) return false;
            }
            finally { Marshal.ReleaseComObject(policy); }
            Speaker.Reset();
            return true;
        }
        catch (Exception e) when (e is COMException or InvalidCastException) { return false; }
    }

    public static string MicGlyph(AudioState? state) => state is { Muted: true } ? "" : "";

    public static string Glyph(AudioState? state) => state switch
    {
        null => "",
        { Muted: true } => "",
        { Volume: <= 0 } => "",
        { Volume: < 0.34 } => "",
        { Volume: < 0.67 } => "",
        _ => ""
    };

    sealed class Endpoint(int flow, int role)
    {
        IAudioEndpointVolume? endpoint;
        DateTime connectedAt;

        public string? Name { get; private set; }

        public void Reset()
        {
            Drop();
            connectedAt = DateTime.MinValue;
        }

        public AudioState? Use(Func<IAudioEndpointVolume, AudioState?> action)
        {
            try
            {
                if (endpoint is null || listener is null && DateTime.UtcNow - connectedAt > Reconnect) Connect();
                return endpoint is null ? null : action(endpoint);
            }
            catch (Exception e) when (e is COMException or InvalidCastException)
            {
                Drop();
                return null;
            }
        }

        void Connect()
        {
            Drop();
            connectedAt = DateTime.UtcNow;
            IMMDeviceEnumerator? enumerator = null;
            IMMDevice? device = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new DeviceEnumerator();
                if (enumerator.GetDefaultAudioEndpoint(flow, role, out device) != 0 || device is null) return;
                var iid = VolumeIid;
                if (device.Activate(ref iid, InProcess, IntPtr.Zero, out var activated) != 0) return;
                endpoint = (IAudioEndpointVolume)activated;
                if (listener is not null) endpoint.RegisterControlChangeNotify(listener);
                if (device.OpenPropertyStore(0, out var store) != 0) return;
                try { Name = PropertyStores.ReadString(store, DeviceFormat, FriendlyName); }
                finally { Marshal.ReleaseComObject(store); }
            }
            finally
            {
                if (device is not null) Marshal.ReleaseComObject(device);
                if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
            }
        }

        void Drop()
        {
            if (endpoint is not null)
            {
                if (listener is not null)
                    try { endpoint.UnregisterControlChangeNotify(listener); }
                    catch (COMException) { }
                Marshal.ReleaseComObject(endpoint);
            }
            endpoint = null;
            Name = null;
        }
    }
}
