# Incursions, damage, and equipment

Protocol 60 adds **North park Hydra invasion**, **Alanee incursion**, typed damage, armor resistances, gear levels, Nut Up, alcohol, first-sight fear, and character inspection.

## Events

Both events use a 100-meter outdoor area and last 15 minutes. God Mode users can start either from Server config. Automatic incursions alternate every 12 hours while players are outdoors, giving each event a 24-hour rotation. Only one incursion runs at a time. Event completion gives each player currently in the area private treasure and 500 XP.

* **North park Hydra invasion:** starts with exactly one Ken. Each defeated Ken creates two nearby, clamped inside the event area. Names cycle through Ken, Kenny, Kenneth, Kenn, Kendall, Kendrick, Kenji, Kenton, Kenzie, Kennedy, Kenny-Joe, and Kenrick, all surnamed Hydra. Fifty player kills complete the shared objective. Kens mumble; ordinary NPCs inside the area temporarily become insulting winter-clad cartoon characters. Surviving ordinary NPCs revert when the event ends.
* **Alanee incursion:** twelve person-sized eggs hatch within four meters of a player with a clear path to the egg. Their occupants are Tim Allen, Woody Allen, and Alan Rickman. Defeating all twelve reveals **Pierce Hawkeye** (Alan Alda), with 100 hearts, a rocket launcher, and a martini. Direct attacks, damage over time, and hazards cannot damage him. Every five seconds he releases a chicken; a player killing one deals exactly 25 hearts to him, bypassing armor. Four chickens defeat him. He pours his martini every seven seconds, leaving a two-meter acid patch lasting exactly five seconds. All timed mechanics are server-owned.

The existing generic portal survival quests exclude these event actors, so killing one Ken cannot satisfy the 50-target event. Event NPC dialogue is isolated from ordinary random speech.

## Damage and resistances

Every attack has a damage profile. Ordinary melee, tails, stomps, bullets, arrows, and other projectiles are physical. Knives and swords add bleeding: 10% of raw damage per second for three/five seconds. Explosives combine physical impact with 15% fire damage per second for four seconds. Fire attacks use fire damage. Acid lingers at 15% for four seconds, gas at 12% for three seconds, and zombie bites combine physical damage with poison at 15% for five seconds.

Fire and gas areas damage occupants each second. Leaving an acid/gas area does not remove its remaining timed effect. Repeated exposure from the same source refreshes duration and keeps the stronger application without stacking independent copies. Different sources can apply simultaneous effects. Armor is checked at each tick, and defeated characters lose their active effects.

NPC/enemy levels follow dungeon difficulty; outdoor levels are deterministically randomized from 1–10. Their hats, shirts, pants, and weapons have gear metadata. Clothing rolls every resistance independently within its quality tier; quality and difficulty increase protection. Resistances combine multiplicatively, with a 90% combined cap.

## Gear, Nut Up, and alcohol

Normal newly found gear rolls from level 1 through the discovering player's level. Event rewards and stronghold treasure (difficulty above 50) have a 10% chance to use an exceptional roll capped at player level +20. Previously owned dropped/traded gear retains its level. The existing inventory stacks by item type: each owned stack has one persistent gear roll, also retained through storage, death drops, and Home shop transfers.

Maximum equip level is **player level + effective Nut Up** for every equipment slot. Level 10 with Nut Up 10 permits level 20 and rejects level 21. Nut Up is a permanent allocatable stat; existing characters gain its default one point without losing their prior allocation budget.

Beer adds 5 Nut Up for 60 seconds, wine 10 for 90 seconds, and spirits 15 for 120 seconds. Drinking again retains the stronger active boost and later expiry. Expiration or stat reassignment rechecks all slots, unequips anything over the new limit, retains the items in inventory, and notifies the player. The same check runs on reconnect; gear metadata and alcohol timers are stored in SQLite through additive, backward-compatible metadata/column changes.

## Inspection and fear

The character action menu includes **View character**, showing level, health, allegiance, friend rating, resistances, effects, attack types, and equipment. Inspection validates visibility and location on the server.

First sight of a foe more than 20 levels above the player triggers server-controlled flight. Base duration is ten seconds. Nut Up and Endurance reduce duration; harmful effects increase it, bounded to 1–15 seconds. Movement respects world/dungeon obstacles. Move and attack commands cannot cancel fear; an already seen threat does not repeatedly trigger it.

## Quote sources

The supplied [IMDb list](https://www.imdb.com/list/ls092182265/) was unavailable during implementation. Only characters with independently sourced quotations were included:

* Tim Allen: [Parade interview](https://parade.com/890563/amyspencer/tim-allen-toy-story-comedy-sobriety/).
* Woody Allen: [The Paris Review, The Art of Humor No. 1](https://www.theparisreview.org/interviews/1550/the-art-of-humor-no-1-woody-allen).
* Alan Rickman: [quotation attributed to his December 4, 2008 IFC interview](https://libquotes.com/alan-rickman/quote/lbs4g9l).
* Pierce Hawkeye: brief excerpts from the *M*A*S*H* dialogue in “The General's Practitioner,” [documented here](https://www.natfinn.com/war-is-war-and-hell-is-hell/). These are character dialogue, distinct from the celebrities' interview quotations.

## Verification

`dotnet test AlternateEarth.sln` and `node --test tools/*.test.cjs` include event lifecycle, exact kill counts, immunity, chicken cadence, effect cadence/expiration, quality/level bounds, alcohol/reconnect/drop persistence, first-sight fear, and rendering tests. `node tools/incursions-smoke.mjs <test-server-url> [test-username]` creates a disposable test account and checks the real WebSocket event and inspection contract; run only against an isolated test server. `tools/prepare-queued-smoke.py` prepares a temporary empty-map fixture for that server.
