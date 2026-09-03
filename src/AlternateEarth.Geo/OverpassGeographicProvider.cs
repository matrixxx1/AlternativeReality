using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public sealed class OverpassGeographicProvider : IGeographicProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly IElevationProvider _elevationProvider;

    public OverpassGeographicProvider(HttpClient httpClient, string cacheDirectory, IElevationProvider elevationProvider)
    {
        _httpClient = httpClient;
        _cacheDirectory = cacheDirectory;
        _elevationProvider = elevationProvider;
        Directory.CreateDirectory(cacheDirectory);
    }

    public string Name => "OpenStreetMap/Overpass";

    public async Task<GeographicDataset> GetAreaAsync(GeographicArea area, CancellationToken cancellationToken = default)
    {
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            FormattableString.Invariant($"v1:{area.Center.Latitude:F6}:{area.Center.Longitude:F6}:{area.SizeMeters}"))))[..16];
        var canonicalCachePath = Path.Combine(_cacheDirectory, $"area-{cacheKey}.json");
        if (File.Exists(canonicalCachePath))
        {
            var cachedJson = await File.ReadAllTextAsync(canonicalCachePath, cancellationToken);
            var cached = JsonSerializer.Deserialize<GeographicDataset>(cachedJson, SharedJson.Options);
            if (cached is not null)
            {
                return cached;
            }
        }

        var rawCachePath = Path.Combine(_cacheDirectory, $"overpass-{cacheKey}.json");
        string rawJson;
        if (File.Exists(rawCachePath))
        {
            rawJson = await File.ReadAllTextAsync(rawCachePath, cancellationToken);
        }
        else
        {
            rawJson = await DownloadOverpassAsync(BuildQuery(area), cancellationToken);
            await File.WriteAllTextAsync(rawCachePath, rawJson, cancellationToken);
        }

        var features = ParseFeatures(rawJson, area);
        IReadOnlyList<ElevationSample> elevation;
        try
        {
            elevation = await _elevationProvider.GetElevationGridAsync(area, 5, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            elevation = FlatElevationProvider.CreateGrid(area, 5);
        }

        var dataset = new GeographicDataset(Name, area, features, elevation, DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(
            canonicalCachePath,
            JsonSerializer.Serialize(dataset, SharedJson.Options),
            cancellationToken);
        return dataset;
    }

    private async Task<string> DownloadOverpassAsync(string query, CancellationToken cancellationToken)
    {
        var configured = new Uri(_httpClient.BaseAddress ?? new Uri("https://overpass-api.de/"), "api/interpreter");
        var endpoints = new[]
        {
            configured,
            new Uri("https://overpass.kumi.systems/api/interpreter"),
            new Uri("https://overpass.nchc.org.tw/api/interpreter")
        }.Distinct().ToArray();
        var errors = new List<string>();
        foreach (var endpoint in endpoints)
        {
            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query });
                using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                errors.Add($"{endpoint.Host}: {(int)response.StatusCode}");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                errors.Add($"{endpoint.Host}: {exception.GetType().Name}");
            }
        }
        throw new HttpRequestException($"All configured Overpass endpoints failed ({string.Join(", ", errors)}). The server can start offline after a successful region cache has been created.");
    }

    private static string BuildQuery(GeographicArea area)
    {
        const double metersPerLatitudeDegree = 111_320.0;
        var half = area.SizeMeters / 2.0;
        var latitudeDelta = half / metersPerLatitudeDegree;
        var longitudeDelta = half / (metersPerLatitudeDegree * Math.Cos(area.Center.Latitude * Math.PI / 180.0));
        var south = area.Center.Latitude - latitudeDelta;
        var north = area.Center.Latitude + latitudeDelta;
        var west = area.Center.Longitude - longitudeDelta;
        var east = area.Center.Longitude + longitudeDelta;
        var bbox = string.Join(',', new[] { south, west, north, east }.Select(v => v.ToString("F7", CultureInfo.InvariantCulture)));

        return $"[out:json][timeout:30];(way[\"highway\"]({bbox});way[\"building\"]({bbox});way[\"natural\"=\"water\"]({bbox});way[\"waterway\"]({bbox}););out body;>;out skel qt;";
    }

    private static IReadOnlyList<CanonicalEntity> ParseFeatures(string rawJson, GeographicArea area)
    {
        using var document = JsonDocument.Parse(rawJson);
        var nodes = new Dictionary<long, GeoCoordinate>();
        var ways = new List<(long Id, long[] Nodes, Dictionary<string, string> Tags)>();

        foreach (var element in document.RootElement.GetProperty("elements").EnumerateArray())
        {
            var type = element.GetProperty("type").GetString();
            if (type == "node" && element.TryGetProperty("lat", out var latitude) && element.TryGetProperty("lon", out var longitude))
            {
                nodes[element.GetProperty("id").GetInt64()] = new GeoCoordinate(latitude.GetDouble(), longitude.GetDouble());
            }
            else if (type == "way" && element.TryGetProperty("nodes", out var nodeArray))
            {
                var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (element.TryGetProperty("tags", out var tagObject))
                {
                    foreach (var tag in tagObject.EnumerateObject())
                    {
                        tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                    }
                }

                ways.Add((
                    element.GetProperty("id").GetInt64(),
                    nodeArray.EnumerateArray().Select(value => value.GetInt64()).ToArray(),
                    tags));
            }
        }

        var projection = new LocalTangentProjection(area.Region);
        var result = new List<CanonicalEntity>();
        foreach (var way in ways)
        {
            var kind = Classify(way.Tags);
            if (kind is null)
            {
                continue;
            }

            var geometry = way.Nodes
                .Where(nodes.ContainsKey)
                .Select(nodeId => projection.Project(nodes[nodeId]))
                .Where(position => area.Bounds.Contains(position.X, position.Y))
                .Select(position => new GeometryPoint(position.X, position.Y, position.Z))
                .ToArray();
            if (geometry.Length < 2)
            {
                continue;
            }

            var centerX = geometry.Average(point => point.X);
            var centerY = geometry.Average(point => point.Y);
            var properties = way.Tags
                .Where(pair => pair.Key is "name" or "highway" or "building" or "natural" or "waterway" or "surface" or "levels")
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            result.Add(new CanonicalEntity(
                $"geo:osm:way:{way.Id}",
                kind.Value,
                new WorldPosition(area.Region, centerX, centerY),
                geometry,
                properties));
        }

        return result;
    }

    private static EntityKind? Classify(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.ContainsKey("highway")) return EntityKind.Road;
        if (tags.ContainsKey("building")) return EntityKind.Building;
        if (tags.TryGetValue("natural", out var natural) && natural == "water") return EntityKind.Water;
        if (tags.ContainsKey("waterway")) return EntityKind.Water;
        return null;
    }
}
