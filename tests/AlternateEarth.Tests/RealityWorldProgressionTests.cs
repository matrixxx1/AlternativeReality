using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task NotSpecialDefaultsMigrateAndNewAllocationsPersist()
    {
        var legacy = System.Text.Json.JsonSerializer.Deserialize<CharacterStats>(
            """{"strength":2,"perception":1,"endurance":1,"charisma":1,"intelligence":1,"agility":1,"luck":0}""", SharedJson.Options)!;
        Assert.Equal(10, legacy.Total);
        Assert.Equal(1, legacy.NutUp); Assert.Equal(1, legacy.Opportunistic); Assert.Equal(1, legacy.Timing);
        var (world, store, _) = await CreateSelectiveLootWorld();
        var stats = new CharacterStats(Strength: 0, Perception: 0, Luck: 0, NutUp: 2, Opportunistic: 2, Timing: 2);
        await world.AssignStatsAsync("collector", new(stats));
        Assert.Equal(0, world.GetProgression("collector").AvailablePoints);
        await world.LeaveAsync("collector"); await world.JoinAsync("collector", "Collector");
        Assert.Equal(stats, world.GetProgression("collector").Stats);
        Assert.Equal(stats, (await store.LoadProgressionAsync(world.Configuration.Id, "collector"))!.Stats);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AssignStatsAsync("collector", new(stats with { Timing = 3 })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AssignStatsAsync("collector", new(stats with { NutUp = -1 })));
        Assert.True(ProgressionRules.ExtraAttackChance(stats) > ProgressionRules.ExtraAttackChance(new()));
        Assert.True(ProgressionRules.ShootingInterval(stats) < ProgressionRules.ShootingInterval(new()));
    }

    [Fact]
    public async Task NutUpResistsStrongerAttackersAndFearIsNotRepeatedForBurstRounds()
    {
        var clock = new ProbulatorTestClock();
        var (world, _, npc) = await ImpressionWorld(clock);
        await world.SetGodModeAsync("pilot", false);
        var player = world.CreateSnapshot().Players.Single(p => p.Id == "pilot");
        ImpressionActors(world)[npc.Id] = npc with { MaximumHealthHearts = player.MaximumHealthHearts * 2 };
        world.ProgressionRoll = () => 0;
        var hit = new CombatEvent(npc.Id, player.Id, "fist", npc.Position, player.Position, true, 1, false, "Hit");
        Assert.True(world.ResolveCombatFear(hit).FleeInFear);
        Assert.False(world.ResolveCombatFear(hit).FleeInFear);
        await world.AwardExperienceAsync(player.Id, 10000, "test", randomize: false);
        await world.AssignStatsAsync(player.Id, new(new(NutUp: 10)));
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.False(world.ResolveCombatFear(hit).FleeInFear);
        Assert.False(world.ResolveCombatFear(hit with { Hit = false }).FleeInFear);
    }

    [Fact]
    public async Task OpportunisticAddsAFreeHitAndTimingReducesServerCooldown()
    {
        var (world, store, npc) = await ImpressionWorld(new ProbulatorTestClock());
        await world.UpdateItemConfigurationAsync("pilot", new("rifle", 7, 200, 300000, 600000, Accuracy: 1, AttackIntervalSeconds: 1));
        await world.LeaveAsync("pilot");
        await store.SaveInventoryAsync(new("pilot", new[] { new ItemStack("rifle", 1), new ItemStack("bullet", 5) }));
        var player = await world.JoinAsync("pilot", "Pilot");
        await world.SetGodModeAsync("pilot", false);
        await world.SetEquipmentAsync("pilot", "weapon", "rifle");
        await world.AwardExperienceAsync("pilot", 20000, "test", randomize: false);
        await world.AssignStatsAsync("pilot", new(new(Opportunistic: 2, Timing: 10)));
        ImpressionActors(world)[npc.Id] = npc with { Position = player.Position, HealthHearts = 100, MaximumHealthHearts = 100 };
        world.ProgressionRoll = () => 0;
        var result = await world.AttackAsync("pilot", new(npc.Id, "rifle"));
        Assert.Equal(14, result.Event.Damage);
        Assert.Single(result.Consequences!);
        Assert.Equal(4, result.Inventory.Items.Single(i => i.ItemType == "bullet").Quantity);
        var attacks = (ConcurrentDictionary<(string, string), DateTimeOffset>)typeof(RealityWorld)
            .GetField("_lastPlayerAttack", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        attacks[("pilot", "rifle")] = DateTimeOffset.UtcNow.AddSeconds(-.8);
        await world.AttackAsync("pilot", new(npc.Id, "rifle"));
        await world.AssignStatsAsync("pilot", new(new(Opportunistic: 2, Timing: 1)));
        attacks[("pilot", "rifle")] = DateTimeOffset.UtcNow.AddSeconds(-.8);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AttackAsync("pilot", new(npc.Id, "rifle")));
    }
    [Fact]
    public async Task UnwitnessedCrimeAddsLessEvilThanWitnessedCrimeAndLuckCanPreventWitnessing()
    {
        var (world, store, npc) = await ImpressionWorld(new ProbulatorTestClock());
        var entities = (ConcurrentDictionary<string, CanonicalEntity>)typeof(RealityWorld).GetField("_baseEntities", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var position = world.CreateSnapshot().Players.Single(item => item.Id == "pilot").Position;
        entities["paint-car"] = new("paint-car", EntityKind.Vehicle, position, Array.Empty<GeometryPoint>(), new Dictionary<string, string>());
        ImpressionActors(world).Clear();
        await world.SprayPaintVehicleAsync("pilot", "paint-car");
        Assert.Equal(-.25, world.GetProgression("pilot").Alignment);
        ImpressionActors(world)[npc.Id] = npc with { Subtype = "resident" };
        world.ProgressionRoll = () => 0;
        var seen = await world.SprayPaintVehicleAsync("pilot", "paint-car");
        Assert.NotNull(seen.WitnessMessage);
        Assert.Equal(-1, world.GetProgression("pilot").Alignment);
        world.ProgressionRoll = () => .999;
        var unseen = await world.SprayPaintVehicleAsync("pilot", "paint-car");
        Assert.Null(unseen.WitnessMessage);
        Assert.Equal(-1.25, (await store.LoadProgressionAsync(world.Configuration.Id, "pilot"))!.Alignment);
    }

    [Fact]
    public async Task LevelsStartAtOneAndGrowingThresholdsGrantOnePointEachWithServerValidation()
    {
        var (world, store, _) = await CreateSelectiveLootWorld();
        Assert.Equal(1, world.GetProgression("collector").Level);
        Assert.Equal(new CharacterStats(), world.GetProgression("collector").Stats);
        Assert.Equal(0, world.GetProgression("collector").AvailablePoints);
        await world.AwardExperienceAsync("collector", 99, "test", randomize: false);
        Assert.Equal(1, world.GetProgression("collector").Level);
        await world.AwardExperienceAsync("collector", 1, "test", randomize: false);
        Assert.Equal(2, world.GetProgression("collector").Level);
        Assert.Equal(200, world.GetProgression("collector").RequiredForNextLevel);
        Assert.Equal(1, world.GetProgression("collector").AvailablePoints);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AssignStatsAsync("collector", new(new(Strength: -1))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AssignStatsAsync("collector", new(new(Strength: 3))));
        await world.AssignStatsAsync("collector", new(new(Strength: 3, Luck: 0)));
        Assert.Equal(60, world.GetPrivateState("collector").Inventory.MaximumWeightPounds);
        Assert.Equal(0, world.GetProgression("collector").AvailablePoints);
        await world.LeaveAsync("collector");
        await world.JoinAsync("collector", "Collector");
        Assert.Equal(3, world.GetProgression("collector").Stats.Strength);
        Assert.Equal(100, (await store.LoadProgressionAsync(world.Configuration.Id, "collector"))!.Experience);
        await world.AwardExperienceAsync("collector", 200, "test", randomize: false);
        Assert.Equal(3, world.GetProgression("collector").Level);
        Assert.Equal(350, world.GetProgression("collector").RequiredForNextLevel);
    }

    [Fact]
    public async Task OnlineMinutesAndExplorationPersistWithoutOfflineCreditOrRepeatedAreaRewards()
    {
        var (world, _, _) = await CreateSelectiveLootWorld();
        world.ProgressionRoll = () => .5;
        var now = DateTimeOffset.UtcNow;
        await world.AdvanceProgressionAsync(now);
        Assert.Equal(75, world.GetProgression("collector").Experience);
        await world.AdvanceProgressionAsync(now.AddSeconds(30));
        Assert.Equal(75, world.GetProgression("collector").Experience);
        await world.LeaveAsync("collector");
        await world.JoinAsync("collector", "Collector");
        await world.AdvanceProgressionAsync(DateTimeOffset.UtcNow.AddSeconds(31));
        Assert.Equal(75.05, world.GetProgression("collector").Experience);
        Assert.Single(world.TakeProgressionNotices(), item => item.Message.Contains("New map area"));
        await world.LeaveAsync("collector");
        await world.AdvanceProgressionAsync(now.AddDays(10));
        Assert.Equal(75.05, world.GetProgression("collector").Experience);
    }

    [Fact]
    public async Task EventPresenceAwardsOnlyWhileInsideActiveAreaAndIntelligenceScalesRandomXp()
    {
        var (world, _, _) = await CreateSelectiveLootWorld();
        world.ProgressionRoll = () => .5;
        await world.AssignStatsAsync("collector", new(new(Intelligence: 2, Strength: 0)));
        Assert.Equal(108, await world.AwardExperienceAsync("collector", 100, "test"), 5);
        Assert.Equal(0, await world.AwardExperienceAsync("collector", 10, "once", "once", randomize: false) - 10.8, 5);
        Assert.Equal(0, await world.AwardExperienceAsync("collector", 10, "once", "once"));
        var player = world.CreateSnapshot().Players.Single();
        var actor = new ActorState("event-progress", EntityKind.Animal, "bear", "Event animal", player.Position, EventStartedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1), EventEndsAtUtc: DateTimeOffset.UtcNow.AddMinutes(10));
        ImpressionActors(world)[actor.Id] = actor;
        var now = DateTimeOffset.UtcNow;
        await world.AdvanceProgressionAsync(now);
        var before = world.GetProgression(player.Id).Experience;
        await world.AdvanceProgressionAsync(now.AddSeconds(60));
        Assert.Equal(3.29, world.GetProgression(player.Id).Experience - before, 5);
        ImpressionActors(world)[actor.Id] = actor with { Position = actor.Position with { X = actor.Position.X + 101 } };
        before = world.GetProgression(player.Id).Experience;
        await world.AdvanceProgressionAsync(now.AddSeconds(120));
        Assert.Equal(.05, world.GetProgression(player.Id).Experience - before, 5);
    }

    [Fact]
    public async Task EnduranceRespecPreservesStaminaFractionAndAllStatsHaveTheRequestedDirection()
    {
        var (world, _, _) = await CreateSelectiveLootWorld();
        var players = (ConcurrentDictionary<string, PlayerState>)typeof(RealityWorld).GetField("_players", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        players["collector"] = players["collector"] with { Stamina = 5 };
        var changed = await world.AssignStatsAsync("collector", new(new(Endurance: 2, Strength: 0)));
        Assert.Equal(12, changed.MaximumStamina); Assert.Equal(6, changed.Stamina);
        changed = await world.AssignStatsAsync("collector", new(new CharacterStats()));
        Assert.Equal(5, changed.Stamina);
        var baseline = new CharacterStats(); var better = new CharacterStats(2,2,2,2,2,2,2);
        Assert.True(ProgressionRules.Damage(better) > ProgressionRules.Damage(baseline));
        Assert.True(ProgressionRules.Accuracy(better) > ProgressionRules.Accuracy(baseline));
        Assert.True(ProgressionRules.Vision(better) > ProgressionRules.Vision(baseline));
        Assert.True(ProgressionRules.Lockpick(better) > ProgressionRules.Lockpick(baseline));
        Assert.True(ProgressionRules.CraftSuccess(better, .2) > ProgressionRules.CraftSuccess(baseline, .2));
        Assert.True(ProgressionRules.Drain(better) < ProgressionRules.Drain(baseline));
        Assert.True(ProgressionRules.NpcSight(better) < ProgressionRules.NpcSight(baseline));
        Assert.True(ProgressionRules.Witness(better) < ProgressionRules.Witness(baseline));
    }

    [Theory]
    [InlineData(-100, -.3)]
    [InlineData(100, .3)]
    [InlineData(-.25, -.00075)]
    [InlineData(.25, .00075)]
    public async Task AlignmentIsPersistentModestAndAppliedOnlyAtTheFirstMeeting(double alignment, double expected)
    {
        var clock = new ProbulatorTestClock();
        var (world, store, npc) = await ImpressionWorld(clock);
        world.SetActionMode("pilot", "neutral");
        await world.AdjustAlignmentAsync("pilot", alignment);
        await world.AdvanceFirstImpressionsAsync();
        Assert.Equal(expected, ImpressionRating(world, "pilot", npc.Id), 5);
        Assert.Equal(alignment, (await store.LoadProgressionAsync(world.Configuration.Id, "pilot"))!.Alignment);
        await world.AdjustAlignmentAsync("pilot", -alignment);
        clock.Advance(TimeSpan.FromDays(1));
        await world.AdvanceFirstImpressionsAsync();
        Assert.Equal(expected, ImpressionRating(world, "pilot", npc.Id), 5);
        await world.AdjustAlignmentAsync("pilot", 1_000);
        Assert.Equal(100, world.GetProgression("pilot").Alignment);
    }

    [Fact]
    public async Task CharismaAffectsFirstMeetingWithQuestNpcsButRespecCannotRewriteIt()
    {
        var clock = new ProbulatorTestClock();
        var (world, _, npc) = await ImpressionWorld(clock);
        ImpressionActors(world)[npc.Id] = npc with { IsQuestGiver = true };
        await world.AssignStatsAsync("pilot", new(new(Charisma: 2, Strength: 0)));
        await world.AdvanceFirstImpressionsAsync();
        Assert.Equal(.02, ImpressionRating(world, "pilot", npc.Id), 5);
        await world.AssignStatsAsync("pilot", new(new(Charisma: 3, Strength: 0, Luck: 0)));
        clock.Advance(TimeSpan.FromDays(1));
        await world.AdvanceFirstImpressionsAsync();
        Assert.Equal(.02, ImpressionRating(world, "pilot", npc.Id), 5);
    }

    [Fact]
    public async Task RepeatedRecipeCopiesConsumeExactlyOneAndPersistTheHalvingStudyCurve()
    {
        var (world, store, _) = await CreateCraftingTestWorld();
        await world.LeaveAsync("crafter");
        await store.SaveInventoryAsync(new InventoryState("crafter", new[] { new ItemStack("recipe:napalmBottle", 3) }));
        await world.JoinAsync("crafter", "Crafter", "crafter-account");
        var initial = world.GetRecipeStudy("crafter", "napalmBottle")!;
        Assert.InRange(initial.InitialChance, .01, .5);
        foreach (var expected in new[] { .05, .075, .0875 })
        {
            await world.ConsumeItemAsync("crafter", "recipe:napalmBottle");
            Assert.Equal(initial.BaseChance + expected, world.GetRecipeStudy("crafter", "napalmBottle")!.BaseChance, 6);
        }
        Assert.DoesNotContain(world.GetPrivateState("crafter").Inventory.Items, item => item.ItemType == "recipe:napalmBottle");
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ConsumeItemAsync("crafter", "recipe:napalmBottle"));
        Assert.Equal(4, Assert.Single(await store.LoadRecipeStudiesAsync(world.Configuration.Id, "crafter"), study => study.RecipeId == "napalmBottle").Count);
        Assert.True(world.GetProgression("crafter").Experience > 0);
        await world.LeaveAsync("crafter"); await world.JoinAsync("crafter", "Crafter", "crafter-account");
        Assert.Equal(initial.BaseChance + .0875, world.GetRecipeStudy("crafter", "napalmBottle")!.BaseChance, 6);
    }

    [Fact]
    public async Task FailedCraftDestroysTableLosesOnlyAttemptedInputsAndDealsExactlyOneDamage()
    {
        var (world, store, building) = await CreateCraftingTestWorld();
        var health = world.CreateSnapshot().Players.Single().HealthHearts;
        var rolls = new Queue<double>(new[] { 0d, .999, .5 }); // One success, then an explosion.
        world.ProgressionRoll = () => rolls.Dequeue();
        var result = await world.CraftItemAsync("crafter", new("craft-table", "napalmBottle", 3));
        Assert.True(result.Crafting.TableDestroyed);
        Assert.Equal(health - 1, result.Player!.HealthHearts);
        Assert.Equal("craftingExplosion", result.Explosion!.Weapon);
        Assert.Equal(1, result.Explosion.Damage);
        var supplies = result.PrivateState.HomeItemStorage!.Items;
        Assert.Equal(1, supplies.Single(item => item.ItemType == "napalmBottle").Quantity);
        Assert.Equal(1, supplies.Single(item => item.ItemType == "emberGel").Quantity);
        Assert.DoesNotContain(result.PrivateState.Dungeon!.Furnishings!, item => item.Id == "craft-table");
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CraftItemAsync("crafter", new("craft-table", "napalmBottle")));
        var restarted = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store);
        await restarted.InitializeAsync(); await restarted.JoinAsync("crafter", "Crafter", "crafter-account");
        Assert.DoesNotContain(restarted.GetPrivateState("crafter").Dungeon!.Furnishings!, item => item.Id == "craft-table");
        Assert.Equal(1, restarted.GetPrivateState("crafter").HomeItemStorage!.Items.Single(item => item.ItemType == "emberGel").Quantity);
    }

    [Fact]
    public async Task DungeonCompletionRequiresEveryFloorAndAwardsOneGrandChestAndBonus()
    {
        var (world, _, _) = await CreateSelectiveLootWorld(); world.ProgressionRoll = () => .5;
        var players = (ConcurrentDictionary<string, PlayerState>)typeof(RealityWorld).GetField("_players", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var floors = (ConcurrentDictionary<string, DungeonState>)typeof(RealityWorld).GetField("_dungeons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var position = players["collector"].Position;
        var first = new ActorState("enemy1", EntityKind.Animal, "bear", "Enemy", position, LocationId: "floor1");
        var second = first with { Id = "enemy2", LocationId = "floor2" };
        DungeonState Floor(string id, ActorState actor) => new(id, "building", 20, 20, Array.Empty<DungeonRoom>(), Array.Empty<DungeonWall>(), position,
            new[] { actor }, Array.Empty<TreasureChestState>(), Array.Empty<string>(), SessionId: "session", LevelCount: 2);
        floors["floor1"] = Floor("floor1", first); floors["floor2"] = Floor("floor2", second);
        async Task Kill(ActorState actor)
        {
            players["collector"] = players["collector"] with { LocationId = actor.LocationId };
            await (Task)typeof(RealityWorld).GetMethod("UpdateActorHealthAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(world, new object[] { players["collector"], actor, 0d, true, CancellationToken.None, true })!;
        }
        await Kill(second);
        Assert.False(floors["floor2"].IsCompleted); Assert.Empty(floors["floor2"].Chests);
        var before = world.GetProgression("collector").Experience;
        await Kill(first);
        Assert.True(floors["floor1"].IsCompleted); Assert.True(floors["floor2"].IsCompleted);
        var chest = Assert.Single(floors.Values.SelectMany(floor => floor.Chests)); Assert.True(chest.IsGrand);
        Assert.True(world.GetProgression("collector").Experience - before >= 215);
        var contents = await world.OpenChestAsync("collector", chest.Id);
        Assert.True(contents.Contents.Items.Count >= 6);
        before = world.GetProgression("collector").Experience;
        await Kill(first);
        Assert.Equal(before, world.GetProgression("collector").Experience);
        Assert.Single(floors.Values.SelectMany(floor => floor.Chests));
        Assert.Single(world.TakeProgressionNotices(), item => item.Message.Contains("Dungeon complete!"));
    }
}
