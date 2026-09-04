using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"alternate-earth-tests-{Guid.NewGuid():N}");
    private readonly RealityConfiguration _reality = new(
        "persistence-test", "Persistence Test", 42,
        new GeographicArea(new GeoCoordinate(45.6387, -122.6615), 2000));

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
    public async Task EntitySurvivesStoreReopenAndCanBeTombstoned()
    {
        var database = Path.Combine(_directory, "reality.db");
        var originalStore = new SqliteRealityStore(database);
        await originalStore.InitializeAsync(_reality);
        var entity = new CanonicalEntity(
            "placed:test", EntityKind.PlayerStructure,
            new WorldPosition(_reality.Area.Region, 10.5, -4.0),
            Array.Empty<GeometryPoint>(),
            new Dictionary<string, string> { ["objectType"] = "wall" },
            IsBaseEntity: false);
        await originalStore.SaveEntityAsync(_reality.Id, entity);

        var reopenedStore = new SqliteRealityStore(database);
        var reloaded = Assert.Single(await reopenedStore.LoadActiveEntitiesAsync(_reality.Id));
        Assert.Equal(entity.Id, reloaded.Id);
        Assert.Equal(entity.Position, reloaded.Position);
        Assert.Equal("wall", reloaded.Properties["objectType"]);

        await reopenedStore.RemoveEntityAsync(_reality.Id, reloaded);
        Assert.Empty(await new SqliteRealityStore(database).LoadActiveEntitiesAsync(_reality.Id));
    }

    [Fact]
    public async Task CharacterStateBelongsToRealityAndPersists()
    {
        var store = new SqliteRealityStore(Path.Combine(_directory, "characters.db"));
        await store.InitializeAsync(_reality);
        var energyBoostUntil = DateTimeOffset.UtcNow.AddMinutes(10);
        var energyCrashUntil = energyBoostUntil.AddMinutes(5);
        var probedUntil = DateTimeOffset.UtcNow.AddMinutes(5);
        var candleUntil = DateTimeOffset.UtcNow.AddSeconds(45);
        var player = new PlayerState("character-1", "Ada", new WorldPosition(_reality.Area.Region, 15, 25), 7,
            TerrainType.Sidewalk, 1.5, 9.75, 10, TravelMode.Skateboard, 6.5, 10, FlashlightOn:true,MagicRunningShoesOn:true,HatOn:false,DirtBikeGasGallons:1.25,MotorcycleGasGallons:3.5,EquippedWeapon:"crossbow",BodyHeat:72.5,EquippedHat:"warmHat",EquippedShirt:"winterJacket",EquippedPants:"warmingPants",
            EnergyDrinkBoostUntilUtc:energyBoostUntil,EnergyDrinkCrashUntilUtc:energyCrashUntil,ProbedUntilUtc:probedUntil,CandleUntilUtc:candleUntil);

        await store.SaveCharacterAsync(_reality.Id, player);
        var loaded = await new SqliteRealityStore(Path.Combine(_directory, "characters.db")).LoadCharacterAsync(_reality.Id, player.Id);

        Assert.NotNull(loaded);
        Assert.Equal(player.Id, loaded.Id);
        Assert.Equal(player.Name, loaded.Name);
        Assert.Equal(player.Position, loaded.Position);
        Assert.Equal(player.Version, loaded.Version);
        Assert.Equal(9.75, loaded.HealthHearts);
        Assert.Equal(TravelMode.Skateboard, loaded.TravelMode);
        Assert.Equal(6.5, loaded.Stamina);
        Assert.True(loaded.FlashlightOn);Assert.False(loaded.LanternOn);Assert.False(loaded.LaserOn);Assert.True(loaded.MagicRunningShoesOn);Assert.False(loaded.MagicHikingShoesOn);Assert.False(loaded.HatOn);
        Assert.Equal(1.25, loaded.DirtBikeGasGallons);Assert.Equal(3.5, loaded.MotorcycleGasGallons);
        Assert.Equal("crossbow", loaded.EquippedWeapon);
        Assert.Equal(72.5, loaded.BodyHeat);Assert.Equal("warmHat", loaded.EquippedHat);Assert.Equal("winterJacket", loaded.EquippedShirt);Assert.Equal("warmingPants", loaded.EquippedPants);
        Assert.Equal(energyBoostUntil, loaded.EnergyDrinkBoostUntilUtc);Assert.Equal(energyCrashUntil, loaded.EnergyDrinkCrashUntilUtc);
        Assert.Equal(probedUntil, loaded.ProbedUntilUtc);
        Assert.Equal(candleUntil, loaded.CandleUntilUtc);
    }

    [Fact]
    public async Task AccountNamesAreUniqueAndServerValidated()
    {
        var store=new SqliteRealityStore(Path.Combine(_directory,"accounts.db"));await store.InitializeAsync(_reality);var accounts=new AccountService(store);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>accounts.SetupOrLoginAsync("ab","password"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>accounts.SetupOrLoginAsync("bad_name","password"));
        var created=await accounts.SetupOrLoginAsync("Player-1","password");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>accounts.SetupOrLoginAsync("player-1","wrong-password"));
        var resumed=await accounts.SetupOrLoginAsync("player-1","password");
        Assert.Equal(created.AccountId,resumed.AccountId);Assert.Equal(created.CharacterId,resumed.CharacterId);Assert.NotEqual(created.SessionToken,resumed.SessionToken);
    }
}
