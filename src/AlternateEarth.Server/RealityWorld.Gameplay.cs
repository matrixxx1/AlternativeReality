using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public const long BasePurchasePriceCents = 35_000_000;
    public const long GodModeBasePurchasePriceCents = 1;
    private static readonly (string Item, string Display, int Min, int Max, bool Single)[] MerchantCatalog =
    {
        ("rock", "a rock", 1, 100, false), ("ballBearing", "a ball bearing", 5, 200, false),
        ("skateboard", "a skateboard", 20_000, 30_000, true), ("bike", "a bike", 40_000, 50_000, true),
        ("dirtBike", "a dirt bike", 300_000, 500_000, true), ("motorcycle", "a motorcycle", 500_000, 1_000_000, true),
        ("gallonOfGas", "a gallon of gas", 500, 1_000, false), ("inflatableRaft", "an inflatable raft", 45_000, 65_000, true),
        ("flashlight", "a flashlight", 1_000, 5_000, true), ("lantern", "a lantern", 5_000, 10_000, true),
        ("laser", "a laser", 20_000, 40_000, true), ("magicHikingShoes", "magic hiking shoes", 10_000, 40_000, true),
        ("magicRunningShoes", "magic running shoes", 10_000, 40_000, true), ("hat", "a hat", 3_000, 7_500, true),
        ("slingshot", "a slingshot", 5_000, 15_000, true), ("crossbow", "a crossbow", 30_000, 50_000, true),
        ("arrow", "an arrow", 5, 500, false), ("pistol", "a pistol", 100_000, 300_000, true),
        ("rifle", "a rifle", 300_000, 600_000, true), ("bullet", "a bullet", 25, 500, false),
        ("knife", "a knife", 2_000, 4_000, true), ("sword", "a sword", 30_000, 50_000, true),
        ("water", "water", 50, 200, false), ("food", "food", 200, 500, false)
    };
    private static readonly string[] WeaponPowerOrder = ["rifle", "sword", "pistol", "crossbow", "knife", "slingshot", "rock", "fist"];
    private const double DirtBikeTankGallons = 2;
    private const double MotorcycleTankGallons = 4;
    private readonly ConcurrentDictionary<string, Dictionary<string, int>> _inventories = new();
    private readonly ConcurrentDictionary<(string Player, string Actor), double> _relationships = new();
    private readonly ConcurrentDictionary<string, DungeonState> _dungeons = new();
    private readonly ConcurrentDictionary<string, WorldPosition> _returnPositions = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastIdleHeal = new();
    private readonly ConcurrentDictionary<(string Player, string Merchant), TradeQuote> _tradeQuotes = new();
    private readonly ConcurrentDictionary<string, LootDropState> _loot = new();
    private readonly ConcurrentDictionary<string, TreasureChestState> _outdoorChests = new();
    private readonly ConcurrentDictionary<(string Actor, string Player), DateTimeOffset> _lastActorAttack = new();
    private readonly ConcurrentDictionary<string, string> _playerAccounts = new();
    private readonly ConcurrentDictionary<string, string> _baseBuildings = new();

    public PlayerPrivateState GetPrivateState(string playerId)
    {
        var inventory = new InventoryState(playerId, GetInventoryItems(playerId));
        _players.TryGetValue(playerId, out var player);
        var dungeon = player is not null && player.LocationId != "outdoor" && _dungeons.TryGetValue(player.LocationId, out var found) ? WithDiscovery(playerId, found) : null;
        var relationships = _relationships.Where(pair => pair.Key.Player == playerId)
            .Select(pair => new RelationshipState(playerId, pair.Key.Actor, pair.Value)).ToArray();
        var location = player?.LocationId ?? "outdoor";
        var chests = location == "outdoor" ? _outdoorChests.Values.ToArray() : dungeon?.Chests ?? Array.Empty<TreasureChestState>();
        var loot = _loot.Values.Where(drop => drop.LocationId == location).ToArray();
        BaseState? baseState = null;
        if (_playerAccounts.TryGetValue(playerId, out var accountId) && _baseBuildings.TryGetValue(accountId, out var buildingId) && _baseEntities.TryGetValue(buildingId, out var building))
        {
            var door = _baseEntities.Values.FirstOrDefault(entity => entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == buildingId);
            if (door is not null) baseState = new BaseState(buildingId, door.Id, door.Position, player?.Name ?? "Explorer");
        }
        return new PlayerPrivateState(inventory, dungeon, relationships, chests, loot, baseState);
    }

    public async Task<PlayerState> SetGodModeAsync(string playerId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var hikingShoesOn = enabled ? player.MagicHikingShoesOn : player.MagicHikingShoesOn && InventoryQuantity(playerId, "magicHikingShoes") > 0;
        var runningShoesOn = enabled ? player.MagicRunningShoesOn : player.MagicRunningShoesOn && InventoryQuantity(playerId, "magicRunningShoes") > 0;
        if (hikingShoesOn && runningShoesOn) runningShoesOn = false;
        var updated = player with
        {
            GodMode = enabled,
            Stamina = enabled ? player.MaximumStamina : player.Stamina,
            Water = enabled ? player.MaximumWater : player.Water,
            WalletCents = enabled ? Math.Max(50_000, player.WalletCents) : player.WalletCents,
            HealthHearts = enabled ? Math.Max(1, player.HealthHearts) : player.HealthHearts,
            TravelMode = !enabled && TravelModeUnavailable(playerId, player) ? TravelMode.Walk : player.TravelMode,
            FlashlightOn = enabled ? player.FlashlightOn : player.FlashlightOn && InventoryQuantity(playerId,"flashlight")>0,
            LanternOn = enabled ? player.LanternOn : player.LanternOn && InventoryQuantity(playerId,"lantern")>0,
            LaserOn = enabled ? player.LaserOn : player.LaserOn && InventoryQuantity(playerId,"laser")>0,
            MagicHikingShoesOn = hikingShoesOn,
            MagicRunningShoesOn = runningShoesOn,
            HatOn = enabled ? player.HatOn : player.HatOn && InventoryQuantity(playerId, "hat") > 0,
            EquippedWeapon = enabled ? player.EquippedWeapon : BestUsableWeapon(playerId, player.EquippedWeapon, false),
            Version = player.Version + 1
        };
        await SavePlayerAsync(updated, cancellationToken); return updated;
    }

    private bool TravelModeUnavailable(string playerId, PlayerState player) => player.TravelMode switch
    {
        TravelMode.Skateboard => InventoryQuantity(playerId, "skateboard") <= 0,
        TravelMode.Bike => InventoryQuantity(playerId, "bike") <= 0,
        TravelMode.Raft => InventoryQuantity(playerId, "inflatableRaft") <= 0,
        TravelMode.DirtBike => InventoryQuantity(playerId, "dirtBike") <= 0 || player.DirtBikeGasGallons <= 0,
        TravelMode.Motorcycle => InventoryQuantity(playerId, "motorcycle") <= 0 || player.MotorcycleGasGallons <= 0,
        _ => false
    };

    public async Task<PlayerState> SetLightsAsync(string playerId,bool flashlight,bool lantern,bool laser,CancellationToken cancellationToken=default)
    { if(!_players.TryGetValue(playerId,out var player))throw new InvalidOperationException("Unknown player.");if(!player.GodMode&&flashlight&&InventoryQuantity(playerId,"flashlight")<=0)throw new InvalidOperationException("You need a flashlight in your inventory.");if(!player.GodMode&&lantern&&InventoryQuantity(playerId,"lantern")<=0)throw new InvalidOperationException("You need a lantern in your inventory.");if(!player.GodMode&&laser&&InventoryQuantity(playerId,"laser")<=0)throw new InvalidOperationException("You need a laser in your inventory.");var updated=player with{FlashlightOn=flashlight,LanternOn=lantern,LaserOn=laser,Version=player.Version+1};await SavePlayerAsync(updated,cancellationToken);return updated; }

    public Task<PlayerState> SetMagicHikingShoesAsync(string playerId, bool enabled, CancellationToken cancellationToken = default) =>
        SetEquipmentAsync(playerId, "shoes", enabled ? "magicHikingShoes" : null, cancellationToken);

    public Task<PlayerState> SetMagicRunningShoesAsync(string playerId, bool enabled, CancellationToken cancellationToken = default) =>
        SetEquipmentAsync(playerId, "shoes", enabled ? "magicRunningShoes" : null, cancellationToken);

    public async Task<PlayerState> SetEquipmentAsync(string playerId, string slot, string? itemType, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        slot = (slot ?? string.Empty).Trim().ToLowerInvariant();
        itemType = string.IsNullOrWhiteSpace(itemType) ? null : itemType.Trim();
        PlayerState updated;
        if (slot == "shoes")
        {
            if (itemType is not null && itemType is not ("magicHikingShoes" or "magicRunningShoes")) throw new InvalidOperationException("That item cannot be worn as shoes.");
            if (itemType is not null && !player.GodMode && InventoryQuantity(playerId, itemType) <= 0) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your inventory.");
            updated = player with { MagicHikingShoesOn = itemType == "magicHikingShoes", MagicRunningShoesOn = itemType == "magicRunningShoes", SpeedMetersPerSecond = 0, Version = player.Version + 1 };
        }
        else if (slot == "hat")
        {
            if (itemType is not null && itemType != "hat") throw new InvalidOperationException("That item cannot be worn as a hat.");
            if (itemType is not null && !player.GodMode && InventoryQuantity(playerId, "hat") <= 0) throw new InvalidOperationException("You need a hat in your inventory.");
            updated = player with { HatOn = itemType == "hat", Version = player.Version + 1 };
        }
        else if (slot == "weapon")
        {
            itemType ??= "none";
            itemType = itemType.ToLowerInvariant();
            if (itemType != "none" && !WeaponPowerOrder.Contains(itemType)) throw new InvalidOperationException("That item cannot be equipped as a weapon.");
            if (itemType != "none" && !player.GodMode && !OwnsWeapon(playerId, itemType)) throw new InvalidOperationException($"You need {DisplayItem(itemType)} in your backpack.");
            updated = player with { EquippedWeapon = itemType, Version = player.Version + 1 };
        }
        else throw new InvalidOperationException("That equipment slot is not available yet.");
        await SavePlayerAsync(updated, cancellationToken);
        return updated;
    }

    private bool playerIsGod(string playerId) => _players.TryGetValue(playerId, out var player) && player.GodMode;

    public async Task<IReadOnlyList<PlayerState>> AdvanceVitalsAsync(TimeSpan elapsed, CancellationToken cancellationToken)
    {
        var changed = new List<PlayerState>(); var now = DateTimeOffset.UtcNow;
        foreach (var pair in _players.ToArray())
        {
            var player = pair.Value; var updated = player;
            var idle = _lastMovement.TryGetValue(pair.Key, out var lastMove) ? now - lastMove : TimeSpan.Zero;
            if (player.GodMode)
            {
                updated = updated with
                {
                    Stamina = player.MaximumStamina, Water = player.MaximumWater,
                    WalletCents = Math.Max(50_000, player.WalletCents),
                    HealthHearts = Math.Min(player.MaximumHealthHearts, Math.Max(1, player.HealthHearts) + (elapsed.TotalSeconds / 5))
                };
            }
            else
            {
                if (idle >= TimeSpan.FromSeconds(1) && player.Stamina < player.MaximumStamina)
                    updated = updated with { Stamina = Math.Min(player.MaximumStamina, player.Stamina + (.25 * elapsed.TotalSeconds)) };
                var wearingHat = player.HatOn && InventoryQuantity(pair.Key, "hat") > 0;
                if (!(player.WaterProtectedUntilUtc > now)) updated = updated with { Water = Math.Max(0, updated.Water - WorldNavigation.WaterDrain(elapsed.TotalSeconds, wearingHat)) };
                if (idle >= TimeSpan.FromSeconds(30) && player.HealthHearts < player.MaximumHealthHearts &&
                    (!_lastIdleHeal.TryGetValue(pair.Key, out var lastHeal) || now - lastHeal >= TimeSpan.FromSeconds(30)))
                {
                    updated = updated with { HealthHearts = Math.Min(player.MaximumHealthHearts, updated.HealthHearts + .5) };
                    _lastIdleHeal[pair.Key] = now;
                }
            }
            if (updated.Stamina == player.Stamina && updated.Water == player.Water && updated.WalletCents == player.WalletCents && updated.HealthHearts == player.HealthHearts) continue;
            updated = updated with { SpeedMetersPerSecond = idle > TimeSpan.FromSeconds(.5) ? 0 : updated.SpeedMetersPerSecond, Version = player.Version + 1 };
            await SavePlayerAsync(updated, cancellationToken); changed.Add(updated);
        }
        foreach (var expired in _loot.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray()) _loot.TryRemove(expired, out _);
        foreach (var expired in _outdoorChests.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray()) _outdoorChests.TryRemove(expired, out _);
        return changed;
    }

    public async Task<PlayerState> ConsumeItemAsync(string playerId, string itemType, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var normalized = itemType.Trim().ToLowerInvariant(); if (normalized is not ("food" or "water" or "gallonofgas")) throw new InvalidOperationException("That item cannot be consumed.");
        if (normalized == "gallonofgas")
        {
            if (player.TravelMode is not (TravelMode.DirtBike or TravelMode.Motorcycle)) throw new InvalidOperationException("Select your dirt bike or motorcycle before adding gas.");
            var current = FuelGallons(player);
            var capacity = player.TravelMode == TravelMode.DirtBike ? DirtBikeTankGallons : MotorcycleTankGallons;
            if (current >= capacity - .0001) throw new InvalidOperationException($"Your {VehicleName(player.TravelMode)} tank is already full.");
        }
        if (!RemoveInventory(playerId, normalized, 1)) throw new InvalidOperationException($"You do not have any {normalized}.");
        var now = DateTimeOffset.UtcNow;
        var updated = normalized == "food"
            ? player with { Stamina = player.MaximumStamina, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2), FoodProtectedUntilUtc = now.AddMinutes(5), Version = player.Version + 1 }
            : normalized == "water"
                ? player with { Water = player.MaximumWater, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2), WaterProtectedUntilUtc = now.AddMinutes(5), Version = player.Version + 1 }
                : player.TravelMode == TravelMode.DirtBike
                    ? player with { DirtBikeGasGallons = Math.Min(DirtBikeTankGallons, player.DirtBikeGasGallons + 1), Version = player.Version + 1 }
                    : player with { MotorcycleGasGallons = Math.Min(MotorcycleTankGallons, player.MotorcycleGasGallons + 1), Version = player.Version + 1 };
        await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken); return updated;
    }

    public async Task<(PlayerState Player, DungeonState Dungeon)> EnterDungeonAsync(string playerId, string doorId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId != "outdoor") throw new InvalidOperationException("You cannot enter that dungeon now.");
        var door = _baseEntities.Values.FirstOrDefault(entity => entity.Id == doorId && entity.Kind == EntityKind.Door) ?? throw new InvalidOperationException("That door does not exist.");
        if (player.Position.Distance2D(door.Position) > 6) throw new InvalidOperationException("Move closer to the door first.");
        var buildingId = door.Properties.GetValueOrDefault("buildingId") ?? throw new InvalidOperationException("The door has no building.");
        var building = _baseEntities.Values.First(entity => entity.Id == buildingId);
        var ownsBase = _playerAccounts.TryGetValue(playerId, out var accountId) && _baseBuildings.GetValueOrDefault(accountId) == buildingId;
        var dungeonId = ownsBase ? $"home:{accountId}:{buildingId}" : $"dungeon:{buildingId}:{playerId}";
        await ResetDungeonSessionAsync(playerId, dungeonId, cancellationToken);
        var dungeon = ownsBase ? GenerateHome(dungeonId, building) : GenerateDungeon(dungeonId, building);
        _dungeons[dungeonId] = dungeon;
        foreach (var actor in dungeon.Actors)
            _relationships[(playerId, actor.Id)] = actor.FriendRating;
        _returnPositions[playerId] = player.Position;
        var discovery = new HashSet<string>();
        if (!dungeon.IsHome) { RevealDungeonCells(discovery, dungeon.Exit.X, dungeon.Exit.Y); foreach (var cell in discovery) await _store.SaveDiscoveryAsync(Configuration.Id, playerId, dungeonId, cell, cancellationToken); }
        var updated = player with { LocationId = dungeonId, Position = dungeon.Exit, Terrain = TerrainType.Pavement, TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Version = player.Version + 1 };
        await SavePlayerAsync(updated, cancellationToken); return (updated, dungeon with { RevealedCells = discovery.ToArray() });
    }

    public async Task<PlayerState> ExitDungeonAsync(string playerId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId == "outdoor") throw new InvalidOperationException("You are not inside a dungeon.");
        var dungeonId = player.LocationId;
        var destination = _returnPositions.TryRemove(playerId, out var saved) ? saved : Navigation.FindNearestWalkable(new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
        var updated = player with { LocationId = "outdoor", Position = destination, Terrain = Navigation.TerrainAt(destination.X, destination.Y), SpeedMetersPerSecond = 0, Version = player.Version + 1 };
        await SavePlayerAsync(updated, cancellationToken);
        await ResetDungeonSessionAsync(playerId, dungeonId, cancellationToken);
        return updated;
    }

    private async Task ResetDungeonSessionAsync(string playerId, string dungeonId, CancellationToken cancellationToken)
    {
        _dungeons.TryRemove(dungeonId, out _);
        foreach (var relationship in _relationships.Keys.Where(key => key.Player == playerId && key.Actor.StartsWith(dungeonId + ":", StringComparison.Ordinal)).ToArray()) _relationships.TryRemove(relationship, out _);
        foreach (var drop in _loot.Where(pair => pair.Value.LocationId == dungeonId).Select(pair => pair.Key).ToArray()) _loot.TryRemove(drop, out _);
        foreach (var quote in _tradeQuotes.Keys.Where(key => key.Player == playerId && key.Merchant.StartsWith(dungeonId + ":", StringComparison.Ordinal)).ToArray()) _tradeQuotes.TryRemove(quote, out _);
        await _store.ResetDungeonStateAsync(Configuration.Id, playerId, dungeonId, cancellationToken);
    }

    public async Task<(PlayerState Player, long PriceCents)> PurchaseBaseAsync(string playerId, PurchaseBaseRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId != "outdoor") throw new InvalidOperationException("A base can only be purchased from outside its door.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId)) throw new InvalidOperationException("An authenticated account is required to purchase a base.");
        var door = _baseEntities.Values.FirstOrDefault(entity => entity.Id == request.DoorId && entity.Kind == EntityKind.Door) ?? throw new InvalidOperationException("That door does not exist.");
        if (player.Position.Distance2D(door.Position) > 6) throw new InvalidOperationException("Move within 6 meters of the door before purchasing this base.");
        var buildingId = door.Properties.GetValueOrDefault("buildingId") ?? throw new InvalidOperationException("The door has no building.");
        var building = _baseEntities.GetValueOrDefault(buildingId) ?? throw new InvalidOperationException("That building is unavailable.");
        if (_baseBuildings.GetValueOrDefault(accountId) == buildingId) throw new InvalidOperationException("This building is already your base.");
        var price = player.GodMode ? GodModeBasePurchasePriceCents : BasePurchasePriceCents;
        if (player.WalletCents < price) throw new InvalidOperationException($"This base costs ${price / 100m:N2}; you do not have enough money.");

        await _basePurchaseLock.WaitAsync(cancellationToken);
        try
        {
            var owner = await _store.LoadBaseOwnerAsync(Configuration.Id, buildingId, cancellationToken);
            if (owner is not null && owner != accountId) throw new InvalidOperationException("That building is already another player's base.");
            await _store.SaveBaseBuildingAsync(accountId, Configuration.Id, buildingId, building.Position, cancellationToken);
            _baseBuildings[accountId] = buildingId;
            var homeId = $"home:{accountId}:{buildingId}";
            _dungeons.GetOrAdd(homeId, _ => GenerateHome(homeId, building));
            foreach (var linkedPlayer in _playerAccounts.Where(pair => pair.Value == accountId).Select(pair => pair.Key)) SetBaseReturnPosition(linkedPlayer, buildingId);
            var updated = player with { WalletCents = player.WalletCents - price, Version = player.Version + 1 };
            await SavePlayerAsync(updated, cancellationToken);
            return (updated, price);
        }
        finally { _basePurchaseLock.Release(); }
    }

    private async Task<MovementOutcome> MoveInDungeonAsync(PlayerState player, MoveRequest request, CancellationToken cancellationToken)
    {
        if (!_dungeons.TryGetValue(player.LocationId, out var dungeon)) return new(player, false, true, false, false, false, "Dungeon unavailable.");
        if (!player.GodMode && IsMotorized(player.TravelMode) && FuelGallons(player) <= 0)
        {
            var stopped = player with { SpeedMetersPerSecond = 0, Version = player.Version + 1 };
            await SavePlayerAsync(stopped, cancellationToken);
            return new(stopped, false, true, false, false, false, $"Your {VehicleName(player.TravelMode)} is out of gas. Add gasoline before using it again.");
        }
        var length = Math.Sqrt(request.X * request.X + request.Y * request.Y); var dx = length > 1 ? request.X / length : request.X; var dy = length > 1 ? request.Y / length : request.Y;
        var now = DateTimeOffset.UtcNow; var previous = _lastMovement.AddOrUpdate(player.Id, now, (_, old) => now); var elapsed = Math.Clamp((now - previous).TotalSeconds, .01, .15);
        var wearingMagicHikingShoes = player.MagicHikingShoesOn && (player.GodMode || InventoryQuantity(player.Id, "magicHikingShoes") > 0);
        var wearingMagicRunningShoes = player.MagicRunningShoesOn && (player.GodMode || InventoryQuantity(player.Id, "magicRunningShoes") > 0);
        var speed = Navigation.SpeedFor(TerrainType.Pavement, player.TravelMode, player.Stamina / player.MaximumStamina, wearingMagicHikingShoes, wearingMagicRunningShoes) * (player.Water <= 0 ? .5 : 1) * (player.GodMode ? 5 : 1);
        var next = player.Position with { X = Math.Clamp(player.Position.X + dx * speed * elapsed, .5, dungeon.Width - .5), Y = Math.Clamp(player.Position.Y + dy * speed * elapsed, .5, dungeon.Height - .5), Z = 0 };
        var blocked = dungeon.Walls.Any(wall => CrossesDungeonWall(player.Position, next, wall)); if (blocked) next = player.Position;
        var distance = player.Position.Distance2D(next);
        var dirtBikeGas = player.DirtBikeGasGallons;
        var motorcycleGas = player.MotorcycleGasGallons;
        if (!player.GodMode && distance > .001)
        {
            if (player.TravelMode == TravelMode.DirtBike) dirtBikeGas = FuelAfterTravel(dirtBikeGas, distance, DirtBikeMilesPerGallon);
            if (player.TravelMode == TravelMode.Motorcycle) motorcycleGas = FuelAfterTravel(motorcycleGas, distance, MotorcycleMilesPerGallon);
        }
        var updated = player with { Position = next, SpeedMetersPerSecond = blocked ? 0 : player.Position.Distance2D(next) / elapsed, Terrain = TerrainType.Pavement,
            Stamina = player.TravelMode == TravelMode.Run && !(player.FoodProtectedUntilUtc > now) ? Math.Max(0, player.Stamina - WorldNavigation.RunningStaminaDrain(elapsed, wearingMagicHikingShoes || wearingMagicRunningShoes && WorldNavigation.MagicRunningShoesReduceStaminaOn(TerrainType.Pavement))) : player.Stamina,
            DirtBikeGasGallons = dirtBikeGas, MotorcycleGasGallons = motorcycleGas, Version = player.Version + 1 };
        await RevealAsync(updated, dungeon, cancellationToken); await SavePlayerAsync(updated, cancellationToken); return new(updated, !blocked, blocked, false, false, false, null);
    }

    private async Task RevealAsync(PlayerState player, DungeonState dungeon, CancellationToken cancellationToken)
    {
        var current = await _store.LoadDiscoveryAsync(Configuration.Id, player.Id, dungeon.Id, cancellationToken); var before = current.Count;
        RevealDungeonCells(current, player.Position.X, player.Position.Y);
        if (current.Count == before) return; foreach (var cell in current) await _store.SaveDiscoveryAsync(Configuration.Id, player.Id, dungeon.Id, cell, cancellationToken);
    }

    private static void RevealDungeonCells(HashSet<string> cells, double x, double y)
    {
        var cx = (int)Math.Floor(x / 3); var cy = (int)Math.Floor(y / 3); for (var ox = -1; ox <= 1; ox++) for (var oy = -1; oy <= 1; oy++) cells.Add($"{cx + ox},{cy + oy}");
    }

    private DungeonState WithDiscovery(string playerId, DungeonState dungeon)
    {
        var cells = _store.LoadDiscoveryAsync(Configuration.Id, playerId, dungeon.Id).GetAwaiter().GetResult(); return dungeon with { RevealedCells = cells.ToArray() };
    }

    private DungeonState GenerateDungeon(string id, CanonicalEntity building)
    {
        var minX = building.Geometry.Min(p => p.X); var maxX = building.Geometry.Max(p => p.X); var minY = building.Geometry.Min(p => p.Y); var maxY = building.Geometry.Max(p => p.Y);
        var width = Math.Clamp(maxX - minX, 12, 60); var height = Math.Clamp(maxY - minY, 12, 60); var seed = StableInt(id); var random = new Random(seed);
        var splitX = width * (.4 + random.NextDouble() * .2); var splitY = height * (.4 + random.NextDouble() * .2); const double door = 2.2;
        var walls = new[]
        {
            new DungeonWall(0,0,width,0), new DungeonWall(width,0,width,height), new DungeonWall(width,height,0,height), new DungeonWall(0,height,0,0),
            new DungeonWall(splitX,0,splitX,height,splitY-door/2,splitY+door/2), new DungeonWall(0,splitY,width,splitY,splitX-door/2,splitX+door/2)
        };
        var rooms = new[] { new DungeonRoom(0,0,splitX,splitY), new DungeonRoom(splitX,0,width-splitX,splitY), new DungeonRoom(0,splitY,splitX,height-splitY), new DungeonRoom(splitX,splitY,width-splitX,height-splitY) };
        var region = building.Position.Region; var actors = new List<ActorState>();
        for (var i = 0; i < random.Next(3, 7); i++)
        {
            var merchant = i == 0 && random.NextDouble() < .55; var foe = merchant ? random.NextDouble() * 2 : -2 + random.NextDouble() * 4;
            actors.Add(new ActorState($"{id}:npc:{i}", EntityKind.Npc, merchant ? "merchant" : "resident", merchant ? $"Merchant {i + 1}" : $"Dungeon Dweller {i + 1}",
                new WorldPosition(region, 3 + random.NextDouble() * (width - 6), 3 + random.NextDouble() * (height - 6)), HealthHearts: 4 + random.Next(5), MaximumHealthHearts: 8,
                FriendRating: foe, IsMerchant: merchant, TravelMode: (TravelMode)random.Next(0, 4), LocationId: id));
        }
        var chests = Enumerable.Range(0, random.Next(1, 4)).Select(i => new TreasureChestState($"{id}:chest:{i}", new WorldPosition(region, 2 + random.NextDouble() * (width - 4), 2 + random.NextDouble() * (height - 4)), id)).ToArray();
        return new DungeonState(id, building.Id, width, height, rooms, walls, new WorldPosition(region, 2, 2), actors, chests, Array.Empty<string>());
    }

    private DungeonState GenerateHome(string id, CanonicalEntity building)
    {
        var width=Math.Clamp(building.Geometry.Max(p=>p.X)-building.Geometry.Min(p=>p.X),12,60);var height=Math.Clamp(building.Geometry.Max(p=>p.Y)-building.Geometry.Min(p=>p.Y),12,60);var region=building.Position.Region;
        var walls=new[]{new DungeonWall(0,0,width,0),new DungeonWall(width,0,width,height),new DungeonWall(width,height,0,height),new DungeonWall(0,height,0,0)};
        CanonicalEntity Furnishing(string key,string type,double x,double y)=>new($"{id}:{key}",EntityKind.PlayerStructure,new WorldPosition(region,x,y),Array.Empty<GeometryPoint>(),new Dictionary<string,string>{{"objectType",type}});
        var furnishings=new[]{Furnishing("fireplace","fireplace",width*.5,height-1.2),Furnishing("bed","bed",2.2,height-2.5),Furnishing("table","table",width*.58,height*.48),Furnishing("chair1","chair",width*.58-1.4,height*.48),Furnishing("chair2","chair",width*.58+1.4,height*.48)};
        return new DungeonState(id,building.Id,width,height,new[]{new DungeonRoom(0,0,width,height)},walls,new WorldPosition(region,2,2),Array.Empty<ActorState>(),Array.Empty<TreasureChestState>(),Array.Empty<string>(),true,furnishings);
    }

    public async Task<PlayerState> RestAtBedAsync(string playerId,string bedId,CancellationToken cancellationToken=default)
    {
        if(!_players.TryGetValue(playerId,out var player)||!_dungeons.TryGetValue(player.LocationId,out var home)||!home.IsHome)throw new InvalidOperationException("You can only rest in your own bed.");
        var bed=home.Furnishings?.FirstOrDefault(item=>item.Id==bedId&&item.Properties.GetValueOrDefault("objectType")=="bed")??throw new InvalidOperationException("Bed not found.");
        if(player.Position.Distance2D(bed.Position)>4)throw new InvalidOperationException("Move closer to the bed.");var until=DateTimeOffset.UtcNow.AddMinutes(5);
        var updated=player with{HealthHearts=player.MaximumHealthHearts,Stamina=player.MaximumStamina,Water=player.MaximumWater,FoodProtectedUntilUtc=until,WaterProtectedUntilUtc=until,Version=player.Version+1};await SavePlayerAsync(updated,cancellationToken);return updated;
    }

    public TradeQuote RequestTrade(string playerId, string merchantId)
    {
        var merchant = FindActor(playerId, merchantId); if (merchant is null || !merchant.IsMerchant) throw new InvalidOperationException("That character is not a merchant.");
        var player = _players[playerId]; if (player.LocationId != merchant.LocationId || player.Position.Distance2D(merchant.Position) > 5) throw new InvalidOperationException("Move closer to trade.");
        return _tradeQuotes.GetOrAdd((playerId, merchantId), _ => GenerateTradeQuote(playerId, merchant));
    }

    public async Task<(PlayerState Player, InventoryState Inventory, RelationshipState Relationship)> ConfirmTradeAsync(string playerId, ConfirmTradeRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player."); var quote = RequestTrade(playerId, request.MerchantId);
        var requested = request.Purchases.Where(line => line.Quantity > 0).ToArray(); if (requested.Length == 0) throw new InvalidOperationException("Select at least one item.");
        long total = 0; foreach (var line in requested) { var offer = quote.Offers.FirstOrDefault(o => o.ItemType.Equals(line.ItemType, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Item not offered."); if (line.Quantity > offer.Quantity) throw new InvalidOperationException("Not enough stock."); checked { total += offer.UnitPriceCents * line.Quantity; } }
        if (total > player.WalletCents) throw new InvalidOperationException("You cannot spend more than you have.");
        foreach (var line in requested) AddInventory(playerId, line.ItemType, line.Quantity); var updated = player with { WalletCents = player.WalletCents - total, Version = player.Version + 1 };
        var friend = Relationship(playerId, request.MerchantId) + .5; _relationships[(playerId, request.MerchantId)] = friend; await _store.SaveRelationshipAsync(Configuration.Id, new(playerId, request.MerchantId, friend), cancellationToken);
        _tradeQuotes.TryRemove((playerId, request.MerchantId), out _); await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken);
        return (updated, new InventoryState(playerId, GetInventoryItems(playerId)), new RelationshipState(playerId, request.MerchantId, friend));
    }

    private TradeQuote GenerateTradeQuote(string playerId, ActorState merchant)
    {
        var baseOffers = BaseMerchantOffers(merchant); var friendship = Relationship(playerId, merchant.Id); var factor = Math.Clamp(1 - friendship * .025, .6, 1.4);
        var offers = baseOffers.Select(offer =>
        {
            var range = MerchantCatalog.First(item => item.Item == offer.ItemType);
            return offer with { UnitPriceCents = (long)Math.Clamp(Math.Round(offer.UnitPriceCents * factor), range.Min, range.Max) };
        }).ToArray();
        return new TradeQuote(merchant.Id, merchant.Name, friendship, offers);
    }

    private MerchantOffer[] BaseMerchantOffers(ActorState merchant)
    {
        var random = new Random(StableInt($"{merchant.Id}:{DateTimeOffset.UtcNow:yyyyMMdd}"));
        var selected = MerchantCatalog.OrderBy(_ => random.Next()).Take(random.Next(3, 7)).ToArray();
        return selected.Select(range => new MerchantOffer(range.Item, range.Single ? 1 : random.Next(3, 31), random.Next(range.Min, range.Max + 1))).ToArray();
    }

    private static string DisplayItem(string itemType) => itemType switch
    {
        "magicHikingShoes" => "magic hiking shoes",
        "magicRunningShoes" => "magic running shoes",
        "dirtBike" => "a dirt bike",
        "gallonOfGas" => "a gallon of gas",
        "ballBearing" => "a ball bearing",
        "fist" => "your fist",
        "none" => "no weapon",
        "crossbow" => "a crossbow",
        "knife" => "a knife",
        "sword" => "a sword",
        "pistol" => "a pistol",
        "rifle" => "a rifle",
        _ => itemType
    };

    public async Task<CombatResult> AttackAsync(string playerId, CombatRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (request.TargetId == playerId) throw new InvalidOperationException("You cannot attack yourself.");
        var actorTarget = FindActor(playerId, request.TargetId);
        var playerTarget = actorTarget is null && _players.TryGetValue(request.TargetId, out var other) ? other : null;
        if (actorTarget is null && playerTarget is null) throw new InvalidOperationException("Target not found.");
        if (playerTarget is not null && !Configuration.PvpEnabled) throw new InvalidOperationException("Player-versus-player combat is disabled in this reality.");
        var targetLocation = actorTarget?.LocationId ?? playerTarget!.LocationId;
        if (targetLocation != player.LocationId) throw new InvalidOperationException("Target is not here.");
        var targetPosition = actorTarget?.Position ?? playerTarget!.Position;
        var targetName = actorTarget?.Name ?? playerTarget!.Name;
        var targetHealth = actorTarget?.HealthHearts ?? playerTarget!.HealthHearts;
        if (player.EquippedWeapon.Equals("none", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Equip a weapon before attacking.");
        var weapon = BestUsableWeapon(playerId, player.EquippedWeapon, player.GodMode);
        var (range, baseDamage, ammo) = WeaponDefinition(weapon);
        var distance = player.Position.Distance2D(targetPosition);
        if (distance > range) throw new InvalidOperationException($"{targetName} is beyond the {range:0.#}-meter range of your {DisplayItem(weapon)}.");
        if (!player.GodMode && ammo is not null && !RemoveInventory(playerId, ammo, 1)) throw new InvalidOperationException($"You need {DisplayItem(ammo)}.");
        var hitChance = weapon == "fist" ? 1 : Math.Clamp(.97 - distance / (range * 1.25), .15, .97);
        var hit = RandomNumberGenerator.GetInt32(1_000_000) < hitChance * 1_000_000;
        var damage = hit ? baseDamage : 0;
        var died = hit && !((playerTarget?.GodMode) ?? false) && targetHealth - damage <= 0;
        RelationshipState? relationship = null;
        if (actorTarget is not null)
        {
            var relation = Relationship(playerId, actorTarget.Id) - 1; _relationships[(playerId, actorTarget.Id)] = relation;
            relationship = new(playerId, actorTarget.Id, relation);
            await _store.SaveRelationshipAsync(Configuration.Id, relationship, cancellationToken);
            if (hit) UpdateActorHealth(player, actorTarget, Math.Max(0, targetHealth - damage), died);
        }
        PlayerState? updatedTarget = null;
        if (playerTarget is not null && hit)
        {
            var remaining = playerTarget.GodMode ? Math.Max(1, targetHealth - damage) : Math.Max(0, targetHealth - damage);
            updatedTarget = died ? ResetPlayer(playerTarget with { HealthHearts = 0 }) : playerTarget with { HealthHearts = remaining, Version = playerTarget.Version + 1 };
            await SavePlayerAsync(updatedTarget, cancellationToken);
        }
        if (!player.GodMode && ammo is not null) await SaveInventoryAsync(playerId, cancellationToken);
        var nextWeapon = player.GodMode ? weapon : BestUsableWeapon(playerId, weapon, false);
        var updatedAttacker = player.EquippedWeapon == nextWeapon ? player : player with { EquippedWeapon = nextWeapon, Version = player.Version + 1 };
        if (!ReferenceEquals(updatedAttacker, player)) await SavePlayerAsync(updatedAttacker, cancellationToken);
        var message = hit ? (died ? $"{targetName} was defeated." : $"Hit {targetName} for {damage:0.##} heart{(damage == 1 ? "" : "s")}.") : $"Missed {targetName}.";
        if (nextWeapon != weapon) message += $" Switched to {DisplayItem(nextWeapon)}.";
        var eventHealth = updatedTarget?.HealthHearts ?? (hit ? Math.Max(0, targetHealth - damage) : targetHealth);
        var combat = new CombatEvent(playerId, request.TargetId, weapon, player.Position, targetPosition, hit, damage, died, message, eventHealth);
        var dungeon = player.LocationId != "outdoor" && _dungeons.TryGetValue(player.LocationId, out var d) ? WithDiscovery(playerId, d) : null;
        return new(combat, updatedAttacker, updatedTarget, new InventoryState(playerId, GetInventoryItems(playerId)), relationship, dungeon);
    }

    private bool OwnsWeapon(string playerId, string weapon) => weapon == "fist" || InventoryQuantity(playerId, weapon) > 0;

    private bool CanUseWeapon(string playerId, string weapon, bool godMode)
    {
        if (godMode) return WeaponPowerOrder.Contains(weapon);
        if (!OwnsWeapon(playerId, weapon)) return false;
        var ammo = WeaponDefinition(weapon).Ammo;
        return ammo is null || InventoryQuantity(playerId, ammo) > 0;
    }

    private string BestUsableWeapon(string playerId, string requestedWeapon, bool godMode)
    {
        requestedWeapon = (requestedWeapon ?? "fist").ToLowerInvariant();
        if (requestedWeapon == "none") return "none";
        var start = Array.IndexOf(WeaponPowerOrder, requestedWeapon);
        if (start < 0) start = WeaponPowerOrder.Length - 1;
        for (var i = start; i < WeaponPowerOrder.Length; i++) if (CanUseWeapon(playerId, WeaponPowerOrder[i], godMode)) return WeaponPowerOrder[i];
        return "fist";
    }

    private static (double Range, double Damage, string? Ammo) WeaponDefinition(string weapon) => weapon switch
    {
        "fist" => (1.6, .25, null),
        "knife" => (1.6, 2, null),
        "sword" => (2.3, 5, null),
        "rock" => (25, 1, "rock"),
        "slingshot" => (60, 2, "ballBearing"),
        "crossbow" => (100, 3, "arrow"),
        "pistol" => (50, 5, "bullet"),
        "rifle" => (200, 7, "bullet"),
        _ => throw new InvalidOperationException("Unknown weapon.")
    };

    private void UpdateActorHealth(PlayerState player, ActorState actor, double health, bool died)
    {
        if (player.LocationId == "outdoor") { if (died) _actors.TryRemove(actor.Id, out _); else _actors[actor.Id] = actor with { HealthHearts = health, Version = actor.Version + 1 }; }
        else if (_dungeons.TryGetValue(player.LocationId, out var dungeon)) _dungeons[player.LocationId] = dungeon with { Actors = died ? dungeon.Actors.Where(a => a.Id != actor.Id).ToArray() : dungeon.Actors.Select(a => a.Id == actor.Id ? a with { HealthHearts = health, Version = a.Version + 1 } : a).ToArray() };
        if (died)
        {
            var random = new Random(); var items = new List<ItemStack>(); if (random.NextDouble() < .8) items.Add(new("rock", random.Next(1, 6))); if (random.NextDouble() < .6) items.Add(new("ballBearing", random.Next(1, 9)));
            var drop = new LootDropState($"loot:{Guid.NewGuid():N}", actor.Position, player.LocationId, random.Next(1, 1001), items, DateTimeOffset.UtcNow.AddMinutes(5)); _loot[drop.Id] = drop;
        }
    }

    public async Task<(PlayerState Player, InventoryState Inventory, string Message)> OpenChestAsync(string playerId, string chestId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        TreasureChestState? chest;
        DungeonState? dungeon = null;
        if (player.LocationId == "outdoor") chest = _outdoorChests.GetValueOrDefault(chestId);
        else { _dungeons.TryGetValue(player.LocationId, out dungeon); chest = dungeon?.Chests.FirstOrDefault(c => c.Id == chestId); }
        if (chest is null) throw new InvalidOperationException("Chest not found."); if (player.Position.Distance2D(chest.Position) > 4) throw new InvalidOperationException("Move closer to the chest.");
        var random = new Random(StableInt($"{chestId}:{playerId}")); var money = random.Next(25, 5001); var rocks = random.Next(1, 8); var bearings = random.Next(0, 12); AddInventory(playerId, "rock", rocks); if (bearings > 0) AddInventory(playerId, "ballBearing", bearings);
        if (player.LocationId == "outdoor") _outdoorChests.TryRemove(chestId, out _); else _dungeons[player.LocationId] = dungeon! with { Chests = dungeon!.Chests.Where(c => c.Id != chestId).ToArray() }; var updated = player with { WalletCents = player.WalletCents + money, Version = player.Version + 1 };
        await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken); return (updated, new(playerId, GetInventoryItems(playerId)), $"Found {money / 100.0:C}, {rocks} rocks, and {bearings} ball bearings.");
    }

    public TreasureChestState MarkChestSeen(string playerId, string chestId)
    {
        if (!_players.ContainsKey(playerId)) throw new InvalidOperationException("Unknown player.");
        if (!_outdoorChests.TryGetValue(chestId, out var chest)) throw new InvalidOperationException("Chest not found.");
        if (chest.ExpiresAtUtc is not null) return chest;
        var updated = chest with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(3) }; _outdoorChests[chestId] = updated; return updated;
    }

    public async Task<HostileTick> AdvanceHostilityAsync(TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        var changedActors = new Dictionary<string, ActorState>(); var changedPlayers = new List<PlayerState>(); var combat = new List<CombatEvent>();
        var now = DateTimeOffset.UtcNow;
        foreach (var player in _players.Values.ToArray())
        {
            var sight = Weather.IsDay ? 45d : 16d + Math.Max(0, Weather.MoonIllumination) * 18d + (player.FlashlightOn?22:0) + (player.LanternOn?12:0) + (player.LaserOn?30:0);
            _dungeons.TryGetValue(player.LocationId, out var currentDungeon);
            var actors = player.LocationId == "outdoor" ? _actors.Values.ToArray() : currentDungeon?.Actors.ToArray() ?? Array.Empty<ActorState>();
            var target = actors.Select(actor => (Actor: actor, Rating: Relationship(player.Id, actor.Id)))
                .Where(item => item.Rating < 0 && item.Actor.Position.Distance2D(player.Position) <= sight)
                .OrderBy(item => item.Actor.Position.Distance2D(player.Position)).FirstOrDefault();
            if (target.Actor is null) continue;
            var actor = target.Actor; var distance = actor.Position.Distance2D(player.Position); var hostility = Math.Abs(target.Rating);
            if (distance > 1.35)
            {
                var dx = (player.Position.X - actor.Position.X) / distance; var dy = (player.Position.Y - actor.Position.Y) / distance;
                var terrain = player.LocationId == "outdoor" ? Navigation.TerrainAt(actor.Position.X, actor.Position.Y) : TerrainType.Pavement;
                var travel = actor.Kind == EntityKind.Animal ? (hostility >= 2 ? TravelMode.Run : TravelMode.Walk) : actor.TravelMode;
                if (!WorldNavigation.SupportsTravelMode(terrain, travel)) travel = TravelMode.Walk;
                var speed = Navigation.SpeedFor(terrain, travel) * Math.Clamp(1 + hostility * .12, 1, 2.4);
                var next = actor.Position with { X = actor.Position.X + dx * speed * elapsed.TotalSeconds, Y = actor.Position.Y + dy * speed * elapsed.TotalSeconds };
                var blocked = player.LocationId == "outdoor" ? !Navigation.CanTraverse(actor.Position, next, true) : currentDungeon!.Walls.Any(wall => CrossesDungeonWall(actor.Position, next, wall));
                if (!blocked)
                {
                    if (player.LocationId == "outdoor") next = next with { Z = Navigation.ElevationAt(next.X, next.Y) };
                    var facing = Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? "east" : "west") : (dy > 0 ? "north" : "south");
                    actor = actor with { Position = next, Facing = facing, IsMoving = true, TravelMode = travel, Version = actor.Version + 1 };
                    SetActor(player.LocationId, actor); changedActors[actor.Id] = actor;
                }
                continue;
            }
            if (_lastActorAttack.TryGetValue((actor.Id, player.Id), out var last) && now - last < TimeSpan.FromSeconds(3)) continue;
            _lastActorAttack[(actor.Id, player.Id)] = now;
            var damage = Math.Min(3, .5 + hostility * .25); var health = player.GodMode ? Math.Max(1, player.HealthHearts - damage) : Math.Max(0, player.HealthHearts - damage);
            var died = health <= 0; var updated = died ? ResetPlayer(player with { HealthHearts = 0 }) : player with { HealthHearts = health, Version = player.Version + 1 };
            await SavePlayerAsync(updated, cancellationToken); changedPlayers.Add(updated);
            combat.Add(new CombatEvent(actor.Id, player.Id, "attack", actor.Position, player.Position, true, damage, died, died ? $"{actor.Name} defeated you." : $"{actor.Name} attacked for {damage:0.##} hearts.", updated.HealthHearts));
        }
        return new HostileTick(changedActors.Values.ToArray(), changedPlayers, combat);
    }

    private void SetActor(string locationId, ActorState actor)
    {
        if (locationId == "outdoor") _actors[actor.Id] = actor;
        else if (_dungeons.TryGetValue(locationId, out var dungeon)) _dungeons[locationId] = dungeon with { Actors = dungeon.Actors.Select(item => item.Id == actor.Id ? actor : item).ToArray() };
    }

    public bool IsInOwnHome(string playerId)=>_players.TryGetValue(playerId,out var player)&&player.LocationId.StartsWith("home:",StringComparison.Ordinal);
    public async Task PrepareCharacterSwitchAsync(string currentPlayerId,string nextPlayerId,string nextName,CancellationToken cancellationToken=default)
    {
        if(!_players.TryGetValue(currentPlayerId,out var current)||!IsInOwnHome(currentPlayerId))throw new InvalidOperationException("Characters can only be switched inside your base.");
        var existing=await _store.LoadCharacterAsync(Configuration.Id,nextPlayerId,cancellationToken);
        var next=(existing??new PlayerState(nextPlayerId,nextName,current.Position)) with{Name=nextName,Position=current.Position,LocationId=current.LocationId,SpeedMetersPerSecond=0,Version=(existing?.Version??0)+1};
        await _store.SaveCharacterAsync(Configuration.Id,next,cancellationToken);
    }

    public async Task<(PlayerState Player, InventoryState Inventory, string Message)> CollectLootAsync(string playerId, string lootId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_loot.TryGetValue(lootId, out var loot) || loot.LocationId != player.LocationId) throw new InvalidOperationException("Treasure is not available.");
        if (player.Position.Distance2D(loot.Position) > 4) throw new InvalidOperationException("Move closer to the treasure.");
        _loot.TryRemove(lootId, out _); foreach (var item in loot.Items) AddInventory(playerId, item.ItemType, item.Quantity);
        var updated = player with { WalletCents = player.WalletCents + loot.MoneyCents, Version = player.Version + 1 };
        await SaveInventoryAsync(playerId, cancellationToken); await SavePlayerAsync(updated, cancellationToken);
        return (updated, new(playerId, GetInventoryItems(playerId)), $"Collected {loot.MoneyCents / 100.0:C} and dropped supplies.");
    }

    private ActorState? FindActor(string playerId, string actorId)
    {
        if (!_players.TryGetValue(playerId, out var player)) return null; if (player.LocationId == "outdoor") return _actors.GetValueOrDefault(actorId);
        return _dungeons.TryGetValue(player.LocationId, out var dungeon) ? dungeon.Actors.FirstOrDefault(actor => actor.Id == actorId) : null;
    }

    private double Relationship(string playerId, string actorId) => _relationships.GetValueOrDefault((playerId, actorId));
    private int InventoryQuantity(string playerId, string item) => _inventories.TryGetValue(playerId, out var inventory) && inventory.TryGetValue(item, out var quantity) ? quantity : 0;
    private void AddInventory(string playerId, string item, int quantity) { var inventory = _inventories.GetOrAdd(playerId, _ => new(StringComparer.OrdinalIgnoreCase)); lock (inventory) inventory[item] = inventory.GetValueOrDefault(item) + quantity; }
    private bool RemoveInventory(string playerId, string item, int quantity) { var inventory = _inventories.GetOrAdd(playerId, _ => new(StringComparer.OrdinalIgnoreCase)); lock (inventory) { var current = inventory.GetValueOrDefault(item); if (current < quantity) return false; inventory[item] = current - quantity; return true; } }
    private IReadOnlyList<ItemStack> GetInventoryItems(string playerId) { if (!_inventories.TryGetValue(playerId, out var inventory)) return Array.Empty<ItemStack>(); lock (inventory) return inventory.Where(p => p.Value > 0).OrderBy(p => p.Key).Select(p => new ItemStack(p.Key, p.Value)).ToArray(); }
    private Task SaveInventoryAsync(string playerId, CancellationToken cancellationToken) => _store.SaveInventoryAsync(new(playerId, GetInventoryItems(playerId)), cancellationToken);

    private static bool CrossesDungeonWall(WorldPosition start, WorldPosition end, DungeonWall wall)
    {
        static double Cross(double ax,double ay,double bx,double by,double cx,double cy)=>(bx-ax)*(cy-ay)-(by-ay)*(cx-ax);
        var c1=Cross(start.X,start.Y,end.X,end.Y,wall.X1,wall.Y1);var c2=Cross(start.X,start.Y,end.X,end.Y,wall.X2,wall.Y2);var c3=Cross(wall.X1,wall.Y1,wall.X2,wall.Y2,start.X,start.Y);var c4=Cross(wall.X1,wall.Y1,wall.X2,wall.Y2,end.X,end.Y);if(!((c1<=0&&c2>=0||c1>=0&&c2<=0)&&(c3<=0&&c4>=0||c3>=0&&c4<=0)))return false;
        if(wall.DoorStart>=0){var coordinate=Math.Abs(wall.X1-wall.X2)<.01?(start.Y+end.Y)/2:(start.X+end.X)/2;if(coordinate>=wall.DoorStart&&coordinate<=wall.DoorEnd)return false;}return true;
    }

    private static int StableInt(string value) => BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(value)), 0);
}

public sealed record HostileTick(IReadOnlyList<ActorState> Actors, IReadOnlyList<PlayerState> Players, IReadOnlyList<CombatEvent> Combat);
public sealed record CombatResult(CombatEvent Event, PlayerState Attacker, PlayerState? TargetPlayer, InventoryState Inventory, RelationshipState? Relationship, DungeonState? Dungeon);
