using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task NearbyTreasureCombinesDropsAndCollectsCashBeforeSelectingOverweightItems()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        drops["second"] = loot with { Id = "second", MoneyCents = 200 };
        drops["far"] = loot with { Id = "far", Position = loot.Position with { X = loot.Position.X + 20 } };
        drops["private"] = loot with { Id = "private", DropKind = "eventReward", OwnerId = "someone-else" };
        var before = world.GetRetroBattleUpdate("collector").Player!.WalletCents;
        var opened = await world.OpenNearbyTreasureAsync("collector", loot.Id, false);
        Assert.Equal(2, opened.Sources.Count(s => !s.Chest));
        Assert.Equal(20, opened.Items.Single(i => i.ItemType == "rock").Quantity);
        Assert.Equal(before + 323, opened.Player.WalletCents);
        Assert.Equal(0, world.OpenLoot("collector", loot.Id).MoneyCents);
        var again = await world.OpenNearbyTreasureAsync("collector", loot.Id, false);
        Assert.Equal(opened.Player.WalletCents, again.Player.WalletCents);
        var request = new TakeNearbyTreasureRequest(loot.Id, opened.Sources.Select(s => new TreasureSourceRequest(s.Id, s.Chest)).ToArray(), [new("rock", 20)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.TakeNearbyTreasureAsync("collector", request));
        Assert.Equal(20, drops.Values.Where(l => l.Id is "test-loot" or "second").SelectMany(l => l.Items).Where(i => i.ItemType == "rock").Sum(i => i.Quantity));
        var partial = await world.TakeNearbyTreasureAsync("collector", request with { Items = [new("rock", 1)] });
        Assert.Equal(19, partial.Items.Single(i => i.ItemType == "rock").Quantity);
        Assert.Equal(99, world.GetPrivateState("collector").Inventory.Items.Single(i => i.ItemType == "rock").Quantity);
    }

    [Fact]
    public async Task GodBackpackHasUnlimitedWeightAndWeaponTypesRemainUnlimitedOutsideGodMode()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        await world.SetGodModeAsync("collector", true);
        drops[loot.Id] = loot with { Items = [new("rock", 1000), new("knife", 1), new("sword", 1), new("hockeyStick", 1), new("iceSkate", 1)] };
        var open = await world.OpenNearbyTreasureAsync("collector", loot.Id, false);
        var result = await world.TakeNearbyTreasureAsync("collector", new(loot.Id, [new(loot.Id, false)], open.Items.Select(i => new PurchaseLine(i.ItemType, i.Quantity)).ToArray()));
        var inventory = world.GetPrivateState("collector").Inventory;
        Assert.Null(inventory.MaximumWeightPounds);
        Assert.Null(inventory.MaximumWeaponSlots);
        Assert.True(inventory.WeightPounds > 500);
        Assert.True(inventory.WeaponSlotsUsed > 3);
        await world.DropInventoryItemAsync("collector", new("rock", 1098));
        await world.SetGodModeAsync("collector", false);
        inventory = world.GetPrivateState("collector").Inventory;
        Assert.NotNull(inventory.MaximumWeightPounds);
        Assert.Null(inventory.MaximumWeaponSlots);
        drops["more"] = loot with { Id = "more", MoneyCents = 0, Items = [new("slingshot", 1)] };
        await world.TakeLootItemsAsync("collector", new("more", [new("slingshot", 1)]));
        Assert.Contains(world.GetPrivateState("collector").Inventory.Items, i => i.ItemType == "slingshot");
    }

    [Fact]
    public async Task NearbyTreasureIncludesChestAndRejectsStaleBatchBeforeTakingAnyItems()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        await world.SetGodModeAsync("collector", true);
        var chest = world.GetPrivateState("collector").Chests!.First();
        await world.TeleportAsync("collector", new(chest.Position.X, chest.Position.Y, true));
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        drops[loot.Id] = loot with { Position = chest.Position };
        var opened = await world.OpenNearbyTreasureAsync("collector", chest.Id, true);
        Assert.Contains(opened.Sources, s => s.Chest && s.Id == chest.Id);
        Assert.Contains(opened.Sources, s => !s.Chest && s.Id == loot.Id);
        var reopened = await world.OpenNearbyTreasureAsync("collector", loot.Id, false);
        Assert.Equal(opened.Player.WalletCents, reopened.Player.WalletCents);
        var quantity = world.GetPrivateState("collector").Inventory.Items.Single(i => i.ItemType == "rock").Quantity;
        drops.TryRemove(loot.Id, out _);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.TakeNearbyTreasureAsync("collector", new(chest.Id,
            opened.Sources.Select(s => new TreasureSourceRequest(s.Id, s.Chest)).ToArray(), [new("rock", 1)])));
        Assert.Equal(quantity, world.GetPrivateState("collector").Inventory.Items.Single(i => i.ItemType == "rock").Quantity);
    }

    [Theory]
    [InlineData("canadianGas")]
    [InlineData("molotovFire")]
    [InlineData("areaHazard")]
    public async Task GasAndFireDamageNeverInterruptAttackWithFear(string weapon)
    {
        var (world, _, npc) = await ImpressionWorld(new ProbulatorTestClock());
        await world.SetGodModeAsync("pilot", false);
        var player = world.GetRetroBattleUpdate("pilot").Player!;
        ImpressionActors(world)[npc.Id] = npc with { MaximumHealthHearts = player.MaximumHealthHearts * 10 };
        world.ProgressionRoll = () => 0;
        var hit = new CombatEvent(npc.Id, player.Id, weapon, npc.Position, player.Position, true, 1, false, "Hazard");
        Assert.False(world.ResolveCombatFear(hit).FleeInFear);
        Assert.True(world.ResolveCombatFear(hit with { Weapon = "fist" }).FleeInFear);
    }

    [Fact]
    public async Task MooseSyrupHalvesSpeedWithoutDamageExpiresAndCanBeCollected()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        var player = world.GetRetroBattleUpdate("collector").Player!;
        var moose = new ActorState("moose", EntityKind.Animal, "angryMoose", "Angry moose", player.Position);
        var speed = world.ConfiguredSpeedMetersPerSecond(player, TerrainType.Pavement);
        typeof(RealityWorld).GetMethod("MooseSyrupAttack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(world, [moose, player, DateTimeOffset.UtcNow]);
        var puddle = Assert.Single(world.GetPrivateState(player.Id).Loot!, l => l.DropKind == "mapleSyrupPuddle");
        Assert.InRange((puddle.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds, 4, 5);
        Assert.Equal(speed / 2, world.ConfiguredSpeedMetersPerSecond(player, TerrainType.Pavement), 6);
        Assert.Equal(player.HealthHearts, world.GetRetroBattleUpdate(player.Id).Player!.HealthHearts);
        Assert.Equal(speed, world.ConfiguredSpeedMetersPerSecond(player with { Position = player.Position with { X = player.Position.X + 3 } }, TerrainType.Pavement), 6);
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        drops[puddle.Id] = puddle with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
        Assert.Equal(speed, world.ConfiguredSpeedMetersPerSecond(player, TerrainType.Pavement), 6);
        drops[puddle.Id] = puddle;
        await world.TakeLootItemsAsync(player.Id, new(puddle.Id, [new("mapleSyrup", 1)]));
        Assert.Equal(world.ConfiguredSpeedMetersPerSecond(player with { Position = player.Position with { X = player.Position.X + 3 } }, TerrainType.Pavement), world.ConfiguredSpeedMetersPerSecond(player, TerrainType.Pavement), 6);
        Assert.Contains(world.GetPrivateState(player.Id).Inventory.Items, i => i.ItemType == "mapleSyrup" && i.Quantity == 1);
    }
}
