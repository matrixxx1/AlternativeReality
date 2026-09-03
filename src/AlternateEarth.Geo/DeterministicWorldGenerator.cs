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
        var sidewalks = GenerateSidewalks(geographic.Features);
        var withSidewalks = geographic.Features.Concat(sidewalks).ToArray();
        var doors = GenerateDoors(withSidewalks);
        var trees = GenerateResourceNodes(reality, 96, withSidewalks);
        var vehicles = GenerateVehicles(reality, withSidewalks, 12);
        return geographic with { Features = withSidewalks.Concat(doors).Concat(trees).Concat(vehicles).ToArray() };
    }

    public static IReadOnlyList<CanonicalEntity> GenerateResourceNodes(
        RealityConfiguration reality,
        int count,
        IReadOnlyList<CanonicalEntity>? obstacles = null)
    {
        var bounds = reality.Area.Bounds;
        var random = new Random(StableSeed(reality.Seed, reality.Area.Region));
        var result = new List<CanonicalEntity>(count);
        for (var i = 0; i < count; i++)
        {
            double x;
            double y;
            var attempts = 0;
            do
            {
                x = bounds.MinimumX + (random.NextDouble() * (bounds.MaximumX - bounds.MinimumX));
                y = bounds.MinimumY + (random.NextDouble() * (bounds.MaximumY - bounds.MinimumY));
                attempts++;
            } while (attempts < 40 && obstacles is not null && obstacles.Any(entity => BlocksGeneratedPoint(entity, x, y, 1.2)));
            var subtype = random.Next(0, 3) switch { 0 => "pine", 1 => "fir", _ => "oak" };
            result.Add(new CanonicalEntity(
                $"generated:{reality.Id}:tree:{i}",
                EntityKind.Tree,
                new WorldPosition(reality.Area.Region, x, y),
                Array.Empty<GeometryPoint>(),
                new Dictionary<string, string> { ["species"] = subtype, ["health"] = "100", ["collisionRadius"] = "0.85" }));
        }

        return result;
    }

    public static IReadOnlyList<CanonicalEntity> GenerateSidewalks(IReadOnlyList<CanonicalEntity> features) => features
        .Where(entity => entity.Kind == EntityKind.Road && entity.Geometry.Count >= 2 &&
                         entity.Properties.GetValueOrDefault("highway") is not ("motorway" or "trunk" or "track"))
        .Select(entity =>
        {
            var roadWidth = ParseDouble(entity.Properties.GetValueOrDefault("widthMeters"), 5);
            return new CanonicalEntity(
                $"generated:sidewalk:{entity.Id}",
                EntityKind.Sidewalk,
                entity.Position,
                entity.Geometry,
                new Dictionary<string, string>
                {
                    ["terrain"] = "sidewalk",
                    ["widthMeters"] = (roadWidth + 3).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    ["roadId"] = entity.Id
                });
        }).ToArray();

    public static IReadOnlyList<CanonicalEntity> GenerateDoors(IReadOnlyList<CanonicalEntity> features)
    {
        var sidewalks = features.Where(entity => entity.Kind == EntityKind.Sidewalk && entity.Geometry.Count >= 2).ToArray();
        var roads = features.Where(entity => entity.Kind == EntityKind.Road && entity.Geometry.Count >= 2).ToArray();
        var approaches = sidewalks.Length > 0 ? sidewalks : roads;
        var doors = new List<CanonicalEntity>();
        foreach (var building in features.Where(entity => entity.Kind == EntityKind.Building && entity.Geometry.Count >= 3))
        {
            var nearbyApproaches = approaches
                .Where(entity => entity.Position.Distance2D(building.Position) <= 250 ||
                                 entity.Geometry.Any(point => Distance(point, new GeometryPoint(building.Position.X, building.Position.Y)) <= 250))
                .ToArray();
            if (nearbyApproaches.Length == 0) nearbyApproaches = approaches;
            var bestDistance = double.MaxValue;
            GeometryPoint bestDoor = building.Geometry[0];
            GeometryPoint bestApproach = building.Geometry[0];
            for (var index = 0; index < building.Geometry.Count - 1; index++)
            {
                var start = building.Geometry[index];
                var end = building.Geometry[index + 1];
                var candidates = new[] { start, end, Midpoint(start, end) };
                foreach (var candidate in candidates)
                {
                    foreach (var approach in nearbyApproaches)
                    {
                        for (var segment = 0; segment < approach.Geometry.Count - 1; segment++)
                        {
                            var nearest = ClosestPoint(candidate, approach.Geometry[segment], approach.Geometry[segment + 1]);
                            var distance = Distance(candidate, nearest);
                            if (distance >= bestDistance) continue;
                            bestDistance = distance;
                            bestDoor = candidate;
                            bestApproach = nearest;
                        }
                    }
                }
            }

            var facing = Math.Atan2(bestApproach.Y - bestDoor.Y, bestApproach.X - bestDoor.X) * 180 / Math.PI;
            doors.Add(new CanonicalEntity(
                $"generated:door:{building.Id}",
                EntityKind.Door,
                building.Position with { X = bestDoor.X, Y = bestDoor.Y },
                Array.Empty<GeometryPoint>(),
                new Dictionary<string, string>
                {
                    ["buildingId"] = building.Id,
                    ["facingDegrees"] = facing.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    ["approach"] = sidewalks.Length > 0 ? "sidewalk" : "road"
                }));
        }
        return doors;
    }

    public static IReadOnlyList<CanonicalEntity> GenerateVehicles(RealityConfiguration reality, IReadOnlyList<CanonicalEntity> features, int count)
    {
        var roads = features.Where(entity => entity.Kind == EntityKind.Road && entity.Geometry.Count >= 2).ToArray();
        if (roads.Length == 0) return Array.Empty<CanonicalEntity>();
        var random = new Random(StableSeed(reality.Seed + 7919, reality.Area.Region));
        var vehicles = new List<CanonicalEntity>();
        for (var index = 0; index < count; index++)
        {
            var road = roads[random.Next(roads.Length)];
            var segment = random.Next(road.Geometry.Count - 1);
            var start = road.Geometry[segment];
            var end = road.Geometry[segment + 1];
            var amount = .2 + random.NextDouble() * .6;
            var x = start.X + ((end.X - start.X) * amount);
            var y = start.Y + ((end.Y - start.Y) * amount);
            var rotation = Math.Atan2(end.Y - start.Y, end.X - start.X);
            var roadWidth = ParseDouble(road.Properties.GetValueOrDefault("widthMeters"), 5);
            var offset = Math.Max(1.2, (roadWidth / 2) - 1);
            x += -Math.Sin(rotation) * offset;
            y += Math.Cos(rotation) * offset;
            vehicles.Add(new CanonicalEntity(
                $"generated:{reality.Id}:vehicle:{index}",
                EntityKind.Vehicle,
                new WorldPosition(reality.Area.Region, x, y),
                Array.Empty<GeometryPoint>(),
                new Dictionary<string, string>
                {
                    ["vehicleType"] = "car",
                    ["lengthMeters"] = "4.5",
                    ["widthMeters"] = "1.9",
                    ["rotationDegrees"] = (rotation * 180 / Math.PI).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                }));
        }
        return vehicles;
    }

    private static bool BlocksGeneratedPoint(CanonicalEntity entity, double x, double y, double padding)
    {
        if (entity.Kind == EntityKind.Building && entity.Geometry.Count >= 3) return PointInPolygon(x, y, entity.Geometry);
        if (entity.Kind is EntityKind.Road or EntityKind.Sidewalk or EntityKind.Water)
        {
            var width = entity.Kind == EntityKind.Water ? 3 : ParseDouble(entity.Properties.GetValueOrDefault("widthMeters"), 4);
            return DistanceToGeometry(x, y, entity.Geometry) <= (width / 2) + padding;
        }
        return false;
    }

    private static bool PointInPolygon(double x, double y, IReadOnlyList<GeometryPoint> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = i == 0 ? polygon.Count - 1 : i - 1;
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) && x < ((b.X - a.X) * (y - a.Y) / ((b.Y - a.Y) + double.Epsilon)) + a.X) inside = !inside;
        }
        return inside;
    }

    private static double DistanceToGeometry(double x, double y, IReadOnlyList<GeometryPoint> geometry)
    {
        if (geometry.Count == 0) return double.MaxValue;
        var point = new GeometryPoint(x, y);
        var best = double.MaxValue;
        for (var index = 0; index < geometry.Count - 1; index++) best = Math.Min(best, Distance(point, ClosestPoint(point, geometry[index], geometry[index + 1])));
        return best;
    }

    private static GeometryPoint Midpoint(GeometryPoint a, GeometryPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);

    private static GeometryPoint ClosestPoint(GeometryPoint point, GeometryPoint start, GeometryPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= double.Epsilon) return start;
        var amount = Math.Clamp((((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared, 0, 1);
        return new GeometryPoint(start.X + (amount * dx), start.Y + (amount * dy));
    }

    private static double Distance(GeometryPoint a, GeometryPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static double ParseDouble(string? value, double fallback) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static int StableSeed(long seed, RegionId region)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{region.LatitudeBand}:{region.LongitudeBand}"));
        return BitConverter.ToInt32(bytes, 0);
    }
}
