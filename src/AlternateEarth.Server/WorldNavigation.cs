using System.Globalization;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed record NavigationResult(bool Success, IReadOnlyList<WorldPosition> Waypoints, string? Message = null);

public sealed class WorldNavigation
{
    public const double PlayerRadiusMeters = 0.35;
    private const double SpatialCellMeters = 32;
    private readonly WorldBounds _bounds;
    private readonly IReadOnlyList<ElevationSample> _elevation;
    private readonly Dictionary<(int X, int Y), List<CanonicalEntity>> _spatial = new();

    public WorldNavigation(WorldBounds bounds, IReadOnlyList<CanonicalEntity> entities, IReadOnlyList<ElevationSample> elevation)
    {
        _bounds = bounds;
        _elevation = elevation;
        foreach (var entity in entities) Index(entity);
    }

    public TerrainType TerrainAt(double x, double y)
    {
        var terrain = TerrainType.Grass;
        var priority = 0;
        foreach (var entity in Candidates(x, y))
        {
            switch (entity.Kind)
            {
                case EntityKind.Terrain when entity.Geometry.Count >= 3 && PointInPolygon(x, y, entity.Geometry):
                    var surface = ParseTerrain(entity.Properties.GetValueOrDefault("terrain"));
                    if (priority < 1) { terrain = surface; priority = 1; }
                    break;
                case EntityKind.Sidewalk when DistanceToGeometry(x, y, entity.Geometry) <= Width(entity, 3) / 2:
                    if (priority < 3) { terrain = TerrainType.Sidewalk; priority = 3; }
                    break;
                case EntityKind.Road when DistanceToGeometry(x, y, entity.Geometry) <= Width(entity, 5) / 2:
                    if (priority < 4) { terrain = RoadTerrain(entity); priority = 4; }
                    break;
                case EntityKind.Water when IsOnWater(entity, x, y):
                    terrain = WaterTerrain(entity, x, y);
                    priority = 10;
                    break;
            }
        }
        return terrain;
    }

    public double SpeedFor(TerrainType terrain) => terrain switch
    {
        TerrainType.Sidewalk or TerrainType.Pavement or TerrainType.Road => MilesPerHour(3.5),
        TerrainType.Grass => MilesPerHour(2.75),
        TerrainType.Forest => MilesPerHour(2),
        TerrainType.Sand => MilesPerHour(1),
        TerrainType.Mud => MilesPerHour(.75),
        TerrainType.ShallowWater => MilesPerHour(.5),
        TerrainType.DeepWater => MilesPerHour(.25),
        _ => MilesPerHour(2.75)
    };

