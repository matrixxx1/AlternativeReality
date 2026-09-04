# Renderer-neutral protocol v29

The prototype uses camel-case JSON text frames over WebSockets at `/ws`. Browser clients authenticate with an HTTP-only account-session cookie. The server selects the active character and sends `welcome` with an authoritative public snapshot plus player-private inventory, relationships, base, dungeon, chest, and loot state. Snapshots include exact `loadedAreas` cells in addition to aggregate bounds so clients never mistake an unloaded gap for generated geography.

## Client to server

| Type | Logical payload | Server validation |
|---|---|---|
| `moveRequest` | direction `x`, `y`; client `sequence`; optional remaining-distance cap and waypoint coordinates | recomputes direction and distance from the authoritative position, bounds elapsed time/speed, prevents queued fast travel from overshooting, and clamps world bounds |
| `pathRequest` | destination `x`, `y`; client `sequence` | bounds range, avoids deep water and canonical collision geometry, applies terrain traversal costs |
| `setTravelMode` | walk, run, skateboard, bike, dirt bike, motorcycle, or raft | validates terrain, equipment ownership, motorized-vehicle fuel, and indoor vehicle restrictions |
| `setEquipment` | logical hat, shirt, pants, shoes, offhand, or weapon slot and item; null to unequip | validates ownership and slot compatibility; offhand light equipment is mutually exclusive, and the weapon slot supports a true empty state while fists remain permanently available |
| `updateItemConfiguration` | item ID, damage, range, additive speed/visibility modifiers, minimum price, maximum price | requires authoritative God Mode, validates safe bounds, persists server-wide rules, and invalidates existing merchant quotes |
| `configureInventoryItem` | item ID and `take` or `give` action | requires authoritative God Mode; `take` adds one to account-wide Home inventory, while `give` removes one from Home first and falls back to the backpack |
| `dropItem` | item ID and quantity | removes owned inventory server-side, normalizes equipment/travel state, and creates collectible ground loot; the permanent fist cannot be dropped |
| `updateMovementConfiguration` | base speed, base visibility, terrain modifiers, travel-mode modifiers | requires authoritative God Mode, validates safe bounds, and persists additive movement/visibility rules |
| `updateServerEvents` | simulated clock offset, weather mode/temperature, light/merchant/door refreshes, and UFO/T-Rex/bear intervals/durations | requires authoritative God Mode, validates bounded values, persists the reality-wide schedule, and never changes the host operating-system clock |
| `setGodMode`, `setLights`, `setEquipment`, `setMagicHikingShoes`, `setMagicRunningShoes` | requested administrative, light, and equipped-clothing state | persists state, prevents footwear bonus stacking, and bypasses ownership and fuel checks only while God Mode is active |
| `triggerWorldEvent` | UFO, T-Rex, or bear event type | requires authoritative God Mode and creates or resets one bounded manual event actor near the triggering player's outdoor position |
| `enterDungeon`, `exitDungeon`, `restAtBed` | logical door/bed intent | validates proximity, ownership, current location, and the active configured door-lock cycle; dungeon session state is fresh on entry and discarded on exit |
| `changeDungeonLevel` | stair direction `-1` up or `1` down | requires a multi-level dungeon, valid floor boundary, and proximity to the shared stairwell |
| `moveFurniture`, `placeFurniture`, `rotateFurniture`, `storeFurniture` | furniture instance and requested logical Home position/action | requires the owner's Home; validates walls, doors, exit clearance, other furniture, rotation, and storage rules before persisting |
| `requestQuest`, `acceptQuest`, `completeQuest`, `abandonQuest`, `captureQuestPet` | actor/quest identifiers only | quest generation, objectives, inventory transfer, and rewards remain server-authoritative |
| `chopVegetation`, `attackWorldObject` | logical world entity identifier | validates weapon, range, rewards, persistent vegetation deltas, crimes, wanted level, and delayed police response |
| `pickLock` | door ID | requires a lock-pick set (or God Mode), validates 3 m range, rolls 15% success, and applies witnessed-crime / 10% police-call rules server-side |

Daily UFOs are ordinary logical actors with subtype `ufo`; green-beam strikes use the existing authoritative `combatEvent` shape with weapon `greenBeam` and 10-heart damage.
| `openHomeStorage`, `transferHomeStorage` | built-in chest ID, item ID, direction, and quantity | requires the owner's Home and its actual storage chest; deposits persist without a capacity limit, while withdrawals enforce backpack category slots and weight |
| `purchaseBase` | logical building door ID | validates proximity, account ownership, and the normal displayed price; changes the account-wide base, with God Mode bypassing affordability and deduction |
| `attack` | target ID plus the client's current weapon hint | ignores spoofed weapon strength and uses the server-persisted equipped weapon; validates ownership, ammo, range, clear line of sight, accuracy, damage, death, fallback, and relationship changes |
| `requestTrade`, `confirmTrade`, `consumeItem` | merchant/item intent | validates proximity, categorized store stock, wallet, effects, gasoline, and persistent current-block map discovery; God Mode purchases retain normal prices but bypass affordability and wallet deduction |
| `openChest`, `takeChestItems`, `chestSeen`, `collectLoot` | chest/loot IDs and selected item quantities | validates proximity; chest cash is collected automatically, while item selection remains authoritative and capacity checked |
| `requestArea` | local-meter viewport location | streams and locally caches another geographic cell before the player enters it |

