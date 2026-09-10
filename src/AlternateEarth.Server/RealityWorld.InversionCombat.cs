using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private async Task StepInversionCombatAsync(double seconds, DateTimeOffset now, CancellationToken token)
    {
        if (_activeInversion is not { } e) return;
        if (e.Type == "northern") { await StepNorthernCombatAsync(seconds, now, token); return; }
        var held = e.Type is "hold" or "office" && (now.ToUnixTimeSeconds() % 15) >= 11;
        var missiles = e.Missiles.Select(m => held ? m with { ArrivesAtUtc = m.ArrivesAtUtc.AddSeconds(seconds) } : m).ToList();
        while (_reflections.TryDequeue(out var reflection))
        { var index = missiles.FindIndex(m => m.Id == reflection.Missile && m.Kind == "returnable" && m.ArrivesAtUtc > now); if (index >= 0 && _players.TryGetValue(reflection.Player, out var returner) && returner.Position.Distance2D(missiles[index].Target) <= 10) { var m = missiles[index]; missiles[index] = m with { OwnerId = returner.Id, Position = returner.Position, Target = m.Position, ArrivesAtUtc = now.AddSeconds(2), Kind = "returned" }; } }
        foreach (var missile in missiles.Where(m => !held && m.ArrivesAtUtc <= now).ToArray())
        {
            missiles.Remove(missile);
            if (missile.Kind == "returned" && _actors.Values.FirstOrDefault(a => ManagedEventActor(a) && a.Position.Distance2D(missile.Target) < 8) is { } receiver && Random.Shared.NextDouble() < .4 && _players.TryGetValue(missile.OwnerId, out var tennisPlayer))
            { missiles.Add(missile with { Id=Guid.NewGuid().ToString("N"), OwnerId=receiver.Id, Position=receiver.Position, Target=tennisPlayer.Position, Kind="returnable", ArrivesAtUtc=now.AddSeconds(4) }); EventSay(receiver.Id,receiver.Name,"RETURN TO RETURN TO SENDER!"); continue; }
            if (missile.Kind == "ram" && _actors.TryGetValue(missile.OwnerId,out var ark) && Navigation.CanTraverse(ark.Position,missile.Target)) _actors[ark.Id]=ark with {Position=missile.Target,Version=ark.Version+1};
            var radius = missile.Kind == "bullet" ? 1 : missile.Kind == "stomp" ? 5 : 8;
            var damage = missile.Kind == "bullet" ? 3 : missile.Kind is "stomp" or "ram" ? 9 : 5;
            await EventBlastAsync(missile.Target, radius, damage, missile.OwnerId, token, missile.Kind);
        }
        foreach (var original in _actors.Values.Where(a => ManagedEventActor(a) || _transformedActors.ContainsKey(a.Id)).ToArray())
        {
            var actor = original;
            var fromCenter=actor.Position.Distance2D(e.Center);
            if(fromCenter>e.Radius) {var inside=actor.Position with {X=e.Center.X+(actor.Position.X-e.Center.X)/fromCenter*(e.Radius-3),Y=e.Center.Y+(actor.Position.Y-e.Center.Y)/fromCenter*(e.Radius-3)};inside=Navigation.FindNearestWalkable(inside);if(inside.Distance2D(e.Center)>e.Radius)inside=e.Center;actor=actor with {Position=inside,Version=actor.Version+1};_actors[actor.Id]=actor;}
            if (actor.IsPassingThroughPortal(now) || held) continue;
            var targets = _players.Values.Where(InsideInversion).ToArray();
            var target = targets.OrderBy(p => p.Position.Distance2D(actor.Position)).FirstOrDefault();
            if (target is null) continue;
            var distance = actor.Position.Distance2D(target.Position); var boss = actor.Id == e.BossId;
            var barrel = actor.Subtype == "supportBarrel";
            var range = barrel ? 1.8 : e.Type is "mech" or "flood" or "plants" or "sender" or "office" or "hoa" ? 35 : boss ? 6 : 2;
            if (e.Type == "geese" && _stolenWeapons.ContainsKey(actor.Id) && distance < 15 && distance > .1)
            { var escape=actor.Position with {X=actor.Position.X+(actor.Position.X-target.Position.X)/distance*seconds*3,Y=actor.Position.Y+(actor.Position.Y-target.Position.Y)/distance*seconds*3};if(escape.Distance2D(e.Center)<e.Radius-2&&Navigation.CanTraverse(actor.Position,escape))_actors[actor.Id]=actor with {Position=escape,IsMoving=true,Version=actor.Version+1};continue; }
            if (distance > range)
            {
                var step = Math.Min(distance - range * .7, seconds * (boss ? 3 : 2)*SyrupSlow(actor.Position,actor.LocationId));
                var next = actor.Position with { X = actor.Position.X + (target.Position.X - actor.Position.X) / distance * step, Y = actor.Position.Y + (target.Position.Y - actor.Position.Y) / distance * step };
                if (next.Distance2D(e.Center) <= e.Radius - 2 && Navigation.CanTraverse(actor.Position, next))
                { actor = actor with { Position = next, IsMoving = true, Version = actor.Version + 1 }; _actors[actor.Id] = actor; }
            }
            if (barrel || distance > range || _inversionAttacks.GetValueOrDefault(actor.Id) > now) continue;
            _inversionAttacks[actor.Id] = now.AddSeconds(boss ? 2 : 3);
            if (e.Type == "geese" && target.EquippedWeapon is not ("none" or "fist" or "probulator") && !_stolenWeapons.ContainsKey(actor.Id))
            {
                var weapon = target.EquippedWeapon;
                if (RemoveInventory(target.Id, weapon, 1))
                { _stolenWeapons[actor.Id] = (target.Id, weapon); await SaveInventoryAsync(target.Id, token); await SavePlayerAsync(target with { EquippedWeapon = "fist", Version = target.Version + 1 }, token); EventSay(actor.Id, actor.Name, "Your weapon is now goose property. Catch me!"); }
            }
            var kind = e.Type == "mech" ? distance < 5 ? "stomp" : Random.Shared.Next(3) == 0 ? "missile" : "bullet"
                : e.Type == "flood" ? Random.Shared.Next(3) == 0 ? "ram" : "broadside"
                : e.Type == "sender" ? "returnable" : boss ? "stomp" : e.Type == "plants" ? "seed" : "bullet";
            // Every heavy strike announces a fixed impact position, allowing a player to dodge it.
            missiles.Add(new(Guid.NewGuid().ToString("N"), actor.Id, actor.Position, target.Position, kind, now.AddSeconds(kind == "bullet" ? .5 : kind == "returnable" ? 4 : 2)));
            if (e.Type == "mech")
            {
                var npc = _actors.Values.Where(a => !ManagedEventActor(a) && a.LocationId == "outdoor" && a.Position.Distance2D(actor.Position) <= 35).OrderBy(a => a.Position.Distance2D(actor.Position)).FirstOrDefault();
                if (npc is not null) missiles.Add(new(Guid.NewGuid().ToString("N"), actor.Id, actor.Position, npc.Position, "bullet", now.AddSeconds(.5)));
            }
        }
        if (_activeInversion is not null) _activeInversion = _activeInversion with { Missiles = missiles.TakeLast(160).ToArray() };
    }
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Player,string Missile)> _reflections = new();
    public void ReflectEventMissile(string playerId, string missileId)
    {
        if (!_players.TryGetValue(playerId, out var player) || !InsideInversion(player) || _activeInversion is not { Type: "sender" } e)
            throw new InvalidOperationException("No missile tennis match is active here.");
        var missile = e.Missiles.FirstOrDefault(m => m.Id == missileId && m.Kind == "returnable") ?? throw new InvalidOperationException("That projectile has already arrived.");
        var now = _probulatorClock.GetUtcNow();
        if (now >= missile.ArrivesAtUtc || player.Position.Distance2D(missile.Target) > 10) throw new InvalidOperationException("Move within 10 meters of the incoming projectile.");
        _reflections.Enqueue((playerId, missileId));
    }
    private async Task EventBlastAsync(WorldPosition position, double radius, double maximumDamage, string owner, CancellationToken token, string weapon = "eventExplosion")
    {
        if (_activeInversion is not { } e) return;
        foreach (var target in _players.Values.Where(p => InsideInversion(p) && p.Position.Distance2D(position) <= radius).ToArray())
        {
            var damage = radius <= 1 || weapon is "stomp" or "ram" ? maximumDamage : Math.Max(1, maximumDamage - (maximumDamage - 1) * target.Position.Distance2D(position) / radius);
            await EventHurtPlayerAsync(target, damage, owner, position, weapon, token);
        }
        foreach (var actor in _actors.Values.Where(a => a.Id != owner && a.LocationId == "outdoor" && a.Position.Distance2D(position) <= radius).ToArray())
        {
            var damage = radius <= 1 || weapon is "stomp" or "ram" ? maximumDamage : Math.Max(1, maximumDamage - (maximumDamage - 1) * actor.Position.Distance2D(position) / radius);
            var health = Math.Max(0, actor.HealthHearts - damage);
            if (health <= 0)
            {
                var attacker = _players.GetValueOrDefault(owner);
                if (attacker is not null) await UpdateActorHealthAsync(attacker, actor, 0, true, token);
                else { _actors.TryRemove(actor.Id, out _); _inversionKills.Enqueue((owner, actor)); _inversionRemovals.Enqueue(actor.Id); }
            }
            else _actors[actor.Id] = actor with { HealthHearts = health, Version = actor.Version + 1 };
            _inversionCombat.Enqueue(new(owner, actor.Id, weapon, position, actor.Position, true, damage, health <= 0, $"{actor.Name} took {damage:0.##} hearts.", health));
        }
    }
    private async Task EventHurtPlayerAsync(PlayerState target, double damage, string owner, WorldPosition origin, string weapon, CancellationToken token)
    {
        var health = Math.Max(PlayerCanDie(target.Id) ? 0 : 1, target.HealthHearts - damage);
        var died = health <= 0;
        var updated = target with { HealthHearts = health, Version = target.Version + 1 };
        if (died) updated = await DieAndResetPlayerAsync(updated, token);
        await SavePlayerAsync(updated, token);
        _inversionCombat.Enqueue(new(owner, target.Id, weapon, origin, target.Position, true, damage, died,
            weapon == "stink" && died ? "Trusted a fart. Paid the price." : $"{target.Name} took {damage:0.##} hearts from {weapon}.", updated.HealthHearts));
    }
    private readonly HashSet<string> _floodWaveHits = new();
    private async Task StepInversionEnvironmentAsync(DateTimeOffset now, CancellationToken token)
    {
        if (_activeInversion is not { } e) return;
        var patches = e.Patches.Where(p => p.EndsAtUtc > now).ToList();
        var people = _players.Values.Where(InsideInversion).ToArray();
        if (e.Type == "fruit")
        {
            var emitters = people.Select(p => p.Position).Concat(_actors.Values.Where(a => a.LocationId == "outdoor" && a.Position.Distance2D(e.Center) <= e.Radius).Select(a => a.Position));
            foreach (var position in emitters.Take(256)) patches.Add(new(Guid.NewGuid().ToString("N"), position, 1.2, "stink", now.AddSeconds(.5), now.AddSeconds(4)));
            foreach (var player in people)
            {
                var count = patches.Count(p => p.Kind == "stink" && p.ChangesAtUtc <= now && p.Position.Distance2D(player.Position) <= p.Radius);
                if (count > 0) await EventHurtPlayerAsync(player, .25 * count, e.BossId, player.Position, "stink", token);
                if (count >= 4 && _players.GetValueOrDefault(player.Id)?.HealthHearts > 0) await UnlockAchievementAsync(player.Id, "Poor Air Quality", token);
            }
            foreach (var actor in _actors.Values.Where(a => a.LocationId == "outdoor" && a.Position.Distance2D(e.Center) <= e.Radius).ToArray())
            {
                var damage = .25 * patches.Count(p => p.Kind == "stink" && p.ChangesAtUtc <= now && p.Position.Distance2D(actor.Position) <= p.Radius);
                if (damage <= 0) continue;
                if (damage >= actor.HealthHearts)
                { _actors.TryRemove(actor.Id, out _); _inversionKills.Enqueue(("environment", actor)); _inversionRemovals.Enqueue(actor.Id); }
                else _actors[actor.Id] = actor with { HealthHearts = actor.HealthHearts - damage, Version = actor.Version + 1 };
            }
        }
        if (e.Type is "flood" or "lava" or "hoa" && patches.Count < 20)
        {
            var angle = Random.Shared.NextDouble() * Math.Tau; var r = Random.Shared.NextDouble() * 85;
            patches.Add(new(Guid.NewGuid().ToString("N"), e.Center with { X = e.Center.X + Math.Cos(angle) * r, Y = e.Center.Y + Math.Sin(angle) * r }, e.Type == "flood" ? 12 : 6,
                e.Type == "flood" ? Random.Shared.Next(2) == 0 ? "shallowWater" : "deepWater" : e.Type == "lava" ? "lava" : "violation", now.AddSeconds(3), now.AddSeconds(15)));
        }
        foreach (var patch in patches.Where(p => p.ChangesAtUtc <= now && p.Kind is "lava" or "violation"))
        {
            await EventBlastAsync(patch.Position, patch.Radius, patch.Kind == "lava" ? 2 : 1, e.BossId, token, patch.Kind);
        }
        if (e.Type == "flood")
        {
            var wave = patches.FirstOrDefault(p => p.Kind.StartsWith("wave"));
            if (wave is null && now.ToUnixTimeSeconds() % 20 == 0)
                patches.Add(new(Guid.NewGuid().ToString("N"), e.Center, e.Radius, "wave" + Random.Shared.Next(4), now.AddSeconds(3), now.AddSeconds(13)));
            if (wave is not null && now >= wave.ChangesAtUtc)
            {
                var direction = int.Parse(wave.Kind[^1..]); var axis = direction % 2; var sign = direction < 2 ? 1 : -1;
                var offset = -e.Radius + (now - wave.ChangesAtUtc).TotalSeconds * e.Radius / 5;
                foreach (var player in people)
                {
                    var coordinate = axis == 0 ? player.Position.X - e.Center.X : player.Position.Y - e.Center.Y;
                    if (Math.Abs(coordinate * sign - offset) > 10 || !_floodWaveHits.Add(wave.Id+":"+player.Id)) continue;
                    var destination = player.Position with { X = player.Position.X + (axis == 0 ? sign * 8 : 0), Y = player.Position.Y + (axis == 1 ? sign * 8 : 0) };
                    if (!Navigation.CanTraverse(player.Position, destination)) { if (_baseEntities.Values.Any(b=>b.Kind==EntityKind.Building&&TransitGeometry.Overlaps(TransitGeometry.Footprint(destination,0,1,1), b.Geometry))) _drowningUntil[player.Id] = now.AddSeconds(5); }
                    else if (destination.Distance2D(e.Center) <= e.Radius) { await SavePlayerAsync(player with { Position = destination, Version = player.Version + 1 }, token); }
                    await EventHurtPlayerAsync(_players.GetValueOrDefault(player.Id, player), 2, e.BossId, wave.Position, "flood wall", token);
                }
                foreach(var actor in _actors.Values.Where(a=>a.Id!=e.BossId&&a.LocationId=="outdoor"&&a.Position.Distance2D(e.Center)<=e.Radius).ToArray())
                {
                    var coordinate=axis==0?actor.Position.X-e.Center.X:actor.Position.Y-e.Center.Y;
                    if(Math.Abs(coordinate*sign-offset)>10||!_floodWaveHits.Add(wave.Id+":"+actor.Id))continue;
                    var destination=actor.Position with {X=actor.Position.X+(axis==0?sign*8:0),Y=actor.Position.Y+(axis==1?sign*8:0)};
                    if(!Navigation.CanTraverse(actor.Position,destination)||destination.Distance2D(e.Center)>e.Radius)destination=actor.Position;
                    if(actor.HealthHearts<=2){_actors.TryRemove(actor.Id,out _);_inversionRemovals.Enqueue(actor.Id);_inversionKills.Enqueue(("flood",actor));}
                    else {_actors[actor.Id]=actor with{Position=destination,HealthHearts=actor.HealthHearts-2,Version=actor.Version+1};_inversionActorUpdates.Enqueue(_actors[actor.Id]);}
                }
            }
        }
        if (_activeInversion is not null) _activeInversion = _activeInversion with { Patches = patches.TakeLast(1024).ToArray() };
    }
    private TerrainType EventTerrainAt(WorldPosition position, TerrainType original)
    {
        if (_activeInversion is not { Type: "flood" } e || position.Distance2D(e.Center) > e.Radius || IsWater(original)) return original;
        var now = _probulatorClock.GetUtcNow();
        var patch = e.Patches.LastOrDefault(p => p.Kind is "deepWater" or "shallowWater" && p.ChangesAtUtc <= now && p.EndsAtUtc > now && p.Position.Distance2D(position) <= p.Radius);
        if (patch is null) return original;
        var reverse = (now-patch.ChangesAtUtc).TotalSeconds >= 6;
        return (patch.Kind == "deepWater") != reverse ? TerrainType.DeepWater : TerrainType.ShallowWater;
    }
}
