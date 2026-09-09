# Northern exposure and Haney

Northern exposure is an outdoor Reality inversion in the hourly Server Vote pool. It lasts up to 20 minutes. Sixty Canadians arrive: 42 use fists, 12 carry hockey sticks, and 6 carry ice skates. Every human invader farts every 3–5 seconds, including while no player is in melee range. Each melee attack and fart produces Canadian dialogue.

Green gas appears after a half-second warning and persists for 3.5 seconds. It damages outdoor players and NPCs once per second, with overlapping clouds capped at 2 hearts per tick. Canadians and their allied event wildlife are immune. Ordinary clouds have a 2.5-meter radius; the two bosses have 6-meter clouds. Both fart animations are synchronized through actor state: a full bend forward, or a backward bend with one leg raised.

The invasion also brings four angry moose (28 hearts), eight helmet-wearing beavers (14 hearts), and a tactical goose battalion of twelve birds (8 hearts each), arranged in three four-bird squads. They immediately pursue grounded players throughout the event area and route around obstacles. Moose charge at 6 m/s and hit for 2.5 hearts every 0.9 seconds; beavers chase at 4.5 m/s and bite for 1 heart every 0.65 seconds; geese flank at 5.5 m/s and peck for 0.5 hearts every 0.5 seconds. Attack timers are resolved on the server's half-second simulation ticks. Each attack draws from the shared Canadian dialogue pool, including all ten requested additional lines. Wildlife has distinct antlers, beaver helmets/teeth/tails, and goose helmets/tactical vests. They do not fart or count toward the 50-human-Canadian boss trigger.

Angry moose also pee maple syrup on their target, at most once every six seconds while attacking. The syrup attack itself deals no damage. Its amber puddle has a 2.5-meter radius and lasts five seconds; grounded players standing in it move at half speed, with overlapping puddles never multiplying the penalty. Collecting it through nearby treasure gives one ordinary maple syrup item for inventory, food, or crafting use and removes the puddle immediately. Leaving it or letting it expire ends the slowdown.

The server counts distinct Canadian defeats across all players. At 50, Mecha Terry and Mecha Phil appear in oversized, sweltering robot costumes, with 250 hearts each. They only attack by farting. Defeating one does not finish the event or respawn that boss; both must die. Players in the event area at victory receive individual treasure: 5 maple syrups, 100 Canadian money, and 2 hockey sticks. Each of the two recipes independently has a 30% chance to appear. Expiry gives no victory reward.

| Item | Behavior |
| --- | --- |
| Hockey stick | Melee, 1.5 base hearts of physical damage only, 2.2-meter reach. Recipe: exactly 3 wood. |
| Ice skate | Melee, 3 base hearts of physical damage, 1.8-meter reach. A surviving target is pushed up to 3 meters along a collision-checked path and bleeds for 0.25 hearts once per second for three ticks. |
| Maple syrup | Food; restores stamina and 2 hearts and grants +25% movement speed and +5 effective perception for five minutes. Reconsumption refreshes the timer; bonuses do not stack. Recipe: 2 sugar and 1 water. |
| Canadian money | Maple-leaf inventory icon. Every NPC vendor pays exactly $0.01 each, regardless of friendship. |

Damage values are before the existing weapon quality, character stats, and defensive modifiers. Recipe discovery and crafting use the existing crafting-table and recipe-study systems. Maple syrup's temporary boost is held for the current server session and expires on death or server restart.

Eustace Charleston Haney appears occasionally in a rusty old pickup. The initial opportunity is randomized to 1–3 minutes after an outdoor player is present; later visits are 3–7 minutes after departure. He requires a reachable, unobstructed driveway or parking area within 80 meters of a player. His truck approaches along the road network at 4 meters per second, yields to traffic and players, and parks before the merchant appears and advertises every 12 seconds. He and the truck disappear when no outdoor player is within 250 meters. If a safe route is unavailable, he retries later.

Haney offers 14 randomly selected saleable inventory items, each at its configured maximum price and with Crude quality. Friendship never discounts his prices. Stock uses the existing merchant refresh schedule; items without quality-dependent effects retain their ordinary behavior.

Automated coverage includes shared and duplicate kill accounting, both-boss completion, reward contents, gas immunity, syrup expiry, vendor prices, crafting ingredients, bleed duration, and the complete driveway/advertising/despawn visit.
