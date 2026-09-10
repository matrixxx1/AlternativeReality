using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task UpgradeLootTakesBestUnownedGearMaterialsAndCashAndPersistsQuality()
    {
        var (world, store, loot) = await CreateSelectiveLootWorld();
        typeof(RealityWorld).GetMethod("RemoveInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, ["collector", "rock", 98]);
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        drops.Clear();
        drops[loot.Id] = loot with { Items = [new("knife", 1, Quality: "Poor"), new("knife", 2, Quality: "Fine"),
            new("hat", 2), new("metal", 2), new("pen", 1), new("canadianMoney", 3)] };
        drops["best"] = loot with { Id = "best", MoneyCents = 0, Items = [new("knife", 1, Quality: "Legendary")] };
        var before = world.GetRetroBattleUpdate("collector").Player!.WalletCents;
        var result = await world.AutoTakeNearbyTreasureAsync("collector", loot.Id, false, upgradesOnly: true);
        var inventory = (await store.LoadInventoryAsync("collector")).Items;
        var knife = Assert.Single(inventory, item => item.ItemType == "knife");
        Assert.Equal(1, knife.Quantity);Assert.Equal("Legendary", knife.Quality);
        Assert.Equal(1, Assert.Single(inventory, item => item.ItemType == "hat").Quantity);
        Assert.Equal(2, Assert.Single(inventory, item => item.ItemType == "metal").Quantity);
        Assert.Equal(3, Assert.Single(inventory, item => item.ItemType == "canadianMoney").Quantity);
        Assert.DoesNotContain(inventory, item => item.ItemType == "pen");
        Assert.Equal(before + 123, result.Player.WalletCents);
        Assert.Equal(3, result.Items.Single(item => item.ItemType == "knife").Quantity);
        await world.AutoTakeNearbyTreasureAsync("collector", loot.Id, false, upgradesOnly: true);
        Assert.Equal(1, world.GetPrivateState("collector").Inventory.Items.Single(item => item.ItemType == "knife").Quantity);
        drops["upgrade"] = loot with { Id = "upgrade", MoneyCents = 0, Items = [new("knife", 1, Quality: "Godly"), new("hat", 1, Quality: "Fine")] };
        await world.AutoTakeNearbyTreasureAsync("collector", "upgrade", false, upgradesOnly: true);
        inventory = (await store.LoadInventoryAsync("collector")).Items;
        Assert.Equal("Godly", inventory.Single(item => item.ItemType == "knife").Quality);
        Assert.Equal("Fine", inventory.Single(item => item.ItemType == "hat").Quality);
        await world.TakeLootItemsAsync("collector", new(loot.Id, [new("knife", 1)]));
        Assert.Equal("Godly", world.GetPrivateState("collector").Inventory.Items.Single(item => item.ItemType == "knife").Quality);
    }

    [Fact]
    public async Task UpgradeLootRespectsCapacityAndStillCollectsCashAndFittingMaterials()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        typeof(RealityWorld).GetMethod("RemoveInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, ["collector", "rock", 1]);
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        drops[loot.Id] = loot with { Items = [new("rifle", 1, Quality: "Godly"), new("metal", 10)] };
        var before = world.GetRetroBattleUpdate("collector").Player!.WalletCents;
        var result = await world.AutoTakeNearbyTreasureAsync("collector", loot.Id, false, upgradesOnly: true);
        Assert.Equal(before + 123, result.Player.WalletCents);
        var inventory = world.GetPrivateState("collector").Inventory;
        Assert.DoesNotContain(inventory.Items, item => item.ItemType == "rifle");
        Assert.Equal(1, inventory.Items.Single(item => item.ItemType == "metal").Quantity);
        Assert.True(inventory.WeightPounds <= inventory.MaximumWeightPounds);
        Assert.Equal(9, result.Items.Single(item => item.ItemType == "metal").Quantity);
    }

    [Fact]
    public async Task UpgradeChestCollectsOnlyTheBestCopyAndLeavesLesserQualities()
    {
        var (world, _, _) = await CreateSelectiveLootWorld();
        await world.SetGodModeAsync("collector", true);
        var chest = world.GetPrivateState("collector").Chests!.First();
        await world.TeleportAsync("collector", new(chest.Position.X, chest.Position.Y, true));
        await world.OpenNearbyTreasureAsync("collector", chest.Id, true);
        var contents = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, ChestContentsState>>(world, "_chestContents");
        contents[chest.Id] = contents[chest.Id] with { Items = [new("sword", 2, Quality: "Poor"), new("sword", 2, Quality: "Epic")] };
        var taken = await world.AutoTakeNearbyTreasureAsync("collector", chest.Id, true, upgradesOnly: true);
        var sword = Assert.Single(world.GetPrivateState("collector").Inventory.Items, item => item.ItemType == "sword");
        Assert.Equal("Epic", sword.Quality);Assert.Equal(1, sword.Quantity);
        Assert.Equal(3, taken.Items.Single(item => item.ItemType == "sword").Quantity);
        Assert.Equal(2, contents[chest.Id].Items.Single(item => item.Quality == "Poor").Quantity);
    }

    [Fact]
    public async Task AutoTreasureLeavesOverweightItemsThenCollectsWhenSpaceIsAvailable()
    {
        var (world, store, loot) = await CreateSelectiveLootWorld();
        var blocked = await world.AutoTakeNearbyTreasureAsync("collector", loot.Id, false);
        Assert.Equal(10, blocked.Items.Single(item => item.ItemType == "rock").Quantity);
        Assert.Equal(98, world.GetPrivateState("collector").Inventory.Items.Single(item => item.ItemType == "rock").Quantity);
        var wallet = blocked.Player.WalletCents;
        typeof(RealityWorld).GetMethod("RemoveInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, ["collector", "rock", 20]);
        var taken = await world.AutoTakeNearbyTreasureAsync("collector", loot.Id, false);
        Assert.Empty(taken.Items);Assert.Contains(loot.Id, taken.RemovedLoot);Assert.Equal(wallet, taken.Player.WalletCents);
        Assert.Equal(88, (await store.LoadInventoryAsync("collector")).Items.Single(item => item.ItemType == "rock").Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AutoTakeNearbyTreasureAsync("collector", loot.Id, false));
    }

    [Fact]
    public async Task AutoTreasureCollectsNearbyChestsAndOwnEventRewardsButRejectsRemoteOrPrivateRewards()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        await world.SetGodModeAsync("collector", true);
        var chest = world.GetPrivateState("collector").Chests!.First();
        await world.TeleportAsync("collector", new(chest.Position.X, chest.Position.Y, true));
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        drops[loot.Id] = loot with { Position = chest.Position, DropKind = "eventReward", OwnerId = "collector" };
        drops["private"] = loot with { Id = "private", Position = chest.Position, DropKind = "eventReward", OwnerId = "other" };
        drops["remote"] = loot with { Id = "remote", Position = chest.Position with { X = chest.Position.X + 20 }, DropKind = "eventReward", OwnerId = "collector" };
        var taken = await world.AutoTakeNearbyTreasureAsync("collector", chest.Id, true);
        Assert.Empty(taken.Items);Assert.Contains(loot.Id, taken.RemovedLoot);
        Assert.DoesNotContain(world.GetPrivateState("collector").Chests!, item => item.Id == chest.Id);
        Assert.True(drops.ContainsKey("private"));Assert.True(drops.ContainsKey("remote"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AutoTakeNearbyTreasureAsync("collector", "private", false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AutoTakeNearbyTreasureAsync("collector", "remote", false));
    }

    [Fact]
    public async Task RemoteEventRewardsRemainSelectableAfterTheFirstDropIsEmptied()
    {
        var (world, _, loot) = await CreateSelectiveLootWorld();
        var drops = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, LootDropState>>(world, "_loot");
        drops.Clear();
        var reward = loot with { Id = "reward-a", DropKind = "eventReward", OwnerId = "collector", LocationId = "finished-dungeon", MoneyCents = 0, Items = [new("mapleSyrup", 1)] };
        drops[reward.Id] = reward;
        drops["reward-b"] = reward with { Id = "reward-b", Items = [new("canadianMoney", 2)] };
        var opened = await world.OpenNearbyTreasureAsync("collector", reward.Id, false);
        Assert.True(opened.IsEventReward);
        Assert.Equal(2, opened.Items.Count);
        var partial = await world.TakeNearbyTreasureAsync("collector", new(reward.Id,
            opened.Sources.Select(s => new TreasureSourceRequest(s.Id, s.Chest)).ToArray(), [new("mapleSyrup", 1)]));
        Assert.True(partial.IsEventReward);
        Assert.Equal("canadianMoney", Assert.Single(partial.Items).ItemType);
        var finished = await world.TakeNearbyTreasureAsync("collector", new(reward.Id,
            partial.Sources.Select(s => new TreasureSourceRequest(s.Id, s.Chest)).ToArray(), [new("canadianMoney", 2)]));
        Assert.Empty(finished.Items);
        Assert.True(finished.IsEventReward);
        Assert.Contains("Everything collected", finished.Message);
    }

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
