# September 8 recovery review

The working-tree implementation survived the interrupted app session. This review used the later approved requests as the current requirements, including the Retro rename, Server Vote, two personal dungeon completions, and redeemable/sellable camera prints. Existing unrelated edits were preserved; nothing was reset, committed, or pushed.

| Request group | Recovery evidence |
| --- | --- |
| Retro side-scroller | Present in server, client and shared models. Tests cover grounded jumping, stomp-only damage, difficulty, looping, bosses, two personal clears, reward privacy, expiry and voluntary exit. Current name is **Jump Now, Regret Later!**. |
| NOT SPECIAL | Present in progression models, gameplay, persistence and UI. Included in the fresh server/client regression suites; the browser shows the NOT SPECIAL control. |
| Approved expansion | The implementation checklist is in `QUEUED-EXPANSION.md`. Event registry, voting, turn/card battles, Air/swimming, dungeon barriers, adventures, NPC commuting and idle map preparation remain in the checkout. Their existing automated tests passed in this review. |
| Camera follow-up | Persistent distinct photo items, film consumption, quest redemption, vendor sales, preview storage and access checks are implemented. All seven photo server tests passed. A fresh WebSocket test verified preview access and exclusion of image bytes from routine state. In the browser, taking a new picture consumed one film and the resulting backpack item opened the actual captured game view. |
| Map purge | Prior deletion manifest remains at `artifacts/expansion-map-purge.json`. This review did not repeat the purge or change the live player database. |

## Corrections made during recovery

- Fixed a reproduced Server Config popup exception: three fields were queried in the original document after the panel had moved to its popup document. Queries now follow the panel. A regression test exercises both populating and saving the detached panel. Reopening after reloading the fixed build produced no new copy of the exception.
- Kept a fully collected private Retro reward window open until the player presses Close. The newer event-reward loot path had bypassed the original chest-close behavior.
- Replaced visible obsolete per-event timing/name controls with the current Server Vote explanation and trigger. The prior implementation hid individual buttons but still displayed settings that no longer controlled event scheduling.
- Updated Retro documentation to the later approved name, voting and two-dungeon rules. Replaced the obsolete direct-trigger smoke launcher with a delegate to the current vote smoke test.

## Current verification

- Final fresh server regression: **309 passed, 0 failed**; `tests/AlternateEarth.Tests/TestResults/retro-recovery-verified.trx`.
- Final client regression: **102 passed, 0 failed**; `artifacts/recovery-client-tests.log`.
- Server build: **0 warnings, 0 errors** after stopping the disposable test process that temporarily held the output assembly open.
- Live Server Vote: four options, one-minute expiry, Mechanized Warrior selected, subsequent vote accepted while the event remained active. Disposable data: `artifacts/expansion-smoke-1788902690515`.
- Live camera and browser capture/view: disposable data `artifacts/photographs-smoke-1788902605539`.

The production game server was stopped at the start of this review and remains stopped. All servers started for this review used disposable artifact directories and were stopped afterward. The changes are local and uncommitted.

This establishes a clean build, passing regression suite and the recorded live flows. It is not a full-population load test or a manual playthrough of every event and quest. The cause of the reported application closure has not been established by this game-code review; the separate crash-troubleshooting task is investigating Codex.
