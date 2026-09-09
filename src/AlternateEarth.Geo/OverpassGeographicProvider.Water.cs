using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public sealed partial class OverpassGeographicProvider
{
    private static HashSet<long> AddWaterRelations(JsonDocument document, IReadOnlyDictionary<long, long[]> ways,
        IReadOnlyDictionary<long, GeoCoordinate> nodes, GeographicArea area, LocalTangentProjection projection, List<CanonicalEntity> result)
    {
        var consumed = new HashSet<long>();
        var relations = document.RootElement.GetProperty("elements").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "relation" && e.TryGetProperty("tags", out _))
            .DistinctBy(e => e.GetProperty("id").GetInt64());
        foreach (var relation in relations)
        {
            var tags = ReadTags(relation);
            if (tags.GetValueOrDefault("type") != "multipolygon" ||
                !(tags.GetValueOrDefault("natural") == "water" || tags.GetValueOrDefault("waterway") == "riverbank")) continue;
            var members = relation.GetProperty("members").EnumerateArray()
                .Where(m => m.GetProperty("type").GetString() == "way")
                .Select(m => (Id: m.GetProperty("ref").GetInt64(), Role: m.GetProperty("role").GetString() ?? ""))
                .Where(m => m.Role is "outer" or "inner" or "").Distinct().ToArray();
            // Never bridge missing nodes or close an incomplete bank with a straight line.
            if (members.Any(m => !ways.TryGetValue(m.Id, out var ids) || ids.Any(id => !nodes.ContainsKey(id)))) continue;
            var outers = JoinWaterRings(members.Where(m => m.Role != "inner").Select(m => m.Id), ways);
            var inners = JoinWaterRings(members.Where(m => m.Role == "inner").Select(m => m.Id), ways);
            if (outers is null || inners is null || outers.Count == 0) continue;
            GeometryPoint[] Project(long[] ids) => ids.Select(id => projection.ProjectGeometry(nodes[id]))
                .Select(p => new GeometryPoint(p.X, p.Y, p.Z)).ToArray();
            var holes = inners.Select(r => Project(r.Nodes)).ToArray();
            foreach (var outer in outers)
            {
                var geometry = Project(outer.Nodes);
                if (!OverlapsArea(geometry, area.Bounds)) continue;
                var interior = holes.Where(r => WaterGeometry.InsideRing(geometry, r[0].X, r[0].Y)).Cast<IReadOnlyList<GeometryPoint>>().ToArray();
                var properties = tags.Where(p => KeepProperty(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
                result.Add(new CanonicalEntity($"geo:osm:relation:{relation.GetProperty("id").GetInt64()}:outer:{outer.Key}", EntityKind.Water,
                    new WorldPosition(area.Region, geometry.Average(p => p.X), geometry.Average(p => p.Y)), geometry, properties,
                    InteriorRings: interior.Length == 0 ? null : interior));
            }
            consumed.UnionWith(members.Select(m => m.Id));
        }
        return consumed;
    }

    // Member order and direction in OSM are arbitrary. Join by shared node IDs,
    // retaining separate outer polygons and holes, rather than connecting nearby points.
    private static List<(long Key, long[] Nodes)>? JoinWaterRings(IEnumerable<long> ids, IReadOnlyDictionary<long, long[]> ways)
    {
        var remaining = new SortedDictionary<long, long[]>(ids.Distinct().ToDictionary(id => id, id => ways[id]));
        var rings = new List<(long, long[])>();
        while (remaining.Count > 0)
        {
            var first = remaining.First(); remaining.Remove(first.Key);
            if (first.Value.Length < 2) return null;
            var ring = new List<long>(first.Value);
            while (ring[^1] != ring[0])
            {
                var next = remaining.FirstOrDefault(p => p.Value.Length >= 2 && (p.Value[0] == ring[^1] || p.Value[^1] == ring[^1]));
                if (next.Value is null) return null;
                var points = next.Value[0] == ring[^1] ? next.Value : next.Value.Reverse().ToArray();
                ring.AddRange(points.Skip(1)); remaining.Remove(next.Key);
            }
            if (ring.Count < 4) return null;
            rings.Add((first.Key, ring.ToArray()));
        }
        return rings;
    }

    private static bool OverlapsArea(IReadOnlyList<GeometryPoint> geometry, WorldBounds bounds) =>
        geometry.Count > 0 && geometry.Min(p => p.X) <= bounds.MaximumX && geometry.Max(p => p.X) >= bounds.MinimumX &&
        geometry.Min(p => p.Y) <= bounds.MaximumY && geometry.Max(p => p.Y) >= bounds.MinimumY;
}
