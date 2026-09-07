using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task TauntsVaryHaveACooldownAndIncreaseNpcHostilityWithoutDoublingFirstImpressions()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, npc) = await ImpressionWorld(clock);
        await world.AdvanceFirstImpressionsAsync();
        var first = await world.TauntAsync("pilot", npc.Id);
        Assert.Equal("pilot", first.Chat.PlayerId);
        Assert.Contains(npc.Name, first.Chat.Message);
        Assert.Equal(-.05, first.Relationships.Single().FriendRating, 5);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.TauntAsync("pilot", npc.Id));
        Assert.Equal(-.05, ImpressionRating(world, "pilot", npc.Id), 5);
        clock.Advance(3);
        var second = await world.TauntAsync("pilot", npc.Id);
        Assert.NotEqual(first.Chat.Message, second.Chat.Message);
        Assert.Equal(-.15, second.Relationships.Single().FriendRating, 5);
        Assert.Equal(-.2, (await store.LoadRelationshipsAsync(world.Configuration.Id, "pilot")).Single(r => r.ActorId == npc.Id).FriendRating, 5);
    }

    [Fact]
    public async Task PlayerTauntsUpdateBothCharactersRelationshipViews()
    {
        var (world, _, _) = await ImpressionWorld(new ProbulatorTestClock());
        await world.JoinAsync("target", "Other Player");
        await world.SetGodModeAsync("target", true);
        await world.TeleportAsync("target", new(2, 0, true));
        var result = await world.TauntAsync("pilot", "target");
        Assert.Equal(2, result.Relationships.Count);
        Assert.All(result.Relationships, relationship => Assert.Equal(-.1, relationship.FriendRating, 5));
        Assert.Equal(-.1, ImpressionRating(world, "target", "pilot"), 5);
    }

    [Fact]
    public async Task TauntsRejectAnimalsSelfDistantAndMissingTargetsWithoutChangingRelationships()
    {
        var (world, _, npc) = await ImpressionWorld(new ProbulatorTestClock());
        var animal = world.PlaceTestCharacter("pilot", new("animal", 2, 0)).Actor!;
        foreach (var id in new[] { "pilot", "missing", animal.Id })
            await Assert.ThrowsAsync<InvalidOperationException>(() => world.TauntAsync("pilot", id));
        ImpressionActors(world)[npc.Id] = npc with { Position = npc.Position with { X = 30 } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.TauntAsync("pilot", npc.Id));
        Assert.Empty(world.GetPrivateState("pilot").Relationships);
    }
}
