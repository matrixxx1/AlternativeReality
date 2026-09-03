# AlternativeReality

A renderer-neutral, self-hosted multiplayer world derived from real geography. One authoritative reality server drives every client: the current browser-based top-down client and, later, a browser-based WebGL 3D client.

The playable vertical slice imports OpenStreetMap geography around downtown Vancouver, Washington as reference data; it never displays a real map. New 2 km geographic cells generate and cache locally as the camera or player explores beyond the loaded area. It builds a stylized angled tile world with roads, sidewalks, textured terrain, complete buildings, vegetation, wildlife, NPCs, vehicles, weather, dungeons, private bases, and players. The authoritative server owns accounts, inventory, movement, combat, trade, health, hydration, stamina, hourly weather, multiplayer synchronization, and SQLite persistence. Normal object modification remains disabled during this exploration milestone.

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

Open [http://localhost:5080](http://localhost:5080). A first-time browser asks for a unique 3–10 character username and password, then stores an opaque server-issued session in an HTTP-only cookie. Returning players use the same credentials on a new device.

For access from other devices on the same network, run `tools/open-lan-port.ps1` once from an Administrator PowerShell. It creates an inbound TCP 5080 rule restricted to `LocalSubnet`. Other devices can then open `http://<server-lan-ip>:5080`.

- Move with WASD or the arrow keys.
- Left-click anywhere in the world to walk toward that location.
- Use WASD or an arrow key to take direct control and cancel click-to-walk.
- Right-drag to pan the camera; a stationary right-click opens the action window.
- Use the mouse wheel to zoom.
- Choose Walk, Run, Skateboard, Bike, Dirt bike, Motorcycle, or Raft. Equipment must be bought before use; God Mode bypasses ownership checks. Running drains stamina, dehydration halves speed, skateboards fail off paved surfaces, bikes retain most of their speed off-road, and rafts make deep water safe.
- Dirt bikes cost $3,000–$5,000 and reach 40 mph; motorcycles reach 90 mph and currently cost $5,000–$10,000. Their separate 2- and 4-gallon tanks consume fuel by actual distance at representative 50 and 45 mpg. Merchants sell gallons for $5–$10 at one advertised randomized price. Empty motorized vehicles cannot move until refueled; God Mode bypasses both fuel checks and consumption.
- God Mode supplies a 5× movement multiplier, full water/stamina, a $500 wallet floor, health protection/regeneration, equipment access, rebuilding, and teleporting.
- Flashlight, lantern, and laser checkboxes control persistent equipment. A flashlight casts a facing cone, a lantern lights a circle, and the laser draws a straight beam to the first solid object. Each requires its purchased item unless God Mode is active; randomized merchants price lasers from $200–$400.
- Magic hiking shoes are randomized merchant gear priced from $100–$400. Equipping them doubles walking and running speed, halves running stamina drain, and removes the normal mud, grass, and shallow-water speed penalties. God Mode can equip them without ownership.
- Magic running shoes are randomized merchant gear priced from $100–$400. Equipping them triples walking and running speed and halves running stamina drain except on roads and sidewalks. Running and hiking shoes are mutually exclusive, so their bonuses cannot stack.
- The inventory includes a paper-doll equipment view for shoes, pants, shirt, gloves, and hat. Hats cost $30–$75 and halve water drain while worn; clothing slots without available items remain visible for future gear.
- Right-click an NPC to throw a rock, shoot a slingshot, or trade with merchants. Range and accuracy are server-authoritative; hostility can cause pursuit and attacks. Defeated actors leave temporary treasure.
- Building doors lead to footprint-sized procedural dungeons with persistent fog of war, enemies, chests, treasure, and an exit. Each account also receives a permanent base building, shown only to its owner with a compass and flag. Its safe interior contains a fireplace and furniture; clicking the bed restores health, water, stamina, and both five-minute protections. Character management is available only inside the base.
- Use the bottom chat box and **Say** button to speak. Nearby visible clients see a ten-second speech bubble over the character; **Show chat** opens a client-local history of the last ten messages said or seen, with username and time.
- Wildlife and human NPCs use the same speech system. Birds squeak or call, cats meow, dogs bark, and residents tell random jokes on independently randomized schedules ranging from two to thirty minutes.

Paved surfaces use a representative 3.5 mph walking speed, forest uses 2 mph, sand uses 1 mph, and mud, grass, and shallow water have their own canonical rates. Click-to-walk asks the server for a route around solid geometry. Deep water is deliberately excluded from routes; manually entering it drowns and resets the character to a safe starting point.

Real street names are drawn over the stylized road network when OpenStreetMap supplies them. Moving the camera into a new geographic cell shows a loading message; the newly generated terrain arrives under fog of war and is revealed around the player during exploration. Generated maps and raw geographic caches stay local to each reality server under `data/`.

The reality configuration—including center, size, name, seed, and player limit—is in `src/AlternateEarth.Server/appsettings.json`. Runtime state is written under `data/` and is intentionally not committed.

## Verify

```powershell
dotnet test AlternateEarth.sln
node tools/smoke-test.mjs
```

The smoke test creates two authenticated accounts, confirms base assignment, opens two real WebSocket clients, synchronizes movement and chat, verifies God Mode resources and pathfinding, and confirms that exploration-only object placement remains blocked.

## Geographic attribution

Map features are © OpenStreetMap contributors and used under the [Open Database License](https://www.openstreetmap.org/copyright). Elevation is requested from an OpenTopoData SRTM90m endpoint and cached locally.

Current temperature, precipitation, and conditions are supplied by [Open-Meteo](https://open-meteo.com/) and refreshed by the reality server hourly. The client derives sunlight from sunrise/sunset, shows the current moon phase, brightens full-moon nights, and gives the player a small local light in darkness.
