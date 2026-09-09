using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public sealed class OverpassGeographicProvider : IGeographicProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _legacyCacheDirectory;
    private readonly IElevationProvider _elevationProvider;

    public OverpassGeographicProvider(HttpClient httpClient, string cacheDirectory, IElevationProvider elevationProvider)
    {
        _httpClient = httpClient;
        _legacyCacheDirectory = cacheDirectory;
        _elevationProvider = elevationProvider;
        Directory.CreateDirectory(cacheDirectory);
    }

    public string Name => "OpenStreetMap/Overpass";

    public Task<GeographicDataset> GetAreaAsync(GeographicArea area, CancellationToken cancellationToken = default) => ReadAreaAsync(area, false, cancellationToken);
    public Task<GeographicDataset> GetFreshAreaAsync(GeographicArea area, CancellationToken cancellationToken = default) => ReadAreaAsync(area, true, cancellationToken);
    public void ClearLegacyCache()
    {
        foreach (var pattern in new[] { "area-*.json", "overpass-*.json" })
            foreach (var file in Directory.EnumerateFiles(_legacyCacheDirectory, pattern)) File.Delete(file);
    }
    private async Task<GeographicDataset> ReadAreaAsync(GeographicArea area, bool fresh, CancellationToken cancellationToken)
    {
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            FormattableString.Invariant($"v5:{area.Center.Latitude:F6}:{area.Center.Longitude:F6}:{area.SizeMeters}"))))[..16];
        var canonicalCachePath = Path.Combine(_legacyCacheDirectory, $"area-{cacheKey}.json");
        if (!fresh && File.Exists(canonicalCachePath))
        {
            var cachedJson = await File.ReadAllTextAsync(canonicalCachePath, cancellationToken);
            var cached = JsonSerializer.Deserialize<GeographicDataset>(cachedJson, SharedJson.Options);
            if (cached is not null)
            {
                TryDelete(canonicalCachePath);
                return cached with { Features = NormalizeMerchantCategories(cached.Features) };
            }
        }

        var rawCachePath = Path.Combine(_legacyCacheDirectory, $"overpass-{cacheKey}.json");
        string rawJson;
        if (!fresh && File.Exists(rawCachePath))
        {
            rawJson = await File.ReadAllTextAsync(rawCachePath, cancellationToken);
            TryDelete(rawCachePath);
        }
        else
        {
            rawJson = await DownloadOverpassAsync(BuildQuery(area), cancellationToken);
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

        return new GeographicDataset(Name, area, features, elevation, DateTimeOffset.UtcNow);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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
        // Overlap queries so sparse road nodes just beyond a block are included.
        var half = area.SizeMeters / 2.0 + 500;
        var latitudeDelta = half / metersPerLatitudeDegree;
        var longitudeDelta = half / (metersPerLatitudeDegree * Math.Cos(area.Center.Latitude * Math.PI / 180.0));
        var south = area.Center.Latitude - latitudeDelta;
        var north = area.Center.Latitude + latitudeDelta;
        var west = area.Center.Longitude - longitudeDelta;
        var east = area.Center.Longitude + longitudeDelta;
        var bbox = string.Join(',', new[] { south, west, north, east }.Select(v => v.ToString("F7", CultureInfo.InvariantCulture)));

        return $"[out:json][timeout:45];(" +
               $"way[\"highway\"]({bbox});" +
               $"way[\"building\"]({bbox});" +
               $"way[\"barrier\"=\"fence\"]({bbox});" +
               $"way[\"natural\"~\"water|wood|sand|beach|mud|wetland|grassland\"]({bbox});" +
               $"way[\"waterway\"]({bbox});" +
               $"way[\"landuse\"]({bbox});" +
               $"way[\"leisure\"~\"park|garden|recreation_ground\"]({bbox});" +
               $"way[\"amenity\"=\"parking\"]({bbox});" +
               $"nwr[\"amenity\"~\"^(fuel|restaurant|fast_food|food_court|cafe|ice_cream)$\"]({bbox});" +
               $"nwr[\"shop\"]({bbox});" +
               $"nwr[\"takeaway\"~\"^(yes|only)$\"]({bbox});" +
               $"nwr[\"delivery\"=\"yes\"]({bbox});" +
               $"nwr[\"aeroway\"=\"aerodrome\"]({bbox});" +
               $"way[\"boundary\"~\"parcel|lot|cadastral\"]({bbox});" +
               $"way[\"boundary\"=\"administrative\"][\"admin_level\"=\"4\"]({bbox});" +
               ");(._;rel(bw)[\"type\"=\"restriction\"];);out body;>;out skel qt;";
    }

    private static IReadOnlyList<CanonicalEntity> ParseFeatures(string rawJson, GeographicArea area)
    {
        using var document = JsonDocument.Parse(rawJson);
        var nodes = new Dictionary<long, GeoCoordinate>();
        var taggedNodes = new List<(long Id, GeoCoordinate Coordinate, Dictionary<string, string> Tags)>();
        var ways = new List<(long Id, long[] Nodes, Dictionary<string, string> Tags)>();
        var relations = new List<(long Id, long[] Ways, Dictionary<string, string> Tags)>();
        var turnRestrictions = new List<RoadTurnRestriction>();

        foreach (var element in document.RootElement.GetProperty("elements").EnumerateArray())
        {
            var type = element.GetProperty("type").GetString();
            if (type == "node" && element.TryGetProperty("lat", out var latitude) && element.TryGetProperty("lon", out var longitude))
            {
                var id = element.GetProperty("id").GetInt64();
                var coordinate = new GeoCoordinate(latitude.GetDouble(), longitude.GetDouble());
                nodes[id] = coordinate;
                var tags = ReadTags(element);
                if (tags.Count > 0) taggedNodes.Add((id, coordinate, tags));
            }
            else if (type == "way" && element.TryGetProperty("nodes", out var nodeArray))
            {
                ways.Add((
                    element.GetProperty("id").GetInt64(),
                    nodeArray.EnumerateArray().Select(value => value.GetInt64()).ToArray(),
                    ReadTags(element)));
            }
            else if (type == "relation" && element.TryGetProperty("members", out var members))
            {
                var tags = ReadTags(element);
                if (tags.GetValueOrDefault("type") == "restriction" &&
                    !(tags.GetValueOrDefault("except") ?? "").Split(';').Any(value => value.Trim() is "bus" or "psv"))
                {
                    var rule = tags.GetValueOrDefault("restriction:bus") ?? tags.GetValueOrDefault("restriction:psv") ?? tags.GetValueOrDefault("restriction");
                    var from = members.EnumerateArray().FirstOrDefault(m => m.GetProperty("role").GetString() == "from" && m.GetProperty("type").GetString() == "way");
                    var to = members.EnumerateArray().FirstOrDefault(m => m.GetProperty("role").GetString() == "to" && m.GetProperty("type").GetString() == "way");
                    var via = members.EnumerateArray().FirstOrDefault(m => m.GetProperty("role").GetString() == "via" && m.GetProperty("type").GetString() == "node");
                    if (rule is not null && from.ValueKind != JsonValueKind.Undefined && to.ValueKind != JsonValueKind.Undefined && via.ValueKind != JsonValueKind.Undefined)
                        turnRestrictions.Add(new($"geo:osm:way:{from.GetProperty("ref").GetInt64()}", $"geo:osm:way:{to.GetProperty("ref").GetInt64()}", $"osm:{via.GetProperty("ref").GetInt64()}", rule));
                }
                relations.Add((element.GetProperty("id").GetInt64(), members.EnumerateArray()
                    .Where(member => member.GetProperty("type").GetString() == "way")
                    .Select(member => member.GetProperty("ref").GetInt64()).ToArray(), ReadTags(element)));
            }
        }

        // Overpass can return the same way once with tags from `out body` and again
        // as an untagged skeleton from the recursive node expansion. Keep one
        // deterministic, information-rich representation per OSM way ID.
        var waysById = ways
            .GroupBy(way => way.Id)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(way => way.Tags.Count).ThenByDescending(way => way.Nodes.Length).First());

        var projection = new LocalTangentProjection(area.Region);
        var result = new List<CanonicalEntity>();
        foreach (var way in waysById.Values)
        {
            var kind = Classify(way.Tags);
            if (kind is null)
            {
                continue;
            }

            var geometry = way.Nodes
                .Where(nodeId => nodes.ContainsKey(nodeId) && RegionId.FromGeo(nodes[nodeId]) == area.Region)
                .Select(nodeId => projection.Project(nodes[nodeId]))
                .Where(position => kind is EntityKind.Road or EntityKind.Sidewalk || area.Bounds.Contains(position.X, position.Y))
                .Select(position => new GeometryPoint(position.X, position.Y, position.Z))
                .ToArray();
            if (geometry.Length < 2)
            {
                continue;
            }

            var centerX = geometry.Average(point => point.X);
            var centerY = geometry.Average(point => point.Y);
            var properties = way.Tags
                .Where(pair => KeepProperty(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            AddDerivedProperties(properties, way.Tags);
            var restrictions = turnRestrictions.Where(r => r.FromWayId == $"geo:osm:way:{way.Id}").ToArray();
            if (restrictions.Length > 0) properties["turnRestrictions"] = JsonSerializer.Serialize(restrictions, SharedJson.Options);
            if (kind == EntityKind.StateBoundary) properties["stateName"] = way.Tags.GetValueOrDefault("name") ?? "State boundary";
            if (kind == EntityKind.Terrain)
            {
                properties["terrain"] = ClassifyTerrain(way.Tags).ToString().ToLowerInvariant();
            }
            if (kind == EntityKind.Road || kind == EntityKind.Sidewalk)
            {
                properties["widthMeters"] = EstimateWidthMeters(way.Tags).ToString("F1", CultureInfo.InvariantCulture);
                properties["osmNodeIds"] = string.Join(',', way.Nodes.Where(nodeId => nodes.ContainsKey(nodeId) && RegionId.FromGeo(nodes[nodeId]) == area.Region));
                properties["roadClass"] = RoadClassification.Classify(properties);
            }
            result.Add(new CanonicalEntity(
                $"geo:osm:way:{way.Id}",
                kind.Value,
                new WorldPosition(area.Region, centerX, centerY),
                geometry,
                properties));
        }

        foreach (var node in taggedNodes)
        {
            if (Classify(node.Tags) != EntityKind.PointOfInterest) continue;
            if (RegionId.FromGeo(node.Coordinate) != area.Region) continue;
            var position = projection.Project(node.Coordinate);
            if (!area.Bounds.Contains(position.X, position.Y)) continue;
            var properties = node.Tags.Where(pair => KeepProperty(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            AddDerivedProperties(properties, node.Tags);
            result.Add(new CanonicalEntity($"geo:osm:node:{node.Id}", EntityKind.PointOfInterest, position, Array.Empty<GeometryPoint>(), properties));
        }

        foreach (var relation in relations.Where(relation => relation.Tags.GetValueOrDefault("boundary") == "administrative" && relation.Tags.GetValueOrDefault("admin_level") == "4" || relation.Tags.GetValueOrDefault("aeroway") == "aerodrome"))
        {
            var kind = relation.Tags.GetValueOrDefault("aeroway") == "aerodrome" ? EntityKind.Airport : EntityKind.StateBoundary;
            foreach (var wayId in relation.Ways.Distinct())
            {
                if (!waysById.TryGetValue(wayId, out var member)) continue;
                var geometry = member.Nodes.Where(nodeId => nodes.ContainsKey(nodeId) && RegionId.FromGeo(nodes[nodeId]) == area.Region).Select(nodeId => projection.Project(nodes[nodeId]))
                    .Where(position => area.Bounds.Contains(position.X, position.Y)).Select(position => new GeometryPoint(position.X, position.Y, position.Z)).ToArray();
                if (geometry.Length < 2) continue;
                var properties = relation.Tags.Where(pair => KeepProperty(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                if (kind == EntityKind.StateBoundary) properties["stateName"] = relation.Tags.GetValueOrDefault("name") ?? "State boundary";
                result.Add(new CanonicalEntity($"geo:osm:relation:{relation.Id}:way:{wayId}", kind,
                    new WorldPosition(area.Region, geometry.Average(point => point.X), geometry.Average(point => point.Y)), geometry, properties));
            }
        }

        return result;
    }

    private static Dictionary<string, string> ReadTags(JsonElement element)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!element.TryGetProperty("tags", out var tagObject)) return tags;
        foreach (var tag in tagObject.EnumerateObject()) tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
        return tags;
    }

    private static bool KeepProperty(string key) => key is "service" or "office" or "building:use" or "oneway" or "junction" or "lanes" or "maxspeed" or "ref" or "layer" or "bridge" or "tunnel" or "access" or "vehicle" or "motor_vehicle" or "bus" or "motorroad" or "area" or "cuisine" or "takeaway" or "delivery" or "name" or "brand" or "highway" or "building" or "building:levels" or "natural" or "waterway" or "surface" or "levels" or "landuse" or "leisure" or "amenity" or "barrier" or "footway" or "sidewalk" or "width" or "shop" or "aeroway" or "iata" or "icao" or "boundary" or "admin_level" || key.StartsWith("addr:", StringComparison.OrdinalIgnoreCase);

    private static void AddDerivedProperties(Dictionary<string, string> properties, IReadOnlyDictionary<string, string> tags)
    {
        var category = MerchantCategory(tags);
        if (category is not null) properties["merchantCategory"] = category;
    }

    private static IReadOnlyList<CanonicalEntity> NormalizeMerchantCategories(IReadOnlyList<CanonicalEntity> features) => features.Select(entity =>
    {
        var category = MerchantCategory(entity.Properties);
        if (category is null || entity.Properties.GetValueOrDefault("merchantCategory") == category) return entity;
        var properties = new Dictionary<string, string>(entity.Properties, StringComparer.OrdinalIgnoreCase) { ["merchantCategory"] = category };
        return entity with { Properties = properties };
    }).ToArray();

    private static string? MerchantCategory(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.GetValueOrDefault("amenity") == "fuel") return "gas";
        if (FoodBusinesses.OffersDelivery(tags)) return "food";
        if (!tags.TryGetValue("shop", out var shop)) return null;
        if (shop is "furniture" or "interior_decoration" or "bed" or "carpet") return "furniture";
        if (shop is "clothes" or "fashion" or "shoes" or "tailor") return "clothing";
        if (shop is "supermarket" or "grocery" or "bakery" or "butcher" or "greengrocer" or "deli") return "food";
        if (shop == "convenience") return "convenience";
        if (shop is "hardware" or "doityourself" or "tools") return "hardware";
        if (shop is "sports" or "outdoor" or "hunting" or "fishing" or "bicycle") return "sportingGoods";
        if (shop is "weapons" or "gun") return "weapons";
        if (shop is "car" or "motorcycle") return "vehicles";
        return "general";
    }

    private static EntityKind? Classify(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.GetValueOrDefault("aeroway") == "aerodrome") return EntityKind.Airport;
        if (tags.TryGetValue("boundary", out var boundary) && boundary is "parcel" or "lot" or "cadastral") return EntityKind.PropertyBoundary;
        if (boundary == "administrative" && tags.GetValueOrDefault("admin_level") == "4") return EntityKind.StateBoundary;
        if (tags.TryGetValue("natural", out var natural) && natural == "water") return EntityKind.Water;
        if (tags.ContainsKey("waterway")) return EntityKind.Water;
        if (tags.ContainsKey("building")) return EntityKind.Building;
        if (tags.ContainsKey("shop") || FoodBusinesses.OffersDelivery(tags) || tags.GetValueOrDefault("amenity") == "fuel") return EntityKind.PointOfInterest;
        if (tags.TryGetValue("barrier", out var barrier) && barrier == "fence") return EntityKind.Fence;
        if (tags.TryGetValue("highway", out var highway))
            return highway is "footway" or "pedestrian" or "steps" ? EntityKind.Sidewalk : EntityKind.Road;
        if (tags.ContainsKey("landuse") || tags.ContainsKey("leisure") || tags.ContainsKey("amenity") ||
            tags.TryGetValue("natural", out natural) && natural is "wood" or "sand" or "beach" or "mud" or "wetland" or "grassland")
            return EntityKind.Terrain;
        return null;
    }

    private static TerrainType ClassifyTerrain(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("natural", out var natural))
        {
            if (natural is "sand" or "beach") return TerrainType.Sand;
            if (natural is "mud" or "wetland") return TerrainType.Mud;
            if (natural == "wood") return TerrainType.Forest;
        }
        if (tags.TryGetValue("landuse", out var landuse))
        {
            if (landuse is "forest" or "orchard") return TerrainType.Forest;
            if (landuse is "industrial" or "commercial" or "retail" or "construction" or "railway") return TerrainType.Pavement;
        }
        if (tags.TryGetValue("amenity", out var amenity) && amenity == "parking") return TerrainType.Pavement;
        return TerrainType.Grass;
    }

    private static double EstimateWidthMeters(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("width", out var width) && double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return Math.Clamp(parsed, 0.8, 30);
        if (!tags.TryGetValue("highway", out var highway)) return 2;
        return highway switch
        {
            "motorway" or "trunk" => 12,
            "primary" or "secondary" => 8,
            "tertiary" => 7,
            "residential" or "unclassified" => 6,
            "service" => 4.5,
            "cycleway" => 2.5,
            "footway" or "pedestrian" or "steps" => 2,
            "path" or "track" => 1.5,
            _ => 4
        };
    }
}
