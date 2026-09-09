using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<string, PendingAreaHazard> _areaHazards = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _sleepUntil = new();
    private long _areaHazardRevision;
    public long AreaHazardRevision => Interlocked.Read(ref _areaHazardRevision);
    public IReadOnlyList<AreaHazardState> GetAreaHazards() => _areaHazards.Values.Select(item => item.State).ToArray();
    public bool IsGasAsleep(string id) => _sleepUntil.TryGetValue(id, out var until) && until > _probulatorClock.GetUtcNow();

    public async Task<CombatResult> ThrowHazardAsync(string playerId, ThrowHazardRequest request, CancellationToken cancellationToken = default)
    {
        EnsureNotProbulatorAbducted(playerId);
        if (IsGasAsleep(playerId)) throw new InvalidOperationException("You are asleep until the gas effect wears off.");
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (EventPaused(player) || InsideInversion(player) && _activeInversion?.Type == "plants" || _dungeons.GetValueOrDefault(player.LocationId)?.EventBattle is not null) throw new InvalidOperationException("Use this event’s battle controls.");
        var definition = HazardCatalog.Find(player.EquippedWeapon) ?? throw new InvalidOperationException("Equip a crafted gas bottle or jar first.");
        if (player.TravelMode == TravelMode.Ufo) throw new InvalidOperationException("Leave your UFO before throwing a bottle or jar.");
        if (!double.IsFinite(request.X) || !double.IsFinite(request.Y)) throw new InvalidOperationException("Choose a valid throw destination.");
        var target = player.Position with { X = request.X, Y = request.Y };
        var range = _itemConfigurations[definition.ItemType].RangeMeters;
        if (player.Position.Distance2D(target) > range) throw new InvalidOperationException($"Throw within {range:0.#} meters.");
        if (player.LocationId == "outdoor" ? !Navigation.CanTraverse(player.Position, target) :
            !_dungeons.TryGetValue(player.LocationId, out var interior) || !InteriorPositionIsSafe(target, interior) || interior.Walls.Any(wall => CrossesDungeonWall(player.Position, target, wall)))
            throw new InvalidOperationException("A solid obstacle blocks that throw.");
        var now = _probulatorClock.GetUtcNow();
        if (_lastPlayerAttack.TryGetValue((playerId, definition.ItemType), out var previous) && now - previous < TimeSpan.FromSeconds(1))
            throw new InvalidOperationException("Wait a moment before throwing again.");
        if (!player.GodMode && !RemoveInventory(playerId, definition.ItemType, 1)) throw new InvalidOperationException("You have no more of that bottle or jar.");
        await SaveInventoryAsync(playerId, cancellationToken);
        _lastPlayerAttack[(playerId, definition.ItemType)] = now;
        var updated = NormalizeEquipmentAfterInventoryChange(player with { Version = player.Version + 1 });
        await SavePlayerAsync(updated, cancellationToken);
        var state = new AreaHazardState($"hazard:{Guid.NewGuid():N}", playerId, player.LocationId, target,
            definition.Name, definition.Effect, 6, now, now.AddSeconds(definition.DurationSeconds));
        _areaHazards[state.Id] = new PendingAreaHazard(state, definition);
        Interlocked.Increment(ref _areaHazardRevision);
        var combat = new CombatEvent(playerId, state.Id, definition.ItemType, player.Position, target, false, 0, false,
            $"{player.Name} threw {definition.Name}. Area effect lasts {definition.DurationSeconds} seconds and affects everyone inside.");
        return new(combat, updated, null, GetInventoryState(playerId), null, null);
    }

    private bool HazardTouches(AreaHazardState zone, WorldPosition target)
    {
        if (zone.Position.Distance2D(target) > zone.RadiusMeters) return false;
        return zone.LocationId == "outdoor" ? Navigation.CanTraverse(zone.Position, target) :
            _dungeons.TryGetValue(zone.LocationId, out var dungeon) && !dungeon.Walls.Any(wall => CrossesDungeonWall(zone.Position, target, wall));
    }

    private async Task AdvanceAreaHazardsAsync(DateTimeOffset now, Dictionary<string, ActorState> actors,
        List<PlayerState> players, List<CombatEvent> combat, CancellationToken cancellationToken)
    {
        foreach (var sleep in _sleepUntil.ToArray())
        {
            if (sleep.Value > now || !_sleepUntil.TryRemove(sleep.Key, out _)) continue;
            if (_players.TryGetValue(sleep.Key, out var waking))
            {
                var updated = waking with { AsleepUntilUtc = null, Version = waking.Version + 1 };
                if (await SavePlayerAsync(updated, cancellationToken)) players.Add(updated);
            }
            else foreach (var location in _dungeons.Keys.Prepend("outdoor"))
            {
                var wakingActor = ActorsAtLocation(location).FirstOrDefault(actor => actor.Id == sleep.Key);
                if (wakingActor is null) continue;
                var updated = wakingActor with { AsleepUntilUtc = null, Version = wakingActor.Version + 1 };
                SetActor(location, updated); actors[updated.Id] = updated; break;
            }
        }
        foreach (var pending in _areaHazards.Values.ToArray())
        {
            var zone = pending.State;
            var due = Math.Min(pending.Definition.DurationSeconds, Math.Max(0, (int)(now - zone.StartedAtUtc).TotalSeconds));
            var pulses = due - pending.AppliedPulses;
            var damage = pending.Definition.DamagePerSecond * pulses * ProgressionRules.Damage(StatsFor(zone.OwnerId));
            foreach (var target in _players.Values.Where(player => player.LocationId == zone.LocationId && player.TravelMode != TravelMode.Ufo && !IsProbulatorAbducted(player.Id) && HazardTouches(zone, player.Position)).ToArray())
            {
                var sleeps = now < zone.EndsAtUtc && pending.Definition.SleepSeconds > 0 && pending.SleepAffected.Add(target.Id);
                if (damage <= 0 && !sleeps) continue;
                var until = sleeps ? now.AddSeconds(pending.Definition.SleepSeconds) : target.AsleepUntilUtc;
                if (sleeps) { until = _sleepUntil.AddOrUpdate(target.Id, until!.Value, (_, prior) => prior > until.Value ? prior : until.Value); }
                var health = target.GodMode ? Math.Max(1, target.HealthHearts - damage) : Math.Max(0, target.HealthHearts - damage);
                var died = health <= 0;
                var updated = target with { HealthHearts = health, AsleepUntilUtc = until, SpeedMetersPerSecond = sleeps ? 0 : target.SpeedMetersPerSecond, Version = target.Version + 1 };
                if (died) { _sleepUntil.TryRemove(target.Id, out _); updated = await DieAndResetPlayerAsync(updated with { AsleepUntilUtc = null }, cancellationToken); }
                if (!await SavePlayerAsync(updated, cancellationToken)) continue;
                if (died) await RewardPlayerKillAsync(zone.OwnerId, target, cancellationToken);
                players.Add(updated);
                combat.Add(HazardCombat(zone, target.Id, target.Name, target.Position, damage, updated.HealthHearts, died, sleeps, until));
            }
            foreach (var target in ActorsAtLocation(zone.LocationId).Where(actor => actor.Subtype != "ufo" && !actor.IsPassingThroughPortal(now) && !IsProbulatorAbducted(actor.Id) && HazardTouches(zone, actor.Position)).ToArray())
            {
                var sleeps = now < zone.EndsAtUtc && pending.Definition.SleepSeconds > 0 && pending.SleepAffected.Add(target.Id);
                if (damage <= 0 && !sleeps) continue;
                var until = sleeps ? now.AddSeconds(pending.Definition.SleepSeconds) : target.AsleepUntilUtc;
                if (sleeps) { until = _sleepUntil.AddOrUpdate(target.Id, until!.Value, (_, prior) => prior > until.Value ? prior : until.Value); }
                var health = Math.Max(0, target.HealthHearts - damage); var died = health <= 0;
                var updated = target with { HealthHearts = health, AsleepUntilUtc = until, IsMoving = sleeps ? false : target.IsMoving, Version = target.Version + 1 };
                if (died)
                {
                    _sleepUntil.TryRemove(target.Id, out _);
                    var owner = _players.GetValueOrDefault(zone.OwnerId) ?? new PlayerState(zone.OwnerId, "Thrower", zone.Position, LocationId: zone.LocationId);
                    await UpdateActorHealthAsync(owner with { LocationId = zone.LocationId }, target, 0, true, cancellationToken); actors.Remove(target.Id);
                }
                else { SetActor(zone.LocationId, updated); actors[updated.Id] = updated; }
                combat.Add(HazardCombat(zone, target.Id, target.Name, target.Position, damage, health, died, sleeps, until));
            }
            pending.AppliedPulses = due;
            if (now >= zone.EndsAtUtc && _areaHazards.TryRemove(zone.Id, out _)) Interlocked.Increment(ref _areaHazardRevision);
        }
    }

    private static CombatEvent HazardCombat(AreaHazardState zone, string targetId, string name, WorldPosition position,
        double damage, double health, bool died, bool sleeps, DateTimeOffset? sleepUntil) =>
        new(zone.OwnerId, targetId, "areaHazard", zone.Position, position, damage > 0, damage, died,
            sleeps ? $"{name} fell asleep for 10 seconds." : $"{zone.Name} hurt {name} for {damage:0.##} hearts.", health,
            StatusEffect: sleeps ? "Gas sleep" : zone.Effect is "napalm" or "acidGas" ? "Hazard burn" : "Gas exposure",
            StatusEffectUntilUtc: sleeps ? sleepUntil : zone.EndsAtUtc);
}

internal sealed class PendingAreaHazard(AreaHazardState state, HazardDefinition definition)
{
    public AreaHazardState State { get; } = state;
    public HazardDefinition Definition { get; } = definition;
    public int AppliedPulses { get; set; }
    public HashSet<string> SleepAffected { get; } = new();
}
