using System.Globalization;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

// A directed graph of actual OSM nodes: crossings without a shared node are not junctions.
internal sealed class RoadTransitNetwork
{
    public const double StopSpacingMeters = 1609.344;
    public Dictionary<string, RoadEdge> Edges { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<RoadEdge>> Outgoing { get; } = new(StringComparer.Ordinal);
    public List<BusStopState> Stops { get; } = [];
    public Dictionary<string, List<BusStopState>> EdgeStops { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, RoadEdge[]> Routes { get; } = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoadTurnRestriction[]> _turnRestrictions = new(StringComparer.Ordinal);

    public RoadTransitNetwork(IEnumerable<CanonicalEntity> entities)
    {
        foreach (var road in entities.Where(e => e.Kind == EntityKind.Road && RoadClassification.AllowsBus(e.Properties)).OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            if (road.Properties.GetValueOrDefault("turnRestrictions") is { } rules)
                _turnRestrictions[road.Id] = System.Text.Json.JsonSerializer.Deserialize<RoadTurnRestriction[]>(rules, SharedJson.Options) ?? [];
            var ids = road.Properties.GetValueOrDefault("osmNodeIds")?.Split(',');
            var oneWay = RoadClassification.OneWay(road.Properties);
            for (var i = 0; i + 1 < road.Geometry.Count; i++)
            {
                var a = road.Geometry[i]; var b = road.Geometry[i + 1];
                if (Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2)) < .1) continue;
                string Node(int index) => ids?.Length == road.Geometry.Count ? "osm:" + ids[index] :
                    FormattableString.Invariant($"xy:{road.Geometry[index].X:F3}:{road.Geometry[index].Y:F3}:{road.Properties.GetValueOrDefault("layer") ?? "0"}:{road.Properties.GetValueOrDefault("bridge") ?? "no"}:{road.Properties.GetValueOrDefault("tunnel") ?? "no"}");
                if (oneWay >= 0) Add(new($"{road.Id}:{i}:+", road, Node(i), Node(i + 1), a, b, oneWay != 0));
                if (oneWay <= 0) Add(new($"{road.Id}:{i}:-", road, Node(i + 1), Node(i), b, a, oneWay != 0));
            }
        }
        CreateStops();
        CreateRoutes();
        ReduceStopDensity();
    }

    private void Add(RoadEdge edge)
    {
        Edges.Add(edge.Id, edge);
        if (!Outgoing.TryGetValue(edge.From, out var list)) Outgoing[edge.From] = list = [];
        list.Add(edge);
    }

    private static bool SameCorridor(RoadEdge a, RoadEdge b) => a.Road.Id == b.Road.Id ||
        a.Road.Properties.GetValueOrDefault("name") is { Length: > 0 } name && name == b.Road.Properties.GetValueOrDefault("name");

    private void CreateStops()
    {
        // Carry the mile counter across OSM way splits and shape nodes in each direction.
        var eligible = Edges.Values.Where(e => RoadClassification.AllowsStop(e.Road.Properties)).ToArray();
        var previous = eligible.GroupBy(e => e.To).ToDictionary(g => g.Key, g => g.ToArray());
        RoadEdge[] Next(RoadEdge e) => Outgoing.GetValueOrDefault(e.To, []).Where(n => n.To != e.From && SameCorridor(e, n) && RoadClassification.AllowsStop(n.Road.Properties)).ToArray();
        bool HasSinglePrevious(RoadEdge e) => previous.GetValueOrDefault(e.From, []).Count(p => p.From != e.To && SameCorridor(p, e)) == 1;
        var visited = new HashSet<string>();
        foreach (var first in eligible.OrderBy(HasSinglePrevious).ThenBy(e => e.Id, StringComparer.Ordinal))
        {
            if (visited.Contains(first.Id)) continue;
            var chain = new List<RoadEdge>(); var edge = first;
            while (visited.Add(edge.Id))
            {
                chain.Add(edge);
                var next = Next(edge);
                if (next.Length != 1 || !HasSinglePrevious(next[0])) break;
                edge = next[0];
            }
            var length = chain.Sum(e => e.Length);
            if (length < 30) continue;
            // Terminals make short streets usable; intermediate stops are exactly one mile apart.
            var distances = new List<double> { Math.Min(20, length / 2) };
            for (var d = distances[0] + StopSpacingMeters; d < length - 20; d += StopSpacingMeters) distances.Add(d);
            if (length - distances[^1] > StopSpacingMeters * .65) distances.Add(length - 20);
            foreach (var distance in distances)
            {
                var remaining = distance;
                var target = chain[^1];
                foreach (var part in chain) { target = part; if (remaining <= part.Length) break; remaining -= part.Length; }
                var position = target.At(remaining, target.Width / 2 + 1.5);
                var direction = Math.Abs(target.Dx) >= Math.Abs(target.Dy) ? target.Dx > 0 ? "eastbound" : "westbound" : target.Dy > 0 ? "northbound" : "southbound";
                var bench = position with { X = position.X + target.Dx * 1.6 + target.Dy * .5, Y = position.Y + target.Dy * 1.6 - target.Dx * .5 };
                var stop = new BusStopState(FormattableString.Invariant($"bus-stop:{target.Id}:{remaining:F3}"), target.Name, position, target.Id, remaining, direction, bench, target.Heading);
                Stops.Add(stop);
                if (!EdgeStops.TryGetValue(target.Id, out var list)) EdgeStops[target.Id] = list = [];
                list.Add(stop);
            }
        }
    }

