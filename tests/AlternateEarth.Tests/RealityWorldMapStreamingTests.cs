using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World, PlayerState Player)> CreateMapStreamingWorld()
    {
        var config = new RealityConfiguration("map-streaming", "Map streaming", 51, new GeographicArea(new GeoCoordinate(45.5, -122.5), 10000));
        var center = new LocalTangentProjection(config.Area.Region).Project(config.Area.Center);
        var features = new List<CanonicalEntity>();
        for (var x = -4500; x <= 4500; x += 200)
        for (var y = -4500; y <= 4500; y += 200)
            features.Add(new($"tree:{x}:{y}", EntityKind.Tree, center with { X = center.X + x, Y = center.Y + y }, [], new Dictionary<string, string>()));
        features.Add(new("crossing-road", EntityKind.Road, center with { X = center.X - 4500 },
            [new(center.X - 4500, center.Y + 20), new(center.X + 4500, center.Y + 20)], new Dictionary<string, string>()));
        features.Add(new("near-store", EntityKind.PointOfInterest, center with { X = center.X + 490 }, [],
            new Dictionary<string, string> { ["merchantCategory"] = "food", ["name"] = "Nearby store" }));
        var store = new SqliteRealityStore(Path.Combine(_directory, "map-streaming.db"));
        await store.InitializeAsync(config);
        await store.SaveCharacterAsync(config.Id, new("viewer", "Viewer", center));
        var world = new RealityWorld(config, new DeterministicWorldGenerator(new FixedGeographicProvider(features.ToArray())), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        return (world, await world.JoinAsync("viewer", "Viewer"));
    }

    [Fact]
    public async Task LocalSnapshotsDropOverNinetyPercentOfDistantObjectsWithoutChangingServerWorld()
    {
        var (world, player) = await CreateMapStreamingWorld();
        var full = world.CreateSnapshot();
        var local = world.CreateClientSnapshot(player.Id);
        Assert.True(local.BaseEntities.Count < full.BaseEntities.Count / 10, $"Local {local.BaseEntities.Count}, full {full.BaseEntities.Count}");
        Assert.Equal(full.BaseEntities.Count, world.CreateSnapshot().BaseEntities.Count);
        Assert.Contains(local.BaseEntities, item => item.Id == "crossing-road");
        Assert.Contains(local.BaseEntities, item => item.Id == "near-store");
        Assert.Equal(full.Players, local.Players);
        Assert.Equal(full.Elevation, local.Elevation);
        Assert.Equal(full.RealityEntities, local.RealityEntities);
        Assert.Equal(full.Actors, local.Actors);
        Assert.Equal(full.LoadedAreas, local.LoadedAreas);
        Assert.Equal(full.DoorLocks, local.DoorLocks);
        Assert.Equal(full.PublicBases, local.PublicBases);
        Assert.Equal(full.Graves, local.Graves);
        Assert.Equal(full.AreaHazards, local.AreaHazards);
        Assert.Equal(full.Weather, local.Weather);
        Assert.NotEmpty(local.MapCoverage!);
    }

    [Fact]
    public async Task PanningReplacesDistantMapDetailAndKeepsPlayerAndMiniMapArea()
    {
        var (world, player) = await CreateMapStreamingWorld();
        var p = player.Position;
        var east = world.CreateMapWindow(player.Id, new(p.X + 2900, p.Y - 200, p.X + 3200, p.Y + 200));
        Assert.Contains(east.BaseEntities, item => item.Id == "tree:3100:100");
        Assert.Contains(east.BaseEntities, item => item.Id == "near-store");
        var west = world.CreateMapWindow(player.Id, new(p.X - 3200, p.Y - 200, p.X - 2900, p.Y + 200));
        Assert.DoesNotContain(west.BaseEntities, item => item.Id == "tree:3100:100");
        Assert.Contains(west.BaseEntities, item => item.Id == "tree:-3100:100");
        Assert.Contains(west.BaseEntities, item => item.Id == "near-store");
        Assert.Equal(2, west.Coverage.Count);
    }

    [Fact]
    public async Task InvalidMapWindowsAreRejectedWithoutMutatingTheWorld()
    {
        var (world, player) = await CreateMapStreamingWorld();
        foreach (var view in new[] { new WorldBounds(double.NaN, 0, 1, 1), new(10, 0, 1, 1), new(0, 0, 1000000, 1) })
            Assert.Throws<InvalidOperationException>(() => world.CreateMapWindow(player.Id, view));
        Assert.NotEmpty(world.CreateClientSnapshot(player.Id).BaseEntities);
    }
}
