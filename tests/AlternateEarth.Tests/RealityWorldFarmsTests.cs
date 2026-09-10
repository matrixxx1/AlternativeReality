using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;
using System.Collections.Concurrent;
namespace AlternateEarth.Tests;
public sealed partial class RealityWorldTests
{
    private static int FarmQuantity(RealityWorld world,string item)=>world.GetPrivateState("crafter").Inventory.Items.FirstOrDefault(i=>i.ItemType==item)?.Quantity??0;
    [Theory]
    [InlineData(true,.15,.5,"jarOfMilk")]
    [InlineData(false,.15,.5,"bottleOfMilk")]
    [InlineData(true,.149,.49,"jarOfFertilizer")]
    [InlineData(false,.149,.5,"bottleOfUrine")]
    public async Task FarmMilkUsesJarFirstAndWrongContentsThreshold(bool jar,double wrong,double content,string output)
    {
        var (world,store,_,spot)=await GardenWorld();var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");var cow=FarmRules.Create("cow",spot,"farm",true);entities[cow.Id]=cow;
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];pack["emptyGlassJar"]=jar?1:0;pack["emptyGlassBottle"]=2;
        var rolls=new Queue<double>([.799,wrong,content]);world.ProgressionRoll=()=>rolls.Dequeue();var result=await world.UseFarmAnimalAsync("crafter",cow.Id,"milk");
        Assert.Equal(1,FarmQuantity(world,output));Assert.Equal(jar?2:1,FarmQuantity(world,"emptyGlassBottle"));Assert.Equal(0,FarmQuantity(world,"emptyGlassJar"));
        if(wrong<.15)Assert.Equal("Well... I screwed that up!",result.Remark!.Message);else Assert.Null(result.Remark);
        Assert.Contains((await store.LoadInventoryAsync("crafter")).Items,i=>i.ItemType==output&&i.Quantity==1);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseFarmAnimalAsync("crafter",cow.Id,"milk"));
        Assert.Contains(await store.LoadActiveEntitiesAsync(world.Configuration.Id),e=>e.Id==cow.Id&&e.Properties.ContainsKey("milkReadyUtc"));
    }
    [Fact] public async Task FarmMilkFailureBreaksOneJarAndInvalidActionsSpendNothing()
    {
        var (world,_,_,spot)=await GardenWorld();var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");entities["cow"]=FarmRules.Create("cow",spot,"farm",true);entities["hen"]=FarmRules.Create("hen",spot,"farm",false);
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];pack["emptyGlassJar"]=2;pack["emptyGlassBottle"]=2;world.ProgressionRoll=()=>.8;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseFarmAnimalAsync("crafter","hen","milk"));Assert.Equal(2,FarmQuantity(world,"emptyGlassJar"));
        var result=await world.UseFarmAnimalAsync("crafter","cow","milk");Assert.Contains("broke your jar",result.Result.Message);Assert.Equal(1,FarmQuantity(world,"emptyGlassJar"));Assert.Equal(2,FarmQuantity(world,"emptyGlassBottle"));
        entities["cow"]=FarmRules.Create("cow",spot with{X=spot.X+100},"farm",true);await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseFarmAnimalAsync("crafter","cow","milk"));Assert.Equal(1,FarmQuantity(world,"emptyGlassJar"));
    }
    [Fact] public async Task FarmProductionNeedsNearbyPlayersAndCollectionIsSharedAndPersistent()
    {
        var (world,store,house,spot)=await GardenWorld();var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");entities["cow"]=FarmRules.Create("cow",spot,"farm",true);entities["hen"]=FarmRules.Create("hen",spot,"farm",false);entities["far"]=FarmRules.Create("far",spot with{X=spot.X+100},"farm",false);
        world.ProgressionRoll=()=>0;var drops=await world.AdvanceFarmsAsync();Assert.Equal(2,drops.Count);Assert.All(drops,e=>Assert.Equal("1",e.Properties["productQuantity"]));Assert.Empty(await world.AdvanceFarmsAsync());
        var attempts=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{try{await world.UseFarmAnimalAsync("crafter","hen","collect");return true;}catch(InvalidOperationException){return false;}}));Assert.Single(attempts,a=>a);Assert.Equal(1,FarmQuantity(world,"egg"));
        var restarted=new RealityWorld(world.Configuration,new DeterministicWorldGenerator(new FixedGeographicProvider(house)),new FixedWeatherProvider(),store);await restarted.InitializeAsync();await restarted.JoinAsync("crafter","Crafter","crafter-account");
        var players=PhotoField<ConcurrentDictionary<string,PlayerState>>(restarted,"_players");players["crafter"]=players["crafter"] with{Position=spot,LocationId="outdoor"};await restarted.UseFarmAnimalAsync("crafter","cow","collect");Assert.Equal(1,FarmQuantity(restarted,"fertilizer"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>restarted.UseFarmAnimalAsync("crafter","hen","collect"));
    }
    [Fact] public async Task FarmProductionThresholdAndPileLimitAreEnforced()
    {
        var (world,_,_,spot)=await GardenWorld();var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");entities["hen"]=FarmRules.Create("hen",spot,"farm",false);
        world.ProgressionRoll=()=>.3;var first=await world.AdvanceFarmsAsync();Assert.Equal("0",Assert.Single(first).Properties["productQuantity"]);
        void Ready(){typeof(RealityWorld).GetField("_nextFarmTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(world,DateTimeOffset.MinValue);var e=entities["hen"];entities["hen"]=e with {Properties=new Dictionary<string,string>(e.Properties){["nextProductionUtc"]=DateTimeOffset.MinValue.ToString("O")}};}
        world.ProgressionRoll=()=>.299;for(var i=0;i<5;i++){Ready();await world.AdvanceFarmsAsync();}Assert.Equal("5",entities["hen"].Properties["productQuantity"]);Ready();Assert.Empty(await world.AdvanceFarmsAsync());
    }
    [Fact] public async Task FarmFailedSavePreservesContainerAndCowCooldown()
    {
        var (world,_,_,spot)=await GardenWorld();var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");entities["cow"]=FarmRules.Create("cow",spot,"farm",true);PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"]["emptyGlassJar"]=1;
        await using var connection=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_directory,"crafting.db")};Pooling=False");await connection.OpenAsync();var command=connection.CreateCommand();command.CommandText="CREATE TRIGGER reject_farm BEFORE INSERT ON RealityDeltas BEGIN SELECT RAISE(ABORT,'test failure'); END";await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(()=>world.UseFarmAnimalAsync("crafter","cow","milk"));Assert.Equal(1,FarmQuantity(world,"emptyGlassJar"));Assert.False(entities["cow"].Properties.ContainsKey("milkReadyUtc"));
    }
    [Fact] public async Task FarmMilkNutritionReturnsContainersAndJarHasFiveTimesBenefits()
    {
        var (world,_,_,_)=await GardenWorld();var players=PhotoField<ConcurrentDictionary<string,PlayerState>>(world,"_players");players["crafter"]=players["crafter"] with{Water=0,HealthHearts=0,Stamina=0,MaximumWater=100,MaximumHealthHearts=100,MaximumStamina=100};
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];pack["bottleOfMilk"]=1;pack["jarOfMilk"]=1;var bottle=await world.ConsumeItemAsync("crafter","bottleOfMilk");var jar=await world.ConsumeItemAsync("crafter","jarOfMilk");Assert.Equal(bottle.Water*5,jar.Water-bottle.Water,6);Assert.Equal(bottle.HealthHearts*5,jar.HealthHearts-bottle.HealthHearts,6);Assert.Equal(1,FarmQuantity(world,"emptyGlassBottle"));Assert.Equal(1,FarmQuantity(world,"emptyGlassJar"));
    }
    [Fact] public async Task FarmUrineCraftsNitrateAndReturnsJarForGunpowderRecipe()
    {
        var (world,_,_)=await CreateCraftingTestWorld();var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];pack["jarOfUrine"]=1;pack["recipe:jarUrineToNitrate"]=1;await world.ConsumeItemAsync("crafter","recipe:jarUrineToNitrate");
        var result=await world.CraftItemAsync("crafter",new("misc-table","jarUrineToNitrate"));Assert.Contains(result.PrivateState.HomeItemStorage!.Items,i=>i.ItemType=="potassiumNitrate"&&i.Quantity==5);Assert.Contains(result.PrivateState.HomeItemStorage.Items,i=>i.ItemType=="emptyGlassJar"&&i.Quantity==1);Assert.DoesNotContain(result.PrivateState.Inventory.Items,i=>i.ItemType=="jarOfUrine");
        Assert.Contains(CraftingCatalog.Recipes.Single(r=>r.Id=="gunpowder").Ingredients,i=>i.ItemType=="potassiumNitrate");
    }
}
