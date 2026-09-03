# Renderer-neutral protocol v4

The prototype uses camel-case JSON text frames over WebSockets at `/ws`. Clients connect with a server-issued or locally remembered character ID and a display name. The server sends `welcome` with the assigned player ID and an authoritative `WorldSnapshot`.

## Client to server

| Type | Logical payload | Server validation |
|---|---|---|
| `moveRequest` | direction `x`, `y`; client `sequence` | normalizes direction, bounds elapsed time/speed, clamps world bounds |
| `pathRequest` | destination `x`, `y`; client `sequence` | bounds range, avoids deep water and canonical collision geometry, applies terrain traversal costs |
| `setTravelMode` | logical `mode`: walk, run, skateboard, or bike | validates the enum and persists the authoritative character mode |
| `rebuildArea` | `godMode` acknowledgement | requires a connected character and explicit God Mode; clears deltas, regenerates, safely repositions players |
| `teleport` | destination `x`, `y`; `godMode` acknowledgement | requires God Mode and resolves the requested point to a safe canonical destination |
| `placeObject` | reserved logical `objectType`, `x`, `y`, `rotationDegrees` | currently rejected unless an administrator enables object placement |
| `removeObject` | reserved `entityId` | currently rejected unless an administrator enables object placement |
| `requestChunk` | chunk `x`, `y` | prototype returns the area snapshot; chunk filtering is next |
| `ping` | none | no state mutation |

The exploration client currently sends movement, path, chunk, and ping requests. Future gameplay follows the same command pattern: `InteractRequest`, `AttackRequest`, `PickupItem`, `CraftItem`, and `OpenContainer` contain intent and references, never final authoritative results.

## Server to client

| Type | Purpose |
|---|---|
| `welcome` | protocol version, assigned player, complete initial snapshot |
| `chunkSnapshot` | authoritative base and reality state for requested scope |
| `playerJoined`, `playerMoved`, `playerUpdated`, `playerLeft` | multiplayer presence, health, and travel mode |
| `actorsMoved` | server-simulated wildlife and NPC position/state changes |
| `playerFell`, `playerDied` | authoritative damage, death, and safe respawn outcomes |
| `worldRebuilt` | regenerated base snapshot and safe positions after a God Mode refresh |
| `playerTeleported` | authoritative instant relocation visible to every connected client |
| `objectCreated`, `objectRemoved` | accepted reality delta events |
| `error` | rejected command with safe message |
| `pong` | liveness response |

Messages contain `EntityKind`, meter positions, geometry, properties, IDs, and versions. They never mention a sprite, texture, scene, model, animation, or renderer. Version negotiation will reject incompatible clients before binary serialization or mod manifests are introduced.

`pathResult` contains renderer-neutral meter waypoints. `pathUnavailable`, `movementBlocked`, `playerFell`, and `playerDied` explain authoritative navigation outcomes. `weatherChanged` carries the current condition, temperature, precipitation, wind, daylight state, sunrise, sunset, moon phase, observation time, and provider; clients independently decide how to present those facts.
