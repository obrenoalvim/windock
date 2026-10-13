using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WinDock.Services;

public enum ClaudeStatus { Loading, Ok, NoLogin, Expired, Offline, RateLimited, Failed }

public sealed record ClaudeLimits(double? Session, DateTime? SessionReset, double? Week, DateTime? WeekReset, DateTime FetchedAt);

public sealed record ClaudeLogin(string Token, DateTime? Expires, string? Plan);

public sealed class ClaudeUsage
{
    public static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5);
    public static readonly TimeSpan WeekWindow = TimeSpan.FromDays(7);
    static readonly TimeSpan Fresh = TimeSpan.FromMinutes(5);
    static readonly TimeSpan Manual = TimeSpan.FromSeconds(60);
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    Task? running;
    DateTime lastTry = DateTime.MinValue;
    DateTime retryAfter = DateTime.MinValue;
    int backoff;

    public static ClaudeUsage Shared { get; } = new();

    public ClaudeLimits? Last { get; private set; }
    public ClaudeStatus Status { get; private set; } = ClaudeStatus.Loading;
    public string? Plan { get; private set; }

    public event Action? Updated;

    public static string CredentialsPath =>
        Path.Combine(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), ".credentials.json");

    FileSystemWatcher? watcher;
    volatile bool loginChanged;

    // Claude Code renews its sign-in only when it is used; ask again as soon as it writes the new one.
    void Watch()
    {
        if (watcher is not null || Path.GetDirectoryName(CredentialsPath) is not { } dir || !Directory.Exists(dir)) return;
        watcher = new FileSystemWatcher(dir, Path.GetFileName(CredentialsPath)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        watcher.Changed += (_, _) => loginChanged = true;
        watcher.Created += (_, _) => loginChanged = true;
        watcher.Renamed += (_, _) => loginChanged = true;
        watcher.EnableRaisingEvents = true;
    }

    public void Tick(DateTime now)
    {
        Watch();
        if (loginChanged && Status is ClaudeStatus.NoLogin or ClaudeStatus.Expired or ClaudeStatus.Failed or ClaudeStatus.Loading)
        {
            loginChanged = false;
            lastTry = DateTime.MinValue;
        }
        if (running is not null || now < retryAfter) return;
        if (Last is not null && Status == ClaudeStatus.Ok && now - Last.FetchedAt < Fresh) return;
        if (now - lastTry < (Status == ClaudeStatus.Ok ? Fresh : Manual)) return;
        running = Fetch(now);
    }

    public bool Refresh(DateTime now)
    {
        if (running is not null || now < retryAfter || now - lastTry < Manual) return false;
        running = Fetch(now);
        return true;
    }

    async Task Fetch(DateTime now)
    {
        lastTry = now;
        try
        {
            var login = ReadLogin(CredentialsPath);
            Plan = login?.Plan ?? Plan;
            if (login is null)
            {
                Status = ClaudeStatus.NoLogin;
                return;
            }
            if (login.Expires is { } expires && expires <= DateTime.UtcNow)
            {
                Status = ClaudeStatus.Expired;
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
            request.Headers.UserAgent.ParseAdd("WinDock/" + (typeof(ClaudeUsage).Assembly.GetName().Version?.ToString(3) ?? "0"));
            using var response = await Http.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                backoff = Math.Min(backoff + 1, 4);
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(Math.Pow(2, backoff - 1));
                retryAfter = DateTime.Now + (wait > TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : wait);
                Status = ClaudeStatus.RateLimited;
                return;
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Status = ClaudeStatus.Expired;
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                Status = ClaudeStatus.Failed;
                return;
            }
            var limits = Parse(await response.Content.ReadAsStringAsync(), DateTime.Now);
            if (limits is null)
            {
                Status = ClaudeStatus.Failed;
                return;
            }
            backoff = 0;
            Last = limits;
            Status = ClaudeStatus.Ok;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            Status = ClaudeStatus.Offline;
        }
        finally
        {
            running = null;
            Updated?.Invoke();
        }
    }

    public static ClaudeLogin? ReadLogin(string path)
    {
        try { return ParseLogin(File.ReadAllText(path)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public static ClaudeLogin? ParseLogin(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth)) return null;
            var token = oauth.TryGetProperty("accessToken", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (string.IsNullOrEmpty(token)) return null;
            DateTime? expires = oauth.TryGetProperty("expiresAt", out var e) && e.TryGetInt64(out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                : null;
            var plan = oauth.TryGetProperty("subscriptionType", out var s) && s.ValueKind == JsonValueKind.String ? PlanName(s.GetString()) : null;
            return new ClaudeLogin(token, expires, plan);
        }
        catch (JsonException) { return null; }
    }

    public static string? PlanName(string? subscription)
    {
        if (string.IsNullOrWhiteSpace(subscription)) return null;
        var lower = subscription.ToLowerInvariant();
        if (lower.Contains("max")) return "Max";
        if (lower.Contains("pro")) return "Pro";
        if (lower.Contains("team")) return "Team";
        if (lower.Contains("enterprise")) return "Enterprise";
        return lower.Contains("api") ? null : char.ToUpperInvariant(subscription[0]) + subscription[1..];
    }

    public static ClaudeLimits? Parse(string json, DateTime now)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var (session, sessionReset) = Window(root, "five_hour");
            var (week, weekReset) = Window(root, "seven_day");
            return session is null && week is null ? null : new ClaudeLimits(session, sessionReset, week, weekReset, now);
        }
        catch (JsonException) { return null; }
    }

    static (double?, DateTime?) Window(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object) return (null, null);
        double? used = w.TryGetProperty("utilization", out var u) && u.TryGetDouble(out var value) && double.IsFinite(value)
            ? Math.Clamp(value, 0, 100)
            : null;
        DateTime? reset = w.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(r.GetString(), out var at)
            ? at.LocalDateTime
            : null;
        return (used, reset);
    }

    public static double? Projected(double used, DateTime? reset, TimeSpan window, DateTime now)
    {
        if (reset is not { } end) return null;
        var elapsed = window - (end - now);
        if (elapsed <= TimeSpan.FromMinutes(30) || elapsed > window) return null;
        return used * (window / elapsed);
    }
}
