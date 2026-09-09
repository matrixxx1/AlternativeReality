using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class StreetLightLookupTests
{
    [Theory]
    [InlineData(31, 31)]
    [InlineData(-33, -33)]
    public void LightingFindsNeighboringCellsAndUsesTheExactRadius(double x, double y)
    {
        var target = new WorldPosition(new(45, -123), x, y);
        var lamp = new CanonicalEntity("lamp", EntityKind.StreetLight, target with { X = x + 24 }, [], new Dictionary<string, string>());
        var farLamps = Enumerable.Range(0, 1000).Select(i => lamp with { Id = "far" + i, Position = target with { X = x + 1000 + i * 32 } });
        var navigation = new WorldNavigation(new(-50000,-50000,50000,50000), farLamps.Append(lamp).ToArray(), []);
        Assert.True(navigation.HasStreetLightNear(target));
        Assert.False(navigation.HasStreetLightNear(target with { X = x - .01 }));
        Assert.False(navigation.HasStreetLightNear(target with { Region = new(46, -123) }));
    }
}
