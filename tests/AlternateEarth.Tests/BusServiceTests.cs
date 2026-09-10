using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static object MutableBus(RealityWorld world,string id)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var buses=(System.Collections.IDictionary)typeof(RealityWorld).GetField("_buses",flags)!.GetValue(world)!;
        return buses.Values.Cast<object>().Single(b=>((BusState)b.GetType().GetField("State")!.GetValue(b)!).Id==id);
    }
    [Fact]
    public async Task StoppedBusBoardsFromDoorSideAndResumesItsRoute()
    {
        var (world,player)=await TransitWorld();
        await world.TeleportAsync(player.Id,new(0,-100,true));
        for(var i=0;i<200;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var bus=world.GetTransitSnapshot().Buses.First();
        Assert.Equal("out of service",bus.Status);Assert.True(Math.Abs(bus.Position.Y)>5.5);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BoardBusAsync(player.Id,bus.Id));
        var dx=Math.Cos(bus.HeadingRadians);var dy=Math.Sin(bus.HeadingRadians);
        await world.TeleportAsync(player.Id,new(bus.Position.X-dy*2,bus.Position.Y+dx*2,true));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BoardBusAsync(player.Id,bus.Id));
        await world.TeleportAsync(player.Id,new(bus.Position.X+dy*2,bus.Position.Y-dx*2,true));
        var boarded=await world.BoardBusAsync(player.Id,bus.Id);
        Assert.Equal(bus.Id,boarded.RidingBusId);Assert.Null(boarded.WaitingAtBusStopId);
        for(var i=0;i<120;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var moving=world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id);
        Assert.True(moving.SpeedMetersPerSecond>0);Assert.Equal("driving",moving.Status);
        Assert.Equal(moving.Position,world.CreateSnapshot().Players.Single(p=>p.Id==player.Id).Position);
    }
    [Fact]
    public async Task BusAttackUsesWeaponAndDisablesThenPullsOffWithoutRepairing()
    {
        var (world,player)=await TransitWorld();var bus=world.GetTransitSnapshot().Buses.First();
        var mutable=MutableBus(world,bus.Id);
        mutable.GetType().GetField("State")!.SetValue(mutable,bus with {HealthHearts=1});
        await world.AdvanceTransitAsync(TimeSpan.Zero);
        await world.SetEquipmentAsync(player.Id,"weapon","knife");
        await world.UpdateItemConfigurationAsync(player.Id, new("knife", 2, 1.6, 2_000, 4_000, Accuracy: 1));
        // Touching the vehicle gives deterministic configured melee accuracy at zero distance.
        await world.TeleportAsync(player.Id,new(bus.Position.X+Math.Sin(bus.HeadingRadians)*1.25,bus.Position.Y-Math.Cos(bus.HeadingRadians)*1.25,true));
        var result=await world.AttackAsync(player.Id,new(bus.Id,"rifle"));
        Assert.Equal("knife",result.Event.Weapon);Assert.True(result.Event.Hit);
        Assert.Equal(0,world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id).HealthHearts);
        await world.TeleportAsync(player.Id,new(0,-100,true));
        for(var i=0;i<200;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var parked=world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id);
        Assert.Equal("disabled",parked.Status);Assert.Equal(0,parked.SpeedMetersPerSecond);
        Assert.True(Math.Abs(parked.Position.Y)>5.5);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BoardBusAsync(player.Id,bus.Id));
        await RequestTransitService(world);
        for(var i=0;i<10;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        Assert.Equal(parked.Position,world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id).Position);
    }
    [Fact]
    public async Task BusDoesNotParkThroughABuildingWhenNoShoulderIsAvailable()
    {
        var wall=new CanonicalEntity("shoulder-building",EntityKind.Building,new(new(45,-123),-180,-12),
            [new(-250,-4),new(250,-4),new(250,-30),new(-250,-30),new(-250,-4)],new Dictionary<string,string>());
        var (world,player)=await TransitWorld(wall);await world.TeleportAsync(player.Id,new(0,100,true));
        var bus=world.GetTransitSnapshot().Buses.First();
        for(var i=0;i<40;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var stopped=world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id);
        Assert.Equal(bus.Position,stopped.Position);Assert.Equal("waiting for safe pull-over",stopped.Status);
        Assert.False(TransitGeometry.Overlaps(TransitGeometry.Footprint(stopped.Position,stopped.HeadingRadians),wall.Geometry));
    }
    [Fact]
    public async Task MovingBusRejectsDirectBoarding()
    {
        var (world,player)=await TransitWorld();await RequestTransitService(world);
        for(var i=0;i<8;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var bus=world.GetTransitSnapshot().Buses.First();Assert.True(bus.SpeedMetersPerSecond>0);
        await world.TeleportAsync(player.Id,new(bus.Position.X+Math.Sin(bus.HeadingRadians)*2,bus.Position.Y-Math.Cos(bus.HeadingRadians)*2,true));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BoardBusAsync(player.Id,bus.Id));
        Assert.Contains("stops",error.Message);
    }
    [Theory]
    [InlineData("boarding")]
    [InlineData("dropping off")]
    [InlineData("yielding")]
    [InlineData("blocked")]
    [InlineData("waiting for map")]
    [InlineData("waiting for safe pull-over")]
    [InlineData("rejoining route")]
    public async Task ActiveBusRejectsDirectBoardingEvenWhenStationary(string status)
    {
        var (world,player)=await TransitWorld();
        var bus=world.GetTransitSnapshot().Buses.First();
        var mutable=MutableBus(world,bus.Id);
        mutable.GetType().GetField("State")!.SetValue(mutable,bus with {Status=status,SpeedMetersPerSecond=0});
        await world.TeleportAsync(player.Id,new(bus.Position.X+Math.Sin(bus.HeadingRadians)*2,bus.Position.Y-Math.Cos(bus.HeadingRadians)*2,true));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BoardBusAsync(player.Id,bus.Id));
        Assert.Contains("Wait at a bus stop",error.Message);
        Assert.Null(world.CreateSnapshot().Players.Single(p=>p.Id==player.Id).RidingBusId);
    }
}
