using System.Text.Json;
using AlternateEarth.Shared;
using Microsoft.Data.Sqlite;

namespace AlternateEarth.Server;

public sealed class SqliteRealityStore
{
    private readonly string _connectionString;

    public SqliteRealityStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
    }

    public async Task InitializeAsync(RealityConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS Reality (
                Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Seed INTEGER NOT NULL,
                ConfigurationJson TEXT NOT NULL, CreatedUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Characters (
                Id TEXT PRIMARY KEY, RealityId TEXT NOT NULL, Name TEXT NOT NULL,
                RegionLatitude INTEGER NOT NULL, RegionLongitude INTEGER NOT NULL,
                X REAL NOT NULL, Y REAL NOT NULL, Z REAL NOT NULL,
                Health REAL NOT NULL DEFAULT 10, TravelMode TEXT NOT NULL DEFAULT 'Walk', Stamina REAL NOT NULL DEFAULT 10,
                Version INTEGER NOT NULL, UpdatedUtc TEXT NOT NULL,
                FOREIGN KEY (RealityId) REFERENCES Reality(Id)
            );
            CREATE TABLE IF NOT EXISTS RealityDeltas (
                EntityId TEXT PRIMARY KEY, RealityId TEXT NOT NULL, Operation TEXT NOT NULL,
                Kind TEXT, RegionLatitude INTEGER, RegionLongitude INTEGER,
                X REAL, Y REAL, Z REAL, GeometryJson TEXT, PropertiesJson TEXT,
                Version INTEGER NOT NULL, UpdatedUtc TEXT NOT NULL,
                FOREIGN KEY (RealityId) REFERENCES Reality(Id)
            );
            CREATE TABLE IF NOT EXISTS Inventories (
                OwnerId TEXT NOT NULL, Slot INTEGER NOT NULL, ItemType TEXT NOT NULL,
                Quantity INTEGER NOT NULL, MetadataJson TEXT NOT NULL DEFAULT '{}',
                PRIMARY KEY (OwnerId, Slot)
            );
            CREATE TABLE IF NOT EXISTS Containers (
                EntityId TEXT PRIMARY KEY, Capacity INTEGER NOT NULL, InventoryOwnerId TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ServerSettings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Permissions (
                SubjectId TEXT NOT NULL, Permission TEXT NOT NULL,
                PRIMARY KEY (SubjectId, Permission)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "TravelMode", "TEXT NOT NULL DEFAULT 'Walk'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "Stamina", "REAL NOT NULL DEFAULT 10", cancellationToken);

        command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Reality (Id, Name, Seed, ConfigurationJson, CreatedUtc)
            VALUES ($id, $name, $seed, $configuration, $created)
            ON CONFLICT(Id) DO UPDATE SET Name = excluded.Name, ConfigurationJson = excluded.ConfigurationJson;
            """;
        command.Parameters.AddWithValue("$id", configuration.Id);
        command.Parameters.AddWithValue("$name", configuration.Name);
        command.Parameters.AddWithValue("$seed", configuration.Seed);
        command.Parameters.AddWithValue("$configuration", JsonSerializer.Serialize(configuration, SharedJson.Options));
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CanonicalEntity>> LoadActiveEntitiesAsync(string realityId, CancellationToken cancellationToken = default)
    {
        var entities = new List<CanonicalEntity>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT EntityId, Kind, RegionLatitude, RegionLongitude, X, Y, Z, GeometryJson, PropertiesJson, Version FROM RealityDeltas WHERE RealityId = $reality AND Operation = 'upsert'";
        command.Parameters.AddWithValue("$reality", realityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entities.Add(new CanonicalEntity(
                reader.GetString(0),
                Enum.Parse<EntityKind>(reader.GetString(1), true),
                new WorldPosition(new RegionId(reader.GetInt32(2), reader.GetInt32(3)), reader.GetDouble(4), reader.GetDouble(5), reader.GetDouble(6)),
                JsonSerializer.Deserialize<GeometryPoint[]>(reader.GetString(7), SharedJson.Options) ?? Array.Empty<GeometryPoint>(),
                JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(8), SharedJson.Options) ?? new Dictionary<string, string>(),
                reader.GetInt64(9),
                false));
        }
        return entities;
    }

    public async Task SaveEntityAsync(string realityId, CanonicalEntity entity, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RealityDeltas
                (EntityId, RealityId, Operation, Kind, RegionLatitude, RegionLongitude, X, Y, Z, GeometryJson, PropertiesJson, Version, UpdatedUtc)
            VALUES
                ($id, $reality, 'upsert', $kind, $regionLat, $regionLon, $x, $y, $z, $geometry, $properties, $version, $updated)
            ON CONFLICT(EntityId) DO UPDATE SET
                Operation = 'upsert', Kind = excluded.Kind, RegionLatitude = excluded.RegionLatitude,
                RegionLongitude = excluded.RegionLongitude, X = excluded.X, Y = excluded.Y, Z = excluded.Z,
                GeometryJson = excluded.GeometryJson, PropertiesJson = excluded.PropertiesJson,
                Version = excluded.Version, UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$id", entity.Id);
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$kind", entity.Kind.ToString());
        command.Parameters.AddWithValue("$regionLat", entity.Position.Region.LatitudeBand);
        command.Parameters.AddWithValue("$regionLon", entity.Position.Region.LongitudeBand);
        command.Parameters.AddWithValue("$x", entity.Position.X);
        command.Parameters.AddWithValue("$y", entity.Position.Y);
        command.Parameters.AddWithValue("$z", entity.Position.Z);
        command.Parameters.AddWithValue("$geometry", JsonSerializer.Serialize(entity.Geometry, SharedJson.Options));
        command.Parameters.AddWithValue("$properties", JsonSerializer.Serialize(entity.Properties, SharedJson.Options));
        command.Parameters.AddWithValue("$version", entity.Version);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveEntityAsync(string realityId, CanonicalEntity entity, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RealityDeltas (EntityId, RealityId, Operation, Version, UpdatedUtc)
            VALUES ($id, $reality, 'removed', $version, $updated)
            ON CONFLICT(EntityId) DO UPDATE SET Operation = 'removed', Version = excluded.Version, UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$id", entity.Id);
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$version", entity.Version + 1);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PlayerState?> LoadCharacterAsync(string realityId, string characterId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Name, RegionLatitude, RegionLongitude, X, Y, Z, Version, Health, TravelMode, Stamina FROM Characters WHERE RealityId = $reality AND Id = $id";
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$id", characterId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new PlayerState(characterId, reader.GetString(0),
            new WorldPosition(new RegionId(reader.GetInt32(1), reader.GetInt32(2)), reader.GetDouble(3), reader.GetDouble(4), reader.GetDouble(5)),
            reader.GetInt64(6), HealthHearts: Math.Clamp(reader.GetDouble(7), 0, 10),
            TravelMode: Enum.TryParse<TravelMode>(reader.GetString(8), true, out var mode) ? mode : TravelMode.Walk,
            Stamina: Math.Clamp(reader.GetDouble(9), 0, 10));
    }

    public async Task SaveCharacterAsync(string realityId, PlayerState player, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Characters (Id, RealityId, Name, RegionLatitude, RegionLongitude, X, Y, Z, Health, TravelMode, Stamina, Version, UpdatedUtc)
            VALUES ($id, $reality, $name, $regionLat, $regionLon, $x, $y, $z, $health, $travelMode, $stamina, $version, $updated)
            ON CONFLICT(Id) DO UPDATE SET Name = excluded.Name, X = excluded.X, Y = excluded.Y, Z = excluded.Z,
                Health = excluded.Health, TravelMode = excluded.TravelMode, Stamina = excluded.Stamina,
                Version = excluded.Version, UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$id", player.Id);
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$name", player.Name);
        command.Parameters.AddWithValue("$regionLat", player.Position.Region.LatitudeBand);
        command.Parameters.AddWithValue("$regionLon", player.Position.Region.LongitudeBand);
        command.Parameters.AddWithValue("$x", player.Position.X);
        command.Parameters.AddWithValue("$y", player.Position.Y);
        command.Parameters.AddWithValue("$z", player.Position.Z);
        command.Parameters.AddWithValue("$health", player.HealthHearts);
        command.Parameters.AddWithValue("$travelMode", player.TravelMode.ToString());
        command.Parameters.AddWithValue("$stamina", player.Stamina);
        command.Parameters.AddWithValue("$version", player.Version);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearRealityDeltasAsync(string realityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RealityDeltas WHERE RealityId = $reality";
        command.Parameters.AddWithValue("$reality", realityId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        var exists = false;
        await using (var reader = await check.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                exists |= string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase);
        if (exists) return;
        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
