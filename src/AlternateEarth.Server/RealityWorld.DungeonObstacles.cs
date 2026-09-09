using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    internal static DungeonState AddDungeonObstacles(DungeonState dungeon, int seed)
    {
        if (dungeon.IsHome || dungeon.IsStore || dungeon.Difficulty < 5 || Math.Abs(seed % 3) == 0) return dungeon;
        var barriers = new List<DungeonBarrier>();
        foreach (var wall in dungeon.Walls.Skip(dungeon.ExteriorWallCount).Where(w => w.DoorStart >= 0))
        {
            var vertical = Math.Abs(wall.X1 - wall.X2) < .001;
            barriers.Add(new(dungeon.Id + ":barrier:" + barriers.Count, vertical ? wall.X1 - .3 : wall.DoorStart, vertical ? wall.DoorStart : wall.Y1 - .3,
                vertical ? .6 : wall.DoorEnd - wall.DoorStart, vertical ? wall.DoorEnd - wall.DoorStart : .6,
                dungeon.Difficulty >= 10 && barriers.Count % 2 == 1 ? "blast" : "fire"));
        }
        // The stripe crosses the entire interior; deep water has shallow launch banks on both sides.
        IReadOnlyList<DungeonWater> water = dungeon.Difficulty >= 15 && Math.Abs(seed % 2) == 0
            ? [new(0, dungeon.Height * .45 - 1, dungeon.Width, 1, false), new(0, dungeon.Height * .45, dungeon.Width, Math.Max(2, dungeon.Height * .15), true), new(0, dungeon.Height * .6, dungeon.Width, 1, false)] : [];
        var actors=dungeon.Actors.ToArray();
        if(water.Count>0&&actors.Length>0)
        {
            var farY=dungeon.Exit.Y<dungeon.Height*.45?dungeon.Height*.8:dungeon.Height*.2;
            for(var x=1d;x<dungeon.Width-1;x+=1)
            {var target=dungeon.Exit with{X=x,Y=farY};if(!InteriorPositionIsSafe(target,dungeon))continue;actors[^1]=actors[^1] with{Position=target};break;}
        }
        return dungeon with { Barriers = barriers, WaterAreas = water, Actors=actors };
    }
    private TerrainType DungeonTerrainAt(DungeonState d, WorldPosition p) => d.WaterAreas?.LastOrDefault(w => p.X >= w.X && p.X <= w.X + w.Width && p.Y >= w.Y && p.Y <= w.Y + w.Height) is { } water
        ? water.Deep ? TerrainType.DeepWater : TerrainType.ShallowWater : TerrainType.Pavement;
    private static bool BarrierContains(DungeonBarrier b, WorldPosition p) => !b.Destroyed && p.X >= b.X - .2 && p.X <= b.X + b.Width + .2 && p.Y >= b.Y - .2 && p.Y <= b.Y + b.Height + .2;
    public async Task<DungeonState> AttackDungeonBarrierAsync(string playerId, string id, CancellationToken token = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_dungeons.TryGetValue(player.LocationId, out var dungeon)) throw new InvalidOperationException("Enter a dungeon first.");
        var barrier = dungeon.Barriers?.FirstOrDefault(b => b.Id == id && !b.Destroyed) ?? throw new InvalidOperationException("That barrier is gone.");
        var weapon = player.EquippedWeapon;
        var explosive = weapon is "rocketLauncher" or "grenade";
        var fire = explosive || weapon is "molotovCocktail" or "flamethrower" || HazardCatalog.Find(weapon)?.Effect == "napalm";
        if (!fire || barrier.Kind == "blast" && !explosive) throw new InvalidOperationException(barrier.Kind == "blast" ? "This reinforced barrier needs an explosion." : "Burn this wooden barrier with fire or explosives.");
        var target = player.Position with { X = barrier.X + barrier.Width / 2, Y = barrier.Y + barrier.Height / 2 };
        var definition = WeaponDefinition(weapon);
        if (player.Position.Distance2D(target) > definition.Range || dungeon.Walls.Any(w => CrossesDungeonWall(player.Position, target, w))) throw new InvalidOperationException("Move into range with a clear shot at the barrier.");
        var attackTime = _probulatorClock.GetUtcNow();
        if (_lastPlayerAttack.TryGetValue((playerId, weapon), out var prior) && attackTime - prior < TimeSpan.FromSeconds(.5)) throw new InvalidOperationException("Let your weapon recover.");
        var ammo = definition.Ammo ?? (weapon == "flamethrower" ? null : weapon);
        if (!player.GodMode && ammo is not null && !RemoveInventory(playerId, ammo, 1)) throw new InvalidOperationException("You need ammunition for this attack.");
        if (!player.GodMode && weapon == "flamethrower")
        { if (player.FlamethrowerGasGallons < .2) throw new InvalidOperationException("You need flamethrower fuel."); await SavePlayerAsync(player with { FlamethrowerGasGallons = player.FlamethrowerGasGallons - .2, Version = player.Version + 1 }, token); }
        _lastPlayerAttack[(playerId, weapon)] = attackTime;
        await SaveInventoryAsync(playerId, token);
        dungeon = dungeon with { Barriers = dungeon.Barriers!.Select(b => b.Id == id ? b with { Destroyed = true } : b).ToArray() }; _dungeons[dungeon.Id] = dungeon;
        var now = _probulatorClock.GetUtcNow(); var fireId = Guid.NewGuid().ToString("N");
        _fireZones[fireId] = new(fireId, playerId, dungeon.Id, target, 2, now.AddSeconds(5), now);
        return WithDiscovery(playerId, dungeon);
    }
}
