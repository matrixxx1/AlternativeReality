using System.Globalization;
using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private readonly HttpClient _httpClient;

    public OpenMeteoWeatherProvider(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<WeatherState> GetCurrentAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var latitude = coordinate.Latitude.ToString("F6", CultureInfo.InvariantCulture);
        var longitude = coordinate.Longitude.ToString("F6", CultureInfo.InvariantCulture);
        var uri = $"v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,precipitation,rain,showers,snowfall,weather_code,cloud_cover,wind_speed_10m,is_day&timezone=UTC";
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var current = document.RootElement.GetProperty("current");
        var code = current.GetProperty("weather_code").GetInt32();
        var observedAt = DateTimeOffset.TryParse(
            current.GetProperty("time").GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed) ? parsed : DateTimeOffset.UtcNow;

        return new WeatherState(
            Describe(code),
            code,
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("precipitation").GetDouble(),
            current.GetProperty("wind_speed_10m").GetDouble(),
            current.GetProperty("is_day").GetInt32() == 1,
            observedAt,
            "Open-Meteo");
    }

    private static string Describe(int code) => code switch
    {
        0 => "Clear",
        1 => "Mostly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 or 56 or 57 => "Drizzle",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "Rain",
        71 or 73 or 75 or 77 or 85 or 86 => "Snow",
        95 or 96 or 99 => "Thunderstorm",
        _ => "Variable"
    };
}
