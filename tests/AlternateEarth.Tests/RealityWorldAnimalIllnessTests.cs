using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;
using System.Collections.Concurrent;
namespace AlternateEarth.Tests;
public sealed partial class RealityWorldTests
{
    [Theory]
    [InlineData("rawMoose","Mad Moose Flu","Charge!!!")]
    [InlineData("rawCow","Mad cow disease","Moo!")]
    [InlineData("rawPig","Swine flu","Oink!")]
    [InlineData("rawChicken","Avian flu","Kakaw!")]
    [InlineData("rawBird","Avian flu","Kakaw!")]
    [InlineData("rawCrow","Avian flu","Kakaw!")]
    [InlineData("rawSeagull","Avian flu","Kakaw!")]
    [InlineData("rawGoose","Avian flu","Kakaw!")]
    public async Task AnimalDiseaseChanceTimingAndAntibiotics(string meat,string disease,string call)
    {
        var (world,p,clock)=await ScubaFixture();world.ProgressionRoll=()=>0;var now=clock.GetUtcNow();
        Assert.Empty(RealityWorld.ApplyMeatDisease(p,meat,now,.3).Survival?.Illnesses??[]);
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")[p.Id];pack[meat]=1;pack["antibiotics"]=1;
        var eaten=await world.ConsumeItemAsync(p.Id,meat);var infection=Assert.Single(eaten.Survival!.Illnesses!,i=>i.Name==disease);Assert.Equal(now.AddMinutes(15),infection.EndsAtUtc);Assert.Contains(eaten.Survival.Illnesses!,i=>i.Name=="Parasites");
        var changed=new List<PlayerState>();clock.Advance(59);await world.AdvanceAnimalIllnessesAsync(clock.GetUtcNow(),changed,default);Assert.Empty(changed);clock.Advance(1);await world.AdvanceAnimalIllnessesAsync(clock.GetUtcNow(),changed,default);Assert.Contains(world.TakeQuestDialogue(),m=>m.Message==call);Assert.True(RealityWorld.HasRecentAnimalCall(ScubaPlayer(world),clock.GetUtcNow()));
        await world.AdvanceAnimalIllnessesAsync(clock.GetUtcNow(),changed,default);Assert.Empty(world.TakeQuestDialogue());
        var cured=await world.ConsumeItemAsync(p.Id,"antibiotics");Assert.Empty(cured.Survival!.Illnesses!);Assert.False(RealityWorld.HasRecentAnimalCall(cured,clock.GetUtcNow()));clock.Advance(60);await world.AdvanceAnimalIllnessesAsync(clock.GetUtcNow(),changed,default);Assert.Empty(world.TakeQuestDialogue());
        var saved=await PhotoField<SqliteRealityStore>(world,"_store").LoadCharacterAsync(world.Configuration.Id,p.Id);Assert.Empty(saved!.Survival!.Illnesses!);
    }
    [Fact] public async Task BasicCookingNeedsOnlyRawMeatAndNoLearnedRecipe()
    {
        var (world,_,_)=await CreateCraftingTestWorld(0);var stove=world.GetPrivateState("crafter").Dungeon!.Furnishings!.Single(f=>f.Properties["objectType"]=="stove");var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];pack["rawMoose"]=1;world.ProgressionRoll=()=>1;
        var recipe=world.RequestCrafting("crafter",stove.Id).Recipes.Single(r=>r.Id=="cookmoose");Assert.True(recipe.Learned);Assert.Equal(1,recipe.SuccessChance);Assert.Single(recipe.Ingredients);
        var made=await world.CraftItemAsync("crafter",new(stove.Id,"cookmoose"));Assert.Contains(made.PrivateState.HomeItemStorage!.Items,i=>i.ItemType=="cookedmoose"&&i.Quantity==1);Assert.DoesNotContain(made.PrivateState.Inventory.Items,i=>i.ItemType=="rawMoose");
    }
    [Fact] public async Task CookingRawCowHasTenPercentChanceToProduceLeatherInstead()
    {
        var (world,_,_)=await CreateCraftingTestWorld(0);var stove=world.GetPrivateState("crafter").Dungeon!.Furnishings!.Single(f=>f.Properties["objectType"]=="stove");var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];pack["rawCow"]=1;world.ProgressionRoll=()=>0;
        var made=await world.CraftItemAsync("crafter",new(stove.Id,"cookcow"));Assert.Contains(made.PrivateState.HomeItemStorage!.Items,i=>i.ItemType=="leather"&&i.Quantity==1);Assert.DoesNotContain(made.PrivateState.HomeItemStorage.Items,i=>i.ItemType=="cookedcow");Assert.Contains("produced leather instead",made.Message);
    }
    [Fact] public void MeatNutritionMatchesBasicCookedAndPreparedDishesAtLeastDoubleIt()
    {
        foreach(var recipe in NutritionCatalog.Recipes.Where(NutritionCatalog.IsBasicCook))
        {
            var raw=NutritionCatalog.Foods[recipe.Ingredients[0].ItemType];var cooked=NutritionCatalog.Foods[recipe.OutputItemType];Assert.True(raw.Raw);Assert.False(cooked.Raw);Assert.Equal(raw.Hunger,cooked.Hunger);Assert.Equal(raw.Health,cooked.Health);Assert.Equal(raw.Stamina,cooked.Stamina);Assert.Null(cooked.Bonuses);
            Assert.Empty(RealityWorld.EatNutrition(new("p","P",new(default,0,0)),cooked,DateTimeOffset.UtcNow,0,0).Survival!.Illnesses!);
        }
        foreach(var recipe in NutritionCatalog.Recipes.Where(r=>!NutritionCatalog.IsBasicCook(r)&&r.Ingredients.Any(i=>i.ItemType.StartsWith("raw")||i.ItemType=="fish")))
        {
            var dish=NutritionCatalog.Foods[recipe.OutputItemType];Assert.False(dish.Raw);foreach(var meat in recipe.Ingredients.Where(i=>i.ItemType.StartsWith("raw")||i.ItemType=="fish")){var basic=NutritionCatalog.Foods[meat.ItemType];Assert.True(dish.Health>=basic.Health*2);Assert.True(dish.Stamina>=basic.Stamina*2);Assert.True(dish.Hunger>=basic.Hunger*2);}
        }
    }
    private static void MooseGrass(RealityWorld world,IReadOnlyList<CanonicalEntity>? obstacles=null)=>typeof(RealityWorld).GetField("_navigation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(world,new WorldNavigation(new(-1000,-1000,1000,1000),obstacles??[],[]));
    [Fact] public async Task MadMooseChargesThreeHundredFeetAndCollisionDealsExactlyThree()
    {
        var (world,p,clock)=await ScubaFixture();MooseGrass(world);world.ProgressionRoll=()=>0;var ill=RealityWorld.ApplyMeatDisease(p,"rawMoose",clock.GetUtcNow(),0);ScubaPlayer(world,ill);clock.Advance(60);var changed=new List<PlayerState>();var combat=new List<CombatEvent>();await world.AdvanceAnimalIllnessesAsync(clock.GetUtcNow(),changed,default);Assert.Contains(world.TakeQuestDialogue(),m=>m.Message=="Charge!!!");
        var before=ScubaPlayer(world);Assert.False((await world.MoveAsync(p.Id,new(p.Position.X+1,p.Position.Y,1)))!.Moved);
        for(var i=0;i<10;i++)await world.AdvanceMadMooseAsync(clock.GetUtcNow(),TimeSpan.FromSeconds(1),changed,combat,default);
        Assert.Equal(91.44,before.Position.Distance2D(ScubaPlayer(world).Position),5);Assert.Empty(combat);
        var start=ScubaPlayer(world).Position;var obstacle=new CanonicalEntity("wall",EntityKind.Building,start with{X=start.X+2},[new(start.X+1,start.Y-10),new(start.X+3,start.Y-10),new(start.X+3,start.Y+10),new(start.X+1,start.Y+10)],new Dictionary<string,string>());MooseGrass(world,[obstacle]);clock.Advance(60);await world.AdvanceAnimalIllnessesAsync(clock.GetUtcNow(),changed,default);var health=ScubaPlayer(world).HealthHearts;await world.AdvanceMadMooseAsync(clock.GetUtcNow(),TimeSpan.FromSeconds(1),changed,combat,default);Assert.Equal(health-3,ScubaPlayer(world).HealthHearts);Assert.Single(combat);await world.AdvanceMadMooseAsync(clock.GetUtcNow(),TimeSpan.FromSeconds(1),changed,combat,default);Assert.Single(combat);
    }
    [Fact] public async Task MooseSyrupSpawnsAtThreeMinutesNeedsContainerAndOnlyOnePlayerCanCollect()
    {
        var (world,p,clock)=await ScubaFixture();MooseGrass(world);ScubaPlayer(world,RealityWorld.ApplyMeatDisease(p,"rawMoose",clock.GetUtcNow(),0));var changes=new List<PlayerState>();var combat=new List<CombatEvent>();clock.Advance(179);await world.AdvanceMadMooseAsync(clock.GetUtcNow(),TimeSpan.Zero,changes,combat,default);Assert.DoesNotContain(PhotoField<ConcurrentDictionary<string,LootDropState>>(world,"_loot").Values,l=>l.DropKind=="mooseFluSyrup");clock.Advance(1);await world.AdvanceMadMooseAsync(clock.GetUtcNow(),TimeSpan.Zero,changes,combat,default);
        var mound=Assert.Single(PhotoField<ConcurrentDictionary<string,LootDropState>>(world,"_loot").Values,l=>l.DropKind=="mooseFluSyrup");Assert.Equal(clock.GetUtcNow().AddSeconds(30),mound.ExpiresAtUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.TakeLootItemsAsync(p.Id,new(mound.Id,[new("mapleSyrup",1)])));
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")[p.Id];pack.Clear();pack["emptyGlassJar"]=1;pack["emptyGlassBottle"]=1;
        var attempts=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{try{await world.TakeLootItemsAsync(p.Id,new(mound.Id,[new("mapleSyrup",1)]));return true;}catch(InvalidOperationException){return false;}}));Assert.Single(attempts,x=>x);
        Assert.Contains(world.GetPrivateState(p.Id).Inventory.Items,i=>i.ItemType=="jarOfMapleSyrup");Assert.Contains(world.GetPrivateState(p.Id).Inventory.Items,i=>i.ItemType=="emptyGlassBottle");Assert.False(PhotoField<ConcurrentDictionary<string,LootDropState>>(world,"_loot").ContainsKey(mound.Id));
        clock.Advance(180);await world.AdvanceMadMooseAsync(clock.GetUtcNow(),TimeSpan.Zero,changes,combat,default);var expired=Assert.Single(PhotoField<ConcurrentDictionary<string,LootDropState>>(world,"_loot").Values,l=>l.DropKind=="mooseFluSyrup");clock.Advance(30);await Assert.ThrowsAsync<InvalidOperationException>(()=>world.TakeLootItemsAsync(p.Id,new(expired.Id,[new("mapleSyrup",1)])));
    }
    [Fact] public async Task SyrupSlowsAndAttractsEnemiesUntilCollectedOrExpired()
    {
        var (world,p,clock)=await ScubaFixture();MooseGrass(world);var loot=PhotoField<ConcurrentDictionary<string,LootDropState>>(world,"_loot");loot["mound"]=new("mound",p.Position,"outdoor",0,[new("mapleSyrup",1)],clock.GetUtcNow().AddSeconds(30),"mooseFluSyrup");
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var slow=typeof(RealityWorld).GetMethod("SyrupSlow",flags)!;Assert.Equal(.5,(double)slow.Invoke(world,[p.Position,"outdoor"])!);Assert.Equal(1,(double)slow.Invoke(world,[p.Position,"other-room"])!);Assert.True((bool)typeof(RealityWorld).GetMethod("StandingInMapleSyrup",flags)!.Invoke(world,[p])!);
        var actors=PhotoField<ConcurrentDictionary<string,ActorState>>(world,"_actors");actors.Clear();actors["enemy"]=new("enemy",EntityKind.Npc,"resident","Enemy",p.Position with{X=p.Position.X+20},FriendRating:-1);actors["friend"]=new("friend",EntityKind.Npc,"resident","Friend",p.Position with{X=p.Position.X+20},FriendRating:1);
        var moved=new Dictionary<string,ActorState>();typeof(RealityWorld).GetMethod("AttractSyrupEnemies",flags)!.Invoke(world,[clock.GetUtcNow(),TimeSpan.FromSeconds(1),moved]);Assert.Contains("enemy",moved.Keys);Assert.DoesNotContain("friend",moved.Keys);
        clock.Advance(30);Assert.Equal(1,(double)slow.Invoke(world,[p.Position,"outdoor"])!);moved.Clear();typeof(RealityWorld).GetMethod("AttractSyrupEnemies",flags)!.Invoke(world,[clock.GetUtcNow(),TimeSpan.FromSeconds(1),moved]);Assert.Empty(moved);
    }
    [Fact] public async Task FarmAnimalKillsLeaveTheirOwnRawMeatAndStopProduction()
    {
        var (world,_,_,spot)=await GardenWorld();var entities=PhotoField<ConcurrentDictionary<string,CanonicalEntity>>(world,"_baseEntities");var cow=FarmRules.Create("cow",spot,"farm",true);entities[cow.Id]=cow with{Properties=new Dictionary<string,string>(cow.Properties){["healthHearts"]=".01"}};var players=PhotoField<ConcurrentDictionary<string,PlayerState>>(world,"_players");players["crafter"]=players["crafter"] with{EquippedWeapon="rifle"};await world.AttackWorldObjectAsync("crafter",cow.Id);Assert.Equal("dead",entities[cow.Id].Properties["state"]);Assert.Empty(await world.AdvanceFarmsAsync());await world.UseFarmAnimalAsync("crafter",cow.Id,"collect");Assert.Equal(3,FarmQuantity(world,"rawCow"));await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseFarmAnimalAsync("crafter",cow.Id,"milk"));
    }
}
