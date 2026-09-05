namespace AlternateEarth.Server;

public sealed class SmokeTestAccountCleanupService : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(1);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
    private readonly SqliteRealityStore _store;
    private readonly RealityWorld _world;
    private readonly ILogger<SmokeTestAccountCleanupService> _logger;

    public SmokeTestAccountCleanupService(SqliteRealityStore store, RealityWorld world, ILogger<SmokeTestAccountCleanupService> logger)
    {
        _store = store;
        _world = world;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var removed = await _store.DeleteExpiredTestAccountsAsync(DateTimeOffset.UtcNow - Retention, stoppingToken);
            if (removed.Count == 0) continue;
            _world.PurgeTestAccounts(removed);
            _logger.LogInformation("Removed {Count} smoke-test accounts older than one hour: {Accounts}", removed.Count, string.Join(", ", removed.Select(account => account.Username)));
        }
    }
}
