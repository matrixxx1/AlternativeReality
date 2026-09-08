using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<(string Owner, string Item), GearStats> _gear = new();
    private readonly ConcurrentDictionary<(string Target, string Source, DamageType Type), CombatEffect> _combatEffects = new();
    private readonly ConcurrentDictionary<(string Player, string Actor), byte> _seenThreats = new();
    private readonly ConcurrentDictionary<(string Actor, int Level), IReadOnlyDictionary<string, GearStats>> _actorGear = new();
    private readonly SemaphoreSlim _combatRulesTick = new(1, 1);

    private bool IsGear(string item) => item is not ("fist" or "none" or "probulator" or "candle") &&
        (InventoryDefinition(item).Category == InventoryCategory.Weapon || HatItems.Contains(item) || ShirtItems.Contains(item) || PantsItems.Contains(item) || OffhandItems.Contains(item) || item is "magicHikingShoes" or "magicRunningShoes");
    private GearStats RollPlayerGear(string player, string item, string? quality, bool exceptional = false) => CombatRules.RollGear(
        CombatRules.LootLevel(GetProgression(player).Level, exceptional, Random.Shared), quality ?? RandomWeaponQuality(), Random.Shared, InventoryDefinition(item).Category != InventoryCategory.Weapon);
    private GearStats? GearFor(string player, string item) => !IsGear(item) || InventoryQuantity(player, item) <= 0 ? null :
        _gear.GetOrAdd((player, item), _ => RollPlayerGear(player, item, _weaponQualities.GetValueOrDefault((player, item))));
    private void RestoreGear(string owner, IEnumerable<ItemStack> items)
    {
        foreach (var item in items) if (item.Gear is { } gear) _gear[(owner, item.ItemType)] = gear;
    }
    private ItemStack LevelLoot(string player, ItemStack item, bool exceptional = false) => !IsGear(item.ItemType) || item.Gear is not null ? item :
        item with { Gear = RollPlayerGear(player, item.ItemType, item.Quality, exceptional) };
    private ChestContentsState LevelChestLoot(string player, ChestContentsState chest, bool stronghold) => chest with
        { Items = chest.Items.Select(item => LevelLoot(player, item, stronghold && Random.Shared.NextDouble() < .1)).ToArray() };
    public int EffectiveNutUp(string playerId) => StatsFor(playerId).NutUp + (_players.TryGetValue(playerId, out var p) && p.AlcoholUntilUtc > _probulatorClock.GetUtcNow() ? p.AlcoholNutUp : 0);
    private int GearLimit(string player) => CombatRules.EquipLimit(GetProgression(player).Level, EffectiveNutUp(player));
    private bool CanEquipGear(string player, string item) => (GearFor(player, item)?.Level ?? 1) <= GearLimit(player);
    private async Task<PlayerState> DrinkAlcoholAsync(PlayerState player, string item, CancellationToken token)
    {
        if (!RemoveInventory(player.Id, item, 1)) throw new InvalidOperationException($"You do not have any {item}.");
        var boost = item == "beer" ? 5 : item == "wine" ? 10 : 15;
        var now = _probulatorClock.GetUtcNow();
        var until = now.AddSeconds(item == "beer" ? 60 : item == "wine" ? 90 : 120);
        var updated = player with { AlcoholNutUp = Math.Max(boost, player.AlcoholUntilUtc > now ? player.AlcoholNutUp : 0),
            AlcoholUntilUtc = player.AlcoholUntilUtc > until ? player.AlcoholUntilUtc : until, Version = player.Version + 1 };
        await SaveInventoryAsync(player.Id, token); await SavePlayerAsync(updated, token);
        _progressionNotices.Enqueue(new(player.Id, $"Nut Up temporarily +{updated.AlcoholNutUp}; equip limit level {CombatRules.EquipLimit(GetProgression(player.Id).Level, StatsFor(player.Id).NutUp + updated.AlcoholNutUp)}."));
        return updated;
    }
    private PlayerState EnforceGearLevel(PlayerState player)
    {
        var now = _probulatorClock.GetUtcNow();
        var limit = CombatRules.EquipLimit(GetProgression(player.Id).Level, StatsFor(player.Id).NutUp + (player.AlcoholUntilUtc > now ? player.AlcoholNutUp : 0));
        var removed = new List<string>();
        bool Allowed(string item)
        {
            if ((GearFor(player.Id, item)?.Level ?? 1) <= limit) return true;
            removed.Add(DisplayItem(item)); return false;
        }
        var updated = player with {
            EquippedWeapon = Allowed(player.EquippedWeapon) ? player.EquippedWeapon : "fist",
            EquippedHat = Allowed(player.EquippedHat) ? player.EquippedHat : "none",
            EquippedShirt = Allowed(player.EquippedShirt) ? player.EquippedShirt : "none",
            EquippedPants = Allowed(player.EquippedPants) ? player.EquippedPants : "none",
            MagicHikingShoesOn = player.MagicHikingShoesOn && Allowed("magicHikingShoes"),
            MagicRunningShoesOn = player.MagicRunningShoesOn && Allowed("magicRunningShoes"),
            ShieldOn = player.ShieldOn && Allowed("shield"), FlashlightOn = player.FlashlightOn && Allowed("flashlight"),
            LanternOn = player.LanternOn && Allowed("lantern"), LaserOn = player.LaserOn && Allowed("laser"),
            AlcoholNutUp = player.AlcoholUntilUtc > now ? player.AlcoholNutUp : 0,
            AlcoholUntilUtc = player.AlcoholUntilUtc > now ? player.AlcoholUntilUtc : null };
        if (removed.Count > 0) _progressionNotices.Enqueue(new(player.Id, $"Equip limit is now level {limit}. Unequipped {string.Join(", ", removed)}; retained in your inventory."));
        return updated with { HatOn = updated.EquippedHat == "hat" };
    }
    private IEnumerable<string> WornItems(PlayerState p)
    {
        yield return p.EquippedHat; yield return p.EquippedShirt; yield return p.EquippedPants;
        if (p.ShieldOn) yield return "shield";
        if (p.MagicHikingShoesOn) yield return "magicHikingShoes";
        if (p.MagicRunningShoesOn) yield return "magicRunningShoes";
    }
    private ActorState EnsureActorGear(ActorState actor)
    {
        if (actor.Gear is not null && actor.Level > 0) return actor;
        var random = new Random(StableInt("gear:" + actor.Id));
        var level = actor.Level > 0 ? actor.Level : actor.LocationId != "outdoor" && _dungeons.TryGetValue(actor.LocationId, out var dungeon)
            ? Math.Max(1, dungeon.Difficulty) : random.Next(1, 11);
        var quality = CombatRules.Qualities[Math.Min(9, (level - 1) / 10 + random.Next(2))];
        return actor with { Level = level, Gear = _actorGear.GetOrAdd((actor.Id, level), _ => new Dictionary<string, GearStats> {
            ["hat"] = CombatRules.RollGear(level, quality, random), ["shirt"] = CombatRules.RollGear(level, quality, random),
            ["pants"] = CombatRules.RollGear(level, quality, random), ["weapon"] = CombatRules.RollGear(level, actor.WeaponQuality, random, false) }) };
    }
    public IReadOnlyDictionary<DamageType, double> ResistancesFor(string id)
    {
        if (_players.TryGetValue(id, out var player)) return CombatRules.Combine(WornItems(player).Select(item => GearFor(id, item)).OfType<GearStats>());
        var actor = _actors.GetValueOrDefault(id) ?? _dungeons.Values.SelectMany(d => d.Actors).FirstOrDefault(a => a.Id == id);
        return CombatRules.Combine(actor is null ? [] : EnsureActorGear(actor).Gear!.Values);
    }
    private bool Immune(string id) => (_actors.GetValueOrDefault(id) ?? _dungeons.Values.SelectMany(d => d.Actors).FirstOrDefault(a => a.Id == id))?.DamageImmune == true;
    private double TypedPulse(string target, DamageType type, double damage) => Immune(target) ? 0 : CombatRules.Reduce(damage, type, ResistancesFor(target));
    internal double ApplyTypedAttack(string source, string target, string location, string weapon, double raw)
    {
        if (raw <= 0 || Immune(target)) return 0;
        var now = _probulatorClock.GetUtcNow(); var total = 0d;
        foreach (var effect in CombatRules.Attack(weapon))
        {
            total += TypedPulse(target, effect.Type, raw * effect.InitialMultiplier);
            if (effect.Seconds <= 0 || effect.PerSecondMultiplier <= 0) continue;
            var key = (target, source, effect.Type);
            var next = new CombatEffect(source, effect.Type, raw * effect.PerSecondMultiplier, now.AddSeconds(1), now.AddSeconds(effect.Seconds), location);
            _combatEffects.AddOrUpdate(key, next, (_, old) => old.EndsAtUtc < now ? next : next with { NextTickUtc = old.NextTickUtc, DamagePerSecond = Math.Max(old.DamagePerSecond, next.DamagePerSecond) });
        }
        return total;
    }
    private IReadOnlyList<CombatEffect> EffectsFor(string target) => _combatEffects.Where(p => p.Key.Target == target).Select(p => p.Value).ToArray();
    private void ClearCombatEffects(string target) { foreach (var key in _combatEffects.Keys.Where(k => k.Target == target)) _combatEffects.TryRemove(key, out _); }
    private void RecordNonPlayerActorDeath(string source, ActorState actor)
    {
        if (!_actors.TryRemove(actor.Id, out _)) return;
        _actorRoutes.TryRemove(actor.Id, out _);
        ClearCombatEffects(actor.Id);
        _incursionKills.Enqueue((source, actor, false));
    }
    public ActorInspection InspectActor(string playerId, string actorId)
    {
        var actor = FindActor(playerId, actorId) ?? throw new InvalidOperationException("That character is no longer here.");
        var player = _players[playerId];
        if (player.Position.Distance2D(actor.Position) > PlayerCombatSight(player) || !CombatLineOfSight(player.LocationId, player.Position, actor.Position)) throw new InvalidOperationException("That character is out of sight.");
        actor = EnsureActorGear(actor) with { Effects = EffectsFor(actor.Id) };
        var relation = actor.FriendRating + Relationship(playerId, actorId);
        return new(actor, actor.Level, relation, relation < 0 ? "Foe" : relation > 0 ? "Friend" : "Neutral", EffectsFor(actorId), ResistancesFor(actorId), CombatRules.Attack(actor.EquippedWeapon));
    }
    private double PlayerCombatSight(PlayerState player) => _movementConfiguration.BaseVisibilityMeters * ProgressionRules.Vision(StatsFor(player.Id));
    private bool CombatLineOfSight(string location, WorldPosition from, WorldPosition to) => location == "outdoor" ? Navigation.CanTraverse(from, to) :
        _dungeons.TryGetValue(location, out var dungeon) && !dungeon.Walls.Any(w => CrossesDungeonWall(from, to, w));

    private async Task AdvanceCombatRulesAsync(TimeSpan elapsed, DateTimeOffset now, Dictionary<string, ActorState> actors, List<PlayerState> players, List<CombatEvent> combat, CancellationToken token)
    {
        await _combatRulesTick.WaitAsync(token);
        try
        {
            foreach (var original in _actors.Values.Concat(_dungeons.Values.SelectMany(d => d.Actors)).ToArray())
            {
                var actor = EnsureActorGear(original);
                if (original.Gear is null) { SetActor(actor.LocationId, actor); actors[actor.Id] = actor; }
            }
            foreach (var pair in _combatEffects.ToArray())
            {
                var effect = pair.Value;
                if (now < effect.NextTickUtc) continue;
                var target = pair.Key.Target;
                var player = _players.GetValueOrDefault(target);
                var actor = player is null ? ActorsAtLocation(effect.LocationId).FirstOrDefault(a => a.Id == target) : null;
                if ((player is null && actor is null) || player is not null && player.LocationId != effect.LocationId) { _combatEffects.TryRemove(pair.Key, out _); continue; }
                var tick = effect.NextTickUtc; var damage = 0d;
                while (tick <= now && tick <= effect.EndsAtUtc) { damage += TypedPulse(target, effect.Type, effect.DamagePerSecond); tick = tick.AddSeconds(1); }
                if (tick > effect.EndsAtUtc) _combatEffects.TryRemove(pair.Key, out _);
                else _combatEffects.TryUpdate(pair.Key, effect with { NextTickUtc = tick }, effect);
                if (damage <= 0) continue;
                var health = Math.Max(player?.GodMode == true ? 1 : 0, (player?.HealthHearts ?? actor!.HealthHearts) - damage);
                var died = health <= 0;
                var position = player?.Position ?? actor!.Position;
                if (player is not null)
                {
                    var updated = player with { HealthHearts = health, Effects = EffectsFor(target), Version = player.Version + 1 };
                    if (died) updated = await DieAndResetPlayerAsync(updated, token);
                    if (await SavePlayerAsync(updated, token)) players.Add(updated);
                    if (died) await RewardPlayerKillAsync(effect.SourceId, player, token);
                }
                else
                {
                    var owner = _players.GetValueOrDefault(effect.SourceId) ?? new PlayerState(effect.SourceId, "Attack", position, LocationId: effect.LocationId);
                    await UpdateActorHealthAsync(owner with { LocationId = effect.LocationId }, actor!, health, died, token);
                    if (!died) { var changed = actor! with { HealthHearts = health, Effects = EffectsFor(target), Version = actor!.Version + 1 }; SetActor(effect.LocationId, changed); actors[target] = changed; }
                }
                combat.Add(new(effect.SourceId, target, effect.Type.ToString(), position, position, true, damage, died, $"{effect.Type}: {damage:0.##} hearts", health, StatusEffect: effect.Type.ToString(), StatusEffectUntilUtc: effect.EndsAtUtc));
            }
            foreach (var original in _players.Values.ToArray())
            {
                var player = original;
                if (player.AlcoholUntilUtc is not null && player.AlcoholUntilUtc <= now) player = EnforceGearLevel(player);
                foreach (var actor in ActorsAtLocation(player.LocationId).Where(a => a.HealthHearts > 0 && a.Position.Distance2D(player.Position) <= PlayerCombatSight(player)))
                {
                    if (!CombatLineOfSight(player.LocationId, player.Position, actor.Position) || !_seenThreats.TryAdd((player.Id, actor.Id), 0)) continue;
                    if (actor.Level <= GetProgression(player.Id).Level + 20 || actor.FriendRating + Relationship(player.Id, actor.Id) >= 0 || player.GodMode) continue;
                    player = player with { FearedUntilUtc = now.AddSeconds(CombatRules.FearSeconds(EffectiveNutUp(player.Id), StatsFor(player.Id), EffectsFor(player.Id).Count)), FearSourceId = actor.Id };
                    _progressionNotices.Enqueue(new(player.Id, $"Feared by level {actor.Level} {actor.Name}! Running away.")); break;
                }
                if (player.FearedUntilUtc > now && !IsGasAsleep(player.Id) && !IsProbulatorAbducted(player.Id))
                {
                    var threat = FindActor(player.Id, player.FearSourceId ?? "");
                    if (threat is not null)
                    {
                        var angle = Math.Atan2(player.Position.Y - threat.Position.Y, player.Position.X - threat.Position.X);
                        foreach (var offset in new[] { 0d, .5, -.5, 1, -1, 1.5, -1.5 })
                        {
                            var step = Math.Min(2, elapsed.TotalSeconds * 4);
                            var next = player.Position with { X = player.Position.X + Math.Cos(angle + offset) * step, Y = player.Position.Y + Math.Sin(angle + offset) * step };
                            if (!CombatLineOfSight(player.LocationId, player.Position, next) || player.LocationId != "outdoor" && !InteriorPositionIsSafe(next, _dungeons[player.LocationId])) continue;
                            player = player with { Position = next, SpeedMetersPerSecond = 4 }; break;
                        }
                    }
                }
                else if (player.FearedUntilUtc is not null) player = player with { FearedUntilUtc = null, FearSourceId = null, SpeedMetersPerSecond = 0 };
                if (player != original) { player = player with { Version = original.Version + 1 }; if (await SavePlayerAsync(player, token)) players.Add(player); }
            }
        }
        finally { _combatRulesTick.Release(); }
    }
}
