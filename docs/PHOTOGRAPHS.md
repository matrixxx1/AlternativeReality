# Camera photographs

Equip the Camera in the weapon slot, then use **Photograph** under Questionable errands. Each exposure uses one frame of Film and creates a unique 0.01 lb print under Other in the backpack. **View photo** opens the captured game picture, subject and timestamp.

The disaster clerk accepts photographs of three different event creatures. The dungeon inspector requires a barrier, water crossing and exit. Bring the prints to the quest marker and submit them; the three matching prints are consumed. Extra prints stay in the backpack. Pictures may be taken before accepting the quest. Repeated pictures of the same creature do not replace three different subjects.

Vendors offer money for carried prints, including ordinary scenery. Selling removes the print, so it cannot also be submitted. Stored, dropped or previously submitted prints do not count as carried evidence. Photo metadata survives character saves, Home storage, persistent loot and Home shops.

The server checks the actual subject, location, range, film and cooldown. Client images never determine quest eligibility. Previews are 128 by 96 PNG images with a 44,000-character input limit, persisted separately in SQLite and loaded through an authenticated, cacheable image endpoint. Routine inventory updates contain only metadata and a URL. Film consumption, the resulting print and its image are saved together; quest hand-in and print consumption are also saved together.

Verification: `RealityWorldPhotographTests.cs`, `tools/photographs.test.cjs`, camera cases in `tools/inversions.test.cjs`, and `node tools/photographs-smoke.mjs`. The smoke test creates an isolated fixture under `artifacts`, never the main world. Browser verification confirmed taking a game-view picture and opening its preview.
