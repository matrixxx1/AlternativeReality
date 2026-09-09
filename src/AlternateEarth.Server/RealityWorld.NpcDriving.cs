using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private sealed class NpcTrip(string carId, ActorState driver, CanonicalEntity origin, CanonicalEntity destination, Queue<WorldPosition> route)
    {
        public string CarId = carId;
        public ActorState Driver = driver;
        public CanonicalEntity Origin = origin, Destination = destination;
        public Queue<WorldPosition> Route = route;
        public string Phase = "walking to car";
        public DateTimeOffset ResumeAt;
        public HashSet<string> Hit = [];
        public Queue<WorldPosition> WalkingRoute = new();
    }
    private readonly ConcurrentDictionary<string,NpcTrip> _npcTrips = new();
    private readonly HashSet<string> _reservedParking = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _vehicleEncounters = new(), _fleeingVehicles = new();
    private DateTimeOffset _nextNpcTrip;
    private readonly ConcurrentQueue<CanonicalEntity> _changedNpcCars = new();
    public IReadOnlyList<CanonicalEntity> TakeNpcCarUpdates() { var list = new List<CanonicalEntity>(); while (_changedNpcCars.TryDequeue(out var car)) list.Add(car); return list; }
    private bool IsDrivingNpc(string id) => _npcTrips.Values.Any(t => t.Driver.Id == id);
    private WorldNavigation? _npcDriveNavigation;
    private TransitObstacleIndex? _npcDriveObstacles;
    private CanonicalEntity[] _npcPaving = [];
    private bool DriveablePoint(WorldPosition point) => Navigation.TerrainAt(point.X,point.Y)==TerrainType.Road || _npcPaving.Any(e=>TransitGeometry.Overlaps(TransitGeometry.Footprint(point,0,.1,.1),e.Geometry));
    private static double DistanceToEntityLine(WorldPosition point, CanonicalEntity e)
    {
        var best = double.MaxValue;
        for(var i=1;i<e.Geometry.Count;i++) { var a=e.Geometry[i-1];var b=e.Geometry[i];var dx=b.X-a.X;var dy=b.Y-a.Y;var t=Math.Clamp(((point.X-a.X)*dx+(point.Y-a.Y)*dy)/(dx*dx+dy*dy+.00001),0,1);best=Math.Min(best,Math.Sqrt(Math.Pow(point.X-a.X-t*dx,2)+Math.Pow(point.Y-a.Y-t*dy,2))); }
        return best;
    }
    private Queue<WorldPosition>? PlanNpcDrive(CanonicalEntity from, CanonicalEntity to)
    {
        if (_transitNetwork is null) return null;
        static double Along(RoadEdge e, WorldPosition p) => Math.Clamp((p.X-e.Start.X)*e.Dx+(p.Y-e.Start.Y)*e.Dy,0,e.Length);
        RoadEdge[] Candidates(CanonicalEntity p) => _transitNetwork.Edges.Values
            .Where(e => !p.Properties.ContainsKey("roadId") || e.Road.Id == p.Properties["roadId"])
            .OrderBy(e => e.At(Along(e,p.Position)).Distance2D(p.Position)).Take(4).ToArray();
        foreach(var start in Candidates(from)) foreach(var end in Candidates(to))
        {
            var startAlong=Along(start,from.Position);var endAlong=Along(end,to.Position);
            if(start.Id==end.Id&&endAlong<startAlong)continue;
            var queue = new Queue<RoadEdge>();queue.Enqueue(start);var parents = new Dictionary<string,RoadEdge?> { [start.Id]=null };RoadEdge? found=null;
            while(queue.Count>0 && parents.Count<2000)
            {
                var edge=queue.Dequeue();if(edge.Id==end.Id){found=edge;break;}
                foreach(var next in _transitNetwork.Outgoing.GetValueOrDefault(edge.To,[]))
                    if(!parents.ContainsKey(next.Id)&&_transitNetwork.AllowsTurn(edge,next)){parents[next.Id]=edge;queue.Enqueue(next);}
            }
            if(found is null)continue;
            var edges=new List<RoadEdge>();for(var edge=found;edge is not null;edge=parents[edge.Id])edges.Add(edge);edges.Reverse();
            var points=new List<WorldPosition>{from.Position,start.At(startAlong)};
            foreach(var edge in edges) { if(edge.Id!=start.Id)points.Add(edge.At(0));points.Add(edge.At(edge.Id==end.Id?endAlong:edge.Length)); }
            points.Add(to.Position);
            var path=new Queue<WorldPosition>();var previous=from.Position;var valid=true;
            foreach(var next in points.Skip(1))
            {
                var distance=previous.Distance2D(next);var steps=Math.Max(1,(int)Math.Ceiling(distance/.5));
                for(var i=1;i<=steps;i++)
                {var point=previous with{X=previous.X+(next.X-previous.X)*i/steps,Y=previous.Y+(next.Y-previous.Y)*i/steps};if(!DriveablePoint(point)){valid=false;break;}path.Enqueue(point);}
                if(!valid)break;previous=next;
            }
            if(valid)return path;
        }
        return null;
    }
    private async Task AdvanceNpcDrivingAsync(double seconds, CancellationToken token)
    {
        if(!await _transitLock.WaitAsync(0,token))return;
        try
        {
            var now=_probulatorClock.GetUtcNow();var players=_players.Values.Where(p=>p.LocationId=="outdoor").ToArray();
            bool Nearby(WorldPosition p)=>players.Any(a=>a.Position.Distance2D(p)<=250);
            if(players.Length==0)return;
            RefreshTransitNetwork();
            if (_npcDriveNavigation != _navigation)
            { _npcDriveNavigation = _navigation; _npcPaving=_baseEntities.Values.Where(e=>e.Properties.GetValueOrDefault("subtype")=="driveway"||e.Properties.GetValueOrDefault("amenity")=="parking").ToArray(); _npcDriveObstacles = new(_baseEntities.Values.Where(e => e.Kind is EntityKind.Building or EntityKind.Water or EntityKind.Fence or EntityKind.Tree)); }
            if(_npcTrips.Count<3 && now>=_nextNpcTrip)
            {
                _nextNpcTrip=now.AddSeconds(30);
                var parking=_baseEntities.Values.Where(e=>(e.Properties.GetValueOrDefault("subtype")=="driveway"||e.Properties.GetValueOrDefault("amenity")=="parking")&&!_reservedParking.Contains(e.Id)).OrderBy(e=>e.Id).ToArray();
                foreach(var origin in parking.Where(p=>Nearby(p.Position)).Take(8))
                {
                    var driver=_actors.Values.FirstOrDefault(a=>a.Kind==EntityKind.Npc&&!a.IsQuestGiver&&!a.IsMerchant&&a.EventName is null&&!IsDrivingNpc(a.Id)&&a.Position.Distance2D(origin.Position)<35);
                    if(driver is null)continue;
                    var destination=parking.Where(p=>p.Id!=origin.Id&&p.Position.Distance2D(origin.Position)>25&&p.Position.Distance2D(origin.Position)<1000).OrderBy(_=>Random.Shared.Next()).FirstOrDefault();
                    if(destination is null)continue;var route=PlanNpcDrive(origin,destination);if(route is null)continue;
                    var door=_baseEntities.Values.FirstOrDefault(e=>e.Kind==EntityKind.Door&&e.Properties.GetValueOrDefault("buildingId")==origin.Properties.GetValueOrDefault("buildingId"));
                    if(door is not null){driver=driver with{Position=door.Position,Version=driver.Version+1};_actors[driver.Id]=driver;}
                    var id="npc-car:"+driver.Id;_npcTrips[id]=new(id,driver,origin,destination,route);_reservedParking.Add(origin.Id);_reservedParking.Add(destination.Id);
                    var car=new CanonicalEntity(id,EntityKind.Vehicle,origin.Position,[],new Dictionary<string,string>{{"name","Commuter car"},{"healthHearts","100"},{"maximumHealthHearts","100"},{"occupied","false"},{"heading","0"}});
                    _baseEntities[id]=car;_changedNpcCars.Enqueue(car);break;
                }
            }
            foreach(var trip in _npcTrips.Values.ToArray())
            {
                if(!_baseEntities.TryGetValue(trip.CarId,out var car)||!Nearby(trip.Phase=="walking to car"?trip.Driver.Position:car.Position))continue;
                if(double.TryParse(car.Properties.GetValueOrDefault("healthHearts"),out var health)&&health<=0)continue;
                if(trip.ResumeAt>now)continue;
                if(trip.Phase=="parked") {var route=PlanNpcDrive(trip.Destination,trip.Origin);if(route is null){trip.ResumeAt=now.AddSeconds(30);continue;} (trip.Origin,trip.Destination)=(trip.Destination,trip.Origin);trip.Route=route;trip.Phase="walking to car";trip.Hit.Clear();_actors[trip.Driver.Id]=trip.Driver;_inversionActorUpdates.Enqueue(trip.Driver); }
                if(trip.Phase is "walking to car" or "walking inside")
                {
                    var door=_baseEntities.Values.FirstOrDefault(e=>e.Kind==EntityKind.Door&&e.Properties.GetValueOrDefault("buildingId")==trip.Destination.Properties.GetValueOrDefault("buildingId"));
                    var goal=trip.Phase=="walking to car"?car.Position:door?.Position??trip.Destination.Position;
                    var actor=_actors.GetValueOrDefault(trip.Driver.Id,trip.Driver);var distance=actor.Position.Distance2D(goal);
                    if(distance<1.5)
                    {
                        if(trip.Phase=="walking inside") { _actors.TryRemove(actor.Id,out _);_inversionRemovals.Enqueue(actor.Id);trip.Driver=actor;trip.Phase="parked";trip.ResumeAt=now.AddSeconds(60);trip.WalkingRoute.Clear();continue; }
                        trip.WalkingRoute.Clear();trip.Phase="driving";_actors.TryRemove(actor.Id,out _);_inversionRemovals.Enqueue(actor.Id);car=car with{Properties=new Dictionary<string,string>(car.Properties){["occupied"]="true"},Version=car.Version+1};_baseEntities[car.Id]=car;_changedNpcCars.Enqueue(car);
                    }
                    else {if(trip.WalkingRoute.Count==0){var path=Navigation.FindPath(actor.Position,goal.X,goal.Y,_=>1.4,cancellationToken:token);if(!path.Success){trip.ResumeAt=now.AddSeconds(5);continue;}trip.WalkingRoute=new(path.Waypoints);}var waypoint=trip.WalkingRoute.Peek();var waypointDistance=actor.Position.Distance2D(waypoint);if(waypointDistance<.2){trip.WalkingRoute.Dequeue();continue;}var step=Math.Min(waypointDistance,seconds*1.4);var p=actor.Position with{X=actor.Position.X+(waypoint.X-actor.Position.X)/waypointDistance*step,Y=actor.Position.Y+(waypoint.Y-actor.Position.Y)/waypointDistance*step};if(Navigation.CanTraverse(actor.Position,p)){trip.Driver=actor with{Position=p,IsMoving=true,Version=actor.Version+1};_actors[actor.Id]=trip.Driver;_inversionActorUpdates.Enqueue(trip.Driver);} }
                    continue;
                }
                var budget=seconds*(_fleeingVehicles.GetValueOrDefault(car.Id)>now?10:5);
                while(budget>0&&trip.Route.TryPeek(out var next))
                {
                    var d=car.Position.Distance2D(next);if(d>budget)break;
                    var heading=Math.Atan2(next.Y-car.Position.Y,next.X-car.Position.X);
                    var footprint=TransitGeometry.Footprint(next,heading,4.4,1.8);
                    if(HaneyBlocks(footprint) || _npcDriveObstacles!.Hit(footprint) is not null || !DriveablePoint(next))break;
                    if(_buses.Values.Any(b=>TransitGeometry.Overlaps(footprint,TransitGeometry.Footprint(b.State.Position,b.State.HeadingRadians))) || _npcTrips.Values.Where(t=>t.CarId!=car.Id).Any(t=>_baseEntities.TryGetValue(t.CarId,out var other)&&other.Position.Distance2D(next)<5))break;
                    trip.Route.Dequeue();budget-=Math.Max(.01,d);
                    car=car with{Position=next,Properties=new Dictionary<string,string>(car.Properties){["heading"]=heading.ToString(System.Globalization.CultureInfo.InvariantCulture)},Version=car.Version+1};
                    foreach(var p in players.Where(p=>p.TravelMode!=TravelMode.Ufo&&TransitGeometry.Contains(footprint,p.Position)&&trip.Hit.Add(p.Id)))
                    {var fling=Navigation.FindNearestWalkable(p.Position with{X=p.Position.X+Math.Cos(heading)*6,Y=p.Position.Y+Math.Sin(heading)*6});await SavePlayerAsync(p with{Position=fling,Version=p.Version+1},token);await EventHurtPlayerAsync(_players.GetValueOrDefault(p.Id,p),5,car.Id,next,"car collision",token);}
                    foreach(var a in _actors.Values.Where(a=>a.LocationId=="outdoor"&&TransitGeometry.Contains(footprint,a.Position)&&trip.Hit.Add(a.Id)).ToArray())
                    {var fling=Navigation.FindNearestWalkable(a.Position with{X=a.Position.X+Math.Cos(heading)*6,Y=a.Position.Y+Math.Sin(heading)*6});if(a.HealthHearts<=5)_actors.TryRemove(a.Id,out _);else _actors[a.Id]=a with{Position=fling,HealthHearts=a.HealthHearts-5,Version=a.Version+1};}
                }
                _baseEntities[car.Id]=car;_changedNpcCars.Enqueue(car);
                if(trip.Route.Count==0){trip.Phase="walking inside";trip.Driver=trip.Driver with{Position=car.Position,IsMoving=false,Version=trip.Driver.Version+1};_actors[trip.Driver.Id]=trip.Driver;_inversionActorUpdates.Enqueue(trip.Driver);car=car with{Properties=new Dictionary<string,string>(car.Properties){["occupied"]="false"},Version=car.Version+1};_baseEntities[car.Id]=car;_changedNpcCars.Enqueue(car);}
            }
        }
        finally{_transitLock.Release();}
    }
    private void ReactVehicleOccupants(string playerId,string vehicleId,WorldPosition position)
    {
        if (_baseEntities.TryGetValue(vehicleId,out var parked) && parked.Properties.GetValueOrDefault("occupied") != "true") return;
        var now=_probulatorClock.GetUtcNow();var key=vehicleId+":"+playerId;
        if(_vehicleEncounters.TryGetValue(key,out var previous)&&now-previous<TimeSpan.FromSeconds(30)){_vehicleEncounters[key]=now;return;}
        _vehicleEncounters[key]=now;var reaction=Random.Shared.Next(3);
        if(reaction==0)return;
        if(reaction==1){_fleeingVehicles[vehicleId]=now.AddSeconds(20);return;}
        var id="angry-occupant:"+vehicleId;
        var occupant=_npcTrips.TryGetValue(vehicleId,out var trip)?trip.Driver:new ActorState(id,EntityKind.Npc,"driver","Furious bus occupant",position,EquippedWeapon:"pistol");
        occupant=occupant with{Position=Navigation.FindNearestWalkable(position with{X=position.X+12,Y=position.Y+12}),EquippedWeapon="pistol",FriendRating=-10,Version=occupant.Version+1};
        _actors[occupant.Id]=occupant;_inversionActorUpdates.Enqueue(occupant);_relationships[(playerId,occupant.Id)]=-10;
        if(trip is not null){trip.ResumeAt=now.AddSeconds(30);trip.Phase="walking to car";}
    }
}
