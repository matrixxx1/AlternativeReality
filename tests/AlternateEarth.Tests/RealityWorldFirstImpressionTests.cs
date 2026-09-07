using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static ConcurrentDictionary<string, ActorState> ImpressionActors(RealityWorld world) =>
        (ConcurrentDictionary<string, ActorState>)typeof(RealityWorld).GetField("_actors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
    private static double ImpressionRating(RealityWorld world, string player, string actor) =>
        world.GetPrivateState(player).Relationships.FirstOrDefault(r => r.ActorId == actor)?.FriendRating ?? 0;

    private async Task<(RealityWorld World, SqliteRealityStore Store, ActorState Npc)> ImpressionWorld(ProbulatorTestClock clock)
    {
        var (world, pilot, store) = await CreateProbulatorTestWorld(clock: clock);
        await world.SetTravelModeAsync(pilot.Id, TravelMode.Walk);
        await world.TeleportAsync(pilot.Id, new(0, 0, true));
        world.SetActionMode(pilot.Id, "defensive");
        ImpressionActors(world).Clear();
        var npc = world.PlaceTestCharacter(pilot.Id, new("npc", 2, 0)).Actor!;
        return (world, store, npc);
    }

    [Theory]
    [InlineData("neutral", 0)]
    [InlineData("attackReady", -.05)]
    [InlineData("aggressive", -.1)]
    [InlineData("defensive", .05)]
    [InlineData("timid", .1)]
    public async Task FirstMeetingAppliesOncePerDayAndModeSwitchesCannotRepeatIt(string mode, double expected)
    {
        var clock = new ProbulatorTestClock();
        var (world, store, npc) = await ImpressionWorld(clock);
        world.SetActionMode("pilot", mode);
        await world.AdvanceFirstImpressionsAsync();
        Assert.Equal(expected, ImpressionRating(world, "pilot", npc.Id), 5);
        foreach (var next in new[] { "attackReady", "neutral", "defensive" })
        {
            world.SetActionMode("pilot", next);
            Assert.Empty(await world.AdvanceFirstImpressionsAsync());
            Assert.Equal(expected, ImpressionRating(world, "pilot", npc.Id), 5);
        }
        Assert.Single(await store.LoadFirstImpressionsAsync(world.Configuration.Id));
        clock.Advance(TimeSpan.FromDays(1));
        await world.AdvanceFirstImpressionsAsync();
        Assert.Equal(expected + .05, ImpressionRating(world, "pilot", npc.Id), 5);
    }

    [Fact]
    public async Task NameMemorySurvivesRestartAndNpcReplacementButDoesNotTransferToAnotherCharacter()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, npc) = await ImpressionWorld(clock);
        await world.AdvanceFirstImpressionsAsync();
        await world.LeaveAsync("pilot");
        var restarted = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store, clock);
        await restarted.InitializeAsync();
        await restarted.JoinAsync("pilot", "Pilot");
        ImpressionActors(restarted).Clear();
        var replacement = restarted.PlaceTestCharacter("pilot", new("npc", 2, 0)).Actor!;
        Assert.NotEqual(npc.Id, replacement.Id);
        Assert.Equal(.05, ImpressionRating(restarted, "pilot", replacement.Id), 5);
        Assert.Empty(await restarted.AdvanceFirstImpressionsAsync());
        await restarted.JoinAsync("sibling", "Different Character");
        await restarted.SetGodModeAsync("sibling", true);
        await restarted.TeleportAsync("sibling", new(0, 0, true));
        Assert.Equal(0, ImpressionRating(restarted, "sibling", replacement.Id));
        restarted.SetActionMode("sibling", "attack");
        await restarted.AdvanceFirstImpressionsAsync();
        Assert.Equal(-.05, ImpressionRating(restarted, "sibling", replacement.Id), 5);
        Assert.Equal(.05, ImpressionRating(restarted, "pilot", replacement.Id), 5);
    }

    [Theory]
    [InlineData("merchant")]
    [InlineData("quest")]
    [InlineData("employee")]
    [InlineData("category")]
    [InlineData("animal")]
    [InlineData("dungeon")]
    [InlineData("store")]
    [InlineData("distant")]
    public async Task ExcludedPosturingEncountersRememberNeutralHumanMeetings(string kind)
    {
        var (world, store, npc) = await ImpressionWorld(new ProbulatorTestClock());
        ImpressionActors(world)[npc.Id] = kind switch
        {
            "merchant" => npc with { IsMerchant = true },
            "quest" => npc with { IsQuestGiver = true },
            "employee" => npc with { Subtype = "storeEmployee" },
            "category" => npc with { MerchantCategory = "general" },
            "animal" => npc with { Kind = EntityKind.Animal },
            "dungeon" => npc with { LocationId = "dungeon:test" },
            "store" => npc with { LocationId = "store:test" },
            _ => npc with { Position = npc.Position with { X = 100 } }
        };
        world.PlaceTestCharacter("pilot", new("player", 2, 0));
        world.SetActionMode("pilot", "attack");
        Assert.Empty(await world.AdvanceFirstImpressionsAsync());
        if (kind is "merchant" or "quest" or "employee" or "category") Assert.Single(await store.LoadFirstImpressionsAsync(world.Configuration.Id));
        else Assert.Empty(await store.LoadFirstImpressionsAsync(world.Configuration.Id));
        Assert.Equal(0, ImpressionRating(world, "pilot", npc.Id));
    }

    [Fact]
    public async Task CombatDoesNotPersistTheNameBonusTwiceAndRememberedNpcsKeepItIndoors()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, npc) = await ImpressionWorld(clock);
        await world.AdvanceFirstImpressionsAsync();
        await world.SetEquipmentAsync("pilot", "weapon", "rifle");
        await world.AttackAsync("pilot", new(npc.Id, "rifle"));
        Assert.Equal(-.95, ImpressionRating(world, "pilot", npc.Id), 5);
        Assert.Equal(-1, (await store.LoadRelationshipsAsync(world.Configuration.Id, "pilot")).Single(r => r.ActorId == npc.Id).FriendRating, 5);
        ImpressionActors(world)[npc.Id] = npc with { LocationId = "dungeon:test" };
        Assert.Equal(-.95, ImpressionRating(world, "pilot", npc.Id), 5);
    }
}
