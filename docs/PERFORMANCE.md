# Performance review — September 6, 2026

This pass inspected the client render loop, inventory and settings refreshes,
chat rendering, dungeon visibility, map streaming, server simulation broadcasts,
navigation, and persistence call sites. The existing 30 FPS rendering cap remains.

## Implemented

| Area | Avoided work | Behavior preserved |
| --- | --- | --- |
| Inventory | Recreating rows, canvas icons, and event handlers on every movement update | Item quantities, equipment, tabs, names, and slot limits still trigger refreshes. Wallet and capacity summaries update separately. |
| Server settings | Rebuilding the full form for identical private-state updates while moving indoors | Changed settings refresh the form; unchanged updates preserve edits in progress. |
| Speech bubbles | Repeated text wrapping and text-width measurement every frame | Position, animation, text, fonts, dimensions, and expiry remain unchanged. Measurements refresh when text, width, or fonts change. |
| Dungeon discovery | Scanning the discovered-cell array for every visibility query | A set contains exactly the supplied cells and refreshes with each new discovery array. |
| Server elevation | Sorting every elevation sample and allocating temporary arrays for every query | A stable selection of the same four nearest samples uses the original weighting and duplicate/tie order. |
| Client snapshots | Building and sorting a full-world static-object array before discarding it for the local map window | Client snapshots are assembled directly with local map detail. Actors, players, weather, locks, hazards, and other metadata are preserved; full snapshots remain available. |

## Verification

- 188 server tests passed, including exact comparison of old and new height
  calculations across empty, sparse, and large sample sets, equal-distance ties,
  duplicate coordinates, and zero managed allocations for repeated queries.
- 41 client tests passed, including unchanged inventory-node reuse across 120
  movement updates, refreshes after item/equipment changes, settings-form reuse,
  speech measurement reuse, and discovery-cache invalidation.
- A local Release-mode benchmark of 20,000 height queries over 135 samples took
  about 174 ms before and 57 ms after. Temporary managed allocations fell from
  90,559,720 bytes to zero, with identical result checksums. These are function
  benchmarks, not an end-to-end game FPS measurement.
- Client syntax and whitespace checks passed.

Run the regression checks with:

```powershell
dotnet test AlternateEarth.sln --no-restore --verbosity quiet
node --test tools/ui-performance.test.cjs tools/render-performance.test.cjs tools/map-streaming.test.cjs tools/commands.test.cjs tools/combat-effects.test.cjs
node --check src/AlternateEarth.Client2D/app.js
git diff --check
```

## Candidates for a later measured pass

- Indoor movement sends a full private-state payload. A smaller update could
  reduce serialization and browser parsing, but must continue delivering discovery,
  storage, inventory, and quest changes correctly.
- Map-window requests still scan all server static objects and their geometry.
  A dedicated bounds index could reduce this cost as the world grows; invalidation
  must handle area loading, destruction, rebuilding, and crossing geometry.
- Broadcasts serialize identical messages separately for each recipient. Sharing
  the serialized payload would help multiplayer loads; this is unlikely to explain
  low FPS with one connected player.
- Persistence opens SQLite connections with pooling disabled. Measure database
  time and contention before changing connection reuse or save scheduling.

These candidates were identified from source inspection, not established as the
remaining live bottleneck. No simulation rates, saving guarantees, visual quality,
or network update frequency were reduced in this pass.

## Initial entry and readiness follow-up

The startup camera was initialized only when `state.frame === 0`, but connecting
frames already incremented that counter. A new session could therefore glide from
coordinate zero to its actual player position, requesting unrelated unloaded map
blocks along the way. A map request awaited inside the server receive loop can
delay commands behind it for the duration of a geographic fetch.

The welcome handler now places the camera directly on the character before map
requests can run. Initial nearby-block prefetch waits until five seconds after a
successful readiness check, and the hidden server-settings form is populated only
when opened.

A blocking loading overlay shows connection and world-loading progress. Gameplay
unlocks after a correlated ping reply. If a subsequent probe goes unanswered for
2.5 seconds, the overlay returns with elapsed time, clears pending local movement,
and pauses new gameplay commands until the server replies. Probes remain available
while the character is asleep or abducted. Disconnects reset readiness, and stale
probe replies cannot unlock a new session.

The follow-up passed 188 server tests and 46 client tests. An isolated server using
a copy of the cached world reported the first loading stage after 13 ms, its first
welcome after 2,665 ms, and the following posture-command reply after 2 ms. A second
entry reported 1,653 ms to welcome and 1 ms for the command reply. These are server
handshake measurements with cached geography, not guarantees for uncached areas or
end-to-end browser startup time.

Additional checks:

```powershell
node --test tools/startup-readiness.test.cjs
# Against an isolated server with a separate database and copied world cache:
node tools/startup-smoke.mjs http://127.0.0.1:5081
```
