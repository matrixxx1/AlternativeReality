using AlternateEarth.Geo;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class GenerationTests
{
    private static readonly RealityConfiguration Reality = new(
        "test", "Test Reality", 123456,
        new GeographicArea(new GeoCoordinate(45.6387, -122.6615), 2000));

    [Fact]
    public void SameSeedAndRegionGenerateIdenticalResources()
    {
        var first = DeterministicWorldGenerator.GenerateResourceNodes(Reality, 20);
        var second = DeterministicWorldGenerator.GenerateResourceNodes(Reality, 20);

        Assert.Equal(first.Select(entity => entity.Id), second.Select(entity => entity.Id));
        Assert.Equal(first.Select(entity => entity.Position), second.Select(entity => entity.Position));
        Assert.Equal(
            first.Select(entity => entity.Properties["species"]),
            second.Select(entity => entity.Properties["species"]));
    }

    [Fact]
    public void DifferentSeedsGenerateDifferentResources()
    {
        var other = Reality with { Seed = Reality.Seed + 1 };
        var first = DeterministicWorldGenerator.GenerateResourceNodes(Reality, 5);
        var second = DeterministicWorldGenerator.GenerateResourceNodes(other, 5);

        Assert.NotEqual(first[0].Position, second[0].Position);
    }

    [Fact]
    public void EveryBuildingGetsDoorFacingSidewalkWhenAvailable()
    {
        var region = Reality.Area.Region;
        var building = new CanonicalEntity("building", EntityKind.Building, new WorldPosition(region, 5, 5),
            new GeometryPoint[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0) },
            new Dictionary<string, string>());
        var sidewalk = new CanonicalEntity("sidewalk", EntityKind.Sidewalk, new WorldPosition(region, 5, -5),
            new GeometryPoint[] { new(-20, -5), new(20, -5) }, new Dictionary<string, string>());

        var door = Assert.Single(DeterministicWorldGenerator.GenerateDoors(new[] { building, sidewalk }));

        Assert.Equal("building", door.Properties["buildingId"]);
        Assert.Equal("sidewalk", door.Properties["approach"]);
        Assert.Equal(EntityKind.Door, door.Kind);
        Assert.Equal(5, door.Position.X);
        Assert.Equal(0, door.Position.Y);
    }


    [Fact]
    public void GeneratesRequestedWildlifeAndNpcPopulationDeterministically()
    {
        var first = DeterministicWorldGenerator.GenerateActors(Reality, Array.Empty<CanonicalEntity>());
        var second = DeterministicWorldGenerator.GenerateActors(Reality, Array.Empty<CanonicalEntity>());

        Assert.Equal(50, first.Count);
        Assert.Equal(8, first.Count(actor => actor.Properties["subtype"] == "rabbit"));
        Assert.Equal(3, first.Count(actor => actor.Properties["subtype"] == "dog"));
        Assert.Equal(4, first.Count(actor => actor.Properties["subtype"] == "cat"));
        Assert.Equal(10, first.Count(actor => actor.Properties["subtype"] == "bird"));
        Assert.Equal(5, first.Count(actor => actor.Properties["subtype"] == "deer"));
        Assert.Equal(1, first.Count(actor => actor.Properties["subtype"] == "cougar"));
        Assert.Equal(1, first.Count(actor => actor.Properties["subtype"] == "bear"));
        Assert.Equal(18, first.Count(actor => actor.Kind == EntityKind.Npc));
        Assert.Equal(first.Select(actor => actor.Position), second.Select(actor => actor.Position));
        Assert.Contains(first, actor => actor.Kind == EntityKind.Npc && actor.Properties["name"] == "Joe");
        Assert.All(first.Where(actor => actor.Properties["subtype"] == "dog"), actor => Assert.NotEqual("dog", actor.Properties["name"]));
        Assert.All(first.Where(actor => actor.Properties["subtype"] == "cat"), actor => Assert.NotEqual("cat", actor.Properties["name"]));
        Assert.Equal(first.Select(actor => actor.Properties["name"]), second.Select(actor => actor.Properties["name"]));
    }

    [Fact]
    public void PropertyFencesLeaveOpenGatesOnTwoSides()
    {
        var region = Reality.Area.Region;
        var parcel = new CanonicalEntity("parcel", EntityKind.PropertyBoundary, new WorldPosition(region, 10, 10),
            new GeometryPoint[] { new(0, 0), new(20, 0), new(20, 20), new(0, 20), new(0, 0) }, new Dictionary<string, string>());

        var fences = DeterministicWorldGenerator.GeneratePropertyFences(new[] { parcel });

        Assert.Equal(6, fences.Count);
        Assert.Equal(4, fences.Count(fence => fence.Properties["openGateAdjacent"] == "true"));
        Assert.All(fences, fence => Assert.Equal("parcel", fence.Properties["parcelId"]));
    }

    [Fact]
    public void GeographicBusinessesBecomeCategorizedMerchants()
    {
        var region = Reality.Area.Region;
        var station = new CanonicalEntity("station", EntityKind.PointOfInterest, new WorldPosition(region, 10, 10),
            Array.Empty<GeometryPoint>(), new Dictionary<string, string> { ["name"] = "Test Fuel", ["merchantCategory"] = "gas" });

        var merchant = Assert.Single(DeterministicWorldGenerator.GeneratePoiMerchants(Reality, new[] { station }));

        Assert.Equal(EntityKind.Npc, merchant.Kind);
        Assert.Equal("gas", merchant.Properties["merchantCategory"]);
        Assert.Contains("Test Fuel", merchant.Properties["name"]);
        Assert.StartsWith("Joe at ", merchant.Properties["name"]);
    }

    [Fact]
    public async Task OverpassImporterIgnoresRelationNodesOutsideProjectionRegion()
    {
        const string response = """
            {"elements":[
              {"type":"node","id":1,"lat":45.5000,"lon":-122.5000},
              {"type":"node","id":2,"lat":46.1000,"lon":-122.5000},
              {"type":"node","id":3,"lat":45.5002,"lon":-122.5002},
              {"type":"way","id":10,"nodes":[1,2,3],"tags":{"highway":"residential","name":"Safe Road"}}
            ]}
            """;
        var directory = Path.Combine(Path.GetTempPath(), $"alternative-reality-overpass-{Guid.NewGuid():N}");
        try
        {
            using var client = new HttpClient(new StaticJsonHandler(response)) { BaseAddress = new Uri("https://example.test/") };
            var provider = new OverpassGeographicProvider(client, directory, new FlatElevationProvider());

            var dataset = await provider.GetAreaAsync(new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));

            var road = Assert.Single(dataset.Features);
            Assert.Equal(EntityKind.Road, road.Kind);
            Assert.Equal(2, road.Geometry.Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task OverpassImporterClassifiesSpecialtyRetailers()
    {
        const string response = """
            {"elements":[
              {"type":"node","id":1,"lat":45.5000,"lon":-122.5000,"tags":{"shop":"hardware","name":"Handy Hardware"}},
              {"type":"node","id":2,"lat":45.5001,"lon":-122.5001,"tags":{"shop":"sports","name":"Field Sports"}},
              {"type":"node","id":3,"lat":45.5002,"lon":-122.5002,"tags":{"shop":"car","name":"Motor Center"}}
            ]}
            """;
        var directory = Path.Combine(Path.GetTempPath(), $"alternative-reality-retail-{Guid.NewGuid():N}");
        try
        {
            using var client = new HttpClient(new StaticJsonHandler(response)) { BaseAddress = new Uri("https://example.test/") };
            var provider = new OverpassGeographicProvider(client, directory, new FlatElevationProvider());
            var dataset = await provider.GetAreaAsync(new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));

            Assert.Contains(dataset.Features, feature => feature.Properties.GetValueOrDefault("merchantCategory") == "hardware");
            Assert.Contains(dataset.Features, feature => feature.Properties.GetValueOrDefault("merchantCategory") == "sportingGoods");
            Assert.Contains(dataset.Features, feature => feature.Properties.GetValueOrDefault("merchantCategory") == "vehicles");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
