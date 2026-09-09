using System.Collections.Concurrent;
using System.Diagnostics;
using AlternateEarth.Geo;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static readonly Func<TerrainType, bool> RaftTerrainOnly =
        terrain => WorldNavigation.SupportsTravelMode(terrain, TravelMode.Raft);
    private readonly DeterministicWorldGenerator _generator;
    private readonly IWeatherProvider _weatherProvider;
    private readonly SqliteRealityStore _store;
    private readonly ConcurrentDictionary<string, CanonicalEntity> _realityEntities = new();
    private readonly ConcurrentDictionary<string, PlayerState> _players = new();
    private readonly ConcurrentDictionary<string, ActorState> _actors = new();
    private readonly ConcurrentDictionary<string, Queue<WorldPosition>> _actorRoutes = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextActorSpeech = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastMovement = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRaftDrift = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastChat = new();
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private readonly SemaphoreSlim _areaLoadLock = new(1, 1);
    private readonly SemaphoreSlim _areaPrefetchLock = new(1, 1);
    private readonly SemaphoreSlim _basePurchaseLock = new(1, 1);
    private readonly SemaphoreSlim _flagPlacementLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _playerSaveLocks = new();
    private readonly Random _actorRandom;
    private GeographicDataset? _geographic;
    private readonly ConcurrentDictionary<string, CanonicalEntity> _baseEntities = new();
    private readonly ConcurrentDictionary<string, byte> _removedBaseEntityIds = new();
    private readonly ConcurrentDictionary<string, PublicBaseClaim> _publicBaseClaims = new();
    private readonly ConcurrentDictionary<string, string> _homeNotices = new();
    private readonly ConcurrentDictionary<string, ElevationSample> _elevationSamples = new();
    private readonly ConcurrentDictionary<string, WorldBounds> _loadedAreas = new();
    private WorldBounds? _loadedBounds;
    private WorldNavigation? _navigation;
    private long? _lastUfoCycle;
    private long? _lastTrexCycle;
    private long? _lastEventBearCycle;
    private long? _lastBrontosaurusCycle;
    private long? _lastStegosaurusCycle;
    private long? _lastRaptorCycle;
    private long? _lastLandOfGiantsCycle;
    private readonly ConcurrentDictionary<string, byte> _ufoHits = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _eventAttackCooldowns = new();
    private readonly ConcurrentDictionary<string, string> _activeMapOperations = new();
    private long _lastAreaLoadMilliseconds;
    private long _lastAreaPrefetchMilliseconds;
    private int _preparedAreaCount;

    public RealityWorld(RealityConfiguration configuration, DeterministicWorldGenerator generator, IWeatherProvider weatherProvider, SqliteRealityStore store, TimeProvider? timeProvider = null)
    {
        Configuration = configuration;
        _generator = generator;
        _weatherProvider = weatherProvider;
        _store = store;
        _probulatorClock = timeProvider ?? TimeProvider.System;
        _actorRandom = new Random(unchecked((int)configuration.Seed));
    }

    public RealityConfiguration Configuration { get; private set; }
    public bool IsInitialized { get; private set; }
    public int PlayerCount => _players.Count;
    public IReadOnlySet<string> ActiveAccountIds => _playerAccounts.Values.ToHashSet(StringComparer.Ordinal);
    public int BaseEntityCount => _baseEntities.Count + _actors.Count;
    public int RealityEntityCount => _realityEntities.Count;
    public int LoadedAreaCount => _loadedAreas.Count;
    public int ActorCount => _actors.Count;
    public int ElevationSampleCount => _elevationSamples.Count;
    public int PreparedAreaCount => Volatile.Read(ref _preparedAreaCount);
    public IReadOnlyList<string> ActiveMapOperations => _activeMapOperations.OrderBy(item => item.Key).Select(item => item.Value).ToArray();
    public string ActiveMapOperation => ActiveMapOperations.FirstOrDefault() ?? "Idle";
    public long LastAreaLoadMilliseconds => Interlocked.Read(ref _lastAreaLoadMilliseconds);
    public long LastAreaPrefetchMilliseconds => Interlocked.Read(ref _lastAreaPrefetchMilliseconds);
    public string GeographicProvider => _geographic?.Provider ?? "not loaded";
    public WeatherState Weather { get; private set; } = EnsureAmbientWind(WeatherState.Unavailable);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;
        await ParkExistingVehiclesAsync(cancellationToken);
        foreach (var impression in await _store.LoadFirstImpressionsAsync(Configuration.Id, cancellationToken))
            _firstImpressions[(impression.CharacterName, impression.NpcName)] = impression;
        foreach (var item in await _store.LoadItemConfigurationsAsync(Configuration.Id, cancellationToken))
            if (_itemConfigurations.TryGetValue(item.ItemType, out var defaults))
                _itemConfigurations[item.ItemType] = item with
                {
                    SpeedModifierMph = item.ItemType.Equals("ufo", StringComparison.OrdinalIgnoreCase) && Math.Abs((item.SpeedModifierMph ?? 56.5) - 56.5) < .001
                        ? defaults.SpeedModifierMph
                        : item.SpeedModifierMph ?? defaults.SpeedModifierMph,
                    RangeMeters = item.ItemType.Equals("probulator", StringComparison.OrdinalIgnoreCase) && (Math.Abs(item.RangeMeters - 100) < .001 || Math.Abs(item.RangeMeters - 8) < .001)
                        ? defaults.RangeMeters
                        : item.RangeMeters,
                    Effect = item.ItemType.Equals("ufo", StringComparison.OrdinalIgnoreCase) || item.ItemType.Equals("probulator", StringComparison.OrdinalIgnoreCase) ? defaults.Effect : item.Effect,
                    VisibilityModifierMeters = item.VisibilityModifierMeters ?? defaults.VisibilityModifierMeters,
                    WeightPounds = defaults.WeightPounds,
                    Category = defaults.Category,
                    CarriedInBackpack = defaults.CarriedInBackpack
                };
        _movementConfiguration = await _store.LoadMovementConfigurationAsync(Configuration.Id, cancellationToken) ?? DefaultMovementConfiguration;
        var storedEvents = await _store.LoadServerEventConfigurationAsync(Configuration.Id, cancellationToken) ?? DefaultEventConfiguration;
        var storedTimeMode = storedEvents.ServerTimeMode.Trim().ToLowerInvariant();
        if (storedTimeMode is not ("auto" or "manual")) storedTimeMode = "auto";
        // Configurations written before explicit clock modes used a non-zero offset as a manual clock.
        if (storedTimeMode == "auto" && storedEvents.ServerTimeOffsetMinutes != 0) storedTimeMode = "manual";
        _eventConfiguration = storedEvents with { ServerTimeMode = storedTimeMode, ServerUtcOffsetMinutes = 0 };
        ResetScheduledEventCycles(_probulatorClock.GetUtcNow());
        foreach (var entity in await _store.LoadActiveEntitiesAsync(Configuration.Id, cancellationToken))
            _realityEntities[entity.Id] = entity;
        foreach (var entityId in await _store.LoadRemovedEntityIdsAsync(Configuration.Id, cancellationToken))
            _removedBaseEntityIds[entityId] = 0;
        foreach (var loot in await _store.LoadPersistentLootAsync(Configuration.Id, cancellationToken))
            if (loot.ExpiresAtUtc > DateTimeOffset.UtcNow) { _loot[loot.Id] = loot; RestorePhotographs(loot.Items); }
        await _store.ReleaseExpiredBaseClaimsAsync(Configuration.Id, DateTimeOffset.UtcNow, cancellationToken);
        foreach (var claim in await _store.LoadPublicBaseClaimsAsync(Configuration.Id, cancellationToken))
            _publicBaseClaims[claim.BuildingId] = claim;
        ApplyGeneratedWorld(await _generator.GenerateAsync(Configuration, cancellationToken));
        _loadedAreas["0:0"] = Configuration.Area.Bounds;
        await AdvanceTransitAsync(TimeSpan.Zero, cancellationToken);
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
        if (!_eventConfiguration.WeatherMode.Equals("live", StringComparison.OrdinalIgnoreCase))
        {
            Weather = CreateConfiguredWeather(_eventConfiguration.WeatherMode, _eventConfiguration.TemperatureCelsius);
            Weather = EnsureAmbientWind(Weather);
            return true;
        }
        try
        {
            Weather = await _weatherProvider.GetCurrentAsync(Configuration.Area.Center, cancellationToken);
            Weather = EnsureAmbientWind(Weather);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    private static WeatherState EnsureAmbientWind(WeatherState weather)
    {
        if (!weather.IsAvailable || !double.IsFinite(weather.WindSpeedKilometersPerHour) || weather.WindSpeedKilometersPerHour <= 0)
            return weather with { WindSpeedKilometersPerHour = 2 + Random.Shared.NextDouble() * 4, WindDirectionDegrees = Random.Shared.NextDouble() * 360 };
        var direction = double.IsFinite(weather.WindDirectionDegrees) ? weather.WindDirectionDegrees : Random.Shared.NextDouble() * 360;
        return weather with { WindDirectionDegrees = (direction % 360 + 360) % 360 };
    }

    private WeatherState CreateConfiguredWeather(string mode, double? temperatureCelsius)
    {
        var now = CurrentServerTime;
        var profile = mode.ToLowerInvariant() switch
        {
            "clear" => ("Clear", 0, 0d, 8d),
            "rain" => ("Rain", 61, 2.5d, 18d),
            "snow" => ("Snow", 71, 1.8d, 12d),
            "fog" => ("Fog", 45, .1d, 4d),
            "storm" => ("Thunderstorm", 95, 6d, 35d),
            _ => throw new InvalidOperationException("Weather mode must be live, clear, rain, snow, fog, or storm.")
        };
        var hour = now.Hour + now.Minute / 60d;
        var isDay = hour is >= 7 and < 19;
        var date = now.Date;
        var sunrise = new DateTimeOffset(date.AddHours(7), now.Offset).ToUniversalTime();
        var sunset = new DateTimeOffset(date.AddHours(19), now.Offset).ToUniversalTime();
        return new WeatherState(profile.Item1, profile.Item2, temperatureCelsius ?? (mode.Equals("snow", StringComparison.OrdinalIgnoreCase) ? -3 : 18), profile.Item3, profile.Item4, isDay, DateTimeOffset.UtcNow, "server override", sunrise, sunset, Weather.MoonPhase, Weather.MoonIllumination, WindDirectionDegrees: Weather.WindDirectionDegrees);
    }

    private static int ServerUtcOffsetMinutes(DateTimeOffset utcNow) =>
        (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(utcNow).TotalMinutes);

    public DateTimeOffset CurrentServerTime
    {
        get
        {
            var utcNow = _probulatorClock.GetUtcNow();
            var manualOffset = _eventConfiguration.ServerTimeMode.Equals("manual", StringComparison.OrdinalIgnoreCase)
                ? _eventConfiguration.ServerTimeOffsetMinutes
                : 0;
            return utcNow.AddMinutes(ServerUtcOffsetMinutes(utcNow) + manualOffset);
        }
    }

    public async Task<PlayerState> JoinAsync(string characterId, string requestedName, string? accountId = null, CancellationToken cancellationToken = default)
    {
        if (_players.Count >= Configuration.MaximumPlayers) throw new InvalidOperationException("This reality is full.");
        var name = SanitizeName(requestedName);
        var existing = await _store.LoadCharacterAsync(Configuration.Id, characterId, cancellationToken);
        _progression[characterId] = await _store.LoadProgressionAsync(Configuration.Id, characterId, cancellationToken) ?? NewProgression;
        var maximumStamina = ProgressionRules.Stamina(StatsFor(characterId));
        if (!string.IsNullOrWhiteSpace(accountId) && await _store.RefreshBaseActivityAsync(accountId, Configuration.Id, DateTimeOffset.UtcNow, cancellationToken))
        {
            foreach (var stale in _publicBaseClaims.Where(pair => pair.Value.AccountId == accountId).Select(pair => pair.Key).ToArray()) _publicBaseClaims.TryRemove(stale, out _);
            _baseBuildings.TryRemove(accountId, out _);
            _homeNotices[characterId] = "Your previous Home was released after 30 days without a login. Your stored furniture and Home inventory were preserved, and the server assigned you a new Home at no charge.";
        }
        var home = string.IsNullOrWhiteSpace(accountId) ? null : await EnsureHomeAsync(characterId, accountId, cancellationToken, name);
        var center = new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center);
        var newAccountSpawn = existing is null && !string.IsNullOrWhiteSpace(accountId) && await _store.IsFirstAccountCharacterAsync(accountId, characterId, cancellationToken);
        var resumesOutdoors = existing is not null && existing.LocationId == "outdoor" && existing.Position.Region == Configuration.Area.Region;
        var resumesInterior = existing is not null && existing.LocationId != "outdoor" && _dungeons.ContainsKey(existing.LocationId);
        if (resumesOutdoors && !_loadedAreas.Values.Any(area => area.Contains(existing!.Position.X, existing.Position.Y)))
            await EnsureAreaLoadedAsync(existing!.Position.X, existing.Position.Y, cancellationToken);
        var location = resumesInterior ? existing!.LocationId : home is not null && !newAccountSpawn && !resumesOutdoors ? home.Id : "outdoor";
        var inside = location != "outdoor";
        var position = resumesInterior
            ? InteriorPositionIsSafe(existing!.Position, _dungeons[existing.LocationId]) ? existing.Position : _dungeons[existing.LocationId].Exit
            : location == home?.Id ? home.Exit : newAccountSpawn ? InitialBaseSpawn(characterId, home) : resumesOutdoors ? existing!.Position : Navigation.FindNearestWalkable(center);
        position = inside ? position with { Z = 0 } : position with { Z = Navigation.ElevationAt(position.X, position.Y) };
        var health = existing is null || existing.HealthHearts <= 0 ? 10 : Math.Clamp(existing.HealthHearts, .25, 10);
        var player = new PlayerState(characterId, name, position, (existing?.Version ?? 0) + 1,
            inside ? TerrainType.Pavement : Navigation.TerrainAt(position.X, position.Y), 0, health, 10, inside ? TravelMode.Walk : existing?.TravelMode ?? TravelMode.Walk,
            Math.Clamp(existing?.Stamina ?? maximumStamina, 0, maximumStamina), maximumStamina,
            Math.Clamp(existing?.Water ?? 10, 0, 10), 10, existing?.WalletCents ?? 0, existing?.GodMode ?? false,
            existing?.FoodProtectedUntilUtc, existing?.WaterProtectedUntilUtc, location,
            existing?.FlashlightOn ?? false, existing?.LanternOn ?? false, existing?.LaserOn ?? false,
            existing?.MagicHikingShoesOn ?? false, existing?.MagicRunningShoesOn ?? false, existing?.HatOn ?? false,
            existing?.DirtBikeGasGallons ?? 0, existing?.MotorcycleGasGallons ?? 0, existing?.EquippedWeapon ?? "fist",
            Math.Clamp(existing?.BodyHeat ?? 50, 0, 100), 100,
            existing is { EquippedHat: not "none" } ? existing.EquippedHat : existing?.HatOn == true ? "hat" : "none",
            existing?.EquippedShirt ?? "none", existing?.EquippedPants ?? "none", existing?.WantedLevel ?? 0, existing?.EBikeRemainingMeters ?? 1609.344,
            existing?.EnergyDrinkBoostUntilUtc, existing?.EnergyDrinkCrashUntilUtc, existing?.ProbedUntilUtc, existing?.CandleUntilUtc, existing?.ShieldOn ?? false, existing?.Ar15FireMode is "burst" ? "burst" : "single", Math.Clamp(existing?.FlamethrowerGasGallons ?? 0, 0, 5));
        if (player.MagicHikingShoesOn && player.MagicRunningShoesOn) player = player with { MagicRunningShoesOn = false };
        player = player with { UfoRemainingMeters = existing?.UfoRemainingMeters ?? 0 };
        var offhand = ActiveOffhand(player);
        player = player with { FlashlightOn = offhand == "flashlight", LanternOn = offhand == "lantern", LaserOn = offhand == "laser", ShieldOn = offhand == "shield" };
        if (IsGasAsleep(characterId)) player = player with { AsleepUntilUtc = _sleepUntil[characterId] };
        _players[characterId] = player;
        _progressionLastTick[characterId] = DateTimeOffset.UtcNow;
        _lastMovement[characterId] = DateTimeOffset.UtcNow;
        _lastIdleHeal[characterId] = DateTimeOffset.UtcNow;
        _craftingExperience[characterId] = await _store.LoadCraftingExperienceAsync(Configuration.Id, characterId, cancellationToken);
        foreach (var study in await _store.LoadRecipeStudiesAsync(Configuration.Id, characterId, cancellationToken))
            _recipeStudies[(characterId, study.RecipeId)] = study;
        foreach (var recipeId in await _store.LoadLearnedRecipesAsync(Configuration.Id, characterId, cancellationToken))
        {
            _learnedRecipes[(characterId, recipeId)] = 0;
            if (!_recipeStudies.ContainsKey((characterId, recipeId)) && CraftingCatalog.Recipes.FirstOrDefault(recipe => recipe.Id == recipeId) is { } recipe)
            {
                var study = CreateRecipeStudy(recipe);
                await _store.SaveRecipeStudyAsync(Configuration.Id, characterId, study, _craftingExperience[characterId], token: cancellationToken);
                _recipeStudies[(characterId, recipeId)] = study;
            }
        }
        var inventory = await _store.LoadInventoryAsync(characterId, cancellationToken);
        RestorePhotographs(inventory.Items);
        _inventories[characterId] = inventory.Items.ToDictionary(item => item.ItemType, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(accountId)) await ParkCarriedVehiclesAsync(characterId, accountId, cancellationToken);
        foreach (var item in inventory.Items.Where(item => !string.IsNullOrWhiteSpace(item.Quality)))
            _weaponQualities[(characterId, item.ItemType)] = item.Quality!;
        foreach (var item in inventory.Items.Where(item => InventoryDefinition(item.ItemType).Category == InventoryCategory.Weapon && !_weaponQualities.ContainsKey((characterId, item.ItemType))))
            _weaponQualities[(characterId, item.ItemType)] = "Common";
        await EnsurePersonalFlagAllowanceAsync(characterId, cancellationToken);
        foreach (var relationship in await _store.LoadRelationshipsAsync(Configuration.Id, characterId, cancellationToken))
            _relationships[(characterId, relationship.ActorId)] = relationship.FriendRating;
        foreach (var quest in await _store.LoadQuestsAsync(Configuration.Id, characterId, cancellationToken))
            _quests[(characterId, quest.Id)] = quest;
        await RestoreFoodDeliveriesAsync(characterId, cancellationToken);
        foreach (var areaKey in await _store.LoadWorldMapDiscoveryAsync(Configuration.Id, characterId, cancellationToken))
            _revealedWorldAreas[(characterId, areaKey)] = 0;
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var notices = await _store.TakeAccountNoticesAsync(accountId, Configuration.Id, cancellationToken);
            if (notices.Count > 0)
            {
                var existingNotice = _homeNotices.GetValueOrDefault(characterId);
                _homeNotices[characterId] = string.Join("\n", string.IsNullOrWhiteSpace(existingNotice) ? notices : new[] { existingNotice! }.Concat(notices));
            }
        }
        await _store.SaveCharacterAsync(Configuration.Id, player, cancellationToken);
        return player;
    }

    private async Task<DungeonState?> EnsureHomeAsync(string playerId, string accountId, CancellationToken cancellationToken, string? ownerName = null)
    {
        _playerAccounts[playerId] = accountId;
        await _basePurchaseLock.WaitAsync(cancellationToken);
        try
        {
            var assignment = await _store.LoadBaseAssignmentAsync(accountId, Configuration.Id, cancellationToken);
            if (assignment?.Position is { } assignedPosition && assignedPosition.Region == Configuration.Area.Region && !_baseEntities.ContainsKey(assignment.BuildingId))
                await EnsureAreaLoadedAsync(assignedPosition.X, assignedPosition.Y, cancellationToken);
            var baseBuilding = assignment?.BuildingId;
            if (baseBuilding is not null && _baseEntities.TryGetValue(baseBuilding, out var assignedBuilding) && StoreProfileForBuilding(assignedBuilding) is not null)
                baseBuilding = null;
            if (baseBuilding is null || !_baseEntities.ContainsKey(baseBuilding))
            {
                var buildings = _baseEntities.Values
                    .Where(entity => entity.Kind == EntityKind.Building && StoreProfileForBuilding(entity) is null)
                    .OrderBy(entity => entity.Id).ToArray();
                if (buildings.Length == 0) return null;
                var assigned = await _store.LoadAssignedBaseBuildingsAsync(Configuration.Id, cancellationToken);
                var start = (StableInt(accountId) & int.MaxValue) % buildings.Length;
                var building = Enumerable.Range(0, buildings.Length).Select(offset => buildings[(start + offset) % buildings.Length]).FirstOrDefault(candidate => !assigned.Contains(candidate.Id)) ?? buildings[start];
                baseBuilding = building.Id;
                await _store.SaveBaseBuildingAsync(accountId, Configuration.Id, baseBuilding, building.Position, cancellationToken);
            }
            _baseBuildings[accountId] = baseBuilding;
            _publicBaseClaims[baseBuilding] = new PublicBaseClaim(accountId, baseBuilding, ownerName ?? _publicBaseClaims.GetValueOrDefault(baseBuilding)?.OwnerName ?? _players.GetValueOrDefault(playerId)?.Name ?? "Explorer");
            var baseEntity = _baseEntities[baseBuilding];
            await EnsureHomeFurnitureAsync(accountId, baseEntity, cancellationToken);
            await EnsureHomeItemStorageAsync(accountId, cancellationToken);
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

    private WorldPosition InitialBaseSpawn(string characterId, DungeonState? home)
    {
        if (home is not null && _returnPositions.TryGetValue(characterId, out var outside) && !Navigation.IsBlocked(outside.X, outside.Y) && Navigation.TerrainAt(outside.X, outside.Y) != TerrainType.DeepWater)
            return outside;
        return RandomOutdoorSpawn(characterId);
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
        _busReturnPositions.TryRemove(characterId, out _);
        _progressionLastTick.TryRemove(characterId, out _);
        _lastMovement.TryRemove(characterId, out _);
        _lastRaftDrift.TryRemove(characterId, out _);
        _lastChat.TryRemove(characterId, out _);
        _lastIdleHeal.TryRemove(characterId, out _);
        _playerAccounts.TryRemove(characterId, out _);
        _activeProbulatorBeams.TryRemove(characterId, out _);
        _probulatorAbductions.TryRemove(characterId, out _);
    }

    public void PurgeTestAccounts(IEnumerable<ExpiredTestAccount> accounts)
    {
        foreach (var account in accounts)
        {
            if (_baseBuildings.TryRemove(account.AccountId, out var buildingId)) _publicBaseClaims.TryRemove(buildingId, out _);
            foreach (var claim in _publicBaseClaims.Where(pair => pair.Value.AccountId == account.AccountId).Select(pair => pair.Key).ToArray()) _publicBaseClaims.TryRemove(claim, out _);
            _homeFurniture.TryRemove(account.AccountId, out _);
            _homeItemStorage.TryRemove(account.AccountId, out _);
            _homeCash.TryRemove(account.AccountId, out _);
            foreach (var home in _dungeons.Keys.Where(key => key.StartsWith($"home:{account.AccountId}:", StringComparison.Ordinal)).ToArray()) _dungeons.TryRemove(home, out _);

            foreach (var characterId in account.CharacterIds)
            {
                Leave(characterId);
                _progression.TryRemove(characterId, out _);
                _progressionLastTick.TryRemove(characterId, out _);
                _craftingExperience.TryRemove(characterId, out _);
                foreach (var key in _recipeStudies.Keys.Where(key => key.Player == characterId).ToArray()) _recipeStudies.TryRemove(key, out _);
                foreach (var key in _learnedRecipes.Keys.Where(key => key.Player == characterId).ToArray()) _learnedRecipes.TryRemove(key, out _);
                _inventories.TryRemove(characterId, out _);
                _returnPositions.TryRemove(characterId, out _);
                _pendingPolice.TryRemove(characterId, out _);
                _lastWantedDecay.TryRemove(characterId, out _);
                _swatDeployedFor.TryRemove(characterId, out _);
                _ufoHits.TryRemove(characterId, out _);
                foreach (var key in _weaponQualities.Keys.Where(key => key.Player == characterId).ToArray()) _weaponQualities.TryRemove(key, out _);
                foreach (var key in _relationships.Keys.Where(key => key.Player == characterId).ToArray()) _relationships.TryRemove(key, out _);
                foreach (var key in _revealedWorldAreas.Keys.Where(key => key.Player == characterId).ToArray()) _revealedWorldAreas.TryRemove(key, out _);
                foreach (var key in _quests.Keys.Where(key => key.Player == characterId).ToArray()) _quests.TryRemove(key, out _);
                foreach (var key in _questOffers.Keys.Where(key => key.Player == characterId).ToArray()) _questOffers.TryRemove(key, out _);
                foreach (var key in _lastPlayerAttack.Keys.Where(key => key.Player == characterId).ToArray()) _lastPlayerAttack.TryRemove(key, out _);
                foreach (var entity in _realityEntities.Where(pair => pair.Value.Properties.GetValueOrDefault("owner") == characterId).Select(pair => pair.Key).ToArray()) _realityEntities.TryRemove(entity, out _);
            }
        }
    }

    public async Task LeaveAsync(string characterId, CancellationToken cancellationToken = default)
    {
        _actionModes.TryRemove(characterId, out _);
        _players.TryGetValue(characterId, out var player);
        Leave(characterId);
        await _progressionLock.WaitAsync(cancellationToken);
        try { if (_progression.TryGetValue(characterId, out var profile)) await _store.SaveProgressionAsync(Configuration.Id, characterId, profile, cancellationToken); }
        finally { _progressionLock.Release(); }
        if (player is not null && player.LocationId != "outdoor" && !PlayersOccupyingSession(player.LocationId, characterId))
            await ResetDungeonSessionAsync(characterId, player.LocationId, cancellationToken);
    }

    public string? TakeHomeNotice(string characterId) => _homeNotices.TryRemove(characterId, out var notice) ? notice : null;

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
        var outcome = await MoveCoreAsync(characterId, request, cancellationToken);
        if (outcome is { Moved: true }) { await RecordDeliveryHandlingAsync(characterId, outcome.Player.SpeedMetersPerSecond, false, cancellationToken); await ApplyInversionTransformationsAsync(cancellationToken); outcome = outcome with { Player = _players[characterId] }; }
        return outcome;
    }

    private async Task<MovementOutcome?> MoveCoreAsync(string characterId, MoveRequest request, CancellationToken cancellationToken)
    {
        if (!_players.TryGetValue(characterId, out var player)) return null;
        if (player.TravelMode == TravelMode.Swim && !player.GodMode && (player.SwimExhausted || player.Stamina <= 0))
        {
            _swimAttempts[player.Id] = _probulatorClock.GetUtcNow();
            return new(player, false, true, false, false, false, "Too exhausted to swim. Rest; struggling consumes Air.");
        }
        if (EventPaused(player)) return new(player, false, true, false, false, false, "Please hold. Your movement is very important to us.");
        if (player.RidingBusId is not null) return new(player, false, true, false, false, false, "Use Get off bus now before moving.");
        if (player.WaitingAtBusStopId is not null) return new(player, false, true, false, false, false, "Cancel waiting before moving.");
        if (IsGasAsleep(characterId)) return new(player, false, true, false, false, false, "You are asleep until the gas effect wears off.");
        if (IsProbulatorAbducted(characterId)) return new(player, false, true, false, false, false, "The Probulator is holding you until the abduction ends.");
        var vehicle = player.TravelMode switch
        {
            TravelMode.Skateboard => "skateboard", TravelMode.Bike => "bike", TravelMode.EBike => "eBike",
            TravelMode.DirtBike => "dirtBike", TravelMode.Motorcycle => "motorcycle", TravelMode.Raft => "inflatableRaft", TravelMode.Ufo => "ufo", TravelMode.Swim => "swimmies", _ => null
        };
        if (vehicle is not null && (!player.GodMode || player.TravelMode == TravelMode.Ufo) && InventoryQuantity(characterId, vehicle) <= 0)
        {
            var stopped = await SetTravelModeAsync(characterId, TravelMode.Walk, cancellationToken);
            return new(stopped, false, false, false, false, false, "That vehicle is no longer in an inventory you own. Switched to walking.");
        }
        if (player.LocationId != "outdoor") return await MoveInDungeonAsync(player, request, cancellationToken);
        var (directionX, directionY, remainingDistance) = ResolveMovementVector(player, request);
        var now = DateTimeOffset.UtcNow;
        var previous = _lastMovement.AddOrUpdate(characterId, now, (_, old) => now);
        var elapsed = Math.Clamp((now - previous).TotalSeconds, 0.01, 0.15);
        var currentTerrain = TerrainFor(player);
        if (!player.GodMode && player.TravelMode == TravelMode.Ufo && TravelFuelRange(player) <= 0)
        {
            var stopped = player with { SpeedMetersPerSecond = 0, Version = player.Version + 1 };
            await SavePlayerAsync(stopped, cancellationToken);
            return new(stopped, false, true, false, false, false, "Your UFO is out of Kryptonite. Carry Kryptonite in your inventory; one powers 10 miles of flight.");
        }
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
        if (remainingDistance is not null) maximumStep = Math.Min(maximumStep, remainingDistance.Value);
        if (!player.GodMode) maximumStep = Math.Min(maximumStep, TravelFuelRange(player));
        var step = Math.Min(metersPerSecond * elapsed * Configuration.GameSpeed, maximumStep);
        var requested = (_loadedBounds ?? Configuration.Area.Bounds).Clamp(player.Position with
        {
            X = player.Position.X + (directionX * step),
            Y = player.Position.Y + (directionY * step)
        });

        var requestedTerrain = EventTerrainAt(requested, Navigation.TerrainAt(requested.X, requested.Y));
        if (player.TravelMode == TravelMode.Raft && !RaftTerrainOnly(requestedTerrain))
        {
            var stopped = player with { SpeedMetersPerSecond = 0, Terrain = currentTerrain, Version = player.Version + 1 };
            await SavePlayerAsync(stopped, cancellationToken);
            return new(stopped, false, true, false, false, false, "A raft cannot leave the water. Switch to another travel mode before going ashore.");
        }
        if (player.TravelMode == TravelMode.Skateboard && !WorldNavigation.SupportsTravelMode(requestedTerrain, TravelMode.Skateboard))
        {
            var damaged = player with { HealthHearts = player.GodMode ? Math.Max(1, player.HealthHearts - .25) : Math.Max(0, player.HealthHearts - .25), TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Terrain = currentTerrain, Version = player.Version + 1 };
            if (damaged.HealthHearts <= 0)
            {
                var reset = await DieAndResetPlayerAsync(damaged, cancellationToken);
                await SavePlayerAsync(reset, cancellationToken);
                return new(reset, true, false, false, true, true, "You fell, lost your final quarter-heart, and returned to the starting point.");
            }
            await SavePlayerAsync(damaged, cancellationToken);
            return new(damaged, true, false, false, true, false, "The skateboard left a paved surface. You fell and lost ¼ heart.");
        }

        var next = requested;
        var raftTerrain = player.TravelMode == TravelMode.Raft
            ? (_activeInversion?.Type == "flood" && InsideInversion(player) ? (Func<TerrainType,bool>)(_ => true) : RaftTerrainOnly)
            : null;
        var blocked = player.TravelMode == TravelMode.Ufo ? false : raftTerrain is null
            ? !Navigation.CanTraverse(player.Position, requested) || BusBlocksMovement(player.Position, requested)
            : !Navigation.CanTraverse(player.Position, requested, raftTerrain) || BusBlocksMovement(player.Position, requested);
        if (blocked)
        {
            var slideX = requested with { Y = player.Position.Y };
            var slideY = requested with { X = player.Position.X };
            if (!BusBlocksMovement(player.Position, slideX) && (raftTerrain is null ? Navigation.CanTraverse(player.Position, slideX) : Navigation.CanTraverse(player.Position, slideX, raftTerrain))) next = slideX;
            else if (!BusBlocksMovement(player.Position, slideY) && (raftTerrain is null ? Navigation.CanTraverse(player.Position, slideY) : Navigation.CanTraverse(player.Position, slideY, raftTerrain))) next = slideY;
            else next = player.Position;
        }
        var nextTerrain = EventTerrainAt(next, Navigation.TerrainAt(next.X, next.Y));
        if (player.TravelMode == TravelMode.Swim && !IsWater(nextTerrain)) player = player with { TravelMode = TravelMode.Walk };
        // Deep water consumes Air in the vitals tick, never instant death.

        var distance = player.Position.Distance2D(next);
        var dirtBikeGas = player.DirtBikeGasGallons;
        var motorcycleGas = player.MotorcycleGasGallons;
        var eBikeRemaining = player.EBikeRemainingMeters;
        var ufoRemaining = player.UfoRemainingMeters;
        var kryptoniteUsed = 0;
        if (!player.GodMode && distance > 0)
        {
            if (player.TravelMode == TravelMode.DirtBike) dirtBikeGas = FuelAfterTravel(dirtBikeGas, distance, DirtBikeMilesPerGallon);
            if (player.TravelMode == TravelMode.Motorcycle) motorcycleGas = FuelAfterTravel(motorcycleGas, distance, MotorcycleMilesPerGallon);
            if (player.TravelMode == TravelMode.EBike) eBikeRemaining = Math.Max(0, eBikeRemaining - distance);
            if (player.TravelMode == TravelMode.Ufo)
            {
                kryptoniteUsed = (int)Math.Ceiling(Math.Max(0, distance - ufoRemaining - 1e-8) / UfoMetersPerKryptonite);
                ufoRemaining = Math.Max(0, ufoRemaining + kryptoniteUsed * UfoMetersPerKryptonite - distance);
                if (ufoRemaining < 1e-8) ufoRemaining = 0;
            }
        }
        var updated = player with
        {
            Position = next with { Z = Navigation.ElevationAt(next.X, next.Y) }, Terrain = nextTerrain,
            SpeedMetersPerSecond = distance > .001 ? distance / elapsed : 0,
            Stamina = StaminaAfterTravel(player, distance, elapsed, now, reducedStaminaDrain),
            DirtBikeGasGallons = dirtBikeGas,
            MotorcycleGasGallons = motorcycleGas,
            EBikeRemainingMeters = eBikeRemaining,
            UfoRemainingMeters = ufoRemaining,
            Version = player.Version + 1
        };
        if (!player.GodMode && player.TravelMode == TravelMode.EBike && eBikeRemaining <= .001)
        {
            RemoveInventory(characterId, "eBike", 1); updated = updated with { TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0 };
            await SaveInventoryAsync(characterId, cancellationToken); await SavePlayerAsync(updated, cancellationToken);
            return new(updated, distance > .001, false, false, false, false, "The e-bike battery died after one mile. The e-bike disappeared from your inventory.");
        }
        if (!await SavePlayerAsync(updated, cancellationToken, kryptoniteUsed))
            return new(_players.GetValueOrDefault(characterId, player), false, false, false, false, false, null);
        if (distance > .001 && player.TravelMode == TravelMode.Ufo
            && _activeProbulatorBeams.TryGetValue(characterId, out var beam))
            beam.Segments.Enqueue((player.Position, updated.Position));
        return new(updated, distance > .001, blocked && distance <= .001, false, false, false, kryptoniteUsed > 0 ? $"UFO loaded {kryptoniteUsed} Kryptonite: 10 miles of flight per crystal." : null);
    }

    private static (double DirectionX, double DirectionY, double? RemainingDistance) ResolveMovementVector(PlayerState player, MoveRequest request)
    {
        if (request.DestinationX is double destinationX && request.DestinationY is double destinationY &&
            double.IsFinite(destinationX) && double.IsFinite(destinationY))
        {
            var targetX = destinationX - player.Position.X;
            var targetY = destinationY - player.Position.Y;
            var remaining = Math.Sqrt(targetX * targetX + targetY * targetY);
            return remaining > .0001 ? (targetX / remaining, targetY / remaining, remaining) : (0, 0, 0);
        }

        var length = Math.Sqrt(request.X * request.X + request.Y * request.Y);
        return length > 1 ? (request.X / length, request.Y / length, null) : (request.X, request.Y, null);
    }

    public async Task<PlayerState> SetTravelModeAsync(string characterId, TravelMode mode, CancellationToken cancellationToken = default)
    {
        EnsureNotOnBus(characterId);
        EnsureNotProbulatorAbducted(characterId);
        if (IsGasAsleep(characterId)) throw new InvalidOperationException("You are asleep until the gas effect wears off.");
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (ProbedActive(player) && mode != TravelMode.Walk) throw new InvalidOperationException("While Probed, you can only walk. Sleep or wait for the effect to end.");
        if (player.LocationId != "outdoor" && mode is TravelMode.Bike or TravelMode.EBike or TravelMode.DirtBike or TravelMode.Motorcycle or TravelMode.Ufo)
            throw new InvalidOperationException("Bikes, e-bikes, dirt bikes, motorcycles, and UFOs cannot be used inside a dungeon or Home.");
        if (!player.GodMode && mode == TravelMode.Skateboard && InventoryQuantity(characterId, "skateboard") <= 0) throw new InvalidOperationException("You need a skateboard in your inventory.");
        if (!player.GodMode && mode == TravelMode.Bike && InventoryQuantity(characterId, "bike") <= 0) throw new InvalidOperationException("You need a bike in your inventory.");
        if (!player.GodMode && mode == TravelMode.EBike && InventoryQuantity(characterId, "eBike") <= 0) throw new InvalidOperationException("You need an e-bike in your inventory.");
        if (!player.GodMode && mode == TravelMode.DirtBike && InventoryQuantity(characterId, "dirtBike") <= 0) throw new InvalidOperationException("You need a dirt bike in your inventory.");
        if (!player.GodMode && mode == TravelMode.Motorcycle && InventoryQuantity(characterId, "motorcycle") <= 0) throw new InvalidOperationException("You need a motorcycle in your inventory.");
        if (mode == TravelMode.Ufo && InventoryQuantity(characterId, "ufo") <= 0) throw new InvalidOperationException("You need a UFO in your inventory.");
        if (mode == TravelMode.Raft)
        {
            if (!player.GodMode && InventoryQuantity(characterId, "inflatableRaft") <= 0) throw new InvalidOperationException("You need an inflatable raft.");
            if (player.LocationId == "outdoor" && TerrainFor(player) != TerrainType.ShallowWater && player.TravelMode != TravelMode.Raft) throw new InvalidOperationException("A raft can only be deployed from shallow water.");
        }
        if (mode == TravelMode.Swim)
        {
            if (InventoryQuantity(characterId, "swimmies") <= 0) throw new InvalidOperationException("Find Swimmies before swimming.");
            if (!IsWater(TerrainFor(player))) throw new InvalidOperationException("Swimmies can only be used in water.");
        }
        var landing = player.Position;
        if (player.TravelMode == TravelMode.Ufo && mode != TravelMode.Ufo)
        {
            landing = Navigation.FindNearestWalkable(player.Position);
            landing = landing with { Z = Navigation.ElevationAt(landing.X, landing.Y) };
            _activeProbulatorBeams.TryRemove(characterId, out _);
        }
        var updated = player with
        {
            Position = landing,
            Terrain = Navigation.TerrainAt(landing.X, landing.Y),
            TravelMode = mode,
            EquippedWeapon = mode == TravelMode.Ufo ? "probulator" : player.EquippedWeapon == "probulator" ? "fist" : player.EquippedWeapon,
            SpeedMetersPerSecond = 0,
            EBikeRemainingMeters = mode == TravelMode.EBike && player.EBikeRemainingMeters <= 0 ? 1609.344 : player.EBikeRemainingMeters,
            Version = player.Version + 1
        };
        await SavePlayerAsync(updated, cancellationToken);
        return updated;
    }

    public Task<IReadOnlyList<PlayerState>> AdvanceStaminaAsync(TimeSpan elapsed, CancellationToken cancellationToken = default) =>
        AdvanceVitalsAsync(elapsed, cancellationToken);

    public async Task<(PlayerState Player, bool Expanded)> TeleportWithAreaAsync(string characterId, TeleportRequest request, CancellationToken cancellationToken = default)
    {
        EnsureNotOnBus(characterId);
        if (!playerIsGod(characterId)) throw new InvalidOperationException("God Mode must be enabled to teleport.");
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (player.LocationId != "outdoor") throw new InvalidOperationException("Leave the dungeon or Home before teleporting.");
        return await TeleportToOutdoorPositionAsync(characterId, player, request.X, request.Y, false, cancellationToken);
    }

    public async Task<(PlayerState Player, bool Expanded)> MapFastTravelAsync(string characterId, MapFastTravelRequest request, CancellationToken cancellationToken = default)
    {
        EnsureNotOnBus(characterId);
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (player.LocationId != "outdoor") throw new InvalidOperationException("Leave the dungeon, store, or Home before using mini-map fast travel.");
        var targetType = (request.TargetType ?? string.Empty).Trim();
        var targetId = (request.TargetId ?? string.Empty).Trim();
        WorldPosition target;
        if (targetType.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            if (!_playerAccounts.TryGetValue(characterId, out var accountId) ||
                !_baseBuildings.TryGetValue(accountId, out var buildingId) ||
                !string.Equals(buildingId, targetId, StringComparison.Ordinal))
                throw new InvalidOperationException("That Home is not assigned to your account.");
            var door = _baseEntities.Values.FirstOrDefault(entity => entity.Kind == EntityKind.Door && entity.Properties.GetValueOrDefault("buildingId") == buildingId)
                ?? throw new InvalidOperationException("Your Home entrance is not available yet.");
            target = door.Position;
        }
        else if (targetType.Equals("flag", StringComparison.OrdinalIgnoreCase))
        {
            if (!_realityEntities.TryGetValue(targetId, out var flag) || !IsPersonalFlag(flag) ||
                !string.Equals(flag.Properties.GetValueOrDefault("owner"), characterId, StringComparison.Ordinal))
                throw new InvalidOperationException("That flag does not belong to you.");
            target = flag.Position;
        }
        else throw new InvalidOperationException("Mini-map fast travel is available only for your Home and personal flags.");

        return await TeleportToOutdoorPositionAsync(characterId, player, target.X, target.Y, true, cancellationToken);
    }

    private async Task<(PlayerState Player, bool Expanded)> TeleportToOutdoorPositionAsync(string characterId, PlayerState player,
        double x, double y, bool normalizeTravelMode, CancellationToken cancellationToken)
    {
        EnsureNotProbulatorAbducted(characterId);
        if (IsGasAsleep(characterId)) throw new InvalidOperationException("You are asleep until the gas effect wears off.");
        var expanded = await EnsureAreaLoadedAsync(x, y, cancellationToken);
        var requested = (_loadedBounds ?? Configuration.Area.Bounds).Clamp(player.Position with { X = x, Y = y });
        var destination = Navigation.IsBlocked(requested.X, requested.Y) || Navigation.TerrainAt(requested.X, requested.Y) == TerrainType.DeepWater
            ? Navigation.FindNearestWalkable(requested)
            : requested with { Z = Navigation.ElevationAt(requested.X, requested.Y) };
        var terrain = Navigation.TerrainAt(destination.X, destination.Y);
        var travelMode = normalizeTravelMode && !WorldNavigation.SupportsTravelMode(terrain, player.TravelMode)
            ? TravelMode.Walk
            : player.TravelMode;
        var updated = player with
        {
            Position = destination,
            Terrain = terrain,
            TravelMode = travelMode,
            SpeedMetersPerSecond = 0,
            Version = player.Version + 1
        };
        _lastMovement[characterId] = DateTimeOffset.UtcNow;
        if (await SavePlayerAsync(updated, cancellationToken)) await RecordDeliveryHandlingAsync(characterId, 0, true, cancellationToken);
        return (updated, expanded);
    }

    public async Task<PlayerState> TeleportAsync(string characterId, TeleportRequest request, CancellationToken cancellationToken = default) =>
        (await TeleportWithAreaAsync(characterId, request, cancellationToken)).Player;

    public async Task<(NavigationResult Result, bool Expanded)> FindPathAsync(string characterId, PathRequest request, CancellationToken cancellationToken = default)
    {
        EnsureNotOnBus(characterId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_players.TryGetValue(characterId, out var player)) return (new(false, Array.Empty<WorldPosition>(), "Unknown player."), false);
        if (player.LocationId != "outdoor" && _dungeons.TryGetValue(player.LocationId, out var dungeon))
        {
            if (request.X < .5 || request.Y < .5 || request.X > dungeon.Width - .5 || request.Y > dungeon.Height - .5) return (new(false, Array.Empty<WorldPosition>(), dungeon.IsHome ? "That point is outside Home." : "That point is outside the dungeon."), false);
            var target = player.Position with { X = request.X, Y = request.Y, Z = 0 };
            if (dungeon.Walls.Any(wall => CrossesDungeonWall(player.Position, target, wall))) return (new(false, Array.Empty<WorldPosition>(), dungeon.IsHome ? "A wall in Home blocks that route. Move through a doorway." : "A dungeon wall blocks that route. Move through a doorway."), false);
            return (new(true, new[] { target }), false);
        }
        var expanded = await EnsureAreaLoadedAsync(request.X, request.Y, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (player.TravelMode == TravelMode.Ufo)
        {
            var target = (_loadedBounds ?? Configuration.Area.Bounds).Clamp(player.Position with { X = request.X, Y = request.Y });
            return (new(true, new[] { target with { Z = Navigation.ElevationAt(target.X, target.Y) } }), expanded);
        }
        if (player.TravelMode == TravelMode.Raft)
        {
            if (_activeInversion?.Type == "flood" && InsideInversion(player) && IsWater(EventTerrainAt(player.Position with { X=request.X,Y=request.Y },Navigation.TerrainAt(request.X,request.Y)))) return (Navigation.FindPath(player.Position,request.X,request.Y,terrain=>ConfiguredSpeedMetersPerSecond(player,terrain),cancellationToken:cancellationToken),expanded);
            if (!RaftTerrainOnly(Navigation.TerrainAt(request.X, request.Y)))
                return (new(false, Array.Empty<WorldPosition>(), "A raft cannot leave the water. Switch to another travel mode before going ashore."), expanded);
            var waterRoute = Navigation.FindPath(player.Position, request.X, request.Y,
                terrain => ConfiguredSpeedMetersPerSecond(player, terrain), RaftTerrainOnly, cancellationToken);
            return waterRoute.Success
                ? (waterRoute, expanded)
                : (new(false, Array.Empty<WorldPosition>(), "No continuous water route to that destination was found."), expanded);
        }
        return (Navigation.FindPath(player.Position, request.X, request.Y, terrain => ConfiguredSpeedMetersPerSecond(player, terrain), cancellationToken: cancellationToken), expanded);
    }

    public IReadOnlyList<ActorState> TriggerWorldEvent(string characterId, string eventType)
    {
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (!player.GodMode) throw new InvalidOperationException("God Mode must be enabled to trigger a Reality inversion.");
        var key = (eventType ?? string.Empty).Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var anchor = player.LocationId == "outdoor" ? player.Position : _returnPositions.GetValueOrDefault(characterId, new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
        var now = _probulatorClock.GetUtcNow();
        var actors = new List<ActorState>();
        if (key is "retrobattles" or "retro") { StartRetroBattles(); return actors; }
        lock (_actorRandom)
        {
            if (key == "ufo")
            {
                actors.Add(new ActorState("ufo:manual", EntityKind.Npc, "ufo", "UFO", anchor with { X = anchor.X - 35, Z = 100 }, "east", true, EventStartedAtUtc: now, EventEndsAtUtc: now.AddMinutes(_eventConfiguration.UfoDurationMinutes), EventName: _eventConfiguration.UfoEventName));
            }
            else if (key is "trex" or "tyrannosaurus")
            {
                actors.Add(CreateEventDinosaur("trex:manual", "tRex", "Rex", anchor, 18, 50, now.AddMinutes(_eventConfiguration.TrexDurationMinutes), now, _eventConfiguration.TrexEventName));
            }
            else if (key is "brontosaurus" or "bronto")
            {
                actors.Add(CreateEventDinosaur("brontosaurus:manual", "brontosaurus", "Bronto", anchor, 22, 80, now.AddMinutes(_eventConfiguration.BrontosaurusDurationMinutes), now, _eventConfiguration.BrontosaurusEventName));
            }
            else if (key is "stegosaurus" or "stegosaur")
            {
                actors.Add(CreateEventDinosaur("stegosaurus:manual", "stegosaurus", "Steggy", anchor, 18, 45, now.AddMinutes(_eventConfiguration.StegosaurusDurationMinutes), now, _eventConfiguration.StegosaurusEventName));
            }
            else if (key is "raptor" or "raptors")
            {
                var packCenter = CreateEventPosition(anchor, 16);
                for (var index = 0; index < 3; index++)
                    actors.Add(CreateEventDinosaur($"raptor:manual:{index + 1}", "raptor", index == 0 ? "Raptor Alpha" : $"Raptor {index + 1}", packCenter, index == 0 ? 0 : 2.5, 12, now.AddMinutes(_eventConfiguration.RaptorDurationMinutes), now, _eventConfiguration.RaptorEventName));
            }
            else if (key is "giant" or "landofgiants" or "landofthegiants")
            {
                actors.Add(CreateEventDinosaur("giant:manual", "giant", "The Giant", anchor, 20, 100, now.AddMinutes(_eventConfiguration.LandOfGiantsDurationMinutes), now, _eventConfiguration.LandOfGiantsEventName));
            }
            else if (key is "bear" or "greatbear")
            {
                var angle = _actorRandom.NextDouble() * Math.PI * 2;
                var position = CreateEventPosition(anchor, 14);
                actors.Add(new ActorState("event-bear:manual", EntityKind.Animal, "eventBear", "The Great Bear", position, MaximumHealthHearts: 20, HealthHearts: 20, EventStartedAtUtc: now, EventEndsAtUtc: now.AddMinutes(_eventConfiguration.BearDurationMinutes), EventName: _eventConfiguration.BearEventName));
            }
            else throw new InvalidOperationException("Choose UFO, T-Rex, brontosaurus, stegosaurus, raptors, Land of the Giants, or bear.");

            // Separate invocations coexist so an earlier inversion can finish through its exit portal.
            var invocation = Guid.NewGuid().ToString("N");
            for (var index = 0; index < actors.Count; index++) actors[index] = actors[index] with { Id = $"{actors[index].Id}:{invocation}", IsMoving = false };
            foreach (var actor in actors)
            {
                _actors[actor.Id] = actor;
                _actorRoutes.TryRemove(actor.Id, out _);
                foreach (var hit in _ufoHits.Keys.Where(value => value.StartsWith(actor.Id + ":", StringComparison.Ordinal))) _ufoHits.TryRemove(hit, out _);
                foreach (var cooldown in _eventAttackCooldowns.Keys.Where(value => value.StartsWith(actor.Id + ":", StringComparison.Ordinal))) _eventAttackCooldowns.TryRemove(cooldown, out _);
            }
        }
        return actors;
    }

    private WorldPosition CreateEventPosition(WorldPosition anchor, double distance)
    {
        if (TryCreateEventPosition(anchor, distance, out var position)) return position;
        throw new InvalidOperationException("No safe portal location within 50 meters. Move to a nearby open shoreline or street.");
    }

    private bool TryCreateEventPosition(WorldPosition anchor, double distance, out WorldPosition position)
    {
        var angle = _actorRandom.NextDouble() * Math.PI * 2;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var bearing = angle + attempt * Math.PI / 12;
            var candidate = Navigation.FindNearestWalkable(anchor with { X = anchor.X + Math.Cos(bearing) * distance, Y = anchor.Y + Math.Sin(bearing) * distance });
            if (candidate.Distance2D(anchor) <= 50 && !Navigation.IsBlocked(candidate.X, candidate.Y) && Navigation.TerrainAt(candidate.X, candidate.Y) != TerrainType.DeepWater)
            {
                position = candidate;
                return true;
            }
        }
        position = default;
        return false;
    }

    private void SpawnScheduledEvent(ref long? lastCycle, string scheduleKey, string idPrefix, EntityKind kind,
        string subtype, string name, double health, int intervalHours, int durationMinutes, string eventName,
        DateTimeOffset now, PlayerState[] players, List<ActorState> changed, int count = 1)
    {
        var cycle = ScheduledEventCycle(now, scheduleKey, intervalHours);
        if (lastCycle == cycle || players.Length == 0) return;
        var first = _actorRandom.Next(players.Length);
        for (var attempt = 0; attempt < players.Length; attempt++)
        {
            var anchor = players[(first + attempt) % players.Length].Position;
            WorldPosition position;
            if (subtype == "ufo") position = anchor with { X = anchor.X - 35, Z = 100 };
            else if (!TryCreateEventPosition(anchor, 20, out position)) continue;

            // Build the whole pack before publishing; every member stays near the same player.
            var arrivals = new List<ActorState>();
            for (var index = 0; index < count; index++)
            {
                var memberPosition = position;
                if (index > 0 && TryCreateEventPosition(position, 2.5, out var nearby) &&
                    nearby.Distance2D(anchor) <= 50 && nearby.Distance2D(position) <= 5)
                    memberPosition = nearby;
                var id = count == 1 ? $"{idPrefix}:{cycle}" : $"{idPrefix}:{cycle}:{index + 1}";
                var memberName = count == 1 ? name : index == 0 ? "Raptor Alpha" : $"Raptor {index + 1}";
                arrivals.Add(new ActorState(id, kind, subtype, memberName, memberPosition, Facing: subtype == "ufo" ? "east" : "south",
                    MaximumHealthHearts: health, HealthHearts: health, EventStartedAtUtc: now,
                    EventEndsAtUtc: now.AddMinutes(durationMinutes), EventName: eventName));
            }
            foreach (var actor in arrivals) { _actors[actor.Id] = actor; changed.Add(actor); }
            lastCycle = cycle;
            return;
        }
        // Keep the event due until an outdoor player has a safe nearby portal location.
    }

    private ActorState CreateEventDinosaur(string id, string subtype, string name, WorldPosition anchor, double distance, double health, DateTimeOffset endsAt, DateTimeOffset startsAt, string eventName)
    {
        var position = distance <= 0 ? Navigation.FindNearestWalkable(anchor) : CreateEventPosition(anchor, distance);
        return new ActorState(id, EntityKind.Animal, subtype, name, position, MaximumHealthHearts: health, HealthHearts: health, EventStartedAtUtc: startsAt, EventEndsAtUtc: endsAt, EventName: eventName);
    }

    public IReadOnlyList<ActorState> AdvanceActors(TimeSpan elapsed)
    {
        using var timing = Timings.Measure("actors.advance");
        var changed = new List<ActorState>();
        lock (_actorRandom)
        {
            var now = _probulatorClock.GetUtcNow();
            var eventPlayers = _players.Values.Where(player => player.LocationId == "outdoor" && !player.IsTestCharacter).ToArray();
            ApplyCompletedActorRoutes(now);
            var conversationActors = ActiveConversationActors(now);
            var waitingRecipients = _quests.Values.Where(q => q.Kind == "foodDelivery" && q.Status == "active" && q.DeliveryRecipient is not null)
                .Select(q => q.DeliveryRecipient!).ToDictionary(a => a.Id);
            var actorBatch = _actors.ToArray();
            var routeStart = (int)((uint)_actorRouteCursor % (uint)Math.Max(1, actorBatch.Length));
            _actorRouteCursor = (routeStart + _routePlanner.Capacity) % Math.Max(1, actorBatch.Length);
            for (var actorIndex = 0; actorIndex < actorBatch.Length; actorIndex++)
            {
                var pair = actorBatch[(routeStart + actorIndex) % actorBatch.Length];
                var actor = pair.Value;
                if (actor.Subtype == "haney" || ManagedEventActor(actor) || _transformedActors.ContainsKey(actor.Id) || IsDrivingNpc(actor.Id) || actor.Subtype.StartsWith("adventure:")) continue;
                if (actor.IsQuestGiver || conversationActors.Contains(actor.Id))
                {
                    if (actor.IsMoving) { actor = actor with { IsMoving = false, Version = actor.Version + 1 }; _actors[actor.Id] = actor; changed.Add(actor); }
                    continue;
                }
                if (waitingRecipients.TryGetValue(actor.Id, out var waiting) && !IsProbulatorAbducted(actor.Id))
                {
                    if (actor.Position != waiting.Position || actor.IsMoving)
                    {
                        actor = actor with { Position = waiting.Position, IsMoving = false, Version = actor.Version + 1 };
                        _actors[actor.Id] = actor; changed.Add(actor);
                    }
                    continue;
                }
                if (_deliveryDogs.ContainsKey(actor.Id)) continue;
                if (actor.IsTestCharacter || IsProbulatorAbducted(actor.Id) || IsGasAsleep(actor.Id)) continue;
                if (actor.EventEndsAtUtc is { } eventEnd && eventEnd <= now) { _actors.TryRemove(actor.Id, out _); _actorRoutes.TryRemove(actor.Id, out _); continue; }
                if (actor.IsPassingThroughPortal(now))
                {
                    if (actor.IsMoving)
                    {
                        actor = actor with { IsMoving = false, Version = actor.Version + 1 };
                        _actors[actor.Id] = actor;
                        changed.Add(actor);
                    }
                    continue;
                }
                if (actor.Subtype is "fish" or "waterMonster")
                {
                    var swimming = AdvanceWaterActor(actor, elapsed);
                    _actors[actor.Id] = swimming; changed.Add(swimming); continue;
                }
                if (actor.Subtype == "ufo")
                {
                    var ufoPosition = actor.Position with { X = actor.Position.X + 40 * elapsed.TotalSeconds };
                    var updatedUfo = actor with { Position = ufoPosition, Facing = "east", IsMoving = true, Version = actor.Version + 1 };
                    _actors[actor.Id] = updatedUfo; changed.Add(updatedUfo); continue;
                }
                if (!_actorRoutes.TryGetValue(actor.Id, out var route) || route.Count == 0)
                {
                    if (actor.IsMoving)
                    {
                        actor = actor with { IsMoving = false, Version = actor.Version + 1 };
                        _actors[actor.Id] = actor; changed.Add(actor);
                    }
                    if (!_actorRouteRetry.ContainsKey(actor.Id))
                        _routePlanner.TrySchedule(actor, Navigation, _actorRandom.Next(), Timings);
                    continue;
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
            foreach (var dungeonPair in _dungeons.ToArray())
            {
                var dungeon = dungeonPair.Value;
                if (!dungeon.IsStore) continue;
                foreach (var original in dungeon.Actors.Where(actor => actor.Subtype == "storeEmployee").ToArray())
                {
                    var actor = original;
                    if (conversationActors.Contains(actor.Id))
                    {
                        if (actor.IsMoving) { actor = actor with { IsMoving = false, Version = actor.Version + 1 }; SetActor(dungeonPair.Key, actor); changed.Add(actor); }
                        continue;
                    }
                    if (IsGasAsleep(actor.Id)) continue;
                    if (!_actorRoutes.TryGetValue(actor.Id, out var route) || route.Count == 0)
                    {
                        route = CreateInteriorActorRoute(actor, dungeon);
                        _actorRoutes[actor.Id] = route;
                    }
                    if (route.Count == 0)
                    {
                        if (actor.IsMoving) SetActor(dungeonPair.Key, actor with { IsMoving = false, Version = actor.Version + 1 });
                        continue;
                    }
                    var waypoint = route.Peek();
                    var dx = waypoint.X - actor.Position.X; var dy = waypoint.Y - actor.Position.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < .35) { route.Dequeue(); continue; }
                    var step = Math.Min(distance, ActorSpeed(actor.Subtype) * elapsed.TotalSeconds);
                    var position = actor.Position with { X = actor.Position.X + dx / distance * step, Y = actor.Position.Y + dy / distance * step };
                    if (dungeon.Walls.Any(wall => CrossesDungeonWall(actor.Position, position, wall))) { route.Clear(); continue; }
                    var facing = Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? "east" : "west") : (dy > 0 ? "north" : "south");
                    var updated = actor with { Position = position, Facing = facing, IsMoving = true, Version = actor.Version + 1 };
                    SetActor(dungeonPair.Key, updated);
                    changed.Add(updated);
                }
            }
        }
        return changed;
    }

    private long ScheduledEventCycle(DateTimeOffset now, string eventName, int intervalHours)
    {
        var seconds = Math.Max(1, intervalHours) * 60L * 60L;
        var phase = (StableInt($"event-phase:{Configuration.Seed}:{eventName}") & int.MaxValue) % seconds;
        return (now.ToUnixTimeSeconds() + phase) / seconds;
    }

    private void ResetScheduledEventCycles(DateTimeOffset now)
    {
        _lastRetroCycle = ScheduledEventCycle(now, "retro-battles", _eventConfiguration.RetroBattlesIntervalHours);
        _lastUfoCycle = ScheduledEventCycle(now, "ufo", _eventConfiguration.UfoIntervalHours);
        _lastTrexCycle = ScheduledEventCycle(now, "trex", _eventConfiguration.TrexIntervalHours);
        _lastEventBearCycle = ScheduledEventCycle(now, "event-bear", _eventConfiguration.BearIntervalHours);
        _lastBrontosaurusCycle = ScheduledEventCycle(now, "brontosaurus", _eventConfiguration.BrontosaurusIntervalHours);
        _lastStegosaurusCycle = ScheduledEventCycle(now, "stegosaurus", _eventConfiguration.StegosaurusIntervalHours);
        _lastRaptorCycle = ScheduledEventCycle(now, "raptors", _eventConfiguration.RaptorIntervalHours);
        _lastLandOfGiantsCycle = ScheduledEventCycle(now, "land-of-the-giants", _eventConfiguration.LandOfGiantsIntervalHours);
    }

    public IReadOnlyList<ChatMessage> AdvanceActorSpeech(DateTimeOffset now)
    {
        var messages = new List<ChatMessage>();
        lock (_actorRandom)
        {
            foreach (var actor in _actors.Values)
            {
                if (IsNorthernInvader(actor) || actor.Subtype == "haney" || IsProbulatorAbducted(actor.Id) || IsGasAsleep(actor.Id)) continue;
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

    public async Task<WorldSnapshot> RebuildAsync(string characterId, bool godMode, CancellationToken cancellationToken = default, bool fromScratch = false)
    {
        if (!playerIsGod(characterId)) throw new InvalidOperationException("God Mode must be enabled to rebuild this reality.");
        await _rebuildLock.WaitAsync(cancellationToken);
        try
        {
            await _areaLoadLock.WaitAsync(cancellationToken);
            try
            {
            await _areaPrefetchLock.WaitAsync(cancellationToken);
            try
            {
            _activeMapOperations["rebuild"] = fromScratch ? "Rebuilding reality from fresh geography" : "Resetting reality from saved blocks";
            var generated = fromScratch ? await _generator.RebuildFromScratchAsync(Configuration, cancellationToken)
                : await _generator.GenerateAsync(Configuration, cancellationToken);
            await _transitLock.WaitAsync(cancellationToken);
            try
            {
            await _store.ClearTransientWorldStateAsync(Configuration.Id, cancellationToken);
            _buses.Clear();
            _busReturnPositions.Clear();
            _transitSnapshot = new([], [], []);
            _transitBuiltRevision = -1;
            _realityEntities.Clear();
            _baseEntities.Clear(); _removedBaseEntityIds.Clear(); _elevationSamples.Clear(); _loadedAreas.Clear(); _actors.Clear(); _actorRoutes.Clear(); _nextActorSpeech.Clear(); _outdoorChests.Clear(); _chestContents.Clear(); _loot.Clear(); _dungeons.Clear(); _returnPositions.Clear(); _relationships.Clear(); _quests.Clear(); _questOffers.Clear(); _tradeQuotes.Clear(); _loadedBounds = null; _geographic = null;
            ApplyGeneratedWorld(generated);
            Interlocked.Exchange(ref _preparedAreaCount, 0);
            _loadedAreas["0:0"] = Configuration.Area.Bounds;
            _baseBuildings.Clear();
            foreach (var pair in _players.ToArray())
            {
                if (_playerAccounts.TryGetValue(pair.Key, out var accountId)) await EnsureHomeAsync(pair.Key, accountId, cancellationToken);
                await EnsurePersonalFlagAllowanceAsync(pair.Key, cancellationToken);
                await SavePlayerAsync(ResetPlayer(pair.Value), cancellationToken);
            }
            RefreshTransitNetwork();
            return CreateSnapshot();
            }
            finally { _transitLock.Release(); }
            }
            finally { _activeMapOperations.TryRemove("rebuild", out _); _areaPrefetchLock.Release(); }
            }
            finally { _areaLoadLock.Release(); }
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

    public IReadOnlyList<CanonicalEntity> PersonalFlagsForOwner(string characterId) => _realityEntities.Values
        .Where(entity => IsPersonalFlag(entity) && entity.Properties.GetValueOrDefault("owner") == characterId)
        .OrderBy(entity => entity.Id)
        .ToArray();

    private async Task EnsurePersonalFlagAllowanceAsync(string characterId, CancellationToken cancellationToken)
    {
        var availableAllowance = Math.Max(0, 5 - PersonalFlagsForOwner(characterId).Count);
        var availableInventory = InventoryQuantity(characterId, "personalFlag");
        if (availableInventory >= availableAllowance) return;
        AddInventory(characterId, "personalFlag", availableAllowance - availableInventory);
        await SaveInventoryAsync(characterId, cancellationToken);
    }

    public async Task<CanonicalEntity> PlaceFlagAsync(string characterId, PlaceFlagRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (player.LocationId != "outdoor") throw new InvalidOperationException("Flags can only be placed on the main map.");
        var label = new string((request.Label ?? string.Empty).Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (label.Length == 0) throw new InvalidOperationException("Enter a name for the flag.");
        if (label.Length > 60) throw new InvalidOperationException("Flag names are limited to 60 characters.");
        var requestedPosition = new WorldPosition(player.Position.Region, request.X, request.Y, player.Position.Z);
        if (player.Position.Distance2D(requestedPosition) > 6) throw new InvalidOperationException("Move within six meters to place a flag.");
        if (!(_loadedBounds ?? Configuration.Area.Bounds).Contains(request.X, request.Y)) throw new InvalidOperationException("That location has not loaded yet.");
        var snapped = requestedPosition with { X = Math.Round(request.X * 2) / 2d, Y = Math.Round(request.Y * 2) / 2d };
        if (Navigation.IsBlocked(snapped.X, snapped.Y) || Navigation.TerrainAt(snapped.X, snapped.Y) == TerrainType.DeepWater)
            throw new InvalidOperationException("Choose an open location that is not deep water.");

        await _flagPlacementLock.WaitAsync(cancellationToken);
        try
        {
            if (PersonalFlagsForOwner(characterId).Count >= 5) throw new InvalidOperationException("You can place no more than five flags.");
            if (!RemoveInventory(characterId, "personalFlag", 1)) throw new InvalidOperationException("You do not have a personal flag available.");
            var position = snapped with { Z = Navigation.ElevationAt(snapped.X, snapped.Y) };
            var entity = new CanonicalEntity($"flag:{Guid.NewGuid():N}", EntityKind.PlayerStructure, position, Array.Empty<GeometryPoint>(),
                new Dictionary<string, string>
                {
                    ["objectType"] = "personalFlag",
                    ["owner"] = characterId,
                    ["ownerName"] = player.Name,
                    ["label"] = label
                }, IsBaseEntity: false);
            try
            {
                await SaveInventoryAsync(characterId, cancellationToken);
                await _store.SaveEntityAsync(Configuration.Id, entity, cancellationToken);
            }
            catch
            {
                AddInventory(characterId, "personalFlag", 1);
                await SaveInventoryAsync(characterId, CancellationToken.None);
                throw;
            }
            _realityEntities[entity.Id] = entity;
            return entity;
        }
        finally { _flagPlacementLock.Release(); }
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

    public WorldSnapshot CreateSnapshot() => CreateSnapshot(null);

    private WorldSnapshot CreateSnapshot(WorldMapWindow? map)
    {
        var lockSchedule = GetDoorLockSchedule();
        var activePlayerIds = _players.Keys.ToHashSet(StringComparer.Ordinal);
        var visibleRealityEntities = _realityEntities.Values
            .Where(entity => !IsPersonalFlag(entity) || activePlayerIds.Contains(entity.Properties.GetValueOrDefault("owner") ?? string.Empty))
            .OrderBy(entity => entity.Id).ToArray();
        return new(Configuration, _loadedBounds ?? Configuration.Area.Bounds,
            map?.BaseEntities ?? _baseEntities.Values.OrderBy(entity => entity.Id).ToArray(), visibleRealityEntities,
            _players.Values.OrderBy(player => player.Id).ToArray(), map?.Elevation ?? _elevationSamples.Values.ToArray(),
            Weather, _actors.Values.OrderBy(actor => actor.Id).ToArray(), _loadedAreas.Values.OrderBy(area => area.MinimumX).ThenBy(area => area.MinimumY).ToArray(),
            lockSchedule.Doors, lockSchedule.EndsAtUtc,
            _publicBaseClaims.Values.OrderBy(claim => claim.OwnerName).Select(claim => new PublicBaseState(claim.BuildingId, claim.OwnerName)).ToArray(),
            _loot.Values.Where(loot => loot.DropKind == "tombstone" && loot.LocationId == "outdoor").OrderBy(loot => loot.Id).ToArray(), AreaHazards: GetAreaHazards(), MapCoverage: map?.Coverage, Transit: GetTransitSnapshot());
    }

    private static bool IsPersonalFlag(CanonicalEntity entity) => entity.Kind == EntityKind.PlayerStructure &&
        string.Equals(entity.Properties.GetValueOrDefault("objectType"), "personalFlag", StringComparison.OrdinalIgnoreCase);

    private void ApplyGeneratedWorld(GeographicDataset generated)
    {
        var actorEntities = generated.Features.Where(entity => entity.Kind is EntityKind.Animal or EntityKind.Npc).ToList();
        var staticEntities = generated.Features.Where(entity => entity.Kind is not (EntityKind.Animal or EntityKind.Npc)).ToArray();
        _geographic ??= generated with { Features = staticEntities };
        foreach (var entity in staticEntities)
        {
            if (_removedBaseEntityIds.ContainsKey(entity.Id)) continue;
            if (_realityEntities.TryRemove(entity.Id, out var persistedOverride)) _baseEntities[entity.Id] = persistedOverride;
            else _baseEntities[entity.Id] = entity;
            var loaded = _baseEntities[entity.Id];
            if (loaded.Kind is EntityKind.Building or EntityKind.PointOfInterest && FoodBusinesses.OffersDelivery(loaded.Properties))
                _baseEntities[entity.Id] = loaded with { Properties = new Dictionary<string, string>(loaded.Properties) { ["merchantCategory"] = "food" } };
        }
        foreach (var sample in generated.Elevation) _elevationSamples[$"{sample.X:F1}:{sample.Y:F1}"] = sample;
        var bounds = generated.Area.Bounds;
        _loadedBounds = _loadedBounds is null ? bounds : new WorldBounds(Math.Min(_loadedBounds.MinimumX, bounds.MinimumX), Math.Min(_loadedBounds.MinimumY, bounds.MinimumY), Math.Max(_loadedBounds.MaximumX, bounds.MaximumX), Math.Max(_loadedBounds.MaximumY, bounds.MaximumY));
        _navigation = new WorldNavigation(_loadedBounds, _baseEntities.Values.Concat(_realityEntities.Values).ToArray(), _elevationSamples.Values.ToArray());
        PopulateWaterLife(generated);
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
        string[] looseItemTypes = ["pencil", "pen", "marker", "sprayPaint", "book", "calculator", "cellPhone", "rock", "arrow", "gallonOfGas"];
        var looseRandom = new Random(StableInt($"loose-items:{Configuration.Seed}:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}"));
        for (var itemIndex = 0; itemIndex < 14; itemIndex++)
        {
            var candidate = new WorldPosition(generated.Area.Region,
                bounds.MinimumX + looseRandom.NextDouble() * (bounds.MaximumX - bounds.MinimumX),
                bounds.MinimumY + looseRandom.NextDouble() * (bounds.MaximumY - bounds.MinimumY));
            var position = Navigation.FindNearestWalkable(candidate);
            var itemType = looseItemTypes[looseRandom.Next(looseItemTypes.Length)];
            var id = $"loot:world:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}:{itemIndex}";
            _loot.TryAdd(id, new LootDropState(id, position, "outdoor", 0, new[] { InventoryStack(itemType, 1) }, DateTimeOffset.MaxValue));
        }
        var newspaperRandom = new Random(StableInt($"newspapers:{Configuration.Seed}:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}"));
        var residentialBuildingIds = staticEntities.Where(entity => entity.Kind == EntityKind.Building && !entity.Properties.ContainsKey("merchantCategory")).Select(entity => entity.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var door in staticEntities.Where(entity => entity.Kind == EntityKind.Door).OrderBy(entity => entity.Id))
        {
            if (newspaperRandom.NextDouble() > .78) continue;
            var buildingId = door.Properties.GetValueOrDefault("buildingId");
            if (buildingId is null || !residentialBuildingIds.Contains(buildingId)) continue;
            var angle = double.TryParse(door.Properties.GetValueOrDefault("facingDegrees"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var degrees) ? degrees * Math.PI / 180 : 0;
            var nearDoor = door.Position with { X = door.Position.X + Math.Cos(angle) * 1.4, Y = door.Position.Y + Math.Sin(angle) * 1.4 };
            var position = Navigation.FindNearestWalkable(nearDoor);
            var id = $"loot:newspaper:{door.Id}";
            _loot.TryAdd(id, new LootDropState(id, position, "outdoor", 0, new[] { InventoryStack("newspaper", 1) }, DateTimeOffset.MaxValue));
        }
        var roadsideDoors = staticEntities.Where(entity => entity.Kind == EntityKind.Door).OrderBy(entity => entity.Id).ToArray();
        var mailboxRandom = new Random(StableInt($"mailboxes:{Configuration.Seed}:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}"));
        foreach (var door in roadsideDoors)
        {
            if (mailboxRandom.NextDouble() > .72) continue;
            var angle = double.TryParse(door.Properties.GetValueOrDefault("facingDegrees"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var degrees) ? degrees * Math.PI / 180 : 0;
            var position = Navigation.FindNearestWalkable(door.Position with { X = door.Position.X + Math.Cos(angle) * 3, Y = door.Position.Y + Math.Sin(angle) * 3 });
            var mailbox = new CanonicalEntity($"mailbox:{door.Id}", EntityKind.ResourceNode, position, Array.Empty<GeometryPoint>(), new Dictionary<string, string> { ["subtype"] = "mailbox", ["buildingId"] = door.Properties.GetValueOrDefault("buildingId") ?? string.Empty }, IsBaseEntity: true);
            if (!_removedBaseEntityIds.ContainsKey(mailbox.Id)) _baseEntities.TryAdd(mailbox.Id, mailbox);
        }
        var roads = staticEntities.Where(entity => entity.Kind == EntityKind.Road && entity.Geometry.Count > 1).ToArray();
        var postalRandom = new Random(StableInt($"postal-box:{Configuration.Seed}:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}"));
        WorldPosition postalCandidate;
        if (roads.Length > 0)
        {
            var road = roads[postalRandom.Next(roads.Length)]; var segment = postalRandom.Next(road.Geometry.Count - 1);
            var a = road.Geometry[segment]; var b = road.Geometry[segment + 1]; var dx = b.X - a.X; var dy = b.Y - a.Y; var length = Math.Max(.01, Math.Sqrt(dx * dx + dy * dy));
            var amount = .25 + postalRandom.NextDouble() * .5;
            var width = double.TryParse(road.Properties.GetValueOrDefault("widthMeters"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedWidth) ? parsedWidth : 6;
            var side = postalRandom.Next(2) == 0 ? -1 : 1;
            postalCandidate = new WorldPosition(generated.Area.Region, a.X + dx * amount - dy / length * (width / 2 + 2.2) * side, a.Y + dy * amount + dx / length * (width / 2 + 2.2) * side);
        }
        else postalCandidate = new WorldPosition(generated.Area.Region, (bounds.MinimumX + bounds.MaximumX) / 2, (bounds.MinimumY + bounds.MaximumY) / 2);
        var postalPosition = Navigation.FindNearestWalkable(postalCandidate);
        var postalId = $"postal-box:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}";
        _baseEntities.TryAdd(postalId, new CanonicalEntity(postalId, EntityKind.ResourceNode, postalPosition, Array.Empty<GeometryPoint>(),
            new Dictionary<string, string> { ["subtype"] = "postOfficeBox", ["displayName"] = "Postal drop box", ["collisionRadius"] = ".65" }, IsBaseEntity: true));
        var litterTypes = CraftingCatalog.LitterItems;
        for (var index = 0; index < Math.Min(18, roads.Length * 2); index++)
        {
            var road = roads[mailboxRandom.Next(roads.Length)]; var segment = mailboxRandom.Next(road.Geometry.Count - 1); var amount = mailboxRandom.NextDouble();
            var a = road.Geometry[segment]; var b = road.Geometry[segment + 1];
            var position = Navigation.FindNearestWalkable(new WorldPosition(generated.Area.Region, a.X + (b.X - a.X) * amount, a.Y + (b.Y - a.Y) * amount));
            var itemType = litterTypes[mailboxRandom.Next(litterTypes.Length)]; var id = $"loot:litter:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}:{index}";
            _loot.TryAdd(id, new LootDropState(id, position, "outdoor", 0, new[] { InventoryStack(itemType, 1) }, DateTimeOffset.MaxValue));
        }
        _navigation = new WorldNavigation(_loadedBounds, _baseEntities.Values.Concat(_realityEntities.Values).ToArray(), _elevationSamples.Values.ToArray());
        var residentRandom = new Random(StableInt($"additional-residents:{Configuration.Seed}:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}"));
        for (var residentIndex = 0; residentIndex < 12; residentIndex++)
        {
            var id = $"resident:additional:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}:{residentIndex}";
            var candidate = new WorldPosition(generated.Area.Region,
                bounds.MinimumX + residentRandom.NextDouble() * (bounds.MaximumX - bounds.MinimumX),
                bounds.MinimumY + residentRandom.NextDouble() * (bounds.MaximumY - bounds.MinimumY));
            actorEntities.Add(new CanonicalEntity(id, EntityKind.Npc, Navigation.FindNearestWalkable(candidate), Array.Empty<GeometryPoint>(),
                new Dictionary<string, string> { ["subtype"] = "resident", ["name"] = FriendlyHumanName(id, residentIndex) }));
        }
        foreach (var entity in actorEntities)
        {
            var safe = Navigation.FindNearestWalkable(entity.Position);
            var identity = StableInt(entity.Id) & int.MaxValue;
            var merchantCategory = entity.Properties.GetValueOrDefault("merchantCategory");
            var subtype = entity.Properties.GetValueOrDefault("subtype") ?? "unknown";
            var zombie = subtype == "zombie";
            var merchant = entity.Kind == EntityKind.Npc && !zombie && (merchantCategory is not null || identity % 4 == 0);
            var offersFoodDelivery = merchant && (entity.Properties.GetValueOrDefault("offersFoodDelivery") == "true" ||
                entity.Properties.GetValueOrDefault("sourceFeatureId") is { } sourceId && _baseEntities.TryGetValue(sourceId, out var source) && FoodBusinesses.OffersDelivery(source.Properties));
            var questGiver = offersFoodDelivery || entity.Kind == EntityKind.Npc && !zombie && !merchant && identity % 3 == 0;
            var maximumHealth = entity.Kind == EntityKind.Animal && entity.Properties.GetValueOrDefault("subtype") is "bear" or "cougar" ? 8 : 5;
            var travel = entity.Kind == EntityKind.Npc ? (TravelMode)(identity % 10 == 0 ? 3 : identity % 8 == 0 ? 2 : 0) : TravelMode.Walk;
            var preferredName = entity.Properties.GetValueOrDefault("name") ?? "Wanderer";
            var actorName = entity.Kind == EntityKind.Npc ? UniqueNpcName(preferredName, entity.Id) : UniqueAnimalName(subtype, preferredName, entity.Id);
            _actors[entity.Id] = new ActorState(entity.Id, entity.Kind, subtype,
                actorName, safe, HealthHearts: maximumHealth, MaximumHealthHearts: maximumHealth,
                IsMerchant: merchant, TravelMode: travel, MerchantCategory: merchantCategory,
                EquippedWeapon: merchant ? "pistol" : zombie ? "fist" : "none", IsQuestGiver: questGiver, OffersFoodDelivery: offersFoodDelivery);
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
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (_loadedAreas.ContainsKey(key)) return false;
            _activeMapOperations["area-load"] = $"Activating map block {key}";
            var areaConfiguration = AreaConfiguration(cellX, cellY);
            var generated = await _generator.GenerateAsync(areaConfiguration, cancellationToken);
            ApplyGeneratedWorld(generated);
            _loadedAreas[key] = generated.Area.Bounds;
            Interlocked.Increment(ref _transitRevision);
            return true;
        }
        finally
        {
            stopwatch.Stop();
            Interlocked.Exchange(ref _lastAreaLoadMilliseconds, stopwatch.ElapsedMilliseconds);
            _activeMapOperations.TryRemove("area-load", out _);
            _areaLoadLock.Release();
        }
    }

    public Task<bool> LoadAreaAsync(double x,double y,CancellationToken cancellationToken=default)=>EnsureAreaLoadedAsync(x,y,cancellationToken);

    public async Task<AreaPrefetchResult> PrefetchAreasAsync(PrefetchAreaRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _areaPrefetchLock.WaitAsync(0, cancellationToken))
            return new AreaPrefetchResult(0, 0, LastAreaPrefetchMilliseconds, true);
        var stopwatch = Stopwatch.StartNew();
        var prepared = 0;
        var alreadyPrepared = 0;
        try
        {
            var (targetX, targetY) = AreaCellFor(request.X, request.Y);
            var (originX, originY) = AreaCellFor(request.OriginX, request.OriginY);
            var stepX = Math.Sign(targetX - originX);
            var stepY = Math.Sign(targetY - originY);
            var hasDirection = stepX != 0 || stepY != 0;
            if (stepX != 0 && stepY != 0)
            {
                if (Math.Abs(request.X - request.OriginX) >= Math.Abs(request.Y - request.OriginY)) stepY = 0;
                else stepX = 0;
            }
            (int X, int Y)[] orderedCells;
            if (!hasDirection)
            {
                orderedCells =
                [
                    (targetX + 1, targetY), (targetX - 1, targetY),
                    (targetX, targetY + 1), (targetX, targetY - 1),
                    (targetX + 1, targetY + 1), (targetX + 1, targetY - 1),
                    (targetX - 1, targetY + 1), (targetX - 1, targetY - 1)
                ];
            }
            else
            {
                var perpendicularX = -stepY;
                var perpendicularY = stepX;
                orderedCells =
                [
                    (targetX + perpendicularX, targetY + perpendicularY),
                    (targetX - perpendicularX, targetY - perpendicularY),
                    (targetX + stepX, targetY + stepY),
                    (targetX + stepX + perpendicularX, targetY + stepY + perpendicularY),
                    (targetX + stepX - perpendicularX, targetY + stepY - perpendicularY)
                ];
            }
            var candidates = orderedCells.Distinct().Where(cell => !_loadedAreas.ContainsKey($"{cell.X}:{cell.Y}")).ToArray();

            for (var index = 0; index < candidates.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cell = candidates[index];
                var areaConfiguration = AreaConfiguration(cell.X, cell.Y);
                _activeMapOperations["area-prefetch"] = $"Preparing nearby map {index + 1}/{candidates.Length}";
                if (_generator.IsGeneratedWorldCached(areaConfiguration)) alreadyPrepared++;
                else
                {
                    await _generator.GenerateAsync(areaConfiguration, cancellationToken);
                    prepared++;
                }
            }
            Interlocked.Add(ref _preparedAreaCount, prepared);
            return new AreaPrefetchResult(prepared, alreadyPrepared, stopwatch.ElapsedMilliseconds, false);
        }
        finally
        {
            stopwatch.Stop();
            Interlocked.Exchange(ref _lastAreaPrefetchMilliseconds, stopwatch.ElapsedMilliseconds);
            _activeMapOperations.TryRemove("area-prefetch", out _);
            _areaPrefetchLock.Release();
        }
    }

    public bool IsAreaLoaded(double x, double y) => _loadedAreas.ContainsKey(AreaKeyFor(x, y));
    public bool IsAreaLoadRequiredForPath(string characterId, double x, double y) =>
        _players.TryGetValue(characterId, out var player) && player.LocationId == "outdoor" && !IsAreaLoaded(x, y);

    public string AreaKeyFor(double x, double y)
    {
        var (cellX, cellY) = AreaCellFor(x, y);
        return $"{cellX}:{cellY}";
    }

    private (int X, int Y) AreaCellFor(double x, double y)
    {
        var origin = new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center);
        var size = Configuration.Area.SizeMeters;
        return ((int)Math.Floor((x - (origin.X - size / 2d)) / size), (int)Math.Floor((y - (origin.Y - size / 2d)) / size));
    }

    private RealityConfiguration AreaConfiguration(int cellX, int cellY)
    {
        var projection = new LocalTangentProjection(Configuration.Area.Region);
        var origin = projection.Project(Configuration.Area.Center);
        var centerPosition = new WorldPosition(Configuration.Area.Region,
            origin.X + cellX * Configuration.Area.SizeMeters,
            origin.Y + cellY * Configuration.Area.SizeMeters);
        var center = projection.Unproject(centerPosition);
        if (RegionId.FromGeo(center) != Configuration.Area.Region)
            throw new InvalidOperationException("This prototype reached a geographic projection boundary. Cross-region Earth streaming is the next world-scale milestone.");
        return Configuration with { Area = new GeographicArea(center, Configuration.Area.SizeMeters) };
    }

    private Queue<WorldPosition> CreateInteriorActorRoute(ActorState actor, DungeonState dungeon)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var point = new GeometryPoint(.6 + _actorRandom.NextDouble() * Math.Max(.1, dungeon.Width - 1.2), .6 + _actorRandom.NextDouble() * Math.Max(.1, dungeon.Height - 1.2));
            if (!PointInsideFootprint(point, dungeon.Footprint) || dungeon.Footprint is { Count: >= 3 } footprint && DistanceToFootprint(point, footprint) < .5) continue;
            var target = new WorldPosition(actor.Position.Region, point.X, point.Y);
            if (dungeon.Walls.Any(wall => CrossesDungeonWall(actor.Position, target, wall))) continue;
            return new Queue<WorldPosition>(new[] { target });
        }
        return new Queue<WorldPosition>();
    }

    private static double ActorSpeed(string subtype) => subtype switch
    {
        "rabbit" => 2.2, "dog" => 1.8, "cat" => 1.4, "bird" => 2.6,
        "deer" => 2.0, "cougar" => 1.7, "bear" => 1.2, "eventBear" => 4, "tRex" => 6,
        "brontosaurus" => 3, "stegosaurus" => 3.5, "raptor" => 8, "giant" => 4.5, "zombie" => .55, "swatOfficer" => 2.5, "storeEmployee" => 1.1, _ => 1.25
    };

    private string ActorSpeech(ActorState actor)
    {
        if (actor.Subtype == "ufo") return "VMMMMMMMM…";
        if (actor.Subtype == "tRex") return "ROOOAAAR!";
        if (actor.Subtype == "brontosaurus") return "BWOOOOOM!";
        if (actor.Subtype == "stegosaurus") return "HRRROOOH!";
        if (actor.Subtype == "raptor") return "SKREEEE!";
        if (actor.Subtype == "giant") return "FEE-FI-FO-FUM!";
        if (actor.Subtype == "zombie") return _actorRandom.Next(2) == 0 ? "braaains..." : "uurrrgh...";
        if (actor.Kind == EntityKind.Npc)
        {
            if (actor.Subtype == "policeOfficer") return "Stop! You're under arrest!";
            if (actor.IsMerchant)
            {
                var offers = BaseMerchantOffers(actor);
                var offer = offers[_actorRandom.Next(offers.Length)];
                var displayName = offer.DisplayName ?? (_itemConfigurations.TryGetValue(offer.ItemType, out var good) ? good.DisplayName : offer.ItemType);
                return $"For sale! {displayName} for ${offer.UnitPriceCents / 100.0:F2} today. Friends pay less!";
            }
            if (actor.IsQuestGiver && _actorRandom.NextDouble() < .55) return "I could use your help. Come talk to me!";
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

    private static string ActorDisplayName(ActorState actor) => actor.Name;

    private const double MetersPerMile = 1609.344;
    // Representative US MPG: Yamaha XT250 (dual sport) and MT-07 specifications.
    private const double DirtBikeMilesPerGallon = 76;
    private const double MotorcycleMilesPerGallon = 57;
    private const double UfoMetersPerKryptonite = 10 * MetersPerMile;
    private double TravelFuelRange(PlayerState player) => player.TravelMode switch
    {
        TravelMode.DirtBike => player.DirtBikeGasGallons * DirtBikeMilesPerGallon * MetersPerMile,
        TravelMode.Motorcycle => player.MotorcycleGasGallons * MotorcycleMilesPerGallon * MetersPerMile,
        TravelMode.EBike => player.EBikeRemainingMeters,
        TravelMode.Ufo => player.UfoRemainingMeters + InventoryQuantity(player.Id, "kryptonite") * UfoMetersPerKryptonite,
        _ => double.MaxValue
    };
    private double StaminaAfterTravel(PlayerState player, double distance, double elapsed, DateTimeOffset now, bool magicShoes)
    {
        if (player.GodMode || distance <= .001) return player.Stamina;
        if (player.TravelMode == TravelMode.Swim) return Math.Max(0, player.Stamina - elapsed * 2 * (1 + 2 * (1 - player.Air / Math.Max(1, player.MaximumAir))));
        if (player.FoodProtectedUntilUtc > now) return player.Stamina;
        var effort = player.TravelMode switch { TravelMode.Run => 1, TravelMode.Bike => .5, TravelMode.Skateboard => .75, _ => 0 };
        return Math.Max(0, player.Stamina - WorldNavigation.RunningStaminaDrain(elapsed, magicShoes && player.TravelMode == TravelMode.Run) * effort * ProgressionRules.Drain(StatsFor(player.Id)) * (1 + 2 * (1 - player.Air / Math.Max(1, player.MaximumAir))));
    }
    private static bool IsMotorized(TravelMode mode) => mode is TravelMode.DirtBike or TravelMode.Motorcycle;
    private static double FuelGallons(PlayerState player) => player.TravelMode == TravelMode.DirtBike ? player.DirtBikeGasGallons : player.MotorcycleGasGallons;
    private static double FuelAfterTravel(double gallons, double meters, double milesPerGallon) => Math.Max(0, gallons - meters / MetersPerMile / milesPerGallon);
    private static string VehicleName(TravelMode mode) => mode == TravelMode.DirtBike ? "dirt bike" : "motorcycle";

    private PlayerState ResetPlayer(PlayerState player)
    {
        player = player with { RidingBusId = null, WaitingAtBusStopId = null, Air = player.MaximumAir, SwimExhausted = false };
        var home = HomeForPlayer(player.Id);
        if (home is not null)
        {
            SetBaseReturnPosition(player.Id, home.BuildingId);
            return player with { Position = home.Exit, Terrain = TerrainType.Pavement, SpeedMetersPerSecond = 0,
                HealthHearts = 10, Stamina = ProgressionRules.Stamina(StatsFor(player.Id)), MaximumStamina = ProgressionRules.Stamina(StatsFor(player.Id)), Water = 10, BodyHeat = 50, TravelMode = TravelMode.Walk, LocationId = home.Id,
                EquippedWeapon = player.EquippedWeapon == "probulator" ? "fist" : player.EquippedWeapon,
                FoodProtectedUntilUtc = null, WaterProtectedUntilUtc = null, EnergyDrinkBoostUntilUtc = null, EnergyDrinkCrashUntilUtc = null, ProbedUntilUtc = null, CandleUntilUtc = null, Version = player.Version + 1 };
        }
        var spawn = Navigation.FindNearestWalkable(new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
        return player with { Position = spawn, Terrain = Navigation.TerrainAt(spawn.X, spawn.Y), SpeedMetersPerSecond = 0,
            HealthHearts = 10, Stamina = ProgressionRules.Stamina(StatsFor(player.Id)), MaximumStamina = ProgressionRules.Stamina(StatsFor(player.Id)), Water = 10, BodyHeat = 50, TravelMode = TravelMode.Walk, LocationId = "outdoor",
            EquippedWeapon = player.EquippedWeapon == "probulator" ? "fist" : player.EquippedWeapon,
            FoodProtectedUntilUtc = null, WaterProtectedUntilUtc = null, EnergyDrinkBoostUntilUtc = null, EnergyDrinkCrashUntilUtc = null, ProbedUntilUtc = null, CandleUntilUtc = null, Version = player.Version + 1 };
    }

    private async Task<bool> SavePlayerAsync(PlayerState player, CancellationToken cancellationToken, int kryptoniteUsed = 0)
    {
        var saveLock = _playerSaveLocks.GetOrAdd(player.Id, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            if (player.IsTestCharacter && !_players.ContainsKey(player.Id)) return false;
            // Periodic simulation work may have captured this player before a
            // doorway transition completed. Never let that older snapshot
            // overwrite a newer authoritative location or its persisted state.
            if (_players.TryGetValue(player.Id, out var current) && current.Version >= player.Version) return false;
            if (kryptoniteUsed > 0 && !RemoveInventory(player.Id, "kryptonite", kryptoniteUsed))
                throw new InvalidOperationException("Your UFO needs Kryptonite to continue flying.");
            try
            {
                if (!player.IsTestCharacter) await _store.SaveCharacterAsync(Configuration.Id,
                    TransitPersistenceState(player.Abduction is { } abduction ? player with { Position = abduction.Origin, Abduction = null } : player),
                    cancellationToken, kryptoniteUsed > 0 ? GetInventoryState(player.Id) : null);
            }
            catch
            {
                if (kryptoniteUsed > 0) AddInventory(player.Id, "kryptonite", kryptoniteUsed);
                throw;
            }
            _players[player.Id] = player;
            return true;
        }
        finally { saveLock.Release(); }
    }

    private WorldNavigation Navigation => _navigation ?? throw new InvalidOperationException("World navigation is not initialized.");

    private static string SanitizeName(string value)
    {
        var cleaned = new string((value ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Explorer" : cleaned[..Math.Min(cleaned.Length, 24)];
    }
}

public sealed record MovementOutcome(PlayerState Player, bool Moved, bool Blocked, bool Drowned, bool Fell, bool Died, string? Message);
