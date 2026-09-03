# Renderer-neutral protocol v11

The prototype uses camel-case JSON text frames over WebSockets at `/ws`. Browser clients authenticate with an HTTP-only account-session cookie. The server selects the active character and sends `welcome` with an authoritative public snapshot plus player-private inventory, relationships, base, dungeon, chest, and loot state.

## Client to server

| Type | Logical payload | Server validation |
|---|---|---|
| `moveRequest` | direction `x`, `y`; client `sequence` | normalizes direction, bounds elapsed time/speed, clamps world bounds |
| `pathRequest` | destination `x`, `y`; client `sequence` | bounds range, avoids deep water and canonical collision geometry, applies terrain traversal costs |
| `setTravelMode` | walk, run, skateboard, bike, dirt bike, motorcycle, or raft | validates terrain, equipment ownership, and motorized-vehicle fuel |
| `setGodMode`, `setLights`, `setEquipment`, `setMagicHikingShoes`, `setMagicRunningShoes` | requested administrative, light, and equipped-clothing state | persists state, prevents footwear bonus stacking, and bypasses ownership and fuel checks only while God Mode is active |
| `enterDungeon`, `exitDungeon`, `restAtBed` | logical door/bed intent | validates proximity, ownership, and current location; dungeon session state is fresh on entry and discarded on exit |
| `purchaseBase` | logical building door ID | validates proximity, account ownership, price, and wallet; changes the account-wide base |
| `attack` | target ID plus the client's current weapon hint | ignores spoofed weapon strength and uses the server-persisted equipped weapon; validates ownership, ammo, range, accuracy, damage, death, fallback, and relationship changes |
| `requestTrade`, `confirmTrade`, `consumeItem` | merchant/item intent | validates proximity, inventory, stock, wallet, effects, and adding purchased gasoline to the selected vehicle's tank |
| `openChest`, `chestSeen`, `collectLoot` | world-object intent | validates visibility/proximity and authoritative rewards |
| `requestArea` | local-meter camera location | streams and locally caches another geographic cell |
| `rebuildArea` | `godMode` acknowledgement | requires a connected character and explicit God Mode; resets all loaded regions and transient world state, regenerates, and returns players to their bases |
| `teleport` | destination `x`, `y`; `godMode` acknowledgement | requires God Mode and resolves the requested point to a safe canonical destination |
| `say` | `message` text | derives username/player ID and UTC time on the server; rejects blank, rapid, or over-180-character messages |
| `placeObject` | reserved logical `objectType`, `x`, `y`, `rotationDegrees` | currently rejected unless an administrator enables object placement |
| `removeObject` | reserved `entityId` | currently rejected unless an administrator enables object placement |
| `requestChunk` | chunk `x`, `y` | prototype returns the area snapshot; chunk filtering is next |
| `ping` | none | no state mutation |

The exploration client sends movement, path, equipment, attack, trade, area, and ping requests. Gameplay commands contain intent and references, never final authoritative results. Left-click selects an NPC or player target; right-click does not attack.

## Server to client

| Type | Purpose |
|---|---|
| `welcome` | protocol version, assigned player, complete initial snapshot |
| `chunkSnapshot` | authoritative base and reality state for requested scope |
| `playerJoined`, `playerMoved`, `playerUpdated`, `playerLeft` | multiplayer presence, health, and travel mode |
| `actorsMoved` | server-simulated wildlife and NPC position/state changes |
| `privateState`, `dungeonEntered`, `dungeonUpdated`, `dungeonExited` | character-private inventory, base, relationships, fog, and interior state |
| `tradeQuote`, `tradeCompleted`, `basePurchased`, `combatEvent`, `chestOpened`, `lootCollected` | authoritative gameplay outcomes |
| `worldExpanded` | merged snapshot after another local geographic cell loads |
| `playerFell`, `playerDied` | authoritative damage, death, and safe respawn outcomes |
| `worldRebuilt` | regenerated base snapshot and safe positions after a God Mode refresh |
| `playerTeleported` | authoritative instant relocation visible to every connected client |
| `chatSaid` | server-authored player ID, username, text, and timestamp for ephemeral speech and local history |
| `objectCreated`, `objectRemoved` | accepted reality delta events |
| `error` | rejected command with safe message |
| `pong` | liveness response |

Messages contain `EntityKind`, meter positions, geometry, properties, IDs, and versions. They never mention a sprite, texture, scene, model, animation, or renderer. Version negotiation will reject incompatible clients before binary serialization or mod manifests are introduced.

Dirt-bike and motorcycle tanks are separate authoritative character fields. Movement consumes gas from distance traveled at 50 mpg and 45 mpg respectively; an empty tank blocks motorized movement. God Mode neither checks nor consumes gasoline.

`pathResult` contains renderer-neutral meter waypoints. `pathUnavailable`, `movementBlocked`, `playerFell`, and `playerDied` explain authoritative navigation outcomes. `weatherChanged` carries the current condition, temperature, precipitation, wind, daylight state, sunrise, sunset, moon phase, observation time, and provider; clients independently decide how to present those facts.
