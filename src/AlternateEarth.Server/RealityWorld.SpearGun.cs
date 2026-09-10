using System.Collections.Concurrent;
using System.Security.Cryptography;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private sealed record FlyingSpear(string Id, string PlayerId, string TargetId, string LocationId, WorldPosition Start, WorldPosition End, double Damage, bool Accurate, DateTimeOffset Arrives);
    private readonly ConcurrentDictionary<string, FlyingSpear> _flyingSpears = new();
    private readonly ConcurrentQueue<PlayerState> _spearPlayerUpdates = new();
    public IReadOnlyList<PlayerState> TakeSpearPlayerUpdates() { var players = new List<PlayerState>(); while (_spearPlayerUpdates.TryDequeue(out var p)) players.Add(p); return players; }
    internal static double SpearFlightSeconds(double distance) => Math.Clamp(distance / 40, .35, 5);

    private async Task<CombatResult> LaunchSpearAsync(PlayerState player, string targetId, WorldPosition end, double damage, double range, CancellationToken token)
    {
        var distance = player.Position.Distance2D(end);
        var chance = Math.Clamp(_itemConfigurations["spearGun"].Accuracy * (1 - distance / range * .7) * ProgressionRules.Accuracy(StatsFor(player.Id)), .01, 1);
        var shot = new FlyingSpear(Guid.NewGuid().ToString("N"), player.Id, targetId, player.LocationId, player.Position, end, damage,
            RandomNumberGenerator.GetInt32(1_000_000) < chance * 1_000_000, _probulatorClock.GetUtcNow().AddSeconds(SpearFlightSeconds(distance)));
        _flyingSpears[shot.Id] = shot;
        await SaveInventoryAsync(player.Id, token);
        return new(new CombatEvent(player.Id, targetId, "spearGun", player.Position, end, false, 0, false, "Spear fired.", StatusEffect: "Spear in flight"),
            player, null, GetInventoryState(player.Id), null, _dungeons.GetValueOrDefault(player.LocationId));
    }

    public async Task<IReadOnlyList<CombatEvent>> AdvanceSpearsAsync(CancellationToken token = default)
    {
        var events = new List<CombatEvent>();
        foreach (var pending in _flyingSpears.Values.Where(s => s.Arrives <= _probulatorClock.GetUtcNow()).ToArray())
        {
            if (!_flyingSpears.TryRemove(pending.Id, out var shot) || !_players.TryGetValue(shot.PlayerId, out var player) || player.LocationId != shot.LocationId) continue;
            // Resolve against current positions: moving away can dodge a slow spear.
            var actor = FindActor(player.Id, shot.TargetId);
            var victim = _players.GetValueOrDefault(shot.TargetId);
            var bus = _buses.Values.FirstOrDefault(b => b.State.Id == shot.TargetId);
            var point = actor?.Position ?? victim?.Position ?? (bus is null ? null : (WorldPosition?)TransitGeometry.ClosestPoint(bus.State, shot.End));
            var location = actor?.LocationId ?? victim?.LocationId ?? "outdoor";
            var hit = shot.Accurate && point is not null && location == shot.LocationId && point.Value.Distance2D(shot.End) <= 2
                && actor?.IsPassingThroughPortal(_probulatorClock.GetUtcNow()) != true && !IsProbulatorAbducted(shot.TargetId);
            if (victim is not null && (!Configuration.PvpEnabled || ShieldDeflects(victim, true))) hit = false;
            var damage = hit ? victim is null ? shot.Damage : ShieldReducedDamage(victim, shot.Damage) : 0;
            var health = actor?.HealthHearts ?? victim?.HealthHearts ?? bus?.State.HealthHearts ?? 0;
            var died = hit && victim?.GodMode != true && health <= damage;
            health = hit ? Math.Max(victim?.GodMode == true ? 1 : 0, health - damage) : health;
            if (hit && actor is not null)
            {
                _relationships[(player.Id, actor.Id)] = Math.Min(-2, Relationship(player.Id, actor.Id) - 1);
                await UpdateActorHealthAsync(player, actor, health, died, token);
                if (died) await RecordQuestKillAsync(player.Id, actor, token);
            }
            if (hit && victim is not null)
            {
                var updated = died ? await DieAndResetPlayerAsync(victim with { HealthHearts = 0 }, token) : victim with { HealthHearts = health, Version = victim.Version + 1 };
                await SavePlayerAsync(updated, token); _spearPlayerUpdates.Enqueue(updated);
                if (died) await RewardPlayerKillAsync(player.Id, victim, token);
            }
            if (hit && bus is not null)
            {
                await _transitLock.WaitAsync(token);
                try
                {
                    health = Math.Max(0, bus.State.HealthHearts - damage); died = health <= 0;
                    bus.State = bus.State with { HealthHearts = health, Status = died ? "pulling over" : bus.State.Status, Version = bus.State.Version + 1 };
                    if (died) { bus.Dwell = 0; bus.ReverseMeters = 0; }
                    ReactVehicleOccupants(player.Id, bus.State.Id, bus.State.Position); PublishTransit();
                }
                finally { _transitLock.Release(); }
            }
            events.Add(new(player.Id, shot.TargetId, "spearImpact", shot.Start, shot.End, hit, damage, died,
                hit ? died ? "Spear defeated the target." : $"Spear hit for {damage:0.#} hearts." : "Spear missed.", point is null ? null : health));
        }
        return events;
    }

    private async Task<HostileTick> AdvanceUnderwaterAsync(PlayerState player, DungeonState dungeon, TimeSpan elapsed, CancellationToken token)
    {
        var changed = new List<ActorState>(); var combat = new List<CombatEvent>(); var players = new List<PlayerState>();
        var now = _probulatorClock.GetUtcNow();
        foreach (var actor in dungeon.Actors)
        {
            var fish = actor.Subtype == "fish" || NutritionCatalog.Shellfish.Contains(actor.Subtype);
            var chest = dungeon.Chests.FirstOrDefault(c => c.Id == actor.FactionId);
            var chase = !fish && actor.Position.Distance2D(player.Position) < (chest is null ? 18 : 12);
            var destination = chase ? player.Position : chest?.Position ?? actor.Position with
            { X = actor.Position.X + Math.Cos(now.ToUnixTimeMilliseconds() / 2500d + StableInt(actor.Id)), Y = actor.Position.Y + Math.Sin(now.ToUnixTimeMilliseconds() / 3300d + StableInt(actor.Id)) };
            var distance = actor.Position.Distance2D(destination);
            var speed = fish ? .7 : 1.3 + dungeon.Difficulty * .018;
            var step = Math.Min(distance, speed * Math.Clamp(elapsed.TotalSeconds, 0, 1));
            var next = distance < .01 ? actor.Position : actor.Position with
            { X = Math.Clamp(actor.Position.X + (destination.X - actor.Position.X) / distance * step, 1, dungeon.Width - 1),
              Y = Math.Clamp(actor.Position.Y + (destination.Y - actor.Position.Y) / distance * step, 1, dungeon.Height - 2) };
            next = ScubaGeometry.ClampPosition(dungeon.Width, dungeon.Height, dungeon.Underwater!, next);
            var updated = actor with { Position = next, Facing = next.X < actor.Position.X ? "west" : "east", IsMoving = true, Version = actor.Version + 1 };
            SetActor(dungeon.Id, updated); changed.Add(updated);
            if (!chase || next.Distance2D(player.Position) > 1.6 || _lastActorAttack.TryGetValue((actor.Id, player.Id), out var prior) && now - prior < TimeSpan.FromSeconds(2.5)) continue;
            _lastActorAttack[(actor.Id, player.Id)] = now;
            var damage = (chest is null ? .5 : 1) + dungeon.Difficulty * .025;
            var health = Math.Max(player.GodMode ? 1 : 0, player.HealthHearts - damage);
            var died = health <= 0;
            player = died ? await DieAndResetPlayerAsync(player with { HealthHearts = 0 }, token) : player with { HealthHearts = health, Version = player.Version + 1 };
            await SavePlayerAsync(player, token); players.Add(player);
            combat.Add(new(actor.Id, player.Id, actor.Subtype.Contains("Octopus") || actor.Subtype == "octopus" ? "tentacles" : "bite", next, player.Position, true, damage, died, $"{actor.Name} attacked for {damage:0.#} hearts.", health));
            if (died) break;
        }
        return new(changed, players, combat, []);
    }
}
