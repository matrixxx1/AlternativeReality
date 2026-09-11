using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TeleportRestrictionBlocksDirectAndHomeTravelEvenInGodMode(bool godMode)
    {
        var (world, _, _) = await CreateCraftingTestWorld();
        await world.ExitDungeonAsync("crafter");
        await world.SetGodModeAsync("crafter", godMode);
        var home = world.GetPrivateState("crafter").Base!;
        var before = world.CreateSnapshot().Players.Single(player => player.Id == "crafter");
        await world.UpdatePlayerTestingAsync("crafter", new PlayerTestingSettings(CantTeleport: true));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.TeleportAsync("crafter", new(20, 20, true)));
        Assert.Contains("can't teleport", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.MapFastTravelAsync("crafter", new("home", home.BuildingId)));
        Assert.Equal(before.Position, world.CreateSnapshot().Players.Single(player => player.Id == "crafter").Position);
        await world.UpdatePlayerTestingAsync("crafter", new PlayerTestingSettings(CantTeleport: false));
        await world.TeleportAsync("crafter", new(20, 20, false));
        await world.MapFastTravelAsync("crafter", new("home", home.BuildingId));
    }

    [Fact]
    public async Task CombatTestingRulesProvideUnlimitedAmmoMassiveDamageAndDeathProtection()
    {
        var configuration = new RealityConfiguration("player-testing-combat", "Player Testing Combat", 77,
            new GeographicArea(new GeoCoordinate(45.5, -122.5), 500), PvpEnabled: true);
        var store = new SqliteRealityStore(Path.Combine(_directory, "player-testing-combat.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("attacker", [new ItemStack("rifle", 1), new ItemStack("bullet", 1)]));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var attacker = await world.JoinAsync("attacker", "Attacker");
        var target = await world.JoinAsync("target", "Target");
        await world.UpdatePlayerTestingAsync(attacker.Id, new PlayerTestingSettings(ConsumesAmmo: false, DoesNormalDamage: false));
        await world.UpdatePlayerTestingAsync(target.Id, new PlayerTestingSettings(CanDie: false));
        await world.UpdateItemConfigurationAsync(attacker.Id, new UpdateItemConfigurationRequest("rifle", 7, 200, 0, 0, Accuracy: 1));
        await world.SetEquipmentAsync(attacker.Id, "weapon", "rifle");

        var combat = await world.AttackAsync(attacker.Id, new CombatRequest(target.Id, "rifle"));

        Assert.True(combat.Event.Hit);
        Assert.Equal(700, combat.Event.Damage);
        Assert.False(combat.Event.TargetDied);
        Assert.Equal(1, combat.TargetPlayer!.HealthHearts);
        Assert.Equal(1, combat.Inventory.Items.Single(item => item.ItemType == "bullet").Quantity);
    }

    [Fact]
    public async Task PlayerTestingDefaultsToNormalAndPersistsPerCharacter()
    {
        var (world, store, building) = await CreateCraftingTestWorld();
        await world.SetGodModeAsync("crafter", false);
        var defaults = world.GetPrivateState("crafter").PlayerTesting!;
        Assert.False(defaults.CantTeleport);
        Assert.True(defaults.CanDie);
        Assert.True(defaults.ConsumesAmmo);
        Assert.True(defaults.ConsumesCraftingMaterials);
        Assert.True(defaults.ConsumesAirWhenSwimming);
        Assert.True(defaults.ConsumesStaminaWhenMoving);
        Assert.True(defaults.MustMeetCraftingMaterialRequirements);
        Assert.True(defaults.CanFailWhenCrafting);
        Assert.True(defaults.DoesNormalDamage);
        Assert.True(defaults.GetsNormalMovementSpeed);
        Assert.True(defaults.ConsumesVehicleFuel);
        Assert.True(defaults.ObeysBackpackWeightLimit);

        var testing = defaults with
        {
            CantTeleport = true,
            CanDie = false,
            ConsumesAmmo = false,
            ConsumesCraftingMaterials = false,
            MustMeetCraftingMaterialRequirements = false,
            CanFailWhenCrafting = false,
            DoesNormalDamage = false,
            GetsNormalMovementSpeed = false
        };
        var ordinary = world.CreateSnapshot().Players.Single(player => player.Id == "crafter");
        var normalSpeed = world.ConfiguredSpeedMetersPerSecond(ordinary, ordinary.Terrain);
        Assert.Equal(testing, await world.UpdatePlayerTestingAsync("crafter", testing));
        Assert.Equal(normalSpeed * 5, world.ConfiguredSpeedMetersPerSecond(ordinary, ordinary.Terrain), 8);

        var restarted = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await restarted.InitializeAsync();
        var player = await restarted.JoinAsync("crafter", "Crafter", "crafter-account");
        Assert.False(player.GodMode);
        Assert.Equal(testing, restarted.GetPrivateState("crafter").PlayerTesting);
    }

    [Fact]
    public async Task CraftingTestingRulesAllowFreeGuaranteedBatchesWithoutMaterials()
    {
        var (world, _, _) = await CreateCraftingTestWorld();
        await world.SetGodModeAsync("crafter", false);
        var settings = new PlayerTestingSettings(
            ConsumesCraftingMaterials: false,
            MustMeetCraftingMaterialRequirements: false,
            CanFailWhenCrafting: false);
        await world.UpdatePlayerTestingAsync("crafter", settings);

        var available = Assert.Single(world.RequestCrafting("crafter", "craft-table").Recipes, recipe => recipe.Id == "napalmBottle");
        Assert.Equal(99, available.MaximumCraftable);
        var result = await world.CraftItemAsync("crafter", new("craft-table", "napalmBottle", 5));
        var items = result.PrivateState.HomeItemStorage!.Items.ToDictionary(item => item.ItemType, item => item.Quantity);

        Assert.Equal(5, items["napalmBottle"]);
        Assert.Equal(3, items["emberGel"]);
        Assert.Equal(3, items["bindingResin"]);
        Assert.Equal(3, items["emptyGlassBottle"]);
        Assert.False(result.Crafting.TableDestroyed);
    }
}
