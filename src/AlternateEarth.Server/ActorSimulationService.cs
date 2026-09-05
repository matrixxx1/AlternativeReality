namespace AlternateEarth.Server;

public sealed class ActorSimulationService : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(500);
    private readonly RealityWorld _world;
    private readonly RealitySocketHub _hub;
    private long _doorLockCycle;

    public ActorSimulationService(RealityWorld world, RealitySocketHub hub)
    {
        _world = world;
        _hub = hub;
        _doorLockCycle = world.CurrentDoorLockCycle;
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
            var hostile = await _world.AdvanceHostilityAsync(Tick, stoppingToken);
            await _hub.BroadcastActorsAsync(hostile.Actors, stoppingToken);
            await _hub.BroadcastPlayersAsync(hostile.Players, stoppingToken);
            await _hub.BroadcastCombatAsync(hostile.Combat, stoppingToken);
            await _hub.BroadcastRemovedWorldObjectsAsync(hostile.RemovedWorldObjectIds ?? Array.Empty<string>(), stoppingToken);
            var speech = _world.AdvanceActorSpeech(DateTimeOffset.UtcNow);
            await _hub.BroadcastChatAsync(speech, stoppingToken);
            var currentLockCycle = _world.CurrentDoorLockCycle;
            if (currentLockCycle != _doorLockCycle)
            {
                var lockSchedule = _world.GetDoorLockSchedule();
                _doorLockCycle = lockSchedule.Cycle;
                await _hub.BroadcastDoorLocksAsync(lockSchedule, stoppingToken);
            }
        }
    }
}
