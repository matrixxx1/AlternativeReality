namespace AlternateEarth.Server;

public sealed class ActorSimulationService : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(500);
    private readonly RealityWorld _world;
    private readonly RealitySocketHub _hub;
    private long _doorLockCycle;
    private long _storeHour;
    private long _areaHazardRevision;

    public ActorSimulationService(RealityWorld world, RealitySocketHub hub)
    {
        _world = world;
        _hub = hub;
        _doorLockCycle = world.CurrentDoorLockCycle;
        _storeHour = world.CurrentStoreHour;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var tickTiming = _world.Timings.Measure("actors.tick");
            await _world.AdvanceInversionsAsync(Tick, stoppingToken);
            await _hub.BroadcastInversionsAsync(stoppingToken);
            await _world.AdvanceProgressionAsync(DateTimeOffset.UtcNow, stoppingToken);
            await _hub.SendProgressionNoticesAsync(_world.TakeProgressionNotices(), stoppingToken);
            await _world.AdvanceFoodDeliveriesAsync(stoppingToken);
            await _hub.SendQuestNoticesAsync(_world.TakeQuestNotices(), stoppingToken);
            await _hub.BroadcastChatAsync(_world.TakeQuestDialogue(), stoppingToken);
            await _hub.BroadcastActorsAsync(_world.AdvanceDeliveryDogs(Tick), stoppingToken);
            await _hub.SendRelationshipsAsync(_world.TakeDeliveryDogRelationships(), stoppingToken);
            await _hub.BroadcastRemovedActorsAsync(_world.TakeDeliveryActorRemovals(), stoppingToken);
            await _hub.BroadcastGardensAsync(await _world.AdvanceFarmsAsync(stoppingToken),stoppingToken);
            var gardens=await _world.AdvanceGardensAsync(stoppingToken);
            await _hub.BroadcastGardensAsync(gardens.Changed,stoppingToken);
            await _hub.BroadcastRemovedWorldObjectsAsync(gardens.Removed,stoppingToken);
            var actors = _world.AdvanceActors(Tick);
            await _hub.BroadcastActorsAsync(actors, stoppingToken);
            var players = await _world.AdvanceStaminaAsync(Tick, stoppingToken);
            await _hub.BroadcastPlayersAsync(players, stoppingToken);
            var firstImpressions = await _world.AdvanceFirstImpressionsAsync(stoppingToken);
            await _hub.SendRelationshipsAsync(firstImpressions, stoppingToken);
            await _hub.BroadcastCombatAsync(await _world.AdvanceSpearsAsync(stoppingToken), stoppingToken);
            await _hub.BroadcastPlayersAsync(_world.TakeSpearPlayerUpdates(), stoppingToken);
            var hostile = await _world.AdvanceHostilityAsync(Tick, stoppingToken);
            await _hub.BroadcastActorsAsync(hostile.Actors, stoppingToken);
            await _hub.BroadcastPlayersAsync(hostile.Players, stoppingToken);
            await _hub.BroadcastCombatAsync(hostile.Combat, stoppingToken);
            var hazardRevision = _world.AreaHazardRevision;
            if (_areaHazardRevision != hazardRevision)
            {
                await _hub.BroadcastAreaHazardsAsync(stoppingToken);
                _areaHazardRevision = hazardRevision;
            }
            await _hub.BroadcastRemovedWorldObjectsAsync(hostile.RemovedWorldObjectIds ?? Array.Empty<string>(), stoppingToken);
            await _hub.BroadcastLootAsync(_world.TakeDeathDropAnnouncements(), stoppingToken);
            var speech = _world.AdvanceActorSpeech(DateTimeOffset.UtcNow);
            await _hub.BroadcastChatAsync(speech, stoppingToken);
            var currentLockCycle = _world.CurrentDoorLockCycle;
            var storeHour = _world.CurrentStoreHour;
            if (currentLockCycle != _doorLockCycle || storeHour != _storeHour)
            {
                var lockSchedule = _world.GetDoorLockSchedule();
                _doorLockCycle = lockSchedule.Cycle;
                _storeHour = storeHour;
                await _hub.BroadcastDoorLocksAsync(lockSchedule, stoppingToken);
            }
        }
    }
}
