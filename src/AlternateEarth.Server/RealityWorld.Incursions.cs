using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly SemaphoreSlim _incursionTick = new(1, 1);
    private readonly ConcurrentQueue<(string Player, ActorState Actor, bool PlayerKill)> _incursionKills = new();
    private readonly ConcurrentQueue<string> _incursionRemoved = new();
    private readonly ConcurrentQueue<ChatMessage> _incursionSpeech = new();
    private readonly Dictionary<string, ActorState> _northParkOriginals = new();
    private readonly HashSet<string> _incursionCounted = new();
    private readonly Dictionary<string, DateTimeOffset> _incursionAttackAt = new();
    private IncursionState? _incursion;
    private DateTimeOffset _nextIncursionAt, _nextIncursionSpeech, _nextChickenAt, _nextMartiniAt;
    private bool _nextAlanee;
    private int _kenSerial;
    public IncursionState? Incursion => _incursion;
    private static bool IsIncursionActor(ActorState a) => a.Id.StartsWith("incursion:", StringComparison.Ordinal);
    private bool IncursionContains(PlayerState player) => _incursion is { } e && player.LocationId == "outdoor" && player.Position.Distance2D(e.Center) <= e.Radius;
    public IReadOnlyList<string> TakeIncursionRemovals() { var result = new List<string>(); while (_incursionRemoved.TryDequeue(out var id)) result.Add(id); return result; }
    public IReadOnlyList<ChatMessage> TakeIncursionSpeech() { var result = new List<ChatMessage>(); while (_incursionSpeech.TryDequeue(out var item)) result.Add(item); return result; }
    public static IReadOnlyList<CelebrityQuote> AlaneeCast { get; } = [
        new("Tim Allen", "I don’t want to be too philosophical about it, but comedy is my coping mechanism.", "https://parade.com/890563/amyspencer/tim-allen-toy-story-comedy-sobriety/"),
        new("Woody Allen", "What I really like to do best is whatever I’m not doing at the moment.", "https://www.theparisreview.org/interviews/1550/the-art-of-humor-no-1-woody-allen"),
        new("Alan Rickman", "Actors are agents of change.", "https://libquotes.com/alan-rickman/quote/lbs4g9l") ];
    private static readonly string[] KenNames = ["Ken", "Kenny", "Kenneth", "Kenn", "Kendall", "Kendrick", "Kenji", "Kenton", "Kenzie", "Kennedy", "Kenny-Joe", "Kenrick"];
    private static readonly string[] NorthParkInsults = ["Your aim needs a permission slip.", "Even the snowman thinks you’re a dumbass.", "Did your inventory come from a lost-and-found?", "You fight like your mittens are tied together.", "Nice plan, genius. Did a bus write it?"];

    public async Task<IncursionState> StartIncursionAsync(string playerId, string type, CancellationToken token = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || !player.GodMode) throw new InvalidOperationException("God Mode is required to start a server event manually.");
        if (player.LocationId != "outdoor") throw new InvalidOperationException("Start this event outdoors.");
        if (type is not ("northPark" or "alanee")) throw new InvalidOperationException("Choose North park Hydra invasion or Alanee incursion.");
        await _incursionTick.WaitAsync(token);
        try { if (_incursion is not null) throw new InvalidOperationException("An incursion is already active."); StartIncursion(type, player.Position, _probulatorClock.GetUtcNow()); return _incursion!; }
        finally { _incursionTick.Release(); }
    }
    private void SayIncursion(string id, string name, string text) => _incursionSpeech.Enqueue(new(Guid.NewGuid().ToString("N"), id, name, text, _probulatorClock.GetUtcNow()));
    private void StartIncursion(string type, WorldPosition center, DateTimeOffset now)
    {
        var id = "incursion:" + Guid.NewGuid().ToString("N");
        _incursion = new(id, type, type == "northPark" ? "North park Hydra invasion" : "Alanee incursion", center, 100, now.AddMinutes(15), 0,
            type == "northPark" ? 50 : 12, type == "northPark" ? "Defeat 50 Ken Hydra targets. Every defeated Ken splits into two." : "Approach the person-sized eggs. Defeat their occupants to reveal Pierce Hawkeye.", id + ":boss");
        _incursionCounted.Clear(); _incursionAttackAt.Clear(); _kenSerial = 0; _nextIncursionSpeech = now; _nextIncursionAt = now.AddHours(12);
        if (type == "northPark") SpawnKen(center with { X = center.X + 8 });
        else for (var i = 0; i < 12; i++)
        {
            var angle = i * Math.Tau / 12;
            SpawnIncursionActor(id + ":egg:" + i, "alaneeEgg", "Person-sized egg", center with { X = center.X + Math.Cos(angle) * (12 + i * 3), Y = center.Y + Math.Sin(angle) * (12 + i * 3) }, 1, immune: true);
        }
        SayIncursion("server", _incursion.Name, _incursion.Objective);
    }
    private ActorState SpawnIncursionActor(string id, string subtype, string name, WorldPosition point, double health, string weapon = "fist", bool immune = false)
    {
        var e = _incursion!;
        var distance = point.Distance2D(e.Center);
        if (distance > e.Radius - 2) point = point with { X = e.Center.X + (point.X - e.Center.X) / distance * (e.Radius - 2), Y = e.Center.Y + (point.Y - e.Center.Y) / distance * (e.Radius - 2) };
        var safe = Navigation.FindNearestWalkable(point);
        if (safe.Distance2D(e.Center) > e.Radius || Navigation.IsBlocked(safe.X, safe.Y)) safe = e.Center;
        var actor = EnsureActorGear(new(id, EntityKind.Npc, subtype, name, safe, HealthHearts: health, MaximumHealthHearts: health,
            FriendRating: -10, EquippedWeapon: weapon, EventName: e.Name, EventEndsAtUtc: e.EndsAtUtc, DamageImmune: immune));
        _actors[id] = actor; return actor;
    }
    private void SpawnKen(WorldPosition point)
    {
        var serial = _kenSerial++;
        SpawnIncursionActor(_incursion!.Id + ":ken:" + serial, "kenHydra", KenNames[serial % KenNames.Length] + " Hydra", point, 5);
    }
    private async Task FinishIncursionAsync(bool won, CancellationToken token)
    {
        if (_incursion is not { } e) return;
        if (won) foreach (var player in _players.Values.Where(IncursionContains).Where(p => !p.IsTestCharacter).ToArray())
        {
            var loot = new LootDropState(e.Id + ":reward:" + player.Id, player.Position, player.LocationId, 15000,
                [LevelLoot(player.Id, InventoryStack("sword", 1, quality: "Fine"), exceptional: Random.Shared.NextDouble() < .1), InventoryStack("beer", 2)],
                DateTimeOffset.MaxValue, "eventReward", player.Name, player.Id);
            _loot[loot.Id] = loot; await _store.SavePersistentLootAsync(Configuration.Id, loot, token); _deathDropAnnouncements.Enqueue(loot);
            await AwardExperienceAsync(player.Id, 500, e.Name + " complete", e.Id, randomize: false, cancellationToken: token);
        }
        foreach (var actor in _actors.Values.Where(IsIncursionActor).ToArray()) { _actors.TryRemove(actor.Id, out _); ClearCombatEffects(actor.Id); _incursionRemoved.Enqueue(actor.Id); }
        foreach (var pair in _northParkOriginals) if (_actors.TryGetValue(pair.Key, out var survivor))
            _actors[pair.Key] = pair.Value with { Position = survivor.Position, HealthHearts = survivor.HealthHearts, Version = survivor.Version + 1 };
        _northParkOriginals.Clear();
        foreach (var zone in _areaHazards.Where(p => p.Value.State.OwnerId == e.BossId).ToArray()) if (_areaHazards.TryRemove(zone.Key, out _)) Interlocked.Increment(ref _areaHazardRevision);
        SayIncursion("server", e.Name, won ? "Event complete! Your reward is ready." : "The event has ended.");
        _incursion = null;
    }
    private async Task AdvanceIncursionsAsync(TimeSpan elapsed, DateTimeOffset now, Dictionary<string, ActorState> actors, List<PlayerState> players, List<CombatEvent> combat, CancellationToken token)
    {
        await _incursionTick.WaitAsync(token);
        try
        {
            if (_nextIncursionAt == default) _nextIncursionAt = now.AddHours(12);
            if (_incursion is null && now >= _nextIncursionAt && _players.Values.FirstOrDefault(p => !p.IsTestCharacter && p.LocationId == "outdoor") is { } anchor)
            { StartIncursion(_nextAlanee ? "alanee" : "northPark", anchor.Position, now); _nextAlanee = !_nextAlanee; }
            if (_incursion is not { } e) return;
            if (now >= e.EndsAtUtc) { await FinishIncursionAsync(false, token); foreach (var a in _actors.Values) actors[a.Id] = a; return; }
            while (_incursionKills.TryDequeue(out var kill))
            {
                if (!kill.Actor.Id.StartsWith(e.Id + ":", StringComparison.Ordinal) || !_incursionCounted.Add(kill.Actor.Id)) continue;
                _incursionRemoved.Enqueue(kill.Actor.Id);
                if (e.Type == "northPark" && kill.Actor.Subtype == "kenHydra")
                {
                    e = e with { Kills = e.Kills + (kill.PlayerKill ? 1 : 0) }; _incursion = e;
                    SpawnKen(kill.Actor.Position with { X = kill.Actor.Position.X + 2 }); SpawnKen(kill.Actor.Position with { X = kill.Actor.Position.X - 2 });
                    if (e.Kills >= 50) { await FinishIncursionAsync(true, token); foreach (var a in _actors.Values) actors[a.Id] = a; return; }
                }
                else if (e.Type == "alanee" && !kill.PlayerKill && kill.Actor.Subtype == "alaneeCelebrity")
                {
                    SpawnIncursionActor(kill.Actor.Id + ":replacement", "alaneeEgg", "Person-sized egg", kill.Actor.Position, 1, immune: true);
                }
                else if (e.Type == "alanee" && kill.PlayerKill && kill.Actor.Subtype == "alaneeCelebrity")
                {
                    e = e with { Kills = e.Kills + 1 }; _incursion = e;
                    if (e.Kills >= e.Goal && !_actors.ContainsKey(e.BossId))
                    {
                        SpawnIncursionActor(e.BossId, "pierceHawkeye", "Pierce Hawkeye", e.Center with { X = e.Center.X + 12 }, 100, "rocketLauncher", true);
                        _nextChickenAt = now.AddSeconds(5); _nextMartiniAt = now.AddSeconds(7);
                        _incursion = e = e with { Objective = "Pierce Hawkeye is immune. Kill his chickens: each deals 25 hearts to him." };
                    }
                }
                else if (kill.PlayerKill && kill.Actor.Subtype == "hawkeyeChicken" && _actors.TryGetValue(e.BossId, out var boss))
                {
                    var health = Math.Max(0, boss.HealthHearts - 25);
                    combat.Add(new(kill.Player, boss.Id, "chickenSacrifice", kill.Actor.Position, boss.Position, true, 25, health == 0, "Chicken defeated: Pierce Hawkeye loses 25 hearts.", health));
                    if (health == 0) { await FinishIncursionAsync(true, token); return; }
                    _actors[boss.Id] = boss with { HealthHearts = health, Version = boss.Version + 1 };
                }
            }
            var participants = _players.Values.Where(IncursionContains).ToArray();
            if (e.Type == "northPark") foreach (var actor in _actors.Values.Where(a => a.Kind == EntityKind.Npc && !IsIncursionActor(a) && a.LocationId == "outdoor" && a.Position.Distance2D(e.Center) <= e.Radius).ToArray())
            {
                if (_northParkOriginals.TryAdd(actor.Id, actor)) _actors[actor.Id] = actor with { Subtype = "northParkCitizen", Name = new[] { "Stanley Park", "Kyle Snow", "Eric Mitten", "Wendy Frost" }[(StableInt(actor.Id) & int.MaxValue) % 4], EventName = e.Name, IsMerchant = false, IsQuestGiver = false, OffersFoodDelivery = false, Version = actor.Version + 1 };
            }
            foreach (var egg in _actors.Values.Where(a => a.Subtype == "alaneeEgg" && participants.Any(p => p.Position.Distance2D(a.Position) <= 4 && CombatLineOfSight("outdoor", p.Position, a.Position))).ToArray())
            {
                _actors.TryRemove(egg.Id, out _); _incursionRemoved.Enqueue(egg.Id);
                var member = AlaneeCast[(StableInt(egg.Id) & int.MaxValue) % AlaneeCast.Count];
                var hatched = SpawnIncursionActor(egg.Id + ":hatched", "alaneeCelebrity", member.Name, egg.Position, 10, "fist");
                SayIncursion(hatched.Id, hatched.Name, member.Quote);
            }
            if (_actors.TryGetValue(e.BossId, out var hawkeye))
            {
                while (now >= _nextChickenAt)
                {
                    var id = e.Id + ":chicken:" + _nextChickenAt.ToUnixTimeMilliseconds(); _nextChickenAt = _nextChickenAt.AddSeconds(5);
                    SpawnIncursionActor(id, "hawkeyeChicken", "Hawkeye’s chicken", hawkeye.Position with { X = hawkeye.Position.X + 2 }, 1);
                }
                if (now >= _nextMartiniAt)
                {
                    _nextMartiniAt = now.AddSeconds(7);
                    hawkeye = hawkeye with { PouringUntilUtc = now.AddSeconds(1), Version = hawkeye.Version + 1 }; _actors[hawkeye.Id] = hawkeye;
                    var point = Navigation.FindNearestWalkable(hawkeye.Position with { X = hawkeye.Position.X + 3 });
                    var state = new AreaHazardState(e.Id + ":martini:" + Guid.NewGuid().ToString("N"), hawkeye.Id, "outdoor", point, "Martini acid", "acid", 2, now, now.AddSeconds(5));
                    _areaHazards[state.Id] = new PendingAreaHazard(state, new("martini", "Martini acid", "acid", 5, 1, 0, "", "")); Interlocked.Increment(ref _areaHazardRevision);
                }
            }
            foreach (var original in _actors.Values.Where(IsIncursionActor).ToArray())
            {
                if (original.Subtype is "alaneeEgg" or "hawkeyeChicken") { actors[original.Id] = original; continue; }
                var actor = original;
                var fromCenter = actor.Position.Distance2D(e.Center);
                if (fromCenter > e.Radius) actor = actor with { Position = e.Center, Version = actor.Version + 1 };
                var target = participants.Select(p => _players.GetValueOrDefault(p.Id, p)).OrderBy(p => p.Position.Distance2D(actor.Position)).FirstOrDefault();
                if (target is null) { actors[actor.Id] = actor; continue; }
                var distance = actor.Position.Distance2D(target.Position); var range = actor.Subtype == "pierceHawkeye" ? 35 : 2;
                if (distance > range)
                {
                    var step = Math.Min(Math.Min(1, elapsed.TotalSeconds * 2), distance - range);
                    var next = actor.Position with { X = actor.Position.X + (target.Position.X - actor.Position.X) / distance * step, Y = actor.Position.Y + (target.Position.Y - actor.Position.Y) / distance * step };
                    if (next.Distance2D(e.Center) <= e.Radius && Navigation.CanTraverse(actor.Position, next)) actor = actor with { Position = next, IsMoving = true, Version = actor.Version + 1 };
                }
                if (distance <= range && _incursionAttackAt.GetValueOrDefault(actor.Id) <= now && CombatLineOfSight("outdoor", actor.Position, target.Position))
                {
                    _incursionAttackAt[actor.Id] = now.AddSeconds(actor.Subtype == "pierceHawkeye" ? 3 : 4);
                    var damage = ApplyTypedAttack(actor.Id, target.Id, target.LocationId, actor.EquippedWeapon, actor.Subtype == "pierceHawkeye" ? 3 : .5);
                    var health = target.GodMode ? Math.Max(1, target.HealthHearts - damage) : Math.Max(0, target.HealthHearts - damage);
                    var updated = target with { HealthHearts = health, Version = target.Version + 1 };
                    if (health == 0) updated = await DieAndResetPlayerAsync(updated, token);
                    if (await SavePlayerAsync(updated, token)) players.Add(updated);
                    combat.Add(new(actor.Id, target.Id, actor.EquippedWeapon, actor.Position, target.Position, true, damage, health == 0, $"{actor.Name} attacks {target.Name}.", health));
                }
                _actors[actor.Id] = actor; actors[actor.Id] = actor;
            }
            foreach (var id in _northParkOriginals.Keys) if (_actors.TryGetValue(id, out var transformed)) actors[id] = transformed;
            if (now >= _nextIncursionSpeech)
            {
                _nextIncursionSpeech = now.AddSeconds(3);
                foreach (var actor in _actors.Values.Where(a => (IsIncursionActor(a) || _northParkOriginals.ContainsKey(a.Id)) && participants.Any(p => p.Position.Distance2D(a.Position) < 45)).OrderBy(_ => Random.Shared.Next()).Take(6))
                {
                    var line = actor.Subtype switch { "kenHydra" => "Mmph mmph! Mmmph-mmph!", "northParkCitizen" => NorthParkInsults[Random.Shared.Next(NorthParkInsults.Length)],
                        "alaneeCelebrity" => AlaneeCast.First(c => c.Name == actor.Name).Quote, "pierceHawkeye" => now.ToUnixTimeSeconds() % 2 == 0 ? "War isn’t Hell." : "War is war, and Hell is Hell.", _ => null };
                    if (line is not null) SayIncursion(actor.Id, actor.Name, line);
                }
            }
        }
        finally { _incursionTick.Release(); }
    }
}
