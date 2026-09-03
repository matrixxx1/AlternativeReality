# Alternate Earth

A renderer-neutral, self-hosted multiplayer world derived from real geography. One authoritative reality server drives every client: the current browser-based top-down client and, later, a browser-based WebGL 3D client.

The playable vertical slice imports and caches a 2 km × 2 km area of OpenStreetMap around downtown Vancouver, Washington; samples elevation; and renders terrain, roads, sidewalks, buildings, doors, water, trees, fences, vehicles, and players in an angled tile-styled 2D view. The authoritative server applies terrain-dependent real-world walking speeds, collision, route finding, elevation, deep-water drowning, live geographic weather, multiplayer synchronization, and SQLite character persistence. Object modification remains disabled during this exploration milestone.

## Chosen stack

- **Server and canonical model:** C# / .NET 8 and ASP.NET Core. It is cross-platform, fast, easy to debug and publish as a native service, and exposes a renderer-neutral protocol.
- **Browser clients:** dependency-free HTML Canvas and JavaScript for 2D now, with WebGL through an open-source library such as Babylon.js for the later 3D view. Browsers are not limited to tile graphics; the current client already renders projected vector roads and footprints.
- **Shared contract:** canonical C# records define server state, while the versioned JSON protocol is the client boundary. TypeScript definitions can be generated from that contract as the browser client grows.
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

For access from other devices on the same network, run `tools/open-lan-port.ps1` once from an Administrator PowerShell. It creates an inbound TCP 5080 rule restricted to `LocalSubnet`. Other devices can then open `http://<server-lan-ip>:5080`.

- Move with WASD or the arrow keys.
- Left-click anywhere in the world to walk toward that location.
- Use WASD or an arrow key to take direct control and cancel click-to-walk.
- Right-drag to pan the camera; a stationary right-click opens the action window.
- Use the mouse wheel to zoom.

Paved surfaces use a representative 3.5 mph walking speed, forest uses 2 mph, sand uses 1 mph, and mud, grass, and shallow water have their own canonical rates. Click-to-walk asks the server for a route around solid geometry. Deep water is deliberately excluded from routes; manually entering it drowns and resets the character to a safe starting point.

The reality configuration—including center, size, name, seed, and player limit—is in `src/AlternateEarth.Server/appsettings.json`. Runtime state is written under `data/` and is intentionally not committed.

## Verify

```powershell
dotnet test AlternateEarth.sln
node tools/smoke-test.mjs
```

The smoke test opens two real WebSocket clients, moves client A, confirms both receive the same authoritative position, requests a server-computed walking path, and verifies that the server rejects object placement while exploration-only mode is active.

## Geographic attribution

Map features are © OpenStreetMap contributors and used under the [Open Database License](https://www.openstreetmap.org/copyright). Elevation is requested from an OpenTopoData SRTM90m endpoint and cached locally.

Current weather is supplied by [Open-Meteo](https://open-meteo.com/) and refreshed by the reality server every ten minutes.
