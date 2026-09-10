using AlternateEarth.Shared;

namespace AlternateEarth.Server;

internal static class TransitGeometry
{
    public const double BusLength = 9;
    public const double BusWidth = 2.5;
    public static bool InBusWarningPath(BusState bus, WorldPosition person, double speed)
    {
        if (person.Region != bus.Position.Region || speed <= .1) return false;
        var x = person.X - bus.Position.X; var y = person.Y - bus.Position.Y;
        var dx = Math.Cos(bus.HeadingRadians); var dy = Math.Sin(bus.HeadingRadians);
        var ahead = x * dx + y * dy;
        var side = Math.Abs(x * -dy + y * dx);
        return ahead >= 0 && ahead <= BusLength / 2 + Math.Clamp(speed * 1.4, 3, 24) && side <= BusWidth / 2 + .65;
    }
    public static WorldPosition ClosestPoint(BusState bus, WorldPosition point)
    {
        var dx = Math.Cos(bus.HeadingRadians); var dy = Math.Sin(bus.HeadingRadians);
        var x = point.X - bus.Position.X; var y = point.Y - bus.Position.Y;
        var along = Math.Clamp(x * dx + y * dy, -BusLength / 2, BusLength / 2);
        var side = Math.Clamp(x * -dy + y * dx, -BusWidth / 2, BusWidth / 2);
        return bus.Position with { X = bus.Position.X + dx * along - dy * side, Y = bus.Position.Y + dy * along + dx * side };
    }
    public static bool SegmentHitsBus(WorldPosition from, WorldPosition to, BusState bus, double radius)
    {
        if (from.Region != bus.Position.Region) return false;
        var footprint = Footprint(bus.Position, bus.HeadingRadians, BusLength + radius * 2, BusWidth + radius * 2);
        // Let a player already caught inside a vehicle step out instead of trapping them there.
        if (Contains(footprint, from)) return Contains(footprint, to) && to.Distance2D(bus.Position) <= from.Distance2D(bus.Position);
        if (Contains(footprint, to)) return true;
        var a = new GeometryPoint(from.X, from.Y); var b = new GeometryPoint(to.X, to.Y);
        for (var i = 0; i < footprint.Length; i++)
            if (Intersects(a, b, footprint[i], footprint[(i + 1) % footprint.Length])) return true;
        return false;
    }
    public static GeometryPoint[] Footprint(WorldPosition p, double heading, double length = BusLength, double width = BusWidth)
    {
        var dx = Math.Cos(heading); var dy = Math.Sin(heading);
        return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(c =>
            new GeometryPoint(p.X + dx * c.Item1 * length / 2 - dy * c.Item2 * width / 2,
                p.Y + dy * c.Item1 * length / 2 + dx * c.Item2 * width / 2)).ToArray();
    }

    public static bool Contains(IReadOnlyList<GeometryPoint> polygon, WorldPosition p)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    public static bool Overlaps(IReadOnlyList<GeometryPoint> a, IReadOnlyList<GeometryPoint> b)
    {
        if (a.Count < 3 || b.Count < 3) return false;
        if (a.Max(p => p.X) < b.Min(p => p.X) || b.Max(p => p.X) < a.Min(p => p.X) ||
            a.Max(p => p.Y) < b.Min(p => p.Y) || b.Max(p => p.Y) < a.Min(p => p.Y)) return false;
        var region = new RegionId(0, 0);
        if (Contains(a, new(region, b[0].X, b[0].Y)) || Contains(b, new(region, a[0].X, a[0].Y))) return true;
        for (var i = 0; i < a.Count; i++)
        for (var j = 0; j < b.Count; j++)
            if (Intersects(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
        return false;
    }

    private static bool Intersects(GeometryPoint a, GeometryPoint b, GeometryPoint c, GeometryPoint d)
    {
        static double Cross(GeometryPoint p, GeometryPoint q, GeometryPoint r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Math.Max(a.X, b.X) >= Math.Min(c.X, d.X) && Math.Max(c.X, d.X) >= Math.Min(a.X, b.X) &&
            Math.Max(a.Y, b.Y) >= Math.Min(c.Y, d.Y) && Math.Max(c.Y, d.Y) >= Math.Min(a.Y, b.Y) &&
            Cross(a, b, c) * Cross(a, b, d) <= 0 && Cross(c, d, a) * Cross(c, d, b) <= 0;
    }

    public static IReadOnlyList<GeometryPoint> ObjectFootprint(CanonicalEntity e) => e.Geometry.Count >= 3 ? e.Geometry :
        Footprint(e.Position, Number(e, "rotationDegrees", 0) * Math.PI / 180, Number(e, "lengthMeters", 4.5), Number(e, "widthMeters", 1.9));
    public static double Number(CanonicalEntity e, string key, double fallback) => double.TryParse(e.Properties.GetValueOrDefault(key),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : fallback;
}

internal sealed class TransitObstacleIndex
{
    private readonly Dictionary<(int, int), List<CanonicalEntity>> _cells = new();
    public TransitObstacleIndex(IEnumerable<CanonicalEntity> entities)
    {
        foreach (var entity in entities.Where(e => e.Kind is EntityKind.Building or EntityKind.Vehicle or EntityKind.PlayerStructure or EntityKind.Fence))
        {
            var p = TransitGeometry.ObjectFootprint(entity);
            for (var x = Cell(p.Min(v => v.X)); x <= Cell(p.Max(v => v.X)); x++)
            for (var y = Cell(p.Min(v => v.Y)); y <= Cell(p.Max(v => v.Y)); y++)
            {
                if (!_cells.TryGetValue((x, y), out var list)) _cells[(x, y)] = list = [];
                list.Add(entity);
            }
        }
    }
    private static int Cell(double x) => (int)Math.Floor(x / 32);
    public CanonicalEntity? Hit(IReadOnlyList<GeometryPoint> footprint)
    {
        var seen = new HashSet<string>();
        for (var x = Cell(footprint.Min(p => p.X)); x <= Cell(footprint.Max(p => p.X)); x++)
        for (var y = Cell(footprint.Min(p => p.Y)); y <= Cell(footprint.Max(p => p.Y)); y++)
        foreach (var e in _cells.GetValueOrDefault((x, y), []))
            if (seen.Add(e.Id) && TransitGeometry.Overlaps(footprint, TransitGeometry.ObjectFootprint(e))) return e;
        return null;
    }
}
