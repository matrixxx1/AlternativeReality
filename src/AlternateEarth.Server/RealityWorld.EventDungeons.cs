using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static readonly string[] EventCards = ["Strike", "Guard", "Second Wind", "Heavy Blow", "Riposte", "Poison", "Focus", "Wild Card"];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string[]> _battleDecks = new();
    public async Task<DungeonState> EnterEventDungeonAsync(string playerId, CancellationToken token = default)
    {
        await _inversionTick.WaitAsync(token);
        try
        {
            if (!_players.TryGetValue(playerId, out var player) || _activeInversion is not { } e) throw new InvalidOperationException("No dungeon event is active.");
            var definition = InversionCatalog.All.First(x => x.Id == e.Type);
            if (definition.Mode == "outdoor") throw new InvalidOperationException("This inversion is fought outdoors.");
            var continuing = _dungeons.TryGetValue(player.LocationId, out var previous) && previous.IsCompleted && previous.EventBattle?.EventId == e.Id;
            if (!InsideInversion(player) && !continuing) throw new InvalidOperationException("Enter through the event area or finish your current event dungeon first.");
            var number = _eventDungeonWins.GetValueOrDefault(playerId) + 1;
            if (number > 2) throw new InvalidOperationException("You already completed both event dungeons.");
            if (player.LocationId == "outdoor") _returnPositions[player.Id] = player.Position;
            var id = e.Id + ":dungeon:" + playerId + ":" + number;
            var exit = e.Center with { X = 6, Y = 2, Z = 0 };
            var boss = number == 1 ? "Assistant " + definition.Boss : definition.Boss;
            var health = number == 1 ? 24 : 60;
            var dungeon = new DungeonState(id, e.Id, 32, 12, [new(0, 0, 32, 12)], [], exit, [], [], [], SessionId: id,
                Difficulty: number * 10, RetroBattle: definition.Mode == "retro" ? CreateRetroBattle(number * 10, _probulatorClock.GetUtcNow()) : null,
                EventBattle: new(e.Id, definition.Mode, number, boss, health, health, 0, 0, definition.Mode == "cards" ? _battleDecks.GetValueOrDefault(playerId, EventCards).Take(3).ToArray() : ["Strike", "Guard", "Second Wind"], "Choose your move. The enemy intends to attack."));
            if (dungeon.RetroBattle is { } run)
                dungeon = dungeon with { RetroBattle = run with { Enemies = run.Enemies.Select((enemy, i) => i == run.Enemies.Count - 1 ? enemy with { Hearts = number == 1 ? 2 : 5, IsBoss = true } : enemy).ToArray() } };
            _dungeons[id] = dungeon;
            await SavePlayerAsync(player with { LocationId = id, Position = exit, TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Version = player.Version + 1 }, token);
            return WithDiscovery(playerId, dungeon);
        }
        finally { _inversionTick.Release(); }
    }
    public async Task<DungeonState> PlayEventBattleAsync(string playerId, string action, CancellationToken token = default)
    {
        await _inversionTick.WaitAsync(token);
        try
        {
            if (!_players.TryGetValue(playerId, out var player) || !_dungeons.TryGetValue(player.LocationId, out var dungeon) || dungeon.EventBattle is not { } battle || battle.Mode == "retro") throw new InvalidOperationException("You are not in a turn-based battle.");
            if (_activeInversion?.Id != battle.EventId || _activeInversion.EndsAtUtc <= _probulatorClock.GetUtcNow() || dungeon.IsCompleted) throw new InvalidOperationException("This battle has ended.");
            if (battle.Mode == "cards" && !battle.Hand.Contains(action)) throw new InvalidOperationException("That card is not in your hand.");
            if (!EventCards.Contains(action)) throw new InvalidOperationException("Choose an available battle action.");
            var damage = action switch { "Strike" => 6, "Heavy Blow" => 10, "Poison" => 2, "Wild Card" => Random.Shared.Next(2, 13), "Riposte" => 4, _ => 0 };
            if (battle.Mode == "turns" && action is not ("Strike" or "Guard" or "Second Wind")) throw new InvalidOperationException("Choose Attack, Guard, or Second Wind.");
            var guard = action is "Guard" or "Riposte" ? 4 : 0;
            damage += action is "Strike" or "Heavy Blow" or "Riposte" or "Wild Card" ? battle.Focus : 0;
            var poison = action == "Poison" ? 3 : battle.Poison;
            var enemyHealth = Math.Max(0, battle.EnemyHealth - damage * ProgressionRules.Damage(StatsFor(playerId)) * PlayerDamageMultiplier(playerId) - (poison > 0 ? 2 : 0));
            var won = enemyHealth <= 0;
            var heal = action == "Second Wind" ? 4 : 0;
            var enemyDamage = won ? 0 : Math.Max(0, (battle.Turn % 3 == 2 ? 5 : 2) - guard);
            var health = Math.Min(player.MaximumHealthHearts, player.HealthHearts + heal);
            health = Math.Max(PlayerCanDie(player.Id) ? 0 : 1, health - enemyDamage);
            var deck = _battleDecks.GetValueOrDefault(playerId, EventCards);
            var hand = battle.Mode == "cards" ? battle.Hand.Where((c, i) => i != Array.IndexOf(battle.Hand.ToArray(), action)).Append(deck[(battle.Turn + 3) % deck.Length]).ToArray() : battle.Hand;
            var next = battle with { Poison = Math.Max(0, poison - 1), Focus = action == "Focus" ? 8 : damage > 0 ? 0 : battle.Focus, EnemyHealth = enemyHealth, Turn = battle.Turn + 1, Guard = guard, Hand = hand, Won = won,
                Message = won ? "Boss defeated!" : $"{action}: {damage} damage, {heal} healing. Enemy dealt {enemyDamage}. Next attack: {(battle.Turn % 3 == 1 ? "heavy (5)" : "normal (2)")}." };
            dungeon = dungeon with { EventBattle = next, IsCompleted = won }; _dungeons[dungeon.Id] = dungeon;
            var updated = player with { HealthHearts = health, Version = player.Version + 1 };
            if (health <= 0 && PlayerCanDie(player.Id)) updated = await DieAndResetPlayerAsync(updated, token);
            await SavePlayerAsync(updated, token);
            if (won) await CompleteEventDungeonAsync(updated, dungeon, token);
            return dungeon;
        }
        finally { _inversionTick.Release(); }
    }
    public void ConfigureEventDeck(string playerId, string[] cards)
    {
        if (cards.Length != 6 || cards.Any(c => !EventCards.Contains(c)) || cards.GroupBy(c => c).Any(g => g.Count() > 2)) throw new InvalidOperationException("Choose six cards, at most two of each type.");
        if (_players.TryGetValue(playerId, out var p) && _dungeons.TryGetValue(p.LocationId, out var d) && d.EventBattle is { Turn: > 0 }) throw new InvalidOperationException("Finish the battle before changing your deck.");
        _battleDecks[playerId] = cards;
        if (_players.TryGetValue(playerId, out var player) && _dungeons.TryGetValue(player.LocationId, out var dungeon) && dungeon.EventBattle is { Mode: "cards", Turn: 0 } battle)
            _dungeons[dungeon.Id] = dungeon with { EventBattle = battle with { Hand = cards.Take(3).ToArray(), Message = "Deck saved. Choose your first card." } };
    }
    private async Task CompleteEventDungeonAsync(PlayerState player, DungeonState dungeon, CancellationToken token)
    {
        if (_activeInversion is not { } e || dungeon.EventBattle?.EventId != e.Id || !dungeon.IsCompleted) return;
        var number = dungeon.EventBattle.DungeonNumber;
        if (_eventDungeonWins.GetValueOrDefault(player.Id) >= number) return;
        _eventDungeonWins[player.Id] = number;
        if (number == 2) await AwardInversionAsync(player, e, token);
        else _progressionNotices.Enqueue(new(player.Id, "Small boss defeated. Complete the second dungeon and its big boss to win your event reward."));
    }
}
