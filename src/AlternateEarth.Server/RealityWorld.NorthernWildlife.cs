using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly Dictionary<string, (DateTimeOffset ReplanAt, Queue<WorldPosition> Points)> _northernWildlifeRoutes = new();
    private readonly Dictionary<string, DateTimeOffset> _mooseSyrupAttacks = new();

    private void MooseSyrupAttack(ActorState moose, PlayerState target, DateTimeOffset now)
    {
        var drop = new LootDropState($"syrup:{Guid.NewGuid():N}", target.Position, target.LocationId, 0,
            [InventoryStack("mapleSyrup", 1)], now.AddSeconds(5), "mapleSyrupPuddle");
        _loot[drop.Id] = drop; _deathDropAnnouncements.Enqueue(drop);
        _inversionCombat.Enqueue(new(moose.Id, target.Id, "mooseSyrup", moose.Position, target.Position, false, 0, false,
            "The moose pees maple syrup! Standing in the puddle halves movement speed for up to 5 seconds. Collect it as maple syrup."));
    }

    private bool StandingInMapleSyrup(PlayerState player) => player.TravelMode != TravelMode.Ufo &&
        _loot.Values.Any(drop => drop.DropKind == "mapleSyrupPuddle" && drop.ExpiresAtUtc > _probulatorClock.GetUtcNow() &&
            drop.LocationId == player.LocationId && drop.Position.Distance2D(player.Position) <= 2.5);

    private void SpawnNorthernWildlife(InversionState e)
    {
        _northernWildlifeRoutes.Clear();
        _mooseSyrupAttacks.Clear();
        for (var n = 0; n < 24; n++)
        {
            var subtype = n < 4 ? "angryMoose" : n < 12 ? "helmetBeaver" : "tacticalGoose";
            var name = n < 4 ? "Angry moose" : n < 12 ? "Helmet-wearing beaver" : $"Tactical goose · squad {(n - 12) / 4 + 1}";
            var health = n < 4 ? 28 : n < 12 ? 14 : 8;
            // Geese arrive in three four-bird squads; the other wildlife surrounds the portal.
            var angle = n < 12 ? n * 2.39996 : (n - 12) / 4 * Math.Tau / 3;
            var radius = n < 12 ? 32 : 45 + (n % 4) * 2;
            var point = e.Center with { X = e.Center.X + Math.Cos(angle) * radius, Y = e.Center.Y + Math.Sin(angle) * radius };
            var id = e.Id + ":wildlife:" + n;
            SpawnInversionActor(e, id, subtype, name, health, point);
            _actors[id] = _actors[id] with { Kind = EntityKind.Animal, FactionId = e.Id, EquippedWeapon = "none" };
        }
    }

    private async Task StepNorthernWildlifeAsync(InversionState e, double seconds, DateTimeOffset now, CancellationToken token)
    {
        foreach (var id in _northernWildlifeRoutes.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) _northernWildlifeRoutes.Remove(id);
        foreach (var original in _actors.Values.Where(a => IsNorthernWildlife(a) && a.EventName == e.Name).ToArray())
        {
            if (original.IsPassingThroughPortal(now) || IsGasAsleep(original.Id) || IsProbulatorAbducted(original.Id)) continue;
            var target = _players.Values.Where(p => InsideInversion(p) && p.HealthHearts > 0 && p.TravelMode != TravelMode.Ufo && !IsProbulatorAbducted(p.Id))
                .OrderBy(p => p.Position.Distance2D(original.Position)).FirstOrDefault();
            var actor = original with { IsMoving = false };
            if (target is null) { if (original.IsMoving) { actor = actor with { Version = actor.Version + 1 }; _actors[actor.Id] = actor; _inversionActorUpdates.Enqueue(actor); } continue; }
            var moose = actor.Subtype == "angryMoose";
            var beaver = actor.Subtype == "helmetBeaver";
            var speed = moose ? 6 : beaver ? 4.5 : 5.5;
            var range = moose ? 2.4 : beaver ? 1.3 : 1.5;
            var distance = actor.Position.Distance2D(target.Position);
            if (distance > range)
            {
                var goal = target.Position;
                // The goose battalion approaches from alternating flanks before closing to peck.
                if (!moose && !beaver && distance > 7)
                {
                    var flank = (StableInt(actor.Id) & 1) == 0 ? -3d : 3d;
                    goal = goal with { X = goal.X - (goal.Y - actor.Position.Y) / distance * flank, Y = goal.Y + (goal.X - actor.Position.X) / distance * flank };
                }
                if (Navigation.CanTraverse(actor.Position, goal)) _northernWildlifeRoutes.Remove(actor.Id);
                else
                {
                    if (!_northernWildlifeRoutes.TryGetValue(actor.Id, out var route) || now >= route.ReplanAt)
                    {
                        var path = Navigation.FindPath(actor.Position, goal.X, goal.Y, _ => speed, cancellationToken: token);
                        route = (now.AddSeconds(2), new Queue<WorldPosition>(path.Success ? path.Waypoints : []));
                        _northernWildlifeRoutes[actor.Id] = route;
                    }
                    while (route.Points.TryPeek(out var point) && actor.Position.Distance2D(point) < .3) route.Points.Dequeue();
                    if (route.Points.TryPeek(out var waypoint)) goal = waypoint;
                    else goal = actor.Position;
                }
                var remaining = actor.Position.Distance2D(goal);
                if (remaining > .001)
                {
                    var step = Math.Min(remaining, Math.Min(Math.Max(0, distance - range * .8), Math.Clamp(seconds, 0, 1) * speed));
                    var next = actor.Position with { X = actor.Position.X + (goal.X - actor.Position.X) / remaining * step, Y = actor.Position.Y + (goal.Y - actor.Position.Y) / remaining * step };
                    if (next.Distance2D(e.Center) <= e.Radius && Navigation.CanTraverse(actor.Position, next))
                        actor = actor with { Position = next, IsMoving = true, Facing = goal.X < actor.Position.X ? "west" : "east" };
                }
            }
            if (actor.Position.Distance2D(target.Position) <= range && Navigation.CanTraverse(actor.Position, target.Position) && _inversionAttacks.GetValueOrDefault(actor.Id) <= now)
            {
                _inversionAttacks[actor.Id] = now.AddSeconds(moose ? .9 : beaver ? .65 : .5);
                CanadianAttackSpeech(actor);
                if (moose && !_mooseSyrupAttacks.ContainsKey(actor.Id)) _mooseSyrupAttacks[actor.Id] = now.AddSeconds(6);
                if (moose && _mooseSyrupAttacks.GetValueOrDefault(actor.Id) <= now)
                {
                    _mooseSyrupAttacks[actor.Id] = now.AddSeconds(6);
                    MooseSyrupAttack(actor, target, now);
                }
                else await EventHurtPlayerAsync(target, moose ? 2.5 : beaver ? 1 : .5, actor.Id, actor.Position, moose ? "mooseCharge" : beaver ? "beaverBite" : "goosePeck", token);
            }
            actor = actor with { Version = actor.Version + 1 }; _actors[actor.Id] = actor; _inversionActorUpdates.Enqueue(actor);
        }
    }
}
