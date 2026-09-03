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
  first.socket.send(JSON.stringify({ type: 'moveRequest', x: 1, y: 0, sequence: 1 }));
  const [seenByA, seenByB] = await Promise.all([
    first.waitFor(message => message.type === 'playerMoved' && message.player.id === welcomeA.playerId),
    second.waitFor(message => message.type === 'playerMoved' && message.player.id === welcomeA.playerId)
  ]);
  if (seenByA.player.position.x !== seenByB.player.position.x) throw new Error('Clients received different authoritative positions.');
  if (seenByA.player.position.x <= playerA.position.x) throw new Error('Authoritative player did not move east.');

  first.socket.send(JSON.stringify({
    type: 'placeObject', objectType: 'must-be-rejected',
    x: seenByA.player.position.x + 1, y: seenByA.player.position.y, rotationDegrees: 0
  }));
  const rejection = await first.waitFor(message => message.type === 'error' && message.message.includes('disabled'));
  const world = await fetch(`${baseUrl}/api/world`).then(response => response.json());
  const authoritativePlayer = world.players.find(player => player.id === welcomeA.playerId);
  if (!authoritativePlayer || authoritativePlayer.position.x !== seenByA.player.position.x) throw new Error('Live world snapshot did not match the movement event.');
  console.log(JSON.stringify({
    ok: true,
    protocol: welcomeA.protocolVersion,
    movementSynchronized: true,
    authoritativePosition: seenByA.player.position,
    objectPlacementRejected: rejection.message
  }, null, 2));
} finally {
  first.socket.close();
  second.socket.close();
}
