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
                EquippedShirt TEXT NOT NULL DEFAULT 'none', EquippedPants TEXT NOT NULL DEFAULT 'none', WantedLevel INTEGER NOT NULL DEFAULT 0,
                EBikeRemainingMeters REAL NOT NULL DEFAULT 1609.344,
                EnergyDrinkBoostUntilUtc TEXT, EnergyDrinkCrashUntilUtc TEXT, ProbedUntilUtc TEXT, CandleUntilUtc TEXT,
                ShieldOn INTEGER NOT NULL DEFAULT 0, Ar15FireMode TEXT NOT NULL DEFAULT 'single', FlamethrowerGasGallons REAL NOT NULL DEFAULT 0,
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
                ActiveCharacterId TEXT NOT NULL, CreatedUtc TEXT NOT NULL, LastSeenUtc TEXT
            );
            CREATE TABLE IF NOT EXISTS AccountCharacters (
                Id TEXT PRIMARY KEY, AccountId TEXT NOT NULL, Name TEXT NOT NULL, CreatedUtc TEXT NOT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS AccountBases (
                AccountId TEXT NOT NULL, RealityId TEXT NOT NULL, BuildingId TEXT NOT NULL,
                RegionLatitude INTEGER, RegionLongitude INTEGER, X REAL, Y REAL, LastActiveUtc TEXT,
                PRIMARY KEY (AccountId, RealityId)
            );
            CREATE TABLE IF NOT EXISTS HomeFurniture (
                AccountId TEXT NOT NULL, RealityId TEXT NOT NULL,
                FurnitureJson TEXT NOT NULL, UpdatedUtc TEXT NOT NULL,
                PRIMARY KEY (AccountId, RealityId)
            );
            CREATE TABLE IF NOT EXISTS PlayerQuests (
                RealityId TEXT NOT NULL, PlayerId TEXT NOT NULL, QuestId TEXT NOT NULL,
                QuestJson TEXT NOT NULL, UpdatedUtc TEXT NOT NULL,
                PRIMARY KEY (RealityId, PlayerId, QuestId)
            );
            CREATE TABLE IF NOT EXISTS HomeShopListings (
                AccountId TEXT NOT NULL, RealityId TEXT NOT NULL, ItemType TEXT NOT NULL,
                Quantity INTEGER NOT NULL, UnitPriceCents INTEGER NOT NULL, Quality TEXT, UpdatedUtc TEXT NOT NULL,
                PRIMARY KEY (AccountId, RealityId, ItemType)
            );
            CREATE TABLE IF NOT EXISTS AccountNotices (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, AccountId TEXT NOT NULL, RealityId TEXT NOT NULL,
                Message TEXT NOT NULL, CreatedUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS HomeCashBalances (
                AccountId TEXT NOT NULL, RealityId TEXT NOT NULL, BalanceCents INTEGER NOT NULL DEFAULT 0,
                UpdatedUtc TEXT NOT NULL, PRIMARY KEY (AccountId, RealityId)
            );
            CREATE TABLE IF NOT EXISTS PersistentWorldLoot (
                Id TEXT PRIMARY KEY, RealityId TEXT NOT NULL, LootJson TEXT NOT NULL, CreatedUtc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "TravelMode", "TEXT NOT NULL DEFAULT 'Walk'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "Stamina", "REAL NOT NULL DEFAULT 10", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "Water", "REAL NOT NULL DEFAULT 10", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "WantedLevel", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "EBikeRemainingMeters", "REAL NOT NULL DEFAULT 1609.344", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "EnergyDrinkBoostUntilUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "EnergyDrinkCrashUntilUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "ProbedUntilUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "CandleUntilUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "ShieldOn", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "Ar15FireMode", "TEXT NOT NULL DEFAULT 'single'", cancellationToken);
        await EnsureColumnAsync(connection, "Characters", "FlamethrowerGasGallons", "REAL NOT NULL DEFAULT 0", cancellationToken);
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
        await EnsureColumnAsync(connection, "AccountBases", "LastActiveUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Accounts", "LastSeenUtc", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Accounts", "IsTestAccount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);

        // Older smoke runs predate the explicit marker. Their generated names are always
        // SmokeA/SmokeB followed by exactly four hexadecimal characters.
        command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Accounts
            SET IsTestAccount = 1
            WHERE length(Username) = 10
              AND (Username LIKE 'SmokeA%' OR Username LIKE 'SmokeB%')
              AND lower(substr(Username, 7, 4)) NOT GLOB '*[^0-9a-f]*';
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

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

    public async Task<IReadOnlyList<string>> LoadRemovedEntityIdsAsync(string realityId, CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT EntityId FROM RealityDeltas WHERE RealityId=$reality AND Operation='removed'";
        command.Parameters.AddWithValue("$reality", realityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetString(0));
        return ids;
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
        command.CommandText = "SELECT Name, RegionLatitude, RegionLongitude, X, Y, Z, Version, Health, TravelMode, Stamina, Water, WalletCents, GodMode, FoodProtectedUntilUtc, WaterProtectedUntilUtc, LocationId, FlashlightOn, LanternOn, LaserOn, MagicHikingShoesOn, MagicRunningShoesOn, HatOn, DirtBikeGasGallons, MotorcycleGasGallons, EquippedWeapon, BodyHeat, EquippedHat, EquippedShirt, EquippedPants, WantedLevel, EBikeRemainingMeters, EnergyDrinkBoostUntilUtc, EnergyDrinkCrashUntilUtc, ProbedUntilUtc, CandleUntilUtc, ShieldOn, Ar15FireMode, FlamethrowerGasGallons FROM Characters WHERE RealityId = $reality AND Id = $id";
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
            EquippedPants: reader.IsDBNull(28) ? "none" : reader.GetString(28), WantedLevel: reader.GetInt32(29), EBikeRemainingMeters: reader.GetDouble(30),
            EnergyDrinkBoostUntilUtc: ReadDate(reader, 31), EnergyDrinkCrashUntilUtc: ReadDate(reader, 32), ProbedUntilUtc: ReadDate(reader, 33), CandleUntilUtc: ReadDate(reader, 34), ShieldOn: reader.GetInt64(35) != 0, Ar15FireMode: reader.IsDBNull(36) ? "single" : reader.GetString(36), FlamethrowerGasGallons: reader.GetDouble(37));
    }

    public async Task SaveCharacterAsync(string realityId, PlayerState player, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Characters (Id, RealityId, Name, RegionLatitude, RegionLongitude, X, Y, Z, Health, TravelMode, Stamina, Water, WalletCents, GodMode, FoodProtectedUntilUtc, WaterProtectedUntilUtc, LocationId, FlashlightOn, LanternOn, LaserOn, MagicHikingShoesOn, MagicRunningShoesOn, HatOn, DirtBikeGasGallons, MotorcycleGasGallons, EquippedWeapon, BodyHeat, EquippedHat, EquippedShirt, EquippedPants, WantedLevel, EBikeRemainingMeters, EnergyDrinkBoostUntilUtc, EnergyDrinkCrashUntilUtc, ProbedUntilUtc, CandleUntilUtc, ShieldOn, Ar15FireMode, FlamethrowerGasGallons, Version, UpdatedUtc)
            VALUES ($id, $reality, $name, $regionLat, $regionLon, $x, $y, $z, $health, $travelMode, $stamina, $water, $wallet, $god, $foodUntil, $waterUntil, $location, $flashlight, $lantern, $laser, $magicHikingShoes, $magicRunningShoes, $hat, $dirtBikeGas, $motorcycleGas, $equippedWeapon, $bodyHeat, $equippedHat, $equippedShirt, $equippedPants, $wantedLevel, $eBikeRemaining, $energyBoostUntil, $energyCrashUntil, $probedUntil, $candleUntil, $shieldOn, $ar15FireMode, $flamethrowerGas, $version, $updated)
            ON CONFLICT(Id) DO UPDATE SET Name = excluded.Name, X = excluded.X, Y = excluded.Y, Z = excluded.Z,
                Health = excluded.Health, TravelMode = excluded.TravelMode, Stamina = excluded.Stamina, Water = excluded.Water,
                WalletCents = excluded.WalletCents, GodMode = excluded.GodMode,
                FoodProtectedUntilUtc = excluded.FoodProtectedUntilUtc, WaterProtectedUntilUtc = excluded.WaterProtectedUntilUtc,
                LocationId = excluded.LocationId, FlashlightOn=excluded.FlashlightOn, LanternOn=excluded.LanternOn, LaserOn=excluded.LaserOn,
                MagicHikingShoesOn=excluded.MagicHikingShoesOn, MagicRunningShoesOn=excluded.MagicRunningShoesOn, HatOn=excluded.HatOn,
                DirtBikeGasGallons=excluded.DirtBikeGasGallons, MotorcycleGasGallons=excluded.MotorcycleGasGallons,
                EquippedWeapon=excluded.EquippedWeapon, BodyHeat=excluded.BodyHeat, EquippedHat=excluded.EquippedHat,
                EquippedShirt=excluded.EquippedShirt, EquippedPants=excluded.EquippedPants, WantedLevel=excluded.WantedLevel, EBikeRemainingMeters=excluded.EBikeRemainingMeters,
                EnergyDrinkBoostUntilUtc=excluded.EnergyDrinkBoostUntilUtc, EnergyDrinkCrashUntilUtc=excluded.EnergyDrinkCrashUntilUtc,
                ProbedUntilUtc=excluded.ProbedUntilUtc, CandleUntilUtc=excluded.CandleUntilUtc, ShieldOn=excluded.ShieldOn, Ar15FireMode=excluded.Ar15FireMode, FlamethrowerGasGallons=excluded.FlamethrowerGasGallons,
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
        command.Parameters.AddWithValue("$wantedLevel", player.WantedLevel);
        command.Parameters.AddWithValue("$eBikeRemaining", player.EBikeRemainingMeters);
        command.Parameters.AddWithValue("$energyBoostUntil", (object?)player.EnergyDrinkBoostUntilUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$energyCrashUntil", (object?)player.EnergyDrinkCrashUntilUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$probedUntil", (object?)player.ProbedUntilUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$candleUntil", (object?)player.CandleUntilUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$shieldOn", player.ShieldOn ? 1 : 0);
        command.Parameters.AddWithValue("$ar15FireMode", player.Ar15FireMode);
        command.Parameters.AddWithValue("$flamethrowerGas", player.FlamethrowerGasGallons);
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
                     "DELETE FROM OpenedChests WHERE RealityId=$reality",
                     "DELETE FROM PersistentWorldLoot WHERE RealityId=$reality"
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

    public async Task ResetSharedDungeonStateAsync(string realityId, string dungeonId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var statement in new[]
        {
            "DELETE FROM DungeonDiscovery WHERE RealityId=$r AND (DungeonId=$d OR DungeonId LIKE $prefix)",
            "DELETE FROM PlayerRelationships WHERE RealityId=$r AND ActorId LIKE $actorPrefix",
            "DELETE FROM OpenedChests WHERE RealityId=$r AND ChestId LIKE $actorPrefix"
        })
        {
            var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction)transaction; command.CommandText = statement;
            command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$d", dungeonId);
            command.Parameters.AddWithValue("$prefix", dungeonId + ":level:%"); command.Parameters.AddWithValue("$actorPrefix", dungeonId + ":%");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<InventoryState> LoadInventoryAsync(string playerId, CancellationToken cancellationToken = default)
    {
        var items = new List<ItemStack>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ItemType, Quantity, MetadataJson FROM Inventories WHERE OwnerId = $owner ORDER BY Slot";
        command.Parameters.AddWithValue("$owner", playerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string? quality = null;
            if (!reader.IsDBNull(2))
            {
                try { quality = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(2), SharedJson.Options)?.GetValueOrDefault("quality"); }
                catch (JsonException) { }
            }
            items.Add(new ItemStack(reader.GetString(0), reader.GetInt32(1), Quality: quality));
        }
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
            insert.CommandText = "INSERT INTO Inventories (OwnerId, Slot, ItemType, Quantity, MetadataJson) VALUES ($owner,$slot,$type,$quantity,$metadata)";
            insert.Parameters.AddWithValue("$owner", inventory.PlayerId); insert.Parameters.AddWithValue("$slot", slot);
            insert.Parameters.AddWithValue("$type", item.ItemType); insert.Parameters.AddWithValue("$quantity", item.Quantity);
            insert.Parameters.AddWithValue("$metadata", item.Quality is null ? "{}" : JsonSerializer.Serialize(new Dictionary<string, string> { ["quality"] = item.Quality }, SharedJson.Options));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> LoadHomeCashAsync(string accountId, string realityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT BalanceCents FROM HomeCashBalances WHERE AccountId=$account AND RealityId=$reality";
        command.Parameters.AddWithValue("$account", accountId); command.Parameters.AddWithValue("$reality", realityId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task SaveHomeCashAsync(string accountId, string realityId, long balanceCents, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO HomeCashBalances (AccountId,RealityId,BalanceCents,UpdatedUtc) VALUES ($account,$reality,$balance,$now) ON CONFLICT(AccountId,RealityId) DO UPDATE SET BalanceCents=excluded.BalanceCents,UpdatedUtc=excluded.UpdatedUtc";
        command.Parameters.AddWithValue("$account", accountId); command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$balance", Math.Max(0, balanceCents)); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LootDropState>> LoadPersistentLootAsync(string realityId, CancellationToken cancellationToken = default)
    {
        var result = new List<LootDropState>();
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT LootJson FROM PersistentWorldLoot WHERE RealityId=$reality ORDER BY CreatedUtc";
        command.Parameters.AddWithValue("$reality", realityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var loot = JsonSerializer.Deserialize<LootDropState>(reader.GetString(0), SharedJson.Options);
            if (loot is not null) result.Add(loot);
        }
        return result;
    }

    public async Task SavePersistentLootAsync(string realityId, LootDropState loot, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO PersistentWorldLoot (Id,RealityId,LootJson,CreatedUtc) VALUES ($id,$reality,$json,$now) ON CONFLICT(Id) DO UPDATE SET LootJson=excluded.LootJson";
        command.Parameters.AddWithValue("$id", loot.Id); command.Parameters.AddWithValue("$reality", realityId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(loot, SharedJson.Options)); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemovePersistentLootAsync(string lootId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM PersistentWorldLoot WHERE Id=$id"; command.Parameters.AddWithValue("$id", lootId);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    public async Task<ServerEventConfiguration?> LoadServerEventConfigurationAsync(string realityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM ServerSettings WHERE Key=$key"; command.Parameters.AddWithValue("$key", $"event-configuration:{realityId}");
        var json = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ServerEventConfiguration>(json, SharedJson.Options);
    }

    public async Task SaveServerEventConfigurationAsync(string realityId, ServerEventConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ServerSettings (Key,Value) VALUES ($key,$value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value";
        command.Parameters.AddWithValue("$key", $"event-configuration:{realityId}"); command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(configuration, SharedJson.Options));
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

    public async Task<IReadOnlyList<QuestState>> LoadQuestsAsync(string realityId, string playerId, CancellationToken cancellationToken = default)
    {
        var result = new List<QuestState>();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT QuestJson FROM PlayerQuests WHERE RealityId=$r AND PlayerId=$p ORDER BY UpdatedUtc";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", playerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var quest = JsonSerializer.Deserialize<QuestState>(reader.GetString(0), SharedJson.Options);
            if (quest is not null) result.Add(quest);
        }
        return result;
    }

    public async Task SaveQuestAsync(string realityId, QuestState quest, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO PlayerQuests (RealityId,PlayerId,QuestId,QuestJson,UpdatedUtc) VALUES ($r,$p,$q,$j,$u) ON CONFLICT(RealityId,PlayerId,QuestId) DO UPDATE SET QuestJson=excluded.QuestJson,UpdatedUtc=excluded.UpdatedUtc";
        command.Parameters.AddWithValue("$r", realityId); command.Parameters.AddWithValue("$p", quest.PlayerId); command.Parameters.AddWithValue("$q", quest.Id);
        command.Parameters.AddWithValue("$j", JsonSerializer.Serialize(quest, SharedJson.Options)); command.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("O"));
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

    public async Task CreateAccountAsync(AccountRecord account, string characterName, bool isTestAccount = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var accountCommand = connection.CreateCommand(); accountCommand.Transaction = (SqliteTransaction)transaction;
        accountCommand.CommandText = "INSERT INTO Accounts (Id,Username,PasswordHash,PasswordSalt,SessionTokenHash,ActiveCharacterId,CreatedUtc,IsTestAccount) VALUES ($id,$u,$h,$s,$t,$c,$now,$test)";
        accountCommand.Parameters.AddWithValue("$id",account.Id);accountCommand.Parameters.AddWithValue("$u",account.Username);accountCommand.Parameters.AddWithValue("$h",account.PasswordHash);accountCommand.Parameters.AddWithValue("$s",account.PasswordSalt);accountCommand.Parameters.AddWithValue("$t",account.SessionTokenHash);accountCommand.Parameters.AddWithValue("$c",account.ActiveCharacterId);accountCommand.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));accountCommand.Parameters.AddWithValue("$test",isTestAccount?1:0);await accountCommand.ExecuteNonQueryAsync(cancellationToken);
        var character = connection.CreateCommand(); character.Transaction = (SqliteTransaction)transaction; character.CommandText="INSERT INTO AccountCharacters (Id,AccountId,Name,CreatedUtc) VALUES ($id,$a,$n,$now)";character.Parameters.AddWithValue("$id",account.ActiveCharacterId);character.Parameters.AddWithValue("$a",account.Id);character.Parameters.AddWithValue("$n",characterName);character.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await character.ExecuteNonQueryAsync(cancellationToken);await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateSessionAsync(string accountId, string sessionTokenHash, CancellationToken cancellationToken = default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="UPDATE Accounts SET SessionTokenHash=$t,LastSeenUtc=$now WHERE Id=$a";command.Parameters.AddWithValue("$t",sessionTokenHash);command.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));command.Parameters.AddWithValue("$a",accountId);await command.ExecuteNonQueryAsync(cancellationToken); }

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
    public async Task<IReadOnlyList<PublicBaseClaim>> LoadPublicBaseClaimsAsync(string realityId,CancellationToken cancellationToken=default)
    { var result=new List<PublicBaseClaim>();await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT b.AccountId,b.BuildingId,a.Username FROM AccountBases b JOIN Accounts a ON a.Id=b.AccountId WHERE b.RealityId=$r";command.Parameters.AddWithValue("$r",realityId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))result.Add(new PublicBaseClaim(reader.GetString(0),reader.GetString(1),reader.GetString(2)));return result; }
    public async Task<int> ReleaseExpiredBaseClaimsAsync(string realityId,DateTimeOffset now,CancellationToken cancellationToken=default)
    {
        await using var connection=await OpenAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);var expired=new List<string>();
        var read=connection.CreateCommand();read.Transaction=(SqliteTransaction)transaction;read.CommandText="SELECT AccountId FROM AccountBases WHERE RealityId=$r AND LastActiveUtc IS NOT NULL AND LastActiveUtc<=$cutoff";read.Parameters.AddWithValue("$r",realityId);read.Parameters.AddWithValue("$cutoff",now.AddDays(-30).ToString("O"));await using(var reader=await read.ExecuteReaderAsync(cancellationToken))while(await reader.ReadAsync(cancellationToken))expired.Add(reader.GetString(0));
        foreach(var accountId in expired){var notice=connection.CreateCommand();notice.Transaction=(SqliteTransaction)transaction;notice.CommandText="INSERT INTO AccountNotices(AccountId,RealityId,Message,CreatedUtc) VALUES($a,$r,$m,$now)";notice.Parameters.AddWithValue("$a",accountId);notice.Parameters.AddWithValue("$r",realityId);notice.Parameters.AddWithValue("$m","Your previous Home was released after 30 days without a login. Your stored furniture and Home inventory were preserved, and the server will assign you a new Home at no charge.");notice.Parameters.AddWithValue("$now",now.ToString("O"));await notice.ExecuteNonQueryAsync(cancellationToken);}
        if(expired.Count>0){var remove=connection.CreateCommand();remove.Transaction=(SqliteTransaction)transaction;remove.CommandText="DELETE FROM AccountBases WHERE RealityId=$r AND LastActiveUtc IS NOT NULL AND LastActiveUtc<=$cutoff";remove.Parameters.AddWithValue("$r",realityId);remove.Parameters.AddWithValue("$cutoff",now.AddDays(-30).ToString("O"));await remove.ExecuteNonQueryAsync(cancellationToken);}await transaction.CommitAsync(cancellationToken);return expired.Count;
    }
    public async Task<bool> RefreshBaseActivityAsync(string accountId,string realityId,DateTimeOffset now,CancellationToken cancellationToken=default)
    {
        await using var connection=await OpenAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);
        var read=connection.CreateCommand();read.Transaction=(SqliteTransaction)transaction;read.CommandText="SELECT LastActiveUtc FROM AccountBases WHERE AccountId=$a AND RealityId=$r";read.Parameters.AddWithValue("$a",accountId);read.Parameters.AddWithValue("$r",realityId);var value=await read.ExecuteScalarAsync(cancellationToken);
        if(value is null){await transaction.CommitAsync(cancellationToken);return false;}
        var expired=value is string text&&DateTimeOffset.TryParse(text,out var last)&&now-last>=TimeSpan.FromDays(30);
        var command=connection.CreateCommand();command.Transaction=(SqliteTransaction)transaction;command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$r",realityId);
        if(expired)command.CommandText="DELETE FROM AccountBases WHERE AccountId=$a AND RealityId=$r";
        else{command.CommandText="UPDATE AccountBases SET LastActiveUtc=$now WHERE AccountId=$a AND RealityId=$r";command.Parameters.AddWithValue("$now",now.ToString("O"));}
        await command.ExecuteNonQueryAsync(cancellationToken);await transaction.CommitAsync(cancellationToken);return expired;
    }
    public async Task SaveBaseBuildingAsync(string accountId,string realityId,string buildingId,WorldPosition position,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="INSERT INTO AccountBases (AccountId,RealityId,BuildingId,RegionLatitude,RegionLongitude,X,Y,LastActiveUtc) VALUES ($a,$r,$b,$lat,$lon,$x,$y,$active) ON CONFLICT(AccountId,RealityId) DO UPDATE SET BuildingId=excluded.BuildingId,RegionLatitude=excluded.RegionLatitude,RegionLongitude=excluded.RegionLongitude,X=excluded.X,Y=excluded.Y,LastActiveUtc=excluded.LastActiveUtc";command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$r",realityId);command.Parameters.AddWithValue("$b",buildingId);command.Parameters.AddWithValue("$lat",position.Region.LatitudeBand);command.Parameters.AddWithValue("$lon",position.Region.LongitudeBand);command.Parameters.AddWithValue("$x",position.X);command.Parameters.AddWithValue("$y",position.Y);command.Parameters.AddWithValue("$active",DateTimeOffset.UtcNow.ToString("O"));await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task MarkAccountSeenAsync(string accountId, DateTimeOffset now, CancellationToken cancellationToken = default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="UPDATE Accounts SET LastSeenUtc=$now WHERE Id=$a";command.Parameters.AddWithValue("$now",now.ToString("O"));command.Parameters.AddWithValue("$a",accountId);await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task<IReadOnlyList<AccountRosterEntry>> LoadAccountRosterAsync(CancellationToken cancellationToken = default)
    {
        var accounts=new List<(string Id,string Username,DateTimeOffset? LastSeen)>();await using var connection=await OpenAsync(cancellationToken);
        var accountCommand=connection.CreateCommand();accountCommand.CommandText="SELECT Id,Username,LastSeenUtc FROM Accounts WHERE IsTestAccount=0 ORDER BY Username COLLATE NOCASE";
        await using(var reader=await accountCommand.ExecuteReaderAsync(cancellationToken))while(await reader.ReadAsync(cancellationToken))accounts.Add((reader.GetString(0),reader.GetString(1),reader.IsDBNull(2)?null:DateTimeOffset.Parse(reader.GetString(2),System.Globalization.CultureInfo.InvariantCulture)));
        var result=new List<AccountRosterEntry>();foreach(var account in accounts)result.Add(new AccountRosterEntry(account.Id,account.Username,account.LastSeen,await LoadAccountCharactersAsync(account.Id,cancellationToken)));return result;
    }

    public async Task<IReadOnlyList<ExpiredTestAccount>> DeleteExpiredTestAccountsAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var accounts = new List<(string Id, string Username)>();
        var readAccounts = connection.CreateCommand(); readAccounts.Transaction = (SqliteTransaction)transaction;
        readAccounts.CommandText = "SELECT Id,Username FROM Accounts WHERE IsTestAccount=1 AND CreatedUtc<=$cutoff";
        readAccounts.Parameters.AddWithValue("$cutoff", cutoffUtc.ToString("O"));
        await using (var reader = await readAccounts.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) accounts.Add((reader.GetString(0), reader.GetString(1)));

        var removed = new List<ExpiredTestAccount>();
        foreach (var account in accounts)
        {
            var characterIds = new List<string>();
            var readCharacters = connection.CreateCommand(); readCharacters.Transaction = (SqliteTransaction)transaction;
            readCharacters.CommandText = "SELECT Id FROM AccountCharacters WHERE AccountId=$account";
            readCharacters.Parameters.AddWithValue("$account", account.Id);
            await using (var reader = await readCharacters.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken)) characterIds.Add(reader.GetString(0));

            foreach (var characterId in characterIds)
            {
                foreach (var (table, column) in new[]
                {
                    ("Inventories", "OwnerId"), ("PlayerRelationships", "PlayerId"), ("DungeonDiscovery", "PlayerId"),
                    ("WorldMapDiscovery", "PlayerId"), ("OpenedChests", "PlayerId"), ("PlayerQuests", "PlayerId"),
                    ("Characters", "Id")
                })
                {
                    var removeCharacterData = connection.CreateCommand(); removeCharacterData.Transaction = (SqliteTransaction)transaction;
                    removeCharacterData.CommandText = $"DELETE FROM {table} WHERE {column}=$character";
                    removeCharacterData.Parameters.AddWithValue("$character", characterId);
                    await removeCharacterData.ExecuteNonQueryAsync(cancellationToken);
                }
                var removeOwnedEntities = connection.CreateCommand(); removeOwnedEntities.Transaction = (SqliteTransaction)transaction;
                removeOwnedEntities.CommandText = "DELETE FROM RealityDeltas WHERE PropertiesJson LIKE $owner";
                removeOwnedEntities.Parameters.AddWithValue("$owner", $"%\"owner\":\"{characterId}\"%");
                await removeOwnedEntities.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var table in new[] { "AccountBases", "HomeFurniture", "HomeShopListings", "AccountNotices", "HomeCashBalances" })
            {
                var removeAccountData = connection.CreateCommand(); removeAccountData.Transaction = (SqliteTransaction)transaction;
                removeAccountData.CommandText = $"DELETE FROM {table} WHERE AccountId=$account";
                removeAccountData.Parameters.AddWithValue("$account", account.Id);
                await removeAccountData.ExecuteNonQueryAsync(cancellationToken);
            }
            var removeHomeInventory = connection.CreateCommand(); removeHomeInventory.Transaction = (SqliteTransaction)transaction;
            removeHomeInventory.CommandText = "DELETE FROM Inventories WHERE OwnerId LIKE $owner";
            removeHomeInventory.Parameters.AddWithValue("$owner", $"home-items:%:{account.Id}");
            await removeHomeInventory.ExecuteNonQueryAsync(cancellationToken);
            var removeCharacters = connection.CreateCommand(); removeCharacters.Transaction = (SqliteTransaction)transaction;
            removeCharacters.CommandText = "DELETE FROM AccountCharacters WHERE AccountId=$account";
            removeCharacters.Parameters.AddWithValue("$account", account.Id);
            await removeCharacters.ExecuteNonQueryAsync(cancellationToken);
            var removeAccount = connection.CreateCommand(); removeAccount.Transaction = (SqliteTransaction)transaction;
            removeAccount.CommandText = "DELETE FROM Accounts WHERE Id=$account";
            removeAccount.Parameters.AddWithValue("$account", account.Id);
            await removeAccount.ExecuteNonQueryAsync(cancellationToken);
            removed.Add(new ExpiredTestAccount(account.Id, account.Username, characterIds));
        }
        await transaction.CommitAsync(cancellationToken);
        return removed;
    }

    public async Task<bool> CharacterNameExistsAsync(string name,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT EXISTS(SELECT 1 FROM AccountCharacters WHERE Name=$n COLLATE NOCASE)";command.Parameters.AddWithValue("$n",name);return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken))!=0; }

    public async Task<IReadOnlyList<HomeShopListingRecord>> LoadHomeShopListingsAsync(string accountId,string realityId,CancellationToken cancellationToken=default)
    { var result=new List<HomeShopListingRecord>();await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="SELECT ItemType,Quantity,UnitPriceCents,Quality FROM HomeShopListings WHERE AccountId=$a AND RealityId=$r AND Quantity>0 ORDER BY ItemType";command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$r",realityId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))result.Add(new(reader.GetString(0),reader.GetInt32(1),reader.GetInt64(2),reader.IsDBNull(3)?null:reader.GetString(3)));return result; }

    public async Task SaveHomeShopListingAsync(string accountId,string realityId,HomeShopListingRecord listing,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$r",realityId);command.Parameters.AddWithValue("$i",listing.ItemType);if(listing.Quantity<=0)command.CommandText="DELETE FROM HomeShopListings WHERE AccountId=$a AND RealityId=$r AND ItemType=$i";else{command.CommandText="INSERT INTO HomeShopListings(AccountId,RealityId,ItemType,Quantity,UnitPriceCents,Quality,UpdatedUtc) VALUES($a,$r,$i,$q,$p,$quality,$now) ON CONFLICT(AccountId,RealityId,ItemType) DO UPDATE SET Quantity=excluded.Quantity,UnitPriceCents=excluded.UnitPriceCents,Quality=excluded.Quality,UpdatedUtc=excluded.UpdatedUtc";command.Parameters.AddWithValue("$q",listing.Quantity);command.Parameters.AddWithValue("$p",listing.UnitPriceCents);command.Parameters.AddWithValue("$quality",(object?)listing.Quality??DBNull.Value);command.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));}await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task AddAccountNoticeAsync(string accountId,string realityId,string message,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="INSERT INTO AccountNotices(AccountId,RealityId,Message,CreatedUtc) VALUES($a,$r,$m,$now)";command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$r",realityId);command.Parameters.AddWithValue("$m",message);command.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task<IReadOnlyList<string>> TakeAccountNoticesAsync(string accountId,string realityId,CancellationToken cancellationToken=default)
    { var messages=new List<string>();await using var connection=await OpenAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);var read=connection.CreateCommand();read.Transaction=(SqliteTransaction)transaction;read.CommandText="SELECT Id,Message FROM AccountNotices WHERE AccountId=$a AND RealityId=$r ORDER BY Id";read.Parameters.AddWithValue("$a",accountId);read.Parameters.AddWithValue("$r",realityId);var ids=new List<long>();await using(var reader=await read.ExecuteReaderAsync(cancellationToken))while(await reader.ReadAsync(cancellationToken)){ids.Add(reader.GetInt64(0));messages.Add(reader.GetString(1));}if(ids.Count>0){var remove=connection.CreateCommand();remove.Transaction=(SqliteTransaction)transaction;remove.CommandText="DELETE FROM AccountNotices WHERE AccountId=$a AND RealityId=$r";remove.Parameters.AddWithValue("$a",accountId);remove.Parameters.AddWithValue("$r",realityId);await remove.ExecuteNonQueryAsync(cancellationToken);}await transaction.CommitAsync(cancellationToken);return messages; }

    public async Task CreditActiveCharacterAsync(string accountId,string realityId,long cents,CancellationToken cancellationToken=default)
    { await using var connection=await OpenAsync(cancellationToken);var command=connection.CreateCommand();command.CommandText="UPDATE Characters SET WalletCents=WalletCents+$c,Version=Version+1,UpdatedUtc=$now WHERE RealityId=$r AND Id=(SELECT ActiveCharacterId FROM Accounts WHERE Id=$a)";command.Parameters.AddWithValue("$c",cents);command.Parameters.AddWithValue("$r",realityId);command.Parameters.AddWithValue("$a",accountId);command.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await command.ExecuteNonQueryAsync(cancellationToken); }

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
public sealed record PublicBaseClaim(string AccountId,string BuildingId,string OwnerName);
public sealed record AccountCharacter(string Id,string Name);
public sealed record BaseAssignment(string BuildingId,WorldPosition? Position);
public sealed record AccountRosterEntry(string AccountId,string Username,DateTimeOffset? LastSeenUtc,IReadOnlyList<AccountCharacter> Characters);
public sealed record ExpiredTestAccount(string AccountId,string Username,IReadOnlyList<string> CharacterIds);
public sealed record HomeShopListingRecord(string ItemType,int Quantity,long UnitPriceCents,string? Quality);
