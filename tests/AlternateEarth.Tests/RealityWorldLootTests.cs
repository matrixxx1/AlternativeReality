using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World, SqliteRealityStore Store, LootDropState Loot)> CreateSelectiveLootWorld(bool tombstone = false, bool expired = false)
    {
        var configuration = new RealityConfiguration("selective-loot", "Selective loot", 23,
            new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var store = new SqliteRealityStore(Path.Combine(_directory, "selective-loot.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("collector", new[] { new ItemStack("rock", 98) }));
        var generator = new DeterministicWorldGenerator(new FixedGeographicProvider());
        var world = new RealityWorld(configuration, generator, new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("collector", "Collector");
        var loot = new LootDropState("test-loot", player.Position, player.LocationId, 123,
            new[] { new ItemStack("rock", 10, Quality: "Fine"), new ItemStack("ballBearing", 5) },
            expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.MaxValue, tombstone ? "tombstone" : "loot");
        await store.SavePersistentLootAsync(configuration.Id, loot);
        world = new RealityWorld(configuration, generator, new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        await world.JoinAsync("collector", "Collector");
        return (world, store, loot);
    }

    [Fact]
    public async Task SelectiveLootCanOpenFullBackpackTakeOneAndDropToMakeRoom()
    {
        var (world, store, loot) = await CreateSelectiveLootWorld();
        var before = world.CreateSnapshot().Players.Single(player => player.Id == "collector").WalletCents;
        Assert.Equal(10, world.OpenLoot("collector", loot.Id).Items.Single(item => item.ItemType == "rock").Quantity);
        Assert.Equal(before, world.CreateSnapshot().Players.Single(player => player.Id == "collector").WalletCents);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.TakeLootItemsAsync("collector", new(loot.Id, new[] { new PurchaseLine("rock", 10) })));
        Assert.Equal(123, world.OpenLoot("collector", loot.Id).MoneyCents);
        var taken = await world.TakeLootItemsAsync("collector", new(loot.Id, new[] { new PurchaseLine("ROCK", 1) }));
        Assert.Equal(before + 123, taken.Player.WalletCents);
        Assert.Equal(9, taken.Remaining!.Items.Single(item => item.ItemType == "rock").Quantity);
        Assert.Equal("Fine", taken.Remaining.Items.Single(item => item.ItemType == "rock").Quality);
        Assert.Equal(0, taken.Remaining.MoneyCents);
        Assert.Equal(99, (await store.LoadInventoryAsync("collector")).Items.Single(item => item.ItemType == "rock").Quantity);
        var dropped = await world.DropInventoryItemAsync("collector", new("rock", 10));
        var finished = await world.TakeLootItemsAsync("collector", new(loot.Id, new[] { new PurchaseLine("rock", 9), new PurchaseLine("ballBearing", 5) }));
        Assert.Null(finished.Remaining);
        Assert.Equal(before + 123, finished.Player.WalletCents);
        Assert.Throws<InvalidOperationException>(() => world.OpenLoot("collector", loot.Id));
        Assert.Equal(10, Assert.Single(world.OpenLoot("collector", dropped.Drop.Id).Items).Quantity);
    }

    [Fact]
    public async Task SelectiveTombstonePersistsRemainderAndSupportsCashOnlyAtCapacity()
    {
        var (world, store, loot) = await CreateSelectiveLootWorld(tombstone: true);
        var cash = await world.TakeLootItemsAsync("collector", new(loot.Id, Array.Empty<PurchaseLine>()));
        Assert.Equal(2, cash.Remaining!.Items.Count);
        Assert.Equal(0, cash.Remaining.MoneyCents);
        var taken = await world.TakeLootItemsAsync("collector", new(loot.Id, new[] { new PurchaseLine("ballBearing", 2) }));
        var persisted = Assert.Single(await store.LoadPersistentLootAsync(world.Configuration.Id));
        Assert.Equal(3, persisted.Items.Single(item => item.ItemType == "ballBearing").Quantity);
        Assert.Equal(0, persisted.MoneyCents);
        var reloaded = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store);
        await reloaded.InitializeAsync();
        await reloaded.JoinAsync("collector", "Collector");
        Assert.Equal(3, reloaded.OpenLoot("collector", loot.Id).Items.Single(item => item.ItemType == "ballBearing").Quantity);
        await reloaded.DropInventoryItemAsync("collector", new("rock", 20));
        Assert.Null((await reloaded.TakeLootItemsAsync("collector", new(loot.Id, new[] { new PurchaseLine("rock", 10), new PurchaseLine("ballBearing", 3) }))).Remaining);
        Assert.Empty(await store.LoadPersistentLootAsync(world.Configuration.Id));
    }

    [Fact]
    public async Task SelectiveLootRejectsInvalidAndStaleQuantitiesWithoutLosingItems()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        foreach (var lines in new[]
        {
            new[] { new PurchaseLine("rock", -1) }, new[] { new PurchaseLine("rock", int.MaxValue) },
            new[] { new PurchaseLine("missing", 1) }, new[] { new PurchaseLine("rock", 6), new PurchaseLine("ROCK", 5) }
        }) await Assert.ThrowsAsync<InvalidOperationException>(() => world.TakeLootItemsAsync("collector", new(loot.Id, lines)));
        async Task<bool> TakeBearings()
        {
            try { await world.TakeLootItemsAsync("collector", new(loot.Id, new[] { new PurchaseLine("ballBearing", 5) })); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Single(await Task.WhenAll(TakeBearings(), TakeBearings()), success => success);
        Assert.Equal(5, world.GetPrivateState("collector").Inventory.Items.Single(item => item.ItemType == "ballBearing").Quantity);
        Assert.Equal(10, Assert.Single(world.OpenLoot("collector", loot.Id).Items).Quantity);
    }

    [Fact]
    public async Task SelectiveLootRejectsExpiredAndDistantTreasure()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld(expired: true);
        Assert.Throws<InvalidOperationException>(() => world.OpenLoot("collector", loot.Id));
        var dropped = await world.DropInventoryItemAsync("collector", new("rock", 1));
        await world.SetGodModeAsync("collector", true);
        await world.TeleportAsync("collector", new(dropped.Drop.Position.X + 20, dropped.Drop.Position.Y + 20, true));
        Assert.Throws<InvalidOperationException>(() => world.OpenLoot("collector", dropped.Drop.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.TakeLootItemsAsync("collector", new(dropped.Drop.Id, new[] { new PurchaseLine("rock", 1) })));
    }

    [Fact]
    public async Task DungeonDroppedTreasureSupportsPartialPickup()
    {
        var configuration = new RealityConfiguration("dungeon-loot", "Dungeon Loot", 23, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var building = SizedBuilding("loot-dungeon", configuration.Area.Region, 30, 30, 30, 20) with
        { Properties = new Dictionary<string, string> { ["questItem"] = "true", ["building"] = "yes" } };
        var store = new SqliteRealityStore(Path.Combine(_directory, "dungeon-loot.db"));
        await store.InitializeAsync(configuration);
        await store.SaveInventoryAsync(new InventoryState("crafter", new[] { new ItemStack("rock", 4) }));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        await world.JoinAsync("crafter", "Collector");
        await world.SetGodModeAsync("crafter", true);
        var door = world.CreateSnapshot().BaseEntities.Single(item => item.Kind == EntityKind.Door);
        await world.TeleportAsync("crafter", new(door.Position.X, door.Position.Y, true));
        await world.EnterDungeonAsync("crafter", door.Id);
        var dropped = await world.DropInventoryItemAsync("crafter", new("rock", 2));
        Assert.NotEqual("outdoor", dropped.Drop.LocationId);
        var taken = await world.TakeLootItemsAsync("crafter", new(dropped.Drop.Id, new[] { new PurchaseLine("rock", 1) }));
        Assert.Equal(1, Assert.Single(taken.Remaining!.Items).Quantity);
        await world.ExitDungeonAsync("crafter");
        Assert.Throws<InvalidOperationException>(() => world.OpenLoot("crafter", dropped.Drop.Id));
    }
}
