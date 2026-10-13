using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Windows.Devices.Geolocation;

namespace WinDock.Services;

public enum WeatherStatus { Loading, Ok, NoPlace, NotFound, Offline }

public sealed record WeatherHour(DateTime Time, double Temp, int Code, bool Day);

public sealed record WeatherReport(string Place, double Temp, double Feels, int Code, bool Day, double High, double Low,
    IReadOnlyList<WeatherHour> Hours, DateTime FetchedAt);

// Open-Meteo: free, no key. Only the city name (or the Windows location, when allowed) leaves the PC.
public sealed class Weather
{
    // ponytail: Open-Meteo's free tier allows under 10,000 calls a day (8.64 s each); 9 s stays under it. Its current
    // conditions only change every 15 minutes, so most calls bring the same numbers; raise this if the IP is shared.
    public static readonly TimeSpan Fresh = TimeSpan.FromSeconds(9);
    static readonly TimeSpan Retry = TimeSpan.FromMinutes(2);
    static readonly TimeSpan Limited = TimeSpan.FromHours(1);
    // One gate for every weather widget (a dock per screen can show two), so together they keep to the limit.
    static DateTime nextCall = DateTime.MinValue;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    Task? running;
    DateTime lastTry = DateTime.MinValue;
    string? place;
    (double Lat, double Lon, string Name)? spot;

    public WeatherReport? Last { get; private set; }
    public WeatherStatus Status { get; private set; } = WeatherStatus.Loading;
    public string? Place => place;

    public event Action? Updated;

    public static bool Fahrenheit => !RegionInfo.CurrentRegion.IsMetric;

    public void Tick(DateTime now, string? city)
    {
        city = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        if (city != place)
        {
            place = city;
            spot = null;
            Last = null;
            Status = WeatherStatus.Loading;
            lastTry = DateTime.MinValue;
        }
        if (running is not null || now < nextCall || now - lastTry < (Status == WeatherStatus.Ok ? Fresh : Retry)) return;
        running = Fetch(now);
    }

    public bool Refresh(DateTime now)
    {
        if (running is not null || now < nextCall) return false;
        running = Fetch(now);
        return true;
    }

    async Task Fetch(DateTime now)
    {
        lastTry = now;
        nextCall = now + Fresh;
        var asked = place;
        try
        {
            var found = spot ?? (asked is null ? await Here() : await Find(asked));
            if (asked != place) return;
            if (found is not { } at)
            {
                Status = asked is null ? WeatherStatus.NoPlace : WeatherStatus.NotFound;
                return;
            }
            spot = at;
            var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={at.Lat:0.####}&longitude={at.Lon:0.####}") +
                "&current=temperature_2m,apparent_temperature,weather_code,is_day&hourly=temperature_2m,weather_code,is_day" +
                "&daily=temperature_2m_max,temperature_2m_min&forecast_days=2&timezone=auto" +
                (Fahrenheit ? "&temperature_unit=fahrenheit" : "");
            var report = Parse(await Http.GetStringAsync(url), at.Name, DateTime.Now);
            if (asked != place) return;
            Last = report ?? Last;
            Status = report is null ? WeatherStatus.Offline : WeatherStatus.Ok;
        }
        catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            nextCall = DateTime.Now + Limited;
            Status = WeatherStatus.Offline;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            Status = WeatherStatus.Offline;
        }
        finally
        {
            running = null;
            Updated?.Invoke();
        }
    }

    static async Task<(double, double, string)?> Find(string city)
    {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?count=1&format=json&language={Loc.Instance.Language}&name={Uri.EscapeDataString(city)}";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
        var first = results[0];
        return (first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble(),
            first.TryGetProperty("name", out var name) ? name.GetString() ?? city : city);
    }

    // Only when no city is typed, and only if Windows lets desktop apps read the location.
    static async Task<(double, double, string)?> Here()
    {
        try
        {
            if (await Geolocator.RequestAccessAsync() != GeolocationAccessStatus.Allowed) return null;
            var position = await new Geolocator { DesiredAccuracy = PositionAccuracy.Default }
                .GetGeopositionAsync(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(10));
            var point = position.Coordinate.Point.Position;
            return (point.Latitude, point.Longitude, Loc.T("weatherHere"));
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException or TaskCanceledException)
        {
            return null;
        }
    }

    public static WeatherReport? Parse(string json, string place, DateTime fetchedAt)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("current", out var current)) return null;
        var now = Time(current.GetProperty("time"));
        var hourly = root.GetProperty("hourly");
        var times = hourly.GetProperty("time");
        var temps = hourly.GetProperty("temperature_2m");
        var codes = hourly.GetProperty("weather_code");
        var days = hourly.TryGetProperty("is_day", out var d) ? d : default;
        var hours = new List<WeatherHour>();
        for (var i = 0; i < times.GetArrayLength() && hours.Count < 12; i++)
        {
            var t = Time(times[i]);
            if (t <= now || temps[i].ValueKind != JsonValueKind.Number) continue;
            hours.Add(new WeatherHour(t, temps[i].GetDouble(), codes[i].GetInt32(), days.ValueKind != JsonValueKind.Array || days[i].GetInt32() == 1));
        }
        var daily = root.GetProperty("daily");
        return new WeatherReport(place,
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("apparent_temperature").GetDouble(),
            current.GetProperty("weather_code").GetInt32(),
            current.GetProperty("is_day").GetInt32() == 1,
            daily.GetProperty("temperature_2m_max")[0].GetDouble(),
            daily.GetProperty("temperature_2m_min")[0].GetDouble(),
            hours, fetchedAt);
    }

    static DateTime Time(JsonElement e) => DateTime.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    // WMO weather codes, as Open-Meteo sends them. Glyphs are Segoe UI Symbol.
    public static (string Glyph, string Key) Describe(int code, bool day) => code switch
    {
        0 => (day ? "☀" : "\U0001F319", "weatherClear"),
        1 or 2 => (day ? "⛅" : "\U0001F319", "weatherPartly"),
        3 => ("☁", "weatherCloudy"),
        45 or 48 => ("\U0001F32B", "weatherFog"),
        >= 51 and <= 57 => ("\U0001F327", "weatherDrizzle"),
        >= 61 and <= 67 => ("\U0001F327", "weatherRain"),
        (>= 71 and <= 77) or 85 or 86 => ("❄", "weatherSnow"),
        >= 80 and <= 82 => ("\U0001F326", "weatherShowers"),
        >= 95 => ("⛈", "weatherStorm"),
        _ => ("☁", "weatherCloudy")
    };

    public static string Degrees(double t) => $"{(int)Math.Round(t)}°";
}
