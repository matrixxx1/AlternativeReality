using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class LatestCommandWorkerTests
{
    [Fact]
    public async Task ReplacementCancelsTheOldRouteBeforeStartingTheNewOne()
    {
        await using var worker = new LatestCommandWorker();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replaced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldStopped = false;
        worker.Replace(async token =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { oldStopped = true; }
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        worker.Replace(token =>
        {
            Assert.True(oldStopped);
            Assert.False(token.IsCancellationRequested);
            replaced.SetResult();
            return Task.CompletedTask;
        });
        await replaced.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelStopsAnActiveRouteWithoutNeedingAReplacement()
    {
        var worker = new LatestCommandWorker();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = false;
        worker.Replace(async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); completed = true; });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        worker.Cancel();
        await worker.DisposeAsync();
        Assert.False(completed);
    }

    [Fact]
    public void CancelledNavigationCannotReturnEvenADirectRoute()
    {
        var navigation = new WorldNavigation(new WorldBounds(-50, -50, 50, 50), Array.Empty<CanonicalEntity>(), Array.Empty<ElevationSample>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => navigation.FindPath(new WorldPosition(new RegionId(45, -123), 0, 0), 5, 5, cancellationToken: cancellation.Token));
    }
}
