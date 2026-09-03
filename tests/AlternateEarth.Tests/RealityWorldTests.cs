using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class RealityWorldTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"alternate-earth-world-tests-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_directory, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task NewAccountStartsOutdoorsThenReconnectsInsidePersistentBase()
    {
        var configuration = new RealityConfiguration("home-test", "Home Test", 17, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var building = new CanonicalEntity("test-building", EntityKind.Building, new WorldPosition(region, 20, 20),
            new GeometryPoint[] { new(15, 15), new(25, 15), new(25, 25), new(15, 25), new(15, 15) },
            new Dictionary<string, string> { ["building"] = "yes" });
        var store = new SqliteRealityStore(Path.Combine(_directory, "world.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("account-1", "Player", "hash", "salt", "token", "character-1"), "Player");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();

        var first = await world.JoinAsync("character-1", "Player", "account-1");
        var privateState = world.GetPrivateState(first.Id);

        Assert.Equal("outdoor", first.LocationId);
        Assert.Null(privateState.Dungeon);
        Assert.NotNull(privateState.Base);
        Assert.False(first.Position.X is >= 15 and <= 25 && first.Position.Y is >= 15 and <= 25);

        world.Leave(first.Id);
        var reconnected = await world.JoinAsync("character-1", "Player", "account-1");
        Assert.StartsWith("home:account-1:", reconnected.LocationId);
        Assert.Equal(world.GetPrivateState(reconnected.Id).Dungeon!.Exit, reconnected.Position);
    }

    [Fact]
    public async Task MotorizedTravelRequiresFuelAndGodModeBypassesConsumption()
    {
        var configuration = new RealityConfiguration("fuel-test", "Fuel Test", 18, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var building = new CanonicalEntity("fuel-building", EntityKind.Building, new WorldPosition(region, 20, 20),
            new GeometryPoint[] { new(15, 15), new(25, 15), new(25, 25), new(15, 25), new(15, 15) },
            new Dictionary<string, string> { ["building"] = "yes" });
        var store = new SqliteRealityStore(Path.Combine(_directory, "fuel.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("rider", new[] { new ItemStack("dirtBike", 1), new ItemStack("gallonOfGas", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("rider", "Rider", "fuel-account");

        var empty = await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.DirtBike));
        Assert.Contains("out of gas", empty.Message);

        await world.SetGodModeAsync(player.Id, true);
        var selected = await world.SetTravelModeAsync(player.Id, TravelMode.DirtBike);
        var godRide = await world.MoveAsync(player.Id, new MoveRequest(1, 0, 1));
        Assert.NotNull(godRide);
        Assert.Equal(selected.DirtBikeGasGallons, godRide.Player.DirtBikeGasGallons);

        var fueled = await world.ConsumeItemAsync(player.Id, "gallonOfGas");
        Assert.Equal(1, fueled.DirtBikeGasGallons);
        await world.SetGodModeAsync(player.Id, false);
        var normalRide = await world.MoveAsync(player.Id, new MoveRequest(1, 0, 2));
        Assert.NotNull(normalRide);
        Assert.True(normalRide.Player.DirtBikeGasGallons < 1);
    }

    [Fact]
    public async Task GodModeTeleportAimedInsideBuildingUsesSafeLandingPoint()
    {
        var configuration = new RealityConfiguration("teleport-test", "Teleport Test", 19, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var building = new CanonicalEntity("teleport-building", EntityKind.Building, new WorldPosition(region, 20, 20),
            new GeometryPoint[] { new(15, 15), new(25, 15), new(25, 25), new(15, 25), new(15, 15) },
            new Dictionary<string, string> { ["building"] = "yes" });
        var store = new SqliteRealityStore(Path.Combine(_directory, "teleport.db"));
        await store.InitializeAsync(configuration);
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("teleporter", "Traveler", "teleport-account");
        await world.SetGodModeAsync(player.Id, true);

        var teleported = await world.TeleportAsync(player.Id, new TeleportRequest(20, 20, false));

        Assert.False(teleported.Position.X is >= 15 and <= 25 && teleported.Position.Y is >= 15 and <= 25);
        Assert.NotEqual(TerrainType.DeepWater, teleported.Terrain);
        var rebuilt = await world.RebuildAsync(player.Id, false);
        Assert.NotEmpty(rebuilt.BaseEntities);
    }

    [Fact]
    public async Task SnapshotIdentifiesLoadedAreasWithoutTreatingBoundingRectangleHolesAsGenerated()
    {
        var configuration = new RealityConfiguration("loaded-cells", "Loaded Cells", 37, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "loaded-cells.db"));
        await store.InitializeAsync(configuration);
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();

        Assert.True(await world.LoadAreaAsync(600, 600));
        var diagonal = world.CreateSnapshot();
        Assert.Equal(2, diagonal.LoadedAreas!.Count);
        Assert.True(diagonal.Bounds.Contains(0, 500));
        Assert.DoesNotContain(diagonal.LoadedAreas, area => area.Contains(0, 500));

        Assert.True(await world.LoadAreaAsync(0, 500));
        var filled = world.CreateSnapshot();
        Assert.Equal(3, filled.LoadedAreas!.Count);
        Assert.Contains(filled.LoadedAreas, area => area.Contains(0, 500));
    }

    [Fact]
    public async Task GodModePurchasesNewAccountBaseForOneCentAndPersistsIt()
    {
        var configuration = new RealityConfiguration("base-purchase", "Base Purchase", 20, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var firstBuilding = Building("base-a", region, 20, 20);
        var secondBuilding = Building("base-b", region, 60, 20);
        var store = new SqliteRealityStore(Path.Combine(_directory, "purchase.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("buyer-account", "Buyer", "hash", "salt", "token", "buyer"), "Buyer");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(firstBuilding, secondBuilding)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("buyer", "Buyer", "buyer-account");
        var originalBase = world.GetPrivateState(player.Id).Base!.BuildingId;
        var targetBuilding = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Building && entity.Id != originalBase);
        var targetDoor = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door && entity.Properties["buildingId"] == targetBuilding.Id);
        await world.SetGodModeAsync(player.Id, true);
        await world.TeleportAsync(player.Id, new TeleportRequest(targetDoor.Position.X, targetDoor.Position.Y, true));
        await world.SetGodModeAsync(player.Id, false);

        var insufficient = await Assert.ThrowsAsync<InvalidOperationException>(() => world.PurchaseBaseAsync(player.Id, new PurchaseBaseRequest(targetDoor.Id)));
        Assert.Contains("$350,000.00", insufficient.Message);
        var before = await world.SetGodModeAsync(player.Id, true);
        var purchased = await world.PurchaseBaseAsync(player.Id, new PurchaseBaseRequest(targetDoor.Id));

        Assert.Equal(1, purchased.PriceCents);
        Assert.Equal(before.WalletCents - 1, purchased.Player.WalletCents);
        Assert.Equal(targetBuilding.Id, world.GetPrivateState(player.Id).Base!.BuildingId);
        world.Leave(player.Id);
        var reconnected = await world.JoinAsync("buyer", "Buyer", "buyer-account");
        Assert.Contains(targetBuilding.Id, reconnected.LocationId);
    }

    [Fact]
    public async Task DungeonSessionIsRecreatedAfterExit()
    {
        var configuration = new RealityConfiguration("dungeon-reset", "Dungeon Reset", 21, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var firstBuilding = Building("dungeon-a", region, 20, 20);
        var secondBuilding = Building("dungeon-b", region, 60, 20);
        var store = new SqliteRealityStore(Path.Combine(_directory, "dungeon-reset.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("dungeon-account", "Dungeon", "hash", "salt", "token", "dungeon-player"), "Dungeon");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(firstBuilding, secondBuilding)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("dungeon-player", "Dungeon", "dungeon-account");
        var ownBase = world.GetPrivateState(player.Id).Base!.BuildingId;
        var door = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door && entity.Properties["buildingId"] != ownBase);
        await world.SetGodModeAsync(player.Id, true);
        await world.TeleportAsync(player.Id, new TeleportRequest(door.Position.X, door.Position.Y, true));
        var first = await world.EnterDungeonAsync(player.Id, door.Id);
        await world.ExitDungeonAsync(player.Id);
        var second = await world.EnterDungeonAsync(player.Id, door.Id);

        Assert.NotSame(first.Dungeon, second.Dungeon);
        Assert.Equal(first.Dungeon.Actors.Count, second.Dungeon.Actors.Count);
        Assert.Equal(first.Dungeon.Chests.Count, second.Dungeon.Chests.Count);
    }

    [Fact]
    public async Task EquippedWeaponConsumesAmmoAndFallsBackToFist()
    {
        var configuration = new RealityConfiguration("weapon-fallback", "Weapon Fallback", 22, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500), PvpEnabled: true);
        var store = new SqliteRealityStore(Path.Combine(_directory, "weapon-fallback.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("attacker", new[] { new ItemStack("rock", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var attacker = await world.JoinAsync("attacker", "Attacker");
        var target = await world.JoinAsync("target", "Target");
        attacker = await world.SetEquipmentAsync(attacker.Id, "weapon", "rock");

        var combat = await world.AttackAsync(attacker.Id, new CombatRequest(target.Id, "rifle"));

        Assert.Equal("rock", combat.Event.Weapon);
        Assert.Equal("fist", combat.Attacker.EquippedWeapon);
        Assert.DoesNotContain(combat.Inventory.Items, item => item.ItemType == "rock");

        var unequipped = await world.SetEquipmentAsync(attacker.Id, "weapon", null);
        Assert.Equal("none", unequipped.EquippedWeapon);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.AttackAsync(attacker.Id, new CombatRequest(target.Id, "fist")));
        Assert.Contains("Equip a weapon", error.Message);
    }

    [Fact]
    public async Task RifleDoesSevenHeartsDamageAndGodModeDoesNotConsumeBullets()
    {
        var configuration = new RealityConfiguration("rifle-damage", "Rifle Damage", 23, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500), PvpEnabled: true);
        var store = new SqliteRealityStore(Path.Combine(_directory, "rifle-damage.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("rifleman", new[] { new ItemStack("rifle", 1), new ItemStack("bullet", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var attacker = await world.JoinAsync("rifleman", "Rifleman");
        var target = await world.JoinAsync("rifle-target", "Target");
        await world.SetGodModeAsync(attacker.Id, true);
        await world.SetEquipmentAsync(attacker.Id, "weapon", "rifle");
        CombatResult? combat = null;
        for (var attempt = 0; attempt < 20 && combat?.Event.Hit != true; attempt++) combat = await world.AttackAsync(attacker.Id, new CombatRequest(target.Id, "fist"));

        Assert.NotNull(combat);
        Assert.True(combat.Event.Hit);
        Assert.Equal(7, combat.Event.Damage);
        Assert.Equal(1, combat.Inventory.Items.Single(item => item.ItemType == "bullet").Quantity);

        await world.SetEquipmentAsync(attacker.Id, "weapon", "sword");
        var sword = await world.AttackAsync(attacker.Id, new CombatRequest(target.Id, "sword"));
        Assert.True(sword.Event.Hit);
        Assert.Equal(5, sword.Event.Damage);
        var configuredKnife = await world.UpdateItemConfigurationAsync(attacker.Id, new UpdateItemConfigurationRequest("knife", 4, 3, 1_500, 4_500, 1.25, 15));
        Assert.Equal(4, configuredKnife.Damage);
        Assert.Equal(3, configuredKnife.RangeMeters);
        Assert.Equal(1.25, configuredKnife.SpeedModifierMph);
        Assert.Equal(15, configuredKnife.VisibilityModifierMeters);
        var persistedConfiguration = await store.LoadItemConfigurationsAsync(configuration.Id);
        Assert.Contains(persistedConfiguration, item => item.ItemType == "knife" && item.Damage == 4 && item.MinimumPriceCents == 1_500 && item.SpeedModifierMph == 1.25 && item.VisibilityModifierMeters == 15);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.UpdateItemConfigurationAsync(target.Id, new UpdateItemConfigurationRequest("knife", 100, 100, 0, 0)));
        await world.SetEquipmentAsync(attacker.Id, "weapon", "knife");
        CombatResult? knife = null;
        for (var attempt = 0; attempt < 20 && knife?.Event.Hit != true; attempt++) knife = await world.AttackAsync(attacker.Id, new CombatRequest(target.Id, "knife"));
        Assert.NotNull(knife);
        Assert.True(knife.Event.Hit);
        Assert.Equal(4, knife.Event.Damage);
    }

    [Fact]
    public async Task MovementConfigurationUsesAdditiveModifiersAndPersists()
    {
        var configuration = new RealityConfiguration("movement-config", "Movement Config", 31, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "movement-config.db"));
        await store.InitializeAsync(configuration);
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("speed-admin", "SpeedAdmin");
        var other = await world.JoinAsync("speed-other", "SpeedOther");
        await world.SetGodModeAsync(player.Id, true);

        var terrain = Enum.GetValues<TerrainType>().ToDictionary(value => value, _ => 0d);
        terrain[TerrainType.Grass] = -1;
        var modes = Enum.GetValues<TravelMode>().ToDictionary(value => value, _ => 0d);
        modes[TravelMode.Run] = 2;
        var movement = await world.UpdateMovementConfigurationAsync(player.Id, new UpdateMovementConfigurationRequest(4, 120, terrain, modes));
        await world.UpdateItemConfigurationAsync(player.Id, new UpdateItemConfigurationRequest("magicHikingShoes", 0, 0, 10_000, 40_000, 4, 25));

        var synthetic = player with { GodMode = false, Water = 10, TravelMode = TravelMode.Run, MagicHikingShoesOn = true, Stamina = 5, MaximumStamina = 10 };
        Assert.Equal(8, world.ConfiguredSpeedMetersPerSecond(synthetic, TerrainType.Grass) * 2.236936, 3);
        var stacked = synthetic with { TravelMode = TravelMode.Bike };
        Assert.Equal(17.5, world.ConfiguredSpeedMetersPerSecond(stacked, TerrainType.Grass) * 2.236936, 3);
        Assert.Equal(120, movement.BaseVisibilityMeters);
        var persisted = await store.LoadMovementConfigurationAsync(configuration.Id);
        Assert.NotNull(persisted);
        Assert.Equal(-1, persisted.TerrainSpeedModifiersMph[TerrainType.Grass]);
        Assert.Equal(2, persisted.TravelModeSpeedModifiersMph[TravelMode.Run]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.UpdateMovementConfigurationAsync(other.Id, new UpdateMovementConfigurationRequest(4, 120, terrain, modes)));
    }

    private static CanonicalEntity Building(string id, RegionId region, double x, double y) =>
        new(id, EntityKind.Building, new WorldPosition(region, x, y),
            new GeometryPoint[] { new(x - 5, y - 5), new(x + 5, y - 5), new(x + 5, y + 5), new(x - 5, y + 5), new(x - 5, y - 5) },
            new Dictionary<string, string> { ["building"] = "yes" });

    private sealed class FixedGeographicProvider(params CanonicalEntity[] buildings) : IGeographicProvider
    {
        public string Name => "test";
        public Task<GeographicDataset> GetAreaAsync(GeographicArea area, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeographicDataset(Name, area, buildings, new[] { new ElevationSample(0, 0, 0) }, DateTimeOffset.UtcNow));
    }

    private sealed class FixedWeatherProvider : IWeatherProvider
    {
        public Task<WeatherState> GetCurrentAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default) =>
            Task.FromResult(WeatherState.Unavailable);
    }
}
