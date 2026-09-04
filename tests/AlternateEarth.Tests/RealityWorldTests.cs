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
    public async Task NewAccountStartsOutdoorsAndReconnectsAtPersistedOutdoorPosition()
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
        Assert.Equal("outdoor", reconnected.LocationId);
        Assert.Equal(first.Position, reconnected.Position);
        Assert.Null(world.GetPrivateState(reconnected.Id).Dungeon);
        Assert.NotNull(world.GetPrivateState(reconnected.Id).Base);
    }

    [Fact]
    public async Task LeavingHomeRemainsOutdoorsAfterReconnect()
    {
        var configuration = new RealityConfiguration("home-exit-test", "Home Exit Test", 117, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var building = Building("exit-home", region, 20, 20);
        var store = new SqliteRealityStore(Path.Combine(_directory, "home-exit.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("exit-account", "ExitPlayer", "hash", "salt", "token", "exit-character"), "ExitPlayer");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("exit-character", "ExitPlayer", "exit-account");
        var door = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door);
        await world.SetGodModeAsync(player.Id, true);
        await world.TeleportAsync(player.Id, new TeleportRequest(door.Position.X, door.Position.Y, true));
        var entered = await world.EnterDungeonAsync(player.Id, door.Id);
        Assert.True(entered.Dungeon.IsHome);

        var exited = await world.ExitDungeonAsync(player.Id);
        Assert.Equal("outdoor", exited.LocationId);
        Assert.Null(world.GetPrivateState(player.Id).Dungeon);
        world.Leave(player.Id);

        var reconnected = await world.JoinAsync(player.Id, "ExitPlayer", "exit-account");
        Assert.Equal("outdoor", reconnected.LocationId);
        Assert.Equal(exited.Position, reconnected.Position);
        Assert.Null(world.GetPrivateState(player.Id).Dungeon);
    }

    [Fact]
    public async Task DungeonPathDoesNotWaitForAnOutdoorAreaLoad()
    {
        var configuration = new RealityConfiguration("dungeon-path", "Dungeon Path", 118, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var building = Building("path-home", region, 20, 20);
        var store = new SqliteRealityStore(Path.Combine(_directory, "dungeon-path.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("path-account", "PathUser", "hash", "salt", "token", "path-character"), "PathUser");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("path-character", "PathUser", "path-account");

        Assert.True(world.IsAreaLoadRequiredForPath(player.Id, 5_000, 5_000));
        await world.SetGodModeAsync(player.Id, true);
        var door = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door);
        await world.TeleportAsync(player.Id, new TeleportRequest(door.Position.X, door.Position.Y, true));
        var entered = await world.EnterDungeonAsync(player.Id, door.Id);

        Assert.False(world.IsAreaLoadRequiredForPath(player.Id, entered.Player.Position.X, entered.Player.Position.Y));
        var path = await world.FindPathAsync(player.Id, new PathRequest(entered.Player.Position.X, entered.Player.Position.Y, 1));
        Assert.True(path.Result.Success);
        Assert.False(path.Expanded);
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
        var player = await world.JoinAsync("rider", "Rider");

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
        var player = await world.JoinAsync("teleporter", "Traveler");
        await world.SetGodModeAsync(player.Id, true);

        var teleported = await world.TeleportAsync(player.Id, new TeleportRequest(20, 20, false));

        Assert.False(teleported.Position.X is >= 15 and <= 25 && teleported.Position.Y is >= 15 and <= 25);
        Assert.NotEqual(TerrainType.DeepWater, teleported.Terrain);
        var rebuilt = await world.RebuildAsync(player.Id, false);
        Assert.NotEmpty(rebuilt.BaseEntities);
    }

    [Fact]
    public async Task GodModeCanTriggerEachWorldEventAuthoritatively()
    {
        var configuration = new RealityConfiguration("manual-events", "Manual Events", 119, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "manual-events.db"));
        await store.InitializeAsync(configuration);
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("event-admin", "EventAdmin");

        var denied = Assert.Throws<InvalidOperationException>(() => world.TriggerWorldEvent(player.Id, "ufo"));
        Assert.Contains("God Mode", denied.Message);
        await world.SetGodModeAsync(player.Id, true);

        var ufo = world.TriggerWorldEvent(player.Id, "ufo");
        var trex = world.TriggerWorldEvent(player.Id, "trex");
        var bear = world.TriggerWorldEvent(player.Id, "bear");
        var actorIds = (world.CreateSnapshot().Actors ?? Array.Empty<ActorState>()).Select(actor => actor.Id).ToHashSet();

        Assert.Equal("ufo", ufo.Subtype);
        Assert.Equal("tRex", trex.Subtype);
        Assert.Equal("eventBear", bear.Subtype);
        Assert.Equal(100, ufo.Position.Z);
        Assert.All(new[] { ufo, trex, bear }, actor =>
        {
            Assert.NotNull(actor.EventStartedAtUtc);
            Assert.NotNull(actor.EventEndsAtUtc);
            Assert.Contains(actor.Id, actorIds);
        });
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
        for (var index = 0; index < 12; index++)
        {
            var accountId = $"loaded-account-{index}"; var characterId = $"loaded-player-{index}"; var username = $"Load{index:00}";
            await store.CreateAccountAsync(new AccountRecord(accountId, username, "hash", "salt", $"token-{index}", characterId), username);
            var player = await world.JoinAsync(characterId, username, accountId);
            Assert.Contains(diagonal.LoadedAreas, area => area.Contains(player.Position.X, player.Position.Y));
        }

        Assert.True(await world.LoadAreaAsync(0, 500));
        var filled = world.CreateSnapshot();
        Assert.Equal(3, filled.LoadedAreas!.Count);
        Assert.Contains(filled.LoadedAreas, area => area.Contains(0, 500));
    }

    [Fact]
    public async Task GodModePurchasesBaseAtDisplayedPriceWithoutSpendingWalletFunds()
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
        var expectedPrice = RealityWorld.CalculateBuildingPriceCents(targetBuilding);
        Assert.Contains($"${expectedPrice / 100m:N2}", insufficient.Message);
        var before = await world.SetGodModeAsync(player.Id, true);
        var purchased = await world.PurchaseBaseAsync(player.Id, new PurchaseBaseRequest(targetDoor.Id));

        Assert.Equal(expectedPrice, purchased.PriceCents);
        Assert.Equal(before.WalletCents, purchased.Player.WalletCents);
        Assert.Equal(targetBuilding.Id, world.GetPrivateState(player.Id).Base!.BuildingId);
        world.Leave(player.Id);
        var reconnected = await world.JoinAsync("buyer", "Buyer", "buyer-account");
        Assert.Equal("outdoor", reconnected.LocationId);
        Assert.Equal(targetBuilding.Id, world.GetPrivateState(reconnected.Id).Base!.BuildingId);
    }

    [Fact]
    public async Task DungeonSessionIsRecreatedAfterExit()
    {
        var configuration = new RealityConfiguration("dungeon-reset", "Dungeon Reset", 21, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var firstBuilding = Building("dungeon-a", region, 20, 20) with { Properties = new Dictionary<string, string> { ["building"] = "yes", ["questItem"] = "true" } };
        var secondBuilding = Building("dungeon-b", region, 60, 20) with { Properties = new Dictionary<string, string> { ["building"] = "yes", ["questItem"] = "true" } };
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
        Assert.NotEqual(first.Dungeon.SessionId, second.Dungeon.SessionId);
        Assert.InRange(first.Dungeon.LevelCount, 1, 10);
        Assert.InRange(second.Dungeon.LevelCount, 1, 10);
        Assert.InRange(first.Dungeon.Actors.Count, 3, 6);
        Assert.InRange(second.Dungeon.Actors.Count, 3, 6);
    }

    [Fact]
    public async Task StoreBuildingsAreSafeCommercialInteriorsAndCannotBecomeHomes()
    {
        var configuration = new RealityConfiguration("store-building", "Store Building", 71, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var storeBuilding = Building("a-store", region, 20, 20) with
        {
            Properties = new Dictionary<string, string>
            {
                ["building"] = "retail", ["merchantCategory"] = "clothing", ["name"] = "Foot Mart", ["questItem"] = "true"
            }
        };
        var residentialBuilding = Building("z-home", region, 60, 20);
        var store = new SqliteRealityStore(Path.Combine(_directory, "store-building.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("shopper-account", "Shopper", "hash", "salt", "token", "shopper-player"), "Shopper");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(storeBuilding, residentialBuilding)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("shopper-player", "Shopper", "shopper-account");

        Assert.Equal(residentialBuilding.Id, world.GetPrivateState(player.Id).Base!.BuildingId);
        player = await world.SetGodModeAsync(player.Id, true);
        var storeDoor = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door && entity.Properties["buildingId"] == storeBuilding.Id);
        await world.TeleportAsync(player.Id, new TeleportRequest(storeDoor.Position.X, storeDoor.Position.Y, true));
        var purchaseError = await Assert.ThrowsAsync<InvalidOperationException>(() => world.PurchaseBaseAsync(player.Id, new PurchaseBaseRequest(storeDoor.Id)));
        Assert.Contains("cannot be purchased", purchaseError.Message, StringComparison.OrdinalIgnoreCase);

        var entered = await world.EnterDungeonAsync(player.Id, storeDoor.Id);
        Assert.True(entered.Dungeon.IsStore);
        Assert.Single(entered.Dungeon.Actors, actor => actor.IsMerchant);
        Assert.DoesNotContain(entered.Dungeon.Actors, actor => actor.Name.Contains("Dungeon Dweller", StringComparison.OrdinalIgnoreCase));
        Assert.All(entered.Dungeon.Actors, actor => Assert.True(actor.IsMerchant || actor.Subtype == "storeEmployee"));
        Assert.All(entered.Dungeon.Actors, actor => Assert.Contains(actor.Name, new[] { "Joe", "Sam", "Dave", "Maria", "Priya", "Marcus", "Elena", "Theo", "Grace", "Jordan", "Leah", "Omar", "Nina", "Henry", "Maya", "Luis" }));
        Assert.Equal("outdoor", (await world.ExitDungeonAsync(player.Id)).LocationId);
    }

    [Fact]
    public async Task DungeonMatchesBuildingFootprintAndTraversesSharedStairs()
    {
        var configuration = new RealityConfiguration("shaped-levels", "Shaped Levels", 23, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        CanonicalEntity ShapedBuilding(string id, double x) => new(id, EntityKind.Building, new WorldPosition(region, x, 20),
            new GeometryPoint[] { new(x - 9, 13), new(x + 9, 13), new(x + 9, 20), new(x + 2, 20), new(x + 2, 27), new(x - 9, 27), new(x - 9, 13) },
            new Dictionary<string, string> { ["building"] = "yes", ["dungeon:levels"] = "3", ["questItem"] = "true" });
        var firstBuilding = ShapedBuilding("shaped-a", 20);
        var secondBuilding = ShapedBuilding("shaped-b", 60);
        var store = new SqliteRealityStore(Path.Combine(_directory, "shaped-levels.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("shaped-account", "Explorer", "hash", "salt", "token", "shaped-player"), "Explorer");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(firstBuilding, secondBuilding)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("shaped-player", "Explorer", "shaped-account");
        var ownBase = world.GetPrivateState(player.Id).Base!.BuildingId;
        var door = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door && entity.Properties["buildingId"] != ownBase);
        await world.SetGodModeAsync(player.Id, true);
        await world.TeleportAsync(player.Id, new TeleportRequest(door.Position.X, door.Position.Y, true));

        var first = await world.EnterDungeonAsync(player.Id, door.Id);
        Assert.Equal(18, first.Dungeon.Width, 3);
        Assert.Equal(14, first.Dungeon.Height, 3);
        Assert.Equal(6, first.Dungeon.Footprint!.Count);
        Assert.Equal(6, first.Dungeon.ExteriorWallCount);
        Assert.Equal(1, first.Dungeon.Level);
        Assert.Equal(3, first.Dungeon.LevelCount);
        Assert.NotNull(first.Dungeon.Stairs);

        var second = await world.ChangeDungeonLevelAsync(player.Id, 1);
        Assert.Equal(2, second.Dungeon.Level);
        Assert.Equal(first.Dungeon.Stairs, second.Dungeon.Stairs);
        var last = await world.ChangeDungeonLevelAsync(player.Id, 1);
        Assert.Equal(3, last.Dungeon.Level);
        Assert.Equal(first.Dungeon.Stairs, last.Dungeon.Stairs);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ChangeDungeonLevelAsync(player.Id, 1));
        var returned = await world.ChangeDungeonLevelAsync(player.Id, -1);
        Assert.Equal(2, returned.Dungeon.Level);
    }

    [Fact]
    public async Task DoorLocksBlockOrdinaryBuildingsButNotBaseOrQuestBuildings()
    {
        var configuration = new RealityConfiguration("door-locks", "Door Locks", 124, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var buildings = Enumerable.Range(0, 24)
            .Select(index => Building($"lock-building-{index}", region, 20 + index % 6 * 28, 20 + index / 6 * 28))
            .ToList();
        for (var index = 0; index < 2; index++)
        {
            var quest = Building($"quest-building-{index}", region, 20 + index * 28, 160);
            buildings.Add(quest with { Properties = new Dictionary<string, string>(quest.Properties) { ["questItem"] = "true" } });
        }
        var store = new SqliteRealityStore(Path.Combine(_directory, "door-locks.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("lock-account", "LockTester", "hash", "salt", "token", "lock-player"), "LockTester");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(buildings.ToArray())), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("lock-player", "LockTester", "lock-account");
        player = await world.SetGodModeAsync(player.Id, true);
        var snapshot = world.CreateSnapshot();
        var baseId = world.GetPrivateState(player.Id).Base!.BuildingId;
        var doors = snapshot.BaseEntities.Where(entity => entity.Kind == EntityKind.Door).ToDictionary(entity => entity.Id);
        var baseDoor = doors.Values.Single(door => door.Properties["buildingId"] == baseId);

        await world.TeleportAsync(player.Id, new TeleportRequest(baseDoor.Position.X, baseDoor.Position.Y, true));
        Assert.True((await world.EnterDungeonAsync(player.Id, baseDoor.Id)).Dungeon.IsHome);
        await world.ExitDungeonAsync(player.Id);

        var locked = snapshot.DoorLocks!.First(state => state.Locked && state.BuildingId != baseId);
        var lockedDoor = doors[locked.DoorId];
        await world.TeleportAsync(player.Id, new TeleportRequest(lockedDoor.Position.X, lockedDoor.Position.Y, true));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.EnterDungeonAsync(player.Id, lockedDoor.Id));
        Assert.Contains("locked", error.Message, StringComparison.OrdinalIgnoreCase);

        var questDoor = doors.Values.First(door => door.Properties["buildingId"].StartsWith("quest-building-", StringComparison.Ordinal) && door.Properties["buildingId"] != baseId);
        Assert.False(snapshot.DoorLocks!.Single(state => state.DoorId == questDoor.Id).Locked);
        await world.TeleportAsync(player.Id, new TeleportRequest(questDoor.Position.X, questDoor.Position.Y, true));
        Assert.False((await world.EnterDungeonAsync(player.Id, questDoor.Id)).Dungeon.IsHome);
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
    public async Task StackedItemsUseOneSlotPerTypeButAllUnitsContributeWeightAndSlowMovement()
    {
        var configuration = new RealityConfiguration("stacked-inventory", "Stacked Inventory", 221, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "stacked-inventory.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("stacker", new[] { new ItemStack("rock", 50), new ItemStack("slingshot", 3) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var loaded = await world.JoinAsync("stacker", "Stacker");
        var empty = await world.JoinAsync("empty-stacker", "Empty");

        var inventory = world.GetPrivateState(loaded.Id).Inventory;
        Assert.Equal(2, inventory.WeaponSlotsUsed);
        Assert.Equal(26.2, inventory.WeightPounds, 3);
        var loadedSpeed = world.ConfiguredSpeedMetersPerSecond(loaded, TerrainType.Pavement);
        var emptySpeed = world.ConfiguredSpeedMetersPerSecond(empty, TerrainType.Pavement);
        Assert.Equal(.738, loadedSpeed / emptySpeed, 3);

        var god = await world.SetGodModeAsync(loaded.Id, true);
        Assert.Equal(5, world.ConfiguredSpeedMetersPerSecond(god, TerrainType.Pavement) / emptySpeed, 3);
    }

    [Fact]
    public async Task BikeRaftAndMotorcycleDoNotContributeToPlayerWeight()
    {
        var configuration = new RealityConfiguration("weightless-travel-items", "Weightless Travel Items", 224, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "weightless-travel-items.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("traveler", new[]
        {
            new ItemStack("bike", 1), new ItemStack("inflatableRaft", 1), new ItemStack("motorcycle", 1), new ItemStack("rock", 2)
        }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("traveler", "Traveler");

        var inventory = world.GetPrivateState(player.Id).Inventory;

        Assert.Equal(1, inventory.WeightPounds, 3);
        Assert.Equal(0, inventory.Items.Single(item => item.ItemType == "bike").UnitWeightPounds);
        Assert.Equal(0, inventory.Items.Single(item => item.ItemType == "inflatableRaft").UnitWeightPounds);
        Assert.Equal(0, inventory.Items.Single(item => item.ItemType == "motorcycle").UnitWeightPounds);
    }

    [Fact]
    public async Task DroppedItemBecomesCollectibleLootAndLastEquippedItemFallsBack()
    {
        var configuration = new RealityConfiguration("drop-inventory", "Drop Inventory", 225, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "drop-inventory.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("dropper", new[] { new ItemStack("knife", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("dropper", "Dropper");
        player = await world.SetEquipmentAsync(player.Id, "weapon", "knife");

        var dropped = await world.DropInventoryItemAsync(player.Id, new DropItemRequest("knife"));

        Assert.Equal("fist", dropped.Player.EquippedWeapon);
        Assert.DoesNotContain(dropped.PrivateState.Inventory.Items, item => item.ItemType == "knife");
        Assert.Equal(player.Position, dropped.Drop.Position);
        Assert.Equal(player.LocationId, dropped.Drop.LocationId);
        Assert.Equal("knife", Assert.Single(dropped.Drop.Items).ItemType);
        var collected = await world.CollectLootAsync(player.Id, dropped.Drop.Id);
        Assert.Equal(1, collected.Inventory.Items.Single(item => item.ItemType == "knife").Quantity);
        Assert.Contains("1 × Knife", collected.Message);
        Assert.DoesNotContain("supplies", collected.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await store.LoadInventoryAsync(player.Id);
        Assert.Equal(1, persisted.Items.Single(item => item.ItemType == "knife").Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.DropInventoryItemAsync(player.Id, new DropItemRequest("fist")));
    }

    [Fact]
    public async Task HomeStorageChestTransfersStacksWithoutCapacityLimit()
    {
        var configuration = new RealityConfiguration("home-item-storage", "Home Item Storage", 222, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var store = new SqliteRealityStore(Path.Combine(_directory, "home-item-storage.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("storage-account", "Storage", "hash", "salt", "token", "storage-player"), "Storage");
        await store.SaveInventoryAsync(new InventoryState("storage-player", new[] { new ItemStack("rock", 50), new ItemStack("ballBearing", 25) }));
        await store.SaveInventoryAsync(new InventoryState("home-items:home-item-storage:storage-account", new[] { new ItemStack("quest:first", 1), new ItemStack("quest:second", 1), new ItemStack("quest:third", 1), new ItemStack("quest:fourth", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(Building("storage-home", region, 20, 20))), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("storage-player", "Storage", "storage-account");
        player = await world.SetGodModeAsync(player.Id, true);
        var door = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door);
        await world.TeleportAsync(player.Id, new TeleportRequest(door.Position.X, door.Position.Y, true));
        var entered = await world.EnterDungeonAsync(player.Id, door.Id);
        var chest = entered.Dungeon.Furnishings!.Single(item => item.Properties.GetValueOrDefault("objectType") == "storageChest");

        var opened = world.OpenHomeItemStorage(player.Id, chest.Id);
        Assert.True(opened.Unlimited);
        var deposited = await world.TransferHomeItemAsync(player.Id, new TransferHomeStorageRequest(chest.Id, "rock", 40, true));
        Assert.Equal(10, deposited.PrivateState.Inventory.Items.Single(item => item.ItemType == "rock").Quantity);
        Assert.Equal(40, deposited.PrivateState.HomeItemStorage!.Items.Single(item => item.ItemType == "rock").Quantity);
        var withdrawn = await world.TransferHomeItemAsync(player.Id, new TransferHomeStorageRequest(chest.Id, "rock", 5, false));
        Assert.Equal(15, withdrawn.PrivateState.Inventory.Items.Single(item => item.ItemType == "rock").Quantity);
        Assert.Equal(35, withdrawn.PrivateState.HomeItemStorage!.Items.Single(item => item.ItemType == "rock").Quantity);
        var persisted = await store.LoadInventoryAsync("home-items:home-item-storage:storage-account");
        Assert.Equal(35, persisted.Items.Single(item => item.ItemType == "rock").Quantity);
        await world.TransferHomeItemAsync(player.Id, new TransferHomeStorageRequest(chest.Id, "quest:first", 1, false));
        await world.TransferHomeItemAsync(player.Id, new TransferHomeStorageRequest(chest.Id, "quest:second", 1, false));
        await world.TransferHomeItemAsync(player.Id, new TransferHomeStorageRequest(chest.Id, "quest:third", 1, false));
        var questLimit = await Assert.ThrowsAsync<InvalidOperationException>(() => world.TransferHomeItemAsync(player.Id, new TransferHomeStorageRequest(chest.Id, "quest:fourth", 1, false)));
        Assert.Contains("quest-item slots", questLimit.Message);
    }

    [Fact]
    public async Task BackpackRejectsRewardsAboveAbsoluteWeightOrWeaponSlotLimit()
    {
        var configuration = new RealityConfiguration("inventory-limits", "Inventory Limits", 223, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "inventory-limits.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("heavy", new[] { new ItemStack("rock", 100) }));
        await store.SaveInventoryAsync(new InventoryState("armed", new[] { new ItemStack("knife", 1), new ItemStack("sword", 1), new ItemStack("slingshot", 1) }));
        await store.SaveInventoryAsync(new InventoryState("packed", new[] { new ItemStack("water", 1), new ItemStack("food", 1), new ItemStack("flashlight", 1), new ItemStack("hat", 1), new ItemStack("laser", 1), new ItemStack("arrow", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();

        foreach (var (characterId, expectedMessage) in new[] { ("heavy", "maximum"), ("armed", "weapon slots"), ("packed", "other-item slots") })
        {
            var player = await world.JoinAsync(characterId, characterId);
            player = await world.SetGodModeAsync(player.Id, true);
            var chest = world.GetPrivateState(player.Id).Chests!.First();
            await world.TeleportAsync(player.Id, new TeleportRequest(chest.Position.X, chest.Position.Y, true));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.OpenChestAsync(player.Id, chest.Id));
            Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
        }
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
        Assert.Contains("Rifleman", combat.Event.Message);
        Assert.Contains("7 hearts damage", combat.Event.Message);
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

    [Fact]
    public async Task PurchasedAreaMapRevealsCurrentBlockAndPersists()
    {
        var configuration = new RealityConfiguration("map-purchase", "Map Purchase", 44, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "map-purchase.db"));
        await store.InitializeAsync(configuration);
        var merchantPoi = new CanonicalEntity("map-store", EntityKind.PointOfInterest, new WorldPosition(configuration.Area.Region, 15, 15),
            Array.Empty<GeometryPoint>(), new Dictionary<string, string> { ["name"] = "Map Store", ["merchantCategory"] = "general" });
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(merchantPoi)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("map-buyer", "MapBuyer");
        player = await world.SetGodModeAsync(player.Id, true);
        const long displayedPrice = 100_000_000;
        await world.UpdateItemConfigurationAsync(player.Id, new UpdateItemConfigurationRequest("areaMap", 0, 0, displayedPrice, displayedPrice));
        var merchant = world.CreateSnapshot().Actors!.First(actor => actor.IsMerchant);
        await world.TeleportAsync(player.Id, new TeleportRequest(merchant.Position.X, merchant.Position.Y, true));
        var quote = world.RequestTrade(player.Id, merchant.Id);
        Assert.Contains(quote.Offers, offer => offer.ItemType == "areaMap" && offer.UnitPriceCents == displayedPrice);

        player = await world.SetGodModeAsync(player.Id, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ConfirmTradeAsync(player.Id, new ConfirmTradeRequest(merchant.Id, new[] { new PurchaseLine("areaMap", 1) })));
        player = await world.SetGodModeAsync(player.Id, true);
        var purchase = await world.ConfirmTradeAsync(player.Id, new ConfirmTradeRequest(merchant.Id, new[] { new PurchaseLine("areaMap", 1) }));
        Assert.Equal(player.WalletCents, purchase.Player.WalletCents);
        var expectedArea = world.AreaKeyFor(merchant.Position.X, merchant.Position.Y);
        Assert.Contains(expectedArea, world.GetPrivateState(player.Id).RevealedWorldAreas!);
        Assert.DoesNotContain(world.RequestTrade(player.Id, merchant.Id).Offers, offer => offer.ItemType == "areaMap");

        world.Leave(player.Id);
        await world.JoinAsync(player.Id, "MapBuyer");
        Assert.Contains(expectedArea, world.GetPrivateState(player.Id).RevealedWorldAreas!);
    }

    [Fact]
    public async Task IndoorTravelRejectsBikesButAllowsDungeonModes()
    {
        var configuration = new RealityConfiguration("indoor-travel", "Indoor Travel", 45, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var store = new SqliteRealityStore(Path.Combine(_directory, "indoor-travel.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("indoor-account", "Indoor", "hash", "salt", "token", "indoor-player"), "Indoor");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(Building("indoor-building", region, 20, 20))), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("indoor-player", "Indoor", "indoor-account");
        await world.SetGodModeAsync(player.Id, true);
        var door = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door);
        await world.TeleportAsync(player.Id, new TeleportRequest(door.Position.X, door.Position.Y, true));
        await world.EnterDungeonAsync(player.Id, door.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.Bike));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.DirtBike));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.Motorcycle));
        Assert.Equal(TravelMode.Skateboard, (await world.SetTravelModeAsync(player.Id, TravelMode.Skateboard)).TravelMode);
        Assert.Equal(TravelMode.Raft, (await world.SetTravelModeAsync(player.Id, TravelMode.Raft)).TravelMode);
        Assert.Equal(TravelMode.Run, (await world.SetTravelModeAsync(player.Id, TravelMode.Run)).TravelMode);
    }

    [Fact]
    public async Task MovementHonorsRemainingDistanceToPreventFastVehicleOvershoot()
    {
        var configuration = new RealityConfiguration("arrival-cap", "Arrival Cap", 46, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "arrival-cap.db"));
        await store.InitializeAsync(configuration);
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("arrival-player", "Arrival");
        await world.SetGodModeAsync(player.Id, true);
        await world.SetTravelModeAsync(player.Id, TravelMode.Motorcycle);

        var destinationX = player.Position.X + .05;
        var request = new MoveRequest(1, 0, 1, .05, destinationX, player.Position.Y);
        var moved = await world.MoveAsync(player.Id, request);
        var repeatedStaleRequest = await world.MoveAsync(player.Id, request);

        Assert.NotNull(moved);
        Assert.NotNull(repeatedStaleRequest);
        Assert.InRange(moved.Player.Position.Distance2D(player.Position), 0, .050001);
        Assert.InRange(repeatedStaleRequest.Player.Position.Distance2D(player.Position), 0, .050001);
    }

    [Fact]
    public void BuildingPriceStartsAtThreeHundredFiftyThousandAndGrowsExponentially()
    {
        var region = new RegionId(45, -123);
        var tiny = Building("tiny", region, 0, 0);
        var large = new CanonicalEntity("large", EntityKind.Building, new WorldPosition(region, 0, 0),
            new GeometryPoint[] { new(-20, -10), new(20, -10), new(20, 10), new(-20, 10), new(-20, -10) }, new Dictionary<string, string>());

        Assert.True(RealityWorld.CalculateBuildingPriceCents(tiny) >= 35_000_000);
        Assert.True(RealityWorld.CalculateBuildingPriceCents(large) > RealityWorld.CalculateBuildingPriceCents(tiny));
    }

    [Fact]
    public async Task HomeUsesBuildingDimensionsAndFurniturePersistsInHomeOnlyStorage()
    {
        var configuration = new RealityConfiguration("home-furniture", "Home Furniture", 47, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var building = new CanonicalEntity("large-home", EntityKind.Building, new WorldPosition(region, 30, 30),
            new GeometryPoint[] { new(15, 20), new(45, 20), new(45, 40), new(15, 40), new(15, 20) }, new Dictionary<string, string>());
        var store = new SqliteRealityStore(Path.Combine(_directory, "home-furniture.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("home-account", "HomeUser", "hash", "salt", "token", "home-player"), "HomeUser");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("home-player", "HomeUser", "home-account");
        await world.SetGodModeAsync(player.Id, true);
        var door = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door);
        await world.TeleportAsync(player.Id, new TeleportRequest(door.Position.X, door.Position.Y, true));
        var entered = await world.EnterDungeonAsync(player.Id, door.Id);

        Assert.True(entered.Dungeon.IsHome);
        Assert.Equal(30, entered.Dungeon.Width, 3);
        Assert.Equal(20, entered.Dungeon.Height, 3);
        Assert.Equal(4, entered.Dungeon.Footprint!.Count);
        Assert.Equal(4, entered.Dungeon.ExteriorWallCount);
        Assert.NotNull(entered.Dungeon.Doorway);
        Assert.True(IsInside(entered.Dungeon.Exit, entered.Dungeon.Footprint));
        Assert.All(entered.Dungeon.Furnishings!, item => Assert.True(IsInside(item.Position, entered.Dungeon.Footprint)));
        Assert.All(entered.Dungeon.Walls.Skip(entered.Dungeon.ExteriorWallCount), wall => Assert.True(wall.DoorStart >= 0 && wall.DoorEnd > wall.DoorStart));
        Assert.Contains(entered.Dungeon.Furnishings!, item => item.Properties["objectType"] == "wardrobe");
        var chair = entered.Dungeon.Furnishings!.First(item => item.Properties["objectType"] == "diningChair");
        var originalPosition = chair.Position;
        var rotated = await world.RotateFurnitureAsync(player.Id, new RotateFurnitureRequest(chair.Id));
        Assert.Equal("90", rotated.Furnishings!.Single(item => item.Id == chair.Id).Properties["rotationDegrees"]);
        await world.StoreFurnitureAsync(player.Id, new StoreFurnitureRequest(chair.Id));
        Assert.Contains(world.GetPrivateState(player.Id).HomeStorage!, item => item.Id == chair.Id);
        await world.PlaceFurnitureAsync(player.Id, new PlaceFurnitureRequest(chair.Id, originalPosition.X, originalPosition.Y, 90));
        Assert.DoesNotContain(world.GetPrivateState(player.Id).HomeStorage!, item => item.Id == chair.Id);
        await world.StoreFurnitureAsync(player.Id, new StoreFurnitureRequest(chair.Id));

        await world.ExitDungeonAsync(player.Id);
        Assert.Null(world.GetPrivateState(player.Id).HomeStorage);
        await world.EnterDungeonAsync(player.Id, door.Id);
        Assert.Contains(world.GetPrivateState(player.Id).HomeStorage!, item => item.Id == chair.Id);
        world.Leave(player.Id);
        var savedFurniture = (await store.LoadHomeFurnitureAsync("home-account", configuration.Id)).ToList();
        var wardrobeIndex = savedFurniture.FindIndex(item => item.Properties["objectType"] == "wardrobe");
        savedFurniture[wardrobeIndex] = savedFurniture[wardrobeIndex] with
        {
            Position = new WorldPosition(region, entered.Dungeon.Width + 50, entered.Dungeon.Height + 50),
            Properties = new Dictionary<string, string>(savedFurniture[wardrobeIndex].Properties, StringComparer.OrdinalIgnoreCase) { ["stored"] = "false" }
        };
        await store.SaveHomeFurnitureAsync("home-account", configuration.Id, savedFurniture);
        var reloadedWorld = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await reloadedWorld.InitializeAsync();
        await reloadedWorld.JoinAsync("home-player", "HomeUser", "home-account");
        Assert.Contains(reloadedWorld.GetPrivateState("home-player").HomeStorage!, item => item.Id == chair.Id);
        var repairedHome = reloadedWorld.GetPrivateState("home-player").Dungeon!;
        var repairedWardrobe = repairedHome.Furnishings!.Single(item => item.Properties["objectType"] == "wardrobe");
        Assert.True(IsInside(repairedWardrobe.Position, repairedHome.Footprint));
    }

    [Fact]
    public async Task FurnitureStoreOffersIllustratedVariantsAndPurchasesIntoHome()
    {
        var configuration = new RealityConfiguration("furniture-store", "Furniture Store", 48, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var building = Building("furniture-home", region, 20, 20);
        var storePoi = new CanonicalEntity("furniture-shop", EntityKind.PointOfInterest, new WorldPosition(region, 45, 45), Array.Empty<GeometryPoint>(),
            new Dictionary<string, string> { ["name"] = "Cozy Rooms", ["merchantCategory"] = "furniture" });
        var store = new SqliteRealityStore(Path.Combine(_directory, "furniture-store.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("shop-account", "Shopper", "hash", "salt", "token", "shop-player"), "Shopper");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building, storePoi)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("shop-player", "Shopper", "shop-account");
        await world.SetGodModeAsync(player.Id, true);
        var merchant = world.CreateSnapshot().Actors!.Single(actor => actor.MerchantCategory == "furniture");
        await world.TeleportAsync(player.Id, new TeleportRequest(merchant.Position.X, merchant.Position.Y, true));
        var quote = world.RequestTrade(player.Id, merchant.Id);
        var furniture = quote.Offers.Where(offer => offer.ItemType.StartsWith("furniture:", StringComparison.Ordinal)).OrderBy(offer => offer.UnitPriceCents).First();

        Assert.NotNull(furniture.DisplayName);
        Assert.NotNull(furniture.ImageKey);
        Assert.True(furniture.Properties!.ContainsKey("color"));
        Assert.True(furniture.Properties.ContainsKey("pattern"));
        Assert.True(furniture.UnitPriceCents <= 50_000);
        await world.ConfirmTradeAsync(player.Id, new ConfirmTradeRequest(merchant.Id, new[] { new PurchaseLine(furniture.ItemType, 1) }));

        var baseDoor = world.CreateSnapshot().BaseEntities.Single(entity => entity.Kind == EntityKind.Door && entity.Properties["buildingId"] == world.GetPrivateState(player.Id).Base!.BuildingId);
        await world.TeleportAsync(player.Id, new TeleportRequest(baseDoor.Position.X, baseDoor.Position.Y, true));
        await world.EnterDungeonAsync(player.Id, baseDoor.Id);
        var privateState = world.GetPrivateState(player.Id);
        Assert.Contains(privateState.Dungeon!.Furnishings!.Concat(privateState.HomeStorage!), item => item.Id.Contains(":", StringComparison.Ordinal) && item.Properties.GetValueOrDefault("builtIn") == "false");
    }

    private static CanonicalEntity Building(string id, RegionId region, double x, double y) =>
        new(id, EntityKind.Building, new WorldPosition(region, x, y),
            new GeometryPoint[] { new(x - 5, y - 5), new(x + 5, y - 5), new(x + 5, y + 5), new(x - 5, y + 5), new(x - 5, y - 5) },
            new Dictionary<string, string> { ["building"] = "yes" });

    [Fact]
    public async Task EBikeBatteryExpiresAfterItsRemainingMileAndRemovesVehicle()
    {
        var configuration = new RealityConfiguration("ebike-test", "E-bike Test", 333, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "ebike.db")); await store.InitializeAsync(configuration);
        var center = new LocalTangentProjection(configuration.Area.Region).Project(configuration.Area.Center);
        await store.SaveCharacterAsync(configuration.Id, new PlayerState("rider", "Rider", center, EBikeRemainingMeters: .01));
        await store.SaveInventoryAsync(new InventoryState("rider", new[] { new ItemStack("eBike", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store); await world.InitializeAsync();
        var player = await world.JoinAsync("rider", "Rider"); player = await world.SetTravelModeAsync(player.Id, TravelMode.EBike);
        var movement = await world.MoveAsync(player.Id, new MoveRequest(1, 0, 1));
        Assert.NotNull(movement); Assert.Equal(TravelMode.Walk, movement!.Player.TravelMode); Assert.Contains("battery died", movement.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.GetPrivateState(player.Id).Inventory.Items, item => item.ItemType == "eBike");
    }

    [Fact]
    public async Task ClothingAndOffhandEquipmentDriveTemperatureAndLighting()
    {
        var configuration = new RealityConfiguration("climate-test", "Climate Test", 901, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "climate.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("climber", new[]
        {
            new ItemStack("warmHat", 1), new ItemStack("winterJacket", 1), new ItemStack("warmingPants", 1),
            new ItemStack("flashlight", 1), new ItemStack("lantern", 1), new ItemStack("laser", 1)
        }));
        var cold = new WeatherState("Clear", 0, -10, 0, 0, true, DateTimeOffset.UtcNow, "test");
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(cold), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("climber", "Climber");

        player = await world.SetEquipmentAsync(player.Id, "hat", "warmHat");
        player = await world.SetEquipmentAsync(player.Id, "shirt", "winterJacket");
        player = await world.SetEquipmentAsync(player.Id, "pants", "warmingPants");
        player = await world.SetEquipmentAsync(player.Id, "offhand", "lantern");
        Assert.Equal("warmHat", player.EquippedHat);Assert.Equal("winterJacket", player.EquippedShirt);Assert.Equal("warmingPants", player.EquippedPants);
        Assert.True(player.LanternOn);Assert.False(player.FlashlightOn);Assert.False(player.LaserOn);

        player = await world.SetEquipmentAsync(player.Id, "offhand", "laser");
        Assert.True(player.LaserOn);Assert.False(player.LanternOn);Assert.False(player.FlashlightOn);
        var changed = await world.AdvanceVitalsAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        player = Assert.Single(changed, item => item.Id == player.Id);
        Assert.InRange(player.BodyHeat, 49, 50);
        player = await world.SetGodModeAsync(player.Id, true);
        Assert.Equal(50, player.BodyHeat);
    }

    [Fact]
    public async Task GodModeConfigInventoryUsesHomeBeforeBackpack()
    {
        var configuration = new RealityConfiguration("config-inventory", "Config Inventory", 902, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var region = configuration.Area.Region;
        var store = new SqliteRealityStore(Path.Combine(_directory, "config-inventory.db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("config-account", "ConfigUser", "hash", "salt", "token", "config-player"), "ConfigUser");
        await store.SaveInventoryAsync(new InventoryState("config-player", new[] { new ItemStack("rock", 1) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(Building("config-home", region, 20, 20))), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("config-player", "ConfigUser", "config-account");
        await world.SetGodModeAsync(player.Id, true);

        var taken = await world.ConfigureInventoryItemAsync(player.Id, new ConfigureInventoryItemRequest("rock", "take"));
        Assert.Contains("Home inventory", taken.Message);
        var removedHome = await world.ConfigureInventoryItemAsync(player.Id, new ConfigureInventoryItemRequest("rock", "give"));
        Assert.Contains("Home inventory", removedHome.Message);
        Assert.Equal(1, removedHome.PrivateState.Inventory.Items.Single(item => item.ItemType == "rock").Quantity);

        var removedBackpack = await world.ConfigureInventoryItemAsync(player.Id, new ConfigureInventoryItemRequest("rock", "give"));
        Assert.Contains("backpack", removedBackpack.Message);
        Assert.DoesNotContain(removedBackpack.PrivateState.Inventory.Items, item => item.ItemType == "rock");
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ConfigureInventoryItemAsync(player.Id, new ConfigureInventoryItemRequest("rock", "give")));
    }

    private static bool IsInside(WorldPosition position, IReadOnlyList<GeometryPoint>? polygon)
    {
        if (polygon is null || polygon.Count < 3) return false;
        var inside = false;
        for (var index = 0; index < polygon.Count; index++)
        {
            var previous = index == 0 ? polygon.Count - 1 : index - 1;
            var a = polygon[index]; var b = polygon[previous];
            if ((a.Y > position.Y) != (b.Y > position.Y) && position.X < (b.X - a.X) * (position.Y - a.Y) / ((b.Y - a.Y) + double.Epsilon) + a.X) inside = !inside;
        }
        return inside;
    }

    private sealed class FixedGeographicProvider(params CanonicalEntity[] buildings) : IGeographicProvider
    {
        public string Name => "test";
        public Task<GeographicDataset> GetAreaAsync(GeographicArea area, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeographicDataset(Name, area, buildings, new[] { new ElevationSample(0, 0, 0) }, DateTimeOffset.UtcNow));
    }

    private sealed class FixedWeatherProvider(WeatherState? weather = null) : IWeatherProvider
    {
        public Task<WeatherState> GetCurrentAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default) =>
            Task.FromResult(weather ?? WeatherState.Unavailable);
    }
}
