using System.Collections.Concurrent;
using AlternateEarth.Geo;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed class RealityWorld
{
    private readonly DeterministicWorldGenerator _generator;
    private readonly SqliteRealityStore _store;
    private readonly ConcurrentDictionary<string, CanonicalEntity> _realityEntities = new();
    private readonly ConcurrentDictionary<string, PlayerState> _players = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastMovement = new();
    private GeographicDataset? _geographic;

    public RealityWorld(RealityConfiguration configuration, DeterministicWorldGenerator generator, SqliteRealityStore store)
    {
        Configuration = configuration;
        _generator = generator;
        _store = store;
    }

    public RealityConfiguration Configuration { get; }
    public int PlayerCount => _players.Count;
    public int BaseEntityCount => _geographic?.Features.Count ?? 0;
    public int RealityEntityCount => _realityEntities.Count;
    public string GeographicProvider => _geographic?.Provider ?? "not loaded";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _geographic = await _generator.GenerateAsync(Configuration, cancellationToken);
        foreach (var entity in await _store.LoadActiveEntitiesAsync(Configuration.Id, cancellationToken))
        {
            _realityEntities[entity.Id] = entity;
        }
    }

    public async Task<PlayerState> JoinAsync(string characterId, string requestedName, CancellationToken cancellationToken = default)
    {
        if (_players.Count >= Configuration.MaximumPlayers) throw new InvalidOperationException("This reality is full.");
        var name = SanitizeName(requestedName);
        var existing = await _store.LoadCharacterAsync(Configuration.Id, characterId, cancellationToken);
        var center = new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center);
        var player = existing is null
            ? new PlayerState(characterId, name, center)
            : existing with { Name = name, Version = existing.Version + 1 };
        _players[characterId] = player;
        _lastMovement[characterId] = DateTimeOffset.UtcNow;
        await _store.SaveCharacterAsync(Configuration.Id, player, cancellationToken);
        return player;
    }

    public void Leave(string characterId)
    {
        _players.TryRemove(characterId, out _);
        _lastMovement.TryRemove(characterId, out _);
    }

    public async Task<PlayerState?> MoveAsync(string characterId, MoveRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) return null;
        var length = Math.Sqrt((request.X * request.X) + (request.Y * request.Y));
        var directionX = length > 1 ? request.X / length : request.X;
        var directionY = length > 1 ? request.Y / length : request.Y;
        var now = DateTimeOffset.UtcNow;
        var previous = _lastMovement.AddOrUpdate(characterId, now, (_, old) => now);
        var elapsed = Math.Clamp((now - previous).TotalSeconds, 0.01, 0.15);
        const double metersPerSecond = 7.0;
        var updated = player with
        {
            Position = Configuration.Area.Bounds.Clamp(player.Position with
            {
                X = player.Position.X + (directionX * metersPerSecond * elapsed * Configuration.GameSpeed),
                Y = player.Position.Y + (directionY * metersPerSecond * elapsed * Configuration.GameSpeed)
            }),
            Version = player.Version + 1
        };
        _players[characterId] = updated;
        await _store.SaveCharacterAsync(Configuration.Id, updated, cancellationToken);
        return updated;
    }

    public async Task<CanonicalEntity> PlaceObjectAsync(string characterId, PlaceObjectRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        var requestedPosition = new WorldPosition(player.Position.Region, request.X, request.Y, player.Position.Z);
        if (player.Position.Distance2D(requestedPosition) > 5.0) throw new InvalidOperationException("Objects must be placed within five meters of the character.");
        if (!Configuration.Area.Bounds.Contains(request.X, request.Y)) throw new InvalidOperationException("Object is outside the reality bounds.");

        var snapped = requestedPosition with { X = Math.Round(request.X * 2) / 2.0, Y = Math.Round(request.Y * 2) / 2.0 };
        var type = string.IsNullOrWhiteSpace(request.ObjectType) ? "marker" : request.ObjectType[..Math.Min(request.ObjectType.Length, 32)];
        var entity = new CanonicalEntity(
            $"placed:{Guid.NewGuid():N}", EntityKind.PlayerStructure, snapped, Array.Empty<GeometryPoint>(),
            new Dictionary<string, string>
            {
                ["objectType"] = type,
                ["rotationDegrees"] = request.RotationDegrees.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                ["owner"] = characterId
            },
            IsBaseEntity: false);
        await _store.SaveEntityAsync(Configuration.Id, entity, cancellationToken);
        _realityEntities[entity.Id] = entity;
        return entity;
    }

    public async Task<CanonicalEntity> RemoveObjectAsync(string characterId, string entityId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(characterId, out var player)) throw new InvalidOperationException("Unknown player.");
        if (!_realityEntities.TryGetValue(entityId, out var entity)) throw new InvalidOperationException("Object does not exist or is part of the immutable base geography.");
        if (player.Position.Distance2D(entity.Position) > 5.0) throw new InvalidOperationException("Objects must be removed from within five meters.");
        if (!Configuration.BuildingDestruction) throw new InvalidOperationException("Object destruction is disabled in this reality.");
        await _store.RemoveEntityAsync(Configuration.Id, entity, cancellationToken);
        _realityEntities.TryRemove(entityId, out _);
        return entity;
    }

    public WorldSnapshot CreateSnapshot() => new(
        Configuration,
        Configuration.Area.Bounds,
        _geographic?.Features ?? Array.Empty<CanonicalEntity>(),
        _realityEntities.Values.OrderBy(entity => entity.Id).ToArray(),
        _players.Values.OrderBy(player => player.Id).ToArray(),
        _geographic?.Elevation ?? Array.Empty<ElevationSample>());

    private static string SanitizeName(string value)
    {
        var cleaned = new string((value ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Explorer" : cleaned[..Math.Min(cleaned.Length, 24)];
    }
}
