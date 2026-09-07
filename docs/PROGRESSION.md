# Character progression

Overall character level is separate from the existing crafting skill level. Both start at 1. Overall level costs `100 + 75 × (level − 1) + 25 × (level − 1)²` XP: 100, 200, 350, 550, and so on. Each earned level adds one assignable stat point. The Character stats / Level up button in Stats opens allocation. Players start with one point in each of seven stats, may redistribute them, and may leave points unspent. The server rejects negative values and totals above `7 + level − 1`.

## XP rewards

| Activity | Base XP |
| --- | ---: |
| Connected for one minute | 1 |
| One minute within 100 m of an active server event actor | 3 additional |
| First visit to a map area (2 km grid) | 75 |
| Study each recipe copy | 30 |
| Read a crafting skill book | 50 |
| Successful craft batch | 4 + required crafting level / 10 |
| Failed craft batch | 1 |
| Defeat an NPC or animal | 12 + min(50, maximum hearts × 2) |
| Defeat a server event actor | Twice the ordinary kill reward |
| Defeat another player | 40 |
| Complete a quest | 50 + min(100, reward cents / 1000) |
| Clear every floor of a dungeon | 150 + difficulty × 5 + floor count × 30 |

Intelligence multiplies every XP reward. Rewards other than online time vary randomly by ±20%. Online time tracks actual connected wall-clock time, retains partial minutes across reconnects, and grants no offline credit. Event time counts only inside an active event area; overlapping events do not stack. Exploration and quest receipts persist to prevent duplicate rewards. Simulated test characters grant no kill XP.

Every floor must be explored and cleared before a dungeon completes. Players still inside that dungeon receive the completion reward. A visibly larger gold chest appears where the final enemy fell with more money, ammunition, a Fine sword, a recipe, and a crafting book. Completion is awarded once per dungeon session. Leaving and resetting the dungeon creates a new challenge.

## Stat effects

The initial value of 1 preserves the previous baseline. Each additional Strength point adds 10% base weapon damage and 5 lb capacity. Perception adds 4% base accuracy, 5% vision, and 3.5 percentage points of lockpicking success. Endurance adds 2 maximum stamina. Charisma adds .02 to a new NPC's friend rating. Intelligence adds 8% base XP and 2.5 percentage points of crafting success. Luck adds 2 percentage points of crafting success, 2.5 of lockpicking success, and removes 3.5 from witness probability.

Agility divides stamina drain and NPC sight distance by `1 + .06 × (Agility − 1)`, bounded at 20% and 25% of baseline respectively. Witness probability starts at 90%, bounded to 10–95%. Lockpicking starts at 15%, bounded to 3–95%. Final crafting success is bounded to 1–98%. Respeccing Endurance preserves the current stamina fraction, preventing a free refill.

## Good and Evil

Alignment starts at zero and persists from −100 (Evil) to +100 (Good). Quest completion adds .25 Good; an unwitnessed crime adds .25 Evil and a witnessed crime adds .75 Evil. Existing crime reporting determines what counts as witnessed. Agility and Luck influence those witness checks.

At the first visible meeting with a human NPC, alignment contributes `alignment × .003` to the friend rating, at most ±.3. Charisma adds its separate bonus. The memory belongs to the character/NPC name pair, following the game's existing name-memory convention. Changing alignment or stats later does not rewrite that meeting. Existing daily posturing adjustments still apply to ordinary outdoor NPCs. Quest and trade changes do not duplicate the first-meeting bonus.

## Repeat recipe study and craft failure

The first recipe copy rolls a permanent base success chance between 1% and a usefulness-adjusted ceiling, never above 50%. The ceiling is `1% + 49% / (1 + utility)`, where utility is the larger of damage / 10 and maximum price cents / 100,000, plus 3 for vehicles. Higher power/value lowers the ceiling.

The second copy adds exactly 5 percentage points, the third 2.5, the fourth 1.25, then .625, and so on. These are additions to crafting success, not chances to learn the page. Each page is consumed and awards learning XP. Intelligence/Luck bonuses apply after the study chance. The crafting screen displays final chance, base chance, study count, and the next copy's bonus. Existing learned recipes migrate to one studied copy without additional XP.

Each craft batch rolls separately. Success consumes the ingredients and deposits output in Home storage. Failure consumes that batch's ingredients, destroys the placed table, deals exactly 1 heart of damage, and stops the order. Earlier successful output and all unattempted materials remain. Input/output changes, crafting XP, and table destruction commit together in SQLite. Place a new table before crafting again.

Protocol 56 adds `assignStats`, `statsAssigned`, and `progressionUpdated`, with progression in private state and study chances in crafting state. Deploy server and browser assets together.
