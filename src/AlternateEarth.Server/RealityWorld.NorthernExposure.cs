using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private bool _northernBossesSpawned;
    private readonly Dictionary<string, DateTimeOffset> _canadianFarts = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _mapleBoosts = new();
    private sealed record Bleed(string Owner, string Location, DateTimeOffset Next, int Ticks);
    private readonly ConcurrentDictionary<string, Bleed> _bleeds = new();
    private static bool IsCanadian(ActorState actor) => actor.Subtype is "canadian" or "canadianBoss";
    private bool MapleBoostActive(string id) => _mapleBoosts.GetValueOrDefault(id) > _probulatorClock.GetUtcNow();
    private static readonly string[] CanadianLines = ["Eh hoser!", "Knobber!", "Sorry! Terribly sorry about your ribs, eh.", "Pardon me! Was that your personal space?", "Oh, sorry! After you. Into the gas, preferably.", "Take off, eh!", "Please accept my sincerest apologies for the atmosphere.", "Sorry, bud. I'll send a very polite get-well card."];
    private void CanadianAttackSpeech(ActorState actor) => EventSay(actor.Id, actor.Name, CanadianLines[Random.Shared.Next(CanadianLines.Length)]);

    private void SpawnNorthernBosses(InversionState e)
    {
        _northernBossesSpawned = true;
        SpawnInversionActor(e, e.BossId, "canadianBoss", "Mecha Terry", 250, e.Center with { X = e.Center.X - 8 });
        SpawnInversionActor(e, e.BossId + ":phil", "canadianBoss", "Mecha Phil", 250, e.Center with { X = e.Center.X + 8 });
        _activeInversion = e with { Message = "50 Canadians defeated! Defeat BOTH Mecha Terry and Mecha Phil in their sweltering robot costumes." };
        EventSay("server", e.Name, _activeInversion.Message);
    }

    internal ItemStack[] NorthernTreasure()
    {
        var loot = new List<ItemStack> { InventoryStack("mapleSyrup", 5), InventoryStack("canadianMoney", 100), InventoryStack("hockeyStick", 2, quality: "Common") };
        if (ProgressionRoll() < .3) loot.Add(InventoryStack("recipe:mapleSyrup", 1));
        if (ProgressionRoll() < .3) loot.Add(InventoryStack("recipe:hockeyStick", 1));
        return loot.ToArray();
    }

    private async Task<PlayerState> ConsumeMapleSyrupAsync(PlayerState player, CancellationToken token)
    {
        if (!RemoveInventory(player.Id, "mapleSyrup", 1)) throw new InvalidOperationException("You do not have any maple syrup.");
        _mapleBoosts[player.Id] = _probulatorClock.GetUtcNow().AddMinutes(5);
        var updated = player with { Stamina = player.MaximumStamina, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2), FoodProtectedUntilUtc = _mapleBoosts[player.Id], Version = player.Version + 1 };
        await SaveInventoryAsync(player.Id, token); await SavePlayerAsync(updated, token);
        _progressionNotices.Enqueue(new(player.Id, "Maple syrup: +25% speed and +5 perception for five minutes."));
        return updated;
    }

    private async Task StepNorthernCombatAsync(double seconds, DateTimeOffset now, CancellationToken token)
    {
        if (_activeInversion is not { } e) return;
        var patches = e.Patches.Where(p => p.EndsAtUtc > now).ToList();
        foreach (var original in _actors.Values.Where(a => IsCanadian(a) && a.EventName == e.Name).ToArray())
        {
            if (original.IsPassingThroughPortal(now) || IsGasAsleep(original.Id) || IsProbulatorAbducted(original.Id)) continue;
            var actor = original;
            var boss = actor.Subtype == "canadianBoss";
            var target = _players.Values.Where(InsideInversion).OrderBy(p => p.Position.Distance2D(actor.Position)).FirstOrDefault();
            var range = boss ? 4 : _itemConfigurations[actor.EquippedWeapon].RangeMeters;
            if (target is not null && (actor.FartUntilUtc is null || actor.FartUntilUtc <= now))
            {
                var distance = actor.Position.Distance2D(target.Position);
                if (distance > range)
                {
                    var step = Math.Min(distance - range * .8, Math.Clamp(seconds, 0, 1) * (boss ? 2.5 : 2));
                    var next = actor.Position with { X = actor.Position.X + (target.Position.X - actor.Position.X) / distance * step, Y = actor.Position.Y + (target.Position.Y - actor.Position.Y) / distance * step };
                    if (next.Distance2D(e.Center) <= e.Radius && Navigation.CanTraverse(actor.Position, next)) actor = actor with { Position = next, IsMoving = true, Version = actor.Version + 1 };
                }
                else if (!boss && target.TravelMode != TravelMode.Ufo && _inversionAttacks.GetValueOrDefault(actor.Id) <= now)
                {
                    _inversionAttacks[actor.Id] = now.AddSeconds(1.5);
                    CanadianAttackSpeech(actor);
                    await EventHurtPlayerAsync(target, _itemConfigurations[actor.EquippedWeapon].Damage, actor.Id, actor.Position, actor.EquippedWeapon, token);
                    if (actor.EquippedWeapon == "iceSkate" && _players.GetValueOrDefault(target.Id)?.Position == target.Position)
                        await ApplyIceSkateHitAsync(actor.Id, target.Id, actor.Position, "outdoor", token);
                }
            }
            if (_canadianFarts.GetValueOrDefault(actor.Id) <= now)
            {
                _canadianFarts[actor.Id] = now.AddSeconds(Random.Shared.Next(3, 6));
                actor = actor with { FartUntilUtc = now.AddSeconds(1.2), FartPose = Random.Shared.Next(2) == 0 ? "bendOver" : "legUp", IsMoving = false, Version = actor.Version + 1 };
                patches.Add(new(Guid.NewGuid().ToString("N"), actor.Position, boss ? 6 : 2.5, "canadianGas", now.AddSeconds(.5), now.AddSeconds(3.5)));
                CanadianAttackSpeech(actor);
                _inversionCombat.Enqueue(new(actor.Id, actor.Id, "canadianFart", actor.Position, actor.Position, false, 0, false, "A green gas cloud!", StatusEffect: actor.FartPose, StatusEffectUntilUtc: actor.FartUntilUtc));
            }
            _actors[actor.Id] = actor; _inversionActorUpdates.Enqueue(actor);
        }
        if (now >= _nextCanadianGasTick)
        {
            _nextCanadianGasTick = now.AddSeconds(1);
            var gas = patches.Where(p => p.ChangesAtUtc <= now).ToArray();
            foreach (var player in _players.Values.Where(p => p.LocationId == "outdoor").ToArray())
            {
                var damage = Math.Min(4, gas.Count(p => p.Position.Distance2D(player.Position) <= p.Radius)) * .5;
                if (damage > 0) await EventHurtPlayerAsync(player, damage, e.BossId, player.Position, "canadianGas", token);
            }
            foreach (var actor in _actors.Values.Where(a => a.LocationId == "outdoor" && !IsCanadian(a)).ToArray())
            {
                var damage = Math.Min(4, gas.Count(p => p.Position.Distance2D(actor.Position) <= p.Radius)) * .5;
                if (damage <= 0) continue;
                var health = Math.Max(0, actor.HealthHearts - damage);
                if (health <= 0) { _actors.TryRemove(actor.Id, out _); _inversionRemovals.Enqueue(actor.Id); }
                else { var hurt = actor with { HealthHearts = health, Version = actor.Version + 1 }; _actors[actor.Id] = hurt; _inversionActorUpdates.Enqueue(hurt); }
                _inversionCombat.Enqueue(new(e.BossId, actor.Id, "canadianGas", actor.Position, actor.Position, true, damage, health <= 0, "Canadian gas", health));
            }
        }
        _activeInversion = _activeInversion! with { Patches = patches.ToArray() };
    }
    private DateTimeOffset _nextCanadianGasTick;

    private WorldPosition SkateKnockback(WorldPosition origin, WorldPosition position, string location)
    {
        var distance = origin.Distance2D(position); if (distance < .001) return position;
        var result = position;
        for (var n = 1; n <= 6; n++)
        {
            var next = position with { X = position.X + (position.X - origin.X) / distance * n * .5, Y = position.Y + (position.Y - origin.Y) / distance * n * .5 };
            if (location == "outdoor" ? !Navigation.CanTraverse(result, next) : !_dungeons.TryGetValue(location, out var dungeon) || next.X < 1 || next.Y < 1 || next.X > dungeon.Width - 1 || next.Y > dungeon.Height - 1 || dungeon.Walls.Any(w => CrossesDungeonWall(result, next, w))) break;
            result = next;
        }
        return result;
    }
    private async Task ApplyIceSkateHitAsync(string owner, string targetId, WorldPosition origin, string location, CancellationToken token)
    {
        if (_players.TryGetValue(targetId, out var player))
        {
            if (player.GodMode || player.HealthHearts <= 0 || player.LocationId != location) return;
            await SavePlayerAsync(player with { Position = SkateKnockback(origin, player.Position, location), Version = player.Version + 1 }, token);
        }
        else if (ActorsAtLocation(location).FirstOrDefault(a => a.Id == targetId) is { } actor && actor.HealthHearts > 0)
        {
            var pushed = actor with { Position = SkateKnockback(origin, actor.Position, location), Version = actor.Version + 1 };
            if (location == "outdoor") { _actors[targetId] = pushed; _inversionActorUpdates.Enqueue(pushed); }
            else if (_dungeons.TryGetValue(location, out var dungeon)) _dungeons[location] = dungeon with { Actors = dungeon.Actors.Select(a => a.Id == targetId ? pushed : a).ToArray() };
        }
        else return;
        _bleeds[targetId] = new(owner, location, _probulatorClock.GetUtcNow().AddSeconds(1), 3);
    }
    private async Task AdvanceBleedingAsync(DateTimeOffset now, CancellationToken token)
    {
        foreach (var (id, bleed) in _bleeds.ToArray())
        {
            if (now < bleed.Next) continue;
            if (bleed.Ticks <= 1) _bleeds.TryRemove(id, out _); else _bleeds[id] = bleed with { Ticks = bleed.Ticks - 1, Next = now.AddSeconds(1) };
            if (_players.TryGetValue(id, out var player))
            {
                if (player.LocationId != bleed.Location || player.GodMode) { _bleeds.TryRemove(id, out _); continue; }
                await EventHurtPlayerAsync(player, .25, bleed.Owner, player.Position, "bleeding", token);
                if (player.HealthHearts <= .25) _bleeds.TryRemove(id, out _);
            }
            else if (_players.TryGetValue(bleed.Owner, out var attacker) && ActorsAtLocation(bleed.Location).FirstOrDefault(a => a.Id == id) is { } actor && actor.LocationId == bleed.Location)
            {
                var health = Math.Max(0, actor.HealthHearts - .25);
                await UpdateActorHealthAsync(attacker with { LocationId = bleed.Location }, actor, health, health <= 0, token);
                _inversionCombat.Enqueue(new(bleed.Owner, id, "bleeding", actor.Position, actor.Position, true, .25, health <= 0, "Bleeding", health));
                if (health <= 0) _bleeds.TryRemove(id, out _);
            }
            else _bleeds.TryRemove(id, out _);
        }
    }
}
