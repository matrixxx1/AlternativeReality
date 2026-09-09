namespace AlternateEarth.Server;

public sealed class BusSimulationService(RealityWorld world, RealitySocketHub hub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = TimeSpan.FromMilliseconds(100);
        using var timer = new PeriodicTimer(tick);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!world.IsInitialized) continue;
            using var tickTiming = world.Timings.Measure("transit.tick");
            await hub.BroadcastTransitAsync(await world.AdvanceTransitAsync(tick, stoppingToken), stoppingToken);
        }
    }
}

// Geographic downloads never stall movement or the actor simulation.
public sealed class BusAreaLoadingService(RealityWorld world, ILogger<BusAreaLoadingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!world.IsInitialized) continue;
            try { await world.PrepareBusAreasAsync(stoppingToken); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(ex, "Bus map expansion failed; service will retry."); }
        }
    }
}
