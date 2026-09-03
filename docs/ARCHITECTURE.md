# Architecture

## North star

The reality server owns a canonical simulation expressed in meters and logical entities. Clients send intentions such as move, place, attack, or interact. The server validates and applies them, then publishes logical state changes. A sprite, mesh, sound, animation, or client physics body is never authoritative.

```text
 Geographic providers ──> deterministic generator ──> canonical base chunks
                                                        + reality deltas
                                                               │
                                                        Reality Server
                                                        WebSocket protocol
                                                ┌──────────────┴──────────────┐
                                          2D presentation              3D presentation
```

Each hosted server is one independent reality with its own configuration, seed, characters, permissions, and deltas. Direct connection is sufficient. A future directory service may advertise servers but must never own gameplay state or be required after discovery.

## Technology decision

The canonical server, coordinate math, protocol records, persistence, and geographic conversion use .NET 8/C#. ASP.NET Core supplies a mature cross-platform host and WebSocket stack. SQLite is embedded, easy to back up, and adequate for a small server; repository interfaces and logical IDs keep a later PostgreSQL implementation possible.

Browser clients are the near-term and default presentation technology. The current dependency-free Canvas client handles the top-down view. A later browser 3D client can use WebGL through an open-source library such as Babylon.js or Three.js while consuming the exact same protocol. This does not constrain the canonical world to tiles: roads and footprints are already continuous vector geometry, and terrain can become meshes in WebGL.

Native engines remain possible later, but are intentionally out of scope for now. Keeping the wire protocol renderer-neutral preserves that option without adding an engine toolchain to the current project.

## Repository topology

```text
src/
  AlternateEarth.Shared/     coordinates, entities, configuration, wire DTOs
  AlternateEarth.Geo/        provider interfaces, OSM importer, elevation, generator
  AlternateEarth.Server/     authoritative state, WebSockets, SQLite, static host
  AlternateEarth.Client2D/   current Canvas renderer and input only
  AlternateEarth.Client3D/   future browser WebGL client boundary
tests/AlternateEarth.Tests/  coordinate, generation, and persistence tests
tools/                       end-to-end protocol smoke tests and future import tools
docs/                        architecture and contracts
data/                        runtime SQLite database and geographic caches, ignored by Git
```

Dependencies point inward: clients, server, geographic adapters, and tools may depend on `Shared`; `Shared` depends on none of them. The server never references client code or asset identifiers.

## Canonical coordinate model

WGS84 latitude/longitude is an import and discovery coordinate, not a gameplay coordinate. A deterministic one-degree `RegionId` is derived with `floor(latitude)` and `floor(longitude)`. Within a region, a WGS84 local tangent projection produces continuous meters:

- `X`: meters east of the region origin
- `Y`: meters north of the region origin
- `Z`: meters above the selected elevation datum
- one simulation unit equals one meter

The prototype area must fit inside one region. Region-edge traversal will convert through ECEF and enter the adjacent local frame; that is deliberately a later, isolated coordinate feature. It will not alter entity semantics or client rendering. This model avoids Web Mercator's severe scale distortion while keeping ordinary simulation math local and stable.

Exact device location is unnecessary. Reality creation can accept a city, postal code, or selected map point; a server may randomize character spawn within a privacy radius before producing a canonical position.

## Chunking

`ChunkCoordinate.FromPosition` uses mathematical floor division and a current default of 256 meters. Negative local coordinates therefore map correctly. The first 2 km prototype loads one area snapshot, but the contract supports requesting chunks independently.

The next streaming step is an 8 × 8-chunk active region (2,048 meters square) around each player. Base chunks are immutable and cacheable by provider version, area, source revision, and generator version. Reality deltas are loaded only for active chunks. Sizes remain configuration/performance choices, not protocol assumptions.

## Shared entity model

`CanonicalEntity` currently carries the minimal component set needed by the slice:

- stable logical `Id`
- renderer-neutral `EntityKind`
- canonical `WorldPosition`
- optional logical geometry in meters
- logical properties such as road classification, tree species, owner, and structure type
- monotonic version
- base-versus-reality provenance