    public double SpeedFor(TerrainType terrain, TravelMode mode, double staminaFraction = 1, bool magicHikingShoes = false, bool magicRunningShoes = false)
    {
        var running = magicRunningShoes && mode is TravelMode.Walk or TravelMode.Run;
        var hiking = !running && magicHikingShoes && mode is TravelMode.Walk or TravelMode.Run;
        var effectiveTerrain = hiking && terrain is TerrainType.Mud or TerrainType.Grass or TerrainType.ShallowWater
            ? TerrainType.Pavement
            : terrain;
        var speed = mode switch
        {
            TravelMode.Run => SpeedFor(effectiveTerrain) * (1 + Math.Clamp(staminaFraction, 0, 1)),
            TravelMode.Skateboard when effectiveTerrain is TerrainType.Road or TerrainType.Pavement => SpeedFor(effectiveTerrain) * 4,
            TravelMode.Skateboard when effectiveTerrain == TerrainType.Sidewalk => SpeedFor(effectiveTerrain) * 3,
            TravelMode.Skateboard => SpeedFor(effectiveTerrain),
            TravelMode.Bike when effectiveTerrain is TerrainType.Road or TerrainType.Pavement => SpeedFor(effectiveTerrain) * 4,
            TravelMode.Bike when effectiveTerrain == TerrainType.Sidewalk => SpeedFor(effectiveTerrain) * 3,
            TravelMode.Bike => SpeedFor(effectiveTerrain) * 2.6,
            TravelMode.EBike => effectiveTerrain switch
            {
                TerrainType.Road or TerrainType.Pavement => MilesPerHour(25),
                TerrainType.Sidewalk => MilesPerHour(15),
                TerrainType.Grass or TerrainType.Sand => MilesPerHour(20),
                TerrainType.Forest or TerrainType.Mud => MilesPerHour(12),
                TerrainType.ShallowWater => MilesPerHour(4),
                _ => 0
            },
            TravelMode.Raft when effectiveTerrain is TerrainType.ShallowWater or TerrainType.DeepWater => MilesPerHour(3),
            TravelMode.Raft => MilesPerHour(.5),
            TravelMode.DirtBike => effectiveTerrain switch
            {
                TerrainType.Road or TerrainType.Pavement => MilesPerHour(40),
                TerrainType.Sidewalk => MilesPerHour(15),
                TerrainType.Grass or TerrainType.Sand => MilesPerHour(30),
                TerrainType.Forest or TerrainType.Mud => MilesPerHour(18),
                TerrainType.ShallowWater => MilesPerHour(8),
                _ => 0
            },
            TravelMode.Motorcycle => effectiveTerrain switch
            {
                TerrainType.Road or TerrainType.Pavement => MilesPerHour(90),
                TerrainType.Sidewalk => MilesPerHour(20),
                TerrainType.Grass or TerrainType.Sand => MilesPerHour(15),
                TerrainType.Forest or TerrainType.Mud => MilesPerHour(8),
                TerrainType.ShallowWater => MilesPerHour(3),
                _ => 0
            },
            _ => SpeedFor(effectiveTerrain)
        };
        return running ? speed * 3 : hiking ? speed * 2 : speed;
    }

    public static double RunningStaminaDrain(double elapsedSeconds, bool reducedByMagicShoes) =>
        .45 * Math.Max(0, elapsedSeconds) * (reducedByMagicShoes ? .5 : 1);

    public static bool MagicRunningShoesReduceStaminaOn(TerrainType terrain) =>
        terrain is not TerrainType.Road and not TerrainType.Sidewalk;

    public static double WaterDrain(double elapsedSeconds, bool wearingHat) =>
        .01 * Math.Max(0, elapsedSeconds) * (wearingHat ? .5 : 1);

    public static bool SupportsTravelMode(TerrainType terrain, TravelMode mode) => mode switch
    {
        TravelMode.Skateboard => terrain is TerrainType.Road or TerrainType.Pavement or TerrainType.Sidewalk,
        TravelMode.Bike => terrain != TerrainType.DeepWater,
        TravelMode.Raft => terrain is TerrainType.ShallowWater or TerrainType.DeepWater,
        TravelMode.EBike or TravelMode.DirtBike or TravelMode.Motorcycle => terrain != TerrainType.DeepWater,
        _ => true
    };

    public double ElevationAt(double x, double y)
    {
        if (_elevation.Count == 0) return 0;
        var nearest = _elevation
            .Select(sample => (Sample: sample, DistanceSquared: Math.Pow(sample.X - x, 2) + Math.Pow(sample.Y - y, 2)))
            .OrderBy(item => item.DistanceSquared)
            .Take(4)
            .ToArray();
        if (nearest[0].DistanceSquared < .0001) return nearest[0].Sample.ElevationMeters;
        var weights = nearest.Select(item => 1 / Math.Max(1, item.DistanceSquared)).ToArray();
        return nearest.Select((item, index) => item.Sample.ElevationMeters * weights[index]).Sum() / weights.Sum();
    }

