using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;
using System.Collections.Concurrent;
namespace AlternateEarth.Tests;
public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World,SqliteRealityStore Store,CanonicalEntity House,WorldPosition Spot)> GardenWorld()
    {
        var (world,store,house)=await CreateCraftingTestWorld();await world.ExitDungeonAsync("crafter");
        var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");
        // Isolate a known clear grass yard for behavioral tests. Geometry generation is tested separately.
        foreach(var key in entities.Keys.Where(k=>k!=house.Id).ToArray())entities.TryRemove(key,out _);
        PhotoField<ConcurrentDictionary<string,ActorState>>(world,"_actors").Clear();
        var spot=house.Position with{X=house.Position.X-25,Y=house.Position.Y};
        var players=PhotoField<ConcurrentDictionary<string,PlayerState>>(world,"_players");players["crafter"]=players["crafter"] with {Position=spot with {Y=spot.Y+3},LocationId="outdoor"};
        var supplies=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];supplies["seed:watermelon"]=100;supplies["wood"]=40;supplies["fertilizer"]=10;
        world.ProgressionRoll=()=>0;return (world,store,house,spot);
    }
    [Fact] public async Task GardenBuildHarvestSharedCooldownPersistenceAndPermanentRubble()
    {
        var (world,store,house,spot)=await GardenWorld();var made=await world.BuildGardenAsync("crafter",new("watermelon",spot.X,spot.Y));Assert.NotNull(made.Entity);
        Assert.Equal(50,PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"]["seed:watermelon"]);
        var harvest=await world.HarvestGardenAsync("crafter",made.Entity!.Id);Assert.Contains("Harvested",harvest.Message);
        Assert.Contains("Check again tomorrow",(await world.HarvestGardenAsync("crafter",made.Entity.Id)).Message);
        var players=PhotoField<ConcurrentDictionary<string,PlayerState>>(world,"_players");players["visitor"]=players["crafter"] with{Id="visitor"};
        Assert.Contains("Nothing available",(await world.HarvestGardenAsync("visitor",made.Entity.Id)).Message);
        var saved=(await store.LoadActiveEntitiesAsync(world.Configuration.Id)).Single(e=>e.Id==made.Entity.Id);Assert.Equal(harvest.Entity!.Properties["readyAtUtc"],saved.Properties["readyAtUtc"]);
        var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");entities[saved.Id]=saved with {Properties=new Dictionary<string,string>(saved.Properties){["healthHearts"]=".1"}};
        players["crafter"]=players["crafter"] with{EquippedWeapon="rifle"};var destroyed=await world.AttackWorldObjectAsync("crafter",saved.Id);Assert.Equal("rubble",destroyed.Entity.Properties["state"]);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.HarvestGardenAsync("crafter",saved.Id));
        entities[saved.Id]=destroyed.Entity with {Properties=new Dictionary<string,string>(destroyed.Entity.Properties){["rubbleUntilUtc"]=DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O")}};
        var expired=await world.AdvanceGardensAsync();Assert.Contains(saved.Id,expired.Removed);Assert.Contains(saved.Id,await store.LoadRemovedEntityIdsAsync(world.Configuration.Id));
    }
    [Fact] public async Task GardenInvalidPlacementCostsNothingAndFailureUsesOneBatch()
    {
        var (world,_,house,spot)=await GardenWorld();var before=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"]["wood"];
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BuildGardenAsync("crafter",new("watermelon",house.Position.X,house.Position.Y)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BuildGardenAsync("crafter",new("watermelon",spot.X+100,spot.Y)));
        Assert.Equal(before,PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"]["wood"]);
        world.ProgressionRoll=()=>1;var failed=await world.BuildGardenAsync("crafter",new("watermelon",spot.X,spot.Y));Assert.Null(failed.Entity);Assert.Contains("failed",failed.Message);Assert.Equal(before-20,PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"]["wood"]);
    }
    [Fact] public async Task GardenBookIsPermanentNonstackingAndPersists()
    {
        var (world,store,_,_)=await GardenWorld();PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"]["gardeningBook"]=2;
        var player=await world.ConsumeItemAsync("crafter","gardeningBook");Assert.True(player.Survival!.GardeningBookRead);Assert.True((await store.LoadCharacterAsync(world.Configuration.Id,"crafter"))!.Survival!.GardeningBookRead);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.ConsumeItemAsync("crafter","gardeningBook"));Assert.Equal(1,world.GetPrivateState("crafter").Inventory.Items.Single(i=>i.ItemType=="gardeningBook").Quantity);
    }
    [Fact] public async Task KitchenSinkDispensesFreeWaterAndReturnsMatchingContainers()
    {
        var (world,store,_)=await CreateCraftingTestWorld();var home=world.GetPrivateState("crafter").Dungeon!;var sink=home.Furnishings!.Single(e=>e.Properties["objectType"]=="kitchenSink");Assert.Equal("false",sink.Properties["stored"]);
        var players=PhotoField<ConcurrentDictionary<string,PlayerState>>(world,"_players");players["crafter"]=players["crafter"] with{Position=sink.Position,Water=0,HealthHearts=1,MaximumWater=100,MaximumHealthHearts=100};
        var inventory=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];inventory["emptyGlassBottle"]=1;inventory["emptyGlassJar"]=1;
        var drink=await world.UseKitchenSinkAsync("crafter",sink.Id,"drink");Assert.Equal(10,drink.Water);Assert.Equal(3,drink.HealthHearts);Assert.Equal(1,inventory["emptyGlassBottle"]);
        await world.UseKitchenSinkAsync("crafter",sink.Id,"bottle");await world.ConsumeItemAsync("crafter","bottledWater");Assert.Contains(world.GetPrivateState("crafter").Inventory.Items,i=>i.ItemType=="emptyGlassBottle"&&i.Quantity==1);
        var before=players["crafter"];await world.UseKitchenSinkAsync("crafter",sink.Id,"jar");var jar=await world.ConsumeItemAsync("crafter","jarOfWater");Assert.Equal(before.Water+50,jar.Water);Assert.Equal(before.HealthHearts+10,jar.HealthHearts);Assert.Contains(world.GetPrivateState("crafter").Inventory.Items,i=>i.ItemType=="emptyGlassJar"&&i.Quantity==1);
        var persisted=await store.LoadInventoryAsync("crafter");Assert.Contains(persisted.Items,i=>i.ItemType=="emptyGlassJar");
    }
    [Fact] public void HoseDirtyWaterAlwaysInfectsAndHasAtLeastElevenDifferentRemarks()
    {
        var player=new PlayerState("p","P",new(default,0,0),Water:0);var now=DateTimeOffset.UtcNow;
        Assert.Contains(RealityWorld.ApplyHoseWater(player,true,now,.5).Survival!.Illnesses!,i=>i.Name=="Parasites");
        Assert.Empty(RealityWorld.ApplyHoseWater(player,false,now,.5).Survival!.Illnesses!);Assert.True(RealityWorld.DirtyHoseRemarks.Distinct().Count()>=11);
    }
    [Fact] public async Task GardenConcurrentHarvestOnlyAwardsOnceAndReloadKeepsCooldown()
    {
        var (world,store,house,spot)=await GardenWorld();var made=(await world.BuildGardenAsync("crafter",new("watermelon",spot.X,spot.Y))).Entity!;
        var results=await Task.WhenAll(world.HarvestGardenAsync("crafter",made.Id),world.HarvestGardenAsync("crafter",made.Id));Assert.Single(results,r=>r.Message.StartsWith("Harvested"));
        var restarted=new RealityWorld(world.Configuration,new DeterministicWorldGenerator(new FixedGeographicProvider(house)),new FixedWeatherProvider(),store);
        await restarted.InitializeAsync();await restarted.JoinAsync("crafter","Crafter","crafter-account");
        var players=PhotoField<ConcurrentDictionary<string,PlayerState>>(restarted,"_players");players["crafter"]=players["crafter"] with{Position=spot,LocationId="outdoor"};
        Assert.Contains("Nothing available",(await restarted.HarvestGardenAsync("crafter",made.Id)).Message);
        var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(restarted,"_baseEntities");var garden=entities[made.Id];entities[made.Id]=garden with{Properties=new Dictionary<string,string>(garden.Properties){["readyAtUtc"]=DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O")}};
        Assert.Contains("Harvested",(await restarted.HarvestGardenAsync("crafter",made.Id)).Message);
    }
    [Fact] public async Task GardenFailedPersistenceCannotSpendSuppliesOrCreatePlot()
    {
        var (world,_,_,spot)=await GardenWorld();await using var connection=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_directory,"crafting.db")};Pooling=False");await connection.OpenAsync();var command=connection.CreateCommand();command.CommandText="CREATE TRIGGER reject_garden BEFORE INSERT ON RealityDeltas BEGIN SELECT RAISE(ABORT,'test failure'); END";await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(()=>world.BuildGardenAsync("crafter",new("watermelon",spot.X,spot.Y)));
        Assert.Equal(40,PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"]["wood"]);Assert.DoesNotContain(world.CreateSnapshot().BaseEntities,e=>e.Id.StartsWith("garden:player:"));
    }
    [Fact] public async Task HoseEnforcesRangeAndSeventyFivePercentDirtyThreshold()
    {
        var (world,_,_,spot)=await GardenWorld();var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");var hose=new CanonicalEntity("test-hose",EntityKind.ResourceNode,spot,[],new Dictionary<string,string>{["subtype"]="gardenHose"});entities[hose.Id]=hose;
        world.ProgressionRoll=()=>.749;var dirty=await world.DrinkHoseAsync("crafter",hose.Id);Assert.NotNull(dirty.Remark);Assert.Contains(dirty.Player.Survival!.Illnesses!,i=>i.Name=="Parasites");
        PhotoField<ConcurrentDictionary<string,DateTimeOffset>>(world,"_lastHoseDrink").Clear();world.ProgressionRoll=()=>.75;Assert.Null((await world.DrinkHoseAsync("crafter",hose.Id)).Remark);
        entities[hose.Id]=hose with{Position=spot with{X=spot.X+20}};await Assert.ThrowsAsync<InvalidOperationException>(()=>world.DrinkHoseAsync("crafter",hose.Id));
    }

}
