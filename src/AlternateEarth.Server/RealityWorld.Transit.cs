using System.Globalization;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed record TransitTick(TransitSnapshot Transit, IReadOnlyList<PlayerState> Players, IReadOnlyList<ActorState> Actors,
    IReadOnlyList<string> RemovedActors, IReadOnlyList<CombatEvent> Combat, IReadOnlyList<CanonicalEntity> Objects, bool NetworkChanged, IReadOnlyList<WorldSoundEvent>? Sounds = null);

public sealed partial class RealityWorld
{
    private readonly SemaphoreSlim _transitLock = new(1, 1);
    private RoadTransitNetwork? _transitNetwork;
    private TransitObstacleIndex? _transitObstacles;
    private WorldNavigation? _transitNavigation;
    private readonly Dictionary<string, SimulatedBus> _buses = new();
    private TransitSnapshot _transitSnapshot = new([], [], []);
    private int _transitRevision;
    private int _transitBuiltRevision = -1;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, WorldPosition> _busReturnPositions = new();
    public TransitSnapshot GetTransitSnapshot() => _transitSnapshot;

    private void EnsureNotOnBus(string playerId)
    {
        if (_players.TryGetValue(playerId, out var player) && player.RidingBusId is not null)
            throw new InvalidOperationException("Use Get off bus now first.");
    }

    private PlayerState TransitPersistenceState(PlayerState player) => player with
    {
        Position = player.RidingBusId is not null ? _busReturnPositions.GetValueOrDefault(player.Id, player.Position) : player.Position,
        RidingBusId = null, WaitingAtBusStopId = null
    };

    private sealed class SimulatedBus(string id, string routeId, RoadEdge[] route)
    {
        public string RouteId = routeId;
        public RoadEdge[] Route = route;
        public int EdgeIndex;
        public double Distance = Math.Min(10, route[0].Length / 2);
        public BusState State = new(id, routeId, route[0].Name, route[0].At(Math.Min(10, route[0].Length / 2)), route[0].Heading);
        public double Dwell;
        public double HornCooldown;
        public int HornCount;
        public BusTurnQueue Turn = new();
        public List<BusBreadcrumb> History = new();
        public double ReverseMeters;
        public double TurnExitDistance;
        public bool TurningAround;
        public HashSet<string> ServedStops = [];
        public HashSet<string> HitPeople = [];
        public string? LastObstacle;
        public WorldPosition? ServiceOrigin;
        public WorldPosition? ServiceTarget;
        public double ServiceRetry;
        public RoadEdge Edge => Route[EdgeIndex];
    }

    private double _fleetRefreshSeconds;
    private int _fleetPopulation;

    private bool RefreshTransitNetwork()
    {
        var revision = Volatile.Read(ref _transitRevision);
        if (_transitBuiltRevision == revision)
        {
            if (_transitNavigation != _navigation)
            {
                _transitObstacles = new(_baseEntities.Values.Concat(_realityEntities.Values).Where(e => e.Properties.GetValueOrDefault("subtype") != "haneyPickup"));
                _transitNavigation = _navigation;
            }
            return false;
        }
        var entities = _baseEntities.Values.Concat(_realityEntities.Values).ToArray();
        _transitNetwork = new(entities);
        _transitObstacles = new(entities.Where(e => e.Properties.GetValueOrDefault("subtype") != "haneyPickup"));
        _transitNavigation = _navigation;
        // Keep seats out of nearby buildings/water, and retain an occupied bench on expansion.
        var oldStops = _transitSnapshot.Stops.ToDictionary(s => s.Id);
        for (var index = 0; index < _transitNetwork.Stops.Count; index++)
        {
            var stop = _transitNetwork.Stops[index];
            var edge = _transitNetwork.Edges[stop.EdgeId];
            var bench = oldStops.GetValueOrDefault(stop.Id)?.BenchPosition ?? stop.BenchPosition ?? stop.Position;
            if (Navigation.IsBlocked(bench.X, bench.Y) || Navigation.TerrainAt(bench.X, bench.Y) == TerrainType.DeepWater)
            {
                foreach (var along in new[] { -1.6, 1.6, -1.2, 1.2 })
                {
                    var candidate = stop.Position with { X = stop.Position.X + edge.Dx * along, Y = stop.Position.Y + edge.Dy * along };
                    if (Navigation.IsBlocked(candidate.X, candidate.Y) || Navigation.TerrainAt(candidate.X, candidate.Y) == TerrainType.DeepWater) continue;
                    bench = candidate; break;
                }
            }
            var updated = stop with { BenchPosition = bench with { Z = Navigation.ElevationAt(bench.X, bench.Y) } };
            _transitNetwork.Stops[index] = updated;
            var edgeStops = _transitNetwork.EdgeStops[stop.EdgeId];
            edgeStops[edgeStops.FindIndex(s => s.Id == stop.Id)] = updated;
        }
        // Do not move an existing stop out from under somebody waiting when a way is extended.
        foreach (var stop in _transitSnapshot.Stops)
        {
            if (!_players.Values.Any(p => p.WaitingAtBusStopId == stop.Id)) continue;
            if (!_transitNetwork.Edges.ContainsKey(stop.EdgeId) || _transitNetwork.Stops.Any(s => s.Id == stop.Id)) continue;
            _transitNetwork.Stops.Add(stop);
            if (!_transitNetwork.EdgeStops.TryGetValue(stop.EdgeId,out var list)) _transitNetwork.EdgeStops[stop.EdgeId]=list=[];
            list.Add(stop);
        }
        // Existing buses retain their promised itinerary when neighboring blocks arrive.
        _transitBuiltRevision = revision;
        PublishTransit(true);
        return true;
    }

