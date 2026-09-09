using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public async Task<PlayerState> BoardBusAsync(string playerId, string busId, CancellationToken token = default)
    {
        await _transitLock.WaitAsync(token);
        try
        {
            if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
            var bus = _buses.Values.FirstOrDefault(b => b.State.Id == busId) ?? throw new InvalidOperationException("Bus not found.");
            if (player.LocationId != "outdoor" || player.RidingBusId is not null || player.Abduction is not null || IsGasAsleep(playerId) || player.HealthHearts <= 0)
                throw new InvalidOperationException("You cannot board a bus right now.");
            if (bus.State.HealthHearts <= 0) throw new InvalidOperationException("This bus is disabled by damage.");
            if (bus.State.SpeedMetersPerSecond > .1) throw new InvalidOperationException("Wait until the bus stops.");
            var point = TransitGeometry.ClosestPoint(bus.State, player.Position);
            var dx = Math.Cos(bus.State.HeadingRadians); var dy = Math.Sin(bus.State.HeadingRadians);
            var right = (player.Position.X-bus.State.Position.X)*dy-(player.Position.Y-bus.State.Position.Y)*dx;
            if (point.Region != player.Position.Region || point.Distance2D(player.Position) > 3 || right < 1.25 ||
                !Navigation.CanTraverse(player.Position, point, true))
                throw new InvalidOperationException("Move within 3 meters of the stopped bus on its right-hand door side.");
            _busReturnPositions[playerId] = player.Position;
            await SavePlayerAsync(player with { Position = bus.State.Position, RidingBusId = bus.State.Id,
                WaitingAtBusStopId = null, TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Version = player.Version + 1 }, token);
            bus.Dwell = 2; bus.State = bus.State with { Status = "boarding", Version = bus.State.Version + 1 };
            PublishTransit();
            return _players[playerId];
        }
        finally { _transitLock.Release(); }
    }

    private bool SafeServicePosition(SimulatedBus bus, WorldPosition position, PlayerState[] people, ActorState[] actors)
    {
        var footprint = TransitGeometry.Footprint(position,bus.State.HeadingRadians,9.4,2.9);
        if (_transitObstacles!.Hit(footprint) is not null ||
            _buses.Values.Any(other => other != bus && other.State.Position.Region == position.Region &&
                other.State.Position.Distance2D(position) < 15 && TransitGeometry.Overlaps(footprint,TransitGeometry.Footprint(other.State.Position,other.State.HeadingRadians))) ||
            people.Any(p => p.Position.Region == position.Region && p.RidingBusId != bus.State.Id && p.TravelMode != TravelMode.Ufo && TransitGeometry.Contains(footprint,p.Position)) ||
            actors.Any(a => a.Position.Region == position.Region && TransitGeometry.Contains(footprint,a.Position))) return false;
        var dx = Math.Cos(bus.State.HeadingRadians); var dy = Math.Sin(bus.State.HeadingRadians);
        for (var along=-4.7; along<=4.701; along+=.47)
        foreach (var side in new[]{-1.45,0,1.45})
        {
            var x=position.X+dx*along-dy*side; var y=position.Y+dy*along+dx*side;
            if (!_loadedAreas.Values.Any(a=>a.Contains(x,y)) || Navigation.IsBlocked(x,y) ||
                Navigation.TerrainAt(x,y) is TerrainType.DeepWater or TerrainType.ShallowWater) return false;
        }
        return true;
    }

    private bool OffTrafficLanes(SimulatedBus bus, WorldPosition position)
    {
        foreach (var edge in _transitNetwork!.Edges.Values)
        {
            if (edge.Road.Position.Region != position.Region) continue;
            // Road rectangles, not just lane center points, must clear the parked bus.
            var center = position with { X=(edge.Start.X+edge.End.X)/2, Y=(edge.Start.Y+edge.End.Y)/2 };
            if (center.Distance2D(position)>edge.Length/2+10) continue;
            if (TransitGeometry.Overlaps(TransitGeometry.Footprint(position,bus.State.HeadingRadians,9.4,2.9),
                TransitGeometry.Footprint(center,edge.Heading,edge.Length+.6,edge.Width+.6))) return false;
        }
        return true;
    }

    private void MoveBusIntoServicePosition(SimulatedBus bus, bool resume, double dt, PlayerState[] people, ActorState[] actors)
    {
        bus.ServiceOrigin ??= bus.State.Position;
        var origin=bus.ServiceOrigin.Value;
        if (!resume && bus.ServiceTarget is null)
        {
            bus.ServiceRetry -= dt;
            if (bus.ServiceRetry<=0)
            {
                bus.ServiceRetry=2;
                var dx=Math.Cos(bus.State.HeadingRadians); var dy=Math.Sin(bus.State.HeadingRadians);
                var minimum=Math.Max(4,bus.Edge.Width/2+2-bus.Edge.LaneOffset);
                foreach(var along in new[]{0d,5,10,-5,15})
                {
                    for(var side=minimum;side<=minimum+8;side+=2)
                    {
                        var candidate=origin with {X=origin.X+dy*side+dx*along,Y=origin.Y-dx*side+dy*along};
                        if(!OffTrafficLanes(bus,candidate))continue;
                        var distance=origin.Distance2D(candidate);var safe=true;
                        for(var d=.25;d<distance+.25;d+=.25)
                        {
                            var f=Math.Min(1,d/distance);
                            var p=origin with {X=origin.X+(candidate.X-origin.X)*f,Y=origin.Y+(candidate.Y-origin.Y)*f};
                            if(!SafeServicePosition(bus,p,people,actors)){safe=false;break;}
                        }
                        if(safe){bus.ServiceTarget=candidate;break;}
                    }
                    if(bus.ServiceTarget is not null)break;
                }
            }
        }
        var target=resume?origin:bus.ServiceTarget;
        if(target is null){bus.State=bus.State with {Status="waiting for safe pull-over",SpeedMetersPerSecond=0,Version=bus.State.Version+1};return;}
        var remaining=bus.State.Position.Distance2D(target.Value);
        var budget=Math.Min(remaining,dt*1.2);
        while(budget>.001)
        {
            var step=Math.Min(.2,budget);var p=bus.State.Position;
            remaining=p.Distance2D(target.Value);var f=Math.Min(1,step/remaining);
            var next=p with {X=p.X+(target.Value.X-p.X)*f,Y=p.Y+(target.Value.Y-p.Y)*f};
            if(!SafeServicePosition(bus,next,people,actors))
            {bus.State=bus.State with {Status=resume?"waiting to rejoin":"waiting for safe pull-over",SpeedMetersPerSecond=0,Version=bus.State.Version+1};return;}
            next=next with {Z=Navigation.ElevationAt(next.X,next.Y)};
            bus.State=bus.State with {Position=next,Status=resume?"rejoining route":"pulling over",SpeedMetersPerSecond=1.2,Version=bus.State.Version+1};
            budget-=step;
        }
        if(bus.State.Position.Distance2D(target.Value)<.001)
        {
            var status=resume?"boarding":bus.State.HealthHearts<=0?"disabled":"out of service";
            if(bus.State.Status!=status || bus.State.SpeedMetersPerSecond!=0)
                bus.State=bus.State with {Status=status,SpeedMetersPerSecond=0,Version=bus.State.Version+1};
            if(resume){bus.ServiceOrigin=null;bus.ServiceTarget=null;bus.ServiceRetry=0;}
        }
    }
}
