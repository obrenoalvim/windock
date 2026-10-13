using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using Manager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace WinDock.Services;

public sealed record MediaInfo(string Title, string Artist, string? AppId, bool Playing, ImageSource? Cover, bool CanPrevious, bool CanNext, Session Session);

public sealed class MediaSessions
{
    readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    Manager? manager;
    List<Session> sessions = [];
    Task? starting;
    int version;

    public static MediaSessions Shared { get; } = new();

    // The session Windows treats as current (what the tile shows), else the first one playing.
    public MediaInfo? Current { get; private set; }

    public IReadOnlyList<MediaInfo> All { get; private set; } = [];

    public event Action? Changed;

    public void Start() => starting ??= Connect();

    async Task Connect()
    {
        try
        {
            manager = await Manager.RequestAsync();
            manager.CurrentSessionChanged += (_, _) => Later(Follow);
            manager.SessionsChanged += (_, _) => Later(Follow);
            Follow();
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException) { }
    }

    void Later(Action action) => dispatcher.BeginInvoke(action);

    void Follow()
    {
        if (manager is null) return;
        List<Session> next;
        try { next = [.. manager.GetSessions()]; }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { return; }
        foreach (var gone in sessions.Where(s => !next.Contains(s)))
        {
            gone.MediaPropertiesChanged -= OnSessionChanged;
            gone.PlaybackInfoChanged -= OnSessionChanged;
        }
        foreach (var added in next.Where(s => !sessions.Contains(s)))
        {
            added.MediaPropertiesChanged += OnSessionChanged;
            added.PlaybackInfoChanged += OnSessionChanged;
        }
        sessions = next;
        _ = Refresh();
    }

    void OnSessionChanged(Session sender, object args) => Later(() => _ = Refresh());

    // ponytail: rereads every session and cover on any change; cache covers per session if many players get chatty
    async Task Refresh()
    {
        var mine = ++version;
        var all = new List<MediaInfo>();
        foreach (var s in sessions)
            if (await Read(s) is { } info) all.Add(info);
        Session? current = null;
        try { current = manager?.GetCurrentSession(); }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
        if (mine != version) return;
        All = all;
        Current = all.FirstOrDefault(i => ReferenceEquals(i.Session, current)) ?? all.FirstOrDefault(i => i.Playing) ?? all.FirstOrDefault();
        Changed?.Invoke();
    }

    static async Task<MediaInfo?> Read(Session s)
    {
        try
        {
            var props = await s.TryGetMediaPropertiesAsync();
            var playback = s.GetPlaybackInfo();
            var title = props.Title?.Trim() ?? "";
            if (title.Length == 0) return null;
            var cover = props.Thumbnail is { } thumb ? await Cover(thumb) : null;
            return new MediaInfo(title, (props.Artist ?? props.AlbumArtist ?? "").Trim(), s.SourceAppUserModelId,
                playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, cover,
                playback.Controls.IsPreviousEnabled, playback.Controls.IsNextEnabled, s);
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException) { return null; }
    }

    static async Task<ImageSource?> Cover(Windows.Storage.Streams.IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            using var source = stream.AsStreamForRead();
            var memory = new MemoryStream();
            await source.CopyToAsync(memory);
            memory.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 160;
            image.StreamSource = memory;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException or NotSupportedException or InvalidOperationException) { return null; }
    }

    // No target means the current session, the one the tile shows.
    public async Task Toggle(MediaInfo? target = null)
    {
        if ((target ?? Current)?.Session is { } s) await Safe(s.TryTogglePlayPauseAsync().AsTask());
    }

    public async Task Next(MediaInfo? target = null)
    {
        if ((target ?? Current)?.Session is { } s) await Safe(s.TrySkipNextAsync().AsTask());
    }

    public async Task Previous(MediaInfo? target = null)
    {
        if ((target ?? Current)?.Session is { } s) await Safe(s.TrySkipPreviousAsync().AsTask());
    }

    static async Task Safe(Task<bool> action)
    {
        try { await action; }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
    }
}