    private void PublishTransit(bool networkChanged = false)
    {
        if (!networkChanged)
        {
            _transitSnapshot = _transitSnapshot with { Buses = _buses.Values.Select(b => b.State).ToArray() };
            return;
        }
        var stops = networkChanged ? _transitNetwork?.Stops.ToArray() ?? [] : _transitSnapshot.Stops.ToArray();
        var stopsByEdge = stops.GroupBy(s => s.EdgeId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.DistanceMeters).ToArray());
        var routes = networkChanged ? _buses.Values.Select(bus => new BusRouteState(bus.RouteId, bus.Route[0].Name,
            bus.Route.SelectMany(e => new[] { e.At(0), e.At(e.Length) }).ToArray(),
            bus.Route.SelectMany(e => stopsByEdge.GetValueOrDefault(e.Id, []).Select(s => s.Id)).Distinct().ToArray())).ToArray() : _transitSnapshot.Routes;
        var served = routes.SelectMany(r => r.StopIds).ToHashSet();
        _transitSnapshot = new(stops.Where(s => served.Contains(s.Id)).ToArray(), _buses.Values.Select(b => b.State).ToArray(), routes);
    }

    public async Task<PlayerState> WaitForBusAsync(string playerId, string stopId, CancellationToken cancellationToken = default)
    {
        await _transitLock.WaitAsync(cancellationToken);
        try
        {
            RefreshTransitNetwork();
            if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
            if (player.RidingBusId is not null) throw new InvalidOperationException("You are already on a bus.");
            if (player.LocationId != "outdoor" || IsGasAsleep(playerId) || IsProbulatorAbducted(playerId)) throw new InvalidOperationException("You cannot wait for a bus right now.");
            var stop = _transitSnapshot.Stops.FirstOrDefault(s => s.Id == stopId) ?? throw new InvalidOperationException("This stop has no bus service.");
            if (player.Position.Region != stop.Position.Region || player.Position.Distance2D(stop.Position) > 3)
                throw new InvalidOperationException("Move within 3 meters of this bus stop, on its side of the road.");
            var edge = _transitNetwork!.Edges[stop.EdgeId];
            if (RightSideDistance(player.Position, edge) <= 0) throw new InvalidOperationException("Cross to this stop's side of the road before waiting.");
            var seat = stop.BenchPosition ?? stop.Position;
            if (!Navigation.CanTraverse(player.Position, seat, true)) throw new InvalidOperationException("Move closer to the bench on this side of the road.");
            var updated = player with { Position = seat, WaitingAtBusStopId = stopId, TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Version = player.Version + 1 };
            await SavePlayerAsync(updated, cancellationToken);
            _fleetRefreshSeconds = 0;
            return _players[playerId];
        }
        finally { _transitLock.Release(); }
    }

    public async Task<PlayerState> CancelBusWaitAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await _transitLock.WaitAsync(cancellationToken);
        try
        {
            if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
            if (player.WaitingAtBusStopId is null) return player;
            await SavePlayerAsync(player with { WaitingAtBusStopId = null, Version = player.Version + 1 }, cancellationToken);
            return _players[playerId];
        }
        finally { _transitLock.Release(); }
    }

    public async Task<PlayerState> GetOffBusAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await _transitLock.WaitAsync(cancellationToken);
        try
        {
            if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
            var bus = _buses.Values.FirstOrDefault(b => b.State.Id == player.RidingBusId) ?? throw new InvalidOperationException("You are not on a bus.");
            bus.Dwell = 2;
            bus.State = bus.State with { SpeedMetersPerSecond = 0, Status = "dropping off", Version = bus.State.Version + 1 };
            var position = FindBusExit(bus);
            if (position is null) throw new InvalidOperationException("The right-hand exit is blocked. The bus has stopped; try again when the way is clear.");
            await SavePlayerAsync(player with { Position = position.Value, RidingBusId = null, WaitingAtBusStopId = null, TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Version = player.Version + 1 }, cancellationToken);
            PublishTransit();
            return _players[playerId];
        }
        finally { _transitLock.Release(); }
    }

    private WorldPosition? FindBusExit(SimulatedBus bus)
    {
        var p = bus.State.Position; var dx = Math.Cos(bus.State.HeadingRadians); var dy = Math.Sin(bus.State.HeadingRadians);
        // Search only the passenger-door (right) side, never across the centerline.
        for (var side = Math.Max(2.5, bus.Edge.Width / 2 + 1.5 - bus.Edge.LaneOffset); side <= 15; side += .5)
        foreach (var along in new[] { 2d, 0, -2, 4d, -4 })
        {
            var candidate = p with { X = p.X + dy * side + dx * along, Y = p.Y - dx * side + dy * along };
            if (!Navigation.IsBlocked(candidate.X, candidate.Y) && Navigation.TerrainAt(candidate.X, candidate.Y) is not (TerrainType.DeepWater or TerrainType.ShallowWater) &&
                !_buses.Values.Any(b => TransitGeometry.Contains(TransitGeometry.Footprint(b.State.Position, b.State.HeadingRadians, 10, 3.5), candidate)))
                return candidate with { Z = Navigation.ElevationAt(candidate.X, candidate.Y) };
        }
        return null;
    }

    private static double RightSideDistance(WorldPosition p, RoadEdge edge) => (p.X - edge.Start.X) * edge.Dy - (p.Y - edge.Start.Y) * edge.Dx;

    public async Task<TransitTick> AdvanceTransitAsync(TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        await _transitLock.WaitAsync(cancellationToken);
        try
        {
            var changedNetwork = RefreshTransitNetwork();
            var players = new Dictionary<string, PlayerState>(); var actors = new Dictionary<string, ActorState>();
            var sounds = new List<WorldSoundEvent>();
            var removed = new HashSet<string>(); var combat = new List<CombatEvent>(); var objects = new Dictionary<string, CanonicalEntity>();
            var dt = Math.Clamp(elapsed.TotalSeconds * Configuration.GameSpeed, 0, 1);
            var people = _players.Values.Where(p => p.LocationId == "outdoor").ToArray();
            if (changedNetwork || people.Length != _fleetPopulation || (_fleetRefreshSeconds -= Math.Max(0, elapsed.TotalSeconds)) <= 0)
            {
                _fleetPopulation = people.Length;
                _fleetRefreshSeconds = 1;
                changedNetwork |= RefreshNearbyFleet(people);
            }
            var loadedAreas = _loadedAreas.Values.ToArray();
            var outdoorActors = _actors.Values.Where(a => a.LocationId == "outdoor").ToArray();
            var anyoneWaiting = people.Any(p => p.WaitingAtBusStopId is not null);
            foreach (var bus in _buses.Values.OrderBy(b => b.State.Id, StringComparer.Ordinal))
            {
                bus.HornCooldown = Math.Max(0, bus.HornCooldown - Math.Max(0, elapsed.TotalSeconds));
                var serviceNeeded=people.Any(p => p.RidingBusId == bus.State.Id || WaitingForRoute(p,bus.RouteId));
                if (!serviceNeeded || bus.State.HealthHearts<=0 || bus.ServiceOrigin is not null)
                    MoveBusIntoServicePosition(bus,serviceNeeded && bus.State.HealthHearts>0,dt,people,outdoorActors);
                else
                {
                bus.HitPeople.RemoveWhere(id =>
                {
                    WorldPosition? p = _players.TryGetValue(id, out var player) ? player.Position : _actors.TryGetValue(id, out var actor) ? actor.Position : null;
                    return p is null || p.Value.Region != bus.State.Position.Region || p.Value.Distance2D(bus.State.Position) > 18;
                });
                if (bus.ReverseMeters > 0) ReverseBusToYield(bus, dt, people, outdoorActors);
                else if (bus.Dwell > 0) { bus.Dwell -= dt; bus.State = bus.State with { SpeedMetersPerSecond = 0 }; }
                else if (bus.State.HealthHearts <= 0) bus.State = bus.State with { Status = "disabled", SpeedMetersPerSecond = 0 };
                else
                {
                    var speed = bus.Turn.Count > 0 ? bus.TurningAround ? 1.2 : 3 : BusRoadSpeed(bus.Edge) * (_fleeingVehicles.GetValueOrDefault(bus.State.Id) > DateTimeOffset.UtcNow ? 1.35 : 1);
                    if (dt > 0) WarnBusPeople(bus, speed, people, outdoorActors, sounds);
                    var budget = speed * dt;
                    while (budget > .001)
                    {
                        var step = Math.Min(.25, budget); var wasTurning = bus.Turn.Count > 0;
                        var candidate = NextBusPosition(bus, step);
                        if (!wasTurning && bus.Turn.Count > 0) { speed = bus.TurningAround ? 1.2 : 3; budget = Math.Min(budget, speed * dt); }
                        var footprint = TransitGeometry.Footprint(candidate.Position, candidate.Heading);
                        // Do not enter unactivated geography: buildings/cars there have not loaded yet.
                        if (!footprint.All(p => loadedAreas.Any(a => a.Contains(p.X, p.Y))))
                        { bus.State = bus.State with { Status = "waiting for map", SpeedMetersPerSecond = 0 }; break; }
                        if (HaneyBlocks(footprint)) { bus.Dwell = .25; bus.State = bus.State with { Status = "yielding to merchant", SpeedMetersPerSecond = 0 }; break; }
                        var obstacle = _transitObstacles!.Hit(footprint);
                        var trafficFootprint = TransitGeometry.Footprint(candidate.Position, candidate.Heading, 10, 3);
                        var other = _buses.Values.FirstOrDefault(b => b != bus && b.State.Position.Distance2D(candidate.Position) < 14 && TransitGeometry.Overlaps(trafficFootprint, TransitGeometry.Footprint(b.State.Position, b.State.HeadingRadians)));
                        if (other is not null)
                        {
                            // Traffic waits rather than repeatedly damaging and disabling both buses.
                            bus.LastObstacle = other.State.Id; bus.Dwell = .25;
                            bus.State = bus.State with { Status = "yielding", SpeedMetersPerSecond = 0, Version = bus.State.Version + 1 };
                            if (other.LastObstacle == bus.State.Id && bus.History.Count > 0 && other.ReverseMeters <= 0 &&
                                (StringComparer.Ordinal.Compare(bus.State.Id, other.State.Id) > 0 || other.History.Count == 0))
                            { bus.ReverseMeters = 12; bus.Dwell = 0; }
                            break;
                        }
                        if (obstacle is not null)
                        {
                            var id = obstacle.Id;
                            if (bus.LastObstacle != id)
                            {
                                var damage = Math.Clamp(speed * 2, 1, 25);
                                bus.State = bus.State with { HealthHearts = Math.Max(0, bus.State.HealthHearts - damage) };
                                objects[id] = await DamageBusObstacleAsync(obstacle, damage, cancellationToken);
                                combat.Add(new(bus.State.Id, id, "busCollision", bus.State.Position, candidate.Position, true, damage, false, "Bus collision."));
                            }
                            bus.LastObstacle = id; bus.Dwell = 2;
                            bus.State = bus.State with { Status = "blocked", SpeedMetersPerSecond = 0, Version = bus.State.Version + 1 };
                            break;
                        }
                        bus.LastObstacle = null;
                        var travelled = bus.State.Position.Distance2D(candidate.Position);
                        RememberBusStep(bus);
                        bus.State = bus.State with { Position = candidate.Position, HeadingRadians = candidate.Heading, SpeedMetersPerSecond = speed,
                            Status = bus.Turn.Count > 0 ? bus.TurningAround ? "turning around" : "turning" : "driving", Version = bus.State.Version + 1 };
                        CommitBusStep(bus, travelled, wasTurning);
                        budget -= Math.Max(.01, travelled);
                        await HitBusPeopleAsync(bus, footprint, people, outdoorActors, players, actors, removed, combat, cancellationToken);
                        if (anyoneWaiting && await BoardWaitingPassengersAsync(bus, players, cancellationToken)) break;
                    }
                }
                }
                foreach (var passenger in people.Where(p => p.RidingBusId == bus.State.Id))
                {
                    if (passenger.LocationId != "outdoor" || passenger.Abduction is not null)
                    {
                        var released = passenger with { RidingBusId = null, WaitingAtBusStopId = null, Version = passenger.Version + 1 };
                        await SavePlayerAsync(released, cancellationToken); players[released.Id] = _players[released.Id]; continue;
                    }
                    var updated = passenger with { Position = bus.State.Position, SpeedMetersPerSecond = bus.State.SpeedMetersPerSecond, Version = passenger.Version + 1 };
                    if (bus.State.HealthHearts <= 0 && bus.State.SpeedMetersPerSecond==0 && FindBusExit(bus) is { } exit) updated = updated with { Position = exit, RidingBusId = null, SpeedMetersPerSecond = 0 };
                    // Passenger movement is authoritative in memory; persist a safe curb position at boarding/drop-off,
                    // not a database transaction for every 100 ms vehicle tick.
                    if (updated.RidingBusId is null) await SavePlayerAsync(updated, cancellationToken);
                    else _players.TryUpdate(updated.Id, updated, passenger);
                    if (_players.TryGetValue(updated.Id, out var currentPassenger)) players[updated.Id] = currentPassenger;
                }
            }
            PublishTransit(changedNetwork);
            return new(_transitSnapshot, players.Values.ToArray(), actors.Values.ToArray(), removed.ToArray(), combat, objects.Values.ToArray(), changedNetwork, sounds);
        }
        finally { _transitLock.Release(); }
    }

    private static double BusRoadSpeed(RoadEdge edge)
    {
        double speed = RoadClassification.Classify(edge.Road.Properties) is "freeway" or "highway" ? 18 : RoadClassification.Classify(edge.Road.Properties) == "mainRoad" ? 11 : 7;
        var text = edge.Road.Properties.GetValueOrDefault("maxspeed") ?? "";
        if (double.TryParse(text.Replace("mph", "", StringComparison.OrdinalIgnoreCase).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var limit) && limit > 0)
            speed = Math.Min(speed, limit * (text.Contains("mph", StringComparison.OrdinalIgnoreCase) ? .44704 : 1 / 3.6));
        return speed;
    }

    private (WorldPosition Position, double Heading) NextBusPosition(SimulatedBus bus, double distance)
    {
        var edge = bus.Edge; var next = bus.Route[(bus.EdgeIndex + 1) % bus.Route.Length];
        var trim = Math.Min(8, Math.Min(edge.Length, next.Length) / 3);
        if (bus.Turn.Count == 0 && bus.Distance >= edge.Length - trim - .001)
        {
            var start = bus.State.Position; var end = next.At(trim);
            bus.TurningAround = next.To == edge.From;
            var control = bus.TurningAround ? Math.Max(8, edge.Width) : Math.Max(2, start.Distance2D(end) * .55);
            var p1 = start with { X = start.X + edge.Dx * control, Y = start.Y + edge.Dy * control };
            var p2 = end with { X = end.X - next.Dx * control, Y = end.Y - next.Dy * control };
            var count = (int)Math.Ceiling((start.Distance2D(end) + control * 2) / .15);
            for (var i = 1; i <= count; i++)
            {
                var t = i / (double)count; var u = 1 - t;
                var p = start with { X = u*u*u*start.X + 3*u*u*t*p1.X + 3*u*t*t*p2.X + t*t*t*end.X,
                    Y = u*u*u*start.Y + 3*u*u*t*p1.Y + 3*u*t*t*p2.Y + t*t*t*end.Y, Z = start.Z + (end.Z - start.Z) * t };
                var dx = 3*u*u*(p1.X-start.X) + 6*u*t*(p2.X-p1.X) + 3*t*t*(end.X-p2.X);
                var dy = 3*u*u*(p1.Y-start.Y) + 6*u*t*(p2.Y-p1.Y) + 3*t*t*(end.Y-p2.Y);
                bus.Turn.Enqueue((p, Math.Atan2(dy, dx)));
            }
            bus.TurnExitDistance = trim;
        }
        if (bus.Turn.TryPeek(out var target))
        {
            var d = bus.State.Position.Distance2D(target.Position);
            if (d <= distance) return target;
            var t = distance / d; var p = bus.State.Position;
            return (p with { X = p.X + (target.Position.X - p.X) * t, Y = p.Y + (target.Position.Y - p.Y) * t, Z = p.Z + (target.Position.Z - p.Z) * t }, target.Heading);
        }
        return (edge.At(Math.Min(edge.Length - trim, bus.Distance + distance)), edge.Heading);
    }

    private static void CommitBusStep(SimulatedBus bus, double travelled, bool wasTurning)
    {
        if (bus.Turn.TryPeek(out var target))
        {
            if (bus.State.Position.Distance2D(target.Position) > .001) return;
            bus.Turn.Dequeue();
            if (bus.Turn.Count > 0) return;
            bus.EdgeIndex = (bus.EdgeIndex + 1) % bus.Route.Length;
            bus.Distance = bus.TurnExitDistance; bus.ServedStops.Clear();
        }
        else bus.Distance += travelled;
    }

    private async Task<bool> BoardWaitingPassengersAsync(SimulatedBus bus, Dictionary<string, PlayerState> changed, CancellationToken token)
    {
        var edges = new[] { bus.Edge, bus.Route[(bus.EdgeIndex + bus.Route.Length - 1) % bus.Route.Length], bus.Route[(bus.EdgeIndex + 1) % bus.Route.Length] }.DistinctBy(e => e.Id);
        foreach (var edge in edges)
        foreach (var stop in _transitNetwork!.EdgeStops.GetValueOrDefault(edge.Id, []))
        {
            var along = (bus.State.Position.X - stop.Position.X) * edge.Dx + (bus.State.Position.Y - stop.Position.Y) * edge.Dy;
            var aligned = Math.Cos(bus.State.HeadingRadians) * edge.Dx + Math.Sin(bus.State.HeadingRadians) * edge.Dy > .85;
            if (bus.ServedStops.Contains(stop.Id) || Math.Abs(along) > .5 || !aligned || bus.State.Position.Distance2D(stop.Position) > edge.Width / 2 + 2) continue;
            bus.ServedStops.Add(stop.Id);
            var waiting = _players.Values.Where(p => p.WaitingAtBusStopId == stop.Id && p.LocationId == "outdoor" &&
                p.RidingBusId is null && p.Abduction is null && !IsGasAsleep(p.Id) && p.Position.Distance2D(stop.Position) <= 3 && RightSideDistance(p.Position, edge) > 0).ToArray();
            if (waiting.Length == 0) continue;
            bus.Dwell = 3; bus.State = bus.State with { Status = "boarding", SpeedMetersPerSecond = 0 };
            foreach (var player in waiting)
            {
                _busReturnPositions[player.Id] = player.Position;
                var updated = player with { Position = bus.State.Position, WaitingAtBusStopId = null, RidingBusId = bus.State.Id, SpeedMetersPerSecond = 0, TravelMode = TravelMode.Walk, Version = player.Version + 1 };
                await SavePlayerAsync(updated, token); changed[player.Id] = _players[player.Id];
            }
            return true;
        }
        return false;
    }

    private async Task HitBusPeopleAsync(SimulatedBus bus, GeometryPoint[] footprint, PlayerState[] people, ActorState[] outdoorActors, Dictionary<string, PlayerState> players,
        Dictionary<string, ActorState> actors, HashSet<string> removed, List<CombatEvent> combat, CancellationToken token)
    {
        WorldPosition Fling(WorldPosition p)
        {
            var dx = Math.Cos(bus.State.HeadingRadians); var dy = Math.Sin(bus.State.HeadingRadians);
            for (var forward = 9; forward >= 0; forward--)
            for (var side = 5; side <= 10; side++)
            {
                var destination = p with { X = p.X + dx * forward + dy * side, Y = p.Y + dy * forward - dx * side };
                if (!Navigation.IsBlocked(destination.X, destination.Y) && !TransitGeometry.Contains(footprint, destination)) return destination;
            }
            return p;
        }
        foreach (var original in people.Where(p => p.LocationId == "outdoor" && p.RidingBusId is null && p.Abduction is null && p.TravelMode != TravelMode.Ufo && p.Position.Distance2D(bus.State.Position) < 7).ToArray())
        {
            var p = _players.GetValueOrDefault(original.Id) ?? original;
            if (!TransitGeometry.Contains(footprint, p.Position) || !bus.HitPeople.Add(p.Id)) continue;
            var health = p.GodMode ? Math.Max(1, p.HealthHearts - 5) : Math.Max(0, p.HealthHearts - 5);
            var updated = p with { HealthHearts = health, Position = Fling(p.Position), WaitingAtBusStopId = null, Version = p.Version + 1 };
            if (health <= 0) updated = await DieAndResetPlayerAsync(updated, token);
            await SavePlayerAsync(updated, token); players[p.Id] = _players[p.Id];
            combat.Add(new(bus.State.Id, p.Id, "busCollision", bus.State.Position, p.Position, true, 5, health <= 0, "Hit by a bus for 5 hearts.", health, updated.Position));
        }
        foreach (var original in outdoorActors.Where(a => _actors.ContainsKey(a.Id) && a.LocationId == "outdoor" && a.Abduction is null && a.Subtype != "ufo" && a.Position.Distance2D(bus.State.Position) < 7).ToArray())
        {
            if (!_actors.TryGetValue(original.Id, out var a)) continue;
            if (!TransitGeometry.Contains(footprint, a.Position) || !bus.HitPeople.Add(a.Id)) continue;
            var health = Math.Max(0, a.HealthHearts - 5); var destination = Fling(a.Position);
            if (health <= 0) { _actors.TryRemove(a.Id, out _); removed.Add(a.Id); }
            else { var updated = a with { HealthHearts = health, Position = destination, Version = a.Version + 1 }; _actors[a.Id] = updated; actors[a.Id] = updated; }
            _actorRoutes.TryRemove(a.Id, out _);
            combat.Add(new(bus.State.Id, a.Id, "busCollision", bus.State.Position, a.Position, true, 5, health <= 0, "Hit by a bus for 5 hearts.", health, destination));
        }
    }

    private async Task<CanonicalEntity> DamageBusObstacleAsync(CanonicalEntity obstacle, double damage, CancellationToken token)
    {
        var current = _baseEntities.GetValueOrDefault(obstacle.Id) ?? _realityEntities.GetValueOrDefault(obstacle.Id) ?? obstacle;
        if (current.Kind == EntityKind.Building && (!Configuration.BuildingDestruction || _publicBaseClaims.ContainsKey(current.Id))) return current;
        var properties = new Dictionary<string, string>(current.Properties);
        if (current.Kind == EntityKind.Building)
        {
            var health = Math.Max(0, TransitGeometry.Number(current, "healthHearts", 5000) - damage);
            properties["healthHearts"] = health.ToString("F2", CultureInfo.InvariantCulture);
            properties["maximumHealthHearts"] = "5000";
            if (health <= 0) { properties["state"] = "rubble"; properties["destroyedUntilUtc"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"); }
        }
        else properties["damage"] = (TransitGeometry.Number(current, "damage", 0) + damage).ToString("F2", CultureInfo.InvariantCulture);
        var updated = current with { Properties = properties, Version = current.Version + 1 };
        if (_baseEntities.ContainsKey(current.Id)) _baseEntities[current.Id] = updated; else _realityEntities[current.Id] = updated;
        await _store.SaveEntityAsync(Configuration.Id, updated, token);
        return updated;
    }

    public async Task PrepareBusAreasAsync(CancellationToken token)
    {
        var snapshot = GetTransitSnapshot();
        foreach (var bus in snapshot.Buses.Where(b => b.HealthHearts > 0 && b.Status != "paused"))
        {
            if (!_players.Values.Any(p => p.LocationId == "outdoor" && (p.RidingBusId == bus.Id || WaitingForRoute(p,bus.RouteId) || p.Position.Distance2D(bus.Position) < 700))) continue;
            var x = bus.Position.X + Math.Cos(bus.HeadingRadians) * 40;
            var y = bus.Position.Y + Math.Sin(bus.HeadingRadians) * 40;
            if (IsAreaLoaded(x, y)) continue;
            if (RegionId.FromGeo(new LocalTangentProjection(Configuration.Area.Region).Unproject(new(Configuration.Area.Region, x, y))) != Configuration.Area.Region) continue;
            await EnsureAreaLoadedAsync(x, y, token);
        }
    }

    private bool WaitingForRoute(PlayerState player,string routeId) => player.WaitingAtBusStopId is { } stop &&
        _transitSnapshot.Routes.Any(r=>r.Id==routeId&&r.StopIds.Contains(stop));
}
