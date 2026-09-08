using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static ConcurrentDictionary<string, PlayerState> QueuedPlayers(RealityWorld world) => (ConcurrentDictionary<string, PlayerState>)typeof(RealityWorld).GetField("_players", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;
    private static Task DefeatQueuedActor(RealityWorld world, string player, ActorState actor) => (Task)typeof(RealityWorld).GetMethod("UpdateActorHealthAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, [QueuedPlayers(world)[player], actor, 0d, true, CancellationToken.None, false])!;
    private async Task<(RealityWorld World, ProbulatorTestClock Clock)> QueuedWorld()
    {
        var clock = new ProbulatorTestClock(); var (world, pilot, _) = await CreateProbulatorTestWorld(clock: clock);
        await world.SetTravelModeAsync(pilot.Id, TravelMode.Walk); ImpressionActors(world).Clear();
        return (world, clock);
    }
    [Fact]
    public async Task HydraStartsWithOneSplitsInsideAreaCountsOnceAndCompletesAtFifty()
    {
        var (world, clock) = await QueuedWorld();
        var resident = world.PlaceTestCharacter("pilot", new("npc", 1, 0)).Actor!;
        var run = await world.StartIncursionAsync("pilot", "northPark");
        Assert.Single(world.CreateSnapshot().Actors!, a => a.Subtype == "kenHydra");
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal("northParkCitizen", ImpressionActors(world)[resident.Id].Subtype);
        for (var kills = 0; kills < 50; kills++)
        {
            var ken = world.CreateSnapshot().Actors!.First(a => a.Subtype == "kenHydra");
            Assert.EndsWith(" Hydra", ken.Name);
            await DefeatQueuedActor(world, "pilot", ken); await DefeatQueuedActor(world, "pilot", ken);
            clock.Advance(.01); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.01));
            if (kills == 49) break;
            Assert.Equal(kills + 1, world.Incursion!.Kills);
            var targets = world.CreateSnapshot().Actors!.Where(a => a.Subtype == "kenHydra").ToArray();
            Assert.Equal(kills + 2, targets.Length);
            Assert.All(targets, a => Assert.True(a.Position.Distance2D(run.Center) <= run.Radius));
        }
        Assert.Null(world.Incursion);
        Assert.DoesNotContain(world.CreateSnapshot().Actors!, a => a.Subtype == "kenHydra");
        Assert.Equal(resident.Name, ImpressionActors(world)[resident.Id].Name);
        Assert.Single(world.GetPrivateState("pilot").Loot!, l => l.DropKind == "eventReward");
    }
    [Fact]
    public async Task AlaneeEggsRequireProximityAndBossOnlyTakesTwentyFiveHeartsPerChicken()
    {
        var (world, clock) = await QueuedWorld(); var run = await world.StartIncursionAsync("pilot", "alanee");
        Assert.Equal(12, world.CreateSnapshot().Actors!.Count(a => a.Subtype == "alaneeEgg"));
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal(12, world.CreateSnapshot().Actors!.Count(a => a.Subtype == "alaneeEgg"));
        var eggs = world.CreateSnapshot().Actors!.Where(a => a.Subtype == "alaneeEgg").ToArray();
        foreach (var egg in eggs)
        {
            await world.TeleportAsync("pilot", new(egg.Position.X, egg.Position.Y, true));
            await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
            foreach (var celebrity in world.CreateSnapshot().Actors!.Where(a => a.Subtype == "alaneeCelebrity"))
            { Assert.Contains(RealityWorld.AlaneeCast, c => c.Name == celebrity.Name && c.Quote.Length > 0 && c.Source.StartsWith("https://")); await DefeatQueuedActor(world, "pilot", celebrity); }
            await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        }
        var boss = world.CreateSnapshot().Actors!.Single(a => a.Subtype == "pierceHawkeye");
        Assert.Equal("Pierce Hawkeye", boss.Name); Assert.True(boss.DamageImmune);
        Assert.Equal(0, world.ApplyTypedAttack("pilot", boss.Id, "outdoor", "rocketLauncher", 999));
        await DefeatQueuedActor(world, "pilot", boss); Assert.Equal(100, ImpressionActors(world)[boss.Id].HealthHearts);
        var ufo = new ActorState("immunity-ufo", EntityKind.Npc, "ufo", "Test UFO", boss.Position);
        ImpressionActors(world)[ufo.Id] = ufo;
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.1));
        Assert.Equal(100, ImpressionActors(world)[boss.Id].HealthHearts);
        ImpressionActors(world).TryRemove(ufo.Id, out _);
        clock.Advance(4.9); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.DoesNotContain(world.CreateSnapshot().Actors!, a => a.Subtype == "hawkeyeChicken");
        clock.Advance(.1); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.1));
        for (var i = 0; i < 4; i++)
        {
            var chicken = world.CreateSnapshot().Actors!.First(a => a.Subtype == "hawkeyeChicken");
            await DefeatQueuedActor(world, "pilot", chicken); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
            if (i == 3) break;
            Assert.Equal(100 - 25 * (i + 1), ImpressionActors(world)[boss.Id].HealthHearts);
            clock.Advance(5); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
            if (i == 0) { var acid = Assert.Single(world.GetAreaHazards()); Assert.Equal(5, (acid.EndsAtUtc - acid.StartedAtUtc).TotalSeconds); }
        }
        Assert.Null(world.Incursion); Assert.Empty(world.GetAreaHazards());
    }
    [Theory]
    [InlineData("knife", DamageType.Bleeding, 3)]
    [InlineData("sword", DamageType.Bleeding, 5)]
    [InlineData("rocketLauncher", DamageType.Fire, 4)]
    [InlineData("zombieBite", DamageType.Poison, 5)]
    [InlineData("acid", DamageType.Acid, 4)]
    [InlineData("gas", DamageType.Gas, 3)]
    public async Task DamageEffectsTickOncePerSecondThenExpire(string weapon, DamageType type, int seconds)
    {
        var (world, clock) = await QueuedWorld();
        var actor = world.PlaceTestCharacter("pilot", new("npc", 2, 0)).Actor! with { HealthHearts = 100, MaximumHealthHearts = 100 };
        ImpressionActors(world)[actor.Id] = actor;
        var initial = world.ApplyTypedAttack("pilot", actor.Id, "outdoor", weapon, 2);
        Assert.InRange(initial, 0, 2);
        clock.Advance(.9); Assert.DoesNotContain((await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.9))).Combat, c => c.TargetId == actor.Id);
        clock.Advance(.1); var tick = await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.1));
        Assert.Single(tick.Combat, c => c.TargetId == actor.Id && c.Weapon == type.ToString());
        Assert.DoesNotContain((await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.1))).Combat, c => c.TargetId == actor.Id);
        clock.Advance(seconds - 1); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(seconds - 1));
        var after = ImpressionActors(world)[actor.Id].HealthHearts;
        clock.Advance(10); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(after, ImpressionActors(world)[actor.Id].HealthHearts);
        Assert.Empty(world.InspectActor("pilot", actor.Id).Effects);
    }
    [Fact]
    public async Task AlcoholExpiryUnequipsAllOverlevelGearAndPreservesGearAcrossRejoinAndDrop()
    {
        var clock = new ProbulatorTestClock(); var (world, _, store) = await CreateProbulatorTestWorld(clock: clock);
        await world.SetTravelModeAsync("pilot", TravelMode.Walk); await world.LeaveAsync("pilot");
        var gear = CombatRules.RollGear(7, "Fine", new Random(42));
        await store.SaveInventoryAsync(new("pilot", [new("sword", 1, Quality: "Fine", Gear: gear), new("winterJacket", 1, Gear: gear), new("beer", 1)]));
        await world.JoinAsync("pilot", "Pilot");
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetEquipmentAsync("pilot", "weapon", "sword"));
        await world.ConsumeItemAsync("pilot", "beer"); Assert.Equal(6, world.EffectiveNutUp("pilot"));
        await world.SetEquipmentAsync("pilot", "weapon", "sword"); await world.SetEquipmentAsync("pilot", "shirt", "winterJacket");
        await world.LeaveAsync("pilot"); await world.JoinAsync("pilot", "Pilot");
        Assert.Equal(6, world.EffectiveNutUp("pilot")); Assert.Equal("sword", QueuedPlayers(world)["pilot"].EquippedWeapon);
        clock.Advance(60); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.Equal(1, world.EffectiveNutUp("pilot")); Assert.Equal("fist", QueuedPlayers(world)["pilot"].EquippedWeapon); Assert.Equal("none", QueuedPlayers(world)["pilot"].EquippedShirt);
        Assert.Contains(world.TakeProgressionNotices(), n => n.Message.Contains("Unequipped"));
        var saved = (await store.LoadInventoryAsync("pilot")).Items.Single(i => i.ItemType == "sword");
        Assert.Equal(gear.Level, saved.Gear!.Level); Assert.Equal(gear.Resistances, saved.Gear.Resistances);
        var drop = await world.DropInventoryItemAsync("pilot", new("sword", 1));
        Assert.Equal(7, Assert.Single(drop.Drop.Items).Gear!.Level);
        await world.CollectLootAsync("pilot", drop.Drop.Id);
        Assert.Equal(7, world.GetPrivateState("pilot").Inventory.Items.Single(i => i.ItemType == "sword").Gear!.Level);
    }
    [Fact]
    public async Task FirstSightFearUsesLevelGapAndFoeRatingAndCannotBeCancelledByMoving()
    {
        var (world, clock) = await QueuedWorld();
        var actor = world.PlaceTestCharacter("pilot", new("npc", 6, 0)).Actor! with { Level = 22, FriendRating = -5 };
        ImpressionActors(world)[actor.Id] = actor;
        QueuedPlayers(world)["pilot"] = QueuedPlayers(world)["pilot"] with { GodMode = false };
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        var scared = QueuedPlayers(world)["pilot"];
        Assert.Equal(clock.GetUtcNow().AddSeconds(10), scared.FearedUntilUtc);
        Assert.True(scared.Position.X < 0);
        var move = await world.MoveAsync("pilot", new(1, 0, 1)); Assert.False(move!.Moved);
        clock.Advance(10.1); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.Null(QueuedPlayers(world)["pilot"].FearedUntilUtc);
        await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5)); Assert.Null(QueuedPlayers(world)["pilot"].FearedUntilUtc);
    }
    [Fact]
    public void GearBoundsQualityTiersAndAttackTypesAreConsistent()
    {
        Assert.Equal(20, CombatRules.EquipLimit(10, 10));
        var random = new Random(17);
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(CombatRules.LootLevel(10, false, random), 1, 10);
            Assert.InRange(CombatRules.LootLevel(10, true, random), 1, 30);
        }
        var low = CombatRules.RollGear(1, "Crude", new Random(1)); var high = CombatRules.RollGear(1, "Godly", new Random(1));
        Assert.All(Enum.GetValues<DamageType>(), t => Assert.True(high.Resistances[t] > low.Resistances[t]));
        foreach (var weapon in new[] { "fist", "tailSwipe", "stomp", "pistol", "arrow" }) Assert.Equal(DamageType.Physical, Assert.Single(CombatRules.Attack(weapon)).Type);
        Assert.Equal(DamageType.Fire, Assert.Single(CombatRules.Attack("flamethrower")).Type);
    }
}
