using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static void NorthernField(RealityWorld world, string name, object value) => typeof(RealityWorld).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, value);
    private static async Task NorthernCall(RealityWorld world, string name, params object[] args) => await (Task)typeof(RealityWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, args)!;

    [Fact]
    public async Task NorthernKillsAreSharedDeduplicatedAndRequireBothBosses()
    {
        var (world, id, _, clock) = await RetroFixture();
        var player = world.GetRetroBattleUpdate(id).Player!;
        world.StartInversion("northern", player, clock.GetUtcNow());
        var actors = ImpressionActors(world);
        var invaders = actors.Values.Where(a => a.Subtype == "canadian").ToArray();
        Assert.Equal(60, invaders.Length); Assert.Equal(42, invaders.Count(a => a.EquippedWeapon == "fist"));
        Assert.Equal(12, invaders.Count(a => a.EquippedWeapon == "hockeyStick")); Assert.Equal(6, invaders.Count(a => a.EquippedWeapon == "iceSkate"));
        var kills = PhotoField<ConcurrentQueue<(string Player, ActorState Actor)>>(world, "_inversionKills");
        for (var n = 0; n < 49; n++) { actors.TryRemove(invaders[n].Id, out _); kills.Enqueue((n % 2 == 0 ? id : "teammate", invaders[n])); }
        kills.Enqueue((id, invaders[0]));
        await world.AdvanceInversionsAsync(TimeSpan.Zero);
        Assert.Equal(49, world.GetInversionView(id).Active!.Kills); Assert.DoesNotContain(actors.Values, a => a.Subtype == "canadianBoss");
        actors.TryRemove(invaders[49].Id, out _); kills.Enqueue(("teammate", invaders[49]));
        await world.AdvanceInversionsAsync(TimeSpan.Zero);
        var bosses = actors.Values.Where(a => a.Subtype == "canadianBoss").ToArray(); Assert.Equal(2, bosses.Length);
        Assert.Contains(bosses, a => a.Name == "Mecha Terry"); Assert.Contains(bosses, a => a.Name == "Mecha Phil");
        actors.TryRemove(bosses[0].Id, out _); kills.Enqueue((id, bosses[0]));
        await world.AdvanceInversionsAsync(TimeSpan.Zero);
        Assert.NotNull(world.GetInversionView(id).Active); Assert.Single(actors.Values, a => a.Subtype == "canadianBoss");
        Assert.DoesNotContain(world.GetPrivateState(id).Loot!, l => l.DropKind == "eventReward");
        actors.TryRemove(bosses[1].Id, out _); kills.Enqueue(("teammate", bosses[1]));
        world.ProgressionRoll = () => 0;
        await world.AdvanceInversionsAsync(TimeSpan.Zero);
        Assert.Null(world.GetInversionView(id).Active);
        var reward = Assert.Single(world.GetPrivateState(id).Loot!, l => l.DropKind == "eventReward");
        Assert.Equal(0, reward.MoneyCents);
        Assert.Equal(new[] { "canadianMoney", "hockeyStick", "mapleSyrup", "recipe:hockeyStick", "recipe:mapleSyrup" }, reward.Items.Select(i => i.ItemType).OrderBy(i => i).ToArray());
    }

    [Fact]
    public async Task CanadianGasHurtsPlayersAndNpcsButNeverCanadiansAndBossesOnlyFart()
    {
        var (world, id, _, clock) = await RetroFixture();
        var player = await world.SetGodModeAsync(id, false);
        world.StartInversion("northern", player, clock.GetUtcNow()); clock.Advance(7);
        var actors = ImpressionActors(world); actors.Clear();
        var e = world.GetInversionView(id).Active!;
        var canadian = new ActorState(e.BossId, EntityKind.Npc, "canadianBoss", "Mecha Terry", player.Position, HealthHearts: 250, MaximumHealthHearts: 250, EventName: e.Name);
        actors[canadian.Id] = canadian;
        actors["immune"] = canadian with { Id = "immune", Subtype = "canadian", EquippedWeapon = "fist", Position = player.Position with { X = player.Position.X + 1 } };
        actors["bystander"] = new("bystander", EntityKind.Npc, "resident", "Bystander", player.Position);
        await world.AdvanceInversionsAsync(TimeSpan.Zero);
        var cloud = Assert.Single(world.GetInversionView(id).Active!.Patches, p => p.Radius == 6);
        Assert.Equal("canadianGas", cloud.Kind); Assert.NotNull(actors[canadian.Id].FartUntilUtc);
        world.TakeInversionCombat(); clock.Advance(1); await world.AdvanceInversionsAsync(TimeSpan.Zero);
        Assert.True(world.GetRetroBattleUpdate(id).Player!.HealthHearts < player.HealthHearts);
        Assert.True(actors["bystander"].HealthHearts < 5); Assert.Equal(250, actors[canadian.Id].HealthHearts); Assert.Equal(250, actors["immune"].HealthHearts);
        var genericCombat = await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.DoesNotContain(genericCombat.Combat, c => c.AttackerId == canadian.Id || c.AttackerId == "immune");
        Assert.DoesNotContain(world.TakeInversionCombat(), c => c.AttackerId == canadian.Id && c.Weapon is "bullet" or "stomp" or "fist");
    }

    [Fact]
    public async Task MapleSyrupConsumesOneAndBoostExpiresWithoutChangingAllocatedStats()
    {
        var (world, id, _, clock) = await RetroFixture();
        var before = world.GetProgression(id);
        typeof(RealityWorld).GetMethod("AddInventory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [id, "mapleSyrup", 2, null]);
        await world.ConsumeItemAsync(id, "mapleSyrup");
        Assert.Equal(1, world.GetPrivateState(id).Inventory.Items.Single(i => i.ItemType == "mapleSyrup").Quantity);
        Assert.True(world.GetProgression(id).VisionMultiplier > before.VisionMultiplier);
        Assert.Equal(before.Stats, world.GetProgression(id).Stats);
        clock.Advance(301); Assert.Equal(before.VisionMultiplier, world.GetProgression(id).VisionMultiplier);
    }

    [Fact]
    public async Task HaneyAlwaysSellsCrudeAtMaximumAndCanadianMoneySellsForAPenny()
    {
        var (world, id, _, _) = await RetroFixture(); var player = world.GetRetroBattleUpdate(id).Player!;
        var merchant = new ActorState("haney-test", EntityKind.Npc, "haney", "Eustace Charleston Haney", player.Position, IsMerchant: true);
        ImpressionActors(world)[merchant.Id] = merchant;
        PhotoField<ConcurrentDictionary<(string Player,string Actor),double>>(world, "_relationships")[(id, merchant.Id)] = 100;
        typeof(RealityWorld).GetMethod("AddInventory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [id, "canadianMoney", 12, null]);
        var items = PhotoField<ConcurrentDictionary<string,ItemConfiguration>>(world, "_itemConfigurations");
        var quote = world.RequestTrade(id, merchant.Id);
        Assert.All(quote.Offers, o => { Assert.Equal(items[o.ItemType].MaximumPriceCents, o.UnitPriceCents); Assert.Equal("Crude", o.Properties!["quality"]); });
        Assert.Equal(1, quote.BuyOffers!.Single(o => o.ItemType == "canadianMoney").UnitPriceCents);
        var sale = await world.ConfirmTradeAsync(id, new(merchant.Id, [], [new("canadianMoney", 12)]));
        Assert.Equal(player.WalletCents + 12, sale.Player.WalletCents);
    }

    [Fact]
    public void HockeyRecipeUsesExactlyThreeWoodAndNorthernRecipesAreOptional()
    {
        var recipe = CraftingCatalog.Recipes.Single(r => r.Id == "hockeyStick"); Assert.Equal("hockeyStick", recipe.OutputItemType);
        var ingredient = Assert.Single(recipe.Ingredients); Assert.Equal("wood", ingredient.ItemType); Assert.Equal(3, ingredient.Quantity);
    }

    [Fact]
    public async Task IceSkateKnocksBackAndBleedsForExactlyThreeTicks()
    {
        var (world, id, _, clock) = await RetroFixture(); var player = world.GetRetroBattleUpdate(id).Player!;
        player = await world.TeleportAsync(id, new(0, 0, true));
        var target = new ActorState("skate-target", EntityKind.Npc, "resident", "Target", player.Position with { X = player.Position.X + 1 }, HealthHearts: 10, MaximumHealthHearts: 10);
        ImpressionActors(world)[target.Id] = target;
        await NorthernCall(world, "ApplyIceSkateHitAsync", id, target.Id, player.Position, "outdoor", CancellationToken.None);
        Assert.True(ImpressionActors(world)[target.Id].Position.Distance2D(target.Position) > 0);
        for (var n = 0; n < 5; n++) { clock.Advance(1); await NorthernCall(world, "AdvanceBleedingAsync", clock.GetUtcNow(), CancellationToken.None); }
        Assert.Equal(9.25, ImpressionActors(world)[target.Id].HealthHearts);
    }

    [Fact]
    public async Task HaneyDrivesIntoDrivewayTradesThenDisappearsWithoutNearbyPlayers()
    {
        var region = new RegionId(45, -123);
        var parking = new CanonicalEntity("haney-driveway", EntityKind.Terrain, new(region, -110, -10),
            [new(-115,-14),new(-105,-14),new(-105,0),new(-115,0),new(-115,-14)],
            new Dictionary<string,string> { ["terrain"] = "pavement", ["subtype"] = "driveway", ["roadId"] = "transit-road" });
        var (world, player) = await TransitWorld(parking);
        ImpressionActors(world).Clear();
        PhotoField<ConcurrentDictionary<string, PlayerState>>(world,"_players")[player.Id] = player with { Position = new(region,-110,-17) };
        NorthernField(world, "_nextHaneyVisit", DateTimeOffset.UtcNow.AddSeconds(-1));
        await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        var truck = Assert.Single(world.CreateSnapshot().BaseEntities, e => e.Properties.GetValueOrDefault("subtype") == "haneyPickup");
        var start = truck.Position;
        // Traffic is absent on the approach in this fixture.
        var buses = typeof(RealityWorld).GetField("_buses", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;
        buses.GetType().GetMethod("Clear")!.Invoke(buses, null);
        for (var n = 0; n < 180 && !ImpressionActors(world).Values.Any(a => a.Subtype == "haney"); n++) await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        truck = world.CreateSnapshot().BaseEntities.Single(e => e.Id == truck.Id);
        Assert.True(truck.Position.Distance2D(start) > 20);
        Assert.Equal(parking.Position, truck.Position);
        var merchant = Assert.Single(ImpressionActors(world).Values, a => a.Subtype == "haney"); Assert.Equal("Eustace Charleston Haney", merchant.Name);
        Assert.Contains(world.TakeInversionChat(), c => c.PlayerId == merchant.Id && c.Message.Contains("stuff for sale"));
        PhotoField<ConcurrentDictionary<string, PlayerState>>(world,"_players")[player.Id] = player with { Position = new(region,2000,2000) };
        await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));
        Assert.DoesNotContain(world.CreateSnapshot().BaseEntities, e => e.Id == truck.Id);
        Assert.DoesNotContain(ImpressionActors(world).Values, a => a.Id == merchant.Id);
        Assert.Contains(truck.Id, world.TakeHaneyTruckRemovals());
    }
}
