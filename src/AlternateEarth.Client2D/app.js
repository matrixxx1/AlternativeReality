(() => {
  const canvas = document.querySelector('#world');
  const ctx = canvas.getContext('2d');
  const status = document.querySelector('#status');
  const dot = document.querySelector('#connectionDot');
  const realityName = document.querySelector('#realityName');
  const toast = document.querySelector('#toast');
  const state = {
    socket: null, playerId: null, snapshot: null,
    base: [], reality: new Map(), players: new Map(), facings: new Map(), keys: new Set(),
    camera: { x: 0, y: 0 }, scale: 10, sequence: 0, facing: 'south'
  };

  function createClientId() {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
    const bytes = new Uint8Array(16);
    if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
      crypto.getRandomValues(bytes);
    } else {
      for (let index = 0; index < bytes.length; index++) bytes[index] = Math.floor(Math.random() * 256);
    }
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = [...bytes].map(value => value.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
  }

  const storedId = sessionStorage.getItem('alternate-earth-character') || createClientId();
  sessionStorage.setItem('alternate-earth-character', storedId);
  const defaultName = `Explorer-${storedId.slice(0, 4)}`;
  const name = new URLSearchParams(location.search).get('name') || defaultName;

  function connect() {
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    state.socket = new WebSocket(`${protocol}//${location.host}/ws?characterId=${encodeURIComponent(storedId)}&name=${encodeURIComponent(name)}`);
    state.socket.addEventListener('open', () => setStatus('Connected — synchronizing world', true));
    state.socket.addEventListener('close', () => { setStatus('Disconnected — retrying', false); setTimeout(connect, 1500); });
    state.socket.addEventListener('message', event => handleMessage(JSON.parse(event.data)));
  }

  function handleMessage(message) {
    switch (message.type) {
      case 'welcome': {
        state.playerId = message.playerId;
        state.snapshot = message.snapshot;
        state.base = message.snapshot.baseEntities;
        state.reality = new Map(message.snapshot.realityEntities.map(entity => [entity.id, entity]));
        state.players = new Map(message.snapshot.players.map(player => [player.id, player]));
        const me = state.players.get(state.playerId);
        if (me) state.camera = { x: me.position.x, y: me.position.y };
        realityName.textContent = message.snapshot.reality.name;
        setStatus(`${message.snapshot.players.length} linked · ${message.snapshot.baseEntities.length} base objects · protocol v${message.protocolVersion}`, true);
        break;
      }
      case 'playerJoined': state.players.set(message.player.id, message.player); break;
      case 'playerMoved': {
        const previous = state.players.get(message.player.id);
        if (previous) {
          const dx = message.player.position.x - previous.position.x;
          const dy = message.player.position.y - previous.position.y;
          if (Math.abs(dx) > Math.abs(dy)) state.facings.set(message.player.id, dx > 0 ? 'east' : 'west');
          else if (Math.abs(dy) > 0) state.facings.set(message.player.id, dy > 0 ? 'north' : 'south');
        }
        state.players.set(message.player.id, message.player);
        break;
      }
      case 'playerLeft': state.players.delete(message.playerId); break;
      case 'objectCreated': state.reality.set(message.entity.id, message.entity); break;
      case 'objectRemoved': state.reality.delete(message.entityId); break;
      case 'error': showToast(message.message); break;
    }
  }

  function setStatus(text, online) {
    status.textContent = text;
    dot.classList.toggle('online', online);
  }

  function showToast(text) {
    toast.textContent = text;
    toast.classList.add('show');
    clearTimeout(showToast.timer);
    showToast.timer = setTimeout(() => toast.classList.remove('show'), 2600);
  }

  function send(message) {
    if (state.socket?.readyState === WebSocket.OPEN) state.socket.send(JSON.stringify(message));
  }

  function resize() {
    const ratio = window.devicePixelRatio || 1;
    canvas.width = Math.round(innerWidth * ratio);
    canvas.height = Math.round(innerHeight * ratio);
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  }

  const toScreen = point => ({
    x: innerWidth / 2 + (point.x - state.camera.x) * state.scale,
    y: innerHeight / 2 - (point.y - state.camera.y) * state.scale
  });
  const toWorld = point => ({
    x: state.camera.x + (point.x - innerWidth / 2) / state.scale,
    y: state.camera.y - (point.y - innerHeight / 2) / state.scale
  });

  function drawGeometry(entity, fill, stroke, width = 1, close = false) {
    if (!entity.geometry?.length) return;
    ctx.beginPath();
    entity.geometry.forEach((point, index) => {
      const screen = toScreen(point);
      if (index === 0) ctx.moveTo(screen.x, screen.y); else ctx.lineTo(screen.x, screen.y);
    });
    if (close) ctx.closePath();
    if (fill) { ctx.fillStyle = fill; ctx.fill(); }
    if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = width; ctx.lineJoin = 'round'; ctx.lineCap = 'round'; ctx.stroke(); }
  }

  function render() {
    requestAnimationFrame(render);
    ctx.clearRect(0, 0, innerWidth, innerHeight);
    const gradient = ctx.createLinearGradient(0, 0, 0, innerHeight);
    gradient.addColorStop(0, '#233a31'); gradient.addColorStop(1, '#182a25');
    ctx.fillStyle = gradient; ctx.fillRect(0, 0, innerWidth, innerHeight);
    if (!state.snapshot) return drawConnecting();

    const me = state.players.get(state.playerId);
    if (me) {
      state.camera.x += (me.position.x - state.camera.x) * .12;
      state.camera.y += (me.position.y - state.camera.y) * .12;
    }
    drawChunkGrid();
    for (const entity of state.base) if (entity.kind === 'water') drawGeometry(entity, 'rgba(46,112,132,.82)', '#6da6ae', 1, true);
    for (const entity of state.base) if (entity.kind === 'road') drawRoad(entity);
    for (const entity of state.base) if (entity.kind === 'building') drawGeometry(entity, '#6d5b4f', '#9f8871', 1, true);
    for (const entity of state.base) if (entity.kind === 'tree') drawTree(entity);
    for (const entity of state.reality.values()) drawStructure(entity);
    for (const player of state.players.values()) drawPlayer(player, player.id === state.playerId);
    drawCoordinates();
  }

  function drawConnecting() {
    ctx.fillStyle = 'rgba(231,234,223,.38)'; ctx.font = '14px system-ui'; ctx.textAlign = 'center';
    ctx.fillText('Resolving geographic reality…', innerWidth / 2, innerHeight / 2);
  }

  function drawChunkGrid() {
    const size = 256;
    const topLeft = toWorld({ x: 0, y: 0 });
    const bottomRight = toWorld({ x: innerWidth, y: innerHeight });
    ctx.strokeStyle = 'rgba(214,225,205,.055)'; ctx.lineWidth = 1;
    for (let x = Math.floor(topLeft.x / size) * size; x <= bottomRight.x; x += size) {
      const a = toScreen({ x, y: topLeft.y }), b = toScreen({ x, y: bottomRight.y });
      ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
    }
    for (let y = Math.floor(bottomRight.y / size) * size; y <= topLeft.y; y += size) {
      const a = toScreen({ x: topLeft.x, y }), b = toScreen({ x: bottomRight.x, y });
      ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
    }
  }

  function drawRoad(entity) {
    const major = ['primary', 'secondary', 'tertiary'].includes(entity.properties?.highway);
    const outerWidth = state.scale * (major ? 8 : 5);
    const innerWidth = state.scale * (major ? 6 : 3.5);
    drawGeometry(entity, null, '#36413b', Math.max(major ? 8 : 5, outerWidth));
    drawGeometry(entity, null, major ? '#d4c7a6' : '#a8a38e', Math.max(major ? 5 : 3, innerWidth));
  }

  function drawTree(entity) {
    const p = toScreen(entity.position);
    const r = Math.max(2.5, state.scale * 1.15);
    ctx.fillStyle = '#10251d'; ctx.beginPath(); ctx.arc(p.x + 1, p.y + 2, r + 1, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = entity.properties?.species === 'oak' ? '#507b4e' : '#38664d'; ctx.beginPath(); ctx.arc(p.x, p.y, r, 0, Math.PI * 2); ctx.fill();
  }

  function drawStructure(entity) {
    const p = toScreen(entity.position);
    const radius = Math.max(5, state.scale * 1.5);
    ctx.save(); ctx.translate(p.x, p.y); ctx.rotate((Number(entity.properties?.rotationDegrees || 0) * Math.PI) / 180);
    ctx.fillStyle = '#e4b95f'; ctx.strokeStyle = '#3f2f20'; ctx.lineWidth = 2;
    ctx.fillRect(-radius, -radius, radius * 2, radius * 2); ctx.strokeRect(-radius, -radius, radius * 2, radius * 2); ctx.restore();
  }

  function drawPlayer(player, self) {
    const p = toScreen(player.position);
    const unit = Math.max(2, Math.round(state.scale / 5));
    const facing = state.facings.get(player.id) || (self ? state.facing : 'south');
    const tunic = self ? '#d6a84a' : characterColor(player.id);
    const tunicDark = self ? '#8f652b' : '#31576b';

    ctx.fillStyle = 'rgba(3,10,8,.42)';
    ctx.beginPath(); ctx.ellipse(p.x, p.y + unit * 5, unit * 4.2, unit * 1.8, 0, 0, Math.PI * 2); ctx.fill();

    ctx.fillStyle = '#302a25';
    ctx.fillRect(p.x - unit * 3, p.y + unit * 2, unit * 2, unit * 3);
    ctx.fillRect(p.x + unit, p.y + unit * 2, unit * 2, unit * 3);
    ctx.fillStyle = '#171918';
    ctx.fillRect(p.x - unit * 3, p.y + unit * 4, unit * 2, unit);
    ctx.fillRect(p.x + unit, p.y + unit * 4, unit * 2, unit);

    ctx.fillStyle = tunicDark;
    ctx.fillRect(p.x - unit * 5, p.y - unit, unit, unit * 4);
    ctx.fillRect(p.x + unit * 4, p.y - unit, unit, unit * 4);
    ctx.fillStyle = tunic;
    ctx.fillRect(p.x - unit * 4, p.y - unit * 2, unit * 8, unit * 5);
    ctx.fillStyle = '#493921';
    ctx.fillRect(p.x - unit * 4, p.y + unit, unit * 8, unit);
    ctx.fillStyle = '#ead0aa';
    ctx.fillRect(p.x - unit * 3, p.y - unit * 6, unit * 6, unit * 4);
    ctx.fillStyle = self ? '#6a3f24' : '#44352d';
    ctx.fillRect(p.x - unit * 3, p.y - unit * 7, unit * 6, unit * 2);
    ctx.fillRect(p.x - unit * 4, p.y - unit * 6, unit, unit * 3);
    ctx.fillRect(p.x + unit * 3, p.y - unit * 6, unit, unit * 3);

    ctx.fillStyle = '#26251f';
    if (facing === 'south') {
      ctx.fillRect(p.x - unit * 2, p.y - unit * 4, unit, unit);
      ctx.fillRect(p.x + unit, p.y - unit * 4, unit, unit);
    } else if (facing === 'east') {
      ctx.fillRect(p.x + unit, p.y - unit * 4, unit, unit);
    } else if (facing === 'west') {
      ctx.fillRect(p.x - unit * 2, p.y - unit * 4, unit, unit);
    } else {
      ctx.fillStyle = self ? '#6a3f24' : '#44352d';
      ctx.fillRect(p.x - unit * 2, p.y - unit * 5, unit * 4, unit * 2);
    }

    ctx.fillStyle = '#edf0e7'; ctx.font = '11px system-ui'; ctx.textAlign = 'center'; ctx.fillText(player.name, p.x, p.y - unit * 9);
    if (self) { ctx.strokeStyle = 'rgba(228,185,95,.18)'; ctx.beginPath(); ctx.arc(p.x, p.y, 5 * state.scale, 0, Math.PI * 2); ctx.stroke(); }
  }

  function characterColor(id) {
    const palette = ['#5489a3', '#668f57', '#9b6158', '#785f9e', '#b17743'];
    let hash = 0;
    for (const character of id) hash = ((hash * 31) + character.charCodeAt(0)) >>> 0;
    return palette[hash % palette.length];
  }

  function drawCoordinates() {
    const me = state.players.get(state.playerId); if (!me) return;
    ctx.fillStyle = 'rgba(229,234,222,.55)'; ctx.font = '11px ui-monospace, monospace'; ctx.textAlign = 'right';
    ctx.fillText(`${me.position.region.latitudeBand},${me.position.region.longitudeBand}  X ${me.position.x.toFixed(1)}m  Y ${me.position.y.toFixed(1)}m  Z ${me.position.z.toFixed(1)}m  ·  ${state.scale.toFixed(1)}px/m`, innerWidth - 18, innerHeight - 18);
  }

  addEventListener('keydown', event => { if (!event.repeat) state.keys.add(event.key.toLowerCase()); });
  addEventListener('keyup', event => state.keys.delete(event.key.toLowerCase()));
  addEventListener('blur', () => state.keys.clear());
  canvas.addEventListener('wheel', event => { event.preventDefault(); state.scale = Math.max(.5, Math.min(32, state.scale * (event.deltaY > 0 ? .88 : 1.14))); }, { passive: false });
  canvas.addEventListener('click', event => {
    const point = toWorld({ x: event.clientX, y: event.clientY });
    send({ type: 'placeObject', objectType: 'wooden-crate', x: point.x, y: point.y, rotationDegrees: 0 });
  });
  canvas.addEventListener('contextmenu', event => {
    event.preventDefault(); const point = toWorld({ x: event.clientX, y: event.clientY });
    const nearest = [...state.reality.values()].map(entity => ({ entity, distance: Math.hypot(entity.position.x - point.x, entity.position.y - point.y) })).sort((a, b) => a.distance - b.distance)[0];
    if (nearest && nearest.distance < 3) send({ type: 'removeObject', entityId: nearest.entity.id }); else showToast('No reality object at that point.');
  });

  setInterval(() => {
    const x = (state.keys.has('d') || state.keys.has('arrowright') ? 1 : 0) - (state.keys.has('a') || state.keys.has('arrowleft') ? 1 : 0);
    const y = (state.keys.has('w') || state.keys.has('arrowup') ? 1 : 0) - (state.keys.has('s') || state.keys.has('arrowdown') ? 1 : 0);
    if (x || y) {
      if (Math.abs(x) > Math.abs(y)) state.facing = x > 0 ? 'east' : 'west';
      else state.facing = y > 0 ? 'north' : 'south';
      state.facings.set(state.playerId, state.facing);
      send({ type: 'moveRequest', x, y, sequence: ++state.sequence });
    }
  }, 50);

  addEventListener('resize', resize); resize(); connect(); render();
})();
