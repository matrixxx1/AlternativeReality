using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class ActorRoutePlannerTests
{
    [Fact]
    public async Task RouteWorkersAreBoundedAndReturnSnapshotsWithoutMovingActors()
    {
        var planner = new ActorRoutePlanner();
        var timings = new PerformanceTimings();
        var navigation = new WorldNavigation(new(-100, -100, 100, 100), [], []);
        var actor = new ActorState("test", EntityKind.Npc, "resident", "Resident", new(new(45, -123), 0, 0));
        for (var i = 0; i < planner.Capacity; i++)
            Assert.True(planner.TrySchedule(actor with { Id = $"actor:{i}" }, navigation, 123, timings));
        Assert.False(planner.TrySchedule(actor, navigation, 123, timings));
        Assert.False(planner.TrySchedule(actor with { Id = "actor:0" }, navigation, 123, timings));
        var completed = new List<ActorRoutePlanner.Result>();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (completed.Count < planner.Capacity && DateTime.UtcNow < deadline)
        {
            completed.AddRange(planner.TakeCompleted());
            await Task.Delay(10);
        }
        Assert.Equal(planner.Capacity, completed.Count);
        Assert.All(completed, result =>
        {
            Assert.Equal(actor.Position, result.Actor.Position);
            Assert.Same(navigation, result.Navigation);
            Assert.True(result.Matches(result.Actor, navigation));
            Assert.False(result.Matches(result.Actor with { Position = actor.Position with { X = 10 } }, navigation));
            Assert.False(result.Matches(result.Actor with { Version = actor.Version + 1 }, navigation));
            Assert.False(result.Matches(result.Actor, new WorldNavigation(new(-100, -100, 100, 100), [], [])));
            Assert.NotEmpty(result.Waypoints);
            Assert.All(result.Waypoints, point => Assert.True(navigation.CanTraverse(actor.Position, point, true)));
        });
        Assert.Equal(0, planner.PendingCount);
        Assert.Equal(planner.Capacity, timings.Snapshot()["actors.pathfinding"].Count);
    }

    [Fact]
    public async Task TimingsPreserveCountsFromConcurrentWorkers()
    {
        var timings = new PerformanceTimings();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => { using var measurement = timings.Measure("parallel"); })));
        var result = timings.Snapshot()["parallel"];
        Assert.Equal(100, result.Count);
        Assert.True(result.MaximumMilliseconds >= result.LastMilliseconds);
    }
}
