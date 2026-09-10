"""Check staged map geometry and persistent Home references before deployment."""
import json
import pathlib
import sqlite3
import sys

stage = pathlib.Path(sys.argv[1])
bad_before = {entry['id']: entry for entry in json.loads(pathlib.Path(sys.argv[2]).read_text())}
buildings = {}
inconsistent = set()
invalid = []
files = list(stage.glob('world-*.json'))
for path in files:
    world = json.loads(path.read_text(encoding='utf-8'))
    for entity in world['features']:
        if entity['kind'] != 'building':
            continue
        points = entity['geometry']
        ox, oy = points[0]['x'], points[0]['y']
        area = abs(sum((a['x']-ox)*(b['y']-oy)-(b['x']-ox)*(a['y']-oy)
                       for a, b in zip(points, points[1:]))) / 2
        if len(points) < 4 or points[0] != points[-1] or area < 1:
            invalid.append(entity['id'])
        identity = (entity['position']['region']['latitudeBand'], entity['position']['region']['longitudeBand'], entity['id'])
        previous = buildings.get(identity)
        if previous and previous['geometry'] != points:
            inconsistent.add(entity['id'])
        buildings[identity] = entity

ids = {b['id'] for b in buildings.values()}
with sqlite3.connect(f'file:{pathlib.Path(sys.argv[3]).resolve().as_posix()}?mode=ro', uri=True) as db:
    claims = [row[0] for row in db.execute('SELECT BuildingId FROM AccountBases')]
    overrides = [row[0] for row in db.execute("SELECT EntityId FROM RealityDeltas WHERE Kind='Building'")]
report = {
    'areas': len(files), 'uniqueBuildings': len(ids),
    'previousMalformedBuildingsNowValid': len(ids & bad_before.keys()),
    'invalidBuildings': sorted(set(invalid)), 'inconsistentBuildingsAcrossBlocks': sorted(inconsistent),
    'homeClaims': len(claims), 'missingClaimedHomes': sorted(set(claims)-ids),
    'savedBuildingOverrides': len(overrides), 'missingOverriddenBuildings': sorted(set(overrides)-ids),
}
(stage.parent / 'validation-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
sys.exit(bool(invalid or inconsistent or report['missingClaimedHomes'] or report['missingOverriddenBuildings']))
