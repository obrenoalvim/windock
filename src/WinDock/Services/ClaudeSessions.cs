using System.Text.Json;
using System.Text.Json.Nodes;
using WinDock.Models;

namespace WinDock.Services;

public enum ClaudeSessionState { Working, Waiting, Done }

public sealed record ClaudeSession(string Id, string Folder, ClaudeSessionState State, string? Message, DateTime At, string? Transcript = null)
{
    public string Name => Path.GetFileName(Folder.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : Folder;
}

public sealed class ClaudeSessions
{
    public const string HookArgument = "--claude-hook";

    static readonly string[] Events = ["UserPromptSubmit", "Notification", "Stop", "SessionEnd"];
    static readonly TimeSpan Forget = TimeSpan.FromHours(12);
    static readonly TimeSpan Resumed = TimeSpan.FromSeconds(3);
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    FileSystemWatcher? watcher;

    public static ClaudeSessions Shared { get; } = new();

    public static string Dir => Path.Combine(Settings.Dir, "claude");

    public static string SettingsPath => Path.Combine(Path.GetDirectoryName(ClaudeUsage.CredentialsPath)!, "settings.json");

    public event Action? Changed;

    public void Watch()
    {
        if (watcher is not null) return;
        Directory.CreateDirectory(Dir);
        watcher = new FileSystemWatcher(Dir, "*.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        watcher.Changed += (_, _) => Raise();
        watcher.Created += (_, _) => Raise();
        watcher.Deleted += (_, _) => Raise();
        watcher.EnableRaisingEvents = true;
    }

    void Raise() => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Changed?.Invoke());

    public static void Record(string json, string dir)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (Text(root, "session_id") is not { Length: > 0 } id || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) return;
            var file = Path.Combine(dir, id + ".json");
            ClaudeSessionState? state = Text(root, "hook_event_name") switch
            {
                "UserPromptSubmit" => ClaudeSessionState.Working,
                "Notification" => ClaudeSessionState.Waiting,
                "Stop" => ClaudeSessionState.Done,
                _ => null
            };
            if (state is null)
            {
                File.Delete(file);
                return;
            }
            Directory.CreateDirectory(dir);
            var session = new ClaudeSession(id, Text(root, "cwd") ?? "", state.Value, Text(root, "message"), DateTime.UtcNow, Text(root, "transcript_path"));
            File.WriteAllText(file, JsonSerializer.Serialize(session));
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
    }

    static string? Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static List<ClaudeSession> Read(string dir, DateTime nowUtc)
    {
        var sessions = new List<ClaudeSession>();
        if (!Directory.Exists(dir)) return sessions;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<ClaudeSession>(File.ReadAllText(file)) is not { } session) continue;
                if (nowUtc - session.At > Forget) File.Delete(file);
                else sessions.Add(Settled(session));
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        }
        return sessions.OrderByDescending(s => s.At).ToList();
    }

    static ClaudeSession Settled(ClaudeSession session)
    {
        if (session.State != ClaudeSessionState.Waiting || session.Transcript is not { } transcript || !File.Exists(transcript)) return session;
        return File.GetLastWriteTimeUtc(transcript) - session.At > Resumed ? session with { State = ClaudeSessionState.Working } : session;
    }

    public static string? WithHooks(string? settings, string exe)
    {
        if (Parse(settings) is not { } root) return null;
        if (root["hooks"] is not JsonObject hooks) root["hooks"] = hooks = [];
        var command = $"\"{exe.Replace('\\', '/').Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`")}\" {HookArgument}";
        foreach (var name in Events)
        {
            if (hooks[name] is not JsonArray entries) hooks[name] = entries = [];
            Strip(entries);
            entries.Add(new JsonObject { ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command }) });
        }
        return root.ToJsonString(Indented);
    }

    public static string? WithoutHooks(string? settings)
    {
        if (Parse(settings) is not { } root) return null;
        if (root["hooks"] is not JsonObject hooks) return root.ToJsonString(Indented);
        foreach (var name in Events)
        {
            if (hooks[name] is not JsonArray entries) continue;
            Strip(entries);
            if (entries.Count == 0) hooks.Remove(name);
        }
        if (hooks.Count == 0) root.Remove("hooks");
        return root.ToJsonString(Indented);
    }

    public static bool HasHooks(string? settings) =>
        Parse(settings)?["hooks"] is JsonObject hooks && Events.All(name => hooks[name] is JsonArray entries && entries.Any(Ours));

    static JsonObject? Parse(string? settings)
    {
        if (string.IsNullOrWhiteSpace(settings)) return [];
        try { return JsonNode.Parse(settings) as JsonObject; }
        catch (JsonException) { return null; }
    }

    static void Strip(JsonArray entries)
    {
        foreach (var ours in entries.Where(Ours).ToList()) entries.Remove(ours);
    }

    static bool Ours(JsonNode? entry) =>
        entry is JsonObject group && group["hooks"] is JsonArray commands
        && commands.Any(c => c is JsonObject hook && hook["command"] is JsonValue value && value.TryGetValue<string>(out var text) && text.Contains(HookArgument));

    public static bool Installed => HasHooks(ReadSettings());

    public static bool Install() => Environment.ProcessPath is { } exe && Rewrite(text => WithHooks(text, exe));

    public static bool Remove() => Rewrite(WithoutHooks);

    static string? ReadSettings()
    {
        try { return File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : ""; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    static bool Rewrite(Func<string?, string?> change)
    {
        if (ReadSettings() is not { } current || change(current) is not { } next) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temp = SettingsPath + ".windock";
            File.WriteAllText(temp, next);
            File.Move(temp, SettingsPath, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
