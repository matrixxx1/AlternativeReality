namespace AlternateEarth.Server;

public sealed class WeatherRefreshService : BackgroundService
{
    private readonly RealityWorld _world;
    private readonly RealitySocketHub _hub;
    private readonly ILogger<WeatherRefreshService> _logger;

    public WeatherRefreshService(RealityWorld world, RealitySocketHub hub, ILogger<WeatherRefreshService> logger)
    {
        _world = world;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!await _world.RefreshWeatherAsync(stoppingToken))
            {
                _logger.LogWarning("Live weather refresh failed; retaining the last successful conditions.");
                continue;
            }
            await _hub.BroadcastWeatherAsync(stoppingToken);
        }
    }
}
