using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task WaterRecipeStartsKnownPersistsAndRecipeBookTracksStudyCopiesAndActualChance()
    {
        var (world, store, _) = await CreateCraftingTestWorld();
        var start = world.GetRecipeBook("crafter").Single(r => r.Id == "water");
        Assert.Equal(1, start.Level); Assert.Equal("Food/Water", start.Category);
        Assert.Equal("stove", start.StationType); Assert.Equal(0, start.AvailableCopies);
        var xp = world.GetCraftingSkill("crafter").Experience;
        var inventory = PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_inventories")["crafter"];
        inventory["recipe:water"] = 2;
        Assert.Equal(2, world.GetRecipeBook("crafter").Single(r => r.Id == "water").AvailableCopies);
        await world.ConsumeItemAsync("crafter", "recipe:water");
        var studied = world.GetRecipeBook("crafter").Single(r => r.Id == "water");
        Assert.Equal(2, studied.Level); Assert.Equal(1, studied.AvailableCopies); Assert.True(studied.SuccessChance > start.SuccessChance);
        var stove = world.GetPrivateState("crafter").Dungeon!.Furnishings!.Single(i => i.Properties["objectType"] == "stove");
        inventory["cloth"]=1;inventory["dirtyWater"]=3;
        Assert.Equal(studied.SuccessChance, world.RequestCrafting("crafter", stove.Id).Recipes.Single(r => r.Id == "water").SuccessChance);
        Assert.Equal(2, (await store.LoadRecipeStudiesAsync(world.Configuration.Id, "crafter")).Single(r => r.RecipeId == "water").Count);
        await world.LeaveAsync("crafter"); await world.JoinAsync("crafter", "Crafter", "crafter-account");
        Assert.Equal(2, world.GetRecipeStudy("crafter", "water")!.Count);
        Assert.Equal(xp + CraftingCatalog.ExperiencePerNewRecipe, world.GetCraftingSkill("crafter").Experience);
        Assert.Equal("Vehicles", world.GetRecipeBook("crafter").Single(r => r.Id == "skateboard").Category);
        Assert.Equal("Ammo", world.GetRecipeBook("crafter").Single(r => r.Id == "bullet").Category);
        Assert.Equal("Weapons", world.GetRecipeBook("crafter").Single(r => r.Id == "rocketLauncher").Category);
    }

    [Fact]
    public async Task StationsEnforceWaterAndVehicleRecipesAndConsumeExactHomeSupplies()
    {
        var (world, _, _) = await CreateCraftingTestWorld();
        var home = world.GetPrivateState("crafter").Dungeon!;
        var stove = home.Furnishings!.Single(i => i.Properties["objectType"] == "stove");
        var bench = home.Furnishings!.Single(i => i.Properties["objectType"] == "garageWorkbench");
        var supplies = PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_homeItemStorage")["crafter-account"];
        supplies["cloth"] = 10; supplies["dirtyWater"] = 9; supplies["paper"] = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "water")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new(bench.Id, "water")));
        Assert.Equal(9, supplies["dirtyWater"]);
        await world.CraftItemAsync("crafter", new(stove.Id, "water", 3));
        var inventory = PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_inventories")["crafter"];
        inventory["recipe:purifiedWater"] = 1; inventory["recipe:skateboard"] = 1;
        await world.ConsumeItemAsync("crafter", "recipe:purifiedWater");
        await world.ConsumeItemAsync("crafter", "recipe:skateboard");
        await world.CraftItemAsync("crafter", new(stove.Id, "purifiedWater"));
        var stock = world.GetPrivateState("crafter").HomeItemStorage!.Items;
        Assert.Equal(1, stock.Single(i => i.ItemType == "purifiedWater").Quantity);
        Assert.Equal(6, stock.Single(i => i.ItemType == "cloth").Quantity);
        Assert.DoesNotContain(stock, i => i.ItemType is "dirtyWater" or "water" or "paper");
        Assert.DoesNotContain(world.RequestCrafting("crafter", bench.Id).Recipes, r => r.Id == "skateboard");
        foreach (var station in new[] { "craft-table", stove.Id })
            await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new(station, "skateboard")));
        supplies = PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_homeItemStorage")["crafter-account"];
        foreach(var ingredient in CraftingCatalog.Recipes.Single(r=>r.Id=="skateboard").Ingredients) supplies[ingredient.ItemType] = ingredient.Quantity;
        await world.CraftItemAsync("crafter", new(bench.Id, "skateboard"));
        Assert.Contains(world.GetPrivateState("crafter").Dungeon!.Garage!.Vehicles!, v => v.ItemType == "skateboard" && v.Quantity == 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.MoveFurnitureAsync("crafter", new(bench.Id, 8, 5)));
    }

    [Fact]
    public async Task GarageIsConnectedWalkableAndStationsMigrateWithoutDuplicates()
    {
        var (world, store, building) = await CreateCraftingTestWorld();
        var home = world.GetPrivateState("crafter").Dungeon!; var garage = home.Garage!;
        Assert.True(garage.Room.X > home.Footprint!.Max(p=>p.X));
        Assert.Equal(28,garage.Room.Width); Assert.Equal(18,garage.Room.Height);
        var safe = typeof(RealityWorld).GetMethod("InteriorPositionIsSafe", BindingFlags.Static|BindingFlags.NonPublic)!;
        var y=(garage.Passage[0].Y+garage.Passage[^1].Y)/2;
        // A continuous path through the doorway must agree with server collision.
        for(var x=garage.Passage[0].X-.65;x<garage.Room.X+1.6;x+=.2)
            Assert.True((bool)safe.Invoke(null,[new WorldPosition(home.Exit.Region,x,y),home])!, $"Garage entrance blocked at {x}, {y}");
        Assert.False((bool)safe.Invoke(null,[new WorldPosition(home.Exit.Region,garage.Room.X+3,garage.Room.Y-.2),home])!);
        Assert.False((bool)safe.Invoke(null,[new WorldPosition(home.Exit.Region,garage.Room.X+garage.Room.Width,garage.Room.Y+5),home])!);
        Assert.True((bool)safe.Invoke(null,[new WorldPosition(home.Exit.Region,garage.Room.X+24,garage.Room.Y+15),home])!);
        var reloaded = new RealityWorld(world.Configuration,new DeterministicWorldGenerator(new FixedGeographicProvider(building)),new FixedWeatherProvider(),store);
        await reloaded.InitializeAsync(); await reloaded.JoinAsync("crafter","Crafter","crafter-account");
        foreach(var type in new[]{"stove","garageWorkbench"}) Assert.Single(reloaded.GetPrivateState("crafter").Dungeon!.Furnishings!,i=>i.Properties["objectType"]==type);
        var actual = world.CreateSnapshot().BaseEntities.Single(i=>i.Id==building.Id);
        Assert.Equal(building.Geometry,actual.Geometry);
    }

    [Theory]
    [InlineData("dirtyWater", .8999, true)]
    [InlineData("dirtyWater", .9, false)]
    [InlineData("water", .0499, true)]
    [InlineData("water", .05, false)]
    [InlineData("purifiedWater", 0, false)]
    public void WaterQualityUsesExactParasiteThresholds(string type,double roll,bool infected)
    {
        var p=new PlayerState("water","Water",new(default,0,0),Water:0);
        var eaten=RealityWorld.EatNutrition(p,NutritionCatalog.Foods[type],DateTimeOffset.UtcNow,roll,.5);
        Assert.Equal(infected,eaten.Survival!.Illnesses!.Any(i=>i.Name=="Parasites"));Assert.Equal(10,eaten.Water);
    }

    [Theory]
    [InlineData(.0999,true)]
    [InlineData(.1,false)]
    public void PurifiedWaterCanRemoveOnlyParasites(double roll,bool cured)
    {
        var now=DateTimeOffset.UtcNow;
        var p=new PlayerState("water","Water",new(default,0,0),Survival:new(0,[new("Parasites",now.AddHours(1)),new("Fever",now.AddHours(1))]));
        var eaten=RealityWorld.EatNutrition(p,NutritionCatalog.Foods["purifiedWater"],now,0,.5,roll);
        Assert.Equal(!cured,eaten.Survival!.Illnesses!.Any(i=>i.Name=="Parasites"));Assert.Contains(eaten.Survival.Illnesses!,i=>i.Name=="Fever");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CollectDirtyWaterRequiresWaterAndBackpackSpaceAndPersists(bool shallow)
    {
        var (world,p,_) = await ScubaFixture(shallow:shallow);
        var inventory=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")[p.Id]; inventory.Clear();
        await world.CollectDirtyWaterAsync(p.Id);
        Assert.Equal(1,world.GetPrivateState(p.Id).Inventory.Items.Single(i=>i.ItemType=="dirtyWater").Quantity);
        var consumed=await world.ConsumeItemAsync(p.Id,"dirtyWater");
        Assert.DoesNotContain(world.GetPrivateState(p.Id).Inventory.Items,i=>i.ItemType=="dirtyWater");
        ScubaPlayer(world,p); inventory=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")[p.Id]; inventory["water"]=10000;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.CollectDirtyWaterAsync(p.Id));
        inventory.Clear(); ScubaPlayer(world,p with{Position=p.Position with{X=p.Position.X+300}});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.CollectDirtyWaterAsync(p.Id));
    }
}
