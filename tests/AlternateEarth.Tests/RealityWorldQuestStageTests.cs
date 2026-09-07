using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task QuestOfferWaitsForAcceptanceHasSuppliesAndExpiresWithoutReward()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, npc) = await ImpressionWorld(clock);
        var giver = npc with { IsQuestGiver = true };
        ImpressionActors(world)[giver.Id] = giver;
        var offer = world.RequestQuest("pilot", giver.Id);
        Assert.True(offer.IsOffer);
        Assert.Empty(world.GetPrivateState("pilot").Quests!);
        Assert.Null(offer.Quest.DeadlineUtc);
        Assert.Equal(3, offer.Quest.RewardItems!.Count);
        Assert.True(offer.Quest.DeliveryMinutes >= 45);
        clock.Advance(TimeSpan.FromMinutes(10));
        var accepted = await world.AcceptQuestAsync("pilot", offer.Quest.Id);
        Assert.Equal(clock.GetUtcNow().AddMinutes(offer.Quest.DeliveryMinutes!.Value), accepted.Quest.DeadlineUtc);
        var balance = accepted.Player.WalletCents;
        clock.Advance(TimeSpan.FromMinutes(offer.Quest.DeliveryMinutes.Value + 1));
        await world.AdvanceFoodDeliveriesAsync();
        var expired = Assert.Single((await store.LoadQuestsAsync(world.Configuration.Id, "pilot")));
        Assert.Equal("failed", expired.Status);
        Assert.Equal(balance, world.CreateSnapshot().Players.Single(p => p.Id == "pilot").WalletCents);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteQuestAsync("pilot", new(expired.Id, giver.Id)));
    }

    [Fact]
    public async Task ManualInversionIsNearbyBecomesQuestAndPaysOnlyOnce()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, _) = await ImpressionWorld(clock);
        var origin = world.CreateSnapshot().Players.Single(p => p.Id == "pilot").Position;
        var actor = Assert.Single(world.TriggerWorldEvent("pilot", "trex"));
        Assert.InRange(actor.Position.Distance2D(origin), 0, 50);
        await world.AdvanceFoodDeliveriesAsync();
        var quest = Assert.Single(world.GetPrivateState("pilot").Quests!);
        Assert.Equal("inversion", quest.Kind);
        Assert.Equal(actor.Position, quest.NextStagePosition);
        var balance = world.CreateSnapshot().Players.Single(p => p.Id == "pilot").WalletCents;
        clock.Advance(actor.EventEndsAtUtc!.Value - clock.GetUtcNow() + TimeSpan.FromSeconds(1));
        await world.AdvanceFoodDeliveriesAsync();
        await world.AdvanceFoodDeliveriesAsync();
        Assert.Equal("completed", Assert.Single(world.GetPrivateState("pilot").Quests!).Status);
        Assert.Equal(balance + quest.RewardCents, world.CreateSnapshot().Players.Single(p => p.Id == "pilot").WalletCents);
    }
}
