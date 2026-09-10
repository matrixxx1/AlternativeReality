using System.Collections.Concurrent;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task PermanentUpgradeChargesOnceAppliesImmediatelyAndPersistsWithoutInventoryItems()
    {
        var (world,store,building)=await CreateCraftingTestWorld();
        var p=world.CreateSnapshot().Players.Single(p=>p.Id=="crafter");
        p=p with{GodMode=false,WalletCents=100000};ScubaPlayer(world,p);await store.SaveCharacterAsync(world.Configuration.Id,p);
        var before=world.GetRecipeBook(p.Id).Single(r=>r.Id=="water");
        var bought=await world.BuyHomeUpgradeAsync(p.Id,"spiceRack");
        Assert.Equal(92500,bought.WalletCents);
        var after=world.GetRecipeBook(p.Id).Single(r=>r.Id=="water");Assert.Equal(before.SuccessChance+.03,after.SuccessChance,6);
        Assert.Equal(.05,after.Bonuses!.Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BuyHomeUpgradeAsync(p.Id,"spiceRack"));
        Assert.Equal(92500,world.CreateSnapshot().Players.Single(p=>p.Id=="crafter").WalletCents);
        Assert.DoesNotContain(world.GetPrivateState(p.Id).Inventory.Items,i=>i.ItemType=="spiceRack");
        Assert.DoesNotContain(world.GetPrivateState(p.Id).HomeItemStorage!.Items,i=>i.ItemType=="spiceRack");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.DropInventoryItemAsync(p.Id,new("spiceRack",1)));
        var restarted=new RealityWorld(world.Configuration,new DeterministicWorldGenerator(new FixedGeographicProvider(building)),new FixedWeatherProvider(),store);
        await restarted.InitializeAsync();await restarted.JoinAsync(p.Id,p.Name,"crafter-account");
        Assert.Contains("spiceRack",restarted.GetPrivateState(p.Id).HomeWorkshop!.Progress.Installed);
        Assert.Equal(after.SuccessChance,restarted.GetRecipeBook(p.Id).Single(r=>r.Id=="water").SuccessChance);
    }

    [Fact]
    public async Task PurifierStopsAfterFiftyUsesConsumesOneReplacementAndPersistsItsCount()
    {
        var (world,store,_) = await CreateCraftingTestWorld();
        await world.BuyHomeUpgradeAsync("crafter","waterPurifier");
        for(var i=0;i<50;i++)await world.UseWaterPurifierAsync("crafter",false);
        Assert.Equal(0,world.GetPrivateState("crafter").HomeWorkshop!.Progress.FilterUsesRemaining);
        Assert.Equal(50,world.GetPrivateState("crafter").HomeItemStorage!.Items.Single(i=>i.ItemType=="purifiedWater").Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseWaterPurifierAsync("crafter",false));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseWaterPurifierAsync("crafter",true));
        var inventory=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];inventory["recipe:waterFilter"]=1;
        await world.ConsumeItemAsync("crafter","recipe:waterFilter");
        var supplies=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];supplies["paper"]=1;supplies["cloth"]=1;
        await world.CraftItemAsync("crafter",new("misc-table","waterFilter"));
        await world.UseWaterPurifierAsync("crafter",true);
        Assert.DoesNotContain(world.GetPrivateState("crafter").HomeItemStorage!.Items,i=>i.ItemType=="waterFilter");
        await world.UseWaterPurifierAsync("crafter",false);
        Assert.Equal(49,(await store.LoadHomeWorkshopAsync(world.Configuration.Id,"crafter-account")).FilterUsesRemaining);
        Assert.Equal(51,world.GetPrivateState("crafter").HomeItemStorage!.Items.Single(i=>i.ItemType=="purifiedWater").Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseWaterPurifierAsync("crafter",true));
        Assert.Contains("waterFilter",CraftingCatalog.LitterItems);
    }

    [Fact]
    public async Task FoodAndAmmoBonusesAddFreeOutputsAndVehicleUpgradesSaveMaterialsOnlyOnSuccess()
    {
        var (world,_,_)=await CreateCraftingTestWorld();
        foreach(var id in new[]{"spiceRack","reloadingSet","welder"})await world.BuyHomeUpgradeAsync("crafter",id);
        var inventory=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];
        foreach(var id in new[]{"bullet","skateboard"}){inventory["recipe:"+id]=1;await world.ConsumeItemAsync("crafter","recipe:"+id);}
        var home=world.GetPrivateState("crafter").Dungeon!;
        foreach(var (recipeId,station) in new[]{("water","stove"),("bullet","weaponsBench"),("skateboard","garageWorkbench")})
        {
            var recipe=CraftingCatalog.Recipes.Single(r=>r.Id==recipeId);
            var supplies=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];
            foreach(var ingredient in recipe.Ingredients)supplies[ingredient.ItemType]=ingredient.Quantity;
            var before=supplies.GetValueOrDefault(recipe.OutputItemType);
            var furniture=home.Furnishings!.Single(i=>i.Properties["objectType"]==station);
            var result=await world.CraftItemAsync("crafter",new(furniture.Id,recipeId));
            var stock=result.PrivateState.HomeItemStorage!.Items;
            Assert.Equal(before+recipe.OutputQuantity+(recipeId=="skateboard"?0:1),stock.Single(i=>i.ItemType==recipe.OutputItemType).Quantity);
            if(recipeId=="skateboard")foreach(var ingredient in recipe.Ingredients)Assert.True(stock.Single(i=>i.ItemType==ingredient.ItemType).Quantity>0);
        }
        var weapon=world.GetRecipeBook("crafter").Single(r=>r.Id=="napalmBottle");Assert.Equal(0,weapon.Bonuses!.Quantity);
        var bench=home.Furnishings!.Single(i=>i.Properties["objectType"]=="garageWorkbench");
        var remaining=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];
        foreach(var ingredient in CraftingCatalog.Recipes.Single(r=>r.Id=="skateboard").Ingredients)remaining[ingredient.ItemType]=ingredient.Quantity;
        world.ProgressionRoll=()=>1;
        var failed=await world.CraftItemAsync("crafter",new(bench.Id,"skateboard"));
        Assert.True(failed.Crafting.TableDestroyed);Assert.DoesNotContain(failed.PrivateState.HomeItemStorage!.Items,i=>i.ItemType is "wood" or "plastic" or "metal" or "rubber");
    }

    [Fact]
    public async Task IngredientQualityTransfersBothWaysPersistsAndChangesDisplayedAndActualCraftChance()
    {
        var (world,store,building)=await CreateCraftingTestWorld();
        var home=world.GetPrivateState("crafter").Dungeon!;var chest=home.Furnishings!.Single(i=>i.Properties["objectType"]=="storageChest");
        var inventory=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")["crafter"];
        var quality=PhotoField<ConcurrentDictionary<(string Player,string Item),string>>(world,"_weaponQualities");
        inventory["cloth"]=2;inventory["dirtyWater"]=6;quality[("crafter","cloth")]="Crude";quality[("crafter","dirtyWater")]="Crude";
        foreach(var (id,quantity) in new[]{("cloth",2),("dirtyWater",6)})await world.TransferHomeItemAsync("crafter",new(chest.Id,id,quantity,true));
        var crude=world.GetRecipeBook("crafter").Single(r=>r.Id=="water");Assert.Equal(.35,crude.SuccessChance,6);Assert.Equal(-.15,crude.Bonuses!.IngredientQuality,6);
        Assert.All(world.GetPrivateState("crafter").HomeItemStorage!.Items.Where(i=>i.ItemType is "cloth" or "dirtyWater"),i=>Assert.Equal("Crude",i.Quality));
        await world.TransferHomeItemAsync("crafter",new(chest.Id,"cloth",1,false));
        Assert.Equal("Crude",world.GetPrivateState("crafter").Inventory.Items.Single(i=>i.ItemType=="cloth").Quality);
        var restarted=new RealityWorld(world.Configuration,new DeterministicWorldGenerator(new FixedGeographicProvider(building)),new FixedWeatherProvider(),store);
        await restarted.InitializeAsync();await restarted.JoinAsync("crafter","Crafter","crafter-account");
        Assert.Equal(crude.SuccessChance,restarted.GetRecipeBook("crafter").Single(r=>r.Id=="water").SuccessChance);
        quality[("home-items:crafting-tests:crafter-account","cloth")]="Superior";
        quality[("home-items:crafting-tests:crafter-account","dirtyWater")]="Superior";
        var premium=world.GetRecipeBook("crafter").Single(r=>r.Id=="water");Assert.Equal(.60,premium.SuccessChance,6);
        var stove=home.Furnishings!.Single(i=>i.Properties["objectType"]=="stove");
        Assert.Equal(premium.SuccessChance,world.RequestCrafting("crafter",stove.Id).Recipes.Single(r=>r.Id=="water").SuccessChance);
        world.ProgressionRoll=()=>.55;
        Assert.False((await world.CraftItemAsync("crafter",new(stove.Id,"water"))).Crafting.TableDestroyed);
        Assert.Equal(.1,RealityWorld.IngredientQualityModifier("Premium"));
    }

    [Fact]
    public async Task HomeUpgradePersistenceFailureDoesNotChargeOrInstallAndVisitorsCannotBuy()
    {
        var (world,_,_) = await CreateCraftingTestWorld();
        await using var connection=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_directory,"crafting.db")};Pooling=False");await connection.OpenAsync();
        var command=connection.CreateCommand();command.CommandText="CREATE TRIGGER reject_home_upgrade BEFORE INSERT ON HomeWorkshops BEGIN SELECT RAISE(ABORT,'test failure'); END";await command.ExecuteNonQueryAsync();
        var before=world.CreateSnapshot().Players.Single(p=>p.Id=="crafter").WalletCents;
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(()=>world.BuyHomeUpgradeAsync("crafter","spiceRack"));
        Assert.Empty(world.GetPrivateState("crafter").HomeWorkshop!.Progress.Installed);Assert.Equal(before,world.CreateSnapshot().Players.Single(p=>p.Id=="crafter").WalletCents);
        await world.JoinAsync("visitor","Visitor");await world.SetGodModeAsync("visitor",true);
        var door=world.CreateSnapshot().BaseEntities.Single(i=>i.Kind==EntityKind.Door);await world.TeleportAsync("visitor",new(door.Position.X,door.Position.Y,true));await world.EnterDungeonAsync("visitor",door.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.BuyHomeUpgradeAsync("visitor","spiceRack"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.UseWaterPurifierAsync("visitor",false));
    }
}
