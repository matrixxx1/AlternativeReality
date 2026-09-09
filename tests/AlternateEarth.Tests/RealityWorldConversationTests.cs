using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Server;
using AlternateEarth.Shared;
namespace AlternateEarth.Tests;
public sealed partial class RealityWorldTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NpcStopsDuringTradeOrQuestAndResumesAfterClosing(bool quest)
    {
        var clock=new ProbulatorTestClock();var (world,player,_)=await CreateProbulatorTestWorld(clock:clock);
        var npc=world.PlaceTestCharacter(player.Id,new("npc",1,0)).Actor! with { IsTestCharacter=false,IsMerchant=true,IsQuestGiver=quest,IsMoving=true };
        ImpressionActors(world)[npc.Id]=npc;
        var routes=(ConcurrentDictionary<string,Queue<WorldPosition>>)typeof(RealityWorld).GetField("_actorRoutes",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(world)!;
        var navigation=new WorldNavigation(world.Configuration.Area.Bounds,world.CreateSnapshot().BaseEntities,[]);
        var destination=Enumerable.Range(0,8).Select(i=>npc.Position with { X=npc.Position.X+Math.Cos(i*Math.PI/4)*3,Y=npc.Position.Y+Math.Sin(i*Math.PI/4)*3 }).First(p=>navigation.CanTraverse(npc.Position,p,true));
        routes[npc.Id]=new Queue<WorldPosition>([destination]);
        if(quest)world.RequestQuest(player.Id,npc.Id);else world.RequestTrade(player.Id,npc.Id);
        world.AdvanceActors(TimeSpan.FromSeconds(.2));
        Assert.Equal(npc.Position,ImpressionActors(world)[npc.Id].Position);Assert.False(ImpressionActors(world)[npc.Id].IsMoving);
        clock.Advance(15);world.UpdateConversation(player.Id,npc.Id,true);clock.Advance(10);world.AdvanceActors(TimeSpan.FromSeconds(.2));
        Assert.Equal(npc.Position,ImpressionActors(world)[npc.Id].Position);
        world.UpdateConversation(player.Id,npc.Id,false);world.AdvanceActors(TimeSpan.FromSeconds(.2));
        if (quest) Assert.Equal(npc.Position,ImpressionActors(world)[npc.Id].Position); else Assert.NotEqual(npc.Position,ImpressionActors(world)[npc.Id].Position);
    }
    [Fact]
    public async Task AbandonedConversationExpiresAndHeartbeatCannotFreezeAnUnrequestedNpc()
    {
        var clock=new ProbulatorTestClock();var (world,player,_)=await CreateProbulatorTestWorld(clock:clock);
        var npc=world.PlaceTestCharacter(player.Id,new("npc",1,0)).Actor! with { IsMerchant=true };
        ImpressionActors(world)[npc.Id]=npc;
        var leases=(ConcurrentDictionary<(string Player,string Actor),DateTimeOffset>)typeof(RealityWorld).GetField("_conversations",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(world)!;
        world.UpdateConversation(player.Id,npc.Id,true);Assert.Empty(leases);
        world.RequestTrade(player.Id,npc.Id);Assert.Single(leases);
        clock.Advance(21);world.AdvanceActors(TimeSpan.FromSeconds(.1));Assert.Empty(leases);
        world.RequestTrade(player.Id,npc.Id);await world.LeaveAsync(player.Id);world.AdvanceActors(TimeSpan.FromSeconds(.1));Assert.Empty(leases);
    }
}
