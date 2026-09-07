using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class NavigationElevationTests
{
    private static double Reference(IReadOnlyList<ElevationSample> samples, double x, double y)
    {
        if (samples.Count == 0) return 0;
        var nearest = samples.Select(sample => (Sample: sample, DistanceSquared: Math.Pow(sample.X - x, 2) + Math.Pow(sample.Y - y, 2)))
            .OrderBy(item => item.DistanceSquared).Take(4).ToArray();
        if (nearest[0].DistanceSquared < .0001) return nearest[0].Sample.ElevationMeters;
        var weights = nearest.Select(item => 1 / Math.Max(1, item.DistanceSquared)).ToArray();
        return nearest.Select((item, index) => item.Sample.ElevationMeters * weights[index]).Sum() / weights.Sum();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(135)]
    [InlineData(1000)]
    public void HeightsMatchOriginalWeightedInterpolation(int count)
    {
        var random = new Random(891);
        var samples = Enumerable.Range(0, count).Select(_ => new ElevationSample(random.NextDouble() * 2000 - 1000,
            random.NextDouble() * 2000 - 1000, random.NextDouble() * 200 - 50)).ToArray();
        var navigation = new WorldNavigation(new(-2000, -2000, 2000, 2000), [], samples);
        for (var query = 0; query < 300; query++)
        {
            var x = random.NextDouble() * 4000 - 2000;
            var y = random.NextDouble() * 4000 - 2000;
            Assert.Equal(Reference(samples, x, y), navigation.ElevationAt(x, y));
        }
        foreach (var sample in samples.Take(10))
            Assert.Equal(Reference(samples, sample.X, sample.Y), navigation.ElevationAt(sample.X, sample.Y));
    }

    [Fact]
    public void EqualDistanceAndDuplicateSamplesKeepTheirOriginalOrdering()
    {
        ElevationSample[] samples = [new(1, 0, 10), new(-1, 0, 20), new(0, 1, 30), new(0, -1, 40), new(1, 0, 1000)];
        var navigation = new WorldNavigation(new(-10, -10, 10, 10), [], samples);
        Assert.Equal(25, navigation.ElevationAt(0, 0));
        Assert.Equal(10, navigation.ElevationAt(1, 0));
    }

    [Fact]
    public void RepeatedHeightQueriesDoNotAllocateManagedObjects()
    {
        var samples = Enumerable.Range(0, 135).Select(i => new ElevationSample(i * 10, i % 7 * 10, i)).ToArray();
        var navigation = new WorldNavigation(new(-2000, -2000, 2000, 2000), [], samples);
        for (var i = 0; i < 100; i++) navigation.ElevationAt(i, i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) navigation.ElevationAt(i, i);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
