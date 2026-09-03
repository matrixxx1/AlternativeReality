using System.Collections.Concurrent;
using AlternateEarth.Geo;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly DeterministicWorldGenerator _generator;
    private readonly IWeatherProvider _weatherProvider;
    private readonly SqliteRealityStore _store;
    private readonly ConcurrentDictionary<string, CanonicalEntity> _realityEntities = new();
    private readonly ConcurrentDictionary<string, PlayerState> _players = new();
    private readonly ConcurrentDictionary<string, ActorState> _actors = new();
    private readonly ConcurrentDictionary<string, Queue<WorldPosition>> _actorRoutes = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextActorSpeech = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastMovement = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastChat = new();
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private readonly SemaphoreSlim _areaLoadLock = new(1, 1);
    private readonly SemaphoreSlim _basePurchaseLock = new(1, 1);
    private readonly Random _actorRandom;
    private GeographicDataset? _geographic;
    private readonly ConcurrentDictionary<string, CanonicalEntity> _baseEntities = new();
    private readonly ConcurrentDictionary<string, ElevationSample> _elevationSamples = new();
    private readonly ConcurrentDictionary<string, WorldBounds> _loadedAreas = new();
    private WorldBounds? _loadedBounds;
    private WorldNavigation? _navigation;

    public RealityWorld(RealityConfiguration configuration, DeterministicWorldGenerator generator, IWeatherProvider weatherProvider, SqliteRealityStore store)
    {
        Configuration = configuration;
        _generator = generator;
        _weatherProvider = weatherProvider;
        _store = store;
        _actorRandom = new Random(unchecked((int)configuration.Seed));
    }

    public RealityConfiguration Configuration { get; private set; }
    public bool IsInitialized { get; private set; }
    public int PlayerCount => _players.Count;
    public int BaseEntityCount => _baseEntities.Count + _actors.Count;
    public int RealityEntityCount => _realityEntities.Count;
    public string GeographicProvider => _geographic?.Provider ?? "not loaded";
    public WeatherState Weather { get; private set; } = WeatherState.Unavailable;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;
        foreach (var item in await _store.LoadItemConfigurationsAsync(Configuration.Id, cancellationToken))
            if (_itemConfigurations.TryGetValue(item.ItemType, out var defaults))
                _itemConfigurations[item.ItemType] = item with
                {
                    SpeedModifierMph = item.SpeedModifierMph ?? defaults.SpeedModifierMph,
                    VisibilityModifierMeters = item.VisibilityModifierMeters ?? defaults.VisibilityModifierMeters
                };
        _movementConfiguration = await _store.LoadMovementConfigurationAsync(Configuration.Id, cancellationToken) ?? DefaultMovementConfiguration;
        foreach (var entity in await _store.LoadActiveEntitiesAsync(Configuration.Id, cancellationToken))
            _realityEntities[entity.Id] = entity;
        ApplyGeneratedWorld(await _generator.GenerateAsync(Configuration, cancellationToken));
        _loadedAreas["0:0"] = Configuration.Area.Bounds;
        await RefreshWeatherAsync(cancellationToken);
        IsInitialized = true;
    }

    public async Task ConfigureInitialLocationAsync(GeoCoordinate center, CancellationToken cancellationToken = default)
    {
        if (center.Latitude is < -85 or > 85 || center.Longitude is < -180 or > 180) throw new InvalidOperationException("Enter valid latitude and longitude coordinates.");
        await _rebuildLock.WaitAsync(cancellationToken);
        try
        {
            if (IsInitialized || !_players.IsEmpty) throw new InvalidOperationException("This reality has already been initialized.");
            Configuration = Configuration with { Area = new GeographicArea(center, Configuration.Area.SizeMeters) };
            await _store.InitializeAsync(Configuration, cancellationToken);
            await InitializeAsync(cancellationToken);
        }
        finally { _rebuildLock.Release(); }
    }

    public async Task<bool> RefreshWeatherAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Weather = await _weatherProvider.GetCurrentAsync(Configuration.Area.Center, cancellationToken);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    public async Task<PlayerState> JoinAsync(string characterId, string requestedName, string? accountId = null, CancellationToken cancellationToken = default)
    {
        if (_players.Count >= Configuration.MaximumPlayers) throw new InvalidOperationException("This reality is full.");
        var name = SanitizeName(requestedName);
        var existing = await _store.LoadCharacterAsync(Configuration.Id, characterId, cancellationToken);
        var home = string.IsNullOrWhiteSpace(accountId) ? null : await EnsureHomeAsync(characterId, accountId, cancellationToken);
        var center = new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center);
        var newAccountSpawn = existing is null && !string.IsNullOrWhiteSpace(accountId) && await _store.IsFirstAccountCharacterAsync(accountId, characterId, cancellationToken);
        var startsAtHome = home is not null && !newAccountSpawn;
        var location = startsAtHome ? home!.Id : "outdoor";
        var position = startsAtHome ? home!.Exit : newAccountSpawn ? RandomOutdoorSpawn(characterId) : Navigation.FindNearestWalkable(center);
        position = startsAtHome ? position with { Z = 0 } : position with { Z = Navigation.ElevationAt(position.X, position.Y) };
        var health = existing is null || existing.HealthHearts <= 0 ? 10 : Math.Clamp(existing.HealthHearts, .25, 10);
        var player = new PlayerState(characterId, name, position, (existing?.Version ?? 0) + 1,
            startsAtHome ? TerrainType.Pavement : Navigation.TerrainAt(position.X, position.Y), 0, health, 10, startsAtHome ? TravelMode.Walk : existing?.TravelMode ?? TravelMode.Walk,
            Math.Clamp(existing?.Stamina ?? 10, 0, 10), 10,
            Math.Clamp(existing?.Water ?? 10, 0, 10), 10, existing?.WalletCents ?? 0, existing?.GodMode ?? false,
            existing?.FoodProtectedUntilUtc, existing?.WaterProtectedUntilUtc, location,
            existing?.FlashlightOn ?? false, existing?.LanternOn ?? false, existing?.LaserOn ?? false,
            existing?.MagicHikingShoesOn ?? false, existing?.MagicRunningShoesOn ?? false, existing?.HatOn ?? false,
            existing?.DirtBikeGasGallons ?? 0, existing?.MotorcycleGasGallons ?? 0, existing?.EquippedWeapon ?? "fist");
        if (player.MagicHikingShoesOn && player.MagicRunningShoesOn) player = player with { MagicRunningShoesOn = false };
        _players[characterId] = player;
        _lastMovement[characterId] = DateTimeOffset.UtcNow;
        _lastIdleHeal[characterId] = DateTimeOffset.UtcNow;
        var inventory = await _store.LoadInventoryAsync(characterId, cancellationToken);
        _inventories[characterId] = inventory.Items.ToDictionary(item => item.ItemType, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        foreach (var relationship in await _store.LoadRelationshipsAsync(Configuration.Id, characterId, cancellationToken))
            _relationships[(characterId, relationship.ActorId)] = relationship.FriendRating;
        foreach (var areaKey in await _store.LoadWorldMapDiscoveryAsync(Configuration.Id, characterId, cancellationToken))
            _revealedWorldAreas[(characterId, areaKey)] = 0;
        await _store.SaveCharacterAsync(Configuration.Id, player, cancellationToken);
        return player;
    }

    private async Task<DungeonState?> EnsureHomeAsync(string playerId, string accountId, CancellationToken cancellationToken)
    {
        _playerAccounts[playerId] = accountId;
        await _basePurchaseLock.WaitAsync(cancellationToken);
        try
        {
            var assignment = await _store.LoadBaseAssignmentAsync(accountId, Configuration.Id, cancellationToken);
            if (assignment?.Position is { } assignedPosition && assignedPosition.Region == Configuration.Area.Region && !_baseEntities.ContainsKey(assignment.BuildingId))
                await EnsureAreaLoadedAsync(assignedPosition.X, assignedPosition.Y, cancellationToken);
            var baseBuilding = assignment?.BuildingId;
            if (baseBuilding is null || !_baseEntities.ContainsKey(baseBuilding))
            {
                var buildings = _baseEntities.Values.Where(entity => entity.Kind == EntityKind.Building).OrderBy(entity => entity.Id).ToArray();
                if (buildings.Length == 0) return null;
                var assigned = await _store.LoadAssignedBaseBuildingsAsync(Configuration.Id, cancellationToken);
                var start = (StableInt(accountId) & int.MaxValue) % buildings.Length;
                var building = Enumerable.Range(0, buildings.Length).Select(offset => buildings[(start + offset) % buildings.Length]).FirstOrDefault(candidate => !assigned.Contains(candidate.Id)) ?? buildings[start];
                baseBuilding = building.Id;
                await _store.SaveBaseBuildingAsync(accountId, Configuration.Id, baseBuilding, building.Position, cancellationToken);
            }
            _baseBuildings[accountId] = baseBuilding;
            var baseEntity = _baseEntities[baseBuilding];
            await EnsureHomeFurnitureAsync(accountId, baseEntity, cancellationToken);
            var homeId = $"home:{accountId}:{baseBuilding}";
            var home = _dungeons.GetOrAdd(homeId, _ => GenerateHome(homeId, baseEntity));
            SetBaseReturnPosition(playerId, baseBuilding);
            return home;
        }
        finally { _basePurchaseLock.Release(); }
    }

    private WorldPosition RandomOutdoorSpawn(string characterId)
    {
        var loadedAreas = _loadedAreas.Values.ToArray();
        if (loadedAreas.Length == 0) loadedAreas = [Configuration.Area.Bounds];
        var random = new Random(StableInt($"spawn:{Configuration.Seed}:{characterId}"));
        for (var attempt = 0; attempt < 48; attempt++)
        {
            var bounds = loadedAreas[random.Next(loadedAreas.Length)];
            var marginX = Math.Min(12, Math.Max(0, (bounds.MaximumX - bounds.MinimumX) / 4));
            var marginY = Math.Min(12, Math.Max(0, (bounds.MaximumY - bounds.MinimumY) / 4));
            var candidate = new WorldPosition(Configuration.Area.Region,
                bounds.MinimumX + marginX + random.NextDouble() * Math.Max(1, bounds.MaximumX - bounds.MinimumX - marginX * 2),
                bounds.MinimumY + marginY + random.NextDouble() * Math.Max(1, bounds.MaximumY - bounds.MinimumY - marginY * 2));
            var safe = Navigation.FindNearestWalkable(candidate);
            if (loadedAreas.Any(area => area.Contains(safe.X, safe.Y)) && !Navigation.IsBlocked(safe.X, safe.Y) && Navigation.TerrainAt(safe.X, safe.Y) != TerrainType.DeepWater) return safe;
        }
        return Navigation.FindNearestWalkable(new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
    }

    private DungeonState? HomeForPlayer(string playerId)
    {
        if (!_playerAccounts.TryGetValue(playerId, out var accountId) || !_baseBuildings.TryGetValue(accountId, out var buildingId) || !_baseEntities.TryGetValue(buildingId, out var building)) return null;
        var homeId = $"home:{accountId}:{buildingId}";
        return _dungeons.GetOrAdd(homeId, _ => GenerateHome(homeId, building));
    }

    private void SetBaseReturnPosition(string playerId, string buildingId)
    {
        var door = _baseEntities.Values.FirstOrDefault(entity => entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == buildingId);
        if (door is not null) _returnPositions[playerId] = Navigation.FindNearestWalkable(door.Position);
    }

    public void Leave(string characterId)
    {
        _players.TryRemove(characterId, out _);
        _lastMovement.TryRemove(characterId, out _);
        _lastChat.TryRemove(characterId, out _);
        _lastIdleHeal.TryRemove(characterId, out _);
        _playerAccounts.TryRemove(characterId, out _);
    }

    public ChatMessage Say(string characterId, SayRequest request)
    {
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        var now = DateTimeOffset.UtcNow;
        if (_lastChat.TryGetValue(characterId, out var last) && now - last < TimeSpan.FromMilliseconds(500))
            throw new InvalidOperationException("Please wait a moment before saying something else.");
        var cleaned = new string((request.Message ?? string.Empty).Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (cleaned.Length == 0) throw new InvalidOperationException("Enter a message first.");
        if (cleaned.Length > 180) throw new InvalidOperationException("Chat messages are limited to 180 characters.");
        _lastChat[characterId] = now;
        return new ChatMessage($"chat:{Guid.NewGuid():N}", player.Id, player.Name, cleaned, now);
    }

    public async Task<MovementOutcome?> MoveAsync(string characterId, MoveRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) return null;
        if (player.LocationId != "outdoor") return await MoveInDungeonAsync(player, request, cancellationToken);
        var length = Math.Sqrt((request.X * request.X) + (request.Y * request.Y));
        var directionX = length > 1 ? request.X / length : request.X;
        var directionY = length > 1 ? request.Y / length : request.Y;
        var now = DateTimeOffset.UtcNow;
        var previous = _lastMovement.AddOrUpdate(characterId, now, (_, old) => now);
        var elapsed = Math.Clamp((now - previous).TotalSeconds, 0.01, 0.15);
        var currentTerrain = Navigation.TerrainAt(player.Position.X, player.Position.Y);
        if (!player.GodMode && IsMotorized(player.TravelMode) && FuelGallons(player) <= 0)
        {
            var stopped = player with { SpeedMetersPerSecond = 0, Version = player.Version + 1 };
            await SavePlayerAsync(stopped, cancellationToken);
            return new(stopped, false, true, false, false, false, $"Your {VehicleName(player.TravelMode)} is out of gas. Add gasoline before using it again.");
        }
        var staminaFraction = player.MaximumStamina <= 0 ? 0 : player.Stamina / player.MaximumStamina;
        var wearingMagicHikingShoes = player.MagicHikingShoesOn && (player.GodMode || InventoryQuantity(characterId, "magicHikingShoes") > 0);
        var wearingMagicRunningShoes = player.MagicRunningShoesOn && (player.GodMode || InventoryQuantity(characterId, "magicRunningShoes") > 0);
        var reducedStaminaDrain = wearingMagicHikingShoes || wearingMagicRunningShoes && WorldNavigation.MagicRunningShoesReduceStaminaOn(currentTerrain);
        var metersPerSecond = ConfiguredSpeedMetersPerSecond(player, currentTerrain, staminaFraction, wearingMagicHikingShoes, wearingMagicRunningShoes);
        var maximumStep = request.MaximumDistanceMeters is > 0 and < double.MaxValue ? request.MaximumDistanceMeters.Value : double.MaxValue;
        var step = Math.Min(metersPerSecond * elapsed * Configuration.GameSpeed, maximumStep);
        var requested = (_loadedBounds ?? Configuration.Area.Bounds).Clamp(player.Position with
        {
            X = player.Position.X + (directionX * step),
            Y = player.Position.Y + (directionY * step)
        });

        var requestedTerrain = Navigation.TerrainAt(requested.X, requested.Y);
        if (player.TravelMode == TravelMode.Skateboard && !WorldNavigation.SupportsTravelMode(requestedTerrain, TravelMode.Skateboard))
        {
            var damaged = player with { HealthHearts = player.GodMode ? Math.Max(1, player.HealthHearts - .25) : Math.Max(0, player.HealthHearts - .25), TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Terrain = currentTerrain, Version = player.Version + 1 };
            if (damaged.HealthHearts <= 0)
            {
                var reset = ResetPlayer(damaged);
                await SavePlayerAsync(reset, cancellationToken);
                return new(reset, true, false, false, true, true, "You fell, lost your final quarter-heart, and returned to the starting point.");
            }
            await SavePlayerAsync(damaged, cancellationToken);
            return new(damaged, true, false, false, true, false, "The skateboard left a paved surface. You fell and lost ¼ heart.");
        }

        var next = requested;
        var blocked = !Navigation.CanTraverse(player.Position, requested);
        if (blocked)
        {
            var slideX = requested with { Y = player.Position.Y };
            var slideY = requested with { X = player.Position.X };
            if (Navigation.CanTraverse(player.Position, slideX)) next = slideX;
            else if (Navigation.CanTraverse(player.Position, slideY)) next = slideY;
            else next = player.Position;
        }
        var nextTerrain = Navigation.TerrainAt(next.X, next.Y);
        if (nextTerrain == TerrainType.DeepWater && player.TravelMode != TravelMode.Raft && !player.GodMode)
        {
            var reset = ResetPlayer(player with { HealthHearts = 0 });
            await SavePlayerAsync(reset, cancellationToken);
            return new(reset, true, false, true, false, true, "You drowned, lost all hearts, and returned to the starting point.");
        }

        var distance = player.Position.Distance2D(next);
        var dirtBikeGas = player.DirtBikeGasGallons;
        var motorcycleGas = player.MotorcycleGasGallons;
        if (!player.GodMode && distance > .001)
        {
            if (player.TravelMode == TravelMode.DirtBike) dirtBikeGas = FuelAfterTravel(dirtBikeGas, distance, DirtBikeMilesPerGallon);
            if (player.TravelMode == TravelMode.Motorcycle) motorcycleGas = FuelAfterTravel(motorcycleGas, distance, MotorcycleMilesPerGallon);
        }
        var updated = player with
        {
            Position = next with { Z = Navigation.ElevationAt(next.X, next.Y) }, Terrain = nextTerrain,
            SpeedMetersPerSecond = distance > .001 ? distance / elapsed : 0,
            Stamina = player.TravelMode == TravelMode.Run && distance > .001 && !(player.FoodProtectedUntilUtc > now) ? Math.Max(0, player.Stamina - WorldNavigation.RunningStaminaDrain(elapsed, reducedStaminaDrain)) : player.Stamina,
            DirtBikeGasGallons = dirtBikeGas,
            MotorcycleGasGallons = motorcycleGas,
            Version = player.Version + 1
        };
        await SavePlayerAsync(updated, cancellationToken);
        return new(updated, distance > .001, blocked && distance <= .001, false, false, false, null);
    }

    public async Task<PlayerState> SetTravelModeAsync(string characterId, TravelMode mode, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (player.LocationId != "outdoor" && mode is TravelMode.Bike or TravelMode.DirtBike or TravelMode.Motorcycle)
            throw new InvalidOperationException("Bikes, dirt bikes, and motorcycles cannot be used inside a dungeon or Home.");
        if (!player.GodMode && mode == TravelMode.Skateboard && InventoryQuantity(characterId, "skateboard") <= 0) throw new InvalidOperationException("You need a skateboard in your inventory.");
        if (!player.GodMode && mode == TravelMode.Bike && InventoryQuantity(characterId, "bike") <= 0) throw new InvalidOperationException("You need a bike in your inventory.");
        if (!player.GodMode && mode == TravelMode.DirtBike && InventoryQuantity(characterId, "dirtBike") <= 0) throw new InvalidOperationException("You need a dirt bike in your inventory.");
        if (!player.GodMode && mode == TravelMode.Motorcycle && InventoryQuantity(characterId, "motorcycle") <= 0) throw new InvalidOperationException("You need a motorcycle in your inventory.");
        if (!player.GodMode && mode == TravelMode.DirtBike && player.DirtBikeGasGallons <= 0) throw new InvalidOperationException("Your dirt bike is out of gas. Use a gallon of gas while the dirt bike is selected.");
        if (!player.GodMode && mode == TravelMode.Motorcycle && player.MotorcycleGasGallons <= 0) throw new InvalidOperationException("Your motorcycle is out of gas. Use a gallon of gas while the motorcycle is selected.");
        if (mode == TravelMode.Raft)
        {
            if (!player.GodMode && InventoryQuantity(characterId, "inflatableRaft") <= 0) throw new InvalidOperationException("You need an inflatable raft.");
            if (player.LocationId == "outdoor" && player.Terrain != TerrainType.ShallowWater && player.TravelMode != TravelMode.Raft) throw new InvalidOperationException("A raft can only be deployed from shallow water.");
        }
        if (player.TravelMode == TravelMode.Raft && mode != TravelMode.Raft && player.Terrain == TerrainType.DeepWater && !player.GodMode)
        {
            var drowned = ResetPlayer(player with { HealthHearts = 0 });
            await SavePlayerAsync(drowned, cancellationToken);
            return drowned;
        }
        var updated = player with { TravelMode = mode, SpeedMetersPerSecond = 0, Version = player.Version + 1 };
        await SavePlayerAsync(updated, cancellationToken);
        return updated;
    }

    public Task<IReadOnlyList<PlayerState>> AdvanceStaminaAsync(TimeSpan elapsed, CancellationToken cancellationToken = default) =>
        AdvanceVitalsAsync(elapsed, cancellationToken);

    public async Task<(PlayerState Player, bool Expanded)> TeleportWithAreaAsync(string characterId, TeleportRequest request, CancellationToken cancellationToken = default)
    {
        if (!playerIsGod(characterId)) throw new InvalidOperationException("God Mode must be enabled to teleport.");
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (player.LocationId != "outdoor") throw new InvalidOperationException("Leave the dungeon or Home before teleporting.");
        var expanded = await EnsureAreaLoadedAsync(request.X, request.Y, cancellationToken);
        var requested = (_loadedBounds ?? Configuration.Area.Bounds).Clamp(player.Position with { X = request.X, Y = request.Y });
        var destination = Navigation.IsBlocked(requested.X, requested.Y) || Navigation.TerrainAt(requested.X, requested.Y) == TerrainType.DeepWater
            ? Navigation.FindNearestWalkable(requested)
            : requested with { Z = Navigation.ElevationAt(requested.X, requested.Y) };
        var updated = player with
        {
            Position = destination,
            Terrain = Navigation.TerrainAt(destination.X, destination.Y),
            SpeedMetersPerSecond = 0,
            Version = player.Version + 1
        };
        _lastMovement[characterId] = DateTimeOffset.UtcNow;
        await SavePlayerAsync(updated, cancellationToken);
        return (updated, expanded);
    }

    public async Task<PlayerState> TeleportAsync(string characterId, TeleportRequest request, CancellationToken cancellationToken = default) =>
        (await TeleportWithAreaAsync(characterId, request, cancellationToken)).Player;

    public async Task<(NavigationResult Result, bool Expanded)> FindPathAsync(string characterId, PathRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) return (new(false, Array.Empty<WorldPosition>(), "Unknown player."), false);
        if (player.LocationId != "outdoor" && _dungeons.TryGetValue(player.LocationId, out var dungeon))
        {
            if (request.X < .5 || request.Y < .5 || request.X > dungeon.Width - .5 || request.Y > dungeon.Height - .5) return (new(false, Array.Empty<WorldPosition>(), dungeon.IsHome ? "That point is outside Home." : "That point is outside the dungeon."), false);
            var target = player.Position with { X = request.X, Y = request.Y, Z = 0 };
            if (dungeon.Walls.Any(wall => CrossesDungeonWall(player.Position, target, wall))) return (new(false, Array.Empty<WorldPosition>(), dungeon.IsHome ? "A wall in Home blocks that route. Move through a doorway." : "A dungeon wall blocks that route. Move through a doorway."), false);
            return (new(true, new[] { target }), false);
        }
        var expanded = await EnsureAreaLoadedAsync(request.X, request.Y, cancellationToken);
        return (Navigation.FindPath(player.Position, request.X, request.Y, terrain => ConfiguredSpeedMetersPerSecond(player, terrain)), expanded);
    }

    public IReadOnlyList<ActorState> AdvanceActors(TimeSpan elapsed)
    {
        var changed = new List<ActorState>();
        lock (_actorRandom)
        {
            foreach (var pair in _actors)
            {
                var actor = pair.Value;
                if (!_actorRoutes.TryGetValue(actor.Id, out var route) || route.Count == 0)
                {
                    route = CreateActorRoute(actor);
                    _actorRoutes[actor.Id] = route;
                }
                if (route.Count == 0) { _actors[actor.Id] = actor with { IsMoving = false }; continue; }
                var waypoint = route.Peek();
                var dx = waypoint.X - actor.Position.X;
                var dy = waypoint.Y - actor.Position.Y;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance < .35) { route.Dequeue(); continue; }
                var step = Math.Min(distance, ActorSpeed(actor.Subtype) * elapsed.TotalSeconds);
                var position = actor.Position with { X = actor.Position.X + (dx / distance * step), Y = actor.Position.Y + (dy / distance * step) };
                if (!Navigation.CanTraverse(actor.Position, position, true)) { route.Clear(); continue; }
                position = position with { Z = Navigation.ElevationAt(position.X, position.Y) };
                var facing = Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? "east" : "west") : (dy > 0 ? "north" : "south");
                var updated = actor with { Position = position, Facing = facing, IsMoving = true, Version = actor.Version + 1 };
                _actors[actor.Id] = updated;
                changed.Add(updated);
            }
        }
        return changed;
    }

    public IReadOnlyList<ChatMessage> AdvanceActorSpeech(DateTimeOffset now)
    {
        var messages = new List<ChatMessage>();
        lock (_actorRandom)
        {
            foreach (var actor in _actors.Values)
            {
                if (!_nextActorSpeech.TryGetValue(actor.Id, out var next))
                {
                    _nextActorSpeech[actor.Id] = now.AddSeconds(_actorRandom.Next(10, 61));
                    continue;
                }
                if (now < next) continue;
                var line = ActorSpeech(actor);
                messages.Add(new ChatMessage($"chat:{Guid.NewGuid():N}", actor.Id, ActorDisplayName(actor), line, now));
                _nextActorSpeech[actor.Id] = now.AddMinutes(2 + (_actorRandom.NextDouble() * 28));
            }
        }
        return messages;
    }

    public async Task<WorldSnapshot> RebuildAsync(string characterId, bool godMode, CancellationToken cancellationToken = default)
    {
        if (!playerIsGod(characterId)) throw new InvalidOperationException("God Mode must be enabled to rebuild this reality.");
        await _rebuildLock.WaitAsync(cancellationToken);
        try
        {
            await _store.ClearTransientWorldStateAsync(Configuration.Id, cancellationToken);
            _realityEntities.Clear();
            _baseEntities.Clear(); _elevationSamples.Clear(); _loadedAreas.Clear(); _actors.Clear(); _actorRoutes.Clear(); _nextActorSpeech.Clear(); _outdoorChests.Clear(); _loot.Clear(); _dungeons.Clear(); _returnPositions.Clear(); _relationships.Clear(); _tradeQuotes.Clear(); _loadedBounds = null; _geographic = null;
            ApplyGeneratedWorld(await _generator.GenerateAsync(Configuration, cancellationToken));
            _loadedAreas["0:0"] = Configuration.Area.Bounds;
            _baseBuildings.Clear();
            foreach (var pair in _players.ToArray())
            {
                if (_playerAccounts.TryGetValue(pair.Key, out var accountId)) await EnsureHomeAsync(pair.Key, accountId, cancellationToken);
                await SavePlayerAsync(ResetPlayer(pair.Value), cancellationToken);
            }
            return CreateSnapshot();
        }
        finally { _rebuildLock.Release(); }
    }

    public async Task<CanonicalEntity> PlaceObjectAsync(string characterId, PlaceObjectRequest request, CancellationToken cancellationToken = default)
    {
        if (!Configuration.ObjectPlacementEnabled) throw new InvalidOperationException("Object placement is disabled in this exploration-only reality.");
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        var requestedPosition = new WorldPosition(player.Position.Region, request.X, request.Y, player.Position.Z);
        if (player.Position.Distance2D(requestedPosition) > 5.0) throw new InvalidOperationException("Objects must be placed within five meters of the character.");
        if (!(_loadedBounds ?? Configuration.Area.Bounds).Contains(request.X, request.Y)) throw new InvalidOperationException("Object is outside the loaded world.");
        var snapped = requestedPosition with { X = Math.Round(request.X * 2) / 2.0, Y = Math.Round(request.Y * 2) / 2.0 };
        var type = string.IsNullOrWhiteSpace(request.ObjectType) ? "marker" : request.ObjectType[..Math.Min(request.ObjectType.Length, 32)];
        var entity = new CanonicalEntity($"placed:{Guid.NewGuid():N}", EntityKind.PlayerStructure, snapped, Array.Empty<GeometryPoint>(),
            new Dictionary<string, string> { ["objectType"] = type, ["rotationDegrees"] = request.RotationDegrees.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), ["owner"] = characterId }, IsBaseEntity: false);
        await _store.SaveEntityAsync(Configuration.Id, entity, cancellationToken);
        _realityEntities[entity.Id] = entity;
        return entity;
    }

    public async Task<CanonicalEntity> RemoveObjectAsync(string characterId, string entityId, CancellationToken cancellationToken = default)
    {
        if (!Configuration.ObjectPlacementEnabled) throw new InvalidOperationException("Object modification is disabled in this exploration-only reality.");
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (!_realityEntities.TryGetValue(entityId, out var entity)) throw new InvalidOperationException("Object does not exist or is part of the immutable base geography.");
        if (player.Position.Distance2D(entity.Position) > 5.0) throw new InvalidOperationException("Objects must be removed from within five meters.");
        if (!Configuration.BuildingDestruction) throw new InvalidOperationException("Object destruction is disabled in this reality.");
        await _store.RemoveEntityAsync(Configuration.Id, entity, cancellationToken);
        _realityEntities.TryRemove(entityId, out _);
        return entity;
    }

    public WorldSnapshot CreateSnapshot() => new(Configuration, _loadedBounds ?? Configuration.Area.Bounds,
        _baseEntities.Values.OrderBy(entity => entity.Id).ToArray(), _realityEntities.Values.OrderBy(entity => entity.Id).ToArray(),
        _players.Values.OrderBy(player => player.Id).ToArray(), _elevationSamples.Values.ToArray(),
        Weather, _actors.Values.OrderBy(actor => actor.Id).ToArray(), _loadedAreas.Values.OrderBy(area => area.MinimumX).ThenBy(area => area.MinimumY).ToArray());

    private void ApplyGeneratedWorld(GeographicDataset generated)
    {
        var actorEntities = generated.Features.Where(entity => entity.Kind is EntityKind.Animal or EntityKind.Npc).ToArray();
        var staticEntities = generated.Features.Where(entity => entity.Kind is not (EntityKind.Animal or EntityKind.Npc)).ToArray();
        _geographic ??= generated with { Features = staticEntities };
        foreach (var entity in staticEntities) _baseEntities[entity.Id] = entity;
        foreach (var sample in generated.Elevation) _elevationSamples[$"{sample.X:F1}:{sample.Y:F1}"] = sample;
        var bounds = generated.Area.Bounds;
        _loadedBounds = _loadedBounds is null ? bounds : new WorldBounds(Math.Min(_loadedBounds.MinimumX, bounds.MinimumX), Math.Min(_loadedBounds.MinimumY, bounds.MinimumY), Math.Max(_loadedBounds.MaximumX, bounds.MaximumX), Math.Max(_loadedBounds.MaximumY, bounds.MaximumY));
        _navigation = new WorldNavigation(_loadedBounds, _baseEntities.Values.Concat(_realityEntities.Values).ToArray(), _elevationSamples.Values.ToArray());
        var chestRandom = new Random(StableInt($"chests:{Configuration.Seed}:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}"));
        for (var chestIndex = 0; chestIndex < 2; chestIndex++)
        {
            var candidate = new WorldPosition(generated.Area.Region,
                bounds.MinimumX + chestRandom.NextDouble() * (bounds.MaximumX - bounds.MinimumX),
                bounds.MinimumY + chestRandom.NextDouble() * (bounds.MaximumY - bounds.MinimumY));
            var safe = Navigation.FindNearestWalkable(candidate);
            var id = $"chest:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}:{chestIndex}";
            _outdoorChests.TryAdd(id, new TreasureChestState(id, safe, "outdoor"));
        }
        foreach (var entity in actorEntities)
        {
            var safe = Navigation.FindNearestWalkable(entity.Position);
            var identity = StableInt(entity.Id) & int.MaxValue;
            var merchantCategory = entity.Properties.GetValueOrDefault("merchantCategory");
            var merchant = entity.Kind == EntityKind.Npc && (merchantCategory is not null || identity % 4 == 0);
            var maximumHealth = entity.Kind == EntityKind.Animal && entity.Properties.GetValueOrDefault("subtype") is "bear" or "cougar" ? 8 : 5;
            var travel = entity.Kind == EntityKind.Npc ? (TravelMode)(identity % 10 == 0 ? 3 : identity % 8 == 0 ? 2 : 0) : TravelMode.Walk;
            _actors[entity.Id] = new ActorState(entity.Id, entity.Kind, entity.Properties.GetValueOrDefault("subtype") ?? "unknown",
                entity.Properties.GetValueOrDefault("name") ?? "Wanderer", safe, HealthHearts: maximumHealth, MaximumHealthHearts: maximumHealth,
                IsMerchant: merchant, TravelMode: travel, MerchantCategory: merchantCategory);
        }
    }

    private async Task<bool> EnsureAreaLoadedAsync(double x, double y, CancellationToken cancellationToken)
    {
        var origin = new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center);
        var size = Configuration.Area.SizeMeters;
        var cellX = (int)Math.Floor((x - (origin.X - size / 2d)) / size);
        var cellY = (int)Math.Floor((y - (origin.Y - size / 2d)) / size);
        var key = $"{cellX}:{cellY}";
        if (_loadedAreas.ContainsKey(key)) return false;
        await _areaLoadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loadedAreas.ContainsKey(key)) return false;
            var centerPosition = new WorldPosition(Configuration.Area.Region, origin.X + cellX * size, origin.Y + cellY * size);
            var center = new LocalTangentProjection(Configuration.Area.Region).Unproject(centerPosition);
            if (RegionId.FromGeo(center) != Configuration.Area.Region) throw new InvalidOperationException("This prototype reached a geographic projection boundary. Cross-region Earth streaming is the next world-scale milestone.");
            var areaConfiguration = Configuration with { Area = new GeographicArea(center, size) };
            var generated = await _generator.GenerateAsync(areaConfiguration, cancellationToken);
            ApplyGeneratedWorld(generated);
            _loadedAreas[key] = generated.Area.Bounds;
            return true;
        }
        finally { _areaLoadLock.Release(); }
    }

    public Task<bool> LoadAreaAsync(double x,double y,CancellationToken cancellationToken=default)=>EnsureAreaLoadedAsync(x,y,cancellationToken);
    public bool IsAreaLoaded(double x, double y) => _loadedAreas.ContainsKey(AreaKeyFor(x, y));

    public string AreaKeyFor(double x, double y)
    {
        var origin = new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center);
        var size = Configuration.Area.SizeMeters;
        var cellX = (int)Math.Floor((x - (origin.X - size / 2d)) / size);
        var cellY = (int)Math.Floor((y - (origin.Y - size / 2d)) / size);
        return $"{cellX}:{cellY}";
    }

    private Queue<WorldPosition> CreateActorRoute(ActorState actor)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var distance = 6 + (_actorRandom.NextDouble() * 24);
            var angle = _actorRandom.NextDouble() * Math.PI * 2;
            var path = Navigation.FindPath(actor.Position, actor.Position.X + (Math.Cos(angle) * distance), actor.Position.Y + (Math.Sin(angle) * distance));
            if (path.Success) return new Queue<WorldPosition>(path.Waypoints);
        }
        return new Queue<WorldPosition>();
    }

    private static double ActorSpeed(string subtype) => subtype switch
    {
        "rabbit" => 2.2, "dog" => 1.8, "cat" => 1.4, "bird" => 2.6,
        "deer" => 2.0, "cougar" => 1.7, "bear" => 1.2, _ => 1.25
    };

    private string ActorSpeech(ActorState actor)
    {
        if (actor.Kind == EntityKind.Npc)
        {
            if (actor.IsMerchant)
            {
                var offers = BaseMerchantOffers(actor);
                var offer = offers[_actorRandom.Next(offers.Length)];
                var displayName = offer.DisplayName ?? (_itemConfigurations.TryGetValue(offer.ItemType, out var good) ? good.DisplayName : offer.ItemType);
                return $"For sale! {displayName} for ${offer.UnitPriceCents / 100.0:F2} today. Friends pay less!";
            }
            var jokes = new[]
            {
                "Why did the scarecrow win an award? It was outstanding in its field!",
                "I tried to catch some fog earlier. I mist.",
                "Why don't skeletons fight each other? They don't have the guts.",
                "Two parallel lines have so much in common. It's a shame they'll never meet.",
                "I know a great map joke, but you had to be there.",
                "Why was the bicycle tired? It was two-tired.",
                "The shovel was a groundbreaking invention.",
                "What do you call a bear with no teeth? A gummy bear!"
            };
            return jokes[_actorRandom.Next(jokes.Length)];
        }
        return actor.Subtype switch
        {
            "bird" => _actorRandom.Next(2) == 0 ? "squeak!" : "kaaaw!",
            "cat" => "meow!",
            "dog" => "bark!",
            "rabbit" => "sniff sniff",
            "deer" => "snort!",
            "cougar" => "growl...",
            "bear" => "grrr...",
            _ => "..."
        };
    }

    private static string ActorDisplayName(ActorState actor) => actor.Kind == EntityKind.Npc
        ? actor.Name
        : char.ToUpperInvariant(actor.Subtype[0]) + actor.Subtype[1..];

    private const double MetersPerMile = 1609.344;
    private const double DirtBikeMilesPerGallon = 50;
    private const double MotorcycleMilesPerGallon = 45;
    private static bool IsMotorized(TravelMode mode) => mode is TravelMode.DirtBike or TravelMode.Motorcycle;
    private static double FuelGallons(PlayerState player) => player.TravelMode == TravelMode.DirtBike ? player.DirtBikeGasGallons : player.MotorcycleGasGallons;
    private static double FuelAfterTravel(double gallons, double meters, double milesPerGallon) => Math.Max(0, gallons - meters / MetersPerMile / milesPerGallon);
    private static string VehicleName(TravelMode mode) => mode == TravelMode.DirtBike ? "dirt bike" : "motorcycle";

    private PlayerState ResetPlayer(PlayerState player)
    {
        var home = HomeForPlayer(player.Id);
        if (home is not null)
        {
            SetBaseReturnPosition(player.Id, home.BuildingId);
            return player with { Position = home.Exit, Terrain = TerrainType.Pavement, SpeedMetersPerSecond = 0,
                HealthHearts = 10, Stamina = 10, Water = 10, TravelMode = TravelMode.Walk, LocationId = home.Id,
                FoodProtectedUntilUtc = null, WaterProtectedUntilUtc = null, Version = player.Version + 1 };
        }
        var spawn = Navigation.FindNearestWalkable(new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
        return player with { Position = spawn, Terrain = Navigation.TerrainAt(spawn.X, spawn.Y), SpeedMetersPerSecond = 0,
            HealthHearts = 10, Stamina = 10, Water = 10, TravelMode = TravelMode.Walk, LocationId = "outdoor",
            FoodProtectedUntilUtc = null, WaterProtectedUntilUtc = null, Version = player.Version + 1 };
    }

    private async Task SavePlayerAsync(PlayerState player, CancellationToken cancellationToken)
    {
        _players[player.Id] = player;
        await _store.SaveCharacterAsync(Configuration.Id, player, cancellationToken);
    }

    private WorldNavigation Navigation => _navigation ?? throw new InvalidOperationException("World navigation is not initialized.");

    private static string SanitizeName(string value)
    {
        var cleaned = new string((value ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Explorer" : cleaned[..Math.Min(cleaned.Length, 24)];
    }
}

public sealed record MovementOutcome(PlayerState Player, bool Moved, bool Blocked, bool Drowned, bool Fell, bool Died, string? Message);
