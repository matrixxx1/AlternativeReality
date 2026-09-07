using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static readonly string[] StoredVehicleTypes = ["skateboard", "bike", "eBike", "dirtBike", "motorcycle", "inflatableRaft", "ufo"];

    private async Task<(RealityWorld World, SqliteRealityStore Store, PlayerState Player, string HomeOwner)> CreateVehicleStorageWorld(double eBikeRange = 1609.344)
    {
        var id = "vehicle-storage-" + Guid.NewGuid().ToString("N");
        var configuration = new RealityConfiguration(id, "Vehicle Storage", 333, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var center = new LocalTangentProjection(configuration.Area.Region).Project(configuration.Area.Center);
        var store = new SqliteRealityStore(Path.Combine(_directory, id + ".db"));
        await store.InitializeAsync(configuration);
        await store.CreateAccountAsync(new AccountRecord("garage-account", "Garage", "hash", "salt", "token", "garage-player"), "Garage");
        await store.SaveCharacterAsync(id, new PlayerState("garage-player", "Garage", center, EBikeRemainingMeters: eBikeRange));
        await store.SaveInventoryAsync(new InventoryState("garage-player", StoredVehicleTypes.Select(type => new ItemStack(type, 1)).Append(new("food", 1)).ToArray()));
        var homeOwner = $"home-items:{id}:garage-account";
        await store.SaveInventoryAsync(new InventoryState(homeOwner, [new("bike", 2), new("rock", 3)]));
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(Building("garage", configuration.Area.Region, 20, 20))), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        // Startup migration must work before any character joins.
        Assert.DoesNotContain((await store.LoadInventoryAsync("garage-player")).Items, item => StoredVehicleTypes.Contains(item.ItemType));
        Assert.Equal(3, (await store.LoadInventoryAsync(homeOwner)).Items.Single(item => item.ItemType == "bike").Quantity);
        var player = await world.JoinAsync("garage-player", "Garage", "garage-account");
        return (world, store, player, homeOwner);
    }

    [Fact]
    public async Task AllVehiclesAreWeightlessAtHomeAndAvailableOutdoorsWithoutTakingThem()
    {
        var (world, store, player, homeOwner) = await CreateVehicleStorageWorld();
        var state = world.GetPrivateState(player.Id);
        Assert.Equal(StoredVehicleTypes.OrderBy(type => type), state.OwnedVehicles!.OrderBy(type => type));
        Assert.DoesNotContain(state.Inventory.Items, item => StoredVehicleTypes.Contains(item.ItemType));
        foreach (var item in state.ServerConfiguration!.Items.Where(item => StoredVehicleTypes.Contains(item.ItemType)))
        {
            Assert.Equal(0, item.WeightPounds);
            Assert.False(item.CarriedInBackpack);
        }
        foreach (var mode in new[] { TravelMode.Skateboard, TravelMode.Bike, TravelMode.EBike, TravelMode.DirtBike, TravelMode.Motorcycle, TravelMode.Ufo })
            Assert.Equal(mode, (await world.SetTravelModeAsync(player.Id, mode)).TravelMode);
        var raft = await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.Raft));
        Assert.Contains("shallow water", raft.Message);
        Assert.Equal(3, (await store.LoadInventoryAsync(homeOwner)).Items.Single(item => item.ItemType == "rock").Quantity);
        Assert.Contains(state.Inventory.Items, item => item.ItemType == "food");
    }

    [Fact]
    public async Task VehicleMigrationIsIdempotentAndSharesOnlyWithinOwningAccount()
    {
        var (world, store, player, homeOwner) = await CreateVehicleStorageWorld();
        var restarted = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(Building("garage", world.Configuration.Area.Region, 20, 20))), new FixedWeatherProvider(), store);
        await restarted.InitializeAsync();
        await restarted.JoinAsync(player.Id, player.Name, "garage-account");
        Assert.Equal(3, (await store.LoadInventoryAsync(homeOwner)).Items.Single(item => item.ItemType == "bike").Quantity);
        var sibling = await restarted.JoinAsync("sibling", "Sibling", "garage-account");
        if (sibling.LocationId != "outdoor") await restarted.ExitDungeonAsync(sibling.Id);
        Assert.Equal(TravelMode.Bike, (await restarted.SetTravelModeAsync(sibling.Id, TravelMode.Bike)).TravelMode);
        var other = await restarted.JoinAsync("other", "Other", "other-account");
        Assert.Empty(restarted.GetPrivateState(other.Id).OwnedVehicles!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.SetTravelModeAsync(other.Id, TravelMode.Bike));
    }

    [Fact]
    public async Task AcquiredVehiclesGoDirectlyHomeAndDoNotReappearInBackpack()
    {
        var (world, store, player, homeOwner) = await CreateVehicleStorageWorld();
        var drop = await world.DropInventoryItemAsync(player.Id, new("bike", 1));
        Assert.Equal(2, (await store.LoadInventoryAsync(homeOwner)).Items.Single(item => item.ItemType == "bike").Quantity);
        await world.CollectLootAsync(player.Id, drop.Drop.Id);
        Assert.Equal(3, (await store.LoadInventoryAsync(homeOwner)).Items.Single(item => item.ItemType == "bike").Quantity);
        Assert.DoesNotContain(world.GetPrivateState(player.Id).Inventory.Items, item => StoredVehicleTypes.Contains(item.ItemType));
        Assert.DoesNotContain((await store.LoadInventoryAsync(player.Id)).Items, item => StoredVehicleTypes.Contains(item.ItemType));
    }

    [Fact]
    public async Task ExhaustedEBikeIsRemovedFromHomeAndTravelAvailability()
    {
        var (world, store, player, homeOwner) = await CreateVehicleStorageWorld(.01);
        await world.SetTravelModeAsync(player.Id, TravelMode.EBike);
        var movement = (await world.MoveAsync(player.Id, new(1, 0, 1)))!;
        Assert.Equal(TravelMode.Walk, movement.Player.TravelMode);
        Assert.DoesNotContain("eBike", world.GetPrivateState(player.Id).OwnedVehicles!);
        Assert.DoesNotContain((await store.LoadInventoryAsync(homeOwner)).Items, item => item.ItemType == "eBike");
    }
}
