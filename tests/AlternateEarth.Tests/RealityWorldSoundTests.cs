using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Theory]
    [InlineData("cow","Mooooo!")]
    [InlineData("chicken","Cluck cluck!")]
    [InlineData("pig","Oink!")]
    [InlineData("sheep","Baaaa!")]
    [InlineData("goat","Meeeeh!")]
    public async Task LivestockSayMatchingAnimalCalls(string subtype,string line)
    {
        var (world,player)=await TransitWorld();
        var actor=new ActorState("farm-animal",EntityKind.Animal,subtype,subtype,player.Position);
        var method=typeof(RealityWorld).GetMethod("ActorSpeech",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        Assert.Equal(line,method.Invoke(world,[actor]));
    }

    [Fact]
    public void BusWarningDetectsPeopleAheadAtEveryHeadingButNotOnSidewalkBehindOrInAnotherRegion()
    {
        for(var h=0d;h<Math.PI*2;h+=Math.PI/8)
        {
            var bus=new BusState("bus","route","Route",new(new(45,-123),10,20),h);
            WorldPosition Point(double along,double side)=>bus.Position with{X=10+Math.Cos(h)*along-Math.Sin(h)*side,Y=20+Math.Sin(h)*along+Math.Cos(h)*side};
            Assert.True(TransitGeometry.InBusWarningPath(bus,Point(10,0),7));
            Assert.False(TransitGeometry.InBusWarningPath(bus,Point(-10,0),7));
            Assert.False(TransitGeometry.InBusWarningPath(bus,Point(10,3),7));
            Assert.False(TransitGeometry.InBusWarningPath(bus,Point(50,0),7));
            Assert.False(TransitGeometry.InBusWarningPath(bus,Point(10,0) with{Region=new(0,0)},7));
            Assert.False(TransitGeometry.InBusWarningPath(bus,Point(10,0),0));
        }
    }

    [Fact]
    public async Task BusEmitsMatchingHornAndTextBeforeHittingPlayerAndThrottlesRepeatedWarnings()
    {
        var (world,player)=await TransitWorld();await RequestTransitService(world);
        for(var i=0;i<8;i++)await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var bus=world.GetTransitSnapshot().Buses.First();var mutable=MutableBus(world,bus.Id);
        Assert.True(bus.SpeedMetersPerSecond>0);
        await world.TeleportAsync(player.Id,new(bus.Position.X+Math.Cos(bus.HeadingRadians)*12,bus.Position.Y+Math.Sin(bus.HeadingRadians)*12,true));
        var tick=await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));
        var warning=Assert.Single(tick.Sounds!,s=>s.SpeakerId==bus.Id);
        Assert.Equal("honk",warning.Sound);Assert.Equal("HONK!",warning.Text);Assert.DoesNotContain(tick.Combat,c=>c.TargetId==player.Id);
        var next=await world.AdvanceTransitAsync(TimeSpan.FromMilliseconds(100));Assert.DoesNotContain(next.Sounds!,s=>s.SpeakerId==bus.Id);
        foreach(var expected in new[]{("beep","BEEP!"),("ahooga","AhOoooooGa!")})
        {
            mutable.GetType().GetField("HornCooldown")!.SetValue(mutable,0d);
            var current=world.GetTransitSnapshot().Buses.Single(b=>b.Id==bus.Id);
            var npc=new ActorState("warning-npc",EntityKind.Npc,"ordinary","Pedestrian",current.Position with{X=current.Position.X+Math.Cos(current.HeadingRadians)*10,Y=current.Position.Y+Math.Sin(current.HeadingRadians)*10});
            var warnings=new List<WorldSoundEvent>();
            typeof(RealityWorld).GetMethod("WarnBusPeople",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[mutable,7d,Array.Empty<PlayerState>(),new[]{npc},warnings]);
            var sound=Assert.Single(warnings);
            Assert.Equal(expected.Item1,sound.Sound);Assert.Equal(expected.Item2,sound.Text);
        }
    }
}
