using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class ExpansionRulesTests
{
    private static PlayerState Person => new("p", "Player", new(new(45, -123), 0, 0));
    [Fact] public void AirDrainsThenSuffocatesOverFiveSecondsAndRefills()
    {
        var p = Person;
        for (var i=0;i<10;i++) p=RealityWorld.StepAir(p,.5,true,false,false);
        Assert.Equal(0,p.Air);Assert.Equal(10,p.HealthHearts);
        for(var i=0;i<9;i++) p=RealityWorld.StepAir(p,.5,true,false,false);
        Assert.Equal(1,p.HealthHearts);
        p=RealityWorld.StepAir(p,.5,true,false,false);Assert.Equal(0,p.HealthHearts);
        p=Person with{Air=0};for(var i=0;i<10;i++)p=RealityWorld.StepAir(p,.5,false,false,false);
        Assert.Equal(10,p.Air);Assert.Equal(10,p.HealthHearts);
    }
    [Theory]
    [InlineData(TravelMode.Walk,8)] [InlineData(TravelMode.Raft,10)] [InlineData(TravelMode.Ufo,10)] [InlineData(TravelMode.Swim,10)]
    public void DeepWaterProtectionDependsOnTravel(TravelMode mode,double air)
    { Assert.Equal(air,RealityWorld.StepAir(Person with{TravelMode=mode},1,false,true,false).Air); }
    [Fact] public void ExhaustedSwimmersCannotRecoverByConstantStruggling()
    {
        var p=RealityWorld.StepAir(Person with{TravelMode=TravelMode.Swim,Stamina=0},1,false,true,true);
        Assert.True(p.SwimExhausted);Assert.Equal(8,p.Air);
        p=RealityWorld.StepAir(p with{Stamina=2},1,false,true,true);Assert.True(p.SwimExhausted);
        p=RealityWorld.StepAir(p,1,false,true,false);Assert.False(p.SwimExhausted);Assert.Equal(8,p.Air);
    }
    [Fact] public void VoteDefaultsCanBeReplacedAndRunoffsClearAllBallots()
    {
        var now=DateTimeOffset.UtcNow;var vote=new ServerVote(now,["mech","cards","fruit"]);
        vote.SyncPlayers(["a","b"]);var names=new Dictionary<string,string>{{"a","A"},{"b","B"}};
        Assert.Equal(2,vote.Snapshot(names).Options[0].Voters.Count);
        vote.Cast("a","mech",now);vote.Cast("a","cards",now);vote.Cast("b","mech",now);
        Assert.Null(vote.Finish(now.AddMinutes(1)));Assert.Equal(2,vote.Round);
        Assert.Equal(new[]{"mech","cards"},vote.Options);
        Assert.All(vote.Snapshot(names).Options,o=>Assert.Empty(o.Voters));
        vote.SyncPlayers(["a","b"]);Assert.All(vote.Snapshot(names).Options,o=>Assert.Empty(o.Voters));
        vote.Cast("a","cards",now.AddSeconds(61));Assert.Equal("cards",vote.Finish(now.AddMinutes(2)));
    }
    [Fact] public void VoteRejectsUnknownChoicesAndLateVotes()
    {
        var now=DateTimeOffset.UtcNow;var v=new ServerVote(now,["mech","cards","fruit"]);
        Assert.Throws<InvalidOperationException>(()=>v.Cast("a","made-up",now));
        Assert.Throws<InvalidOperationException>(()=>v.Cast("a","mech",now.AddMinutes(1)));
    }
    [Fact] public void IdleCapIncludesTheWholeMapBlock()
    {
        Assert.True(RealityWorld.WithinIdleMapCap(new(-500,-500,500,500),Person.Position));
        Assert.False(RealityWorld.WithinIdleMapCap(new(8000,-500,8500,500),Person.Position));
    }
    [Fact] public void EventRegistryContainsEveryApprovedEventAndNoGravity()
    {
        Assert.Equal(26,InversionCatalog.All.Count);Assert.Equal(26,InversionCatalog.All.Select(e=>e.Id).Distinct().Count());
        Assert.DoesNotContain(InversionCatalog.All,e=>e.Name.Contains("Gravity"));
        Assert.All(InversionCatalog.All,e=>Assert.False(string.IsNullOrWhiteSpace(e.Boss)));
    }
}
