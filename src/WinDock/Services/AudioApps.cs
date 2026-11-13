using System.Runtime.InteropServices;

namespace WinDock.Services;

public sealed record AudioApp(string Key, string Name, string? Exe, double Volume, bool Muted);

public static partial class Audio
{
    public const string SystemSounds = "system";

    const int SessionExpired = 2;

    static readonly Guid SessionManagerIid = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr session, int flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr session, int flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid group);
        [PreserveSig] int SetGroupingParam(ref Guid group, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out int pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    public static List<AudioApp> Apps()
    {
        var apps = new Dictionary<string, AudioApp>(StringComparer.OrdinalIgnoreCase);
        EachSession((key, session, volume) =>
        {
            session.GetState(out var state);
            if (state == SessionExpired) return;
            volume.GetMasterVolume(out var level);
            volume.GetMute(out var muted);
            if (apps.TryGetValue(key, out var known))
            {
                apps[key] = known with { Volume = Math.Max(known.Volume, level), Muted = known.Muted && muted };
                return;
            }
            session.GetProcessId(out var pid);
            apps[key] = key == SystemSounds
                ? new AudioApp(key, Loc.T("systemSounds"), null, level, muted)
                : new AudioApp(key, TrayApps.NameOf(key, [pid]), key, level, muted);
        });
        return apps.Values
            .OrderBy(a => a.Key == SystemSounds)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static void SetAppVolume(string key, double level) => EachSession((found, _, volume) =>
    {
        if (found.Equals(key, StringComparison.OrdinalIgnoreCase)) volume.SetMasterVolume((float)Math.Clamp(level, 0, 1), ref context);
    });

    public static void SetAppMute(string key, bool mute) => EachSession((found, _, volume) =>
    {
        if (found.Equals(key, StringComparison.OrdinalIgnoreCase)) volume.SetMute(mute, ref context);
    });

    static string? KeyOf(IAudioSessionControl2 session)
    {
        if (session.IsSystemSoundsSession() == 0) return SystemSounds;
        session.GetProcessId(out var pid);
        return pid == 0 ? null : TrayApps.Path(pid);
    }

    static void EachSession(Action<string, IAudioSessionControl2, ISimpleAudioVolume> visit)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        object? manager = null;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new DeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(Render, Multimedia, out device) != 0 || device is null) return;
            var iid = SessionManagerIid;
            if (device.Activate(ref iid, InProcess, IntPtr.Zero, out manager) != 0) return;
            if (((IAudioSessionManager2)manager).GetSessionEnumerator(out sessions) != 0 || sessions is null) return;
            sessions.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                if (sessions.GetSession(i, out var session) != 0 || session is null) continue;
                try
                {
                    if (KeyOf(session) is { } key) visit(key, session, (ISimpleAudioVolume)session);
                }
                finally { Marshal.ReleaseComObject(session); }
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException) { }
        finally
        {
            if (sessions is not null) Marshal.ReleaseComObject(sessions);
            if (manager is not null) Marshal.ReleaseComObject(manager);
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }
}
