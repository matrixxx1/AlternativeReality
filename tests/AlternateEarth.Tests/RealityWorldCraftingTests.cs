using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World, SqliteRealityStore Store, CanonicalEntity Building)> CreateCraftingTestWorld(long experience = 5_000)
    {
        var configuration = new RealityConfiguration("crafting-tests", "Crafting Tests", 48,
            new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var building = SizedBuilding("craft-home", configuration.Area.Region, 30, 30, 30, 20);
        var store = new SqliteRealityStore(Path.Combine(_directory, "crafting.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("crafter-account", "Crafter", "hash", "salt", "token", "crafter"), "Crafter");
        await store.SaveHomeFurnitureAsync("crafter-account", configuration.Id, new[]
        {
            new CanonicalEntity("craft-table", EntityKind.PlayerStructure, new WorldPosition(configuration.Area.Region, 5, 5),
                Array.Empty<GeometryPoint>(), new Dictionary<string, string>
                {
                    ["objectType"] = "craftingTable", ["displayName"] = "Crafting table", ["stored"] = "false",
                    ["widthMeters"] = "1.6", ["depthMeters"] = "0.8", ["rotationDegrees"] = "0", ["builtIn"] = "false"
                }, IsBaseEntity: false)
        });
        await store.SaveInventoryAndCraftingProgressAsync(new InventoryState("home-items:crafting-tests:crafter-account", new[]
        {
            new ItemStack("emberGel", 3), new ItemStack("bindingResin", 3), new ItemStack("emptyGlassBottle", 3)
        }), configuration.Id, "crafter", experience);
        await store.SaveLearnedRecipeAsync(configuration.Id, "crafter", "napalmBottle");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        world.ProgressionRoll = () => 0;
        await world.InitializeAsync();
        await world.JoinAsync("crafter", "Crafter", "crafter-account");
        await world.SetGodModeAsync("crafter", true);
        var door = world.CreateSnapshot().BaseEntities.Single(item => item.Kind == EntityKind.Door);
        await world.TeleportAsync("crafter", new(door.Position.X, door.Position.Y, true));
        await world.EnterDungeonAsync("crafter", door.Id);
        return (world, store, building);
    }

    [Fact]
    public async Task CraftingDeductsHomeSuppliesAtomicallyAndPersistsRecipesAndOutputs()
    {
        var (world, store, building) = await CreateCraftingTestWorld();
        var known = Assert.Single(world.RequestCrafting("crafter", "craft-table").Recipes);
        Assert.Equal("napalmBottle", known.Id);
        Assert.Equal(3, known.MaximumCraftable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmJar")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmBottle", 4)));
        Assert.Equal(3, Assert.Single(world.RequestCrafting("crafter", "craft-table").Recipes).MaximumCraftable);

        async Task<bool> CraftTwo()
        {
            try { await world.CraftItemAsync("crafter", new("craft-table", "napalmBottle", 2)); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Single(await Task.WhenAll(CraftTwo(), CraftTwo()), success => success);
        var inventory = world.GetPrivateState("crafter").HomeItemStorage!;
        Assert.Equal(2, inventory.Items.Single(item => item.ItemType == "napalmBottle").Quantity);
        foreach (var ingredient in new[] { "emberGel", "bindingResin", "emptyGlassBottle" })
            Assert.Equal(1, inventory.Items.Single(item => item.ItemType == ingredient).Quantity);
        Assert.DoesNotContain(world.GetPrivateState("crafter").Inventory.Items, item => item.ItemType == "napalmBottle");

        var reloaded = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await reloaded.InitializeAsync();
        await reloaded.JoinAsync("crafter", "Crafter", "crafter-account");
        Assert.Equal(1, Assert.Single(reloaded.RequestCrafting("crafter", "craft-table").Recipes).MaximumCraftable);
        Assert.Equal(2, reloaded.GetPrivateState("crafter").HomeItemStorage!.Items.Single(item => item.ItemType == "napalmBottle").Quantity);
    }

    [Fact]
    public async Task CraftingRejectsVisitorsStoredTablesAndInvalidBatchSizes()
    {
        var (world, _, _) = await CreateCraftingTestWorld();
        var door = world.CreateSnapshot().BaseEntities.Single(item => item.Kind == EntityKind.Door);
        await world.JoinAsync("visitor", "Visitor");
        await world.SetGodModeAsync("visitor", true);
        await world.TeleportAsync("visitor", new(door.Position.X, door.Position.Y, true));
        await world.EnterDungeonAsync("visitor", door.Id);
        Assert.Contains("Visitors", Assert.Throws<InvalidOperationException>(() => world.RequestCrafting("visitor", "craft-table")).Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("visitor", new("craft-table", "napalmBottle")));
        foreach (var quantity in new[] { 0, -1, 100, int.MaxValue })
            await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmBottle", quantity)));
        await world.StoreFurnitureAsync("crafter", new("craft-table"));
        Assert.Throws<InvalidOperationException>(() => world.RequestCrafting("crafter", "craft-table"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmBottle")));
        Assert.Equal(3, world.GetPrivateState("crafter").HomeItemStorage!.Items.Single(item => item.ItemType == "emberGel").Quantity);
    }

    [Fact]
    public async Task DungeonRecipeCollectionLearnsPermanentlyWithoutTakingBackpackSpace()
    {
        var configuration = new RealityConfiguration("recipe-loot", "Recipe Loot", 23, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var building = SizedBuilding("recipe-dungeon", configuration.Area.Region, 30, 30, 30, 20) with
        { Properties = new Dictionary<string, string> { ["questItem"] = "true", ["building"] = "yes" } };
        var store = new SqliteRealityStore(Path.Combine(_directory, "recipe-loot.db"));
        await store.InitializeAsync(configuration);
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        await world.JoinAsync("collector", "Collector");
        await world.SetGodModeAsync("collector", true);
        var door = world.CreateSnapshot().BaseEntities.Single(item => item.Kind == EntityKind.Door);
        await world.TeleportAsync("collector", new(door.Position.X, door.Position.Y, true));
        var entered = await world.EnterDungeonAsync("collector", door.Id);
        var chest = entered.Dungeon.Chests.First();
        // Chest centers can be beside a wall; reconnect at a safe adjacent tile.
        foreach (var (dx, dy) in new[] { (1d, 0d), (-1d, 0d), (0d, 1d), (0d, -1d), (2d, 2d), (-2d, -2d) })
        {
            await store.SaveCharacterAsync(configuration.Id, entered.Player with { Position = chest.Position with { X = chest.Position.X + dx, Y = chest.Position.Y + dy }, Version = entered.Player.Version + 1 });
            var adjacent = await world.JoinAsync("collector", "Collector");
            if (adjacent.Position.Distance2D(chest.Position) < 4) break;
        }
        var opened = await world.OpenChestAsync("collector", chest.Id);
        var book = Assert.Single(opened.Contents.Items, item => item.ItemType.StartsWith("recipe:"));
        var recipeId = book.ItemType[7..];
        var taken = await world.TakeChestItemsAsync("collector", new(chest.Id, new[] { new PurchaseLine(book.ItemType, 1) }));
        Assert.DoesNotContain(taken.Inventory.Items, item => item.ItemType.StartsWith("recipe:"));
        Assert.Contains(recipeId, world.GetPrivateState("collector").LearnedRecipes!);
        Assert.Equal(25, world.GetCraftingSkill("collector").Experience);
        Assert.Contains(recipeId, await store.LoadLearnedRecipesAsync(configuration.Id, "collector"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.TakeChestItemsAsync("collector", new(chest.Id, new[] { new PurchaseLine(book.ItemType, 1) })));
        var reloaded = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await reloaded.InitializeAsync();
        await reloaded.JoinAsync("collector", "Collector");
        Assert.Contains(recipeId, reloaded.GetPrivateState("collector").LearnedRecipes!);
        Assert.Equal(25, reloaded.GetCraftingSkill("collector").Experience);
        // A rediscovered recipe cannot award the learning bonus again, including after reconnect.
        Assert.False(await store.LearnRecipeWithExperienceAsync(configuration.Id, "collector", recipeId, 50));
        Assert.Equal(25, await store.LoadCraftingExperienceAsync(configuration.Id, "collector"));
        Assert.DoesNotContain(world.CreateChestContents("outdoor:chest:0").Items, item => item.ItemType.StartsWith("recipe:"));
    }

    [Fact]
    public async Task CraftingLevelsAreEnforcedAndBooksAdvanceOneLevelWithoutLosingProgress()
    {
        var (world, store, building) = await CreateCraftingTestWorld(99);
        Assert.Equal(1, world.GetCraftingSkill("crafter").Level);
        Assert.Equal(0, Assert.Single(world.RequestCrafting("crafter", "craft-table").Recipes).MaximumCraftable);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmBottle")));
        Assert.Contains("level 25", error.Message);
        Assert.Equal(99, await store.LoadCraftingExperienceAsync(world.Configuration.Id, "crafter"));
        await store.SaveInventoryAsync(new InventoryState("crafter", new[] { new ItemStack("craftingSkillBook", 2) }));
        await world.JoinAsync("crafter", "Crafter", "crafter-account");
        await world.ConsumeItemAsync("crafter", "craftingSkillBook");
        var skill = world.GetCraftingSkill("crafter");
        Assert.Equal(2, skill.Level);
        Assert.Equal(199, skill.Experience);
        Assert.Equal(99, skill.EarnedTowardNextLevel);
        Assert.True(skill.RequiredForNextLevel > 100);
        await world.ConsumeItemAsync("crafter", "craftingSkillBook");
        Assert.Equal(3, world.GetCraftingSkill("crafter").Level);
        Assert.Equal(99, world.GetCraftingSkill("crafter").EarnedTowardNextLevel);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ConsumeItemAsync("crafter", "craftingSkillBook"));
        var reloaded = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await reloaded.InitializeAsync();
        await reloaded.JoinAsync("crafter", "Crafter", "crafter-account");
        Assert.Equal(world.GetCraftingSkill("crafter"), reloaded.GetCraftingSkill("crafter"));
        Assert.DoesNotContain(reloaded.GetPrivateState("crafter").Inventory.Items, item => item.ItemType == "craftingSkillBook");
    }

    [Fact]
    public async Task CraftingExperienceGrowsPerBatchAndExponentialCostsSupportLevelFiveThousand()
    {
        var (world, store, building) = await CreateCraftingTestWorld();
        var before = world.GetCraftingSkill("crafter").Experience;
        await world.CraftItemAsync("crafter", new("craft-table", "napalmBottle", 2));
        Assert.Equal(before + 2, world.GetCraftingSkill("crafter").Experience);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmBottle", 2)));
        Assert.Equal(before + 2, await store.LoadCraftingExperienceAsync(world.Configuration.Id, "crafter"));
        Assert.True(CraftingCatalog.ExperienceForLevel(200) - CraftingCatalog.ExperienceForLevel(100) >
            CraftingCatalog.ExperienceForLevel(100) - CraftingCatalog.ExperienceForLevel(1));
        long highExperience = Enumerable.Range(1, 4_999).Sum(CraftingCatalog.ExperienceForLevel);
        var carried = new InventoryState("crafter", new[] { new ItemStack("craftingSkillBook", 1) });
        await store.SaveInventoryAndCraftingProgressAsync(carried, world.Configuration.Id, "crafter", highExperience);
        await world.JoinAsync("crafter", "Crafter", "crafter-account");
        Assert.Equal(5_000, world.GetCraftingSkill("crafter").Level);
        await world.ConsumeItemAsync("crafter", "craftingSkillBook");
        Assert.Equal(5_001, world.GetCraftingSkill("crafter").Level);
    }

    [Fact]
    public async Task CraftingInventoryAndExperienceRollBackTogetherWhenPersistenceFails()
    {
        var (world, _, _) = await CreateCraftingTestWorld();
        var before = world.GetCraftingSkill("crafter");
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_directory, "crafting.db")};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER reject_test_progress BEFORE UPDATE ON CraftingProgress BEGIN SELECT RAISE(ABORT, 'test write failure'); END";
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmBottle")));
        Assert.Equal(before, world.GetCraftingSkill("crafter"));
        Assert.Equal(3, world.GetPrivateState("crafter").HomeItemStorage!.Items.Single(item => item.ItemType == "emberGel").Quantity);
        command.CommandText = "SELECT Quantity FROM Inventories WHERE OwnerId='home-items:crafting-tests:crafter-account' AND ItemType='emberGel'";
        Assert.Equal(3L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RecipeCatalogHasRequiredMilestonesAndRareDiscoverableResources()
    {
        var (world, pilot, _) = await CreateProbulatorTestWorld();
        var recipes = CraftingCatalog.Recipes.ToDictionary(item => item.Id);
        Assert.Equal(41, recipes.Count);
        foreach (var (id, level) in new[] { ("molotovCocktail", 1), ("skateboard", 25), ("rocketLauncher", 50), ("motorcycle", 100), ("ufo", 5_000) })
            Assert.Equal(level, recipes[id].RequiredLevel);
        Assert.Contains(recipes["ufo"].Ingredients, item => item.ItemType == "kryptonite");
        var config = world.GetPrivateState(pilot.Id).ServerConfiguration!.Items.ToDictionary(item => item.ItemType);
        Assert.All(recipes.Values, recipe =>
        {
            Assert.True(config.ContainsKey(recipe.OutputItemType));
            Assert.All(recipe.Ingredients, ingredient => Assert.True(config.ContainsKey(ingredient.ItemType)));
        });
        Assert.False(config["kryptonite"].ForSale);
        Assert.False(config["craftingSkillBook"].ForSale);
        var drops = Enumerable.Range(0, 10_000).SelectMany(index => world.CreateChestContents($"rare-test:{index}:chest:0", true).Items).ToArray();
        Assert.InRange(drops.Count(item => item.ItemType == "craftingSkillBook"), 30, 200);
        Assert.InRange(drops.Count(item => item.ItemType == "kryptonite"), 3, 60);
        Assert.All(CraftingCatalog.LitterItems, id => Assert.True(config.ContainsKey(id)));
    }

    [Fact]
    public async Task MerchantsKeepSpecialtiesAndGeneralStoresIncludeChemicals()
    {
        var (world, pilot, _) = await CreateProbulatorTestWorld();
        var merchant = world.PlaceTestCharacter(pilot.Id, new("npc", 0, 0)).Actor!;
        var gas = world.BaseMerchantOffers(merchant with { MerchantCategory = "gas" });
        foreach (var id in new[] { "gallonOfGas", "food", "water" }) Assert.Contains(gas, item => item.ItemType == id);
        Assert.All(gas, item => Assert.Contains(item.ItemType, new[] { "gallonOfGas", "craftingGas", "food", "water", "energyDrink" }));
        var furniture = world.BaseMerchantOffers(merchant with { MerchantCategory = "furniture" });
        Assert.All(furniture, item => Assert.StartsWith("furniture:", item.ItemType));
        Assert.Contains(furniture, item => item.Properties!.GetValueOrDefault("furnitureType") == "craftingTable");
        var hardware = world.BaseMerchantOffers(merchant with { MerchantCategory = "hardware" });
        Assert.Contains(hardware, item => item.Properties!.GetValueOrDefault("furnitureType") == "craftingTable");
        Assert.DoesNotContain(hardware, item => item.ItemType is "rifle" or "rocketLauncher" or "food");
        var general = world.BaseMerchantOffers(merchant with { MerchantCategory = "general" });
        Assert.InRange(general.Length, 3, 10);
        Assert.True(general.Count(item => CraftingCatalog.ChemicalItems.Contains(item.ItemType)) >= 2);
    }

    [Theory]
    [InlineData("chloramineGasBottle", 10, 1)]
    [InlineData("chloramineGasJar", 20, 1)]
    [InlineData("chlorineGasBottle", 10, 1)]
    [InlineData("chlorineGasJar", 20, 1)]
    [InlineData("peraceticAcidGasBottle", 10, .5)]
    [InlineData("peraceticAcidGasJar", 20, .5)]
    [InlineData("napalmBottle", 40, 4)]
    [InlineData("napalmJar", 80, 8)]
    public async Task ThrownHazardsDamageEveryoneOncePerSecondAndExpire(string weapon, int duration, double damage)
    {
        var clock = new ProbulatorTestClock();
        var (world, pilot, _) = await CreateProbulatorTestWorld(pvp: false, clock: clock);
        await world.SetTravelModeAsync(pilot.Id, TravelMode.Walk);
        var victim = world.PlaceTestCharacter(pilot.Id, new("player", 1, 0)).Player!;
        var npc = world.PlaceTestCharacter(pilot.Id, new("npc", 1, 0)).Actor!;
        var animal = world.PlaceTestCharacter(pilot.Id, new("animal", 1, 0)).Actor!;
        var outside = world.PlaceTestCharacter(pilot.Id, new("player", 12, 0)).Player!;
        await world.SetEquipmentAsync(pilot.Id, "weapon", weapon);
        await world.ThrowHazardAsync(pilot.Id, new(0, 0));
        var zone = Assert.Single(world.GetAreaHazards());
        Assert.Equal(duration, (zone.EndsAtUtc - zone.StartedAtUtc).TotalSeconds);
        clock.Advance(1);
        var tick = await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(1));
        foreach (var id in new[] { pilot.Id, victim.Id, npc.Id, animal.Id })
            Assert.Equal(damage, Assert.Single(tick.Combat, hit => hit.Weapon == "areaHazard" && hit.TargetId == id).Damage);
        Assert.DoesNotContain(tick.Combat, hit => hit.Weapon == "areaHazard" && hit.TargetId == outside.Id);
        Assert.DoesNotContain((await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5))).Combat, hit => hit.Weapon == "areaHazard");
        clock.Advance(duration - 2);
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(1));
        Assert.Single(world.GetAreaHazards());
        clock.Advance(1);
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(1));
        Assert.Empty(world.GetAreaHazards());
    }

    [Theory]
    [InlineData("chloroformGasBottle", 10)]
    [InlineData("chloroformGasJar", 20)]
    public async Task SleepCloudStopsMovementAndAttacksForTenSecondsWithoutRepeatedSleep(string weapon, int duration)
    {
        var clock = new ProbulatorTestClock();
        var (world, pilot, _) = await CreateProbulatorTestWorld(clock: clock);
        await world.SetTravelModeAsync(pilot.Id, TravelMode.Walk);
        var npc = world.PlaceTestCharacter(pilot.Id, new("npc", 1, 0)).Actor!;
        await world.SetEquipmentAsync(pilot.Id, "weapon", weapon);
        await world.ThrowHazardAsync(pilot.Id, new(0, 0));
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.True(world.IsGasAsleep(pilot.Id));
        Assert.True(world.IsGasAsleep(npc.Id));
        var before = world.CreateSnapshot().Players.Single(item => item.Id == pilot.Id);
        var move = await world.MoveAsync(pilot.Id, new(1, 0, 1));
        Assert.Equal(before.Position, move!.Player.Position);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AttackAsync(pilot.Id, new(npc.Id, "fist")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ThrowHazardAsync(pilot.Id, new(0, 0)));
        clock.Advance(9);
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(1));
        Assert.True(world.IsGasAsleep(pilot.Id));
        clock.Advance(1);
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(1));
        Assert.False(world.IsGasAsleep(pilot.Id));
        Assert.Null(world.CreateSnapshot().Players.Single(item => item.Id == pilot.Id).AsleepUntilUtc);
        Assert.False(world.IsGasAsleep(npc.Id));
        if (duration == 20) Assert.Single(world.GetAreaHazards());
        else Assert.Empty(world.GetAreaHazards());
        Assert.NotEqual(before.Position, (await world.MoveAsync(pilot.Id, new(1, 0, 2)))!.Player.Position);
    }

    [Fact]
    public async Task ThrowingConsumesOneOwnedBottleAndInvalidDestinationsConsumeNothing()
    {
        var (world, pilot, store) = await CreateProbulatorTestWorld();
        await store.SaveInventoryAsync(new InventoryState("thrower", new[] { new ItemStack("napalmBottle", 1) }));
        await world.JoinAsync("thrower", "Thrower");
        await world.SetGodModeAsync("thrower", true);
        await world.TeleportAsync("thrower", new(0, 0, true));
        await world.SetGodModeAsync("thrower", false);
        Assert.Equal("napalmBottle", (await world.SetEquipmentAsync("thrower", "weapon", "NAPALMBOTTLE")).EquippedWeapon);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ThrowHazardAsync("thrower", new(100, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ThrowHazardAsync("thrower", new(double.NaN, 0)));
        Assert.Equal(1, world.GetPrivateState("thrower").Inventory.Items.Single(item => item.ItemType == "napalmBottle").Quantity);
        await world.ThrowHazardAsync("thrower", new(10, 0));
        Assert.DoesNotContain(world.GetPrivateState("thrower").Inventory.Items, item => item.ItemType == "napalmBottle");
        Assert.DoesNotContain((await store.LoadInventoryAsync("thrower")).Items, item => item.ItemType == "napalmBottle");
        Assert.Single(world.CreateSnapshot().AreaHazards!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ThrowHazardAsync("thrower", new(10, 0)));
    }
}
