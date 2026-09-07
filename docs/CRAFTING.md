# Crafting

Players buy a crafting table from a hardware or furniture merchant and place it inside their own Home. Its **Review recipes & craft** action lists collected recipes, required supplies, available Home quantities, output details, and batch controls. Inputs and outputs use Home item storage; finished equipment must be withdrawn before use.

Recipe pages appear in ordinary dungeon treasure chests. The first chest guarantees a page; additional chests have a 40% chance. Taking a page permanently learns its recipe for that character without using backpack space. Homes, stores, and outdoor chests do not generate recipe pages.

The initial catalog contains 39 recipes: gunpowder and Molotov game items; bullets, arrows, ball bearings, rockets; knives, swords, slingshots, crossbows, pistols, rifles, grenades, shields; flashlights, lanterns, candles, lock picks; bottles, jars, cloth salvage, charcoal, hats, shirts, food; skateboards, rocket launchers, motorcycles, UFOs; and bottle/jar variants of four gas effects and napalm. Weapon chemistry uses fictional reagents, and all costs are arbitrary inventory tokens. These are game mechanics, without real manufacturing steps, chemical formulations, or physical measurements.

## Adding a recipe

1. Register a stable item ID in `CraftingCatalog.Materials` or the existing item configuration catalog. Set its display name, weight, category, and sale eligibility. Add appropriate store-category and scavenging sources for new ingredients.
2. Assign a stable level in the catalog requirement mapping, reflecting broad difficulty and damage potential. Add a `CraftingRecipe` to `CraftingCatalog.Recipes` with a unique stable recipe ID, output ID/count, positive ingredient counts, and station type. Current recipes use `craftingTable`.
3. Recipe pages, dungeon drops, permanent discovery, and the crafting screen derive from this catalog. Keep recipe IDs stable so existing discoveries continue working after upgrades.
4. Add client artwork to `itemGlyphs` where useful. Newly introduced weapons also need client loadout registration and authoritative weapon behavior; adding a recipe alone does not implement a new weapon.
5. Verify inventory transactions, discovery persistence, access control, and any new gameplay behavior. Ingredients must have acquisition sources and output IDs must be registered.

`RealityWorld.Crafting` serializes crafting with Home furniture and item transfers, validates ownership and a placed table, requires a learned recipe and its crafting level, and accepts 1–99 batches. It validates every cost before writing. Every batch rolls for success; a failure destroys the table, deals one heart of damage, and stops the order. One SQLite inventory transaction saves attempted input deductions, successful output additions, table destruction, and crafting XP; memory updates after the transaction succeeds. Learned recipes live in `LearnedCraftingRecipes`, keyed by reality, character, and recipe ID.

## Crafting progression

Characters start at level 1. Crafting grants **1 XP per attempted batch**, regardless of output count. Studying each recipe copy grants **25 crafting XP**. Repeated copies improve success by +5, +2.5, +1.25 percentage points and so on. Each level costs `ceil(100 × 1.005^(level − 1))` XP: an exponential curve with increasing costs. The crafting screen shows current level, XP toward the next level, recipe level requirements, and broad difficulty labels. Higher damage and complexity generally require more skill, with the requested level-1 Molotov exception.

Fixed milestones are Molotov level 1, skateboard 25, rocket launcher 50, motorcycle 100, and UFO 5,000. Motorcycles consume substantial stocks of metal, plastic, rubber, and parts; UFOs require a rare kryptonite item along with other supplies.

Dungeon treasure has an independent **1% chance** of dropping **Building shit for dummies** and **0.2% chance** of kryptonite. Neither is ordinarily sold. Withdraw a book into the backpack and choose **Read (+1 crafting level)**. Reading consumes one book, advances exactly one full level, and preserves previously earned XP toward the next level. God Mode still needs the actual book and respects crafting skill requirements.

`CraftingProgress` persists total XP per reality and character. Crafting and book consumption atomically commit inventory and XP; learning atomically commits the unique recipe and discovery bonus. Skill survives reconnects, deaths, and server restarts.

## Area effects

`HazardCatalog` defines fictional reagents and bottle/jar variants. `RealityWorld.Hazards` handles authoritative throw range, obstacle checks, inventory consumption, periodic damage, sleep, and expiration. The client renders the same server-provided centers, radii, and timestamps.

| Effect | Bottle | Jar | Effect on characters inside |
|---|---:|---:|---|
| Chloramine/chlorine gas | 10 seconds | 20 seconds | 1 heart per second |
| Chloroform gas | 10 seconds | 20 seconds | 10-second sleep, once per target per cloud |
| Peracetic acid gas | 10 seconds | 20 seconds | 0.5 hearts per second |
| Napalm | 40 seconds | 80 seconds | 4 / 8 hearts per second respectively |

Clouds affect their thrower, other players regardless of PvP setting, NPCs, and animals. God Mode retains its health floor. Solid obstacles block exposure; UFO riders and abducted characters are above ground effects. Damage stops when a target leaves the area, and sleep wears off after its timer. Clouds and sleep are transient server state; reconnecting to the same running server retains active sleep, while a server restart clears both.

Protocol 50 adds `requestCrafting`, `craftItem`, and `throwHazard` client actions; `craftingOpened`, `craftingUpdated`, and `areaHazardsChanged` server events; learned recipes in private state; area hazards in snapshots; and sleep expiration timestamps on character states.

See [PROGRESSION.md](PROGRESSION.md) for overall character XP, stats, alignment, study chance formulas, and protocol 56.