    private void ReduceStopDensity()
    {
        const double minimumSpacing = 400;
        var servedEdges = Routes.Values.SelectMany(route => route).Select(e => e.Id).ToHashSet();
        var kept = new Dictionary<string, BusStopState>();
        var locations = new Dictionary<(int, int), List<WorldPosition>>();
        var candidates = Stops.Where(s =>
        {
            var edge = Edges[s.EdgeId];
            return (Math.Abs(edge.Dx) >= Math.Abs(edge.Dy) ? edge.Dx > 0 : edge.Dy > 0) ||
                !Outgoing.GetValueOrDefault(edge.To, []).Any(reverse => reverse.To == edge.From && reverse.Road.Id == edge.Road.Id);
        });
        foreach (var stop in candidates.OrderBy(s => RoadClassification.Classify(Edges[s.EdgeId].Road.Properties) == "mainRoad" ? 0 : 1)
            .ThenBy(s => s.Id, StringComparer.Ordinal))
        {
            var x = (int)Math.Floor(stop.Position.X / minimumSpacing); var y = (int)Math.Floor(stop.Position.Y / minimumSpacing);
            var nearby = false;
            for (var dx = -1; dx <= 1; dx++) for (var dy = -1; dy <= 1; dy++)
                if (locations.GetValueOrDefault((x + dx, y + dy), []).Any(p => p.Region == stop.Position.Region && p.Distance2D(stop.Position) < minimumSpacing)) nearby = true;
            if (nearby) continue;
            kept[stop.Id] = stop;
            if (!locations.TryGetValue((x, y), out var bucket)) locations[(x, y)] = bucket = [];
            bucket.Add(stop.Position);
            // Opposite directions share one stop location, instead of putting terminals at both ends of every short way.
            var edge = Edges[stop.EdgeId];
            var reverse = Outgoing.GetValueOrDefault(edge.To, []).FirstOrDefault(e => e.To == edge.From && e.Road.Id == edge.Road.Id && servedEdges.Contains(e.Id));
            if (reverse is null) continue;
            var along = edge.Length - stop.DistanceMeters;
            var position = reverse.At(along, reverse.Width / 2 + 1.5);
            var bench = position with { X = position.X + reverse.Dx * 1.6 + reverse.Dy * .5, Y = position.Y + reverse.Dy * 1.6 - reverse.Dx * .5 };
            var direction = Math.Abs(reverse.Dx) >= Math.Abs(reverse.Dy) ? reverse.Dx > 0 ? "eastbound" : "westbound" : reverse.Dy > 0 ? "northbound" : "southbound";
            var opposite = new BusStopState(FormattableString.Invariant($"bus-stop:{reverse.Id}:{along:F3}"), reverse.Name, position, reverse.Id, along, direction, bench, reverse.Heading);
            kept[opposite.Id] = opposite;
        }
        Stops.Clear(); Stops.AddRange(kept.Values);
        EdgeStops.Clear();
        foreach (var group in Stops.GroupBy(s => s.EdgeId)) EdgeStops[group.Key] = group.ToList();
        var passengerEdges = Stops.Select(s => s.EdgeId).ToHashSet();
        foreach (var id in Routes.Where(pair => !pair.Value.Any(e => passengerEdges.Contains(e.Id))).Select(pair => pair.Key).ToArray()) Routes.Remove(id);
    }

