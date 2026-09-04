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
        var uri = $"v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,precipitation,rain,showers,snowfall,weather_code,cloud_cover,wind_speed_10m,wind_direction_10m,is_day&daily=sunrise,sunset&forecast_days=1&timezone=UTC";
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var current = document.RootElement.GetProperty("current");
        var code = current.GetProperty("weather_code").GetInt32();
        var observedAt = DateTimeOffset.TryParse(
            current.GetProperty("time").GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed) ? parsed : DateTimeOffset.UtcNow;
        var daily = document.RootElement.GetProperty("daily");
        var sunrise = ParseUtc(daily.GetProperty("sunrise")[0].GetString());
        var sunset = ParseUtc(daily.GetProperty("sunset")[0].GetString());
        var (moonPhase, moonIllumination) = CalculateMoon(observedAt);

        return new WeatherState(
            Describe(code),
            code,
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("precipitation").GetDouble(),
            current.GetProperty("wind_speed_10m").GetDouble(),
            current.GetProperty("is_day").GetInt32() == 1,
            observedAt,
            "Open-Meteo",
            sunrise,
            sunset,
            moonPhase,
            moonIllumination,
            WindDirectionDegrees: current.GetProperty("wind_direction_10m").GetDouble());
    }

    private static DateTimeOffset? ParseUtc(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : null;

    private static (string Name, double Illumination) CalculateMoon(DateTimeOffset time)
    {
        var knownNewMoon = new DateTimeOffset(2000, 1, 6, 18, 14, 0, TimeSpan.Zero);
        const double synodicMonthDays = 29.530588853;
        var phase = ((time - knownNewMoon).TotalDays % synodicMonthDays + synodicMonthDays) % synodicMonthDays / synodicMonthDays;
        var illumination = (1 - Math.Cos(phase * Math.PI * 2)) / 2;
        var name = phase switch
        {
            < .0625 or >= .9375 => "New moon",
            < .1875 => "Waxing crescent",
            < .3125 => "First quarter",
            < .4375 => "Waxing gibbous",
            < .5625 => "Full moon",
            < .6875 => "Waning gibbous",
            < .8125 => "Last quarter",
            _ => "Waning crescent"
        };
        return (name, illumination);
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
