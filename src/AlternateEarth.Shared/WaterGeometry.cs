using System.Globalization;

namespace AlternateEarth.Shared;

public static class WaterGeometry
{
    public static bool IsPolygon(CanonicalEntity entity) => entity.Geometry.Count >= 4 && entity.Geometry[0] == entity.Geometry[^1];

    public static double Width(CanonicalEntity entity) =>
        double.TryParse(entity.Properties.GetValueOrDefault("widthMeters") ?? entity.Properties.GetValueOrDefault("width"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var width) && double.IsFinite(width) && width > 0 ? width : 3;

    public static bool Contains(CanonicalEntity entity, double x, double y) => IsPolygon(entity)
        ? InsideRing(entity.Geometry, x, y) && !(entity.InteriorRings?.Any(ring => InsideRing(ring, x, y)) ?? false)
        : DistanceToRing(entity.Geometry, x, y) <= Width(entity) / 2;

    public static double ShoreDistance(CanonicalEntity entity, double x, double y)
    {
        var distance = DistanceToRing(entity.Geometry, x, y);
        foreach (var ring in entity.InteriorRings ?? []) distance = Math.Min(distance, DistanceToRing(ring, x, y));
        return distance;
    }

    public static bool InsideRing(IReadOnlyList<GeometryPoint> ring, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var a = ring[i]; var b = ring[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    private static double DistanceToRing(IReadOnlyList<GeometryPoint> ring, double x, double y)
    {
        var best = double.MaxValue;
        for (var i = 1; i < ring.Count; i++)
        {
            var a = ring[i - 1]; var b = ring[i]; var dx = b.X - a.X; var dy = b.Y - a.Y;
            var square = dx * dx + dy * dy;
            var t = square == 0 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / square, 0, 1);
            best = Math.Min(best, Math.Sqrt(Math.Pow(x - a.X - t * dx, 2) + Math.Pow(y - a.Y - t * dy, 2)));
        }
        return best;
    }
}
