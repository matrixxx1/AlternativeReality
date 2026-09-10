using System.Collections.Concurrent;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task RecipeCountsCombineBothInventoriesAndOnlyCraftableStationRecipesAppear()
    {
        var (world,store,building)=await CreateCraftingTestWorld();
        var stove=world.GetPrivateState("crafter").Dungeon!.Furnishings!.Single(f=>f.Properties["objectType"]=="stove");
        Assert.Empty(world.RequestCrafting("crafter",stove.Id).Recipes);
        Assert.Equal(0,world.GetRecipeBook("crafter").Single(r=>r.Id=="water").MaximumCraftable);
        var home=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];
        home["cloth"]=1;home["dirtyWater"]=2;pack["cloth"]=2;pack["dirtyWater"]=4;
        var book=world.GetRecipeBook("crafter").Single(r=>r.Id=="water");Assert.Equal(2,book.MaximumCraftable);
        var recipe=Assert.Single(world.RequestCrafting("crafter",stove.Id).Recipes);Assert.Equal(2,recipe.MaximumCraftable);
        Assert.Equal(6,recipe.Ingredients.Single(i=>i.ItemType=="dirtyWater").Available);
        var result=await world.CraftItemAsync("crafter",new(stove.Id,"water",2));
        Assert.Equal("pour",result.Sound);Assert.Empty(result.Crafting.Recipes);
        Assert.Equal(2,result.PrivateState.HomeItemStorage!.Items.Single(i=>i.ItemType=="water").Quantity);
        Assert.DoesNotContain(result.PrivateState.HomeItemStorage.Items,i=>i.ItemType is "cloth" or "dirtyWater");
        Assert.Equal(1,result.PrivateState.Inventory.Items.Single(i=>i.ItemType=="cloth").Quantity);
        Assert.DoesNotContain(result.PrivateState.Inventory.Items,i=>i.ItemType=="dirtyWater");
        var restarted=new RealityWorld(world.Configuration,new DeterministicWorldGenerator(new FixedGeographicProvider(building)),new FixedWeatherProvider(),store);
        await restarted.InitializeAsync();await restarted.JoinAsync("crafter","Crafter","crafter-account");
        Assert.Equal(1,restarted.GetPrivateState("crafter").Inventory.Items.Single(i=>i.ItemType=="cloth").Quantity);
        Assert.DoesNotContain(restarted.GetPrivateState("crafter").Inventory.Items,i=>i.ItemType=="dirtyWater");
        Assert.Equal(2,restarted.GetPrivateState("crafter").HomeItemStorage!.Items.Single(i=>i.ItemType=="water").Quantity);
    }

    [Fact]
    public async Task EachBatchUsesTheQualityOfItsActualHomeThenBackpackIngredients()
    {
        var (world,_,_)=await CreateCraftingTestWorld();
        var stove=world.GetPrivateState("crafter").Dungeon!.Furnishings!.Single(f=>f.Properties["objectType"]=="stove");
        var home=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];
        var qualities=PhotoField<ConcurrentDictionary<(string Player,string Item),string>>(world,"_weaponQualities");
        home["cloth"]=1;home["dirtyWater"]=2;pack["cloth"]=3;pack["dirtyWater"]=7;
        qualities[("home-items:crafting-tests:crafter-account","dirtyWater")]="Superior";
        qualities[("crafter","dirtyWater")]="Crude";qualities[("crafter","cloth")]="Fine";
        Assert.Equal(.5125,world.GetRecipeBook("crafter").Single(r=>r.Id=="water").SuccessChance,6);
        world.ProgressionRoll=()=>.45;
        var result=await world.CraftItemAsync("crafter",new(stove.Id,"water",3));
        Assert.True(result.Crafting.TableDestroyed);Assert.Equal(1,result.PrivateState.HomeItemStorage!.Items.Single(i=>i.ItemType=="water").Quantity);
        // First batch succeeds at .5125, second fails at .4; third batch was never touched.
        Assert.Equal(3,result.PrivateState.Inventory.Items.Single(i=>i.ItemType=="dirtyWater").Quantity);
        Assert.Equal(2,result.PrivateState.Inventory.Items.Single(i=>i.ItemType=="cloth").Quantity);
        Assert.Equal("Crude",result.PrivateState.Inventory.Items.Single(i=>i.ItemType=="dirtyWater").Quality);
    }

    [Fact]
    public async Task FailedDatabaseCommitLeavesBackpackHomeAndCraftingProgressUntouched()
    {
        var (world,store,_)=await CreateCraftingTestWorld();
        var stove=world.GetPrivateState("crafter").Dungeon!.Furnishings!.Single(f=>f.Properties["objectType"]=="stove");
        var home=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];
        home["cloth"]=1;pack["dirtyWater"]=3;
        var before=world.GetPrivateState("crafter");await store.SaveInventoriesAsync([before.Inventory,before.HomeItemStorage!]);
        await using var connection=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_directory,"crafting.db")};Pooling=False");await connection.OpenAsync();
        var command=connection.CreateCommand();command.CommandText="CREATE TRIGGER reject_pack_craft BEFORE DELETE ON Inventories WHEN OLD.OwnerId='crafter' BEGIN SELECT RAISE(ABORT,'test failure'); END";await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(()=>world.CraftItemAsync("crafter",new(stove.Id,"water")));
        Assert.Equal(1,world.GetPrivateState("crafter").HomeItemStorage!.Items.Single(i=>i.ItemType=="cloth").Quantity);
        Assert.Equal(3,world.GetPrivateState("crafter").Inventory.Items.Single(i=>i.ItemType=="dirtyWater").Quantity);
        Assert.Equal(before.CraftingSkill,world.GetPrivateState("crafter").CraftingSkill);
        Assert.DoesNotContain((await store.LoadInventoryAsync(before.HomeItemStorage!.PlayerId)).Items,i=>i.ItemType=="water");
    }
    [Theory]
    [InlineData("water","stove","pour")]
    [InlineData("bullet","weaponsBench","craftMetal")]
    [InlineData("skateboard","garageWorkbench","craftMetal")]
    [InlineData("waterFilter","craftingTable","craft")]
    public async Task EveryStationUsesCombinedSuppliesAndReturnsItsMatchingCraftSound(string recipeId,string stationType,string sound)
    {
        var (world,_,_)=await CreateCraftingTestWorld();
        var pack=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];
        if(recipeId!="water"){pack["recipe:"+recipeId]=1;await world.ConsumeItemAsync("crafter","recipe:"+recipeId);}
        var home=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];
        var recipe=CraftingCatalog.Recipes.Single(r=>r.Id==recipeId);
        foreach(var ingredient in recipe.Ingredients){home[ingredient.ItemType]=ingredient.Quantity/2;pack[ingredient.ItemType]=ingredient.Quantity-home[ingredient.ItemType];}
        var station=world.GetPrivateState("crafter").Dungeon!.Furnishings!.Single(f=>f.Properties["objectType"]==stationType);
        Assert.Equal(1,world.RequestCrafting("crafter",station.Id).Recipes.Single(r=>r.Id==recipeId).MaximumCraftable);
        var result=await world.CraftItemAsync("crafter",new(station.Id,recipeId));Assert.Equal(sound,result.Sound);
        Assert.Equal(recipe.OutputQuantity,result.PrivateState.HomeItemStorage!.Items.Single(i=>i.ItemType==recipe.OutputItemType).Quantity);
        foreach(var ingredient in recipe.Ingredients){Assert.DoesNotContain(result.PrivateState.Inventory.Items,i=>i.ItemType==ingredient.ItemType);Assert.DoesNotContain(result.PrivateState.HomeItemStorage.Items,i=>i.ItemType==ingredient.ItemType);}
    }

}