Authenticated HTTP `POST /api/world/prefetch` accepts a target block and movement origin. It prepares nearby canonical blocks on the server without activating them or adding them to a client snapshot. `GET /api/diagnostics` supplies the in-game Performance panel with current map work, cache/load timings, block/entity counts, elevation samples, and process memory.
| `rebuildArea` | legacy `godMode` acknowledgement | checks only the connected character's authoritative God Mode state; resets all loaded regions and transient world state, regenerates, and returns players to their bases |
| `teleport` | destination `x`, `y`; legacy `godMode` acknowledgement | checks only authoritative God Mode, generates an unloaded destination first, then resolves it to a safe canonical point |
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
| `worldEventTriggered` | authoritative manual event actor and announcement for every connected client |
| `doorLocksChanged` | replacement door-lock state and the end of the configured active cycle |
| `privateState`, `dungeonEntered`, `dungeonLevelChanged`, `dungeonUpdated`, `dungeonExited` | character-private inventory, base, relationships, fog, floor number, and interior state |
| `tradeQuote`, `tradeCompleted`, `basePurchased`, `combatEvent`, `chestOpened`, `chestItemsTaken`, `lootCollected` | authoritative gameplay outcomes |
| `configuredInventoryAdjusted` | updated private inventory state and the source/destination used by a God Mode item adjustment |
| `worldExpanded` | merged snapshot after another local geographic cell loads |
| `taskStatus` | names server work that may temporarily delay movement or rendering |
| `homeUpdated`, `homeStorageOpened`, `homeStorageUpdated` | authoritative Home furnishings and private furniture/item-storage state after placement, movement, rotation, or item transfer |
| `playerFell`, `playerDied` | authoritative damage, death, and safe respawn outcomes |
| `worldRebuilt` | regenerated base snapshot and safe positions after a God Mode refresh |
| `playerTeleported` | authoritative instant relocation visible to every connected client |
| `chatSaid` | server-authored player ID, username, text, and timestamp for ephemeral speech and local history |
| `objectCreated`, `objectRemoved` | accepted reality delta events |
| `error` | rejected command with safe message |
| `pong` | liveness response |

Messages contain `EntityKind`, meter positions, geometry, properties, IDs, and versions. They never mention a sprite, texture, scene, model, animation, or renderer. Version negotiation will reject incompatible clients before binary serialization or mod manifests are introduced.

Interior state carries the normalized source-building footprint, exterior-wall count, doorway, current level, total level count, optional shared stair position, and footprint-derived difficulty. Buildings up to about 2,000 square feet start at Difficulty 1, buildings near 10,000 square feet reach about Difficulty 50, and anything above Difficulty 50 is a Stronghold. Difficulty biases floor count, inhabitant population, health, and weapon loadouts; Homes and stores remain safe, single-level exceptions.

Snapshots carry authoritative door-lock state and the current cycle's UTC end time. Approximately 90% of ordinary buildings are locked in each deterministic configured cycle. A player's own base is always enterable, and a building property of `questItem=true` (with `quest:item=true` accepted as an alias) permanently exempts a future quest building from locking.

Dirt-bike and motorcycle tanks are separate authoritative character fields. Movement consumes gas from distance traveled at 50 mpg and 45 mpg respectively; an empty tank blocks motorized movement. God Mode neither checks nor consumes gasoline.

Inventory stacks include their category, per-unit weight, and whether they are physically carried. Distinct carried item types consume one category slot per stack: 3 weapon, 3 quest, and 6 other stacks. Quantity does not consume additional slots, but it multiplies stack weight. The server rejects acquisitions or chest withdrawals above 50 pounds. Carried weight reduces normal movement speed by one percent per pound; God Mode bypasses the speed penalty but not inventory categorization. The permanent fist is virtual and free. Bikes, inflatable rafts, and motorcycles currently add no player-carried weight; dirt bikes and motorcycles are parked assets rather than backpack cargo.

Merchant offers use deterministic server-configured rotation buckets. Reopening a seller within the same bucket returns the same stock and prices for that player; the next bucket produces a newly randomized inventory and quote.

`pathResult` contains renderer-neutral meter waypoints. `pathUnavailable`, `movementBlocked`, `playerFell`, and `playerDied` explain authoritative navigation outcomes. `weatherChanged` carries the current condition, temperature, precipitation, wind, daylight state, sunrise, sunset, moon phase, observation time, and provider; clients independently decide how to present those facts.

`PlayerState.bodyHeat` is the authoritative 0–100 thermal meter. Current hourly weather, outdoor movement, equipped clothing, indoor recovery, hypothermia damage, overheating stamina drain, death reset, and God Mode protection are all evaluated by the server.
