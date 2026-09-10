using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private sealed record DeliveryDog(string PlayerId, string QuestId, string LocationId, long Wave);
    private readonly ConcurrentDictionary<string, DeliveryDog> _deliveryDogs = new();
    private readonly ConcurrentDictionary<string, long> _deliveryAmbushWaves = new();
    private readonly ConcurrentQueue<ActorState> _deliveryActorUpdates = new();
    private readonly ConcurrentQueue<string> _deliveryActorRemovals = new();
    private readonly ConcurrentQueue<RelationshipState> _deliveryDogRelationships = new();

    private void RemoveDeliveryDog(string id)
    {
        if (!_deliveryDogs.TryRemove(id, out var dog)) return;
        _actors.TryRemove(id, out _);
        if (_dungeons.TryGetValue(dog.LocationId, out var interior))
            _dungeons[interior.Id] = interior with { Actors = interior.Actors.Where(a => a.Id != id).ToArray() };
        _actorRoutes.TryRemove(id, out _);
        foreach (var key in _relationships.Keys.Where(k => k.Actor == id).ToArray()) _relationships.TryRemove(key, out _);
        foreach (var key in _lastActorAttack.Keys.Where(k => k.Actor == id).ToArray()) _lastActorAttack.TryRemove(key, out _);
        _deliveryActorRemovals.Enqueue(id);
    }

    private void ClearDeliveryDogs(string playerId)
    {
        foreach (var pair in _deliveryDogs.Where(p => p.Value.PlayerId == playerId).ToArray()) RemoveDeliveryDog(pair.Key);
    }

    private void AdvanceDeliveryAmbushes()
    {
        var now = _probulatorClock.GetUtcNow();
        foreach (var pair in _deliveryDogs.ToArray())
        {
            var dog = pair.Value;
            if (!_players.TryGetValue(dog.PlayerId, out var player) || player.LocationId != dog.LocationId ||
                !_quests.TryGetValue((dog.PlayerId, dog.QuestId), out var quest) || quest.Status != "active" || quest.DeadlineUtc <= now ||
                FindActor(dog.PlayerId, pair.Key) is not { HealthHearts: > 0 } actor || actor.Position.Distance2D(player.Position) > 45)
                RemoveDeliveryDog(pair.Key);
        }
        foreach (var quest in _quests.Values.Where(q => q.Kind == "foodDelivery" && q.Status == "active" && q.DeadlineUtc > now))
        {
            if (!_players.TryGetValue(quest.PlayerId, out var player) || player.HealthHearts <= 0) continue;
            var started = quest.DeadlineUtc!.Value.AddMinutes(-quest.DeliveryMinutes!.Value);
            var wave = (long)((now - started).TotalSeconds / 10);
            if (wave < 1 || _deliveryAmbushWaves.GetValueOrDefault(quest.Id) >= wave) continue;
            // Only the current ambush is spawned after reconnecting or a delayed tick, never a backlog.
            _deliveryAmbushWaves[quest.Id] = wave;
            var position = DeliveryDogSpawn(player, wave);
            if (position is null) continue;
            var pack = _deliveryDogs.Where(p => p.Value.PlayerId == player.Id).OrderBy(p => p.Value.Wave).ToArray();
            foreach (var oldest in pack.Take(Math.Max(0, pack.Length - 2))) RemoveDeliveryDog(oldest.Key);
            var id = $"delivery-dog:{quest.Id}:{wave}";
            var actor = new ActorState(id, EntityKind.Animal, "dog", "Hungry delivery dog", position.Value,
                HealthHearts: 3, MaximumHealthHearts: 3, LocationId: player.LocationId, EquippedWeapon: "fist", TravelMode: TravelMode.Run);
            _deliveryDogs[id] = new(player.Id, quest.Id, player.LocationId, wave);
            if (player.LocationId == "outdoor") _actors[id] = actor;
            else if (_dungeons.TryGetValue(player.LocationId, out var interior))
                _dungeons[interior.Id] = interior with { Actors = interior.Actors.Append(actor).ToArray() };
            _relationships[(player.Id, id)] = -2;
            _deliveryDogRelationships.Enqueue(new(player.Id, id, -2));
            _deliveryActorUpdates.Enqueue(actor);
            _questDialogue.Enqueue(new($"delivery-bark:{id}", id, actor.Name, "Grrrr! Woof!", now));
        }
    }

    private WorldPosition? DeliveryDogSpawn(PlayerState player, long wave)
    {
        _dungeons.TryGetValue(player.LocationId, out var interior);
        foreach (var radius in new[] { 6d, 3d, 1.5d })
        for (var offset = 0; offset < 8; offset++)
        {
            var angle = (wave + offset) * Math.PI / 4;
            var candidate = player.Position with { X = player.Position.X + Math.Cos(angle) * radius, Y = player.Position.Y + Math.Sin(angle) * radius, Z = 0 };
            if (player.LocationId == "outdoor")
            {
                candidate = Navigation.FindNearestWalkable(candidate);
                if (candidate.Distance2D(player.Position) is >= 1 and <= 12 && Navigation.CanTraverse(player.Position, candidate)) return candidate;
            }
            else if (interior is not null && candidate.X > .5 && candidate.Y > .5 && candidate.X < interior.Width - .5 && candidate.Y < interior.Height - .5 &&
                (interior.Footprint is not { Count: >= 3 } || PointInsideWorldFootprint(candidate, interior.Footprint)) &&
                !interior.Walls.Any(w => CrossesDungeonWall(player.Position, candidate, w))) return candidate;
        }
        return null;
    }

    public IReadOnlyList<ActorState> AdvanceDeliveryDogs(TimeSpan elapsed)
    {
        _deliveryLock.Wait();
        try { return AdvanceDeliveryDogsCore(elapsed); }
        finally { _deliveryLock.Release(); }
    }

    private IReadOnlyList<ActorState> AdvanceDeliveryDogsCore(TimeSpan elapsed)
    {
        var updates = new List<ActorState>();
        while (_deliveryActorUpdates.TryDequeue(out var spawned))
            if (_deliveryDogs.ContainsKey(spawned.Id)) updates.Add(spawned);
        foreach (var pair in _deliveryDogs.ToArray())
        {
            if (!_players.TryGetValue(pair.Value.PlayerId, out var player) || player.LocationId != pair.Value.LocationId ||
                FindActor(player.Id, pair.Key) is not { HealthHearts: > 0 } actor || IsGasAsleep(actor.Id) || IsProbulatorAbducted(actor.Id)) continue;
            var distance = actor.Position.Distance2D(player.Position);
            if (distance <= 1.1)
            {
                if (actor.IsMoving) { var stopped = actor with { IsMoving = false, Version = actor.Version + 1 }; SetActor(player.LocationId, stopped); updates.Add(stopped); }
                continue;
            }
            var destination = player.Position;
            if (player.LocationId == "outdoor" && !Navigation.CanTraverse(actor.Position, destination))
            {
                var path = Navigation.FindPath(actor.Position, destination.X, destination.Y);
                if (!path.Success) continue;
                var waypoint = path.Waypoints.FirstOrDefault(p => p.Distance2D(actor.Position) > .1);
                if (waypoint == default) continue;
                destination = waypoint;
            }
            var remaining = actor.Position.Distance2D(destination);
            var step = Math.Min(Math.Max(0, distance - 1), Math.Min(remaining, 3.5 * elapsed.TotalSeconds*SyrupSlow(actor.Position,actor.LocationId)));
            if (remaining < .01) continue;
            var dx = (destination.X - actor.Position.X) / remaining; var dy = (destination.Y - actor.Position.Y) / remaining;
            var position = actor.Position with { X = actor.Position.X + dx * step, Y = actor.Position.Y + dy * step };
            if (_dungeons.TryGetValue(player.LocationId, out var interior) && interior.Walls.Any(w => CrossesDungeonWall(actor.Position, position, w))) continue;
            var updated = actor with { Position = position, Facing = Math.Abs(dx) > Math.Abs(dy) ? dx > 0 ? "east" : "west" : dy > 0 ? "north" : "south", IsMoving = true, Version = actor.Version + 1 };
            SetActor(player.LocationId, updated); updates.Add(updated);
        }
        return updates;
    }

    public IReadOnlyList<RelationshipState> TakeDeliveryDogRelationships()
    {
        var relationships = new List<RelationshipState>();
        while (_deliveryDogRelationships.TryDequeue(out var relationship))
            if (_deliveryDogs.ContainsKey(relationship.ActorId)) relationships.Add(relationship);
        return relationships;
    }

    public IReadOnlyList<string> TakeDeliveryActorRemovals()
    {
        var removed = new List<string>();
        while (_deliveryActorRemovals.TryDequeue(out var id)) removed.Add(id);
        return removed;
    }
}
