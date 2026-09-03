namespace AlternateEarth.Server;

public sealed class ActorSimulationService : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(500);
    private readonly RealityWorld _world;
    private readonly RealitySocketHub _hub;

    public ActorSimulationService(RealityWorld world, RealitySocketHub hub)
    {
        _world = world;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var actors = _world.AdvanceActors(Tick);
            await _hub.BroadcastActorsAsync(actors, stoppingToken);
            var players = await _world.AdvanceStaminaAsync(Tick, stoppingToken);
            await _hub.BroadcastPlayersAsync(players, stoppingToken);
            var speech = _world.AdvanceActorSpeech(DateTimeOffset.UtcNow);
            await _hub.BroadcastChatAsync(speech, stoppingToken);
        }
    }
}