    public RoadEdge? Next(RoadEdge current, IReadOnlyDictionary<string, int> visits)
    {
        var candidates = Outgoing.GetValueOrDefault(current.To, []).Where(e => e.To != current.From && AllowsTurn(current,e)).ToArray();
        if (candidates.Length == 0) candidates = Outgoing.GetValueOrDefault(current.To, []).Where(e => e.To == current.From && AllowsTurn(current,e)).ToArray();
        return candidates.OrderBy(e => visits.GetValueOrDefault(e.Id)).ThenByDescending(e => SameCorridor(current, e))
            .ThenByDescending(e => current.Dx * e.Dx + current.Dy * e.Dy).ThenBy(e => e.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    internal bool AllowsTurn(RoadEdge from, RoadEdge to)
    {
        foreach (var rule in _turnRestrictions.GetValueOrDefault(from.Road.Id, []))
        {
            if (rule.ViaNodeId != from.To) continue;
            var target = rule.ToWayId == to.Road.Id;
            if (rule.Restriction == "no_u_turn") target &= to.To == from.From;
            if (rule.Restriction.StartsWith("no_", StringComparison.Ordinal) && target ||
                rule.Restriction.StartsWith("only_", StringComparison.Ordinal) && !target) return false;
        }
        return true;
    }

    private void CreateRoutes()
    {
        var covered = new HashSet<string>();
        foreach (var seed in Stops.Select(s => Edges[s.EdgeId]).DistinctBy(e => e.Id).OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            if (covered.Contains(seed.Id)) continue;
            var route = new List<RoadEdge> { seed };
            var visits = new Dictionary<string, int> { [seed.Id] = 1 };
            var current = seed;
            for (var i = 0; i < 255; i++)
            {
                var next = Next(current, visits);
                if (next is null || visits.ContainsKey(next.Id)) break;
                route.Add(next); visits[next.Id] = 1; current = next;
                if (current.To == seed.From) break;
            }
            if (current.To != seed.From)
            {
                // Close the service loop using directed roads; never reverse illegally on a one-way.
                var queue = new Queue<RoadEdge>(); queue.Enqueue(current);
                var back = new Dictionary<string, RoadEdge?> { [current.Id] = null };
                RoadEdge? final = null;
                while (queue.Count > 0 && final is null)
                {
                    var prior = queue.Dequeue();
                    foreach (var e in Outgoing.GetValueOrDefault(prior.To, []))
                    {
                        if (!AllowsTurn(prior,e) || !back.TryAdd(e.Id,prior)) continue;
                        if (e.To == seed.From && AllowsTurn(e,seed)) {final=e;break;}
                        queue.Enqueue(e);
                    }
                }
                if (final is null) continue;
                var tail = new List<RoadEdge>(); var cursor = final;
                while (back[cursor.Id] is { } e) { tail.Add(cursor); cursor = e; }
                tail.Reverse(); route.AddRange(tail);
            }
            if (!AllowsTurn(route[^1],seed)) continue;
            if (route.Count < 2) continue;
            Routes["bus-route:" + seed.Id] = route.ToArray();
            foreach (var e in route) covered.Add(e.Id);
        }
        Stops.RemoveAll(s => !covered.Contains(s.EdgeId));
        foreach (var list in EdgeStops.Values) list.RemoveAll(s => !covered.Contains(s.EdgeId));
    }
}

internal sealed record RoadEdge(string Id, CanonicalEntity Road, string From, string To, GeometryPoint Start, GeometryPoint End, bool OneWay)
{
    public double Length { get; } = Math.Sqrt(Math.Pow(End.X - Start.X, 2) + Math.Pow(End.Y - Start.Y, 2));
    public double Dx => (End.X - Start.X) / Length;
    public double Dy => (End.Y - Start.Y) / Length;
    public double Heading => Math.Atan2(Dy, Dx);
    public double Width => RoadClassification.Width(Road.Properties);
    public double LaneOffset => OneWay ? Math.Max(0, Width / 2 - 1.75) : Width / 4;
    public string Name => Road.Properties.GetValueOrDefault("name") ?? Road.Properties.GetValueOrDefault("ref") ?? "Local bus";
    public WorldPosition At(double distance, double? offset = null) => new(Road.Position.Region,
        Start.X + Dx * distance + Dy * (offset ?? LaneOffset), Start.Y + Dy * distance - Dx * (offset ?? LaneOffset), Start.Z + (End.Z - Start.Z) * Math.Clamp(distance / Length, 0, 1));
}
