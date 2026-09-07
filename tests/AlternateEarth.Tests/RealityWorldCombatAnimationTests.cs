using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Theory]
    [InlineData("rocketLauncher", true)]
    [InlineData("rifle", true)]
    [InlineData("pistol", false)]
    public async Task WorldObjectShotsProduceCombatEventsAtTheImpactSurface(string weapon, bool buildingTarget)
    {
        var configuration = new RealityConfiguration("object-animation", "Object animation", 23,
            new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var building = Building("animation-building", configuration.Area.Region, 20, 20);
        var car = new CanonicalEntity("animation-car", EntityKind.Vehicle, new WorldPosition(configuration.Area.Region, 8, 20),
            Array.Empty<GeometryPoint>(), new Dictionary<string, string> { ["subtype"] = "car" });
        var store = new SqliteRealityStore(Path.Combine(_directory, "object-animation.db"));
        await store.InitializeAsync(configuration);
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building, car)), new FixedWeatherProvider(), store);
        await world.InitializeAsync();
        await store.SaveInventoryAsync(new InventoryState("shooter", new[] { new ItemStack("ufo", 1, CarriedInBackpack: false) }));
        await world.JoinAsync("shooter", "Shooter");
        await world.SetGodModeAsync("shooter", true);
        var shooter = await world.TeleportAsync("shooter", new(0, 20, true));
        await world.SetEquipmentAsync("shooter", "weapon", weapon);
        var result = await world.AttackWorldObjectAsync("shooter", buildingTarget ? building.Id : car.Id);
        var combat = Assert.IsType<CombatEvent>(result.Combat);
        Assert.Equal(weapon, combat.Weapon);
        Assert.Equal(shooter.Position, combat.Start);
        Assert.Equal(buildingTarget ? building.Id : car.Id, combat.TargetId);
        Assert.True(combat.Hit);
        Assert.True(combat.Damage > 0);
        Assert.Equal(buildingTarget ? 15 : 8, combat.End.X, 3);
        Assert.Equal(20, combat.End.Y, 3);
        if (buildingTarget) Assert.NotEqual(building.Position, combat.End);
        var cooldown = await Assert.ThrowsAsync<InvalidOperationException>(() => world.AttackWorldObjectAsync("shooter", buildingTarget ? building.Id : car.Id));
        Assert.Contains("not ready", cooldown.Message);
        await world.SetTravelModeAsync("shooter", TravelMode.Ufo);
        world.ToggleProbulator("shooter", new ToggleProbulatorRequest(0, 0));
        Assert.Equal("Probulator inactive", world.CancelPlayerCommand("shooter")?.StatusEffect);
        Assert.Null(world.CancelPlayerCommand("shooter"));
    }
}
