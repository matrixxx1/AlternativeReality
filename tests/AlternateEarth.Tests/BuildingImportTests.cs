using System.Net;
using AlternateEarth.Geo;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class BuildingImportTests
{
    private const string Building = """
    {"elements":[
    {"type":"node","id":1,"lat":45.4999,"lon":-122.5001},
    {"type":"node","id":2,"lat":45.4999,"lon":-122.4999},
    {"type":"node","id":3,"lat":45.5001,"lon":-122.4999},
    {"type":"node","id":4,"lat":45.5001,"lon":-122.5001},
    {"type":"way","id":10,"nodes":[1,2,3,4,1],"tags":{"building":"house"}}]}
    """;

    [Fact]
    public async Task CompleteFootprintIsIdenticalAcrossBlockEdges()
    {
        var a = Assert.Single((await Import(Building, new(new(45.5, -122.5002), 30))).Features);
        var b = Assert.Single((await Import(Building, new(new(45.5, -122.4998), 30))).Features);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.Geometry, b.Geometry);
        Assert.Equal(5, a.Geometry.Count);
        Assert.Equal(a.Geometry[0], a.Geometry[^1]);
        Assert.True(a.Geometry.Max(p => p.X) - a.Geometry.Min(p => p.X) > 15);
        Assert.True(a.Geometry.Max(p => p.Y) - a.Geometry.Min(p => p.Y) > 20);
    }

    [Fact]
    public async Task PreservesCornersAcrossGeographicRegionBoundary()
    {
        var json = Building.Replace("45.4999", "45.9999").Replace("45.5001", "46.0001");
        var building = Assert.Single((await Import(json, new(new(45.9999, -122.5), 50))).Features);
        Assert.Equal(5, building.Geometry.Count);
        Assert.True(building.Geometry.Max(p => p.Y) - building.Geometry.Min(p => p.Y) > 20);
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("[1,2,3,4]")]
    [InlineData("[1,2,999,4,1]")]
    [InlineData("[1,2,1]")]
    [InlineData("[1,3,2,4,1]")]
    public async Task RejectsIncompleteAndDegenerateBuildings(string nodes)
    {
        Assert.Empty((await Import(Building.Replace("[1,2,3,4,1]", nodes), new(new(45.5, -122.5), 100))).Features);
    }

    [Fact]
    public async Task KeepsRealConcaveBuildingsAndSkipsNonoverlappingBuildings()
    {
        var json = Building.Replace("{\"type\":\"way\"", """
        {"type":"node","id":5,"lat":45.5,"lon":-122.5},
        {"type":"node","id":6,"lat":45.5001,"lon":-122.5},
        {"type":"node","id":7,"lat":45.5,"lon":-122.4999},
        {"type":"way"
        """).Replace("[1,2,3,4,1]", "[1,2,7,5,6,4,1]");
        Assert.Equal(7, Assert.Single((await Import(json, new(new(45.5, -122.5), 100))).Features).Geometry.Count);
        Assert.Empty((await Import(json, new(new(45.51, -122.5), 100))).Features);
    }

    [Fact]
    public async Task RejectsPartialOverpassSuccessResponses()
    {
        var json = Building.Replace("{\"elements\":", "{\"remark\":\"runtime error: Query timed out\",\"elements\":");
        await Assert.ThrowsAsync<HttpRequestException>(() => Import(json, new(new(45.5, -122.5), 100)));
    }

    [Fact]
    public async Task DenseAreaFallbackMergesCompleteObjectsWithoutDuplicateBuildings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "building-sections-" + Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new DenseHandler();
            using var client = new HttpClient(handler) { BaseAddress = new("https://overpass-api.de/") };
            var dataset = await new OverpassGeographicProvider(client, directory, new FlatElevationProvider()).GetAreaAsync(new(new(45.5, -122.5), 2000));
            Assert.Equal(7, handler.Calls);
            Assert.Equal(5, Assert.Single(dataset.Features).Geometry.Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class DenseHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(++Calls <= 3 ? new HttpResponseMessage(HttpStatusCode.GatewayTimeout) :
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Building) });
    }

    private static async Task<GeographicDataset> Import(string json, GeographicArea area)
    {
        var directory = Path.Combine(Path.GetTempPath(), "building-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new Handler(json)) { BaseAddress = new("https://example.test/") };
            return await new OverpassGeographicProvider(client, directory, new FlatElevationProvider()).GetAreaAsync(area);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Handler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
