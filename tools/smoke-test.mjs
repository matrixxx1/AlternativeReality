const baseUrl = process.argv[2] || 'http://localhost:5080';
const socketUrl = baseUrl.replace(/^http/, 'ws') + '/ws';

function connect(name) {
  const id = crypto.randomUUID();
  const socket = new WebSocket(`${socketUrl}?characterId=${id}&name=${encodeURIComponent(name)}`);
  const messages = [];
  const waiters = [];
  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data);
    messages.push(message);
    for (const waiter of [...waiters]) {
      if (waiter.predicate(message)) {
        waiter.resolve(message);
        waiters.splice(waiters.indexOf(waiter), 1);
      }
    }
  });
  const waitFor = (predicate, timeoutMs = 8000) => new Promise((resolve, reject) => {
    const existing = messages.find(predicate);
    if (existing) return resolve(existing);
    const waiter = { predicate, resolve };
    waiters.push(waiter);
    setTimeout(() => {
      const index = waiters.indexOf(waiter);
      if (index >= 0) waiters.splice(index, 1);
      reject(new Error(`Timed out waiting for ${name}`));
    }, timeoutMs);
  });
  return { socket, waitFor };
}

const first = connect('Smoke-2D-A');
const second = connect('Smoke-2D-B');
try {
  const [welcomeA, welcomeB] = await Promise.all([
    first.waitFor(message => message.type === 'welcome'),
    second.waitFor(message => message.type === 'welcome')
  ]);
  const playerA = welcomeA.snapshot.players.find(player => player.id === welcomeA.playerId);
  const placement = {
    type: 'placeObject', objectType: 'smoke-marker',
    x: playerA.position.x + 1, y: playerA.position.y, rotationDegrees: 90
  };
  first.socket.send(JSON.stringify(placement));
  const [seenByA, seenByB] = await Promise.all([
    first.waitFor(message => message.type === 'objectCreated'),
    second.waitFor(message => message.type === 'objectCreated')
  ]);
  if (seenByA.entity.id !== seenByB.entity.id) throw new Error('Clients received different canonical entity IDs.');
  const world = await fetch(`${baseUrl}/api/world`).then(response => response.json());
  if (!world.realityEntities.some(entity => entity.id === seenByA.entity.id)) throw new Error('Placed object was not present in authoritative world snapshot.');
  console.log(JSON.stringify({
    ok: true,
    protocol: welcomeA.protocolVersion,
    playersSynchronized: welcomeB.snapshot.players.length >= 2,
    entitySynchronized: seenByA.entity.id,
    persistedInLiveSnapshot: true
  }, null, 2));
} finally {
  first.socket.close();
  second.socket.close();
}
