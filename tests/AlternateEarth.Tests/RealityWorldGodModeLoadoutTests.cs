using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task GodModeLoadoutOffersEveryWeaponAmmoAndVehicleWithoutSavingThem()
    {
        var (world, player, _) = await ScubaFixture(gear: false);
        PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_inventories")[player.Id].Clear();
        var store = PhotoField<SqliteRealityStore>(world, "_store");
        await store.SaveInventoryAsync(new(player.Id, []));
        await world.SetGodModeAsync(player.Id, true);
        var state = world.GetPrivateState(player.Id);
        var loadout = state.GodModeLoadout!.ToDictionary(item => item.ItemType);
        var weapons = (string[])typeof(RealityWorld).GetField("WeaponPowerOrder", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        foreach (var weapon in weapons.Where(item => item != "probulator"))
        {
            Assert.Equal(1, loadout[weapon].Quantity);
            var ammo = state.ServerConfiguration!.Items.Single(item => item.ItemType == weapon).AmmoType;
            if (ammo is not null) Assert.Equal(1, loadout[ammo].Quantity);
            Assert.Equal(weapon, (await world.SetEquipmentAsync(player.Id, "weapon", weapon)).EquippedWeapon);
        }
        foreach (var vehicle in StoredVehicleTypes.Append("swimmies")) Assert.Contains(vehicle, state.OwnedVehicles!);
        Assert.All(loadout.Values, item => Assert.Equal(0, item.UnitWeightPounds));
        Assert.Empty(state.Inventory.Items);
        Assert.Empty((await store.LoadInventoryAsync(player.Id)).Items);
        await world.SetEquipmentAsync(player.Id, "weapon", "camera");
        await world.PhotographAsync(player.Id, null);
        var saved = (await store.LoadInventoryAsync(player.Id)).Items;
        Assert.Single(saved);Assert.StartsWith("photograph:", saved[0].ItemType);
        Assert.Equal(1, world.GetPrivateState(player.Id).GodModeLoadout!.Single(item => item.ItemType == "film").Quantity);
        var normal = await world.SetGodModeAsync(player.Id, false);
        Assert.Equal("fist", normal.EquippedWeapon);
        Assert.Empty(world.GetPrivateState(player.Id).GodModeLoadout!);
        Assert.Empty(world.GetPrivateState(player.Id).OwnedVehicles!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetEquipmentAsync(player.Id, "weapon", "rifle"));
    }

    [Fact]
    public async Task GodModeCanUseUnownedVehiclesAndReturnsToActualEquipmentWhenDisabled()
    {
        var (world, player, _) = await ScubaFixture(gear: false);
        foreach (var mode in new[] { TravelMode.Skateboard, TravelMode.Bike, TravelMode.EBike, TravelMode.DirtBike,
            TravelMode.Motorcycle, TravelMode.Raft, TravelMode.Swim, TravelMode.Ufo })
        {
            ScubaPlayer(world, player with { GodMode = true });
            Assert.Equal(mode, (await world.SetTravelModeAsync(player.Id, mode)).TravelMode);
            if (mode == TravelMode.Ufo)
            {
                Assert.True((await world.MoveAsync(player.Id, new(1, 0, 1)))!.Moved);
                var normal = await world.SetGodModeAsync(player.Id, false);
                Assert.Equal(TravelMode.Walk, normal.TravelMode);Assert.Equal("fist", normal.EquippedWeapon);
            }
        }
        ScubaPlayer(world, player with { GodMode = true });
        Assert.Equal(TravelMode.Scuba, (await world.SetTravelModeAsync(player.Id, TravelMode.Scuba)).TravelMode);
        Assert.True((await world.MoveAsync(player.Id, new(1, 0, 2)))!.Moved);
        var surfaced = await world.SetGodModeAsync(player.Id, false);
        Assert.Equal("outdoor", surfaced.LocationId);Assert.Equal(TravelMode.Walk, surfaced.TravelMode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.Ufo));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.Swim));
    }
}
