using System.Globalization;
using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public sealed class OpenTopoDataElevationProvider : IElevationProvider
{
    private readonly HttpClient _httpClient;

    public OpenTopoDataElevationProvider(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<ElevationSample>> GetElevationGridAsync(
        GeographicArea area,
        int samplesPerAxis,
        CancellationToken cancellationToken = default)
    {
        var projection = new LocalTangentProjection(area.Region);
        var bounds = area.Bounds;
        var positions = new List<WorldPosition>();
        var locations = new List<string>();
        for (var y = 0; y < samplesPerAxis; y++)
        {
            for (var x = 0; x < samplesPerAxis; x++)
            {
                var worldX = Lerp(bounds.MinimumX, bounds.MaximumX, x / (double)(samplesPerAxis - 1));
                var worldY = Lerp(bounds.MinimumY, bounds.MaximumY, y / (double)(samplesPerAxis - 1));
                var position = new WorldPosition(area.Region, worldX, worldY);
                var geo = projection.Unproject(position);
                positions.Add(position);
                locations.Add(FormattableString.Invariant($"{geo.Latitude:F6},{geo.Longitude:F6}"));
            }
        }

        var uri = $"v1/srtm90m?locations={Uri.EscapeDataString(string.Join('|', locations))}";
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
        if (results.Length != positions.Count)
        {
            throw new InvalidDataException("Elevation provider returned an unexpected number of samples.");
        }

        return positions.Select((position, index) => new ElevationSample(
            position.X,
            position.Y,
            results[index].GetProperty("elevation").ValueKind == JsonValueKind.Null
                ? 0
                : results[index].GetProperty("elevation").GetDouble())).ToArray();
    }

    private static double Lerp(double start, double end, double amount) => start + ((end - start) * amount);
}

public sealed class FlatElevationProvider : IElevationProvider
{
    public Task<IReadOnlyList<ElevationSample>> GetElevationGridAsync(
        GeographicArea area,
        int samplesPerAxis,
        CancellationToken cancellationToken = default) => Task.FromResult(CreateGrid(area, samplesPerAxis));

    public static IReadOnlyList<ElevationSample> CreateGrid(GeographicArea area, int samplesPerAxis)
    {
        var bounds = area.Bounds;
        var samples = new List<ElevationSample>();
        for (var y = 0; y < samplesPerAxis; y++)
        {
            for (var x = 0; x < samplesPerAxis; x++)
            {
                samples.Add(new ElevationSample(
                    bounds.MinimumX + ((bounds.MaximumX - bounds.MinimumX) * x / (samplesPerAxis - 1.0)),
                    bounds.MinimumY + ((bounds.MaximumY - bounds.MinimumY) * y / (samplesPerAxis - 1.0)),
                    0));
            }
        }

        return samples;
    }
}
