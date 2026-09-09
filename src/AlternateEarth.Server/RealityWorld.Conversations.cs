using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<(string Player, string Actor), DateTimeOffset> _conversations = new();
    private void BeginConversation(string playerId, string actorId) => _conversations[(playerId, actorId)] = _probulatorClock.GetUtcNow().AddSeconds(20);

    public void UpdateConversation(string playerId, string actorId, bool active)
    {
        var key = (playerId, actorId);
        if (!active) { _conversations.TryRemove(key, out _); return; }
        // Heartbeats can extend an established conversation, never start one remotely.
        if (!_conversations.ContainsKey(key)) return;
        var actor = FindActor(playerId, actorId);
        if (!_players.TryGetValue(playerId, out var player) || actor is null || actor.HealthHearts <= 0 ||
            actor.LocationId != player.LocationId || actor.Position.Distance2D(player.Position) > 6)
            _conversations.TryRemove(key, out _);
        else BeginConversation(playerId, actorId);
    }

    private HashSet<string> ActiveConversationActors(DateTimeOffset now)
    {
        var actors = new HashSet<string>();
        foreach (var (key, expires) in _conversations.ToArray())
        {
            var actor = FindActor(key.Player, key.Actor);
            if (expires <= now || !_players.TryGetValue(key.Player, out var player) || actor is null || actor.HealthHearts <= 0 ||
                player.LocationId != actor.LocationId || player.Position.Distance2D(actor.Position) > 6)
                _conversations.TryRemove(key, out _);
            else actors.Add(key.Actor);
        }
        return actors;
    }
}
