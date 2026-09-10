using System.Collections.Concurrent;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Theory]
    [InlineData("beans")]
    [InlineData("porkAndBeans")]
    public async Task MusicalFruitFoodPersistsRefreshesAndSurvivesOtherMealsAndVitals(string food)
    {
        var (world, player, clock) = await ScubaFixture();
        world.MusicalFruitRoll = () => 0;
        var inventory = PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_inventories")[player.Id];
        inventory[food] = 2; inventory["carrot"] = 1;
        var fed = await world.ConsumeItemAsync(player.Id, food);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), fed.Survival!.MusicalFruit!.EndsAtUtc);
        Assert.Equal(clock.GetUtcNow().AddSeconds(10), fed.Survival.MusicalFruit.NextEmissionAtUtc);
        Assert.Equal(fed.Survival.MusicalFruit, RealityWorld.StepHunger(fed with { GodMode = true }, 2, clock.GetUtcNow()).Survival!.MusicalFruit);
        Assert.Equal(fed.Survival.MusicalFruit, (await world.ConsumeItemAsync(player.Id, "carrot")).Survival!.MusicalFruit);
        Assert.Equal(fed.Survival.MusicalFruit, (await world.JoinAsync(player.Id, player.Name)).Survival!.MusicalFruit);
        clock.Advance(60);
        var refreshed = await world.ConsumeItemAsync(player.Id, food);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), refreshed.Survival!.MusicalFruit!.EndsAtUtc);
        Assert.False(refreshed.Survival.MusicalFruit.FinaleEmitted);
    }

    [Theory]
    [InlineData(0, 10, 5, 1)]
    [InlineData(1, 20, 10, 2.25)]
    public async Task MusicalFruitEmissionsHaveBoundedTimingAndOneMovingTwentySecondFinale(double roll, int interval, int duration, double radius)
    {
        var (world, player, clock) = await ScubaFixture(); world.MusicalFruitRoll = () => roll;
        PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_inventories")[player.Id]["beans"] = 1;
        await world.ConsumeItemAsync(player.Id, "beans");
        var changed = new List<PlayerState>();
        clock.Advance(interval - 1); await world.AdvanceMusicalFruitAsync(clock.GetUtcNow(), changed, default);
        Assert.Empty(world.GetAreaHazards());
        clock.Advance(1); await world.AdvanceMusicalFruitAsync(clock.GetUtcNow(), changed, default);
        var cloud = Assert.Single(world.GetAreaHazards());
        Assert.Equal(radius, cloud.RadiusMeters); Assert.Equal(duration, (cloud.EndsAtUtc - cloud.StartedAtUtc).TotalSeconds);
        Assert.Equal(clock.GetUtcNow().AddSeconds(interval), ScubaPlayer(world).Survival!.MusicalFruit!.NextEmissionAtUtc);
        clock.Advance(300 - interval); await world.AdvanceMusicalFruitAsync(clock.GetUtcNow(), changed, default);
        var finale = Assert.Single(world.GetAreaHazards(), c => c.Effect == "musicalFruitFinale");
        Assert.Equal(5, finale.RadiusMeters); Assert.Equal(20, (finale.EndsAtUtc - finale.StartedAtUtc).TotalSeconds);
        var moved = ScubaPlayer(world) with { Position = player.Position with { X = player.Position.X + 10 } }; ScubaPlayer(world, moved);
        clock.Advance(10); await world.AdvanceMusicalFruitAsync(clock.GetUtcNow(), changed, default);
        Assert.Equal(moved.Position, Assert.Single(world.GetAreaHazards(), c => c.Effect == "musicalFruitFinale").Position);
        clock.Advance(10); await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(1));
        Assert.Null(ScubaPlayer(world).Survival!.MusicalFruit);
        Assert.Empty(world.GetAreaHazards());
    }

    [Fact]
    public async Task MusicalFruitPoisonsOnlyNearbyEnemiesAndReactionsAreThrottled()
    {
        var (world, player, clock) = await ScubaFixture(); world.MusicalFruitRoll = () => 0;
        var ground = PhotoField<WorldNavigation>(world, "_navigation").FindNearestWalkable(player.Position with { X = player.Position.X + 130 });
        player = player with { Position = ground, GodMode = true }; ScubaPlayer(world, player);
        var actors = PhotoField<ConcurrentDictionary<string, ActorState>>(world, "_actors"); actors.Clear();
        var enemy = new ActorState("fruit-enemy", EntityKind.Npc, "ordinary", "Enemy", ground, HealthHearts: 100, MaximumHealthHearts: 100);
        actors[enemy.Id] = enemy;
        actors["friend"] = enemy with { Id = "friend", Name = "Friend" };
        actors["distant"] = enemy with { Id = "distant", Position = ground with { X = ground.X + 20 } };
        actors["elsewhere"] = enemy with { Id = "elsewhere", LocationId = "other-room" };
        var relations = PhotoField<ConcurrentDictionary<(string, string), double>>(world, "_relationships");
        foreach (var id in new[] { enemy.Id, "distant", "elsewhere" }) relations[(player.Id, id)] = -10;
        relations[(player.Id, "friend")] = 10;
        PhotoField<ConcurrentDictionary<string, Dictionary<string, int>>>(world, "_inventories")[player.Id]["beans"] = 1;
        await world.ConsumeItemAsync(player.Id, "beans"); clock.Advance(10);
        await world.AdvanceMusicalFruitAsync(clock.GetUtcNow(), [], default);
        Assert.Single(world.TakeQuestDialogue(), c => c.PlayerId == player.Id);
        clock.Advance(1);
        var tick = await world.AdvanceHostilityAsync(TimeSpan.Zero);
        var hits = tick.Combat.Where(c => c.Weapon == "areaHazard").ToArray();
        Assert.Single(hits); Assert.Equal(enemy.Id, hits[0].TargetId); Assert.True(hits[0].Damage > 0);
        Assert.Single(world.TakeQuestDialogue(), c => c.PlayerId == enemy.Id);
        clock.Advance(1); await world.AdvanceHostilityAsync(TimeSpan.Zero);
        Assert.Empty(world.TakeQuestDialogue());
        Assert.Equal(100, actors["friend"].HealthHearts);
    }
}