    public bool IsBlocked(double x, double y, double radius = PlayerRadiusMeters)
    {
        if (!_bounds.Contains(x, y)) return true;
        foreach (var entity in Candidates(x, y))
        {
            switch (entity.Kind)
            {
                case EntityKind.Building when entity.Geometry.Count >= 3:
                    if (PointInPolygon(x, y, entity.Geometry) || DistanceToGeometry(x, y, entity.Geometry) <= radius) return true;
                    break;
                case EntityKind.Tree or EntityKind.Bush or EntityKind.ResourceNode:
                    if (Distance(x, y, entity.Position.X, entity.Position.Y) <= radius + Width(entity, .85)) return true;
                    break;
                case EntityKind.Fence:
                    if (DistanceToGeometry(x, y, entity.Geometry) <= radius + .12) return true;
                    break;
                case EntityKind.Vehicle:
                    if (InsideVehicle(entity, x, y, radius)) return true;
                    break;
                case EntityKind.PlayerStructure when !string.Equals(entity.Properties.GetValueOrDefault("objectType"), "personalFlag", StringComparison.OrdinalIgnoreCase):
                    if (Distance(x, y, entity.Position.X, entity.Position.Y) <= radius + 1) return true;
                    break;
            }
        }
        return false;
    }

    public bool CanTraverse(WorldPosition start, WorldPosition end, bool avoidDeepWater = false)
        => CanTraverse(start, end, terrain => !avoidDeepWater || terrain != TerrainType.DeepWater);

    public bool CanTraverse(WorldPosition start, WorldPosition end, Func<TerrainType, bool> terrainAllowed)
    {
        var distance = start.Distance2D(end);
        var steps = Math.Max(1, (int)Math.Ceiling(distance / .25));
        for (var step = 1; step <= steps; step++)
        {
            var amount = step / (double)steps;
            var x = start.X + ((end.X - start.X) * amount);
            var y = start.Y + ((end.Y - start.Y) * amount);
            if (IsBlocked(x, y) || !terrainAllowed(TerrainAt(x, y))) return false;
        }
        return true;
    }

    public NavigationResult FindPath(WorldPosition start, double targetX, double targetY, Func<TerrainType, double>? speedForTerrain = null,
        Func<TerrainType, bool>? terrainAllowed = null)
    {
        terrainAllowed ??= terrain => terrain != TerrainType.DeepWater;
        if (!_bounds.Contains(targetX, targetY)) return new(false, Array.Empty<WorldPosition>(), "That destination is outside this reality.");
        if (IsBlocked(targetX, targetY) || !terrainAllowed(TerrainAt(targetX, targetY))) return new(false, Array.Empty<WorldPosition>(), "That destination is blocked or has impassable terrain.");
        var target = start with { X = targetX, Y = targetY, Z = ElevationAt(targetX, targetY) };
        if (CanTraverse(start, target, terrainAllowed)) return new(true, new[] { target });
        if (start.Distance2D(target) > 1500) return new(false, Array.Empty<WorldPosition>(), "That destination is too far away. Choose a closer point.");

        const double cell = 1.5;
        const double padding = 18;
        var minimumX = Math.Max(_bounds.MinimumX, Math.Min(start.X, targetX) - padding);
        var minimumY = Math.Max(_bounds.MinimumY, Math.Min(start.Y, targetY) - padding);
        var maximumX = Math.Min(_bounds.MaximumX, Math.Max(start.X, targetX) + padding);
        var maximumY = Math.Min(_bounds.MaximumY, Math.Max(start.Y, targetY) + padding);
        var columns = (int)Math.Ceiling((maximumX - minimumX) / cell) + 1;
        var rows = (int)Math.Ceiling((maximumY - minimumY) / cell) + 1;
        if ((long)columns * rows > 60_000) return new(false, Array.Empty<WorldPosition>(), "That route is too complex. Choose a closer point.");

        (int X, int Y) Key(double x, double y) => ((int)Math.Round((x - minimumX) / cell), (int)Math.Round((y - minimumY) / cell));
        WorldPosition Position((int X, int Y) key) => start with
        {
            X = minimumX + (key.X * cell),
            Y = minimumY + (key.Y * cell),
            Z = ElevationAt(minimumX + (key.X * cell), minimumY + (key.Y * cell))
        };

        var startKey = Key(start.X, start.Y);
        var targetKey = Key(targetX, targetY);
        var frontier = new PriorityQueue<(int X, int Y), double>();
        var costs = new Dictionary<(int X, int Y), double> { [startKey] = 0 };
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
        frontier.Enqueue(startKey, 0);
        var directions = new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };
        var found = false;
        while (frontier.TryDequeue(out var current, out _))
        {
            if (current == targetKey) { found = true; break; }
            foreach (var direction in directions)
            {
                var next = (X: current.X + direction.Item1, Y: current.Y + direction.Item2);
                if (next.X < 0 || next.Y < 0 || next.X >= columns || next.Y >= rows) continue;
                var nextPosition = Position(next);
                if (IsBlocked(nextPosition.X, nextPosition.Y) || !terrainAllowed(TerrainAt(nextPosition.X, nextPosition.Y))) continue;
                var currentPosition = Position(current);
                if (!CanTraverse(currentPosition, nextPosition, terrainAllowed)) continue;
                var terrainSpeed = (speedForTerrain ?? SpeedFor)(TerrainAt(nextPosition.X, nextPosition.Y));
                if (terrainSpeed <= 0) continue;
                var stepDistance = direction.Item1 != 0 && direction.Item2 != 0 ? cell * Math.Sqrt(2) : cell;
                var nextCost = costs[current] + (stepDistance * (6 / terrainSpeed));
                if (costs.TryGetValue(next, out var known) && known <= nextCost) continue;
                costs[next] = nextCost;
                cameFrom[next] = current;
                var heuristic = Distance(nextPosition.X, nextPosition.Y, targetX, targetY);
                frontier.Enqueue(next, nextCost + heuristic);
            }
        }

