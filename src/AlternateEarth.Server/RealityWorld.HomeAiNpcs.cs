using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public static readonly TimeSpan HomeAiNpcReplacementDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HomeAiNpcCheckInterval = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, HomeAiNpcRecord> _homeAiNpcs = new();
    private readonly SemaphoreSlim _homeAiNpcLock = new(1, 1);
    private DateTimeOffset _nextHomeAiNpcCheck = DateTimeOffset.MinValue;

    private async Task RestoreHomeAiNpcStateAsync(CancellationToken cancellationToken)
    {
        foreach (var record in await _store.LoadHomeAiNpcsAsync(Configuration.Id, cancellationToken)) _homeAiNpcs[record.BuildingId] = record;
        await AdvanceHomeAiNpcsAsync(true, true, cancellationToken);
    }

    public Task<IReadOnlyList<ActorState>> AdvanceHomeAiNpcsAsync(CancellationToken cancellationToken = default) =>
        AdvanceHomeAiNpcsAsync(false, false, cancellationToken);

    private Task<IReadOnlyList<ActorState>> EnsureHomeAiNpcsAsync(CancellationToken cancellationToken) =>
        AdvanceHomeAiNpcsAsync(true, false, cancellationToken);

    private async Task<IReadOnlyList<ActorState>> AdvanceHomeAiNpcsAsync(bool force, bool restoreMissing, CancellationToken cancellationToken)
    {
        var now = _probulatorClock.GetUtcNow();
        if (!force && now < _nextHomeAiNpcCheck) return Array.Empty<ActorState>();
        if (!await _homeAiNpcLock.WaitAsync(0, cancellationToken)) return Array.Empty<ActorState>();
        try
        {
            if (!force && now < _nextHomeAiNpcCheck) return Array.Empty<ActorState>();
            _nextHomeAiNpcCheck = now.Add(HomeAiNpcCheckInterval);
            var changed = new List<ActorState>();
            foreach (var orphan in _homeAiNpcs.Keys.Where(buildingId => !_publicBaseClaims.ContainsKey(buildingId)).ToArray())
            {
                if (_homeAiNpcs.TryRemove(orphan, out var removed))
                {
                    _actors.TryRemove(removed.ActorId, out _); EndAiNpcDialoguesForActor(removed.ActorId);
                    await _store.DeleteHomeAiNpcAsync(Configuration.Id, orphan, cancellationToken);
                }
            }
            foreach (var claim in _publicBaseClaims.Values.OrderBy(claim => claim.BuildingId))
            {
                if (!_baseEntities.TryGetValue(claim.BuildingId, out var building)) continue;
                var record = _homeAiNpcs.GetValueOrDefault(claim.BuildingId);
                var assigned = record is null ? null : _actors.GetValueOrDefault(record.ActorId);
                if (assigned is { HealthHearts: > 0, AiDialogueEnabled: true }) continue;
                var present = _actors.Values.FirstOrDefault(actor => actor.HealthHearts > 0 && actor.AiDialogueEnabled && actor.HomeBuildingId == claim.BuildingId);
                if (present is not null)
                {
                    var adopted = new HomeAiNpcRecord(Configuration.Id, claim.BuildingId, present.Id, present.Name, record?.Generation ?? 0, record?.SpawnedUtc ?? now);
                    _homeAiNpcs[claim.BuildingId] = adopted; await _store.SaveHomeAiNpcAsync(adopted, cancellationToken); continue;
                }
                if (record is { VacantSinceUtc: null } && !restoreMissing)
                {
                    var missing = record with { VacantSinceUtc = now };
                    _homeAiNpcs[claim.BuildingId] = missing; EndAiNpcDialoguesForActor(record.ActorId);
                    await _store.SaveHomeAiNpcAsync(missing, cancellationToken); continue;
                }
                if (record?.VacantSinceUtc is { } vacant && now - vacant < HomeAiNpcReplacementDelay) continue;
                var generation = record is null ? 0 : record.VacantSinceUtc is null ? record.Generation : record.Generation + 1;
                var actorId = record is { VacantSinceUtc: null } ? record.ActorId : $"ai-home:{claim.BuildingId}:{generation}";
                var actorName = record is { VacantSinceUtc: null } ? record.ActorName : UniqueNpcName(FriendlyHumanName(actorId, generation), actorId);
                var door = _baseEntities.Values.FirstOrDefault(entity => entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == claim.BuildingId);
                var anchor = door?.Position ?? building.Position;
                var position = Navigation.FindNearestWalkable(anchor with { X = anchor.X + 1.5, Y = anchor.Y + 1.5 });
                var actor = new ActorState(actorId, EntityKind.Npc, "resident", actorName, position,
                    HealthHearts: 5, MaximumHealthHearts: 5, EquippedWeapon: "fist",
                    AiDialogueEnabled: true, AiDialogueId: actorId, HomeBuildingId: claim.BuildingId);
                _actors[actor.Id] = actor;
                var next = new HomeAiNpcRecord(Configuration.Id, claim.BuildingId, actor.Id, actor.Name, generation, now);
                _homeAiNpcs[claim.BuildingId] = next; await _store.SaveHomeAiNpcAsync(next, cancellationToken); changed.Add(actor);
            }
            return changed;
        }
        finally { _homeAiNpcLock.Release(); }
    }

    private async Task RecordHomeAiNpcDeathAsync(ActorState actor, CancellationToken cancellationToken)
    {
        if (!actor.AiDialogueEnabled || actor.HomeBuildingId is null) return;
        EndAiNpcDialoguesForActor(actor.Id);
        var now = _probulatorClock.GetUtcNow();
        var prior = _homeAiNpcs.GetValueOrDefault(actor.HomeBuildingId) ??
            new HomeAiNpcRecord(Configuration.Id, actor.HomeBuildingId, actor.Id, actor.Name, 0, now);
        var vacant = prior with { ActorId = actor.Id, ActorName = actor.Name, VacantSinceUtc = now };
        _homeAiNpcs[actor.HomeBuildingId] = vacant;
        await _store.SaveHomeAiNpcAsync(vacant, cancellationToken);
    }

    private WorldPosition? HomeAiNpcAnchor(string? buildingId)
    {
        if (buildingId is null) return null;
        var door = _baseEntities.Values.FirstOrDefault(entity => entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == buildingId);
        return door?.Position ?? _baseEntities.GetValueOrDefault(buildingId)?.Position;
    }
}
