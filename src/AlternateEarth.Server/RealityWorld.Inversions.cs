using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly object _voteLock = new();
    private readonly SemaphoreSlim _inversionTick = new(1, 1);
    private ServerVote? _serverVote;
    private long _lastVoteHour = -1;
    private readonly Queue<string> _winningInversions = new();
    private InversionState? _activeInversion;
    private readonly ConcurrentQueue<(string Player, ActorState Actor)> _inversionKills = new();
    private readonly HashSet<string> _countedInversionKills = new();
    private readonly ConcurrentDictionary<string,int> _eventDungeonWins = new();
    private readonly ConcurrentDictionary<string,byte> _eventAwards = new();
    private readonly ConcurrentDictionary<string, ActorState> _transformedActors = new();
    private readonly ConcurrentDictionary<string,string> _transformedWeapons = new();
    private readonly Dictionary<string,DateTimeOffset> _inversionAttacks = new();
    private readonly Dictionary<string,(string Owner,string Weapon)> _stolenWeapons = new();
    private readonly ConcurrentQueue<string> _inversionRemovals = new();
    private readonly ConcurrentQueue<string> _inversionDungeonExits = new();
    public IReadOnlyList<string> TakeInversionDungeonExits() { var ids = new List<string>(); while (_inversionDungeonExits.TryDequeue(out var id)) ids.Add(id); return ids; }
    private readonly ConcurrentQueue<ActorState> _inversionActorUpdates = new();
    public IReadOnlyList<string> TakeInversionRemovals() { var items = new List<string>(); while (_inversionRemovals.TryDequeue(out var id)) items.Add(id); return items; }
    public IReadOnlyList<ActorState> TakeInversionActorUpdates() { var items = new List<ActorState>(); while (_inversionActorUpdates.TryDequeue(out var actor)) items.Add(actor); return items; }
    private bool EventPaused(PlayerState p) => InsideInversion(p) && _activeInversion?.Type is "hold" or "office" && _probulatorClock.GetUtcNow().ToUnixTimeSeconds() % 15 >= 11;
    private DateTimeOffset _nextInversionPulse, _nextInversionSpeech;
    private readonly ConcurrentQueue<CombatEvent> _inversionCombat = new();
    private readonly ConcurrentQueue<ChatMessage> _inversionChat = new();
    public IReadOnlyList<CombatEvent> TakeInversionCombat() { var result = new List<CombatEvent>(); while (_inversionCombat.TryDequeue(out var item)) result.Add(item); return result; }
    public IReadOnlyList<ChatMessage> TakeInversionChat() { var result = new List<ChatMessage>(); while (_inversionChat.TryDequeue(out var item)) result.Add(item); return result; }
    private bool InsideInversion(PlayerState p) => p.LocationId == "outdoor" && _activeInversion is { } e && p.Position.Distance2D(e.Center) <= e.Radius;
    private bool ManagedEventActor(ActorState a) => a.Id.StartsWith("inversion:", StringComparison.Ordinal);
    public IReadOnlyList<PlayerState> InversionPlayers() => _players.Values.ToArray();
    public IReadOnlyList<ActorState> InversionActors() => _actors.Values.Where(a => ManagedEventActor(a) || _transformedActors.ContainsKey(a.Id) || a.Subtype.StartsWith("adventure:")).ToArray();
    public InversionView GetInversionView(string playerId)
    {
        lock (_voteLock) return new(_serverVote?.Snapshot(_players.Values.ToDictionary(p => p.Id, p => p.Name)), _activeInversion,
            _winningInversions.Select(id => InversionCatalog.All.First(e => e.Id == id).Name).ToArray(), _eventDungeonWins.GetValueOrDefault(playerId));
    }
    public void StartServerVote(string playerId)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.IsTestCharacter) throw new InvalidOperationException("Join the server before starting a vote.");
        lock (_voteLock) { if (_serverVote is not null) throw new InvalidOperationException("A Server Vote is already running."); BeginServerVote(_probulatorClock.GetUtcNow()); }
    }
    public void CancelServerVote(string playerId)
    {
        lock (_voteLock)
        {
            if (!_players.TryGetValue(playerId, out var player) || player.IsTestCharacter)
                throw new InvalidOperationException("Join the server before canceling a Server Vote.");
            if (_serverVote is null) throw new InvalidOperationException("No Server Vote is open.");
            _serverVote = null;
            _lastVoteHour = _probulatorClock.GetUtcNow().ToUnixTimeSeconds() / 3600;
            EventSay("server", "Server Vote", $"{player.Name} canceled the Server Vote from World Testing.");
        }
    }
    private void BeginServerVote(DateTimeOffset now)
    {
        _serverVote = new(now, InversionCatalog.All.OrderBy(_ => Random.Shared.Next()).Take(3).Select(e => e.Id));
        _serverVote.SyncPlayers(_players.Values.Where(p => !p.IsTestCharacter).Select(p => p.Id));
        EventSay("server", "Server Vote", "Server Vote — one minute to choose your Reality inversion.");
    }
    public void CastServerVote(string playerId, string option)
    {
        lock (_voteLock)
        {
            if (!_players.TryGetValue(playerId, out var p) || p.IsTestCharacter) throw new InvalidOperationException("Join the server to vote.");
            if (_serverVote is null) throw new InvalidOperationException("No Server Vote is open.");
            _serverVote.Cast(playerId, option, _probulatorClock.GetUtcNow());
        }
    }
    private void EventSay(string id, string name, string text) => _inversionChat.Enqueue(new(Guid.NewGuid().ToString("N"), id, name, text, _probulatorClock.GetUtcNow()));
    public async Task AdvanceInversionsAsync(TimeSpan elapsed, CancellationToken token = default)
    {
        if (!await _inversionTick.WaitAsync(0, token)) return;
        try
        {
            var now = _probulatorClock.GetUtcNow();
            await AdvanceAdventuresAsync(elapsed.TotalSeconds, token);
            await AdvanceNpcDrivingAsync(elapsed.TotalSeconds, token);
            await AdvanceHaneyAsync(elapsed.TotalSeconds, token);
            await AdvanceBleedingAsync(now, token);
            lock (_voteLock)
            {
                var hour = now.ToUnixTimeSeconds() / 3600;
                if (_lastVoteHour < 0) _lastVoteHour = hour;
                if (hour != _lastVoteHour) { _lastVoteHour = hour; if (_serverVote is null) BeginServerVote(now); }
                if (_serverVote is { } vote)
                {
                    vote.SyncPlayers(_players.Values.Where(p => !p.IsTestCharacter).Select(p => p.Id));
                    var round = vote.Round; var winner = vote.Finish(now);
                    if (winner is not null)
                    {
                        if (winner == "random") winner = InversionCatalog.All[Random.Shared.Next(InversionCatalog.All.Count)].Id;
                        _winningInversions.Enqueue(winner); _serverVote = null;
                        EventSay("server", "Server Vote", $"Winner: {InversionCatalog.All.First(e => e.Id == winner).Name}." + (_activeInversion is null ? "" : " Queued until the current inversion ends."));
                    }
                    else if (vote.Round != round) EventSay("server", "Server Vote", "It is a tie! A fresh one-minute runoff is open. Cast your vote again.");
                }
            }
            if (_activeInversion is null)
            {
                var anchors = _players.Values.Where(p => !p.IsTestCharacter).ToArray();
                string? next = null;
                lock (_voteLock) if (anchors.Length > 0 && _winningInversions.Count > 0) next = _winningInversions.Dequeue();
                if (next is not null) StartInversion(next, anchors[Random.Shared.Next(anchors.Length)], now);
            }
            if (_activeInversion is not { } active) return;
            if (now >= active.EndsAtUtc) { await FinishInversionAsync(false, token); return; }
            while (_inversionKills.TryDequeue(out var kill))
            {
                if (!_countedInversionKills.Add(kill.Actor.Id) || kill.Actor.EventName != active.Name) continue;
                if (active.Type == "northern" && IsCanadian(kill.Actor))
                {
                    if (kill.Actor.Subtype == "canadianBoss")
                    {
                        if (_countedInversionKills.Contains(active.BossId) && _countedInversionKills.Contains(active.BossId + ":phil")) { await FinishInversionAsync(true, token); return; }
                        active = active with { Message = "One robot-costumed Canadian remains. Defeat both to win!" };
                    }
                    else active = active with { Kills = active.Kills + 1, Message = $"Canadians defeated: {active.Kills + 1}/50. Then defeat Mecha Terry AND Mecha Phil." };
                    continue;
                }
                if (active.Type == "northern" && IsNorthernWildlife(kill.Actor)) continue;
                if (kill.Actor.Id == active.BossId) { await FinishInversionAsync(true, token); return; }
                active = active with { Kills = active.Kills + 1 };
                if (_stolenWeapons.Remove(kill.Actor.Id, out var weapon))
                { AddInventory(weapon.Owner, weapon.Weapon, 1); await SaveInventoryAsync(weapon.Owner, token); }
                if (active.Type == "barrel") await EventBlastAsync(kill.Actor.Position, 6, 4, kill.Actor.Id, token);
            }
            _activeInversion = active;
            if (active.Type == "smug" && active.Kills >= 50 && !_actors.ContainsKey(active.BossId)) SpawnInversionActor(active, active.BossId, "smugBoss", "Bald Alex Wins", 500, active.Center);
            if (active.Type == "northern" && active.Kills >= 50 && !_northernBossesSpawned) SpawnNorthernBosses(active);
            await ApplyInversionTransformationsAsync(token);
            await StepInversionCombatAsync(elapsed.TotalSeconds, now, token);
            if (now >= _nextInversionPulse) { _nextInversionPulse = now.AddSeconds(1); await StepInversionEnvironmentAsync(now, token); }
            if (active.Type != "northern" && now >= _nextInversionSpeech)
            {
                _nextInversionSpeech = now.AddSeconds(3);
                foreach (var actor in _actors.Values.Where(ManagedEventActor).OrderBy(_ => Random.Shared.Next()).Take(active.Type == "smug" ? 12 : 2))
                    EventSay(actor.Id, actor.Name, InversionSpeech(active.Type, actor.Id == active.BossId));
            }
        }
        finally { _inversionTick.Release(); }
    }
    internal void StartInversion(string type, PlayerState anchor, DateTimeOffset now)
    {
        var definition = InversionCatalog.All.First(e => e.Id == type);
        var center = anchor.LocationId == "outdoor" ? anchor.Position : _returnPositions.GetValueOrDefault(anchor.Id, anchor.Position);
        var id = $"inversion:{Guid.NewGuid():N}";
        _northernBossesSpawned = false; _canadianFarts.Clear();
        _eventDungeonWins.Clear(); _eventAwards.Clear(); _countedInversionKills.Clear(); _inversionAttacks.Clear();
        _activeInversion = new(id, type, definition.Name, center, 100, now.AddMinutes(definition.DurationMinutes), id + ":boss", 0,
            definition.Mode == "outdoor" ? "Defeat the boss before time expires." : "Complete two dungeons: a small boss, then a big boss. Each player earns their own victory.", [], []);
        _nextInversionPulse = now; _nextInversionSpeech = now; _floodWaveHits.Clear();
        if (definition.Mode == "retro") _retroEndsAt = _activeInversion.EndsAtUtc;
        if (definition.Mode == "outdoor")
        {
            if (type is not ("smug" or "northern")) SpawnInversionActor(_activeInversion, _activeInversion.BossId, type switch { "ufo"=>"ufo", "trex"=>"tRex", "brontosaurus"=>"brontosaurus", "stegosaurus"=>"stegosaurus", "raptors"=>"raptor", "giants"=>"giant", "bear"=>"eventBear", _=>type+"Boss" }, definition.Boss, 500, center with { X = center.X + 16 });
            var count = type is "smug" or "northern" ? 60 : type is "mech" or "flood" ? 0 : 12;
            for (var n = 0; n < count; n++)
            {
                var angle = n * 2.39996; var radius = 12 + Math.Sqrt(n + 1) * 7;
                var subtype = type switch { "northern" => "canadian", "plants" => new[] { "sunflower", "rose", "tulip", "daisy" }[n % 4], "geese" => "goose", "normal" => new[] { "chair", "trashCan", "mailbox", "car" }[n % 4], "bosses" => new[] { "tinyRex", "tinyGiant", "tinyBear" }[n % 3], "barrel" => "supportBarrel", _ => type + "Minion" };
                SpawnInversionActor(_activeInversion, id + ":minion:" + n, subtype, subtype == "supportBarrel" ? "Emotional Support Barrel" : type == "office" ? "Assistant to the Regional Manager" : type == "smug" ? "Concerned Superior Citizen" : type == "northern" ? "Canadian invader" : subtype, type == "smug" ? 6 : 12, center with { X = center.X + Math.Cos(angle) * radius, Y = center.Y + Math.Sin(angle) * radius });
                if (type == "northern") { var actorId = id + ":minion:" + n; _actors[actorId] = _actors[actorId] with { EquippedWeapon = n % 10 < 7 ? "fist" : n % 10 < 9 ? "hockeyStick" : "iceSkate" }; }
            }
        }
        if (type == "northern") { SpawnNorthernWildlife(_activeInversion); _activeInversion = _activeInversion with { Message = "Defeat 50 Canadians together, then BOTH Mecha Terry and Mecha Phil. Watch for charging moose, helmeted beavers, and tactical goose squads!" }; }
        EventSay("server", definition.Name, type switch { "fruit" => "The beans have breached containment. Keep moving.", "hoa" => "Your portal is not an approved color.", "barrel" => "It loves you. Please maintain a safe distance.", _ => definition.Name + " is arriving through a portal!" });
    }
    private void SpawnInversionActor(InversionState e, string id, string subtype, string name, double health, WorldPosition point)
    {
        var safe = Navigation.FindNearestWalkable(point);
        if (safe.Distance2D(e.Center) > e.Radius) safe = e.Center;
        _actors[id] = new(id, EntityKind.Npc, subtype, name, safe, HealthHearts: health, MaximumHealthHearts: health,
            FriendRating: -10, EventStartedAtUtc: _probulatorClock.GetUtcNow(), EventEndsAtUtc: e.EndsAtUtc, EventName: e.Name);
    }
    private async Task FinishInversionAsync(bool won, CancellationToken token)
    {
        if (_activeInversion is not { } e) return;
        if (won) foreach (var player in _players.Values.Where(InsideInversion).ToArray()) await AwardInversionAsync(player, e, token);
        EventSay("server", e.Name, won ? "Boss defeated! Your private treasure and bonus XP are ready." : "Time expired. No completion reward; everything already earned is yours to keep.");
        foreach (var actor in _actors.Values.Where(ManagedEventActor).ToArray()) { _actors.TryRemove(actor.Id, out _); _inversionRemovals.Enqueue(actor.Id); }
        foreach (var stolen in _stolenWeapons.Values) { AddInventory(stolen.Owner, stolen.Weapon, 1); await SaveInventoryAsync(stolen.Owner, token); }
        _stolenWeapons.Clear();
        _activeInversion = null; _retroEndsAt = null; _northernWildlifeRoutes.Clear();
        foreach (var player in _players.Values.Where(p => _dungeons.TryGetValue(p.LocationId, out var d) && d.EventBattle?.EventId == e.Id).ToArray())
        {
            await ExitDungeonAsync(player.Id, token);
            _inversionDungeonExits.Enqueue(player.Id);
        }
        await ApplyInversionTransformationsAsync(token);
        foreach (var pair in _transformedActors) if (_actors.TryGetValue(pair.Key, out var survivor)) _actors[pair.Key] = pair.Value with { HealthHearts = survivor.HealthHearts, Position = survivor.Position, Version = survivor.Version + 1 };
        foreach (var id in _transformedActors.Keys) if (_actors.TryGetValue(id, out var restored)) _inversionActorUpdates.Enqueue(restored);
        _transformedActors.Clear();
    }
    private async Task AwardInversionAsync(PlayerState player, InversionState e, CancellationToken token)
    {
        if (player.IsTestCharacter || !_eventAwards.TryAdd(player.Id, 0)) return;
        var loot = new LootDropState(e.Id + ":reward:" + player.Id, player.Position, player.LocationId, e.Type == "northern" ? 0 : 15000,
            e.Type == "northern" ? NorthernTreasure() : [InventoryStack("bullet", 60), InventoryStack("arrow", 30), InventoryStack("film", 15), InventoryStack("grenade", 5), InventoryStack("rocket", 4), InventoryStack("molotovCocktail", 5), InventoryStack("swimmies", 1), InventoryStack("sword", 1, quality: "Fine")],
            DateTimeOffset.MaxValue, "eventReward", player.Name, player.Id);
        await _store.SavePersistentLootAsync(Configuration.Id, loot, token); _loot[loot.Id] = loot;
        await UnlockAchievementAsync(player.Id, e.Type == "flood" ? "Ark Nemesis" : e.Type == "smug" ? "Smug Survivor" : "Reality Check", token);
        await AwardExperienceAsync(player.Id, 500, e.Name + " complete", e.Id + ":complete", randomize: false, cancellationToken: token);
    }
    private async Task ApplyInversionTransformationsAsync(CancellationToken token)
    {
        foreach (var player in _players.Values.ToArray())
        {
            var zombie = _activeInversion?.Type == "plants" && InsideInversion(player);
            if (zombie && !_transformedWeapons.ContainsKey(player.Id))
            { _activeProbulatorBeams.TryRemove(player.Id,out _); _transformedWeapons[player.Id] = player.EquippedWeapon; await SavePlayerAsync(player with { EquippedWeapon = "zombieBite", Version = player.Version + 1 }, token); }
            else if (!zombie && _transformedWeapons.TryRemove(player.Id, out var weapon))
                await SavePlayerAsync(player with { EquippedWeapon = BestUsableWeapon(player.Id, weapon, player.GodMode), Version = player.Version + 1 }, token);
        }
        if (_activeInversion is not { Type: "plants" } e) return;
        foreach (var actor in _actors.Values.Where(a => !ManagedEventActor(a) && a.Subtype != "zombie" && a.LocationId == "outdoor" && a.Position.Distance2D(e.Center) <= e.Radius).ToArray())
        {
            if (_transformedActors.ContainsKey(actor.Id)) continue;
            _transformedActors[actor.Id] = actor;
            _actors[actor.Id] = actor with { Subtype = new[] { "sunflower", "rose", "tulip", "daisy" }[Random.Shared.Next(4)], IsMerchant = false, IsQuestGiver = false, OffersFoodDelivery = false, EventName = e.Name, EquippedWeapon = "plantSeed", FriendRating = -10, Version = actor.Version + 1 };
            foreach (var quest in _quests.Values.Where(q => q.GiverId == actor.Id && q.Status is "active" or "ready").ToArray())
            { var failed = quest with { Status = "failed", FailedAtUtc = _probulatorClock.GetUtcNow() }; _quests[(quest.PlayerId, quest.Id)] = failed; await _store.SaveQuestAsync(Configuration.Id, failed, token); }
        }
    }
    private string InversionSpeech(string type, bool boss) => type == "smug" && boss ? new[] {
        "Next question. (Alec Baldwin quote)", "I only have glib words for you today about this. (Alec Baldwin quote)",
        "My superiority is locally sourced and free range.", "I have personally offset the carbon emissions of this stomp.",
        "The cloud is not smug. You are simply beneath it." }[Random.Shared.Next(5)] : type switch
    {
        "smug" => new[] { "I own a Prius. I'm better than you.", "My carbon footprint wears locally sourced sandals.", "I compost my opinions. They're that organic.", "This cloud is artisanally sourced.", "I was morally superior before it was mainstream.", "I only inhale ethically sourced oxygen.", "Your apology isn't carbon neutral.", "I brought my own reusable superiority." }[Random.Shared.Next(8)],
        "fruit" => new[] { "pfft", "prrrt", "BRAAAP", "Weapons-grade flatulence.", "Trusted a fart. Paid the price." }[Random.Shared.Next(5)],
        "office" => boss ? "Let's circle back to your imminent defeat." : "This could have been an email!",
        "geese" => boss ? "HONK. WICK." : "Honk if you need help!",
        "normal" => "This is a very normal Tuesday. Please stop asking.",
        "hoa" => "Your existence violates subsection 4B.", "budget" => "Your polygons exceeded the budget.",
        "hold" => "Your battle is very important to us. Please hold.", "flood" => "Two of everything. Zero refunds.",
        "bosses" => "BEHOLD MY UNREASONABLY LARGE INTRODUCTION!", _ => "You have entered the wrong neighborhood."
    };
}
