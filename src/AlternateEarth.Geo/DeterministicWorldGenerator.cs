using System.Security.Cryptography;
using System.Text;
using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public sealed class DeterministicWorldGenerator
{
    private readonly IGeographicProvider _geographicProvider;

    public DeterministicWorldGenerator(IGeographicProvider geographicProvider) => _geographicProvider = geographicProvider;

    public async Task<GeographicDataset> GenerateAsync(
        RealityConfiguration reality,
        CancellationToken cancellationToken = default)
    {
        var geographic = await _geographicProvider.GetAreaAsync(reality.Area, cancellationToken);
        var procedural = GenerateResourceNodes(reality, 32);
        return geographic with { Features = geographic.Features.Concat(procedural).ToArray() };
    }

    public static IReadOnlyList<CanonicalEntity> GenerateResourceNodes(RealityConfiguration reality, int count)
    {
        var bounds = reality.Area.Bounds;
        var random = new Random(StableSeed(reality.Seed, reality.Area.Region));
        var result = new List<CanonicalEntity>(count);
        for (var i = 0; i < count; i++)
        {
            var x = bounds.MinimumX + (random.NextDouble() * (bounds.MaximumX - bounds.MinimumX));
            var y = bounds.MinimumY + (random.NextDouble() * (bounds.MaximumY - bounds.MinimumY));
            var subtype = random.Next(0, 3) switch { 0 => "pine", 1 => "fir", _ => "oak" };
            result.Add(new CanonicalEntity(
                $"generated:{reality.Id}:tree:{i}",
                EntityKind.Tree,
                new WorldPosition(reality.Area.Region, x, y),
                Array.Empty<GeometryPoint>(),
                new Dictionary<string, string> { ["species"] = subtype, ["health"] = "100" }));
        }

        return result;
    }

    private static int StableSeed(long seed, RegionId region)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{region.LatitudeBand}:{region.LongitudeBand}"));
        return BitConverter.ToInt32(bytes, 0);
    }
}
