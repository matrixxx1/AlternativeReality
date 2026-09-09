using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private const string HaneyName = "Eustace Charleston Haney";
    private sealed class HaneyVisit(CanonicalEntity truck, CanonicalEntity parking, Queue<WorldPosition> route)
    {
        public CanonicalEntity Truck = truck;
        public CanonicalEntity Parking = parking;
        public Queue<WorldPosition> Route = route;
        public string MerchantId = truck.Id + ":merchant";
        public bool Parked;
        public DateTimeOffset NextSpeech;
        public double Budget;
    }
    private HaneyVisit? _haney;
    private bool HaneyBlocks(IReadOnlyList<GeometryPoint> footprint) => _haney is { } visit && TransitGeometry.Overlaps(footprint, TransitGeometry.ObjectFootprint(visit.Truck));
    private bool HaneyTerrainBlocked(IReadOnlyList<GeometryPoint> footprint) => footprint.Any(p => Navigation.IsBlocked(p.X, p.Y) || IsWater(Navigation.TerrainAt(p.X, p.Y)));
    private DateTimeOffset _nextHaneyVisit;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _removedHaneyTrucks = new();
    public IReadOnlyList<string> TakeHaneyTruckRemovals() { var ids = new List<string>(); while (_removedHaneyTrucks.TryDequeue(out var id)) ids.Add(id); return ids; }

    private async Task AdvanceHaneyAsync(double seconds, CancellationToken token)
    {
        if (!await _transitLock.WaitAsync(0, token)) return;
        try
        {
            var now = _probulatorClock.GetUtcNow();
            var people = _players.Values.Where(p => p.LocationId == "outdoor" && !p.IsTestCharacter).ToArray();
            if (_haney is { } visit && !people.Any(p => p.Position.Distance2D(visit.Truck.Position) <= 250))
            {
                _baseEntities.TryRemove(visit.Truck.Id, out _); _actors.TryRemove(visit.MerchantId, out _);
                _removedHaneyTrucks.Enqueue(visit.Truck.Id); _inversionRemovals.Enqueue(visit.MerchantId);
                _reservedParking.Remove(visit.Parking.Id); _haney = null; _nextHaneyVisit = now.AddSeconds(Random.Shared.Next(180, 421));
            }
            if (people.Length == 0) return;
            if (_nextHaneyVisit == default) { _nextHaneyVisit = now.AddSeconds(Random.Shared.Next(60, 181)); return; }
            if (_haney is null && now >= _nextHaneyVisit)
            {
                _nextHaneyVisit = now.AddSeconds(60);
                RefreshTransitNetwork();
                if (_transitNetwork is null || _npcDriveObstacles is null) return;
                foreach (var parking in _npcPaving.Where(p => !_reservedParking.Contains(p.Id) && people.Any(a => a.Position.Distance2D(p.Position) < 80)).OrderBy(_ => Random.Shared.Next()).Take(12))
                {
                    var parkedFootprint = TransitGeometry.Footprint(parking.Position, 0, 4.8, 1.9);
                    if (HaneyTerrainBlocked(parkedFootprint) || _npcDriveObstacles.Hit(parkedFootprint) is not null || Navigation.TerrainAt(parking.Position.X, parking.Position.Y) == TerrainType.Road) continue;
                    foreach (var edge in _transitNetwork.Edges.Values.Where(e => e.At(0).Distance2D(parking.Position) is > 40 and < 180).OrderBy(_ => Random.Shared.Next()).Take(30))
                    {
                        var start = edge.At(Math.Min(5, edge.Length / 2));
                        if (_npcDriveObstacles.Hit(TransitGeometry.Footprint(start, Math.Atan2(edge.Dy, edge.Dx), 4.8, 1.9)) is not null) continue;
                        var origin = parking with { Position = start, Properties = new Dictionary<string,string> { ["roadId"] = edge.Road.Id } };
                        var route = PlanNpcDrive(origin, parking); if (route is null) continue;
                        var id = "haney-truck:" + Guid.NewGuid().ToString("N");
                        var truck = new CanonicalEntity(id, EntityKind.Vehicle, start, [], new Dictionary<string,string> { ["name"] = HaneyName + "'s old pickup", ["subtype"] = "haneyPickup", ["lengthMeters"] = "4.8", ["widthMeters"] = "1.9", ["occupied"] = "true", ["healthHearts"] = "100", ["maximumHealthHearts"] = "100", ["rotationDegrees"] = "0" });
                        _haney = new(truck, parking, route); _reservedParking.Add(parking.Id); _baseEntities[id] = truck; _changedNpcCars.Enqueue(truck); break;
                    }
                    if (_haney is not null) break;
                }
            }
            if (_haney is not { } current) return;
            if (!current.Parked)
            {
                current.Budget = Math.Min(2, current.Budget + Math.Clamp(seconds, 0, 1) * 4);
                while (current.Route.TryPeek(out var point))
                {
                    var distance = current.Truck.Position.Distance2D(point); if (distance > current.Budget) break;
                    var heading = Math.Atan2(point.Y - current.Truck.Position.Y, point.X - current.Truck.Position.X);
                    var footprint = TransitGeometry.Footprint(point, heading, 4.8, 1.9);
                    if (HaneyTerrainBlocked(footprint) || !DriveablePoint(point) || _npcDriveObstacles!.Hit(footprint) is not null ||
                        _buses.Values.Any(b => TransitGeometry.Overlaps(footprint, TransitGeometry.Footprint(b.State.Position, b.State.HeadingRadians))) ||
                        _baseEntities.Values.Any(v => v.Kind == EntityKind.Vehicle && v.Id != current.Truck.Id && v.Position.Distance2D(point) < 5) ||
                        people.Any(p => TransitGeometry.Contains(footprint, p.Position))) break;
                    current.Route.Dequeue(); current.Budget -= Math.Max(.01, distance);
                    current.Truck = current.Truck with { Position = point, Version = current.Truck.Version + 1, Properties = new Dictionary<string,string>(current.Truck.Properties) { ["rotationDegrees"] = (heading * 180 / Math.PI).ToString(System.Globalization.CultureInfo.InvariantCulture) } };
                }
                if (current.Route.Count == 0)
                {
                    current.Parked = true;
                    current.Truck = current.Truck with { Properties = new Dictionary<string,string>(current.Truck.Properties) { ["occupied"] = "false" }, Version = current.Truck.Version + 1 };
                    var position = Navigation.FindNearestWalkable(current.Truck.Position with { Y = current.Truck.Position.Y + 2 });
                    var merchant = new ActorState(current.MerchantId, EntityKind.Npc, "haney", HaneyName, position, IsMerchant: true, MerchantCategory: "haney", EquippedWeapon: "fist", HealthHearts: 30, MaximumHealthHearts: 30);
                    _actors[merchant.Id] = merchant; _inversionActorUpdates.Enqueue(merchant);
                }
                _baseEntities[current.Truck.Id] = current.Truck; _changedNpcCars.Enqueue(current.Truck);
            }
            if (current.Parked && now >= current.NextSpeech && _actors.TryGetValue(current.MerchantId, out var seller))
            {
                current.NextSpeech = now.AddSeconds(12);
                if (people.Any(p => p.Position.Distance2D(seller.Position) < 80)) EventSay(seller.Id, seller.Name, "I've got stuff for sale! Finest prices, lowest quality! Come see Eustace Charleston Haney!");
            }
        }
        finally { _transitLock.Release(); }
    }
}
