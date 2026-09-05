using System.Globalization;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public static double BuildingSquareFeet(CanonicalEntity building)
    {
        if (building.Geometry.Count < 3) return 800;
        double twiceArea = 0;
        for (var index = 0; index < building.Geometry.Count; index++)
        {
            var current = building.Geometry[index];
            var next = building.Geometry[(index + 1) % building.Geometry.Count];
            twiceArea += current.X * next.Y - next.X * current.Y;
        }
        return Math.Max(1, Math.Abs(twiceArea) / 2d * 10.7639104167);
    }

    public static long CalculateBuildingPriceCents(CanonicalEntity building)
    {
        const double baseDollars = 350_000;
        var squareFeet = BuildingSquareFeet(building);
        var price = baseDollars * Math.Exp(Math.Max(0, squareFeet - 800) / 5_000d);
        price = Math.Clamp(price, baseDollars, 50_000_000);
        return checked((long)(Math.Round(price / 500d) * 500d * 100d));
    }

    private async Task EnsureHomeFurnitureAsync(string accountId, CanonicalEntity building, CancellationToken cancellationToken)
    {
        if (_homeFurniture.ContainsKey(accountId)) return;
        await _homeFurnitureLock.WaitAsync(cancellationToken);
        try
        {
            if (_homeFurniture.ContainsKey(accountId)) return;
            var loaded = (await _store.LoadHomeFurnitureAsync(accountId, Configuration.Id, cancellationToken)).ToList();
            if (loaded.Count == 0)
            {
                loaded = CreateStarterFurniture(accountId, building);
                await _store.SaveHomeFurnitureAsync(accountId, Configuration.Id, loaded, cancellationToken);
            }
            else if (!loaded.Any(item => item.Properties.GetValueOrDefault("objectType") == "storageChest"))
            {
                var definition = FurnitureCatalog.All.First(item => item.Type == "storageChest");
                var chest = CreateFurnitureEntity(accountId, definition, "oak", "ironbound", true, loaded.Count, building.Position.Region);
                var home = EmptyHome($"home:{accountId}:{building.Id}", building, loaded.Where(item => !IsStoredFurniture(item)).ToArray());
                if (TryFindOpenFurniturePosition(home, chest, loaded, out var position)) chest = SetFurniturePlacement(chest, position.X, position.Y, 0, false);
                loaded.Add(chest);
                await _store.SaveHomeFurnitureAsync(accountId, Configuration.Id, loaded, cancellationToken);
            }
            // Older layouts used rectangular bounds and may have persisted an
            // item beyond an irregular building footprint or over the entry.
            // Preserve every valid player placement and relocate only items
            // that are no longer safe in the canonical Home layout.
            if (RepairFurniturePlacements(accountId, building, loaded))
                await _store.SaveHomeFurnitureAsync(accountId, Configuration.Id, loaded, cancellationToken);
            _homeFurniture[accountId] = loaded;
        }
        finally { _homeFurnitureLock.Release(); }
    }

    private List<CanonicalEntity> CreateStarterFurniture(string accountId, CanonicalEntity building)
    {
        var home = EmptyHome($"home:{accountId}:{building.Id}", building, Array.Empty<CanonicalEntity>());
        var starters = new[]
        {
            ("storageChest", "oak", "ironbound"), ("fireplace", "brick", "masonry"), ("bed", "walnut", "striped"), ("wardrobe", "oak", "woodgrain"),
            ("diningTable", "oak", "woodgrain"), ("diningChair", "oak", "solid"), ("diningChair", "oak", "solid")
        };
        var result = new List<CanonicalEntity>();
        for (var index = 0; index < starters.Length; index++)
        {
            var starter = starters[index];
            var definition = FurnitureCatalog.All.First(item => item.Type == starter.Item1);
            var entity = CreateFurnitureEntity(accountId, definition, starter.Item2, starter.Item3, true, index, building.Position.Region);
            if (TryFindOpenFurniturePosition(home, entity, result, out var position)) entity = SetFurniturePlacement(entity, position.X, position.Y, 0, false);
            result.Add(entity);
        }
        return result;
    }

    private static CanonicalEntity CreateFurnitureEntity(string accountId, FurnitureDefinition definition, string color, string pattern, bool builtIn, int ordinal, RegionId region)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["objectType"] = definition.Type,
            ["displayName"] = definition.DisplayName,
            ["imageKey"] = definition.ImageKey,
            ["color"] = color,
            ["pattern"] = pattern,
            ["widthMeters"] = definition.WidthMeters.ToString(CultureInfo.InvariantCulture),
            ["depthMeters"] = definition.DepthMeters.ToString(CultureInfo.InvariantCulture),
            ["rotationDegrees"] = "0",
            ["stored"] = "true",
            ["builtIn"] = builtIn.ToString().ToLowerInvariant()
        };
        var suffix = builtIn ? $"starter:{ordinal}" : Guid.NewGuid().ToString("N");
        return new CanonicalEntity($"furniture:{accountId}:{suffix}", EntityKind.PlayerStructure, new WorldPosition(region, 0, 0), Array.Empty<GeometryPoint>(), properties, IsBaseEntity: false);
    }

    private static bool IsStoredFurniture(CanonicalEntity furniture) =>
        bool.TryParse(furniture.Properties.GetValueOrDefault("stored"), out var stored) && stored;

    private static CanonicalEntity SetFurniturePlacement(CanonicalEntity furniture, double x, double y, double rotation, bool stored)
    {
        var properties = new Dictionary<string, string>(furniture.Properties, StringComparer.OrdinalIgnoreCase)
        {
            ["stored"] = stored.ToString().ToLowerInvariant(),
            ["rotationDegrees"] = (((int)Math.Round(rotation / 90d) * 90 % 360) + 360) % 360 + ""
        };
        return furniture with { Position = furniture.Position with { X = x, Y = y, Z = 0 }, Properties = properties, Version = furniture.Version + 1 };
    }

    private DungeonState BuildHome(string id, CanonicalEntity building)
    {
        var accountId = id.StartsWith("home:", StringComparison.Ordinal) ? id[5..].Split(':')[0] : string.Empty;
        var furnishings = _homeFurniture.TryGetValue(accountId, out var saved) ? saved.Where(item => !IsStoredFurniture(item)).ToArray() : Array.Empty<CanonicalEntity>();
        return EmptyHome(id, building, furnishings);
    }

    private DungeonState EmptyHome(string id, CanonicalEntity building, IReadOnlyList<CanonicalEntity> furnishings)
    {
        var layout = CreateInteriorLayout(building);
        var width = layout.Width; var height = layout.Height;
        const double doorway = 2.2;
        var walls = layout.ExteriorWalls.ToList();
        var exteriorWallCount = walls.Count;
        var rooms = new List<DungeonRoom>();
        // The account/building-specific seed makes each acquired Home layout different,
        // while keeping the same layout after reconnects and server restarts.
        var layoutRandom = new Random(StableInt($"home-layout:{id}"));
        var splitX = width * (.38 + layoutRandom.NextDouble() * .24);
        var splitY = height * (.38 + layoutRandom.NextDouble() * .24);
        var rectangular = IsAxisAlignedRectangle(layout.Footprint);
        if (rectangular && width >= 12) walls.Add(new DungeonWall(splitX, 0, splitX, height, splitY - doorway / 2, splitY + doorway / 2));
        if (rectangular && height >= 12) walls.Add(new DungeonWall(0, splitY, width, splitY, splitX - doorway / 2, splitX + doorway / 2));
        if (rectangular && width >= 12 && height >= 12)
        {
            rooms.AddRange([new(0, 0, splitX, splitY), new(splitX, 0, width - splitX, splitY), new(0, splitY, splitX, height - splitY), new(splitX, splitY, width - splitX, height - splitY)]);
        }
        else if (rectangular && width >= 12) rooms.AddRange([new(0, 0, splitX, height), new(splitX, 0, width - splitX, height)]);
        else if (rectangular && height >= 12) rooms.AddRange([new(0, 0, width, splitY), new(0, splitY, width, height - splitY)]);
        else rooms.Add(new DungeonRoom(0, 0, width, height));
        var home = new DungeonState(id, building.Id, width, height, rooms, walls, layout.Exit, Array.Empty<ActorState>(), Array.Empty<TreasureChestState>(), Array.Empty<string>(), true, furnishings,
            layout.Footprint, exteriorWallCount, Doorway: layout.Doorway, SessionId: id);
        if (InteriorPositionIsSafe(home.Exit, home)) return home;
        WorldPosition? safeEntry = Enumerable.Range(1, Math.Max(1, (int)Math.Floor(height * 2) - 1))
            .SelectMany(y => Enumerable.Range(1, Math.Max(1, (int)Math.Floor(width * 2) - 1)).Select(x => new WorldPosition(building.Position.Region, x / 2d, y / 2d)))
            .Where(candidate => InteriorPositionIsSafe(candidate, home))
            .OrderBy(candidate => candidate.Distance2D(layout.Exit))
            .Select(candidate => (WorldPosition?)candidate)
            .FirstOrDefault();
        return safeEntry is null ? home : home with { Exit = safeEntry.Value };
    }

    private static (double Width, double Depth) FurnitureSize(CanonicalEntity furniture, double? rotationOverride = null)
    {
        var width = double.TryParse(furniture.Properties.GetValueOrDefault("widthMeters"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWidth) ? parsedWidth : .8;
        var depth = double.TryParse(furniture.Properties.GetValueOrDefault("depthMeters"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDepth) ? parsedDepth : .8;
        var rotation = rotationOverride ?? (double.TryParse(furniture.Properties.GetValueOrDefault("rotationDegrees"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRotation) ? parsedRotation : 0);
        return Math.Abs(((int)Math.Round(rotation / 90d)) % 2) == 1 ? (depth, width) : (width, depth);
    }

    private static bool FurnitureContains(CanonicalEntity furniture, WorldPosition point, double margin = 0)
    {
        var size = FurnitureSize(furniture);
        return Math.Abs(point.X - furniture.Position.X) <= size.Width / 2 + margin && Math.Abs(point.Y - furniture.Position.Y) <= size.Depth / 2 + margin;
    }

    private static bool FurniturePlacementValid(DungeonState home, CanonicalEntity furniture, double x, double y, double rotation, IReadOnlyList<CanonicalEntity> existing)
    {
        var size = FurnitureSize(furniture, rotation); const double clearance = .18;
        if (x - size.Width / 2 < .35 || y - size.Depth / 2 < .35 || x + size.Width / 2 > home.Width - .35 || y + size.Depth / 2 > home.Height - .35) return false;
        if (home.Footprint is { Count: >= 3 } footprint)
        {
            var halfWidth = size.Width / 2 + .35; var halfDepth = size.Depth / 2 + .35;
            if (!new[] { new GeometryPoint(x - halfWidth, y - halfDepth), new GeometryPoint(x + halfWidth, y - halfDepth), new GeometryPoint(x + halfWidth, y + halfDepth), new GeometryPoint(x - halfWidth, y + halfDepth) }
                .All(point => PointInsideFootprint(point, footprint))) return false;
        }
        var exitDx = x - home.Exit.X; var exitDy = y - home.Exit.Y;
        if (Math.Sqrt(exitDx * exitDx + exitDy * exitDy) < Math.Max(2.2, Math.Max(size.Width, size.Depth))) return false;
        foreach (var other in existing.Where(item => item.Id != furniture.Id && !IsStoredFurniture(item)))
        {
            var otherSize = FurnitureSize(other);
            if (Math.Abs(x - other.Position.X) < (size.Width + otherSize.Width) / 2 + clearance && Math.Abs(y - other.Position.Y) < (size.Depth + otherSize.Depth) / 2 + clearance) return false;
        }
        foreach (var wall in home.Walls.Skip(home.ExteriorWallCount))
        {
            var vertical = Math.Abs(wall.X1 - wall.X2) < .01;
            if (vertical && Math.Abs(x - wall.X1) < size.Width / 2 + clearance && y + size.Depth / 2 > Math.Min(wall.Y1, wall.Y2) && y - size.Depth / 2 < Math.Max(wall.Y1, wall.Y2))
            {
                if (wall.DoorStart < 0 || y - size.Depth / 2 < wall.DoorStart - .7 || y + size.Depth / 2 > wall.DoorEnd + .7) return false;
            }
            if (!vertical && Math.Abs(y - wall.Y1) < size.Depth / 2 + clearance && x + size.Width / 2 > Math.Min(wall.X1, wall.X2) && x - size.Width / 2 < Math.Max(wall.X1, wall.X2))
            {
                if (wall.DoorStart < 0 || x - size.Width / 2 < wall.DoorStart - .7 || x + size.Width / 2 > wall.DoorEnd + .7) return false;
            }
            if (wall.DoorStart >= 0)
            {
                var doorCenter = (wall.DoorStart + wall.DoorEnd) / 2;
                var doorX = vertical ? wall.X1 : doorCenter; var doorY = vertical ? doorCenter : wall.Y1;
                if (Math.Abs(x - doorX) < size.Width / 2 + 1.1 && Math.Abs(y - doorY) < size.Depth / 2 + 1.1) return false;
            }
        }
        return true;
    }

    private static bool TryFindOpenFurniturePosition(DungeonState home, CanonicalEntity furniture, IReadOnlyList<CanonicalEntity> existing, out WorldPosition position)
    {
        for (var y = .8; y <= home.Height - .8; y += .65)
        for (var x = .8; x <= home.Width - .8; x += .65)
        {
            if (!FurniturePlacementValid(home, furniture, x, y, 0, existing)) continue;
            position = new WorldPosition(home.Exit.Region, x, y); return true;
        }
        position = new WorldPosition(home.Exit.Region, 0, 0); return false;
    }

    private bool RepairFurniturePlacements(string accountId, CanonicalEntity building, List<CanonicalEntity> furniture)
    {
        var home = EmptyHome($"home:{accountId}:{building.Id}", building, Array.Empty<CanonicalEntity>());
        var accepted = new List<CanonicalEntity>(furniture.Count);
        var changed = false;
        foreach (var item in furniture)
        {
            if (IsStoredFurniture(item))
            {
                accepted.Add(item);
                continue;
            }

            var rotation = double.TryParse(item.Properties.GetValueOrDefault("rotationDegrees"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRotation) ? parsedRotation : 0;
            if (FurniturePlacementValid(home, item, item.Position.X, item.Position.Y, rotation, accepted))
            {
                accepted.Add(item);
                continue;
            }

            var repaired = TryFindOpenFurniturePosition(home, item, accepted, out var position)
                ? SetFurniturePlacement(item, position.X, position.Y, rotation, false)
                : SetFurniturePlacement(item, 0, 0, rotation, true);
            accepted.Add(repaired);
            changed = true;
        }

        if (!changed) return false;
        furniture.Clear();
        furniture.AddRange(accepted);
        return true;
    }

    private async Task AddPurchasedFurnitureAsync(string playerId, string offeredItemType, CancellationToken cancellationToken)
    {
        if (!FurnitureCatalog.TryParse(offeredItemType, out var definition, out var color, out var pattern)) throw new InvalidOperationException("Unknown furniture item.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId) || !_baseBuildings.TryGetValue(accountId, out var buildingId) || !_baseEntities.TryGetValue(buildingId, out var building)) throw new InvalidOperationException("A Home is required to purchase furniture.");
        await EnsureHomeFurnitureAsync(accountId, building, cancellationToken);
        await _homeFurnitureLock.WaitAsync(cancellationToken);
        try
        {
            var furniture = _homeFurniture[accountId];
            var purchased = CreateFurnitureEntity(accountId, definition, color, pattern, false, furniture.Count, building.Position.Region);
            var home = EmptyHome($"home:{accountId}:{buildingId}", building, furniture.Where(item => !IsStoredFurniture(item)).ToArray());
            if (TryFindOpenFurniturePosition(home, purchased, furniture, out var position)) purchased = SetFurniturePlacement(purchased, position.X, position.Y, 0, false);
            furniture.Add(purchased);
            await _store.SaveHomeFurnitureAsync(accountId, Configuration.Id, furniture, cancellationToken);
            RefreshHome(accountId, building);
        }
        finally { _homeFurnitureLock.Release(); }
    }

    private void RefreshHome(string accountId, CanonicalEntity building)
    {
        var id = $"home:{accountId}:{building.Id}";
        if (_dungeons.ContainsKey(id)) _dungeons[id] = BuildHome(id, building);
    }

    private async Task MoveFurnitureToNewBaseAsync(string accountId, CanonicalEntity building, CancellationToken cancellationToken)
    {
        await _homeFurnitureLock.WaitAsync(cancellationToken);
        try
        {
            var furniture = _homeFurniture.GetOrAdd(accountId, _ => new List<CanonicalEntity>());
            furniture.RemoveAll(item => item.Properties.GetValueOrDefault("builtIn") == "true");
            for (var index = 0; index < furniture.Count; index++) furniture[index] = SetFurniturePlacement(furniture[index], 0, 0, 0, true);
            furniture.AddRange(CreateStarterFurniture(accountId, building));
            await _store.SaveHomeFurnitureAsync(accountId, Configuration.Id, furniture, cancellationToken);
        }
        finally { _homeFurnitureLock.Release(); }
    }

    private async Task<DungeonState> UpdateFurnitureAsync(string playerId, string furnitureId, Func<DungeonState, CanonicalEntity, List<CanonicalEntity>, CanonicalEntity> update, CancellationToken cancellationToken)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_dungeons.TryGetValue(player.LocationId, out var home) || !home.IsHome) throw new InvalidOperationException("Furniture can only be changed inside your own Home.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId) || _baseBuildings.GetValueOrDefault(accountId) != home.BuildingId || !_homeFurniture.TryGetValue(accountId, out var furniture)) throw new InvalidOperationException("Visitors cannot alter this Home.");
        await _homeFurnitureLock.WaitAsync(cancellationToken);
        try
        {
            var index = furniture.FindIndex(item => item.Id == furnitureId); if (index < 0) throw new InvalidOperationException("Furniture item not found.");
            furniture[index] = update(home, furniture[index], furniture);
            await _store.SaveHomeFurnitureAsync(accountId, Configuration.Id, furniture, cancellationToken);
            var refreshed = BuildHome(home.Id, _baseEntities[home.BuildingId]); _dungeons[home.Id] = refreshed; return refreshed;
        }
        finally { _homeFurnitureLock.Release(); }
    }

    public Task<DungeonState> MoveFurnitureAsync(string playerId, MoveFurnitureRequest request, CancellationToken cancellationToken = default) =>
        UpdateFurnitureAsync(playerId, request.FurnitureId, (home, item, all) => FurniturePlacementValid(home, item, request.X, request.Y, double.Parse(item.Properties.GetValueOrDefault("rotationDegrees") ?? "0", CultureInfo.InvariantCulture), all)
            ? SetFurniturePlacement(item, request.X, request.Y, double.Parse(item.Properties.GetValueOrDefault("rotationDegrees") ?? "0", CultureInfo.InvariantCulture), false)
            : throw new InvalidOperationException("That position overlaps a wall, doorway, exit, or another item."), cancellationToken);

    public Task<DungeonState> PlaceFurnitureAsync(string playerId, PlaceFurnitureRequest request, CancellationToken cancellationToken = default) =>
        UpdateFurnitureAsync(playerId, request.FurnitureId, (home, item, all) => FurniturePlacementValid(home, item, request.X, request.Y, request.RotationDegrees, all)
            ? SetFurniturePlacement(item, request.X, request.Y, request.RotationDegrees, false)
            : throw new InvalidOperationException("That position overlaps a wall, doorway, exit, or another item."), cancellationToken);

    public Task<DungeonState> RotateFurnitureAsync(string playerId, RotateFurnitureRequest request, CancellationToken cancellationToken = default) =>
        UpdateFurnitureAsync(playerId, request.FurnitureId, (home, item, all) =>
        {
            if (IsStoredFurniture(item)) throw new InvalidOperationException("Place this item before rotating it.");
            var current = double.Parse(item.Properties.GetValueOrDefault("rotationDegrees") ?? "0", CultureInfo.InvariantCulture); var rotated = (current + 90) % 360;
            return FurniturePlacementValid(home, item, item.Position.X, item.Position.Y, rotated, all) ? SetFurniturePlacement(item, item.Position.X, item.Position.Y, rotated, false) : throw new InvalidOperationException("There is not enough room to rotate this item here.");
        }, cancellationToken);

    public Task<DungeonState> StoreFurnitureAsync(string playerId, StoreFurnitureRequest request, CancellationToken cancellationToken = default) =>
        UpdateFurnitureAsync(playerId, request.FurnitureId, (_, item, _) => item.Properties.GetValueOrDefault("builtIn") == "true" && item.Properties.GetValueOrDefault("objectType") is "fireplace" or "storageChest"
            ? throw new InvalidOperationException("That built-in fixture cannot be placed in storage.")
            : SetFurniturePlacement(item, 0, 0, 0, true), cancellationToken);
}
