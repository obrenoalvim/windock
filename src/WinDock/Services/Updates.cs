using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace WinDock.Services;

// Asks GitHub for the tag of the latest release. Nothing is downloaded or installed; the user follows the link.
public static class Updates
{
    public const string Page = "https://github.com/obrenoalvim/windock/releases/latest";
    const string Api = "https://api.github.com/repos/obrenoalvim/windock/releases/latest";

    static readonly HttpClient Http = CreateClient();

    public static Version Current => Plain(Assembly.GetEntryAssembly()?.GetName().Version) ?? new Version(0, 0, 0);

    public static async Task<Version?> LatestAsync()
    {
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(Api));
            return doc.RootElement.TryGetProperty("tag_name", out var tag) ? Parse(tag.GetString()) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { return null; }
    }

    // "v0.4.0" or "0.4.0-beta" → 0.4.0. Three parts, so 0.4.0 equals the assembly's 0.4.0.0.
    public static Version? Parse(string? tag) =>
        tag is { Length: > 0 } && Version.TryParse(tag.TrimStart('v', 'V').Split('-', '+')[0], out var v) ? Plain(v) : null;

    static Version? Plain(Version? v) => v is null ? null : new Version(v.Major, v.Minor, Math.Max(0, v.Build));

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WinDock");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
