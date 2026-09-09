# NOT SPECIAL

The level-up panel lists Nut up ability, Opportunistic, Timing, Strength,
Perception, Endurance, Charisma, Intelligence, Agility, and Luck, in that order.
Each attribute starts at 1, including the three new attributes in older saves.
Players receive one additional point per level and can redistribute all ten
starting points plus earned points. Existing SPECIAL allocations are preserved.

- **N — Nut up ability:** each point grants 10% fear resistance, capped at 100%.
  After a damaging attack from an opponent with more maximum hearts, the server
  rolls fear with chance `min(0.8, 1 - playerMaxHearts / attackerMaxHearts)` times
  the unresisted fraction. Checks occur at most once per attacker every five
  seconds. The client cancels its current command and routes eight meters away
  on a failed resistance check. God mode is immune; intentional Timid behavior
  remains available independently of this response.
- **O — Opportunistic:** each point above 1 adds a 3% chance of one extra strike,
  capped at 75%. The normal attack pays its usual ammo or fuel cost; the bonus
  strike rolls accuracy independently and consumes no additional ammo or fuel.
  Direct attacks against world objects receive a second damage application.
- **T — Timing:** ranged attack intervals are multiplied by
  `max(0.2, 1 / (1 + 0.08 * max(0, Timing - 1)))`. This applies to gun bursts and
  direct shots against characters, actors, buses, and world objects. Melee speed,
  thrown hazard timing, and continuous area effects retain their own rules.

These are initial balance values. The server enforces point budgets, bonus
damage, ammo costs, firing cooldowns, and fear rolls; normal movement validation
still applies to the client's retreat route.