        if (!found) return new(false, Array.Empty<WorldPosition>(), "No walkable route to that destination was found.");
        var reverse = new List<WorldPosition> { target };
        var cursor = targetKey;
        while (cursor != startKey)
        {
            reverse.Add(Position(cursor));
            cursor = cameFrom[cursor];
        }
        reverse.Add(start);
        reverse.Reverse();
        return new(true, Smooth(reverse, terrainAllowed));
    }

    public WorldPosition FindNearestWalkable(WorldPosition preferred)
    {
        for (var radius = 0.0; radius <= 100; radius += 1.5)
        {
            var samples = radius == 0 ? 1 : Math.Max(8, (int)Math.Ceiling(radius * 2));
            for (var index = 0; index < samples; index++)
            {
                var angle = index * Math.PI * 2 / samples;
                var x = preferred.X + (Math.Cos(angle) * radius);
                var y = preferred.Y + (Math.Sin(angle) * radius);
                if (!_bounds.Contains(x, y) || IsBlocked(x, y) || TerrainAt(x, y) == TerrainType.DeepWater) continue;
                return preferred with { X = x, Y = y, Z = ElevationAt(x, y) };
            }
        }
        return preferred with { Z = ElevationAt(preferred.X, preferred.Y) };
    }

    private IReadOnlyList<WorldPosition> Smooth(IReadOnlyList<WorldPosition> path, Func<TerrainType, bool> terrainAllowed)
    {
        var result = new List<WorldPosition>();
        var anchor = 0;
        while (anchor < path.Count - 1)
        {
            var furthest = anchor + 1;
            for (var candidate = path.Count - 1; candidate > anchor + 1; candidate--)
            {
                if (!CanTraverse(path[anchor], path[candidate], terrainAllowed)) continue;
                furthest = candidate;
                break;
            }
            result.Add(path[furthest]);
            anchor = furthest;
        }
        return result;
    }

    private void Index(CanonicalEntity entity)
    {
        if (entity.Kind is EntityKind.Door or EntityKind.ResourceNode) return;
        var points = entity.Geometry.Count > 0
            ? entity.Geometry
            : new[] { new GeometryPoint(entity.Position.X, entity.Position.Y) };
        var padding = entity.Kind switch
        {
            EntityKind.Road or EntityKind.Sidewalk => Width(entity, 5) / 2,
            EntityKind.Water => 1.5,
            EntityKind.Tree => Width(entity, .85) + PlayerRadiusMeters,
            EntityKind.Vehicle => 3,
            _ => PlayerRadiusMeters
        };
        var minimumX = points.Min(point => point.X) - padding;
        var maximumX = points.Max(point => point.X) + padding;
        var minimumY = points.Min(point => point.Y) - padding;
        var maximumY = points.Max(point => point.Y) + padding;
        for (var x = Cell(minimumX); x <= Cell(maximumX); x++)
        {
            for (var y = Cell(minimumY); y <= Cell(maximumY); y++)
            {
                if (!_spatial.TryGetValue((x, y), out var list)) _spatial[(x, y)] = list = new List<CanonicalEntity>();
                list.Add(entity);
            }
        }
    }

    private IEnumerable<CanonicalEntity> Candidates(double x, double y) =>
        _spatial.TryGetValue((Cell(x), Cell(y)), out var entities) ? entities : Array.Empty<CanonicalEntity>();

    private static int Cell(double value) => (int)Math.Floor(value / SpatialCellMeters);
    private static TerrainType ParseTerrain(string? value) => Enum.TryParse<TerrainType>(value, true, out var parsed) ? parsed : TerrainType.Grass;
    private static TerrainType RoadTerrain(CanonicalEntity entity) => entity.Properties.GetValueOrDefault("surface") is "unpaved" or "dirt" or "ground" or "gravel" ? TerrainType.Mud : TerrainType.Road;
    private static double Width(CanonicalEntity entity, double fallback) =>
        double.TryParse(entity.Properties.GetValueOrDefault("collisionRadius") ?? entity.Properties.GetValueOrDefault("widthMeters") ?? entity.Properties.GetValueOrDefault("width"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static bool IsOnWater(CanonicalEntity entity, double x, double y) =>
        entity.Geometry.Count >= 4 && entity.Geometry[0] == entity.Geometry[^1]
            ? PointInPolygon(x, y, entity.Geometry)
            : DistanceToGeometry(x, y, entity.Geometry) <= 1.5;

    private static TerrainType WaterTerrain(CanonicalEntity entity, double x, double y)
    {
        var isClosed = entity.Geometry.Count >= 4 && entity.Geometry[0] == entity.Geometry[^1];
        if (!isClosed) return TerrainType.ShallowWater;
        return DistanceToGeometry(x, y, entity.Geometry) <= 3 ? TerrainType.ShallowWater : TerrainType.DeepWater;
    }

    private static bool InsideVehicle(CanonicalEntity entity, double x, double y, double radius)
    {
        var rotation = WidthProperty(entity, "rotationDegrees", 0) * Math.PI / 180;
        var dx = x - entity.Position.X;
        var dy = y - entity.Position.Y;
        var localX = (dx * Math.Cos(rotation)) + (dy * Math.Sin(rotation));
        var localY = (-dx * Math.Sin(rotation)) + (dy * Math.Cos(rotation));
        var length = WidthProperty(entity, "lengthMeters", 4.5);
        var width = WidthProperty(entity, "widthMeters", 1.9);
        return Math.Abs(localX) <= (length / 2) + radius && Math.Abs(localY) <= (width / 2) + radius;
    }

    private static double WidthProperty(CanonicalEntity entity, string key, double fallback) =>
        double.TryParse(entity.Properties.GetValueOrDefault(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

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
        var best = double.MaxValue;
        for (var index = 0; index < geometry.Count - 1; index++)
        {
            var start = geometry[index];
            var end = geometry[index + 1];
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lengthSquared = (dx * dx) + (dy * dy);
            var amount = lengthSquared <= double.Epsilon ? 0 : Math.Clamp((((x - start.X) * dx) + ((y - start.Y) * dy)) / lengthSquared, 0, 1);
            best = Math.Min(best, Distance(x, y, start.X + (amount * dx), start.Y + (amount * dy)));
        }
        return best;
    }

    private static double Distance(double x1, double y1, double x2, double y2) => Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
    public static double MilesPerHour(double value) => value * 0.44704;
}
