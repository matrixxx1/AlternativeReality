# SQLite reality schema

SQLite runs in WAL mode. The schema is intentionally small and uses logical text IDs so the store can later be implemented in PostgreSQL without changing the canonical model.

| Table | Purpose |
|---|---|
| `Reality` | identity, seed, and serialized server configuration |
| `Characters` | reality-owned authoritative name, region, position, health, stamina, hydration, travel mode, timed status effects, equipped items, separate vehicle fuel tanks, and version |
| `RealityDeltas` | entity upserts and removal tombstones over regenerated base geography |
| `Inventories` | owner/slot item stacks and logical metadata |
| `Containers` | entity-to-inventory ownership and capacity metadata |
| `ServerSettings` | administrator-controlled settings |
| `Permissions` | subject/permission grants |
| `WorldMapDiscovery` | persistent per-player ownership/reveal state for purchased geographic-block maps |
| `HomeFurniture` | account-wide furniture instances, appearance variants, positions, rotations, and Home-storage state |

The prototype writes character movement and player-created structures. Character backpack stacks and account-wide Home storage-chest stacks share the `Inventories` table under separate owner IDs; storage-chest contents are exposed only while the character is inside that account's Home.

`RealityDeltas.Operation` is either `upsert` or `removed`. A player-created entity is an upsert. Removing it creates a tombstone rather than erasing the history contract. The same mechanism can later suppress a deterministic base tree or store a terrain patch. Backups consist of the SQLite files plus reality configuration; geographic cache can be regenerated.
# Runtime schema additions

`Accounts` stores unique case-insensitive usernames, salted PBKDF2 password hashes, hashed session tokens, and the active character. `AccountCharacters` supports up to eight characters per account. `AccountBases` keeps one persistent base-building assignment and its streamed world position per account and reality. `HomeFurniture` stores renderer-neutral logical furnishings separately from character inventory so every character on the account shares the same Home. The Home item chest is also account-wide and unlimited. `PlayerRelationships`, `DungeonDiscovery`, and `OpenedChests` support active sessions but dungeon rows are cleared whenever that player enters or exits the dungeon. `WorldMapDiscovery` survives ordinary exploration, reconnects, and world rebuilds. Inventory, hydration, worn equipment, equipped weapon, light state, buffs, wallet, location, and dirt-bike/motorcycle gallons are persisted with the character. Item category and realistic per-unit weight come from server item definitions; stacked quantities share a slot but multiply weight. The permanent fist is a virtual inventory item and does not need a database row.

Character rows also persist the body-heat meter, energy-drink deadlines, the five-minute `Probed` deadline, the one-minute candle burn deadline, equipped hat, shirt/jacket, pants/shorts, and renderer-neutral light state used by the mutually exclusive offhand slot. Schema initialization adds these columns in place for existing realities.

All database files, write-ahead logs, and final generated canonical world blocks remain under `data/` and are ignored by Git. New source geography/elevation responses are transient; legacy provider cache files are consumed once and deleted when that block is converted.
