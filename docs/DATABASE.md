# SQLite reality schema

SQLite runs in WAL mode. The schema is intentionally small and uses logical text IDs so the store can later be implemented in PostgreSQL without changing the canonical model.

| Table | Purpose |
|---|---|
| `Reality` | identity, seed, and serialized server configuration |
| `Characters` | reality-owned authoritative name, region, position, ten-heart health, stamina, travel mode, version |
| `RealityDeltas` | entity upserts and removal tombstones over regenerated base geography |
| `Inventories` | owner/slot item stacks and logical metadata |
| `Containers` | entity-to-inventory ownership and capacity |
| `ServerSettings` | administrator-controlled settings |
| `Permissions` | subject/permission grants |

The prototype writes character movement and player-created structures. Inventory/container tables establish the migration-safe boundary for the next milestone but are not yet exposed to clients.

`RealityDeltas.Operation` is either `upsert` or `removed`. A player-created entity is an upsert. Removing it creates a tombstone rather than erasing the history contract. The same mechanism can later suppress a deterministic base tree or store a terrain patch. Backups consist of the SQLite files plus reality configuration; geographic cache can be regenerated.
