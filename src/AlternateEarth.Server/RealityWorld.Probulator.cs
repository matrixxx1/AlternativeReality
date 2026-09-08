using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public CombatEvent? CancelPlayerCommand(string playerId)
    {
        if (!_activeProbulatorBeams.TryRemove(playerId, out _) || !_players.TryGetValue(playerId, out var player)) return null;
        return new CombatEvent(playerId, string.Empty, "probulator", player.Position, player.Position, false, 0, false,
            "Probulator stopped by a new command.", StatusEffect: "Probulator inactive", StatusEffectUntilUtc: DateTimeOffset.UtcNow);
    }

    private readonly TimeProvider _probulatorClock;
    private readonly ConcurrentDictionary<string, PendingProbulatorAbduction> _probulatorAbductions = new();

    public bool IsProbulatorAbducted(string targetId) => _probulatorAbductions.ContainsKey(targetId);

    private void EnsureNotProbulatorAbducted(string targetId)
    {
        if (IsProbulatorAbducted(targetId)) throw new InvalidOperationException($"The Probulator is holding this character until the {ProbulatorAbductionState.TotalSeconds}-second abduction ends.");
    }

    private bool TryStartProbulatorAbduction(PlayerState pilot, string targetId, bool isPlayer, WorldPosition origin,
        double damage, DateTimeOffset now, out ProbulatorAbductionState state)
    {
        state = new(pilot.Id, now, origin, pilot.Position, FindNearbySafeDrop(pilot.Position, targetId));
        return _probulatorAbductions.TryAdd(targetId, new(isPlayer, state, damage, 0, now.AddSeconds(ProbulatorAbductionState.LiftSeconds), false, []));
    }

    private static CombatEvent ProbulatorCaptureEvent(PlayerState pilot, string targetId, string name,
        WorldPosition origin, double health, ProbulatorAbductionState state) =>
        new(pilot.Id, targetId, "probulator", pilot.Position, origin, true, 0, false,
            $"{pilot.Name}'s Probulator captured {name}: {ProbulatorAbductionState.LiftSeconds} seconds lifting, {ProbulatorAbductionState.AboardSeconds} aboard, {ProbulatorAbductionState.LowerSeconds} lowering.", health,
            StatusEffect: "Abducted", StatusEffectUntilUtc: state.EndsAtUtc);

    private async Task AdvanceProbulatorAbductionsAsync(DateTimeOffset now, Dictionary<string, ActorState> actors,
        List<PlayerState> players, List<CombatEvent> combat, CancellationToken cancellationToken)
    {
        foreach (var pair in _probulatorAbductions.ToArray())
        {
            var pending = pair.Value; var state = pending.State;
            _players.TryGetValue(pair.Key, out var player);
            _actors.TryGetValue(pair.Key, out var actor);
            if (pending.IsPlayer ? player is null : actor is null)
            {
                _probulatorAbductions.TryRemove(pair.Key, out _);
                continue;
            }
            // Captives follow the moving ship until lowering begins. Once lowering starts,
            // freeze that ship anchor and safe landing point so flight cannot drag the landing.
            if (!pending.Lowering)
            {
                if (_players.TryGetValue(state.PilotId, out var pilot) && pilot.LocationId == "outdoor" && pilot.TravelMode == TravelMode.Ufo)
                    state = state with { ShipPosition = pilot.Position };
                if (now >= state.LoweringAtUtc)
                {
                    state = state with { DropPosition = FindNearbySafeDrop(state.ShipPosition, pair.Key) };
                    pending = pending with { Lowering = true };
                }
            }
            var seconds = Math.Max(0, (now - state.StartedAtUtc).TotalSeconds);
            var finished = now >= state.EndsAtUtc;
            var position = seconds < ProbulatorAbductionState.LiftSeconds ? InterpolateAbductionPosition(state.Origin, state.ShipPosition, seconds / ProbulatorAbductionState.LiftSeconds)
                : now < state.LoweringAtUtc ? state.ShipPosition
                : InterpolateAbductionPosition(state.ShipPosition, state.DropPosition, Math.Clamp((now - state.LoweringAtUtc).TotalSeconds / ProbulatorAbductionState.LowerSeconds, 0, 1));
            // Small damage pulses every three seconds through the landing. Catch up after delayed ticks
            // without increasing the configured total damage or applying pulses twice.
            const int pulseCount = ProbulatorAbductionState.TotalSeconds / 3;
            var duePulses = Math.Min(pulseCount, (int)(seconds / 3));
            var damage = TypedPulse(pair.Key, DamageType.Physical, pending.Damage / pulseCount * Math.Max(0, duePulses - pending.AppliedPulses));
            var name = player?.Name ?? actor!.Name;
            var oldHealth = player?.HealthHearts ?? actor!.HealthHearts;
            var health = Math.Round(player?.GodMode == true ? Math.Max(1, oldHealth - damage) : Math.Max(0, oldHealth - damage), 10);
            // Complete the full animation before resolving a lethal outcome.
            var died = finished && health <= 0;
            if (player is not null)
            {
                var updated = player with { Position = position, Terrain = Navigation.TerrainAt(position.X, position.Y), SpeedMetersPerSecond = 0, HealthHearts = health,
                    Abduction = finished ? null : state, Version = player.Version + 1 };
                if (died) updated = await DieAndResetPlayerAsync(updated, cancellationToken);
                if (!await SavePlayerAsync(updated, cancellationToken)) continue;
                if (died) await RewardPlayerKillAsync(state.PilotId, player, cancellationToken);
                health = updated.HealthHearts;
                players.Add(updated);
            }
            else if (died)
            {
                var attacker = _players.GetValueOrDefault(state.PilotId) ?? new PlayerState(state.PilotId, "UFO pilot", state.ShipPosition);
                await UpdateActorHealthAsync(attacker, actor! with { Position = position, Abduction = null }, 0, true, cancellationToken);
                _actorRoutes.TryRemove(pair.Key, out _);
                actors.Remove(pair.Key);
            }
            else
            {
                var updated = actor! with { Position = position, IsMoving = false, HealthHearts = health,
                    Abduction = finished ? null : state, Version = actor!.Version + 1 };
                _actors[pair.Key] = updated; actors[pair.Key] = updated;
            }
            if (damage > 0 || finished)
                combat.Add(new(state.PilotId, pair.Key, "probulator", state.ShipPosition, position, true, damage, died,
                    finished ? $"{name}'s {ProbulatorAbductionState.TotalSeconds}-second abduction finished." : $"The Probulator hurt {name} for {damage:0.##} hearts.",
                    health, StatusEffect: finished ? "Abduction complete" : "Probulator damage", StatusEffectUntilUtc: state.EndsAtUtc));
            if (seconds >= ProbulatorAbductionState.LiftSeconds && now < state.LoweringAtUtc && now >= pending.NextDialogueAtUtc)
            {
                var line = ProbulatorDialogue(pending.SpokenLines);
                combat.Add(new(state.PilotId, pair.Key, "probulator", state.ShipPosition, position, false, 0, false,
                    $"{name}: {line}", health, StatusEffect: "Aboard UFO", StatusEffectUntilUtc: state.EndsAtUtc, Dialogue: line));
                pending = pending with { NextDialogueAtUtc = now.AddSeconds(3), SpokenLines = [.. pending.SpokenLines, line] };
            }
            if (finished)
            {
                if (actor is not null)
                {
                    var relation = Relationship(state.PilotId, pair.Key) - FirstImpressionAdjustment(state.PilotId, pair.Key) - 1;
                    _relationships[(state.PilotId, pair.Key)] = relation;
                    if (!actor.IsTestCharacter) await _store.SaveRelationshipAsync(Configuration.Id, new(state.PilotId, pair.Key, relation), cancellationToken);
                }
                _probulatorAbductions.TryRemove(pair.Key, out _);
                _probulatorHits[(state.PilotId, pair.Key)] = now.AddSeconds(3);
            }
            else _probulatorAbductions.TryUpdate(pair.Key, pending with { State = state, AppliedPulses = duePulses }, pair.Value);
        }
    }

    private static WorldPosition InterpolateAbductionPosition(WorldPosition from, WorldPosition to, double amount) =>
        to with { X = from.X + (to.X - from.X) * amount, Y = from.Y + (to.Y - from.Y) * amount, Z = from.Z + (to.Z - from.Z) * amount };
}

public sealed record PendingProbulatorAbduction(bool IsPlayer, ProbulatorAbductionState State, double Damage,
    int AppliedPulses, DateTimeOffset NextDialogueAtUtc, bool Lowering, string[] SpokenLines);