As behavior grows, this record splits into typed components (`Position`, `Health`, `Collision`, `Inventory`, `Owner`, `Harvestable`, and structure pieces). Component IDs and data stay graphical-engine independent. Clients map `tree/pine` or `playerStructure/wooden-wall` to their own sprite or mesh registries.

Player buildings will be stored as foundations and logical pieces—wall segment, opening, material, height, orientation—not as meshes. Placement is continuous but may snap to the implemented 0.5-meter construction grid.

## Authority and synchronization

The server bounds movement by real meter-per-second rates for each canonical terrain, samples elevation, rejects solid-object intersections, and broadcasts accepted movement. Its spatial index handles building polygons, tree radii, fence lines, vehicles, structures, and shallow/deep water. Click-to-walk runs bounded A* with terrain costs and line-of-sight smoothing; deep water is excluded, while direct entry causes an authoritative drowning/reset event. Object placement and removal are currently rejected because this milestone is exploration-only; the dormant implementation remains behind `ObjectPlacementEnabled` for a later construction milestone.

Character records are scoped by `RealityId`; cross-reality transfer is intentionally absent. Single-player is the same server executable bound locally with a private configuration. Changing visibility later does not require world conversion.

The browser client uses a 3/4 oblique camera, visual ground tiles, animated sprites, and depth-sorted raised geometry. It changes from full to medium to light rendering as the camera zooms out, and suppresses distant animation. This is presentation only: tile boundaries do not constrain movement, collision, entities, or protocol coordinates.

## Geographic abstraction

`IGeographicProvider.GetAreaAsync` returns a canonical `GeographicDataset`, and `IElevationProvider` returns meter samples. The current adapters query OpenStreetMap through Overpass with endpoint failover and SRTM90m elevation through OpenTopoData. Raw and converted results are cached before play, so normal movement generates no public map API traffic.

Provider data remains descriptive—road way, building polygon, water feature, elevation sample. `DeterministicWorldGenerator` performs the game conversion and adds seed-driven resource entities. Future providers can read regional PBF extracts, GeoPackage files, government elevation rasters, or offline packs without changing server or client code.

Road metadata produces separate canonical road and sidewalk traversal bands. Land use and natural polygons become grass, forest, sand, mud, or pavement. Every building receives a deterministic logical door facing the nearest available sidewalk, falling back to a road. Closed water polygons use a three-meter shallow shoreline band and a deep interior.

`IWeatherProvider` keeps weather acquisition separate from simulation. The current Open-Meteo adapter samples the reality location, stores one canonical `WeatherState`, refreshes it hourly, and broadcasts changes. Sunrise, sunset, moon phase, and moon illumination drive client-side ambient presentation; clients render rain, snow, or darkness without becoming authoritative for weather.

## Deterministic generation and delta persistence

The pipeline is:

1. Resolve WGS84 area and provider/version cache key.
2. Read or download raw geographic features.
3. Project nodes into region-local meters.
4. Convert source tags into canonical roads, buildings, and water.
5. Sample/cache elevation.
6. Add procedural entities from `(reality seed, region, generator version)`.
7. Overlay SQLite upserts and tombstones.
8. send logical snapshots/events to clients.

Generated resource IDs and positions are stable for the same inputs. Untouched base entities are not copied into each reality database. SQLite stores only characters, settings, permissions, inventories, and changes. A future generator upgrade needs an explicit version and migration/rebase policy so old deltas never silently target different base objects.

## Near-term implementation order

The completed slice proves shared models, local-meter coordinates, real geographic import, deterministic terrain generation, authoritative collision/pathfinding, hourly weather, health, regenerating stamina and travel modes, wandering server actors, persistent characters, browser exploration, and two-client synchronization. Next: chunk-specific snapshots, authentication/admin controls, harvesting/inventory, structured building pieces, then a minimal browser WebGL 3D observer using the same protocol.
