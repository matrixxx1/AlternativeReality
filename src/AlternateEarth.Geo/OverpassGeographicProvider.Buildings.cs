using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public sealed partial class OverpassGeographicProvider
{
    private static bool ValidBuildingFootprint(IReadOnlyList<GeometryPoint> points)
    {
        if (points.Count < 4 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return false;
        // Keep complete, concave footprints. Never invent walls from incomplete OSM ways.
        var ring = new List<GeometryPoint>();
        foreach (var point in points)
            if (ring.Count == 0 || ring[^1].X != point.X || ring[^1].Y != point.Y) ring.Add(point);
        if (ring.Count > 1 && ring[0].X == ring[^1].X && ring[0].Y == ring[^1].Y) ring.RemoveAt(ring.Count - 1);
        if (ring.Count < 3) return false;
        var origin = ring[0]; double twiceArea = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            twiceArea += (a.X-origin.X)*(b.Y-origin.Y)-(b.X-origin.X)*(a.Y-origin.Y);
        }
        if (Math.Abs(twiceArea) < 2) return false;
        static double Cross(GeometryPoint a, GeometryPoint b, GeometryPoint c) => (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        static bool OnSegment(GeometryPoint a, GeometryPoint b, GeometryPoint c) => Math.Abs(Cross(a,b,c))<1e-8 && c.X>=Math.Min(a.X,b.X)-1e-8 && c.X<=Math.Max(a.X,b.X)+1e-8 && c.Y>=Math.Min(a.Y,b.Y)-1e-8 && c.Y<=Math.Max(a.Y,b.Y)+1e-8;
        for (var i = 0; i < ring.Count; i++)
            for (var j = i + 1; j < ring.Count; j++)
            {
                if (j == i + 1 || i == 0 && j == ring.Count - 1) continue;
                var a=ring[i];var b=ring[(i+1)%ring.Count];var c=ring[j];var d=ring[(j+1)%ring.Count];
                var ac=Cross(a,b,c);var ad=Cross(a,b,d);var ca=Cross(c,d,a);var cb=Cross(c,d,b);
                if (ac*ad<0 && ca*cb<0 || OnSegment(a,b,c) || OnSegment(a,b,d) || OnSegment(c,d,a) || OnSegment(c,d,b)) return false;
            }
        return true;
    }
}
