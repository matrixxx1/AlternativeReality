# Roads and public buses

OSM roads keep their complete ordered geometry instead of dropping nodes at each map block boundary. Neighboring imports share canonical way IDs and OSM junction node IDs. Queries include a 500 m overlap. The generated-world cache is version 2 and the geographic import cache is version 5: old shortened geometry is regenerated on the next load; existing cache files and player saves are not deleted.

Road classifications distinguish freeways, highways, main roads, surface streets and paths. Routing respects one-way direction (including reverse ways and roundabouts), motor-vehicle/bus access, speed limits and node-based OSM turn restrictions. Crossings without a shared node do not become junctions. Metadata follows the [OSM highway definitions](https://wiki.openstreetmap.org/wiki/Key:highway) and [one-way rules](https://wiki.openstreetmap.org/wiki/Key:oneway).

Bus stops follow eligible road corridors with 1,609.344 meters between intermediate stops, plus terminal stops for short corridors. No passenger stops are generated on freeways, controlled-access roads, ramps, bridges or tunnels. Stops are separate for each legal travel direction, on the right side. Services use generated closed itineraries; these are game bus routes, not imported real transit timetables.

Click a blue **B** bus stop to choose **Wait for bus** or **View route**. Waiting requires standing within three meters on that stop's side. Route preview shows direction arrows, numbered stops and the ordered stop list without panning the main camera or requesting additional map blocks. Waiting can be cancelled; opening a preview does not start waiting. An approaching bus stops for waiting passengers and carries them until they press **Get off bus now**. The bus stops immediately, searches its right side for clear dry ground, drops them off and resumes after two seconds. If that exit is obstructed, it stops and reports the obstruction instead of putting the passenger inside an object or across the road.

The server simulates buses every 100 ms and checks oriented nine-by-2.5-meter footprints at sub-meter steps, including curved junction turns. Dead-end reversals follow a slow curve at 1.2 m/s. A bus hitting a player or NPC applies five hearts of damage and pushes them forward and sideways out of its path; God Mode retains its existing survival protection. Building/car/bus impacts stop the bus and damage both objects once per contact. Existing claimed-Home and building-destruction protections apply. Disabled buses release passengers when the right exit is clear. Persistent wrecks remain obstacles.

Click a bus itself for **Attack bus**, **Board bus / resume service**, or **View route**.
Attacks use the equipped weapon, its normal range measured to the bus hull,
ammo, accuracy, and cooldown. Disabled buses are not valid attack/boarding targets.
Direct boarding requires a stopped, serviceable bus and an outdoor player within
three meters of its right-hand door side. Boarding resumes that bus's service;
it does not repair damage. Bus damage is independent of the player-versus-player setting.

When nobody is riding or waiting for its route, a bus pulls onto a clear right
shoulder at 1.2 m/s instead of freezing in a traffic lane. A bus disabled by damage
also pulls over. The maneuver checks the whole body, loaded ground, water,
obstacles, people and other buses, and the final body must clear road rectangles.
If no safe space exists, it reports **waiting for safe pull-over** and retries;
it never teleports through obstacles just to clear traffic. Waiting passengers or
direct boarding make a healthy parked bus retrace its approach and safely rejoin
its saved route. Disabled buses never resume service automatically.

Map loading runs separately from movement. Buses wait before entering geography whose obstacles are not loaded; nearby, occupied or requested services request blocks ahead. Existing itineraries and stops are retained as neighboring blocks load. On disconnect/restart, a passenger's saved position is the safe boarding position rather than a dangling bus attachment.

Current limits: transit operates within the world's existing projection region. OSM quality and missing ways still limit real-world fidelity; the importer does not invent connections. Multi-way/conditional turn restrictions and traffic signals are not a complete traffic-law simulation. An obstructed route waits rather than phasing through obstacles. Vehicles and service positions reset on a server restart.

Validation: `dotnet test AlternateEarth.sln --no-restore`, `node --test tools/*.test.cjs`, and `node tools/transit-smoke.mjs` (isolated port 5081 fixture, WebSocket boarding/drop-off). Add `--keep-running` for manual browser checks; the launcher remains active until the test server is stopped. The active game on port 5080 is not touched by that check.

`SERVER_DLL=<built-server> node tools/bus-service-smoke.mjs` exercises parking,
direct boarding, resumed service and bus combat against an isolated fixture.
