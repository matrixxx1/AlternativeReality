using System.Collections.Concurrent;
using AlternateEarth.Geo;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed class RealityWorld
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
    private readonly Random _actorRandom;
    private GeographicDataset? _geographic;
    private WorldNavigation? _navigation;

    public RealityWorld(RealityConfiguration configuration, DeterministicWorldGenerator generator, IWeatherProvider weatherProvider, SqliteRealityStore store)
    {
        Configuration = configuration;
        _generator = generator;
        _weatherProvider = weatherProvider;
        _store = store;
        _actorRandom = new Random(unchecked((int)configuration.Seed));
    }

    public RealityConfiguration Configuration { get; }
    public int PlayerCount => _players.Count;
    public int BaseEntityCount => (_geographic?.Features.Count ?? 0) + _actors.Count;
    public int RealityEntityCount => _realityEntities.Count;
    public string GeographicProvider => _geographic?.Provider ?? "not loaded";
    public WeatherState Weather { get; private set; } = WeatherState.Unavailable;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entity in await _store.LoadActiveEntitiesAsync(Configuration.Id, cancellationToken))
            _realityEntities[entity.Id] = entity;
        ApplyGeneratedWorld(await _generator.GenerateAsync(Configuration, cancellationToken));
        await RefreshWeatherAsync(cancellationToken);
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

    public async Task<PlayerState> JoinAsync(string characterId, string requestedName, CancellationToken cancellationToken = default)
    {
        if (_players.Count >= Configuration.MaximumPlayers) throw new InvalidOperationException("This reality is full.");
        var name = SanitizeName(requestedName);
        var existing = await _store.LoadCharacterAsync(Configuration.Id, characterId, cancellationToken);
        var center = new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center);
        var position = existing?.Position ?? Navigation.FindNearestWalkable(center);
        if (Navigation.IsBlocked(position.X, position.Y) || Navigation.TerrainAt(position.X, position.Y) == TerrainType.DeepWater)
            position = Navigation.FindNearestWalkable(center);
        position = position with { Z = Navigation.ElevationAt(position.X, position.Y) };
        var health = existing is null || existing.HealthHearts <= 0 ? 10 : Math.Clamp(existing.HealthHearts, .25, 10);
        var player = new PlayerState(characterId, name, position, (existing?.Version ?? 0) + 1,
            Navigation.TerrainAt(position.X, position.Y), 0, health, 10, existing?.TravelMode ?? TravelMode.Walk,
            Math.Clamp(existing?.Stamina ?? 10, 0, 10), 10);
        _players[characterId] = player;
        _lastMovement[characterId] = DateTimeOffset.UtcNow;
        await _store.SaveCharacterAsync(Configuration.Id, player, cancellationToken);
        return player;
    }

    public void Leave(string characterId)
    {
        _players.TryRemove(characterId, out _);
        _lastMovement.TryRemove(characterId, out _);
        _lastChat.TryRemove(characterId, out _);
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
        var length = Math.Sqrt((request.X * request.X) + (request.Y * request.Y));
        var directionX = length > 1 ? request.X / length : request.X;
        var directionY = length > 1 ? request.Y / length : request.Y;
        var now = DateTimeOffset.UtcNow;
        var previous = _lastMovement.AddOrUpdate(characterId, now, (_, old) => now);
        var elapsed = Math.Clamp((now - previous).TotalSeconds, 0.01, 0.15);
        var currentTerrain = Navigation.TerrainAt(player.Position.X, player.Position.Y);
        var staminaFraction = player.MaximumStamina <= 0 ? 0 : player.Stamina / player.MaximumStamina;
        var metersPerSecond = Navigation.SpeedFor(currentTerrain, player.TravelMode, staminaFraction);
        var requested = Configuration.Area.Bounds.Clamp(player.Position with
        {
            X = player.Position.X + (directionX * metersPerSecond * elapsed * Configuration.GameSpeed),
            Y = player.Position.Y + (directionY * metersPerSecond * elapsed * Configuration.GameSpeed)
        });

        var requestedTerrain = Navigation.TerrainAt(requested.X, requested.Y);
        if (player.TravelMode == TravelMode.Skateboard && !WorldNavigation.SupportsTravelMode(requestedTerrain, TravelMode.Skateboard))
        {
            var damaged = player with { HealthHearts = Math.Max(0, player.HealthHearts - .25), TravelMode = TravelMode.Walk, SpeedMetersPerSecond = 0, Terrain = currentTerrain, Version = player.Version + 1 };
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
        if (nextTerrain == TerrainType.DeepWater)
        {
            var reset = ResetPlayer(player with { HealthHearts = 0 });
            await SavePlayerAsync(reset, cancellationToken);
            return new(reset, true, false, true, false, true, "You drowned, lost all hearts, and returned to the starting point.");
        }

        var distance = player.Position.Distance2D(next);
        var updated = player with
        {
            Position = next with { Z = Navigation.ElevationAt(next.X, next.Y) }, Terrain = nextTerrain,
            SpeedMetersPerSecond = distance > .001 ? distance / elapsed : 0,
            Stamina = player.TravelMode == TravelMode.Run && distance > .001 ? Math.Max(0, player.Stamina - (.45 * elapsed)) : player.Stamina,
            Version = player.Version + 1
        };
        await SavePlayerAsync(updated, cancellationToken);
        return new(updated, distance > .001, blocked && distance <= .001, false, false, false, null);
    }

    public async Task<PlayerState> SetTravelModeAsync(string characterId, TravelMode mode, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        var updated = player with { TravelMode = mode, SpeedMetersPerSecond = 0, Version = player.Version + 1 };
        await SavePlayerAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<IReadOnlyList<PlayerState>> AdvanceStaminaAsync(TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        var changed = new List<PlayerState>();
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _players.ToArray())
        {
            if (pair.Value.Stamina >= pair.Value.MaximumStamina ||
                !_lastMovement.TryGetValue(pair.Key, out var lastMove) || now - lastMove < TimeSpan.FromSeconds(1)) continue;
            var updated = pair.Value with
            {
                Stamina = Math.Min(pair.Value.MaximumStamina, pair.Value.Stamina + (.25 * elapsed.TotalSeconds)),
                SpeedMetersPerSecond = 0,
                Version = pair.Value.Version + 1
            };
            await SavePlayerAsync(updated, cancellationToken);
            changed.Add(updated);
        }
        return changed;
    }

    public async Task<PlayerState> TeleportAsync(string characterId, TeleportRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.GodMode) throw new InvalidOperationException("God Mode must be enabled to teleport.");
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        var requested = Configuration.Area.Bounds.Clamp(player.Position with { X = request.X, Y = request.Y });
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
        return updated;
    }

    public NavigationResult FindPath(string characterId, PathRequest request) =>
        _players.TryGetValue(characterId, out var player)
            ? Navigation.FindPath(player.Position, request.X, request.Y)
            : new(false, Array.Empty<WorldPosition>(), "Unknown player.");

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
        if (!godMode || !_players.ContainsKey(characterId)) throw new InvalidOperationException("God Mode must be enabled to rebuild this area.");
        await _rebuildLock.WaitAsync(cancellationToken);
        try
        {
            await _store.ClearRealityDeltasAsync(Configuration.Id, cancellationToken);
            _realityEntities.Clear();
            ApplyGeneratedWorld(await _generator.GenerateAsync(Configuration, cancellationToken));
            foreach (var pair in _players.ToArray())
            {
                var safe = Navigation.IsBlocked(pair.Value.Position.X, pair.Value.Position.Y) || Navigation.TerrainAt(pair.Value.Position.X, pair.Value.Position.Y) == TerrainType.DeepWater
                    ? Navigation.FindNearestWalkable(pair.Value.Position)
                    : pair.Value.Position with { Z = Navigation.ElevationAt(pair.Value.Position.X, pair.Value.Position.Y) };
                await SavePlayerAsync(pair.Value with { Position = safe, Terrain = Navigation.TerrainAt(safe.X, safe.Y), SpeedMetersPerSecond = 0, Version = pair.Value.Version + 1 }, cancellationToken);
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
        if (!Configuration.Area.Bounds.Contains(request.X, request.Y)) throw new InvalidOperationException("Object is outside the reality bounds.");
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

    public WorldSnapshot CreateSnapshot() => new(Configuration, Configuration.Area.Bounds,
        _geographic?.Features ?? Array.Empty<CanonicalEntity>(), _realityEntities.Values.OrderBy(entity => entity.Id).ToArray(),
        _players.Values.OrderBy(player => player.Id).ToArray(), _geographic?.Elevation ?? Array.Empty<ElevationSample>(),
        Weather, _actors.Values.OrderBy(actor => actor.Id).ToArray());

    private void ApplyGeneratedWorld(GeographicDataset generated)
    {
        var actorEntities = generated.Features.Where(entity => entity.Kind is EntityKind.Animal or EntityKind.Npc).ToArray();
        var staticEntities = generated.Features.Where(entity => entity.Kind is not (EntityKind.Animal or EntityKind.Npc)).ToArray();
        _geographic = generated with { Features = staticEntities };
        _navigation = new WorldNavigation(Configuration.Area.Bounds, staticEntities.Concat(_realityEntities.Values).ToArray(), generated.Elevation);
        _actors.Clear();
        _actorRoutes.Clear();
        _nextActorSpeech.Clear();
        foreach (var entity in actorEntities)
        {
            var safe = Navigation.FindNearestWalkable(entity.Position);
            _actors[entity.Id] = new ActorState(entity.Id, entity.Kind, entity.Properties.GetValueOrDefault("subtype") ?? "unknown",
                entity.Properties.GetValueOrDefault("name") ?? "Wanderer", safe);
        }
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

    private PlayerState ResetPlayer(PlayerState player)
    {
        var spawn = Navigation.FindNearestWalkable(new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
        return player with { Position = spawn, Terrain = Navigation.TerrainAt(spawn.X, spawn.Y), SpeedMetersPerSecond = 0,
            HealthHearts = 10, Stamina = 10, TravelMode = TravelMode.Walk, Version = player.Version + 1 };
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
