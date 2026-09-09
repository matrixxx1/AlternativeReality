using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private const int MaximumBusFleet = 8;
    private const double BusRouteActivationDistance = 250;

    private bool RefreshNearbyFleet(PlayerState[] people)
    {
        var selected = new HashSet<string>();
        // A passenger always retains their bus, including when riding beyond the nearby population.
        foreach (var bus in _buses.Values)
            if (people.Any(p => p.RidingBusId == bus.State.Id || WaitingForRoute(p, bus.RouteId))) selected.Add(bus.RouteId);
        var choices = new Dictionary<string, (RoadEdge[] Route, int Index, double Distance, double Along)>();
        foreach (var person in people.OrderByDescending(p => p.WaitingAtBusStopId is not null))
        {
            var nearest = new List<(string Id, RoadEdge[] Route, int Index, double Distance, double Along)>();
            foreach (var (id, route) in _transitNetwork!.Routes)
            {
                var best = double.PositiveInfinity; var index = 0; var along = 0d;
                for (var i = 0; i < route.Length; i++)
                {
                    var edge = route[i];
                    if (edge.Road.Position.Region != person.Position.Region) continue;
                    var distanceAlong = Math.Clamp((person.Position.X - edge.Start.X) * edge.Dx + (person.Position.Y - edge.Start.Y) * edge.Dy, 0, edge.Length);
                    var distance = edge.At(distanceAlong).Distance2D(person.Position);
                    if (distance >= best) continue;
                    best = distance; index = i; along = distanceAlong;
                }
                if (best <= BusRouteActivationDistance || WaitingForRoute(person, id))
                    nearest.Add((id, route, index, WaitingForRoute(person, id) ? -1 : best, along));
            }
            // Prefer existing nearby service to avoid moving buses as somebody walks along the street.
            foreach (var choice in nearest.OrderBy(c => c.Distance < 0 ? 0 : _buses.ContainsKey(c.Id) ? 1 : 2)
                .ThenBy(c => c.Distance).ThenBy(c => c.Id, StringComparer.Ordinal)
                .DistinctBy(c => c.Route[c.Index].Name).Take(2))
            {
                if (selected.Count >= MaximumBusFleet && !selected.Contains(choice.Id)) break;
                selected.Add(choice.Id); choices[choice.Id] = (choice.Route, choice.Index, choice.Distance, choice.Along);
            }
        }
        var changed = false;
        foreach (var id in selected.Where(id => !_buses.ContainsKey(id)).ToArray())
        {
            if (!choices.TryGetValue(id, out var choice)) continue;
            if (_buses.Count >= MaximumBusFleet)
            {
                var dormant = _buses.Keys.FirstOrDefault(key => !selected.Contains(key));
                if (dormant is null) continue;
                _buses.Remove(dormant); changed = true;
            }
            var ordered = choice.Route.Skip(choice.Index).Concat(choice.Route.Take(choice.Index)).ToArray();
            var bus = new SimulatedBus("bus:" + id, id, ordered);
            foreach (var offset in new[] { -12d, -24, -36, 12, 24, 36 })
            {
                var minimum = Math.Min(10, ordered[0].Length / 2);
                var along = Math.Clamp(choice.Along + offset, minimum, Math.Max(minimum, ordered[0].Length - 5));
                var position = ordered[0].At(along);
                var footprint = TransitGeometry.Footprint(position, ordered[0].Heading, 10, 3);
                if (!footprint.All(p => _loadedAreas.Values.Any(a => a.Contains(p.X, p.Y))) ||
                    people.Any(p => p.Position.Region == position.Region && p.Position.Distance2D(position) < 7) || _transitObstacles!.Hit(footprint) is not null ||
                    _buses.Values.Any(b => b.State.Position.Region == position.Region && b.State.Position.Distance2D(position) < 12 && TransitGeometry.Overlaps(footprint, TransitGeometry.Footprint(b.State.Position, b.State.HeadingRadians)))) continue;
                bus.Distance = along; bus.State = bus.State with { Position = position };
                _buses[id] = bus; changed = true; break;
            }
        }
        return changed;
    }
}
