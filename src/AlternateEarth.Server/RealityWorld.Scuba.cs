using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    internal static bool UnderwaterWeapon(string weapon) => weapon is "fist" or "knife" or "sword" or "hockeyStick" or "iceSkate" or "zombieBite" or "spearGun";
    private bool IsSubmerged(PlayerState player) => _dungeons.GetValueOrDefault(player.LocationId)?.Underwater is not null;
    internal static TravelMode SurfaceMode(TerrainType terrain, bool raft) => !IsWater(terrain) ? TravelMode.Walk
        : raft ? TravelMode.Raft : terrain == TerrainType.DeepWater ? TravelMode.Swim : TravelMode.Walk;

    internal static int DiveDifficulty(double visibleWaterSquareMeters) => Math.Clamp(1 + (int)(Math.Sqrt(Math.Max(0, visibleWaterSquareMeters)) / 12), 1, 100);

    private async Task<PlayerState> SubmergeAsync(PlayerState player, WorldBounds? view, CancellationToken token)
    {
        if (IsSubmerged(player)) return player;
        if (player.LocationId != "outdoor" || !IsWater(TerrainFor(player)))
            throw new InvalidOperationException("Activate scuba gear while in water or on a raft in water.");
        if (!player.GodMode && InventoryQuantity(player.Id, "scubaGear") <= 0) throw new InvalidOperationException("You need scuba gear to submerge.");
        var water = _baseEntities.Values.FirstOrDefault(e => e.Kind == EntityKind.Water && WaterGeometry.Contains(e, player.Position.X, player.Position.Y));
        var bounds = view ?? new WorldBounds(player.Position.X - 250, player.Position.Y - 250, player.Position.X + 250, player.Position.Y + 250);
        ValidateMapView(bounds);
        // Sample only this body within the visible map, excluding islands and nearby unrelated lakes.
        var area = 0d; var dx = (bounds.MaximumX - bounds.MinimumX) / 40; var dy = (bounds.MaximumY - bounds.MinimumY) / 40;
        for (var x = 0; x < 40; x++) for (var y = 0; y < 40; y++)
        {
            var px = bounds.MinimumX + (x + .5) * dx; var py = bounds.MinimumY + (y + .5) * dy;
            if ((water is null || WaterGeometry.Contains(water, px, py)) && IsWater(Navigation.TerrainAt(px, py))) area += dx * dy;
        }
        var difficulty = DiveDifficulty(area);
        var west = player.Position.X; var east = west;
        for (var i = 0; i < 500 && IsWater(Navigation.TerrainAt(west - 1, player.Position.Y)) && IsAreaLoaded(west - 1, player.Position.Y); i++) west--;
        for (var i = 0; i < 500 && IsWater(Navigation.TerrainAt(east + 1, player.Position.Y)) && IsAreaLoaded(east + 1, player.Position.Y); i++) east++;
        var width = Math.Clamp(east - west, 40, 180); var height = 20d;
        var id = $"dive:{player.Id}:{Guid.NewGuid():N}";
        var exit = player.Position with { X = Math.Clamp((player.Position.X - west) / Math.Max(1, east - west) * width, 1, width - 1), Y = height - 2, Z = 0 };
        var info = new UnderwaterState(water?.Properties.GetValueOrDefault("name") ?? "Submerged waters", player.Position, west, east,
            !IsWater(Navigation.TerrainAt(west - 1, player.Position.Y)) && !Navigation.IsBlocked(west - 1, player.Position.Y),
            !IsWater(Navigation.TerrainAt(east + 1, player.Position.Y)) && !Navigation.IsBlocked(east + 1, player.Position.Y), area);
        exit = ScubaGeometry.ClampPosition(width, height, info, exit);
        var actors = new List<ActorState>(); var chests = new List<TreasureChestState>();
        var random = new Random(StableInt(id));
        void Add(string subtype, string name, double x, double y, double health, double hostility, string? faction = null)
        {
            var actorId = $"{id}:animal:{actors.Count}";
            actors.Add(new(actorId, EntityKind.Animal, subtype, name, ScubaGeometry.ClampPosition(width, height, info, exit with { X = x, Y = y }), Facing: "east", HealthHearts: health,
                MaximumHealthHearts: health, FriendRating: hostility, TravelMode: TravelMode.Scuba, LocationId: id, FactionId: faction));
            _relationships[(player.Id, actorId)] = hostility;
        }
        for (var i = 0; i < 12 + difficulty / 2; i++) Add("fish", "Fish", 2 + random.NextDouble() * (width - 4), 3 + random.NextDouble() * 13, 2, 0);
        foreach(var type in NutritionCatalog.Shellfish) Add(type,type,2+random.NextDouble()*(width-4),1.5+random.NextDouble()*2,1,0);
        string[] predators = TerrainFor(player) == TerrainType.DeepWater ? ["shark", "octopus", "barracuda", "morayEel"] : ["barracuda", "morayEel"];
        for (var i = 0; i < 2 + difficulty / 8; i++)
        {
            var type = predators[i % predators.Length];
            Add(type, type == "morayEel" ? "Moray eel" : char.ToUpperInvariant(type[0]) + type[1..],
                3 + random.NextDouble() * (width - 6), 3 + random.NextDouble() * 10, 4 + difficulty * .3, -2 - difficulty * .04);
        }
        for (var i = 0; i < 1 + difficulty / 30; i++)
        {
            var chest = new TreasureChestState($"{id}:chest:{i}", ScubaGeometry.ClampPosition(width, height, info,
                exit with { X = width * (i + 1) / (2 + difficulty / 30), Y = 2 }), id, IsGrand: true);
            chests.Add(chest);
            Add(i % 2 == 0 ? "largeShark" : "largeOctopus", i % 2 == 0 ? "Treasure guardian · Large shark" : "Treasure guardian · Giant octopus",
                chest.Position.X + 1, 3, 15 + difficulty * 1.2, -5 - difficulty * .06, chest.Id);
        }
        var dungeon = new DungeonState(id, water?.Id ?? "water", width, height, [new(0, 0, width, height)], [], exit, actors, chests, [],
            SessionId: id, Difficulty: difficulty, WaterAreas: [new(0, 0, width, height, true)], Underwater: info);
        _dungeons[id] = dungeon; _returnPositions[player.Id] = player.Position;
        var weapon = UnderwaterWeapon(player.EquippedWeapon) ? player.EquippedWeapon
            : new[] { "spearGun", "sword", "knife", "hockeyStick", "iceSkate", "fist" }.First(w => CanUseWeapon(player.Id, w, player.GodMode));
        var updated = player with { LocationId = id, Position = exit, TravelMode = TravelMode.Scuba, Terrain = TerrainType.DeepWater,
            EquippedWeapon = weapon, SpeedMetersPerSecond = 0, Version = player.Version + 2 };
        await SavePlayerAsync(updated, token); await RevealAsync(updated, dungeon, token);
        return updated;
    }

    private WorldPosition DiveSurfacePosition(PlayerState player, DungeonState dungeon)
    {
        var dive = dungeon.Underwater!;
        return dive.Origin with { X = dive.WestX + Math.Clamp(player.Position.X / dungeon.Width, 0, 1) * (dive.EastX - dive.WestX) };
    }

    private async Task<MovementOutcome> MoveUnderwaterAsync(PlayerState player, DungeonState dungeon, MoveRequest request, CancellationToken token)
    {
        var (dx, dy, remaining) = ResolveMovementVector(player, request);
        var now = DateTimeOffset.UtcNow;
        var prior = _lastMovement.GetValueOrDefault(player.Id, now);
        _lastMovement[player.Id] = now;
        var seconds = Math.Clamp((now - prior).TotalSeconds, .01, .15);
        var step = Math.Min(3 * seconds, Math.Min(remaining ?? double.MaxValue, request.MaximumDistanceMeters is > 0 ? request.MaximumDistanceMeters.Value : double.MaxValue));
        var x = player.Position.X + dx * step; var y = player.Position.Y + dy * step;
        // Movement stays underwater, including at the waterline and shoreline. Surface explicitly.
        var next = ScubaGeometry.ClampPosition(dungeon.Width, dungeon.Height, dungeon.Underwater!, player.Position with { X = x, Y = y });
        var distance = player.Position.Distance2D(next);
        if (distance > step && player.Position.Y >= ScubaGeometry.FloorHeight(dungeon.Width, dungeon.Height, dungeon.Underwater!, player.Position.X) + .35)
            next = player.Position with { X = player.Position.X + (next.X - player.Position.X) * step / distance,
                Y = player.Position.Y + (next.Y - player.Position.Y) * step / distance };
        var updated = player with { Position = next, SpeedMetersPerSecond = player.Position.Distance2D(next) / seconds, Version = player.Version + 1 };
        await SavePlayerAsync(updated, token); await RevealAsync(updated, dungeon, token);
        return new(updated, true, false, false, false, false, null);
    }

    private async Task<PlayerState> ExitScubaAsync(PlayerState player, DungeonState dungeon, WorldPosition destination, CancellationToken token)
    {
        var terrain = Navigation.TerrainAt(destination.X, destination.Y);
        var updated = player with { LocationId = "outdoor", Position = destination with { Z = Navigation.ElevationAt(destination.X, destination.Y) },
            Terrain = terrain, TravelMode = SurfaceMode(terrain, player.GodMode || InventoryQuantity(player.Id, "inflatableRaft") > 0),
            SpeedMetersPerSecond = 0, SwimExhausted = false, Version = player.Version + 2 };
        await SavePlayerAsync(updated, token); _returnPositions.TryRemove(player.Id, out _);
        await ResetDungeonSessionAsync(player.Id, dungeon.Id, token);
        return updated;
    }

    private bool ChestNearWater(TreasureChestState chest, DungeonState? dungeon)
    {
        if (dungeon?.Underwater is not null) return true;
        for (var x = -12; x <= 12; x += 3) for (var y = -12; y <= 12; y += 3)
        {
            var point = chest.Position with { X = chest.Position.X + x, Y = chest.Position.Y + y };
            if (IsWater(dungeon is null ? Navigation.TerrainAt(point.X, point.Y) : DungeonTerrainAt(dungeon, point))) return true;
        }
        return false;
    }
}
