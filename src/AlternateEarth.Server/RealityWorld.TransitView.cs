using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public TransitSnapshot CreateTransitView(string playerId, WorldBounds? view = null)
    {
        if (!_players.TryGetValue(playerId, out var player)) return new([], [], []);
        var source = GetTransitSnapshot();
        var origin = player.LocationId == "outdoor" ? player.Position : _returnPositions.GetValueOrDefault(playerId, player.Position);
        bool Nearby(WorldPosition position) => position.Region == origin.Region &&
            (position.Distance2D(origin) <= 750 || view is not null && view.Contains(position.X, position.Y));
        var buses = source.Buses.Where(b => b.Id == player.RidingBusId || Nearby(b.Position)).ToArray();
        var stops = source.Stops.Where(s => s.Id == player.WaitingAtBusStopId || Nearby(s.Position)).ToArray();
        var stopIds = stops.Select(s => s.Id).ToHashSet();
        var busRoutes = buses.Select(b => b.RouteId).ToHashSet();
        var routes = source.Routes.Where(r => busRoutes.Contains(r.Id) || r.StopIds.Any(stopIds.Contains))
            .Select(r => r with { Path = Array.Empty<WorldPosition>(), StopIds = r.StopIds.Where(stopIds.Contains).ToArray() }).ToArray();
        // Full geometry is requested only when someone opens a route preview.
        return new(stops, buses, routes);
    }

    public IReadOnlyList<BusState> NearbyBuses(string playerId, WorldBounds? view = null)
    {
        if (!_players.TryGetValue(playerId, out var player)) return [];
        var origin = player.LocationId == "outdoor" ? player.Position : _returnPositions.GetValueOrDefault(playerId, player.Position);
        return GetTransitSnapshot().Buses.Where(b => b.Id == player.RidingBusId || b.Position.Region == origin.Region &&
            (b.Position.Distance2D(origin) <= 750 || view is not null && view.Contains(b.Position.X, b.Position.Y))).ToArray();
    }
}
