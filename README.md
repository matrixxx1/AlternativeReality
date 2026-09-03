# Alternate Earth

A renderer-neutral, self-hosted multiplayer world derived from real geography. One authoritative reality server drives every client: the current browser-based top-down client and, later, full Godot 2D and 3D clients.

The first vertical slice is runnable now. It imports and caches a 2 km × 2 km area of OpenStreetMap around downtown Vancouver, Washington; samples elevation; renders roads, building footprints, water, trees, and players; accepts server-validated movement and object placement; broadcasts changes over WebSockets; and persists reality deltas and character positions in SQLite.

## Chosen stack

- **Server and canonical model:** C# / .NET 8 and ASP.NET Core. It is cross-platform, fast, debuggable, Docker-friendly, and has a strong typed contract shared with future Godot C# clients.
- **Production clients:** Godot 4 with C#. Godot has first-class 2D and 3D renderers, is open source, and lets both presentations share protocol/model code without making either renderer authoritative.
- **Prototype 2D client:** dependency-free HTML Canvas and JavaScript, served by the reality server. This proves the architecture immediately and makes the two-client synchronization test as easy as opening two tabs.
- **Networking:** JSON over WebSockets for the prototype. Messages describe logical actions and entities. Binary serialization can be added after profiling without changing simulation semantics.
- **Persistence:** SQLite in WAL mode. Only characters and reality deltas are saved; untouched geographic features regenerate from cache and seed.
- **Geography:** cached OpenStreetMap/Overpass plus a cached SRTM90m elevation grid through OpenTopoData, behind provider interfaces.

See [Architecture](docs/ARCHITECTURE.md), [Protocol](docs/PROTOCOL.md), and [database schema](docs/DATABASE.md) for the concrete design.

## Run locally

Requires the .NET 8 SDK. The first run needs internet access to populate the geographic cache; subsequent runs use the cache.

```powershell
dotnet run --project src/AlternateEarth.Server
```

Open [http://localhost:5080](http://localhost:5080) in two browser tabs. Give each a readable name with URLs such as `http://localhost:5080/?name=Matt` and `http://localhost:5080/?name=Friend`.

- Move with WASD or the arrow keys.
- Left-click within the gold five-meter ring to place a crate.
- Right-click a nearby player-created object to remove it.
- Use the mouse wheel to zoom.

The reality configuration—including center, size, name, seed, and player limit—is in `src/AlternateEarth.Server/appsettings.json`. Runtime state is written under `data/` and is intentionally not committed.

## Verify

```powershell
dotnet test AlternateEarth.sln
node tools/smoke-test.mjs
```

The smoke test opens two real WebSocket clients, places one object through client A, confirms both receive the same canonical entity, and checks the authoritative world snapshot. Restart the server and query `/api/world` to prove the SQLite delta reload path.

## Docker

```powershell
docker compose up --build
```

The container listens on port 8080 internally and is mapped to [http://localhost:5080](http://localhost:5080). Its database and map cache live in the mounted `data` directory.

## Geographic attribution

Map features are © OpenStreetMap contributors and used under the [Open Database License](https://www.openstreetmap.org/copyright). Elevation is requested from an OpenTopoData SRTM90m endpoint and cached locally.
