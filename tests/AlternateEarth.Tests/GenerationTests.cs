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

        Assert.Equal(40, first.Count);
        Assert.Equal(8, first.Count(actor => actor.Properties["subtype"] == "rabbit"));
        Assert.Equal(3, first.Count(actor => actor.Properties["subtype"] == "dog"));
        Assert.Equal(4, first.Count(actor => actor.Properties["subtype"] == "cat"));
        Assert.Equal(10, first.Count(actor => actor.Properties["subtype"] == "bird"));
        Assert.Equal(5, first.Count(actor => actor.Properties["subtype"] == "deer"));
        Assert.Equal(1, first.Count(actor => actor.Properties["subtype"] == "cougar"));
        Assert.Equal(1, first.Count(actor => actor.Properties["subtype"] == "bear"));
        Assert.Equal(8, first.Count(actor => actor.Kind == EntityKind.Npc));
        Assert.Equal(first.Select(actor => actor.Position), second.Select(actor => actor.Position));
    }
}
