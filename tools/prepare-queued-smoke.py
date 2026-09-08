import hashlib,json,tempfile
from pathlib import Path
folder=Path(tempfile.mkdtemp(prefix='AlternativeReality-queued-smoke-'))
(folder/'geo-cache').mkdir()
(folder/'reality-location.json').write_text(json.dumps({'latitude':45.5,'longitude':-122.5}),encoding='utf-8')
key=hashlib.sha256(b'v5:45.500000:-122.500000:2000').hexdigest().upper()[:16]
(folder/'geo-cache'/f'overpass-{key}.json').write_text('{"elements":[]}',encoding='utf-8')
print(folder)
