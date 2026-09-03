# SQLite reality schema

SQLite runs in WAL mode. The schema is intentionally small and uses logical text IDs so the store can later be implemented in PostgreSQL without changing the canonical model.

| Table | Purpose |
|---|---|
| `Reality` | identity, seed, and serialized server configuration |
| `Characters` | reality-owned authoritative name, region, position, health, stamina, hydration, travel mode, equipped items, separate vehicle fuel tanks, and version |
| `RealityDeltas` | entity upserts and removal tombstones over regenerated base geography |
| `Inventories` | owner/slot item stacks and logical metadata |
| `Containers` | entity-to-inventory ownership and capacity |
| `ServerSettings` | administrator-controlled settings |
| `Permissions` | subject/permission grants |
| `WorldMapDiscovery` | persistent per-player ownership/reveal state for purchased geographic-block maps |

The prototype writes character movement and player-created structures. Inventory/container tables establish the migration-safe boundary for the next milestone but are not yet exposed to clients.

`RealityDeltas.Operation` is either `upsert` or `removed`. A player-created entity is an upsert. Removing it creates a tombstone rather than erasing the history contract. The same mechanism can later suppress a deterministic base tree or store a terrain patch. Backups consist of the SQLite files plus reality configuration; geographic cache can be regenerated.
# Runtime schema additions

`Accounts` stores unique case-insensitive usernames, salted PBKDF2 password hashes, hashed session tokens, and the active character. `AccountCharacters` supports up to eight characters per account. `AccountBases` keeps one persistent base-building assignment and its streamed world position per account and reality. `PlayerRelationships`, `DungeonDiscovery`, and `OpenedChests` support active sessions but dungeon rows are cleared whenever that player enters or exits the dungeon. `WorldMapDiscovery` survives ordinary exploration, reconnects, and world rebuilds. Inventory, hydration, worn equipment, equipped weapon, light state, buffs, wallet, location, and dirt-bike/motorcycle gallons are persisted with the character. The permanent fist is a virtual inventory item and does not need a database row.

All database files, write-ahead logs, downloaded geography, elevation responses, and generated geographic caches remain under `data/` and are ignored by Git.
