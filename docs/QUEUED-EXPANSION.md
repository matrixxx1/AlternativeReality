# Approved September 8 expansion

Implemented and verified locally. Existing uncommitted work was preserved.

- [x] NOT SPECIAL: Nut up fear resistance, ammo-free Opportunistic extra strikes, Timing shooting speed, saved stats and level-up controls.
- [x] Hourly Server Vote: Random + three registry entries, default random votes, named voters, replacement votes, cleared tied-option runoffs, admin vote trigger, winner queue.
- [x] Event boundaries/tints, confinement, timed failure, bosses, private per-player completion rewards; no rollback of earned loot/XP.
- [x] Dungeon events: two individually completed dungeons, miniboss then final boss; rename side scroller; turn-based and card-based AI modes.
- [x] Mechanized Warrior: 500 hearts, bullets 3, missiles 5 with 8m falloff to 1, stomps 9.
- [x] Zombie infection for NPCs including merchants/quest givers; flowers and the plant boss are immune; affected quests can fail; Zombie vs plants transforms all area NPCs, allies zombies, bite-only player transformations on entry/exit.
- [x] Musical fruit: fart clouds, stacking .25 hearts/second/cloud, bounded short-lived emissions and humorous text.
- [x] Lava, office workers, geese/Honk Wick, animated objects, projectile tennis, tiny bosses, HOA, explosive following barrels.
- [x] Flood overlays, three-second warnings, shallow/deep changes, damaging push wave, drowning on building collision; 500-heart Noah ark/broadsides/ramming.
- [x] Air 10, five-second drain/refill, gas plus existing direct damage, low-air stamina penalty, zero-air death in five seconds; replace instant drowning.
- [x] Swimmies/swim ownership, exhausted movement/air drain, very slow recovery, water-only, auto-walk on shore, swimming animation/floaties.
- [x] Dungeon water difficulty 15+, fire barriers 5+, blast barriers 10+, fire from explosions; allowed travel walk/run/raft/swim/skateboard.
- [x] Smug alert: fog and speech clues, shared 50 kills, 500-heart Bald Alex Wins, 9-heart telegraphed stomp, short sourced quotes/original parody.
- [x] Budget Cuts, Please Hold; omit Gravity.
- [x] Ten quests: goose, mimic, apocalypse deliveries, screaming escort, weapon refund, gnomes, toilet paper, missing UFO, ghost lawnmower, monster bait.
- [x] Disaster clerk/camera/film, insurance jobs, rescue errands, dungeon inspections, lost-property office, equipment-provided races, eyeball investigation, achievements/cosmetics.
- [x] Small nearby-only NPC driving population, pause out of range, doors/parking reservations/road-safe routes, stationary quest-givers, damaging/throwing collisions.
- [x] Damageable cars and buses, one occupant retaliation/flee roll per encounter, road-safe speeding.
- [x] Silent tiny online XP and slightly higher movement XP.
- [x] Homage description: Zelda, Fallout, Who Framed Roger Rabbit, Syndicate, TMNT and games since the 1980s.
- [x] Idle/low-resource map prefetch: known players/bases, outward within five miles, one block at a time, yield to gameplay.
- [x] Verify all affected mechanics and UI, then purge only existing map caches for regeneration; preserve accounts/characters/base ownership.

Photography creates unique persistent inventory prints with a small captured-game preview. Matching quest submissions consume three distinct prints; vendor sales consume the sold print instead. Photos survive storage, dropped loot and Home shop persistence. Cameras and film are available from vendors and chests. Each exposure consumes one frame of film. See `PHOTOGRAPHS.md`.

## Verification

- Server build: no warnings or errors; protocol 61.
- Initial expansion server tests: 291 passed, 0 failed, including personal two-dungeon victories, reward persistence/privacy, voting, Air, photography, commuter road/driveway movement and proximity pauses.
- Initial expansion client tests: 96 passed, 0 failed, including stable vote controls, private treasure opening, battle-card actions and dungeon barriers.
- Disposable WebSocket server: a complete 60-second vote started the selected card event; entering its first dungeon and queueing a second winner passed.
- Browser: checked the Air display, NOT SPECIAL controls, event entry, deck saving, AI turns and clickable cards in the battle arena. The small fixture rendered at 30 FPS with roughly 0.3–1 ms frame work; this is not a full-server load benchmark.
- Cleared 102 generated map files and 8 geographic source-cache files (436,595,244 bytes). Both active cache directories are empty. The player database SHA-256 stayed unchanged. Existing backups were retained.

The game server is currently stopped. The new cache and idle-prefetch behavior takes effect at its next start. Idle preparation uses one block per pass, bounded cache probes, a five-mile cap, a low-CPU gate while players are online, and a memory-pressure gate. NPC commuting uses at most three trips and pauses outside player proximity.

Map deletion manifest: `artifacts/expansion-map-purge.json`. Test results: `tests/AlternateEarth.Tests/TestResults/expansion.trx`, `artifacts/expansion-server-tests.log`, and `artifacts/expansion-client-tests.log`. The replacement vote-driven WebSocket smoke test is `node tools/expansion-smoke.mjs`.

## Recovery audit

The Codex interruption did not discard the saved source changes. See `RECOVERY-AUDIT.md` for the request-to-code review and follow-up verification. The audit corrected spawned flower infection and expired event dungeon cleanup, and completed inventory photographs, quest redemption, sale checks and previews. The main player database remains unchanged; no additional map purge was performed.
