using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private sealed record InteriorLayout(
        double Width,
        double Height,
        IReadOnlyList<GeometryPoint> Footprint,
        IReadOnlyList<DungeonWall> ExteriorWalls,
        WorldPosition Exit,
        WorldPosition Doorway);

    private InteriorLayout CreateInteriorLayout(CanonicalEntity building, double maximumDimension = 100)
    {
        var source = building.Geometry.ToList();
        if (source.Count > 3 && PointsEqual(source[0], source[^1])) source.RemoveAt(source.Count - 1);
        if (source.Count < 3)
        {
            source =
            [
                new(building.Position.X - 5, building.Position.Y - 5),
                new(building.Position.X + 5, building.Position.Y - 5),
                new(building.Position.X + 5, building.Position.Y + 5),
                new(building.Position.X - 5, building.Position.Y + 5)
            ];
        }

        var minX = source.Min(point => point.X); var maxX = source.Max(point => point.X);
        var minY = source.Min(point => point.Y); var maxY = source.Max(point => point.Y);
        var sourceWidth = Math.Max(.01, maxX - minX); var sourceHeight = Math.Max(.01, maxY - minY);
        var width = Math.Clamp(sourceWidth, 6, maximumDimension);
        var height = Math.Clamp(sourceHeight, 6, maximumDimension);
        var scaleX = width / sourceWidth; var scaleY = height / sourceHeight;
        var footprint = source.Select(point => new GeometryPoint((point.X - minX) * scaleX, (point.Y - minY) * scaleY)).ToArray();
        var exteriorWalls = Enumerable.Range(0, footprint.Length)
            .Select(index =>
            {
                var start = footprint[index]; var end = footprint[(index + 1) % footprint.Length];
                return new DungeonWall(start.X, start.Y, end.X, end.Y);
            }).ToArray();

        var exteriorDoor = _baseEntities.Values.FirstOrDefault(entity =>
            entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == building.Id);
        var mappedDoor = exteriorDoor is null
            ? new GeometryPoint(width / 2, 0)
            : new GeometryPoint((exteriorDoor.Position.X - minX) * scaleX, (exteriorDoor.Position.Y - minY) * scaleY);
        var doorwayPoint = ClosestPointOnFootprint(mappedDoor, footprint);
        var exitPoint = FindInteriorPointNear(doorwayPoint, footprint, width, height);
        return new InteriorLayout(
            width, height, footprint, exteriorWalls,
            new WorldPosition(building.Position.Region, exitPoint.X, exitPoint.Y),
            new WorldPosition(building.Position.Region, doorwayPoint.X, doorwayPoint.Y));
    }

    private static bool PointsEqual(GeometryPoint first, GeometryPoint second)
        => Math.Abs(first.X - second.X) < .001 && Math.Abs(first.Y - second.Y) < .001;

    private static GeometryPoint ClosestPointOnFootprint(GeometryPoint point, IReadOnlyList<GeometryPoint> footprint)
    {
        var best = footprint[0]; var bestDistance = double.MaxValue;
        for (var index = 0; index < footprint.Count; index++)
        {
            var candidate = ClosestPointOnSegment(point, footprint[index], footprint[(index + 1) % footprint.Count]);
            var distance = Math.Pow(candidate.X - point.X, 2) + Math.Pow(candidate.Y - point.Y, 2);
            if (distance >= bestDistance) continue;
            best = candidate; bestDistance = distance;
        }
        return best;
    }

    private static GeometryPoint ClosestPointOnSegment(GeometryPoint point, GeometryPoint start, GeometryPoint end)
    {
        var dx = end.X - start.X; var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < .000001) return start;
        var amount = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
        return new GeometryPoint(start.X + amount * dx, start.Y + amount * dy);
    }

    private static GeometryPoint FindInteriorPointNear(GeometryPoint doorway, IReadOnlyList<GeometryPoint> footprint, double width, double height)
    {
        GeometryPoint? best = null; var bestDistance = double.MaxValue;
        for (var y = .5; y < height; y += .5)
        for (var x = .5; x < width; x += .5)
        {
            var candidate = new GeometryPoint(x, y);
            // Keep the arrival point far enough from the exterior for the
            // player's collision radius. A half-meter offset could leave the
            // player touching a wall and unable to take the first step.
            if (!PointInsideFootprint(candidate, footprint) || DistanceToFootprint(candidate, footprint) < 1.05) continue;
            var distance = Math.Pow(x - doorway.X, 2) + Math.Pow(y - doorway.Y, 2);
            if (distance >= bestDistance) continue;
            best = candidate; bestDistance = distance;
        }
        if (best is not null) return best.Value;
        return new GeometryPoint(width / 2, height / 2);
    }

    private static bool PointInsideFootprint(GeometryPoint point, IReadOnlyList<GeometryPoint>? footprint)
    {
        if (footprint is null || footprint.Count < 3) return true;
        var inside = false;
        for (var index = 0; index < footprint.Count; index++)
        {
            var previous = index == 0 ? footprint.Count - 1 : index - 1;
            var a = footprint[index]; var b = footprint[previous];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / ((b.Y - a.Y) == 0 ? double.Epsilon : b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static double DistanceToFootprint(GeometryPoint point, IReadOnlyList<GeometryPoint> footprint)
    {
        var best = double.MaxValue;
        for (var index = 0; index < footprint.Count; index++)
        {
            var edge = ClosestPointOnSegment(point, footprint[index], footprint[(index + 1) % footprint.Count]);
            best = Math.Min(best, Math.Sqrt(Math.Pow(edge.X - point.X, 2) + Math.Pow(edge.Y - point.Y, 2)));
        }
        return best;
    }

    private static WorldPosition RandomInteriorPosition(Random random, InteriorLayout layout, RegionId region, WorldPosition? avoid = null)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var point = new GeometryPoint(.6 + random.NextDouble() * Math.Max(.1, layout.Width - 1.2), .6 + random.NextDouble() * Math.Max(.1, layout.Height - 1.2));
            if (!PointInsideFootprint(point, layout.Footprint) || DistanceToFootprint(point, layout.Footprint) < .5) continue;
            if (avoid is { } avoided && Math.Sqrt(Math.Pow(point.X - avoided.X, 2) + Math.Pow(point.Y - avoided.Y, 2)) < 2.5) continue;
            return new WorldPosition(region, point.X, point.Y);
        }
        return layout.Exit;
    }

    private static bool IsAxisAlignedRectangle(IReadOnlyList<GeometryPoint> footprint)
        => footprint.Count == 4 && Enumerable.Range(0, footprint.Count).All(index =>
        {
            var start = footprint[index]; var end = footprint[(index + 1) % footprint.Count];
            return Math.Abs(start.X - end.X) < .01 || Math.Abs(start.Y - end.Y) < .01;
        });

    private static bool InteriorPositionIsSafe(WorldPosition position, DungeonState dungeon)
    {
        var point = new GeometryPoint(position.X, position.Y);
        if (position.X < .5 || position.Y < .5 || position.X > dungeon.Width - .5 || position.Y > dungeon.Height - .5) return false;
        if (!PointInsideFootprint(point, dungeon.Footprint)) return false;
        if (dungeon.Furnishings?.Any(item => item.Properties.GetValueOrDefault("objectType") != "rug" && FurnitureContains(item, position, .38)) == true) return false;
        foreach (var wall in dungeon.Walls)
        {
            var closest = ClosestPointOnSegment(point, new GeometryPoint(wall.X1, wall.Y1), new GeometryPoint(wall.X2, wall.Y2));
            if (Math.Sqrt(Math.Pow(point.X - closest.X, 2) + Math.Pow(point.Y - closest.Y, 2)) >= .55) continue;
            if (wall.DoorStart >= 0)
            {
                var coordinate = Math.Abs(wall.X1 - wall.X2) < .01 ? point.Y : point.X;
                if (coordinate >= wall.DoorStart + .4 && coordinate <= wall.DoorEnd - .4) continue;
            }
            return false;
        }
        return true;
    }
}
