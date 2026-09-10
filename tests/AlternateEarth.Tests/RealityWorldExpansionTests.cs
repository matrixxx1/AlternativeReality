using AlternateEarth.Server;
using AlternateEarth.Shared;
namespace AlternateEarth.Tests;
public sealed partial class RealityWorldTests
{
    [Theory][InlineData("turns")][InlineData("cards")][InlineData("retro")]
    public async Task ExpiredEventDungeonsReturnPlayersWithoutCompletionRewardsOrInventoryPenalty(string mode)
    {
        var (world, id, _, clock) = await RetroFixture();
        var player = world.GetRetroBattleUpdate(id).Player!;
        var items = world.GetPrivateState(id).Inventory.Items.ToArray();
        world.StartInversion(mode, player, clock.GetUtcNow()); await world.EnterEventDungeonAsync(id);
        clock.Advance(601); await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal("outdoor", world.GetRetroBattleUpdate(id).Player!.LocationId);
        Assert.Null(world.GetPrivateState(id).Dungeon);
        Assert.Equal(items, world.GetPrivateState(id).Inventory.Items);
        Assert.DoesNotContain(world.GetPrivateState(id).Loot!, l => l.DropKind == "eventReward");
        Assert.Equal(new[] { id }, world.TakeInversionDungeonExits());
    }
    [Theory]
    [InlineData("resident", true, false, true)]
    [InlineData("resident", false, true, true)]
    [InlineData("resident", false, false, true)]
    [InlineData("sunflower", false, false, false)]
    [InlineData("rose", false, false, false)]
    [InlineData("tulip", false, false, false)]
    [InlineData("daisy", false, false, false)]
    [InlineData("plantsBoss", false, false, false)]
    public async Task ZombieHitsInfectNpcsIncludingQuestGiversButNeverFlowers(string subtype, bool questGiver, bool merchant, bool infects)
    {
        var (world, player, _) = await CreateProbulatorTestWorld();
        var actors = ImpressionActors(world); actors.Clear();
        var position = player.Position with { X = player.Position.X + 10 };
        actors["biter"] = new("biter", EntityKind.Npc, "zombie", "Zombie", position);
        actors["victim"] = new("victim", EntityKind.Npc, subtype, "Victim", position with { X = position.X + .1 }, IsQuestGiver: questGiver, IsMerchant: merchant);
        var quest = new QuestState("infection-test", player.Id, "victim", "Victim", "delivery", "active", "Delivery", "Bring items", 100);
        PhotoField<System.Collections.Concurrent.ConcurrentDictionary<(string Player, string Quest), QuestState>>(world, "_quests")[(player.Id, quest.Id)] = quest;
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal(infects ? "zombie" : subtype, actors["victim"].Subtype);
        if (infects)
        {
            Assert.False(actors["victim"].IsQuestGiver); Assert.False(actors["victim"].IsMerchant);
            Assert.Equal("failed", world.GetPrivateState(player.Id).Quests!.Single(q => q.Id == quest.Id).Status);
        }
    }
    [Fact] public async Task HourlyVoteDefaultsExpireAndStartExactlyOneRegisteredEventNearPlayer()
    {
        var clock=new ProbulatorTestClock();var (world,player,_)=await CreateProbulatorTestWorld(clock:clock);
        await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));Assert.Null(world.GetInversionView(player.Id).Vote);
        clock.Advance(3600);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        var vote=world.GetInversionView(player.Id).Vote!;Assert.Equal(4,vote.Options.Count);Assert.Equal("random",vote.Options[0].Id);Assert.Contains(player.Name,vote.Options[0].Voters);
        var choice=vote.Options[1].Id;world.CastServerVote(player.Id,choice);clock.Advance(59);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));Assert.Null(world.GetInversionView(player.Id).Active);
        clock.Advance(1);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));var e=world.GetInversionView(player.Id).Active!;
        Assert.Equal(choice,e.Type);Assert.Equal(player.Position,e.Center);Assert.Null(world.GetInversionView(player.Id).Vote);
        world.StartServerVote(player.Id);clock.Advance(60);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal(e.Id,world.GetInversionView(player.Id).Active!.Id);Assert.Single(world.GetInversionView(player.Id).Queued);
    }
    [Theory][InlineData("turns")][InlineData("cards")]
    public async Task AiBattleRequiresTwoWinsAndValidatesActionsAndDecks(string mode)
    {
        var (world,id,_,clock)=await RetroFixture();world.StartInversion(mode,world.GetRetroBattleUpdate(id).Player!,clock.GetUtcNow());
        var d=await world.EnterEventDungeonAsync(id);Assert.Equal(1,d.EventBattle!.DungeonNumber);
        Assert.Throws<InvalidOperationException>(()=>world.ConfigureEventDeck(id,["Strike"]));
        if(mode=="cards")world.ConfigureEventDeck(id,["Heavy Blow","Heavy Blow","Strike","Strike","Poison","Focus"]);
        for(var dungeon=1;dungeon<=2;dungeon++)
        {
            for(var turn=0;turn<60&&!world.GetPrivateState(id).Dungeon!.IsCompleted;turn++)
            {var b=world.GetPrivateState(id).Dungeon!.EventBattle!;await world.PlayEventBattleAsync(id,mode=="cards"?b.Hand[0]:"Strike");}
            Assert.True(world.GetPrivateState(id).Dungeon!.IsCompleted);Assert.Equal(dungeon,world.GetInversionView(id).CompletedDungeons);
            if(dungeon==1){Assert.DoesNotContain(world.GetPrivateState(id).Loot!,l=>l.DropKind=="eventReward");d=await world.EnterEventDungeonAsync(id);Assert.Equal(60,d.EventBattle!.MaximumEnemyHealth);}
        }
        var loot=Assert.Single(world.GetPrivateState(id).Loot!,l=>l.DropKind=="eventReward");
        await world.TakeLootItemsAsync(id,new(loot.Id,[]));Assert.Equal(0,world.OpenLoot(id,loot.Id).MoneyCents);
        var store=(AlternateEarth.Server.SqliteRealityStore)typeof(RealityWorld).GetField("_store",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(world)!;
        Assert.Equal(0,(await store.LoadPersistentLootAsync(world.Configuration.Id)).Single(l=>l.Id==loot.Id).MoneyCents);
    }
    [Fact] public async Task PlantTransformationRestoresWeaponAndNeverRestoresDefeatedNpc()
    {
        var clock=new ProbulatorTestClock();var(world,p,_)=await CreateProbulatorTestWorld(clock:clock);
        var npc=world.PlaceTestCharacter(p.Id,new("npc",2,0)).Actor! with {IsQuestGiver=true,IsMerchant=true};ImpressionActors(world)[npc.Id]=npc;
        world.StartInversion("plants",p,clock.GetUtcNow());await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal("zombieBite",world.GetRetroBattleUpdate(p.Id).Player!.EquippedWeapon);
        Assert.Contains(ImpressionActors(world)[npc.Id].Subtype,new[]{"sunflower","rose","tulip","daisy"});Assert.False(ImpressionActors(world)[npc.Id].IsQuestGiver);
        ImpressionActors(world).TryRemove(npc.Id,out _);clock.Advance(601);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        Assert.NotEqual("zombieBite",world.GetRetroBattleUpdate(p.Id).Player!.EquippedWeapon);Assert.DoesNotContain(npc.Id,ImpressionActors(world).Keys);
    }
    [Fact] public void DungeonBarriersAndWaterRespectDifficultyThresholds()
    {
        var d=new DungeonState("d","b",30,30,[],[new(15,0,15,30,12,14),new(0,15,30,15,12,14)],new(new(45,-123),2,2),[],[],[],ExteriorWallCount:0);
        Assert.Null(RealityWorld.AddDungeonObstacles(d with{Difficulty=4},2).Barriers);
        Assert.All(RealityWorld.AddDungeonObstacles(d with{Difficulty=5},2).Barriers!,b=>Assert.Equal("fire",b.Kind));
        var level10=RealityWorld.AddDungeonObstacles(d with{Difficulty=10},2);Assert.Contains(level10.Barriers!,b=>b.Kind=="blast");Assert.Empty(level10.WaterAreas!);
        Assert.Contains(RealityWorld.AddDungeonObstacles(d with{Difficulty=15},2).WaterAreas!,w=>w.Deep);
    }
    [Fact] public async Task CommuterUsesDrivewaysAndRoadsThenPausesOutsidePlayerRange()
    {
        var region=new RegionId(45,-123);
        CanonicalEntity Parking(string id,double x)=>new(id,EntityKind.Terrain,new(region,x,-9),[new(x-2,-12),new(x+2,-12),new(x+2,0),new(x-2,0),new(x-2,-12)],new Dictionary<string,string>{{"terrain","pavement"},{"subtype","driveway"},{"roadId","transit-road"}});
        var(world,player)=await TransitWorld(Parking("commuter-a",-80),Parking("commuter-b",80));
        ImpressionActors(world).Clear();ImpressionActors(world)["commuter"]=new("commuter",EntityKind.Npc,"resident","Commuter",new(region,-80,-9));
        await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        var car=Assert.Single(world.CreateSnapshot().BaseEntities,e=>e.Id.StartsWith("npc-car:"));var start=car.Position;
        var moved=false;
        for(var i=0;i<50;i++){await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));car=world.CreateSnapshot().BaseEntities.Single(e=>e.Id==car.Id);moved|=car.Position.Distance2D(start)>3;Assert.True(Math.Abs(car.Position.Y)<=4||Math.Abs(car.Position.X+80)<=2||Math.Abs(car.Position.X-80)<=2);}
        Assert.True(moved);
        var players=(System.Collections.Concurrent.ConcurrentDictionary<string,PlayerState>)typeof(RealityWorld).GetField("_players",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(world)!;
        players[player.Id]=player with{Position=player.Position with{X=2000,Y=2000},Version=player.Version+1000};var paused=car.Position;
        for(var i=0;i<10;i++)await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal(paused,world.CreateSnapshot().BaseEntities.Single(e=>e.Id==car.Id).Position);
    }

    [Fact] public async Task CameraMustBeEquippedAndConsumesExactlyOneFramePerExposure()
    {
        var(world,id,_,clock)=await RetroFixture();
        typeof(RealityWorld).GetMethod("AddInventory",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(world,[id,"camera",1,null]);
        await world.SetGodModeAsync(id,false);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.PhotographAsync(id,null));
        await world.SetEquipmentAsync(id,"weapon","camera");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.PhotographAsync(id,null));
        typeof(RealityWorld).GetMethod("AddInventory",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(world,[id,"film",2,null]);
        await world.PhotographAsync(id,null);Assert.Equal(1,world.GetPrivateState(id).Inventory.Items.Single(i=>i.ItemType=="film").Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.PhotographAsync(id,null));
        clock.Advance(1);await world.PhotographAsync(id,null);Assert.DoesNotContain(world.GetPrivateState(id).Inventory.Items,i=>i.ItemType=="film");
        clock.Advance(1);await Assert.ThrowsAsync<InvalidOperationException>(()=>world.PhotographAsync(id,null));
    }
    [Fact] public async Task MusicalFruitAllowsPlayersToLeaveTheirCloudBeforeItBecomesHarmful()
    {
        var clock=new ProbulatorTestClock();var(world,p,_)=await CreateProbulatorTestWorld(clock:clock);await world.SetGodModeAsync(p.Id,false);
        var players=(System.Collections.Concurrent.ConcurrentDictionary<string,PlayerState>)typeof(RealityWorld).GetField("_players",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(world)!;
        p=players[p.Id];world.StartInversion("fruit",p,clock.GetUtcNow());
        ImpressionActors(world).Clear();await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));Assert.Equal(10,players[p.Id].HealthHearts);
        players[p.Id]=players[p.Id] with{Position=p.Position with{X=p.Position.X+3},Version=players[p.Id].Version+1};clock.Advance(1);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));Assert.Equal(10,players[p.Id].HealthHearts);
        clock.Advance(1);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));Assert.Equal(9.75,players[p.Id].HealthHearts);
    }

}
