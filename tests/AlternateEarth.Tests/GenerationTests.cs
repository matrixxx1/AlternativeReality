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
}
