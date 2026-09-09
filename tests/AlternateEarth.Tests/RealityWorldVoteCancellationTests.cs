using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task CancelVoteRequiresGodModeAndDoesNotResolveACanceledBallot()
    {
        var (world, id, _, clock) = await RetroFixture();
        world.StartServerVote(id);
        await world.SetGodModeAsync(id, false);
        Assert.Throws<InvalidOperationException>(() => world.CancelServerVote(id));
        Assert.Throws<InvalidOperationException>(() => world.CancelServerVote("unknown"));
        Assert.NotNull(world.GetInversionView(id).Vote);
        await world.SetGodModeAsync(id, true);
        world.CancelServerVote(id);
        Assert.Null(world.GetInversionView(id).Vote);
        Assert.Throws<InvalidOperationException>(() => world.CastServerVote(id, "random"));
        clock.Advance(61); await world.AdvanceInversionsAsync(TimeSpan.Zero);
        Assert.Null(world.GetInversionView(id).Active); Assert.Empty(world.GetInversionView(id).Queued);
        Assert.Contains(world.TakeInversionChat(), m => m.Message.Contains("canceled the Server Vote"));
        Assert.Throws<InvalidOperationException>(() => world.CancelServerVote(id));
        world.StartServerVote(id); Assert.NotNull(world.GetInversionView(id).Vote);
    }

    [Fact]
    public async Task CancelRunoffPreservesActiveEventAndPreviouslyQueuedWinner()
    {
        var (world, id, _, clock) = await RetroFixture();
        world.StartInversion("northern", world.GetRetroBattleUpdate(id).Player!, clock.GetUtcNow());
        var activeId = world.GetInversionView(id).Active!.Id;
        var winners = PhotoField<Queue<string>>(world, "_winningInversions"); winners.Enqueue("geese");
        var runoff = new ServerVote(clock.GetUtcNow(), ["cards", "turns"]);
        runoff.SyncPlayers(["a", "b"]); runoff.Cast("a", "cards", clock.GetUtcNow()); runoff.Cast("b", "turns", clock.GetUtcNow());
        clock.Advance(60); Assert.Null(runoff.Finish(clock.GetUtcNow())); Assert.Equal(2, runoff.Round);
        NorthernField(world, "_serverVote", runoff);
        world.CancelServerVote(id);
        await world.AdvanceInversionsAsync(TimeSpan.Zero);
        var view = world.GetInversionView(id);
        Assert.Null(view.Vote); Assert.Equal(activeId, view.Active!.Id); Assert.Single(view.Queued);
        Assert.Equal("geese", winners.Peek());
        clock.Advance(3600); await world.AdvanceInversionsAsync(TimeSpan.Zero);
        Assert.NotNull(world.GetInversionView(id).Vote);
    }
}
