# Jump Now, Regret Later!

Originally requested as “Side scroll of doooooom!”, the side-scroller was renamed and integrated into Server Vote by the approved September 8 expansion. Those later requirements replace the original daily trigger and single-dungeon victory rule.

- Server Vote runs hourly. Administrators can start a one-minute vote from Server configuration. Random plus three registry entries are offered; the winning event waits if another event is active.
- Enter a Retro event dungeon from the active event area. Each player must complete two separate personal runs: a small boss first, then a big boss. Homes and stores retain their normal behavior.
- Click or tap to jump. The player stays at a fixed horizontal position while scenery and enemies scroll. Only descending head contact damages enemies. Bosses require two stomps in the first dungeon and five in the second. Missed enemies loop back.
- Difficulty controls scroll speed and enemy count. Walking, weapons, and double jumps cannot defeat enemies in this mode.
- Leave dungeon is always available. An unfinished run grants no completion reward. Clearing the first dungeon enables the second without awarding the final treasure.
- Clearing the second dungeon grants private treasure and bonus XP. The treasure opens for its owner; automatic looting cannot bypass the window. After collecting items, press Close to leave. Taking the last item keeps the window open until Close is pressed.
- Event expiry ends unfinished Retro runs and restores normal dungeon behavior. Earned rewards persist and remain private. Failure does not take away previously earned items or XP.

The server owns jump physics, looping, stomp detection, personal completion counts, and rewards. The renderer uses bounded prediction between server updates.

Verification: `dotnet test tests/AlternateEarth.Tests --filter FullyQualifiedName~Retro -m:1` checks two-dungeon victory, bosses, isolation, expiry, movement/weapon rejection, and supported difficulties. `node --test tools/retro-battles.test.cjs tools/treasure-window.test.cjs` checks rendering math and reward-window behavior. `node tools/expansion-smoke.mjs` exercises the current vote-driven WebSocket path against a disposable local server. The old `retro-smoke.mjs` entry point delegates to that vote smoke test; it does not force Retro to win.
