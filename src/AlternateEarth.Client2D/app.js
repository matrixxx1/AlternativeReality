(() => {
  const canvas = document.querySelector('#world');
  const ctx = canvas.getContext('2d');
  const status = document.querySelector('#status');
  const dot = document.querySelector('#connectionDot');
  const realityName = document.querySelector('#realityName');
  const toast = document.querySelector('#toast');
  const terrainValue = document.querySelector('#terrainValue');
  const elevationValue = document.querySelector('#elevationValue');
  const speedValue = document.querySelector('#speedValue');
  const distanceValue = document.querySelector('#distanceValue');
  const weatherValue = document.querySelector('#weatherValue');
  const actionMenu = document.querySelector('#actionMenu');
  const centerButton = document.querySelector('#centerButton');
  const state = {
    socket: null, playerId: null, snapshot: null,
    base: [], reality: new Map(), doorsByBuilding: new Map(), players: new Map(), facings: new Map(), keys: new Set(),
    camera: { x: 0, y: 0 }, scale: 18, pitch: .7, shear: .12,
    sequence: 0, pathSequence: 0, facing: 'south', moveTarget: null, path: [],
    followCamera: true, weather: null, lastMovementAt: 0, lastBlockedToastAt: 0,
    pointer: { down: false, dragged: false, startX: 0, startY: 0, lastX: 0, lastY: 0 }
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
        state.doorsByBuilding = new Map(state.base
          .filter(entity => entity.kind === 'door' && entity.properties?.buildingId)
          .map(entity => [entity.properties.buildingId, entity]));
        state.reality = new Map(message.snapshot.realityEntities.map(entity => [entity.id, entity]));
        state.players = new Map(message.snapshot.players.map(player => [player.id, player]));
        state.weather = message.snapshot.weather;
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
        if (message.player.id === state.playerId) state.lastMovementAt = Date.now();
        break;
      }
      case 'playerLeft': state.players.delete(message.playerId); break;
      case 'objectCreated': state.reality.set(message.entity.id, message.entity); break;
      case 'objectRemoved': state.reality.delete(message.entityId); break;
      case 'pathResult':
        if (message.sequence === state.pathSequence) state.path = message.waypoints;
        break;
      case 'pathUnavailable':
        if (message.sequence === state.pathSequence) { state.path = []; state.moveTarget = null; showToast(message.message); }
        break;
      case 'movementBlocked':
        state.path = []; state.moveTarget = null;
        if (Date.now() - state.lastBlockedToastAt > 1200) { showToast(message.message); state.lastBlockedToastAt = Date.now(); }
        break;
      case 'playerDied':
        state.path = []; state.moveTarget = null; state.followCamera = true; showToast(message.reason); break;
      case 'weatherChanged': state.weather = message.weather; break;
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

  const toScreen = point => {
    const dx = point.x - state.camera.x;
    const dy = point.y - state.camera.y;
    return {
      x: innerWidth / 2 + ((dx + dy * state.shear) * state.scale),
      y: innerHeight / 2 - (dy * state.scale * state.pitch)
    };
  };
  const toWorld = point => {
    const dy = -(point.y - innerHeight / 2) / (state.scale * state.pitch);
    const dx = (point.x - innerWidth / 2) / state.scale - (dy * state.shear);
    return { x: state.camera.x + dx, y: state.camera.y + dy };
  };

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
    if (me && state.followCamera) {
      state.camera.x += (me.position.x - state.camera.x) * .12;
      state.camera.y += (me.position.y - state.camera.y) * .12;
    }
    drawGroundTiles();
    drawChunkGrid();
    for (const entity of state.base) if (entity.kind === 'terrain') drawTerrain(entity);
    for (const entity of state.base) if (entity.kind === 'sidewalk') drawSidewalk(entity);
    for (const entity of state.base) if (entity.kind === 'road') drawRoad(entity);
    for (const entity of state.base) if (entity.kind === 'water') drawWater(entity);
    drawMoveTarget();
    drawRaisedObjects();
    drawWeatherEffects();
    updateTelemetry(me);
    drawCoordinates();
  }

  function drawConnecting() {
    ctx.fillStyle = 'rgba(231,234,223,.38)'; ctx.font = '14px system-ui'; ctx.textAlign = 'center';
    ctx.fillText('Resolving geographic reality…', innerWidth / 2, innerHeight / 2);
  }

  function drawGroundTiles() {
    const corners = [
      toWorld({ x: 0, y: 0 }), toWorld({ x: innerWidth, y: 0 }),
      toWorld({ x: 0, y: innerHeight }), toWorld({ x: innerWidth, y: innerHeight })
    ];
    const tileMeters = 4;
    const minimumX = Math.floor(Math.min(...corners.map(point => point.x)) / tileMeters) * tileMeters;
    const maximumX = Math.ceil(Math.max(...corners.map(point => point.x)) / tileMeters) * tileMeters;
    const minimumY = Math.floor(Math.min(...corners.map(point => point.y)) / tileMeters) * tileMeters;
    const maximumY = Math.ceil(Math.max(...corners.map(point => point.y)) / tileMeters) * tileMeters;
    for (let y = minimumY; y < maximumY; y += tileMeters) {
      for (let x = minimumX; x < maximumX; x += tileMeters) {
        const points = [
          toScreen({ x, y }), toScreen({ x: x + tileMeters, y }),
          toScreen({ x: x + tileMeters, y: y + tileMeters }), toScreen({ x, y: y + tileMeters })
        ];
        const checker = (Math.floor(x / tileMeters) + Math.floor(y / tileMeters)) & 1;
        ctx.fillStyle = checker ? '#3e6337' : '#42693a';
        ctx.beginPath(); ctx.moveTo(points[0].x, points[0].y);
        for (let index = 1; index < points.length; index++) ctx.lineTo(points[index].x, points[index].y);
        ctx.closePath(); ctx.fill();
        ctx.strokeStyle = 'rgba(179,202,176,.035)'; ctx.lineWidth = 1; ctx.stroke();
      }
    }
  }

  function drawChunkGrid() {
    const size = 256;
    const corners = [
      toWorld({ x: 0, y: 0 }), toWorld({ x: innerWidth, y: 0 }),
      toWorld({ x: 0, y: innerHeight }), toWorld({ x: innerWidth, y: innerHeight })
    ];
    const minimumX = Math.min(...corners.map(point => point.x));
    const maximumX = Math.max(...corners.map(point => point.x));
    const minimumY = Math.min(...corners.map(point => point.y));
    const maximumY = Math.max(...corners.map(point => point.y));
    ctx.strokeStyle = 'rgba(214,225,205,.055)'; ctx.lineWidth = 1;
    for (let x = Math.floor(minimumX / size) * size; x <= maximumX; x += size) {
      const a = toScreen({ x, y: minimumY }), b = toScreen({ x, y: maximumY });
      ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
    }
    for (let y = Math.floor(minimumY / size) * size; y <= maximumY; y += size) {
      const a = toScreen({ x: minimumX, y }), b = toScreen({ x: maximumX, y });
      ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
    }
  }

  function drawRoad(entity) {
    const width = propertyNumber(entity, 'widthMeters', 5) * state.scale;
    drawGeometry(entity, null, '#343a38', Math.max(5, width + 2));
    drawGeometry(entity, null, entity.properties?.surface === 'unpaved' ? '#72543c' : '#606663', Math.max(3, width));
  }

  function drawSidewalk(entity) {
    const width = propertyNumber(entity, 'widthMeters', 8) * state.scale;
    drawGeometry(entity, null, '#827e72', Math.max(5, width + 2));
    drawGeometry(entity, null, '#b7b09e', Math.max(3, width));
  }

  function drawTerrain(entity) {
    const terrain = entity.properties?.terrain || 'grass';
    const colors = {
      grass: ['#537643', '#72935a'], forest: ['#244b32', '#3b6944'], sand: ['#c3a765', '#e0c781'],
      mud: ['#664833', '#886247'], pavement: ['#737875', '#929590']
    };
    const color = colors[terrain] || colors.grass;
    drawGeometry(entity, color[0], color[1], 1.5, true);
  }

  function drawWater(entity) {
    const closed = entity.geometry?.length > 3;
    if (closed) {
      drawGeometry(entity, '#174b66', '#65a9b6', Math.max(4, state.scale * 6), true);
      drawGeometry(entity, null, '#327c91', Math.max(2, state.scale * 1.5), true);
    } else {
      drawGeometry(entity, null, '#65a9b6', Math.max(4, state.scale * 3));
      drawGeometry(entity, null, '#327c91', Math.max(2, state.scale * 1.2));
    }
  }

  function drawBuilding(entity) {
    if (!entity.geometry?.length) return;
    const ground = entity.geometry.map(toScreen);
    const levels = Math.max(2, Number(entity.properties?.['building:levels'] || entity.properties?.levels || 2));
    const height = Math.min(160, levels * 3.2 * state.scale * .55);
    const roof = ground.map(point => ({ x: point.x, y: point.y - height }));

    const lastGround = ground[ground.length - 1];
    const edgeCount = ground[0].x === lastGround.x && ground[0].y === lastGround.y ? ground.length - 1 : ground.length;
    for (let index = 0; index < edgeCount; index++) {
      const next = (index + 1) % ground.length;
      ctx.fillStyle = index % 2 ? '#59483e' : '#625044';
      ctx.beginPath(); ctx.moveTo(roof[index].x, roof[index].y); ctx.lineTo(roof[next].x, roof[next].y);
      ctx.lineTo(ground[next].x, ground[next].y); ctx.lineTo(ground[index].x, ground[index].y); ctx.closePath(); ctx.fill();
      ctx.strokeStyle = '#332c27'; ctx.lineWidth = 1; ctx.stroke();
    }

    ctx.fillStyle = '#8a7462'; ctx.strokeStyle = '#b39a7e'; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.moveTo(roof[0].x, roof[0].y);
    for (let index = 1; index < roof.length; index++) ctx.lineTo(roof[index].x, roof[index].y);
    ctx.closePath(); ctx.fill(); ctx.stroke();

    const door = state.doorsByBuilding.get(entity.id);
    if (door) drawDoorOnBuilding(door);
  }

  function drawDoorOnBuilding(entity) {
    const facing = propertyNumber(entity, 'facingDegrees', 0) * Math.PI / 180;
    const tangentX = -Math.sin(facing) * .45;
    const tangentY = Math.cos(facing) * .45;
    const left = toScreen({ x: entity.position.x - tangentX, y: entity.position.y - tangentY });
    const right = toScreen({ x: entity.position.x + tangentX, y: entity.position.y + tangentY });
    const height = 2.1 * state.scale * .55;
    ctx.fillStyle = '#35251b'; ctx.strokeStyle = '#c49a52'; ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(left.x, left.y); ctx.lineTo(right.x, right.y);
    ctx.lineTo(right.x, right.y - height); ctx.lineTo(left.x, left.y - height); ctx.closePath(); ctx.fill(); ctx.stroke();
    ctx.fillStyle = '#e0ba63';
    ctx.beginPath(); ctx.arc(right.x - ((right.x - left.x) * .22), right.y - height * .48, 1.4, 0, Math.PI * 2); ctx.fill();
  }

  function drawRaisedObjects() {
    const renderables = [];
    for (const entity of state.base) {
      if (entity.kind === 'building') {
        const depth = Math.max(...entity.geometry.map(point => toScreen(point).y));
        renderables.push({ depth, draw: () => drawBuilding(entity) });
      } else if (entity.kind === 'tree') {
        renderables.push({ depth: toScreen(entity.position).y, draw: () => drawTree(entity) });
      } else if (entity.kind === 'fence') {
        const depth = Math.max(...entity.geometry.map(point => toScreen(point).y));
        renderables.push({ depth, draw: () => drawFence(entity) });
      } else if (entity.kind === 'vehicle') {
        renderables.push({ depth: toScreen(entity.position).y, draw: () => drawVehicle(entity) });
      }
    }
    for (const player of state.players.values()) {
      renderables.push({ depth: toScreen(player.position).y, draw: () => drawPlayer(player, player.id === state.playerId) });
    }
    renderables.sort((left, right) => left.depth - right.depth);
    for (const renderable of renderables) renderable.draw();
  }

  function drawFence(entity) {
    if (!entity.geometry?.length) return;
    drawGeometry(entity, null, '#4b3424', Math.max(2, state.scale * .16));
    for (const point of entity.geometry) {
      const p = toScreen(point);
      ctx.fillStyle = '#86603a';
      ctx.fillRect(p.x - 2, p.y - state.scale * .55, 4, state.scale * .65);
    }
  }

  function drawVehicle(entity) {
    const p = toScreen(entity.position);
    const length = propertyNumber(entity, 'lengthMeters', 4.5) * state.scale;
    const width = propertyNumber(entity, 'widthMeters', 1.9) * state.scale * state.pitch;
    const angle = propertyNumber(entity, 'rotationDegrees', 0) * Math.PI / 180;
    const projectedAngle = Math.atan2(-Math.sin(angle) * state.pitch, Math.cos(angle) + (Math.sin(angle) * state.shear));
    ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(projectedAngle);
    ctx.fillStyle = 'rgba(3,8,7,.4)'; ctx.fillRect(-length / 2 + 3, -width / 2 + 5, length, width);
    ctx.fillStyle = '#8d4e3e'; ctx.fillRect(-length / 2, -width / 2, length, width);
    ctx.fillStyle = '#b7d0cf'; ctx.fillRect(-length * .15, -width * .42, length * .38, width * .84);
    ctx.strokeStyle = '#302b29'; ctx.lineWidth = 2; ctx.strokeRect(-length / 2, -width / 2, length, width);
    ctx.restore();
  }

  function drawTree(entity) {
    const p = toScreen(entity.position);
    const r = Math.max(3, state.scale * 1.1);
    ctx.fillStyle = 'rgba(3,10,8,.35)'; ctx.beginPath(); ctx.ellipse(p.x + r * .45, p.y + 2, r, r * .38, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#5a3e27'; ctx.fillRect(p.x - 2, p.y - r * .5, 4, r * .8);
    ctx.fillStyle = '#183b2b'; ctx.beginPath(); ctx.arc(p.x + 2, p.y - r * .9, r + 2, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = entity.properties?.species === 'oak' ? '#507b4e' : '#3e7354'; ctx.beginPath(); ctx.arc(p.x, p.y - r, r, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = 'rgba(169,202,137,.32)'; ctx.beginPath(); ctx.arc(p.x - r * .3, p.y - r * 1.3, r * .38, 0, Math.PI * 2); ctx.fill();
  }

  function drawMoveTarget() {
    if (!state.moveTarget) return;
    const p = toScreen(state.moveTarget);
    const pulse = 5 + ((Math.sin(Date.now() / 150) + 1) * 2);
    ctx.strokeStyle = 'rgba(243,214,128,.8)'; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.arc(p.x, p.y, pulse, 0, Math.PI * 2); ctx.stroke();
    ctx.fillStyle = '#f3d680';
    ctx.beginPath();
    ctx.moveTo(p.x, p.y - 4); ctx.lineTo(p.x + 4, p.y); ctx.lineTo(p.x, p.y + 4); ctx.lineTo(p.x - 4, p.y); ctx.closePath(); ctx.fill();
  }

  function drawPlayer(player, self) {
    const p = toScreen(player.position);
    const unit = Math.max(1, state.scale / 14);
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

  function drawWeatherEffects() {
    const weather = state.weather;
    if (!weather?.isAvailable) return;
    const rainy = [51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99].includes(weather.weatherCode);
    const snowy = [71, 73, 75, 77, 85, 86].includes(weather.weatherCode);
    if (!weather.isDay || weather.weatherCode >= 3) {
      ctx.fillStyle = weather.isDay ? 'rgba(20,30,35,.11)' : 'rgba(5,12,24,.30)';
      ctx.fillRect(0, 0, innerWidth, innerHeight);
    }
    if (!rainy && !snowy) return;
    const count = Math.min(180, 45 + Math.round(weather.precipitationMillimeters * 55));
    const time = Date.now() / 12;
    ctx.strokeStyle = snowy ? 'rgba(241,246,241,.8)' : 'rgba(151,205,230,.52)';
    ctx.fillStyle = 'rgba(241,246,241,.8)';
    ctx.lineWidth = 1;
    for (let index = 0; index < count; index++) {
      const x = ((index * 83.17) + time * (snowy ? .15 : .8)) % (innerWidth + 40) - 20;
      const y = ((index * 47.73) + time * (snowy ? .35 : 1.7)) % (innerHeight + 50) - 25;
      if (snowy) { ctx.beginPath(); ctx.arc(x, y, 1.5, 0, Math.PI * 2); ctx.fill(); }
      else { ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x - 4, y + 13); ctx.stroke(); }
    }
  }

  function updateTelemetry(player) {
    if (player) {
      terrainValue.textContent = formatTerrain(player.terrain);
      elevationValue.textContent = `${player.position.z.toFixed(1)} m`;
      const speed = Date.now() - state.lastMovementAt < 220 ? player.speedMetersPerSecond : 0;
      speedValue.textContent = `${(speed * 2.23694).toFixed(1)} mph`;
      distanceValue.textContent = state.moveTarget
        ? `${Math.hypot(state.moveTarget.x - player.position.x, state.moveTarget.y - player.position.y).toFixed(1)} m away`
        : '—';
    }
    const weather = state.weather;
    weatherValue.textContent = weather?.isAvailable
      ? `${weather.condition} · ${Math.round(weather.temperatureCelsius)}°C`
      : 'Unavailable';
  }

  function formatTerrain(value) {
    return String(value || 'grass').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, character => character.toUpperCase());
  }

  function propertyNumber(entity, key, fallback) {
    const value = Number(entity.properties?.[key]);
    return Number.isFinite(value) ? value : fallback;
  }

  function drawCoordinates() {
    const me = state.players.get(state.playerId); if (!me) return;
    ctx.fillStyle = 'rgba(229,234,222,.55)'; ctx.font = '11px ui-monospace, monospace'; ctx.textAlign = 'right';
    ctx.fillText(`${me.position.region.latitudeBand},${me.position.region.longitudeBand}  X ${me.position.x.toFixed(1)}m  Y ${me.position.y.toFixed(1)}m  Z ${me.position.z.toFixed(1)}m  ·  ${state.scale.toFixed(1)}px/m`, innerWidth - 18, innerHeight - 18);
  }

  addEventListener('keydown', event => {
    if (!event.repeat) state.keys.add(event.key.toLowerCase());
    if (['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(event.key.toLowerCase())) {
      state.followCamera = true;
      actionMenu.hidden = true;
    }
  });
  addEventListener('keyup', event => state.keys.delete(event.key.toLowerCase()));
  addEventListener('blur', () => state.keys.clear());
  canvas.addEventListener('wheel', event => { event.preventDefault(); state.scale = Math.max(.5, Math.min(32, state.scale * (event.deltaY > 0 ? .88 : 1.14))); }, { passive: false });
  canvas.addEventListener('click', event => {
    if (event.button !== 0) return;
    actionMenu.hidden = true;
    state.followCamera = true;
    state.moveTarget = toWorld({ x: event.clientX, y: event.clientY });
    state.path = [];
    send({ type: 'pathRequest', x: state.moveTarget.x, y: state.moveTarget.y, sequence: ++state.pathSequence });
  });
  canvas.addEventListener('pointerdown', event => {
    if (event.button !== 2) return;
    state.pointer = { down: true, dragged: false, startX: event.clientX, startY: event.clientY, lastX: event.clientX, lastY: event.clientY };
    canvas.setPointerCapture(event.pointerId);
  });
  canvas.addEventListener('pointermove', event => {
    if (!state.pointer.down) return;
    const totalDistance = Math.hypot(event.clientX - state.pointer.startX, event.clientY - state.pointer.startY);
    if (totalDistance > 4) state.pointer.dragged = true;
    if (state.pointer.dragged) {
      const screenX = event.clientX - state.pointer.lastX;
      const screenY = event.clientY - state.pointer.lastY;
      const worldY = -screenY / (state.scale * state.pitch);
      const worldX = (screenX / state.scale) - (worldY * state.shear);
      state.camera.x -= worldX;
      state.camera.y -= worldY;
      state.followCamera = false;
      actionMenu.hidden = true;
    }
    state.pointer.lastX = event.clientX;
    state.pointer.lastY = event.clientY;
  });
  canvas.addEventListener('pointerup', event => {
    if (event.button !== 2 || !state.pointer.down) return;
    if (!state.pointer.dragged) {
      actionMenu.style.left = `${Math.min(event.clientX, innerWidth - 210)}px`;
      actionMenu.style.top = `${Math.min(event.clientY, innerHeight - 90)}px`;
      actionMenu.hidden = false;
    }
    state.pointer.down = false;
  });
  canvas.addEventListener('pointercancel', () => { state.pointer.down = false; });
  canvas.addEventListener('contextmenu', event => event.preventDefault());
  centerButton.addEventListener('click', () => {
    state.followCamera = true;
    const me = state.players.get(state.playerId);
    if (me) state.camera = { x: me.position.x, y: me.position.y };
    actionMenu.hidden = true;
  });

  setInterval(() => {
    let x = (state.keys.has('d') || state.keys.has('arrowright') ? 1 : 0) - (state.keys.has('a') || state.keys.has('arrowleft') ? 1 : 0);
    let y = (state.keys.has('w') || state.keys.has('arrowup') ? 1 : 0) - (state.keys.has('s') || state.keys.has('arrowdown') ? 1 : 0);
    if (x || y) {
      state.path = [];
      state.moveTarget = null;
    } else if (state.path.length) {
      const me = state.players.get(state.playerId);
      if (me) {
        const waypoint = state.path[0];
        const dx = waypoint.x - me.position.x;
        const dy = waypoint.y - me.position.y;
        const distance = Math.hypot(dx, dy);
        if (distance <= .35) {
          state.path.shift();
          if (!state.path.length) state.moveTarget = null;
        } else {
          x = dx / distance;
          y = dy / distance;
        }
      }
    }
    if (x || y) {
      if (Math.abs(x) > Math.abs(y)) state.facing = x > 0 ? 'east' : 'west';
      else state.facing = y > 0 ? 'north' : 'south';
      state.facings.set(state.playerId, state.facing);
      send({ type: 'moveRequest', x, y, sequence: ++state.sequence });
    }
  }, 50);

  addEventListener('resize', resize); resize(); connect(); render();
})();
