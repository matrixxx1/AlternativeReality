using System.Collections.Concurrent;
using System.Security.Cryptography;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed record TauntResult(ChatMessage Chat, IReadOnlyList<RelationshipState> Relationships);

public sealed partial class RealityWorld
{
    public const double TauntHostility = .1;
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, int Line)> _lastTaunts = new();
    private readonly SemaphoreSlim _tauntLock = new(1, 1);
    private static readonly string[] TauntLines =
    [
        "Your mama called. She wants a refund.",
        "Your mama's so slow, she got overtaken by a loading screen.",
        "Your mama's so loud, the zombies filed a noise complaint.",
        "Go fuck yourself.", "Fuck off.", "You're about as useful as a screen door on a submarine.",
        "I've met rocks with better conversation skills.", "Is that your face, or did your helmet give up?",
        "Even the zombies have better manners than you.", "You fight like a shopping cart with a broken wheel.",
        "I'd challenge you to a battle of wits, but you came unarmed.", "Get lost, jackass."
    ];

    public async Task<TauntResult> TauntAsync(string playerId, string targetId, CancellationToken cancellationToken = default)
    {
        await _tauntLock.WaitAsync(cancellationToken);
        try
        {
            if (!_players.TryGetValue(playerId, out var player) || player.HealthHearts <= 0) throw new InvalidOperationException("Unknown player.");
            if (IsGasAsleep(playerId) || IsProbulatorAbducted(playerId)) throw new InvalidOperationException("You cannot taunt while asleep or abducted.");
            if (targetId == playerId) throw new InvalidOperationException("Choose someone else to taunt.");
            _players.TryGetValue(targetId, out var targetPlayer);
            var actor = ActorsAtLocation(player.LocationId).FirstOrDefault(a => a.Id == targetId && a.Kind == EntityKind.Npc);
            var position = targetPlayer?.Position ?? actor?.Position;
            var location = targetPlayer?.LocationId ?? actor?.LocationId;
            var health = targetPlayer?.HealthHearts ?? actor?.HealthHearts ?? 0;
            if (position is null || location != player.LocationId || health <= 0) throw new InvalidOperationException("That character is not here.");
            if (player.Position.Distance2D(position.Value) > 10) throw new InvalidOperationException("Move within 10 meters to taunt that character.");
            if (player.LocationId == "outdoor" ? !Navigation.CanTraverse(player.Position, position.Value) :
                _dungeons.TryGetValue(player.LocationId, out var dungeon) && dungeon.Walls.Any(w => CrossesDungeonWall(player.Position, position.Value, w)))
                throw new InvalidOperationException("A solid obstacle blocks your taunt.");
            var now = _probulatorClock.GetUtcNow();
            var hadPrior = _lastTaunts.TryGetValue(playerId, out var prior);
            if (hadPrior && now - prior.At < TimeSpan.FromSeconds(3)) throw new InvalidOperationException("Wait a moment before taunting again.");
            var line = RandomNumberGenerator.GetInt32(TauntLines.Length - (hadPrior ? 1 : 0));
            if (hadPrior && line >= prior.Line) line++;
            _lastTaunts[playerId] = (now, line);
            var relationships = new List<RelationshipState>();
            async Task Offend(string owner, string target)
            {
                if (!_players.TryGetValue(owner, out var ownerPlayer)) return;
                var raw = _relationships.GetValueOrDefault((owner, target)) - TauntHostility;
                _relationships[(owner, target)] = raw;
                if (!ownerPlayer.IsTestCharacter) await _store.SaveRelationshipAsync(Configuration.Id, new(owner, target, raw), cancellationToken);
                relationships.Add(new(owner, target, Relationship(owner, target)));
            }
            await Offend(playerId, targetId);
            // A player's own relationship view must also remember who antagonized them.
            if (targetPlayer is not null) await Offend(targetId, playerId);
            await RecordTauntQuestAsync(playerId, targetId, cancellationToken);
            var targetName = targetPlayer?.Name ?? actor!.Name;
            var chat = new ChatMessage($"taunt:{Guid.NewGuid():N}", playerId, player.Name, $"{targetName}, {TauntLines[line]}", now);
            return new(chat, relationships);
        }
        finally { _tauntLock.Release(); }
    }
}
