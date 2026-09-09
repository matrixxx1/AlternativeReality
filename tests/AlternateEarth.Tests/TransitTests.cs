using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class TransitNetworkTests
{
    [Fact]
    public void DenseShortStreetsShareSparsePairedStopLocations()
    {
        var roads = Enumerable.Range(0, 20).Select(i => Road($"street:{i:D2}", [new(0, i * 30), new(200, i * 30)], $"{i*2},{i*2+1}", name:$"Street {i}"));
        var network = new RoadTransitNetwork(roads);
        Assert.InRange(network.Stops.Count, 2, 4);
        foreach (var stop in network.Stops)
            Assert.Contains(network.Stops, other => other.Id != stop.Id && other.Direction != stop.Direction && other.Position.Distance2D(stop.Position) < 20);
        var locations = network.Stops.Where(s => s.Direction == "eastbound").ToArray();
        for (var i = 0; i < locations.Length; i++) for (var j = i + 1; j < locations.Length; j++)
            Assert.True(locations[i].Position.Distance2D(locations[j].Position) >= 400);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.7853981633974483)]
    [InlineData(1.5707963267948966)]
    public void BusCollisionChecksTheEntireMovementSegmentIncludingPlayerRadius(double heading)
    {
        var center = new WorldPosition(new(45,-123),0,0);
        var bus = new BusState("bus", "route", "Route", center, heading);
        WorldPosition Point(double along, double side) => center with { X = Math.Cos(heading)*along-Math.Sin(heading)*side, Y = Math.Sin(heading)*along+Math.Cos(heading)*side };
        Assert.True(TransitGeometry.SegmentHitsBus(Point(-20,0),Point(20,0),bus,.35));
        Assert.True(TransitGeometry.SegmentHitsBus(Point(0,2),Point(0,1.4),bus,.35));
        Assert.False(TransitGeometry.SegmentHitsBus(Point(-20,3),Point(20,3),bus,.35));
        Assert.False(TransitGeometry.SegmentHitsBus(center,Point(0,3),bus,.35));
    }
    internal static CanonicalEntity Road(string id, GeometryPoint[] points, string nodes, string highway = "secondary", string? oneWay = null, string name = "Main Street") =>
        new(id, EntityKind.Road, new(new(45, -123), points[0].X, points[0].Y), points,
            new Dictionary<string, string> { ["highway"] = highway, ["name"] = name, ["widthMeters"] = "8", ["osmNodeIds"] = nodes, ["oneway"] = oneWay ?? "no" });

    [Fact]
    public void StopsCarryMileSpacingAcrossWaySplitsAndStayOnRight()
    {
        var network = new RoadTransitNetwork(new[] {
            Road("a", [new(0,0),new(1000,0)], "1,2"), Road("b", [new(1000,0),new(4000,0)], "2,3") });
        var east = network.Stops.Where(s => s.Direction == "eastbound").OrderBy(s => s.Position.X).ToArray();
        Assert.True(east.Length >= 3);
        Assert.Equal(1609.344, east[1].Position.X - east[0].Position.X, 3);
        Assert.Equal(1609.344, east[2].Position.X - east[1].Position.X, 3);
        Assert.All(east, s => Assert.True(s.Position.Y < 0));
        Assert.All(network.Stops.Where(s => s.Direction == "westbound"), s => Assert.True(s.Position.Y > 0));
        Assert.All(network.Edges.Values, e => Assert.Equal(e.Dx > 0 ? -2 : 2, e.At(100).Y));
    }

    [Theory]
    [InlineData("motorway", "freeway")]
    [InlineData("trunk", "highway")]
    [InlineData("primary", "mainRoad")]
    [InlineData("residential", "surfaceStreet")]
    [InlineData("footway", "path")]
    public void ClassifiesRoads(string highway, string classification) => Assert.Equal(classification, RoadClassification.Classify(new Dictionary<string, string> { ["highway"] = highway }));

    [Fact]
    public void OneWayAndGradeSeparatedCrossingsDoNotCreateIllegalConnections()
    {
        var roads = new[] { Road("east", [new(-100,0),new(0,0),new(100,0)], "1,2,3", oneWay:"yes"),
            Road("bridge", [new(0,-100),new(0,0),new(0,100)], "4,5,6") };
        var network = new RoadTransitNetwork(roads);
        Assert.DoesNotContain(network.Edges.Values, e => e.Road.Id == "east" && e.Dx < 0);
        var next = network.Next(network.Edges["east:0:+"], new Dictionary<string, int>());
        Assert.Equal("east:1:+", next!.Id);
        Assert.DoesNotContain(network.Stops,s => s.EdgeId.StartsWith("east:")); // no legal return service
    }

    [Fact]
    public void ClosedRoutesUseSharedJunctionsAndRespectReverseOneWay()
    {
        var network = new RoadTransitNetwork(new[] { Road("a", [new(0,0),new(100,0)], "1,2", oneWay:"yes"),
            Road("b", [new(100,100),new(100,0)], "3,2", oneWay:"-1"),
            Road("c", [new(100,100),new(0,0)], "3,1", oneWay:"yes") });
        var route = Assert.Single(network.Routes).Value;
        for (var i=0;i<route.Length;i++) Assert.Equal(route[i].To, route[(i+1)%route.Length].From);
        Assert.Contains(route,e=>e.Id=="b:0:-");
        Assert.Equal(3,route.Length);
    }

    [Fact]
    public void FreewaysAndPrivateRoadsHaveNoPassengerStops()
    {
        var freeway = Road("freeway", [new(0,0),new(4000,0)], "1,2", "motorway");
        var privateRoad = Road("private", [new(0,10),new(4000,10)], "3,4") with { Properties = new Dictionary<string,string>{["highway"]="residential",["access"]="private"} };
        var network = new RoadTransitNetwork(new[]{freeway,privateRoad});
        Assert.Empty(network.Stops);
        Assert.DoesNotContain(network.Edges.Values,e=>e.Road.Id=="private");
    }

    [Fact]
    public void FootprintDetectsCornerAndRotatedVehicleImpacts()
    {
        var p = new WorldPosition(new(45,-123),0,0);
        var bus = TransitGeometry.Footprint(p,Math.PI/4);
        var car = TransitGeometry.Footprint(p with {X=3,Y=3},0,4.5,1.9);
        Assert.True(TransitGeometry.Overlaps(bus,car));
        Assert.False(TransitGeometry.Overlaps(bus,TransitGeometry.Footprint(p with{X=20},0)));
    }

    [Fact]
    public void TurnRestrictionsBlockForbiddenJunctionMovement()
    {
        var incoming=Road("a",[new(-100,0),new(0,0)],"1,2");
        var tags=new Dictionary<string,string>(incoming.Properties){["turnRestrictions"]=System.Text.Json.JsonSerializer.Serialize(new[]{new RoadTurnRestriction("a","b","osm:2","no_left_turn")},SharedJson.Options)};
        var network=new RoadTransitNetwork(new[]{incoming with{Properties=tags},Road("b",[new(0,0),new(0,100)],"2,3"),Road("c",[new(0,0),new(100,0)],"2,4")});
        Assert.False(network.AllowsTurn(network.Edges["a:0:+"],network.Edges["b:0:+"]));
        Assert.True(network.AllowsTurn(network.Edges["a:0:+"],network.Edges["c:0:+"]));
        Assert.All(network.Routes.Values,route=>{for(var i=0;i<route.Length;i++)Assert.True(network.AllowsTurn(route[i],route[(i+1)%route.Length]));});
    }
}

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task GodModeWalkingCannotPassThroughAStationaryBus()
    {
        var (world, player) = await TransitWorld();
        var bus = world.GetTransitSnapshot().Buses.First();
        var side = new WorldPosition(bus.Position.Region,bus.Position.X+Math.Sin(bus.HeadingRadians)*1.7,bus.Position.Y-Math.Cos(bus.HeadingRadians)*1.7);
        player = await world.TeleportAsync(player.Id,new(side.X,side.Y,true));
        Assert.True(player.GodMode);
        for (var i=0;i<10;i++) await world.MoveAsync(player.Id,new(-Math.Sin(bus.HeadingRadians),Math.Cos(bus.HeadingRadians),i+1));
        var final = world.CreateSnapshot().Players.Single(p=>p.Id==player.Id);
        Assert.False(TransitGeometry.Contains(TransitGeometry.Footprint(bus.Position,bus.HeadingRadians,9.65,3.15),final.Position));
    }

    [Fact]
    public async Task AYieldingBusCanBackAlongItsHistoryWithoutOverlappingOrDamagingTraffic()
    {
        var (world, _) = await TransitWorld();
        await RequestTransitService(world);
        for(var i=0;i<6;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var buses=(System.Collections.IDictionary)typeof(RealityWorld).GetField("_buses",flags)!.GetValue(world)!;
        var first=buses.Values.Cast<object>().First();var type=first.GetType();
        object Get(object bus,string name)=>type.GetField(name,flags)!.GetValue(bus)!;
        void Set(object bus,string name,object value)=>type.GetField(name,flags)!.SetValue(bus,value);
        var initial=(BusState)Get(first,"State");
        var second=Activator.CreateInstance(type,flags,null,new[]{"bus:000-yield", "test-yield-route",Get(first,"Route")},null)!;
        var position=initial.Position with{X=initial.Position.X+Math.Cos(initial.HeadingRadians)*9.7,Y=initial.Position.Y+Math.Sin(initial.HeadingRadians)*9.7};
        Set(second,"State",initial with{Id="bus:000-yield",RouteId="test-yield-route",Position=position});
        Set(second,"Dwell",100d);Set(second,"LastObstacle",initial.Id);buses.Add("test-yield-route",second);
        var reversed=false;
        for(var i=0;i<30;i++)
        {
            var tick=await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
            var a=tick.Transit.Buses.Single(b=>b.Id==initial.Id);var b=tick.Transit.Buses.Single(b=>b.Id=="bus:000-yield");
            reversed|=a.Status=="reversing to yield";
            Assert.False(TransitGeometry.Overlaps(TransitGeometry.Footprint(a.Position,a.HeadingRadians),TransitGeometry.Footprint(b.Position,b.HeadingRadians)));
            Assert.Equal(100,a.HealthHearts);Assert.Equal(100,b.HealthHearts);
        }
        Assert.True(reversed);
        Assert.True(((BusState)Get(first,"State")).Position.Distance2D(initial.Position)>1);
    }
    private async Task<(RealityWorld World, PlayerState Player)> TransitWorld(params CanonicalEntity[] extra)
    {
        var config = new RealityConfiguration("transit", "Transit test", 123, new(new(45.5,-122.5),1000));
        var road = TransitNetworkTests.Road("transit-road",[new(-200,0),new(200,0)],"1,2");
        var store = new SqliteRealityStore(Path.Combine(_directory,"transit.db")); await store.InitializeAsync(config);
        var world = new RealityWorld(config,new DeterministicWorldGenerator(new FixedGeographicProvider(new[]{road}.Concat(extra).ToArray())),new FixedWeatherProvider(),store);
        await world.InitializeAsync();
        Assert.Empty(world.GetTransitSnapshot().Buses);
        var player = await world.JoinAsync("rider","Rider");
        await world.SetGodModeAsync(player.Id,true);
        player = await world.TeleportAsync(player.Id,new(-180,-5.5,true));
        await world.AdvanceTransitAsync(TimeSpan.Zero);
        Assert.NotEmpty(world.GetTransitSnapshot().Buses);
        return(world,player);
    }

    private static async Task<PlayerState> RequestTransitService(RealityWorld world)
    {
        var player = await world.JoinAsync("service-rider", "Service rider");
        await world.SetGodModeAsync(player.Id, true);
        var stop = world.GetTransitSnapshot().Stops.Where(s => s.Direction == "eastbound").MaxBy(s => s.Position.X)!;
        await world.TeleportAsync(player.Id, new(stop.Position.X, stop.Position.Y, true));
        return await world.WaitForBusAsync(player.Id, stop.Id);
    }

    [Fact]
    public async Task NearbyServiceIsLimitedAndRunsOnlyWhileRequested()
    {
        var roads = Enumerable.Range(1, 20).Select(i => TransitNetworkTests.Road("extra-" + i,
            [new(-200,i*10),new(200,i*10)], $"{i*2+10},{i*2+11}", name: "Street " + i)).ToArray();
        var (world, player) = await TransitWorld(roads);
        Assert.InRange(world.GetTransitSnapshot().Buses.Count, 1, 2);
        var view = world.CreateTransitView(player.Id);
        Assert.NotEmpty(view.Routes);
        Assert.All(view.Routes, route => Assert.Empty(route.Path));
        Assert.All(world.GetTransitSnapshot().Routes, route => Assert.NotEmpty(route.Path));
        await world.TeleportAsync(player.Id, new(0,-400,true));
        await world.AdvanceTransitAsync(TimeSpan.FromSeconds(1.1));
        var paused = world.GetTransitSnapshot().Buses.ToArray();
        Assert.All(paused, bus => Assert.Contains(bus.Status,new[]{"out of service","pulling over","waiting for safe pull-over"}));
        await world.AdvanceTransitAsync(TimeSpan.FromSeconds(1));
        Assert.All(world.GetTransitSnapshot().Buses, bus => Assert.Contains(bus.Status,new[]{"out of service","pulling over","waiting for safe pull-over"}));
        await world.TeleportAsync(player.Id, new(-180,-5.5,true));
        await world.AdvanceTransitAsync(TimeSpan.FromSeconds(1.1));
        Assert.All(world.GetTransitSnapshot().Buses, bus => Assert.Contains(bus.Status,new[]{"out of service","pulling over","waiting for safe pull-over"}));
        var stop = world.GetTransitSnapshot().Stops.First(s => s.Direction == "eastbound" && s.Position.X < 0);
        await world.TeleportAsync(player.Id, new(stop.Position.X, stop.Position.Y, true));
        await world.WaitForBusAsync(player.Id, stop.Id);
        await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        Assert.Contains(world.GetTransitSnapshot().Buses, bus => bus.Status is not ("out of service" or "pulling over" or "waiting for safe pull-over"));
        var second = await world.JoinAsync("second-waiter", "Second waiter");
        await world.SetGodModeAsync(second.Id, true);
        await world.TeleportAsync(second.Id, new(stop.Position.X, stop.Position.Y, true));
        await world.WaitForBusAsync(second.Id, stop.Id);
        await world.CancelBusWaitAsync(player.Id);
        await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        Assert.Contains(world.GetTransitSnapshot().Buses, bus => bus.Status is not ("out of service" or "pulling over" or "waiting for safe pull-over"));
        await world.CancelBusWaitAsync(second.Id);
        await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        Assert.All(world.GetTransitSnapshot().Buses, bus => Assert.Contains(bus.Status,new[]{"out of service","pulling over","waiting for safe pull-over"}));
        await world.WaitForBusAsync(player.Id, stop.Id);
        await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        Assert.Contains(world.GetTransitSnapshot().Buses, bus => bus.Status is not ("out of service" or "pulling over" or "waiting for safe pull-over"));
        await world.LeaveAsync(player.Id);
        await world.AdvanceTransitAsync(TimeSpan.FromSeconds(1));
        Assert.All(world.GetTransitSnapshot().Buses, bus => Assert.Contains(bus.Status,new[]{"out of service","pulling over","waiting for safe pull-over"}));
    }

    [Fact]
    public async Task WaitBoardRideImmediateExitAndWrongSideValidation()
    {
        var (world,player)=await TransitWorld();
        var stop=world.GetTransitSnapshot().Stops.First(s=>s.Direction=="eastbound"&&s.Position.X<0);
        player=await world.TeleportAsync(player.Id,new(stop.Position.X,stop.Position.Y,true));
        var seated = await world.WaitForBusAsync(player.Id,stop.Id);
        Assert.NotNull(stop.BenchPosition);
        Assert.Equal(stop.BenchPosition.Value, seated.Position);
        Assert.Equal(stop.Id, seated.WaitingAtBusStopId);
        Assert.InRange(seated.Position.Distance2D(stop.Position), .1, 3);
        var moved=await world.MoveAsync(player.Id,new(1,0,1));Assert.False(moved!.Moved);
        for(var i=0;i<60&&world.CreateSnapshot().Players.Single(p=>p.Id==player.Id).RidingBusId is null;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var rider=world.CreateSnapshot().Players.Single(p=>p.Id==player.Id);
        Assert.NotNull(rider.RidingBusId);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.SetTravelModeAsync(player.Id,TravelMode.Run));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.TeleportAsync(player.Id,new(0,0,true)));
        for(var i=0;i<50;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        rider=world.CreateSnapshot().Players.Single(p=>p.Id==player.Id);
        Assert.Equal(world.GetTransitSnapshot().Buses.Single(b=>b.Id==rider.RidingBusId).Position,rider.Position);
        var exit=await world.GetOffBusAsync(player.Id);
        Assert.Null(exit.RidingBusId);Assert.True(exit.Position.Y<rider.Position.Y);
        var bus=world.GetTransitSnapshot().Buses.Single(b=>b.Id==rider.RidingBusId);Assert.Equal(0,bus.SpeedMetersPerSecond);
        for(var i=0;i<40;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        Assert.True(world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id).Position.Y < bus.Position.Y);
        Assert.Contains(world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id).Status,new[]{"out of service","pulling over","waiting for safe pull-over"});
        await world.TeleportAsync(player.Id,new(stop.Position.X,-stop.Position.Y,true));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.WaitForBusAsync(player.Id,stop.Id));
        Assert.Contains(world.GetTransitSnapshot().Routes,r=>r.StopIds.Contains(stop.Id)&&r.Path.Count>1);
    }

    [Fact]
    public async Task BusHitsPlayersAndNpcsOnceForFiveHeartsAndFlingsThemClear()
    {
        var (world,player)=await TransitWorld();
        await RequestTransitService(world);
        var npc=world.PlaceTestCharacter(player.Id,new("npc",-175,-2)).Actor!;
        var target=world.PlaceTestCharacter(player.Id,new("player",-160,-2)).Player!;
        var events=new List<CombatEvent>();
        for(var i=0;i<70;i++)events.AddRange((await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100))).Combat);
        Assert.Equal(5,world.CreateSnapshot().Actors!.Single(a=>a.Id==npc.Id).HealthHearts);
        var hitPlayer=world.CreateSnapshot().Players.Single(p=>p.Id==target.Id);
        Assert.Equal(5,hitPlayer.HealthHearts);Assert.True(hitPlayer.Position.Y < -5);Assert.True(hitPlayer.Position.X>target.Position.X);
        Assert.Single(events,e=>e.TargetId==npc.Id);Assert.Single(events,e=>e.TargetId==target.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusCannotPassThroughBuildingsOrCarsAndDamagesBoth(bool car)
    {
        var obstacle=new CanonicalEntity("obstruction",car?EntityKind.Vehicle:EntityKind.Building,new(new(45,-123),-150,-2),
            car?[]:[new(-152,-5),new(-148,-5),new(-148,1),new(-152,1),new(-152,-5)],
            new Dictionary<string,string>{["lengthMeters"]="4.5",["widthMeters"]="1.9"});
        var (world,_)=await TransitWorld(obstacle);
        await RequestTransitService(world);
        for(var i=0;i<80;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var bus=world.GetTransitSnapshot().Buses.First(b=>b.HeadingRadians==0);
        Assert.True(bus.Position.X < -156);Assert.True(bus.HealthHearts<100);Assert.Equal(0,bus.SpeedMetersPerSecond);
        var hit=world.CreateSnapshot().BaseEntities.Single(e=>e.Id=="obstruction");
        Assert.True(car?double.Parse(hit.Properties["damage"],System.Globalization.CultureInfo.InvariantCulture)>0:double.Parse(hit.Properties["healthHearts"],System.Globalization.CultureInfo.InvariantCulture)<5000);
    }

    [Fact]
    public async Task DeadEndUsesSlowContinuousTurnInsteadOfInstantReversal()
    {
        var (world,_)=await TransitWorld();
        await RequestTransitService(world);
        BusState? last=null;var turning=false;var reversed=false;
        for(var i=0;i<1000;i++)
        {
            var tick=await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));var bus=tick.Transit.Buses[0];
            if(bus.Status=="turning around") {turning=true;Assert.InRange(bus.SpeedMetersPerSecond,0,1.2);}
            if(last is not null)Assert.True(bus.Position.Distance2D(last.Position)<2);
            if(turning&&bus.HeadingRadians>3&&bus.Status=="driving") {reversed=true;break;}
            last=bus;
        }
        Assert.True(turning);Assert.True(reversed);
    }

    [Fact]
    public async Task NeighboringBlockKeepsCanonicalRoadStopIdsAndExistingBusRoute()
    {
        var (world,player)=await TransitWorld();
        var before=world.GetTransitSnapshot();var road=world.CreateSnapshot().BaseEntities.Single(e=>e.Id=="transit-road");
        var stop=before.Stops.First(s=>s.Direction=="eastbound"&&s.Position.X<0);
        await world.WaitForBusAsync(player.Id,stop.Id);
        Assert.True(await world.LoadAreaAsync(800,0));
        await world.AdvanceTransitAsync(TimeSpan.Zero);
        var after=world.GetTransitSnapshot();
        Assert.Equal(road.Geometry,world.CreateSnapshot().BaseEntities.Single(e=>e.Id==road.Id).Geometry);
        Assert.Contains(after.Stops,s=>s.Id==stop.Id&&s.Position==stop.Position);
        Assert.Equal(before.Routes[0].Path,after.Routes.First(r=>r.Id==before.Routes[0].Id).Path);
        Assert.Equal(stop.Id,world.CreateSnapshot().Players.Single(p=>p.Id==player.Id).WaitingAtBusStopId);
    }
}
