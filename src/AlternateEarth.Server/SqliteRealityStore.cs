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
                Water REAL NOT NULL DEFAULT 10, WalletCents INTEGER NOT NULL DEFAULT 0, GodMode INTEGER NOT NULL DEFAULT 0,
                FoodProtectedUntilUtc TEXT, WaterProtectedUntilUtc TEXT, LocationId TEXT NOT NULL DEFAULT 'outdoor',
                FlashlightOn INTEGER NOT NULL DEFAULT 0, LanternOn INTEGER NOT NULL DEFAULT 0, LaserOn INTEGER NOT NULL DEFAULT 0,
                MagicHikingShoesOn INTEGER NOT NULL DEFAULT 0, MagicRunningShoesOn INTEGER NOT NULL DEFAULT 0, HatOn INTEGER NOT NULL DEFAULT 0,
                DirtBikeGasGallons REAL NOT NULL DEFAULT 0, MotorcycleGasGallons REAL NOT NULL DEFAULT 0,
                EquippedWeapon TEXT NOT NULL DEFAULT 'fist',
                BodyHeat REAL NOT NULL DEFAULT 50, EquippedHat TEXT NOT NULL DEFAULT 'none',
                EquippedShirt TEXT NOT NULL DEFAULT 'none', EquippedPants TEXT NOT NULL DEFAULT 'none',
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
            CREATE TABLE IF NOT EXISTS PlayerRelationships (
                RealityId TEXT NOT NULL, PlayerId TEXT NOT NULL, ActorId TEXT NOT NULL, FriendRating REAL NOT NULL DEFAULT 0,
                PRIMARY KEY (RealityId, PlayerId, ActorId)
            );
            CREATE TABLE IF NOT EXISTS DungeonDiscovery (
                RealityId TEXT NOT NULL, PlayerId TEXT NOT NULL, DungeonId TEXT NOT NULL, Cell TEXT NOT NULL,
                PRIMARY KEY (RealityId, PlayerId, DungeonId, Cell)
            );
            CREATE TABLE IF NOT EXISTS WorldMapDiscovery (
                RealityId TEXT NOT NULL, PlayerId TEXT NOT NULL, AreaKey TEXT NOT NULL, PurchasedUtc TEXT NOT NULL,
                PRIMARY KEY (RealityId, PlayerId, AreaKey)
            );
            CREATE TABLE IF NOT EXISTS OpenedChests (
                RealityId TEXT NOT NULL, PlayerId TEXT NOT NULL, ChestId TEXT NOT NULL, OpenedUtc TEXT NOT NULL,
                PRIMARY KEY (RealityId, PlayerId, ChestId)
            );
            CREATE TABLE IF NOT EXISTS Accounts (
                Id TEXT PRIMARY KEY, Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                PasswordHash TEXT NOT NULL, PasswordSalt TEXT NOT NULL, SessionTokenHash TEXT NOT NULL,
                ActiveCharacterId TEXT NOT NULL, CreatedUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS AccountCharacters (
                Id TEXT PRIMARY KEY, AccountId TEXT NOT NULL, Name TEXT NOT NULL, CreatedUtc TEXT NOT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS AccountBases (
                AccountId TEXT NOT NULL, RealityId TEXT NOT NULL, BuildingId TEXT NOT NULL,
                RegionLatitude INTEGER, RegionLongitude INTEGER, X REAL, Y REAL,
                PRIMARY KEY (AccountId, RealityId)
            );
            CREATE TABLE IF NOT EXISTS HomeFurniture (
                AccountId TEXT NOT NULL, RealityId TEXT NOT NULL,
                FurnitureJson TEXT NOT NULL, UpdatedUtc TEXT NOT NULL,
                PRIMARY KEY (AccountId, RealityId)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "TravelMode", "TEXT NOT NULL DEFAULT 'Walk'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "Stamina", "REAL NOT NULL DEFAULT 10", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "Water", "REAL NOT NULL DEFAULT 10", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "WalletCents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "GodMode", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "FoodProtectedUntilUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "WaterProtectedUntilUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "LocationId", "TEXT NOT NULL DEFAULT 'outdoor'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "FlashlightOn", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "LanternOn", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "LaserOn", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "MagicHikingShoesOn", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "MagicRunningShoesOn", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "HatOn", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "DirtBikeGasGallons", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "MotorcycleGasGallons", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "EquippedWeapon", "TEXT NOT NULL DEFAULT 'fist'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "BodyHeat", "REAL NOT NULL DEFAULT 50", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "EquippedHat", "TEXT NOT NULL DEFAULT 'none'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "EquippedShirt", "TEXT NOT NULL DEFAULT 'none'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "EquippedPants", "TEXT NOT NULL DEFAULT 'none'", cancellationToken);
        await EnsureColumnAsync(connection, "AccountBases", "RegionLatitude", "INTEGER", cancellationToken);
        await EnsureColumnAsync(connection, "AccountBases", "RegionLongitude", "INTEGER", cancellationToken);
        await EnsureColumnAsync(connection, "AccountBases", "X", "REAL", cancellationToken);
        await EnsureColumnAsync(connection, "AccountBases", "Y", "REAL", cancellationToken);

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
        command.CommandText = "SELECT Name, RegionLatitude, RegionLongitude, X, Y, Z, Version, Health, TravelMode, Stamina, Water, WalletCents, GodMode, FoodProtectedUntilUtc, WaterProtectedUntilUtc, LocationId, FlashlightOn, LanternOn, LaserOn, MagicHikingShoesOn, MagicRunningShoesOn, HatOn, DirtBikeGasGallons, MotorcycleGasGallons, EquippedWeapon, BodyHeat, EquippedHat, EquippedShirt, EquippedPants FROM Characters WHERE RealityId = $reality AND Id = $id";
        command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$id", characterId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new PlayerState(characterId, reader.GetString(0),
            new WorldPosition(new RegionId(reader.GetInt32(1), reader.GetInt32(2)), reader.GetDouble(3), reader.GetDouble(4), reader.GetDouble(5)),
            reader.GetInt64(6), HealthHearts: Math.Clamp(reader.GetDouble(7), 0, 10),
            TravelMode: Enum.TryParse<TravelMode>(reader.GetString(8), true, out var mode) ? mode : TravelMode.Walk,
            Stamina: Math.Clamp(reader.GetDouble(9), 0, 10), Water: Math.Clamp(reader.GetDouble(10), 0, 10),
            WalletCents: reader.GetInt64(11), GodMode: reader.GetInt64(12) != 0,
            FoodProtectedUntilUtc: ReadDate(reader, 13), WaterProtectedUntilUtc: ReadDate(reader, 14),
            LocationId: reader.GetString(15), FlashlightOn: reader.GetInt64(16)!=0, LanternOn: reader.GetInt64(17)!=0, LaserOn: reader.GetInt64(18)!=0,
            MagicHikingShoesOn: reader.GetInt64(19)!=0, MagicRunningShoesOn: reader.GetInt64(20)!=0, HatOn: reader.GetInt64(21)!=0,
            DirtBikeGasGallons: Math.Clamp(reader.GetDouble(22), 0, 2), MotorcycleGasGallons: Math.Clamp(reader.GetDouble(23), 0, 4),
            EquippedWeapon: reader.IsDBNull(24) ? "fist" : reader.GetString(24), BodyHeat: Math.Clamp(reader.GetDouble(25), 0, 100),
            EquippedHat: reader.IsDBNull(26) ? "none" : reader.GetString(26), EquippedShirt: reader.IsDBNull(27) ? "none" : reader.GetString(27),
            EquippedPants: reader.IsDBNull(28) ? "none" : reader.GetString(28));
    }

    public async Task SaveCharacterAsync(string realityId, PlayerState player, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Characters (Id, RealityId, Name, RegionLatitude, RegionLongitude, X, Y, Z, Health, TravelMode, Stamina, Water, WalletCents, GodMode, FoodProtectedUntilUtc, WaterProtectedUntilUtc, LocationId, FlashlightOn, LanternOn, LaserOn, MagicHikingShoesOn, MagicRunningShoesOn, HatOn, DirtBikeGasGallons, MotorcycleGasGallons, EquippedWeapon, BodyHeat, EquippedHat, EquippedShirt, EquippedPants, Version, UpdatedUtc)
            VALUES ($id, $reality, $name, $regionLat, $regionLon, $x, $y, $z, $health, $travelMode, $stamina, $water, $wallet, $god, $foodUntil, $waterUntil, $location, $flashlight, $lantern, $laser, $magicHikingShoes, $magicRunningShoes, $hat, $dirtBikeGas, $motorcycleGas, $equippedWeapon, $bodyHeat, $equippedHat, $equippedShirt, $equippedPants, $version, $updated)
            ON CONFLICT(Id) DO UPDATE SET Name = excluded.Name, X = excluded.X, Y = excluded.Y, Z = excluded.Z,
                Health = excluded.Health, TravelMode = excluded.TravelMode, Stamina = excluded.Stamina, Water = excluded.Water,
                WalletCents = excluded.WalletCents, GodMode = excluded.GodMode,
                FoodProtectedUntilUtc = excluded.FoodProtectedUntilUtc, WaterProtectedUntilUtc = excluded.WaterProtectedUntilUtc,
                LocationId = excluded.LocationId, FlashlightOn=excluded.FlashlightOn, LanternOn=excluded.LanternOn, LaserOn=excluded.LaserOn,
                MagicHikingShoesOn=excluded.MagicHikingShoesOn, MagicRunningShoesOn=excluded.MagicRunningShoesOn, HatOn=excluded.HatOn,
                DirtBikeGasGallons=excluded.DirtBikeGasGallons, MotorcycleGasGallons=excluded.MotorcycleGasGallons,
                EquippedWeapon=excluded.EquippedWeapon, BodyHeat=excluded.BodyHeat, EquippedHat=excluded.EquippedHat,
                EquippedShirt=excluded.EquippedShirt, EquippedPants=excluded.EquippedPants,
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
        command.Parameters.AddWithValue("$water", player.Water);
        command.Parameters.AddWithValue("$wallet", player.WalletCents);
        command.Parameters.AddWithValue("$god", player.GodMode ? 1 : 0);
        command.Parameters.AddWithValue("$foodUntil", (object?)player.FoodProtectedUntilUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$waterUntil", (object?)player.WaterProtectedUntilUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$location", player.LocationId);
        command.Parameters.AddWithValue("$flashlight",player.FlashlightOn?1:0);
        command.Parameters.AddWithValue("$lantern",player.LanternOn?1:0);
        command.Parameters.AddWithValue("$laser",player.LaserOn?1:0);
        command.Parameters.AddWithValue("$magicHikingShoes", player.MagicHikingShoesOn ? 1 : 0);
        command.Parameters.AddWithValue("$magicRunningShoes", player.MagicRunningShoesOn ? 1 : 0);
        command.Parameters.AddWithValue("$hat", player.HatOn ? 1 : 0);
        command.Parameters.AddWithValue("$dirtBikeGas", player.DirtBikeGasGallons);
        command.Parameters.AddWithValue("$motorcycleGas", player.MotorcycleGasGallons);
        command.Parameters.AddWithValue("$equippedWeapon", player.EquippedWeapon);
        command.Parameters.AddWithValue("$bodyHeat", player.BodyHeat);
        command.Parameters.AddWithValue("$equippedHat", player.EquippedHat);
        command.Parameters.AddWithValue("$equippedShirt", player.EquippedShirt);
        command.Parameters.AddWithValue("$equippedPants", player.EquippedPants);
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

    public async Task ClearTransientWorldStateAsync(string realityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var statement in new[]
                 {
                     "DELETE FROM RealityDeltas WHERE RealityId=$reality",
                     "DELETE FROM PlayerRelationships WHERE RealityId=$reality",
                     "DELETE FROM DungeonDiscovery WHERE RealityId=$reality",
                     "DELETE FROM OpenedChests WHERE RealityId=$reality"
                 })
        {
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = statement;
            command.Parameters.AddWithValue("$reality", realityId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResetDungeonStateAsync(string realityId, string playerId, string dungeonId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var discovery = connection.CreateCommand(); discovery.Transaction = (SqliteTransaction)transaction;
        discovery.CommandText = "DELETE FROM DungeonDiscovery WHERE RealityId=$r AND PlayerId=$p AND DungeonId=$d";
        discovery.Parameters.AddWithValue("$r", realityId); discovery.Parameters.AddWithValue("$p", playerId); discovery.Parameters.AddWithValue("$d", dungeonId);
        await discovery.ExecuteNonQueryAsync(cancellationToken);
        var relationships = connection.CreateCommand(); relationships.Transaction = (SqliteTransaction)transaction;
        relationships.CommandText = "DELETE FROM PlayerRelationships WHERE RealityId=$r AND PlayerId=$p AND ActorId LIKE $prefix";
        relationships.Parameters.AddWithValue("$r", realityId); relationships.Parameters.AddWithValue("$p", playerId); relationships.Parameters.AddWithValue("$prefix", dungeonId + ":%");
        await relationships.ExecuteNonQueryAsync(cancellationToken);
        var chests = connection.CreateCommand(); chests.Transaction = (SqliteTransaction)transaction;
        chests.CommandText = "DELETE FROM OpenedChests WHERE RealityId=$r AND PlayerId=$p AND ChestId LIKE $prefix";
        chests.Parameters.AddWithValue("$r", realityId); chests.Parameters.AddWithValue("$p", playerId); chests.Parameters.AddWithValue("$prefix", dungeonId + ":%");
        await chests.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<InventoryState> LoadInventoryAsync(string playerId, CancellationToken cancellationToken = default)
    {
        var items = new List<ItemStack>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ItemType, Quantity FROM Inventories WHERE OwnerId = $owner ORDER BY Slot";
        command.Parameters.AddWithValue("$owner", playerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(new ItemStack(reader.GetString(0), reader.GetInt32(1)));
        return new InventoryState(playerId, items);
    }

    public async Task SaveInventoryAsync(InventoryState inventory, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var remove = connection.CreateCommand(); remove.Transaction = (SqliteTransaction)transaction;
        remove.CommandText = "DELETE FROM Inventories WHERE OwnerId = $owner"; remove.Parameters.AddWithValue("$owner", inventory.PlayerId);
        await remove.ExecuteNonQueryAsync(cancellationToken);
        for (var slot = 0; slot < inventory.Items.Count; slot++)
        {
            var item = inventory.Items[slot]; if (item.Quantity <= 0) continue;
            var insert = connection.CreateCommand(); insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = "INSERT INTO Inventories (OwnerId, Slot, ItemType, Quantity) VALUES ($owner,$slot,$type,$quantity)";
            insert.Parameters.AddWithValue("$owner", inventory.PlayerId); insert.Parameters.AddWithValue("$slot", slot);
            insert.Parameters.AddWithValue("$type", item.ItemType); insert.Parameters.AddWithValue("$quantity", item.Quantity);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemConfiguration>> LoadItemConfigurationsAsync(string realityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM ServerSettings WHERE Key=$key"; command.Parameters.AddWithValue("$key", $"item-configuration:{realityId}");
        var json = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? Array.Empty<ItemConfiguration>() : JsonSerializer.Deserialize<ItemConfiguration[]>(json, SharedJson.Options) ?? Array.Empty<ItemConfiguration>();
    }

    public async Task SaveItemConfigurationsAsync(string realityId, IReadOnlyList<ItemConfiguration> items, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ServerSettings (Key,Value) VALUES ($key,$value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value";
        command.Parameters.AddWithValue("$key", $"item-configuration:{realityId}"); command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(items, SharedJson.Options));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<MovementConfiguration?> LoadMovementConfigurationAsync(string realityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM ServerSettings WHERE Key=$key"; command.Parameters.AddWithValue("$key", $"movement-configuration:{realityId}");
        var json = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<MovementConfiguration>(json, SharedJson.Options);
    }

    public async Task SaveMovementConfigurationAsync(string realityId, MovementConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ServerSettings (Key,Value) VALUES ($key,$value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value";
        command.Parameters.AddWithValue("$key", $"movement-configuration:{realityId}"); command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(configuration, SharedJson.Options));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RelationshipState>> LoadRelationshipsAsync(string realityId, string playerId, CancellationToken cancellationToken = default)
    {
        var result = new List<RelationshipState>(); await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand(); command.CommandText = "SELECT ActorId, FriendRating FROM PlayerRelationships WHERE RealityId=$reality AND PlayerId=$player";
        command.Parameters.AddWithValue("$reality", realityId); command.Parameters.AddWithValue("$player", playerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(playerId, reader.GetString(0), reader.GetDouble(1)));
        return result;
    }

    public async Task SaveRelationshipAsync(string realityId, RelationshipState relationship, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO PlayerRelationships (RealityId,PlayerId,ActorId,FriendRating) VALUES ($r,$p,$a,$f) ON CONFLICT(RealityId,PlayerId,ActorId) DO UPDATE SET FriendRating=excluded.FriendRating";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", relationship.PlayerId);
        command.Parameters.AddWithValue("$a", relationship.ActorId); command.Parameters.AddWithValue("$f", relationship.FriendRating);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<HashSet<string>> LoadDiscoveryAsync(string realityId, string playerId, string dungeonId, CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(); await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT Cell FROM DungeonDiscovery WHERE RealityId=$r AND PlayerId=$p AND DungeonId=$d";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId); command.Parameters.AddWithValue("$d", dungeonId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0)); return result;
    }

    public async Task SaveDiscoveryAsync(string realityId, string playerId, string dungeonId, string cell, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO DungeonDiscovery (RealityId,PlayerId,DungeonId,Cell) VALUES ($r,$p,$d,$c)";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId); command.Parameters.AddWithValue("$d", dungeonId); command.Parameters.AddWithValue("$c", cell);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<HashSet<string>> LoadWorldMapDiscoveryAsync(string realityId, string playerId, CancellationToken cancellationToken = default)
    {
        var result = new HashSet<string>(StringComparer.Ordinal); await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT AreaKey FROM WorldMapDiscovery WHERE RealityId=$r AND PlayerId=$p";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0)); return result;
    }

    public async Task SaveWorldMapDiscoveryAsync(string realityId, string playerId, string areaKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO WorldMapDiscovery (RealityId,PlayerId,AreaKey,PurchasedUtc) VALUES ($r,$p,$a,$u)";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId); command.Parameters.AddWithValue("$a", areaKey); command.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CanonicalEntity>> LoadHomeFurnitureAsync(string accountId, string realityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT FurnitureJson FROM HomeFurniture WHERE AccountId=$a AND RealityId=$r";
        command.Parameters.AddWithValue("$a", accountId); command.Parameters.AddWithValue("$r", realityId);
        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        return string.IsNullOrWhiteSpace(json) ? Array.Empty<CanonicalEntity>() : JsonSerializer.Deserialize<CanonicalEntity[]>(json, SharedJson.Options) ?? Array.Empty<CanonicalEntity>();
    }

    public async Task SaveHomeFurnitureAsync(string accountId, string realityId, IReadOnlyList<CanonicalEntity> furniture, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO HomeFurniture (AccountId,RealityId,FurnitureJson,UpdatedUtc) VALUES ($a,$r,$j,$u) ON CONFLICT(AccountId,RealityId) DO UPDATE SET FurnitureJson=excluded.FurnitureJson,UpdatedUtc=excluded.UpdatedUtc";
        command.Parameters.AddWithValue("$a", accountId); command.Parameters.AddWithValue("$r", realityId);
        command.Parameters.AddWithValue("$j", JsonSerializer.Serialize(furniture, SharedJson.Options)); command.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AccountRecord?> FindAccountByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Username,PasswordHash,PasswordSalt,SessionTokenHash,ActiveCharacterId FROM Accounts WHERE Username=$u"; command.Parameters.AddWithValue("$u", username);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    public async Task<AccountRecord?> FindAccountBySessionHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Username,PasswordHash,PasswordSalt,SessionTokenHash,ActiveCharacterId FROM Accounts WHERE SessionTokenHash=$t"; command.Parameters.AddWithValue("$t", tokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    public async Task CreateAccountAsync(AccountRecord account, string characterName, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var accountCommand = connection.CreateCommand(); accountCommand.Transaction = (SqliteTransaction)transaction;
        accountCommand.CommandText = "INSERT INTO Accounts (Id,Username,PasswordHash,PasswordSalt,SessionTokenHash,ActiveCharacterId,CreatedUtc) VALUES ($id,$u,$h,$s,$t,$c,$now)";
        accountCommand.Parameters.AddWithValue("$id",account.Id);accountCommand.Parameters.AddWithValue("$u",account.Username);accountCommand.Parameters.AddWithValue("$h",account.PasswordHash);accountCommand.Parameters.AddWithValue("$s",account.PasswordSalt);accountCommand.Parameters.AddWithValue("$t",account.SessionTokenHash);accountCommand.Parameters.AddWithValue("$c",account.ActiveCharacterId);accountCommand.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await accountCommand.ExecuteNonQueryAsync(cancellationToken);
        var character = connection.CreateCommand(); character.Transaction = (SqliteTransaction)transaction; character.CommandText="INSERT INTO AccountCharacters (Id,AccountId,Name,CreatedUtc) VALUES ($id,$a,$n,$now)";character.Parameters.AddWithValue("$id",account.ActiveCharacterId);character.Parameters.AddWithValue("$a",account.Id);character.Parameters.AddWithValue("$n",characterName);character.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await character.ExecuteNonQueryAsync(cancellationToken);await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateSessionAsync(string accountId, string sessionTokenHash, CancellationToken cancellationToken = default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="UPDATE Accounts SET SessionTokenHash=$t WHERE Id=$a";command.Parameters.AddWithValue("$t",sessionTokenHash);command.Parameters.AddWithValue("$a",accountId);await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task<IReadOnlyList<AccountCharacter>> LoadAccountCharactersAsync(string accountId,CancellationToken cancellationToken=default)
    { var result=new List<AccountCharacter>();await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT Id,Name FROM AccountCharacters WHERE AccountId=$a ORDER BY CreatedUtc";command.Parameters.AddWithValue("$a",accountId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))result.Add(new(reader.GetString(0),reader.GetString(1)));return result; }
    public async Task AddAccountCharacterAsync(string accountId,AccountCharacter character,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="INSERT INTO AccountCharacters (Id,AccountId,Name,CreatedUtc) VALUES ($id,$a,$n,$now)";command.Parameters.AddWithValue("$id",character.Id);command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$n",character.Name);command.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await command.ExecuteNonQueryAsync(cancellationToken); }
    public async Task SetActiveCharacterAsync(string accountId,string characterId,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="UPDATE Accounts SET ActiveCharacterId=$c WHERE Id=$a AND EXISTS (SELECT 1 FROM AccountCharacters WHERE Id=$c AND AccountId=$a)";command.Parameters.AddWithValue("$c",characterId);command.Parameters.AddWithValue("$a",accountId);if(await command.ExecuteNonQueryAsync(cancellationToken)!=1)throw new InvalidOperationException("Character does not belong to this account."); }
    public async Task DeleteAccountCharacterAsync(string accountId,string characterId,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);foreach(var sql in new[]{"DELETE FROM Inventories WHERE OwnerId=$c","DELETE FROM PlayerRelationships WHERE PlayerId=$c","DELETE FROM DungeonDiscovery WHERE PlayerId=$c","DELETE FROM WorldMapDiscovery WHERE PlayerId=$c","DELETE FROM Characters WHERE Id=$c","DELETE FROM AccountCharacters WHERE Id=$c AND AccountId=$a"}){var command=connection.CreateCommand();command.Transaction=(SqliteTransaction)transaction;command.CommandText=sql;command.Parameters.AddWithValue("$c",characterId);command.Parameters.AddWithValue("$a",accountId);await command.ExecuteNonQueryAsync(cancellationToken);}await transaction.CommitAsync(cancellationToken); }

    public async Task<bool> IsFirstAccountCharacterAsync(string accountId,string characterId,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT Id=$c FROM AccountCharacters WHERE AccountId=$a ORDER BY CreatedUtc LIMIT 1";command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$c",characterId);var result=await command.ExecuteScalarAsync(cancellationToken);return result is not null&&Convert.ToInt64(result)!=0; }

    public async Task<BaseAssignment?> LoadBaseAssignmentAsync(string accountId, string realityId, CancellationToken cancellationToken = default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT BuildingId,RegionLatitude,RegionLongitude,X,Y FROM AccountBases WHERE AccountId=$a AND RealityId=$r";command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$r",realityId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))return null;WorldPosition? position=reader.IsDBNull(1)||reader.IsDBNull(2)||reader.IsDBNull(3)||reader.IsDBNull(4)?null:new WorldPosition(new RegionId(reader.GetInt32(1),reader.GetInt32(2)),reader.GetDouble(3),reader.GetDouble(4));return new BaseAssignment(reader.GetString(0),position); }
    public async Task<IReadOnlySet<string>> LoadAssignedBaseBuildingsAsync(string realityId,CancellationToken cancellationToken=default)
    { var result=new HashSet<string>(StringComparer.Ordinal);await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT BuildingId FROM AccountBases WHERE RealityId=$r";command.Parameters.AddWithValue("$r",realityId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))result.Add(reader.GetString(0));return result; }
    public async Task<string?> LoadBaseOwnerAsync(string realityId,string buildingId,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT AccountId FROM AccountBases WHERE RealityId=$r AND BuildingId=$b LIMIT 1";command.Parameters.AddWithValue("$r",realityId);command.Parameters.AddWithValue("$b",buildingId);return (string?)await command.ExecuteScalarAsync(cancellationToken); }
    public async Task SaveBaseBuildingAsync(string accountId,string realityId,string buildingId,WorldPosition position,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="INSERT INTO AccountBases (AccountId,RealityId,BuildingId,RegionLatitude,RegionLongitude,X,Y) VALUES ($a,$r,$b,$lat,$lon,$x,$y) ON CONFLICT(AccountId,RealityId) DO UPDATE SET BuildingId=excluded.BuildingId,RegionLatitude=excluded.RegionLatitude,RegionLongitude=excluded.RegionLongitude,X=excluded.X,Y=excluded.Y";command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$r",realityId);command.Parameters.AddWithValue("$b",buildingId);command.Parameters.AddWithValue("$lat",position.Region.LatitudeBand);command.Parameters.AddWithValue("$lon",position.Region.LongitudeBand);command.Parameters.AddWithValue("$x",position.X);command.Parameters.AddWithValue("$y",position.Y);await command.ExecuteNonQueryAsync(cancellationToken); }

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

    private static DateTimeOffset? ReadDate(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    private static AccountRecord ReadAccount(SqliteDataReader reader) => new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5));
}

public sealed record AccountRecord(string Id,string Username,string PasswordHash,string PasswordSalt,string SessionTokenHash,string ActiveCharacterId);
public sealed record AccountCharacter(string Id,string Name);
public sealed record BaseAssignment(string BuildingId,WorldPosition? Position);
