using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World, SqliteRealityStore Store, PlayerState Player)> CreateTravelEnergyWorld(
        TravelMode mode, double gas = 1, double ufoRange = 0, int crystals = 0)
    {
        var id = Guid.NewGuid().ToString("N");
        var config = new RealityConfiguration(id, "Travel energy", 123, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var center = new LocalTangentProjection(config.Area.Region).Project(config.Area.Center);
        var road = new CanonicalEntity("energy-road", EntityKind.Road, center,
            [new(center.X - 200, center.Y), new(center.X + 200, center.Y)],
            new Dictionary<string, string> { ["highway"] = "residential", ["width"] = "20" });
        var store = new SqliteRealityStore(Path.Combine(_directory, id + ".db"));
        await store.InitializeAsync(config);
        await store.SaveCharacterAsync(id, new PlayerState("rider", "Rider", center, DirtBikeGasGallons: gas, MotorcycleGasGallons: gas, UfoRemainingMeters: ufoRange));
        await store.SaveInventoryAsync(new InventoryState("rider", [new("bike", 1), new("skateboard", 1), new("dirtBike", 1), new("motorcycle", 1), new("ufo", 1), new("kryptonite", crystals), new("gallonOfGas", 1)]));
        var world = new RealityWorld(config, new DeterministicWorldGenerator(new FixedGeographicProvider(road)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("rider", "Rider");
        player = await world.SetTravelModeAsync(player.Id, mode);
        return (world, store, player);
    }

    [Theory]
    [InlineData(TravelMode.DirtBike, 76)]
    [InlineData(TravelMode.Motorcycle, 57)]
    public async Task GasUseMatchesActualDistanceAndEmptyVehiclesCanBeRefueled(TravelMode mode, double mpg)
    {
        var (world, store, player) = await CreateTravelEnergyWorld(mode);
        var idle = await world.MoveAsync(player.Id, new(0, 0, 1));
        Assert.Equal(1, mode == TravelMode.DirtBike ? idle!.Player.DirtBikeGasGallons : idle!.Player.MotorcycleGasGallons);
        var move = (await world.MoveAsync(player.Id, new(1, 0, 2)))!;
        var distance = player.Position.Distance2D(move.Player.Position);
        Assert.True(distance > .001);
        var expected = 1 - distance / 1609.344 / mpg;
        var saved = (await store.LoadCharacterAsync(world.Configuration.Id, player.Id))!;
        Assert.Equal(expected, mode == TravelMode.DirtBike ? saved.DirtBikeGasGallons : saved.MotorcycleGasGallons, 10);

        var (emptyWorld, _, emptyPlayer) = await CreateTravelEnergyWorld(mode, gas: 0);
        Assert.False((await emptyWorld.MoveAsync(emptyPlayer.Id, new(1, 0, 1)))!.Moved);
        var fueled = await emptyWorld.ConsumeItemAsync(emptyPlayer.Id, "gallonOfGas");
        Assert.Equal(1, mode == TravelMode.DirtBike ? fueled.DirtBikeGasGallons : fueled.MotorcycleGasGallons);
        Assert.True((await emptyWorld.MoveAsync(emptyPlayer.Id, new(1, 0, 2)))!.Moved);
    }

    [Theory]
    [InlineData(TravelMode.DirtBike, 76)]
    [InlineData(TravelMode.Motorcycle, 57)]
    public async Task LastDropOfGasCannotMoveBeyondItsRange(TravelMode mode, double mpg)
    {
        var (world, _, player) = await CreateTravelEnergyWorld(mode, gas: .02 / (1609.344 * mpg));
        var move = (await world.MoveAsync(player.Id, new(1, 0, 1)))!;
        Assert.Equal(.02, player.Position.Distance2D(move.Player.Position), 6);
        var again = (await world.MoveAsync(player.Id, new(1, 0, 2)))!;
        Assert.InRange(player.Position.Distance2D(again.Player.Position), 0, .020001);
    }

    [Fact]
    public async Task UfoConsumesKryptoniteOnlyForFlightAndKeepsPartialFuelOnReconnect()
    {
        var (world, store, player) = await CreateTravelEnergyWorld(TravelMode.Ufo, crystals: 2);
        await world.MoveAsync(player.Id, new(0, 0, 1));
        Assert.Equal(2, world.GetPrivateState(player.Id).Inventory.Items.Single(item => item.ItemType == "kryptonite").Quantity);
        var move = (await world.MoveAsync(player.Id, new(1, 0, 2)))!;
        var distance = player.Position.Distance2D(move.Player.Position);
        Assert.True(distance > .001);
        Assert.Equal(16093.44 - distance, move.Player.UfoRemainingMeters, 7);
        Assert.Equal(1, world.GetPrivateState(player.Id).Inventory.Items.Single(item => item.ItemType == "kryptonite").Quantity);
        Assert.Equal(move.Player.UfoRemainingMeters, (await store.LoadCharacterAsync(world.Configuration.Id, player.Id))!.UfoRemainingMeters);
        var rejoined = await world.JoinAsync(player.Id, player.Name);
        Assert.Equal(move.Player.UfoRemainingMeters, rejoined.UfoRemainingMeters);
        var next = (await world.MoveAsync(player.Id, new(1, 0, 3)))!;
        Assert.Equal(rejoined.UfoRemainingMeters - rejoined.Position.Distance2D(next.Player.Position), next.Player.UfoRemainingMeters, 7);
        Assert.Equal(1, world.GetPrivateState(player.Id).Inventory.Items.Single(item => item.ItemType == "kryptonite").Quantity);
    }

    [Fact]
    public async Task UfoUsesNextCrystalAtBoundaryAndStopsExactlyWhenFuelRunsOut()
    {
        var (world, _, player) = await CreateTravelEnergyWorld(TravelMode.Ufo, ufoRange: .02, crystals: 1);
        var move = (await world.MoveAsync(player.Id, new(1, 0, 1)))!;
        Assert.Equal(.02 + 16093.44 - player.Position.Distance2D(move.Player.Position), move.Player.UfoRemainingMeters, 7);
        Assert.DoesNotContain(world.GetPrivateState(player.Id).Inventory.Items, item => item.ItemType == "kryptonite");
        var (emptyWorld, _, empty) = await CreateTravelEnergyWorld(TravelMode.Ufo, ufoRange: .02);
        var last = (await emptyWorld.MoveAsync(empty.Id, new(1, 0, 1)))!;
        Assert.Equal(.02, empty.Position.Distance2D(last.Player.Position), 6);
        var stopped = (await emptyWorld.MoveAsync(empty.Id, new(1, 0, 2)))!;
        Assert.False(stopped.Moved);
        Assert.InRange(empty.Position.Distance2D(stopped.Player.Position), 0, .020001);
        await emptyWorld.SetGodModeAsync(empty.Id, true);
        var god = (await emptyWorld.MoveAsync(empty.Id, new(1, 0, 3)))!;
        Assert.True(god.Moved);
        Assert.Equal(stopped.Player.UfoRemainingMeters, god.Player.UfoRemainingMeters);
    }

    [Theory]
    [InlineData(TravelMode.Bike)]
    [InlineData(TravelMode.Skateboard)]
    public async Task HumanPoweredVehiclesDrainStaminaOnlyWhileMovingAndSlowWhenTired(TravelMode mode)
    {
        var (world, _, player) = await CreateTravelEnergyWorld(mode);
        var idle = (await world.MoveAsync(player.Id, new(0, 0, 1)))!;
        Assert.Equal(player.Stamina, idle.Player.Stamina);
        var move = (await world.MoveAsync(player.Id, new(1, 0, 2)))!;
        Assert.True(move.Moved);
        Assert.Equal(mode, move.Player.TravelMode);
        Assert.True(move.Player.Stamina < player.Stamina);
        Assert.True(world.ConfiguredSpeedMetersPerSecond(player with { Stamina = 0 }, TerrainType.Road) < world.ConfiguredSpeedMetersPerSecond(player, TerrainType.Road));
        var god = await world.SetGodModeAsync(player.Id, true);
        Assert.Equal(god.Stamina, (await world.MoveAsync(player.Id, new(1, 0, 3)))!.Player.Stamina);
    }
}
