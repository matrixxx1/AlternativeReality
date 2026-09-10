using System.Net;
using System.Text.Json;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class WaterImportTests
{
    private static readonly GeographicArea Area = new(new(45.5, -122.5), 500);
    private const string River = """
    {"elements":[
    {"type":"node","id":1,"lat":45.49,"lon":-122.51},
    {"type":"node","id":2,"lat":45.49,"lon":-122.49},
    {"type":"node","id":3,"lat":45.51,"lon":-122.49},
    {"type":"node","id":4,"lat":45.51,"lon":-122.51},
    {"type":"node","id":5,"lat":45.4995,"lon":-122.5005},
    {"type":"node","id":6,"lat":45.4995,"lon":-122.4995},
    {"type":"node","id":7,"lat":45.5005,"lon":-122.4995},
    {"type":"node","id":8,"lat":45.5005,"lon":-122.5005},
    {"type":"way","id":10,"nodes":[1,2],"tags":{"natural":"water"}},
    {"type":"way","id":11,"nodes":[3,2]},
    {"type":"way","id":12,"nodes":[3,4]},
    {"type":"way","id":13,"nodes":[1,4]},
    {"type":"way","id":14,"nodes":[5,6,7,8,5],"tags":{"natural":"water"}},
    {"type":"way","id":10,"nodes":[1,2]},
    {"type":"relation","id":20,"tags":{"type":"multipolygon","natural":"water","water":"river","waterway:name":"Columbia River"},"members":[
    {"type":"way","ref":12,"role":"outer"},{"type":"way","ref":14,"role":"inner"},
    {"type":"way","ref":10,"role":"outer"},{"type":"way","ref":13,"role":"outer"},{"type":"way","ref":11,"role":"outer"}]}
    ]}
    """;

    [Fact]
    public async Task AssemblesReversedUnorderedBanksWithoutFloodingIslandOrCreatingBlockSeams()
    {
        var a = Assert.Single((await Import(River, Area)).Features);
        var b = Assert.Single((await Import(River, new(new(45.5, -122.498), 500))).Features);
        Assert.Equal(a.Id, b.Id); Assert.Equal(a.Geometry, b.Geometry);
        Assert.Equal(5, a.Geometry.Count); Assert.Equal(a.Geometry[0], a.Geometry[^1]);
        Assert.Single(a.InteriorRings!); Assert.Equal("river", a.Properties["water"]);
        Assert.All(a.Geometry, p => Assert.False(Area.Bounds.Contains(p.X, p.Y)));
        var navigation = new WorldNavigation(Area.Bounds, [a], []);
        Assert.Equal(TerrainType.Grass, navigation.TerrainAt(0, 0));
        Assert.Equal(TerrainType.DeepWater, navigation.TerrainAt(150, 0));
        var islandShore = a.InteriorRings![0].Max(p => p.X);
        Assert.Equal(TerrainType.ShallowWater, navigation.TerrainAt(islandShore + 1, 0));
        Assert.Equal(TerrainType.Grass, navigation.TerrainAt(2000, 0));
        var roundTrip = JsonSerializer.Deserialize<CanonicalEntity>(JsonSerializer.Serialize(a, SharedJson.Options), SharedJson.Options)!;
        Assert.False(WaterGeometry.Contains(roundTrip, 0, 0));
    }

    [Fact]
    public async Task SeparateOuterRingsDoNotConnectAcrossDryLand()
    {
        var json = River.Replace("\"elements\":[", """
        "elements":[
        {"type":"node","id":21,"lat":45.52,"lon":-122.501},
        {"type":"node","id":22,"lat":45.52,"lon":-122.499},
        {"type":"node","id":23,"lat":45.522,"lon":-122.499},
        {"type":"node","id":24,"lat":45.522,"lon":-122.501},
        {"type":"way","id":30,"nodes":[21,22,23,24,21]},
        """).Replace("\"members\":[", "\"members\":[{\"type\":\"way\",\"ref\":30,\"role\":\"outer\"},");
        var area = new GeographicArea(Area.Center, 6000);
        var water = (await Import(json, area)).Features;
        Assert.Equal(2, water.Count);
        Assert.Single(water, w => w.InteriorRings?.Count == 1);
        var dryGap = new LocalTangentProjection(area.Region).Project(new(45.515, -122.5));
        Assert.DoesNotContain(water, w => WaterGeometry.Contains(w, dryGap.X, dryGap.Y));
    }

    [Fact]
    public async Task MissingBankDoesNotBecomeInventedClosedPolygon()
    {
        var dataset = await Import(River.Replace("\"ref\":11", "\"ref\":999"), Area);
        Assert.DoesNotContain(dataset.Features, e => e.Id.StartsWith("geo:osm:relation:"));
    }

    [Fact]
    public async Task KeepsStandaloneClosedWaterAcrossBlockAndGeographicRegionBoundaries()
    {
        const string json = """
        {"elements":[
        {"type":"node","id":1,"lat":45.99,"lon":-122.51},
        {"type":"node","id":2,"lat":45.99,"lon":-122.49},
        {"type":"node","id":3,"lat":46.01,"lon":-122.49},
        {"type":"node","id":4,"lat":46.01,"lon":-122.51},
        {"type":"way","id":10,"nodes":[1,2,3,4,1],"tags":{"natural":"water"}}]}
        """;
        var area = new GeographicArea(new(45.999, -122.5), 500);
        var water = Assert.Single((await Import(json, area)).Features);
        Assert.Equal(5, water.Geometry.Count);
        var center = new LocalTangentProjection(area.Region).Project(area.Center);
        Assert.True(WaterGeometry.Contains(water, center.X, center.Y));
    }

    [Fact]
    public async Task ResourcesAndLandActorsStayOnIslandInsteadOfSpawningInRiver()
    {
        var water = Assert.Single((await Import(River, Area)).Features);
        var reality = new RealityConfiguration("water", "Water", 123, Area);
        var resources = DeterministicWorldGenerator.GenerateResourceNodes(reality, 20, [water]);
        Assert.NotEmpty(resources);
        Assert.All(resources, e => Assert.False(WaterGeometry.Contains(water, e.Position.X, e.Position.Y)));
        Assert.All(DeterministicWorldGenerator.GenerateActors(reality, [water]), e => Assert.False(WaterGeometry.Contains(water, e.Position.X, e.Position.Y)));
    }

    [Fact]
    public async Task BridgesRemainRoadRegardlessOfWaterOrdering()
    {
        var water = Assert.Single((await Import(River, Area)).Features);
        var bridge = new CanonicalEntity("bridge", EntityKind.Road, new(Area.Region, 150, 0), [new(150, -200), new(150, 200)],
            new Dictionary<string, string> { ["bridge"] = "yes", ["widthMeters"] = "10" });
        foreach (var entities in new[] { new[] { water, bridge }, new[] { bridge, water } })
        {
            var navigation = new WorldNavigation(Area.Bounds, entities, []);
            Assert.Equal(TerrainType.Road, navigation.TerrainAt(150, 0));
            Assert.Equal(TerrainType.DeepWater, navigation.TerrainAt(165, 0));
        }
        var sidewalk = new CanonicalEntity("bridge-path", EntityKind.Sidewalk, bridge.Position,
            [new(160,-200), new(160,200)], new Dictionary<string,string> { ["bridge"] = "yes", ["widthMeters"] = "3" });
        Assert.Equal(TerrainType.Sidewalk, new WorldNavigation(Area.Bounds, [water,sidewalk], []).TerrainAt(160,0));
    }

    [Fact]
    public void OpenRiverWidthMatchesNavigationAcrossSpatialCells()
    {
        var water = new CanonicalEntity("river", EntityKind.Water, new(Area.Region, 0, 0), [new(0, -100), new(0, 100)],
            new Dictionary<string, string> { ["width"] = "80" });
        var navigation = new WorldNavigation(Area.Bounds, [water], []);
        Assert.Equal(TerrainType.ShallowWater, navigation.TerrainAt(39, 0));
        Assert.Equal(TerrainType.Sand, navigation.TerrainAt(41, 0));
        Assert.Equal(TerrainType.Grass, navigation.TerrainAt(44, 0));
    }

    [Fact]
    public void ShoresAndIslandEdgesProgressFromSandThroughShallowsToDeepWater()
    {
        GeometryPoint[] Square(double radius) => [new(-radius,-radius),new(radius,-radius),new(radius,radius),new(-radius,radius),new(-radius,-radius)];
        var water = new CanonicalEntity("lake", EntityKind.Water, new(Area.Region, 0, 0), Square(100),
            new Dictionary<string,string>(), InteriorRings: [Square(10)]);
        var navigation = new WorldNavigation(Area.Bounds, [water], []);
        foreach (var (dx,dy) in new[] { (1,0),(-1,0),(0,1),(0,-1) })
        foreach (var island in new[] { false,true })
        {
            var transitions = new List<TerrainType>();
            for (var step = -10; step <= 20; step++)
            {
                var depth = step / 5d;
                var radius = island ? 10 + depth : 100 - depth;
                var terrain = navigation.TerrainAt(dx * radius, dy * radius);
                if (transitions.Count == 0 || transitions[^1] != terrain) transitions.Add(terrain);
            }
            Assert.Equal([TerrainType.Sand,TerrainType.ShallowWater,TerrainType.DeepWater], transitions);
        }
    }

    private static async Task<GeographicDataset> Import(string json, GeographicArea area)
    {
        var directory = Path.Combine(Path.GetTempPath(), "water-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new Handler(json)) { BaseAddress = new("https://example.test/") };
            return await new OverpassGeographicProvider(client, directory, new FlatElevationProvider()).GetAreaAsync(area);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Handler(string json) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = WebUtility.UrlDecode(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Contains("rel[\"type\"=\"multipolygon\"][\"natural\"=\"water\"]", query);
            Assert.Contains("rel[\"type\"=\"multipolygon\"][\"waterway\"=\"riverbank\"]", query);
            return new(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
