This tool rebuilds all areas listed in a cache manifest into a separate staging directory. It downloads current map features through the normal importer, regenerates buildings and world objects, and retains each area's existing elevation samples. It never changes the player database or live cache. Completed staging files are reusable after a failed request or interrupted run.

Build with `dotnet build tools/ReimportWorld/ReimportWorld.csproj`, then run:

```powershell
dotnet tools/ReimportWorld/bin/Debug/net8.0/ReimportWorld.dll snapshot.json manifest.json data/world-cache artifacts/reimport/world-cache
```

The snapshot contains the server's `reality` configuration. The manifest is a JSON array of `{ "file": "world-….json", "area": { … } }` entries from the existing cache. Duplicate areas are grouped by the same rounded coordinates used for generated cache keys. At most two imports run concurrently. `import-report.json` records completed areas and failures; a nonzero exit code means the import is incomplete. Rerun with the same arguments to retry missing areas.

Before installing the staged cache, verify all areas completed and check building geometry and claimed Home IDs. Back up the database, stop the server, copy the staged `world-*.json` files into the live cache, and start the matching server build. Retain the old cache and executable for rollback. Do not use the in-game world reset for this operation.

If replicas returned different revisions of a building during the import, run `ReimportWorld --normalize snapshot.json artifacts/reimport/world-cache`. This makes overlapping blocks use the last imported complete footprint and regenerates dependent doors, driveways, and other generated objects. It works entirely from staged data. Then run `python tools/validate-building-reimport.py artifacts/reimport/world-cache bad-before.json data/reality.db` to check closed polygons, consistency across blocks, and saved Home references.
