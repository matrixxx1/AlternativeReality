using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<string, string> _testCharacterOwners = new();
    private readonly object _testCharacterLock = new();

    public TestCharacterPlacement PlaceTestCharacter(string playerId, PlaceTestCharacterRequest request)
    {
        if (!_players.TryGetValue(playerId, out var owner) || !owner.GodMode)
            throw new InvalidOperationException("God Mode must be enabled to place test characters.");
        if (owner.LocationId != "outdoor") throw new InvalidOperationException("Place test characters outdoors.");
        if (request.Kind is not ("npc" or "player" or "animal")) throw new InvalidOperationException("Choose NPC, fake player, or animal.");
        if (!double.IsFinite(request.X) || !double.IsFinite(request.Y) || !IsAreaLoaded(request.X, request.Y))
            throw new InvalidOperationException("Choose a point inside the loaded map.");
        var position = owner.Position with { X = request.X, Y = request.Y };
        if (Navigation.IsBlocked(position.X, position.Y) || Navigation.TerrainAt(position.X, position.Y) == TerrainType.DeepWater)
            throw new InvalidOperationException("Choose open, dry ground for the test character.");
        position = position with { Z = Navigation.ElevationAt(position.X, position.Y) };
        lock (_testCharacterLock)
        {
            if (_testCharacterOwners.Count(pair => pair.Value == playerId) >= 30)
                throw new InvalidOperationException("Clear your test characters before placing more (30 maximum).");
            var id = $"test-character:{Guid.NewGuid():N}";
            _testCharacterOwners[id] = playerId;
            if (request.Kind == "player")
            {
                var player = new PlayerState(id, "Test Player", position, Terrain: Navigation.TerrainAt(position.X, position.Y), IsTestCharacter: true);
                _players[id] = player;
                return new(player, null, "Placed a fake player. " + (Configuration.PvpEnabled ? "Player combat is enabled." : "PvP is disabled, so player attacks will not affect it."));
            }
            var animal = request.Kind == "animal";
            var actor = new ActorState(id, animal ? EntityKind.Animal : EntityKind.Npc, animal ? "deer" : "human",
                animal ? "Test Deer" : "Test NPC", position, HealthHearts: 10, MaximumHealthHearts: 10, IsTestCharacter: true);
            _actors[id] = actor;
            return new(null, actor, $"Placed {actor.Name}. Test characters stay where placed until affected by combat.");
        }
    }

    public IReadOnlyList<string> ClearTestCharacters(string playerId)
    {
        if (!playerIsGod(playerId)) throw new InvalidOperationException("God Mode must be enabled to clear test characters.");
        lock (_testCharacterLock)
        {
            var ids = _testCharacterOwners.Where(pair => pair.Value == playerId).Select(pair => pair.Key).ToArray();
            foreach (var id in ids)
            {
                _testCharacterOwners.TryRemove(id, out _);
                _players.TryRemove(id, out _);
                _actors.TryRemove(id, out _);
                _actorRoutes.TryRemove(id, out _);
                _burningTargets.TryRemove(id, out _);
                _probulatorAbductions.TryRemove(id, out _);
                _lastMovement.TryRemove(id, out _);
                foreach (var key in _relationships.Keys.Where(key => key.Player == id || key.Actor == id).ToArray()) _relationships.TryRemove(key, out _);
            }
            return ids;
        }
    }
}

public sealed record TestCharacterPlacement(PlayerState? Player, ActorState? Actor, string Message);
