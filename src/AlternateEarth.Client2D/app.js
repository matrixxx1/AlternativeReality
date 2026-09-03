(() => {
  'use strict';
  const $ = selector => document.querySelector(selector);
  const canvas = $('#world');
  const ctx = canvas.getContext('2d');
  const ui = {
    status: $('#status'), dot: $('#connectionDot'), realityName: $('#realityName'), toast: $('#toast'),
    terrain: $('#terrainValue'), elevation: $('#elevationValue'), speed: $('#speedValue'), distance: $('#distanceValue'),
    camera: $('#cameraValue'), weather: $('#weatherValue'), sun: $('#sunValue'), moon: $('#moonValue'), hearts: $('#heartsValue'), stamina: $('#staminaValue'),
    playerGps: $('#playerGpsValue'), destinationGps: $('#destinationGpsValue'), actionMenu: $('#actionMenu'),
    noActions: $('#noActions'), teleport: $('#teleportButton'), chatForm: $('#chatForm'), chatInput: $('#chatInput'),
    chatHistory: $('#chatHistory'), chatMessages: $('#chatMessages'), toggleChat: $('#toggleChatButton'),
    center: $('#centerButton'), god: $('#godMode'), rebuild: $('#rebuildButton')
  };
  const state = {
    socket: null, playerId: null, snapshot: null, weather: null, base: [], lists: {},
    players: new Map(), actors: new Map(), reality: new Map(), doors: new Map(), facings: new Map(), movingUntil: new Map(),
    chat: [], speech: new Map(), chatVisible: false,
    camera: { x: 0, y: 0 }, scale: 18, pitch: .69, shear: .14, follow: true,
    keys: new Set(), path: [], target: null, pathSequence: 0, lastInput: 0, lastBlocked: 0,
    pointer: { down: false, dragged: false, x: 0, y: 0 }, actionPoint: null, frame: 0
  };

  function clientId() {
    if (crypto?.randomUUID) return crypto.randomUUID();
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
      const r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 3) | 8).toString(16);
    });
  }
  const storedId = sessionStorage.getItem('alternative-reality-character') || clientId();
  sessionStorage.setItem('alternative-reality-character', storedId);
  const playerName = new URLSearchParams(location.search).get('name') || `Explorer-${storedId.slice(0, 4)}`;

  function connect() {
    const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:';
    state.socket = new WebSocket(`${scheme}//${location.host}/ws?characterId=${encodeURIComponent(storedId)}&name=${encodeURIComponent(playerName)}`);
    state.socket.addEventListener('open', () => setStatus('Connected — synchronizing world', true));
    state.socket.addEventListener('close', () => { setStatus('Disconnected — retrying', false); setTimeout(connect, 1500); });
    state.socket.addEventListener('message', event => handle(JSON.parse(event.data)));
  }
  function send(message) { if (state.socket?.readyState === WebSocket.OPEN) state.socket.send(JSON.stringify(message)); }

  function applySnapshot(snapshot) {
    state.snapshot = snapshot;
    state.base = snapshot.baseEntities || [];
    state.lists = Object.groupBy ? Object.groupBy(state.base, entity => entity.kind) : state.base.reduce((all, entity) => {
      (all[entity.kind] ||= []).push(entity); return all;
    }, {});
    for (const entity of state.base) entity._bounds = boundsOf(entity);
    state.doors = new Map((state.lists.door || []).filter(x => x.properties?.buildingId).map(x => [x.properties.buildingId, x]));
    state.reality = new Map((snapshot.realityEntities || []).map(x => [x.id, x]));
    state.players = new Map((snapshot.players || []).map(x => [x.id, x]));
    state.actors = new Map((snapshot.actors || []).map(x => [x.id, x]));
    state.weather = snapshot.weather;
    ui.realityName.textContent = snapshot.reality.name;
    const me = state.players.get(state.playerId);
    if (me && !Number.isFinite(state.camera.x)) state.camera = { x: me.position.x, y: me.position.y };
    if (me && state.frame === 0) state.camera = { x: me.position.x, y: me.position.y };
    updateMode(me?.travelMode);
  }

  function handle(message) {
    switch (message.type) {
      case 'welcome':
        state.playerId = message.playerId; applySnapshot(message.snapshot);
        setStatus(`${message.snapshot.players.length} linked · ${(message.snapshot.actors || []).length} living actors · protocol v${message.protocolVersion}`, true);
        break;
      case 'playerJoined': case 'playerUpdated': updatePlayer(message.player); break;
      case 'playersUpdated': for(const player of message.players) updatePlayer(player); break;
      case 'playerMoved': updatePlayer(message.player, true); break;
      case 'playerLeft': state.players.delete(message.playerId); break;
      case 'actorsMoved': for (const actor of message.actors) { state.actors.set(actor.id, actor); if (actor.isMoving) state.movingUntil.set(actor.id, performance.now() + 700); } break;
      case 'pathResult': if (message.sequence === state.pathSequence) state.path = message.waypoints || []; break;
      case 'pathUnavailable': if (message.sequence === state.pathSequence) stopTravel(message.message); break;
      case 'movementBlocked': if (Date.now() - state.lastBlocked > 1200) { state.lastBlocked = Date.now(); stopTravel(message.message); } break;
      case 'playerFell': updatePlayer(message.player); stopTravel(message.message); break;
      case 'playerDied': updatePlayer(message.player); state.follow = true; stopTravel(message.reason); break;
      case 'playerTeleported': updatePlayer(message.player); if(message.player.id===state.playerId){state.follow=true;state.path=[];state.target=null;showToast('Teleported.');} break;
      case 'chatSaid': receiveChat(message.chat); break;
      case 'weatherChanged': state.weather = message.weather; break;
      case 'worldRebuilt': applySnapshot(message.snapshot); state.path = []; state.target = null; showToast('Area rebuilt from its geographic source.'); break;
      case 'objectCreated': state.reality.set(message.entity.id, message.entity); break;
      case 'objectRemoved': state.reality.delete(message.entityId); break;
      case 'error': showToast(message.message); break;
    }
  }
  function updatePlayer(player, moving = false) {
    const old = state.players.get(player.id);
    if (old) {
      const dx = player.position.x - old.position.x, dy = player.position.y - old.position.y;
      if (Math.abs(dx) > Math.abs(dy)) state.facings.set(player.id, dx > 0 ? 'east' : 'west');
      else if (Math.abs(dy) > .001) state.facings.set(player.id, dy > 0 ? 'north' : 'south');
    }
    state.players.set(player.id, player);
    if (moving && player.speedMetersPerSecond > .01) state.movingUntil.set(player.id, performance.now() + 250);
    if (player.id === state.playerId) updateMode(player.travelMode);
  }
  function stopTravel(message) { state.path = []; state.target = null; if (message) showToast(message); }
  function setStatus(text, online) { ui.status.textContent = text; ui.dot.classList.toggle('online', online); }
  function showToast(text) { ui.toast.textContent = text || ''; ui.toast.classList.add('show'); clearTimeout(showToast.timer); showToast.timer = setTimeout(() => ui.toast.classList.remove('show'), 3200); }

  function resize() {
    const ratio = devicePixelRatio || 1; canvas.width = Math.round(innerWidth * ratio); canvas.height = Math.round(innerHeight * ratio);
    canvas.style.width = `${innerWidth}px`; canvas.style.height = `${innerHeight}px`; ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  }
  function toScreen(point) {
    const dx = point.x - state.camera.x, dy = point.y - state.camera.y;
    return { x: innerWidth / 2 + (dx + dy * state.shear) * state.scale, y: innerHeight / 2 - dy * state.scale * state.pitch };
  }
  function toWorld(point) {
    const dy = -(point.y - innerHeight / 2) / (state.scale * state.pitch);
    return { x: state.camera.x + (point.x - innerWidth / 2) / state.scale - dy * state.shear, y: state.camera.y + dy };
  }
  const lod = () => state.scale >= 9 ? 2 : state.scale >= 3.5 ? 1 : 0;
  function viewBounds(margin = 10) {
    const points = [toWorld({x:0,y:0}), toWorld({x:innerWidth,y:0}), toWorld({x:0,y:innerHeight}), toWorld({x:innerWidth,y:innerHeight})];
    return { minX: Math.min(...points.map(p=>p.x))-margin, maxX: Math.max(...points.map(p=>p.x))+margin, minY: Math.min(...points.map(p=>p.y))-margin, maxY: Math.max(...points.map(p=>p.y))+margin };
  }
  function boundsOf(entity) {
    const points = entity.geometry?.length ? entity.geometry : [entity.position];
    return { minX: Math.min(...points.map(p=>p.x)), maxX: Math.max(...points.map(p=>p.x)), minY: Math.min(...points.map(p=>p.y)), maxY: Math.max(...points.map(p=>p.y)) };
  }
  function visible(entity, view) { const b = entity._bounds || boundsOf(entity); return b.maxX >= view.minX && b.minX <= view.maxX && b.maxY >= view.minY && b.minY <= view.maxY; }
  function path(entity, close = false) {
    if (!entity.geometry?.length) return false; ctx.beginPath();
    entity.geometry.forEach((point, index) => { const p = toScreen(point); index ? ctx.lineTo(p.x,p.y) : ctx.moveTo(p.x,p.y); });
    if (close) ctx.closePath(); return true;
  }
  function drawGeometry(entity, fill, stroke, width = 1, close = false) {
    if (!path(entity, close)) return; if (fill) { ctx.fillStyle = fill; ctx.fill(); } if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = width; ctx.lineJoin='round'; ctx.lineCap='round'; ctx.stroke(); }
  }
  function prop(entity, name, fallback) { const value = Number(entity.properties?.[name]); return Number.isFinite(value) ? value : fallback; }
  function hash(value) { let h=2166136261; for (let i=0;i<String(value).length;i++) h=Math.imul(h^String(value).charCodeAt(i),16777619); return (h>>>0)/4294967295; }

  function render(now) {
    requestAnimationFrame(render); state.frame++;
    ctx.clearRect(0,0,innerWidth,innerHeight); ctx.fillStyle='#315a38'; ctx.fillRect(0,0,innerWidth,innerHeight);
    if (!state.snapshot) return connecting();
    const me = state.players.get(state.playerId);
    if (me && state.follow) { state.camera.x += (me.position.x-state.camera.x)*.14; state.camera.y += (me.position.y-state.camera.y)*.14; }
    const view = viewBounds(16), detail = lod();
    drawGrass(view, detail);
    for (const e of state.lists.terrain||[]) if (visible(e,view)) drawTerrain(e,detail);
    drawSidewalkNetwork(state.lists.sidewalk||[],view,detail);
    drawRoadNetwork(state.lists.road||[],view,detail);
    for (const e of state.lists.water||[]) if (visible(e,view)) drawWater(e,detail,now);
    drawTarget(now);
    drawRaised(view,detail,now);
    drawAtmosphere(me,detail,now);
    drawSpeechBubbles(view);
    updateTelemetry(me);
  }
  function connecting() { ctx.fillStyle='#e7eadf'; ctx.textAlign='center'; ctx.font='700 15px monospace'; ctx.fillText('Resolving geographic reality…',innerWidth/2,innerHeight/2); }

  function drawGrass(view,detail) {
    const tile = detail===2?2:detail===1?6:18;
    for(let y=Math.floor(view.minY/tile)*tile;y<view.maxY;y+=tile) for(let x=Math.floor(view.minX/tile)*tile;x<view.maxX;x+=tile){
      const p=[toScreen({x,y}),toScreen({x:x+tile,y}),toScreen({x:x+tile,y:y+tile}),toScreen({x,y:y+tile})];
      const n=hash(`${Math.floor(x/tile)}:${Math.floor(y/tile)}`); ctx.fillStyle=n>.52?'#4f7c3e':'#477339';
      ctx.beginPath();ctx.moveTo(p[0].x,p[0].y);p.slice(1).forEach(q=>ctx.lineTo(q.x,q.y));ctx.closePath();ctx.fill();
      if(detail===2&&n>.68){const c=toScreen({x:x+tile*n,y:y+tile*(1-n)});ctx.strokeStyle='#8eae5d';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(c.x,c.y+3);ctx.lineTo(c.x-2,c.y);ctx.moveTo(c.x,c.y+3);ctx.lineTo(c.x+2,c.y);ctx.stroke();}
    }
  }
  function drawTerrain(e,detail){const t=e.properties?.terrain||'grass';const colors={grass:['#588442','#729a52'],forest:['#244a31','#376641'],sand:['#cbb06a','#ead08b'],mud:['#624632','#866249'],pavement:['#6d736e','#989c96']};const c=colors[t]||colors.grass;drawGeometry(e,c[0],detail?c[1]:null,detail?1:0,true);}
  function drawSidewalkNetwork(entities,view,detail){const roads=entities.filter(e=>visible(e,view));for(const e of roads){const width=prop(e,'widthMeters',8)*state.scale;drawGeometry(e,null,'#827f73',Math.max(3,width+2));}for(const e of roads){const width=prop(e,'widthMeters',8)*state.scale;drawGeometry(e,null,'#c3bdae',Math.max(2,width));}if(detail===2){ctx.save();ctx.setLineDash([state.scale*1.8,state.scale*.12]);for(const e of roads)drawGeometry(e,null,'rgba(92,88,79,.35)',1);ctx.restore();}}
  function drawRoadNetwork(entities,view,detail){const roads=entities.filter(e=>visible(e,view));for(const e of roads){const width=prop(e,'widthMeters',6)*state.scale,unpaved=e.properties?.surface==='unpaved';drawGeometry(e,null,unpaved?'#654b36':'#242a29',Math.max(4,width+3));}for(const e of roads){const width=prop(e,'widthMeters',6)*state.scale,unpaved=e.properties?.surface==='unpaved';drawGeometry(e,null,unpaved?'#806043':'#4b514f',Math.max(3,width));}if(detail){ctx.save();ctx.setLineDash([state.scale*2.2,state.scale*1.4]);for(const e of roads){const highway=e.properties?.highway||'',unpaved=e.properties?.surface==='unpaved';if(!['motorway','trunk','primary','secondary','tertiary'].includes(highway))continue;drawGeometry(e,null,unpaved?'#c9a96a':'#e8c854',Math.max(1,state.scale*.08));}ctx.restore();}}
  function drawWater(e,detail,now){const closed=e.geometry?.length>3;const wave=detail===2?Math.sin(now/400)*2:0;if(closed){drawGeometry(e,'#15516b','#65b3bf',Math.max(3,state.scale*5+wave),true);drawGeometry(e,null,'#2e839b',Math.max(2,state.scale*1.4),true);}else{drawGeometry(e,null,'#65b3bf',Math.max(4,state.scale*3));drawGeometry(e,null,'#1e6e8a',Math.max(2,state.scale*1.2));}}

  function drawRaised(view,detail,now){const items=[];
    for(const e of state.lists.building||[])if(visible(e,view))items.push({d:Math.max(...e.geometry.map(p=>toScreen(p).y)),f:()=>drawBuilding(e,detail)});
    if(detail>0) for(const kind of ['tree','bush','fence','vehicle'])for(const e of state.lists[kind]||[])if(visible(e,view))items.push({d:toScreen(e.position).y,f:()=>drawObject(e,detail)});
    for(const actor of state.actors.values())if(pointVisible(actor.position,view)&&detail>0)items.push({d:toScreen(actor.position).y,f:()=>drawActor(actor,detail,now)});
    for(const player of state.players.values())if(pointVisible(player.position,view))items.push({d:toScreen(player.position).y,f:()=>drawPlayer(player,player.id===state.playerId,detail,now)});
    items.sort((a,b)=>a.d-b.d);for(const item of items)item.f();
  }
  function pointVisible(p,v){return p.x>=v.minX&&p.x<=v.maxX&&p.y>=v.minY&&p.y<=v.maxY;}
  function drawBuilding(e,detail){const ground=e.geometry.map(toScreen);if(ground.length<3)return;const last=ground[ground.length-1];const count=ground[0].x===last.x&&ground[0].y===last.y?ground.length-1:ground.length;const levels=Math.max(1,Number(e.properties?.['building:levels']||e.properties?.levels||2));const height=Math.min(150,levels*3*state.scale*.52);const roof=ground.map(p=>({x:p.x,y:p.y-height}));
    if(detail===0){drawGeometry(e,'#665b50','#302b27',1,true);return;}
    for(let i=0;i<count;i++){const j=(i+1)%count;ctx.fillStyle=i%2?'#665044':'#755a49';ctx.beginPath();ctx.moveTo(roof[i].x,roof[i].y);ctx.lineTo(roof[j].x,roof[j].y);ctx.lineTo(ground[j].x,ground[j].y);ctx.lineTo(ground[i].x,ground[i].y);ctx.closePath();ctx.fill();ctx.strokeStyle='#382c26';ctx.stroke();if(detail===2)drawWindows(roof[i],roof[j],ground[i],ground[j],levels);}
    ctx.fillStyle='#8f755e';ctx.strokeStyle='#c09b75';ctx.lineWidth=1.5;ctx.beginPath();ctx.moveTo(roof[0].x,roof[0].y);for(let i=1;i<count;i++)ctx.lineTo(roof[i].x,roof[i].y);ctx.closePath();ctx.fill();ctx.stroke();
    if(detail===2){const a=roof[0],b=roof[Math.floor(count/2)];ctx.strokeStyle='#594434';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);ctx.stroke();const n=hash(e.id);const c=roof[Math.floor(n*count)%count];ctx.fillStyle='#3c3029';ctx.fillRect(c.x-3,c.y-9,6,10);}
    const door=state.doors.get(e.id);if(door)drawDoor(door);
  }
  function drawWindows(a,b,ga,gb,levels){const length=Math.hypot(b.x-a.x,b.y-a.y);const columns=Math.max(1,Math.floor(length/28));for(let level=0;level<levels;level++)for(let c=1;c<=columns;c++){const t=c/(columns+1),baseY=(ga.y+(gb.y-ga.y)*t),roofY=(a.y+(b.y-a.y)*t),y=baseY-(baseY-roofY)*(level+.55)/levels,x=ga.x+(gb.x-ga.x)*t;ctx.fillStyle=state.weather?.isDay?'#9ec2bd':'#d9bd69';ctx.fillRect(x-3,y-4,6,7);ctx.strokeStyle='#302c29';ctx.strokeRect(x-3,y-4,6,7);}}
  function drawDoor(e){const angle=prop(e,'facingDegrees',0)*Math.PI/180,tx=-Math.sin(angle)*.48,ty=Math.cos(angle)*.48;const l=toScreen({x:e.position.x-tx,y:e.position.y-ty}),r=toScreen({x:e.position.x+tx,y:e.position.y+ty}),h=2.05*state.scale*.52;ctx.fillStyle='#302119';ctx.strokeStyle='#c69745';ctx.beginPath();ctx.moveTo(l.x,l.y);ctx.lineTo(r.x,r.y);ctx.lineTo(r.x,r.y-h);ctx.lineTo(l.x,l.y-h);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#efc965';ctx.fillRect(r.x-2,r.y-h*.48,2,2);}
  function drawObject(e,detail){if(e.kind==='tree')drawTree(e,detail);else if(e.kind==='bush')drawBush(e);else if(e.kind==='fence'){drawGeometry(e,null,'#6d472b',Math.max(2,state.scale*.15));}else if(e.kind==='vehicle')drawVehicle(e);}
  function drawTree(e,detail){const p=toScreen(e.position),r=Math.max(3,state.scale*(detail===2?1.05:.65));ctx.fillStyle='#493322';ctx.fillRect(p.x-2,p.y-r*.35,4,r*.7);ctx.fillStyle='#173f29';ctx.beginPath();ctx.arc(p.x,p.y-r*.65,r,0,Math.PI*2);ctx.fill();ctx.fillStyle='#347044';ctx.beginPath();ctx.arc(p.x-r*.3,p.y-r*.85,r*.65,0,Math.PI*2);ctx.fill();}
  function drawBush(e){const p=toScreen(e.position),r=Math.max(2,state.scale*.42);ctx.fillStyle='#245d35';ctx.beginPath();ctx.arc(p.x-r*.4,p.y,r*.65,0,Math.PI*2);ctx.arc(p.x+r*.35,p.y,r*.72,0,Math.PI*2);ctx.fill();ctx.fillStyle='#5e9b45';ctx.fillRect(p.x-1,p.y-r*.45,2,2);}
  function drawVehicle(e){const p=toScreen(e.position),l=prop(e,'lengthMeters',4.5)*state.scale,w=prop(e,'widthMeters',1.9)*state.scale*state.pitch,a=prop(e,'rotationDegrees',0)*Math.PI/180,pa=Math.atan2(-Math.sin(a)*state.pitch,Math.cos(a)+Math.sin(a)*state.shear);ctx.save();ctx.translate(p.x,p.y);ctx.rotate(pa);ctx.fillStyle='#8d4e3e';ctx.fillRect(-l/2,-w/2,l,w);ctx.fillStyle='#a8c8c8';ctx.fillRect(-l*.14,-w*.38,l*.35,w*.76);ctx.strokeStyle='#242827';ctx.lineWidth=2;ctx.strokeRect(-l/2,-w/2,l,w);ctx.restore();}
  function drawActor(a,detail,now){const p=toScreen(a.position),moving=(a.isMoving||state.movingUntil.get(a.id)>now)&&detail===2,bob=moving?Math.sin(now/90+hash(a.id)*6)*2:0;const animal=a.kind==='animal';const colors={rabbit:'#dad4c5',dog:'#9b6b3e',cat:'#77736a',bird:'#6ba1a5',deer:'#9c7148',cougar:'#c29154',bear:'#4b3427'};ctx.save();ctx.translate(p.x,p.y+bob);if(animal){const size=Math.max(3,state.scale*(a.subtype==='bear'?.55:a.subtype==='deer'?.43:.3));ctx.fillStyle=colors[a.subtype]||'#c7a36a';ctx.beginPath();ctx.ellipse(0,-size*.5,size,size*.62,0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.arc(size*.7,-size*.75,size*.45,0,Math.PI*2);ctx.fill();}else{drawPersonShape(0,0,'#c9864f','#324e7a',moving,now,hash(a.id));ctx.fillStyle='#fff0ba';ctx.font='9px monospace';ctx.textAlign='center';ctx.fillText(a.name,0,-state.scale*1.35);}ctx.restore();}
  function drawPlayer(player,isMe,detail,now){const p=toScreen(player.position),moving=state.movingUntil.get(player.id)>now&&detail===2,facing=state.facings.get(player.id)||'south';ctx.save();ctx.translate(p.x,p.y);drawPersonShape(0,0,isMe?'#e6c86c':'#d68855',isMe?'#37689a':'#784f91',moving,now,hash(player.id),player.travelMode,facing);if(detail>0){ctx.fillStyle=isMe?'#fff3a5':'#f0e8d1';ctx.font='700 9px monospace';ctx.textAlign='center';ctx.fillText(isMe?'YOU':player.name,0,-state.scale*1.55);}ctx.restore();}
  function drawPersonShape(x,y,skin,shirt,moving,now,phase,mode='walk',facing='south'){
    const s=Math.max(5,state.scale*.46),cycle=Math.sin(now/105+phase*6);
    if(mode==='bike'){
      const direction=facing==='west'?-1:1,wheelY=s*.48,wheelRadius=s*.42,left=-s*.72,right=s*.72,spin=moving?now/38:0;
      ctx.fillStyle='rgba(0,0,0,.28)';ctx.beginPath();ctx.ellipse(x,y+s*.86,s*1.35,s*.2,0,0,Math.PI*2);ctx.fill();
      ctx.strokeStyle='#202524';ctx.lineWidth=Math.max(1.5,s*.11);for(const center of [left,right]){ctx.beginPath();ctx.arc(x+center,y+wheelY,wheelRadius,0,Math.PI*2);ctx.stroke();if(moving){for(let spoke=0;spoke<4;spoke++){const a=spin+spoke*Math.PI/2;ctx.beginPath();ctx.moveTo(x+center,y+wheelY);ctx.lineTo(x+center+Math.cos(a)*wheelRadius,y+wheelY+Math.sin(a)*wheelRadius);ctx.stroke();}}}
      ctx.strokeStyle='#d14e42';ctx.lineWidth=Math.max(2,s*.15);ctx.lineJoin='round';ctx.beginPath();ctx.moveTo(x+left,y+wheelY);ctx.lineTo(x-s*.16,y-s*.02);ctx.lineTo(x+s*.2,y+wheelY);ctx.lineTo(x+left,y+wheelY);ctx.lineTo(x+s*.36,y-s*.02);ctx.lineTo(x+right,y+wheelY);ctx.moveTo(x+s*.36,y-s*.02);ctx.lineTo(x+s*.55,y-s*.27);ctx.lineTo(x+s*.72,y-s*.25);ctx.stroke();
      ctx.strokeStyle='#292421';ctx.lineWidth=Math.max(2,s*.17);ctx.lineCap='round';const pedal=moving?cycle*s*.31:0;ctx.beginPath();ctx.moveTo(x-s*.12,y-s*.34);ctx.lineTo(x-s*.18+pedal,y+s*.13);ctx.lineTo(x+s*.2,y+wheelY);ctx.moveTo(x+s*.05,y-s*.34);ctx.lineTo(x+s*.18-pedal,y+s*.13);ctx.lineTo(x-s*.03,y+wheelY);ctx.stroke();
      ctx.fillStyle=shirt;ctx.save();ctx.translate(x,y);ctx.rotate(.13*direction);ctx.fillRect(-s*.37,-s*1.08,s*.72,s*.7);ctx.strokeStyle=skin;ctx.lineWidth=Math.max(2,s*.13);ctx.beginPath();ctx.moveTo(s*.27,-s*.82);ctx.lineTo(s*.62*direction,-s*.33);ctx.stroke();ctx.fillStyle=skin;ctx.beginPath();ctx.arc(-s*.05*direction,-s*1.28,s*.32,0,Math.PI*2);ctx.fill();ctx.restore();return;
    }
    if(mode==='skateboard'){
      const direction=facing==='west'?-1:1,push=moving?Math.max(0,cycle):0,glide=moving?Math.sin(now/210+phase*5)*.05:0;
      ctx.fillStyle='rgba(0,0,0,.3)';ctx.beginPath();ctx.ellipse(x,y+s*.72,s*1.05,s*.24,0,0,Math.PI*2);ctx.fill();
      ctx.save();ctx.translate(x,y+s*.48);ctx.rotate(glide*direction);ctx.fillStyle='#d39a38';ctx.strokeStyle='#38291c';ctx.lineWidth=1.5;ctx.beginPath();ctx.roundRect(-s*.92,-s*.12,s*1.84,s*.24,s*.12);ctx.fill();ctx.stroke();ctx.fillStyle='#202423';ctx.beginPath();ctx.arc(-s*.62,s*.2,s*.12,0,Math.PI*2);ctx.arc(s*.62,s*.2,s*.12,0,Math.PI*2);ctx.fill();ctx.restore();
      ctx.save();ctx.translate(x,y);ctx.rotate(-.08*direction-(moving?cycle*.035:0));
      ctx.strokeStyle='#292421';ctx.lineWidth=Math.max(2,s*.18);ctx.lineCap='round';ctx.beginPath();
      ctx.moveTo(-s*.2*direction,-s*.18);ctx.lineTo(-s*.48*direction,s*.39);
      ctx.moveTo(s*.12*direction,-s*.16);ctx.lineTo((s*.45+s*.7*push)*direction,s*(.36+.48*push));ctx.stroke();
      ctx.fillStyle=shirt;ctx.beginPath();ctx.moveTo(-s*.48,-s*.8);ctx.lineTo(s*.38,-s*.72);ctx.lineTo(s*.28,-s*.05);ctx.lineTo(-s*.36,-s*.1);ctx.closePath();ctx.fill();
      ctx.strokeStyle=skin;ctx.lineWidth=Math.max(2,s*.14);ctx.beginPath();ctx.moveTo(-s*.33,-s*.62);ctx.lineTo(-s*.85*direction,-s*.38);ctx.moveTo(s*.28,-s*.57);ctx.lineTo(s*.82*direction,-s*.77);ctx.stroke();
      ctx.fillStyle=skin;ctx.beginPath();ctx.arc(-s*.08*direction,-s*.98,s*.34,0,Math.PI*2);ctx.fill();ctx.restore();return;
    }
    const step=moving?Math.sin(now/75+phase*6)*s*.32:0,bob=moving?Math.abs(Math.sin(now/75+phase*6))*1.5:0;ctx.fillStyle='rgba(0,0,0,.28)';ctx.beginPath();ctx.ellipse(x,y+2,s*.65,s*.25,0,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#292421';ctx.lineWidth=Math.max(2,s*.18);ctx.beginPath();ctx.moveTo(x-s*.18,y-s*.2-bob);ctx.lineTo(x-s*.22-step,y+s*.48);ctx.moveTo(x+s*.18,y-s*.2-bob);ctx.lineTo(x+s*.22+step,y+s*.48);ctx.stroke();ctx.fillStyle=shirt;ctx.fillRect(x-s*.38,y-s*.82-bob,s*.76,s*.75);ctx.fillStyle=skin;ctx.beginPath();ctx.arc(x,y-s*1.03-bob,s*.34,0,Math.PI*2);ctx.fill();
  }

  function receiveChat(chat){
    state.speech.set(chat.playerId,{chat,expiresAt:Date.now()+10000});
    const speaker=state.players.get(chat.playerId)||state.actors.get(chat.playerId);
    if(chat.playerId===state.playerId||(speaker&&pointVisible(speaker.position,viewBounds()))) {
      state.chat.push(chat); if(state.chat.length>10)state.chat.splice(0,state.chat.length-10); renderChatHistory();
    }
  }
  function renderChatHistory(){
    ui.chatMessages.replaceChildren();
    if(!state.chat.length){const empty=document.createElement('div');empty.className='empty-chat';empty.textContent='No messages yet.';ui.chatMessages.append(empty);return;}
    for(const chat of state.chat){const row=document.createElement('div');row.className='chat-message';const meta=document.createElement('div');meta.className='chat-meta';const user=document.createElement('strong');user.textContent=chat.username;const time=document.createElement('span');time.textContent=new Date(chat.saidAtUtc).toLocaleTimeString([],{hour:'numeric',minute:'2-digit',second:'2-digit'});meta.append(user,time);const message=document.createElement('div');message.className='chat-text';message.textContent=chat.message;row.append(meta,message);ui.chatMessages.append(row);}ui.chatMessages.scrollTop=ui.chatMessages.scrollHeight;
  }
  function drawSpeechBubbles(view){
    const now=Date.now();
    for(const [speakerId,speech] of state.speech){if(speech.expiresAt<=now){state.speech.delete(speakerId);continue;}const speaker=state.players.get(speakerId)||state.actors.get(speakerId);if(!speaker||!pointVisible(speaker.position,view))continue;const anchor=toScreen(speaker.position),maxWidth=220;ctx.save();ctx.font='12px "Trebuchet MS",sans-serif';const lines=wrapChat(`“${speech.chat.message}”`,maxWidth-18);ctx.font='700 10px "Trebuchet MS",sans-serif';const nameWidth=ctx.measureText(speech.chat.username).width;ctx.font='12px "Trebuchet MS",sans-serif';const textWidth=Math.max(nameWidth,...lines.map(line=>ctx.measureText(line).width));const width=Math.min(maxWidth,Math.max(72,textWidth+18)),height=24+lines.length*15;const x=Math.max(5,Math.min(innerWidth-width-5,anchor.x-width/2)),y=Math.max(5,anchor.y-state.scale*1.7-height);ctx.fillStyle='rgba(255,248,218,.96)';ctx.strokeStyle='#4a3520';ctx.lineWidth=2;ctx.beginPath();ctx.roundRect(x,y,width,height,6);ctx.fill();ctx.stroke();const tipX=Math.max(x+12,Math.min(x+width-12,anchor.x));ctx.beginPath();ctx.moveTo(tipX-7,y+height);ctx.lineTo(tipX+7,y+height);ctx.lineTo(anchor.x,anchor.y-state.scale*.75);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#5a3b1f';ctx.font='700 10px "Trebuchet MS",sans-serif';ctx.textAlign='left';ctx.fillText(speech.chat.username,x+9,y+14);ctx.fillStyle='#1d211e';ctx.font='12px "Trebuchet MS",sans-serif';lines.forEach((line,index)=>ctx.fillText(line,x+9,y+29+index*15));ctx.restore();}
  }
  function wrapChat(text,maxWidth){const words=text.split(/\s+/),lines=[];let line='';for(const word of words){const test=line?`${line} ${word}`:word;if(ctx.measureText(test).width<=maxWidth||!line)line=test;else{lines.push(line);line=word;}}if(line)lines.push(line);return lines.slice(0,6);}

  function drawTarget(now){if(!state.target)return;const p=toScreen(state.target),r=8+Math.sin(now/180)*2;ctx.strokeStyle='#fff09a';ctx.lineWidth=2;ctx.beginPath();ctx.arc(p.x,p.y,r,0,Math.PI*2);ctx.stroke();}
  function drawAtmosphere(me,detail,now){const light=daylight();const moonBoost=!state.weather?.isDay?Math.max(0,state.weather?.moonIllumination||0)*.18:0;const darkness=Math.max(0,.7-light*.7-moonBoost);if(darkness>.02){if(me){const p=toScreen(me.position),radius=detail===0?70:150,gradient=ctx.createRadialGradient(p.x,p.y,10,p.x,p.y,radius);gradient.addColorStop(0,`rgba(8,18,29,${darkness*.06})`);gradient.addColorStop(.35,`rgba(8,18,29,${darkness*.16})`);gradient.addColorStop(1,`rgba(8,18,29,${darkness})`);ctx.fillStyle=gradient;}else ctx.fillStyle=`rgba(8,18,29,${darkness})`;ctx.fillRect(0,0,innerWidth,innerHeight);}
    const code=state.weather?.weatherCode??0,rain=(code>=51&&code<=99)&&code<71||code>=80,snow=code>=71&&code<=77;if((rain||snow)&&detail>0){const count=detail===2?90:35;ctx.strokeStyle=rain?'rgba(169,211,232,.55)':'rgba(245,250,255,.8)';ctx.lineWidth=rain?1:2;for(let i=0;i<count;i++){const seed=hash(`${i}:${Math.floor(now/120)}`),x=(seed*innerWidth+(now*.28*(i%3+1)))%innerWidth,y=(hash(`${i}:y`)*innerHeight+now*.45)%innerHeight;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x-(rain?4:1),y+(rain?11:2));ctx.stroke();}}
  }
  function daylight(){if(!state.weather?.sunriseUtc||!state.weather?.sunsetUtc)return state.weather?.isDay?1:.12;const now=Date.now(),rise=Date.parse(state.weather.sunriseUtc),set=Date.parse(state.weather.sunsetUtc),twilight=45*60000;if(now<rise-twilight||now>set+twilight)return .08;if(now<rise)return .08+.92*(now-(rise-twilight))/twilight;if(now>set)return 1-.92*(now-set)/twilight;return 1;}

  function worldToGps(position){const region=position.region,lat0=(region.latitudeBand+.5)*Math.PI/180,lon0=(region.longitudeBand+.5)*Math.PI/180,R=6378137,e2=6.69437999014e-3,sin=Math.sin(lat0),den=Math.sqrt(1-e2*sin*sin),mLon=R*Math.cos(lat0)/den,mLat=R*(1-e2)/Math.pow(1-e2*sin*sin,1.5);return{latitude:(lat0+position.y/mLat)*180/Math.PI,longitude:(lon0+position.x/mLon)*180/Math.PI};}
  function gpsText(position){if(!position)return'—';const g=worldToGps(position);return`${g.latitude.toFixed(6)}, ${g.longitude.toFixed(6)}`;}
  function updateTelemetry(me){if(!me)return;ui.playerGps.textContent=gpsText(me.position);ui.destinationGps.textContent=state.target?gpsText({...state.target,region:me.position.region}):'—';ui.terrain.textContent=title(me.terrain);ui.elevation.textContent=`${me.position.z.toFixed(1)} m / ${(me.position.z*3.28084).toFixed(0)} ft`;ui.speed.textContent=`${(me.speedMetersPerSecond*2.23694).toFixed(1)} mph`;const distance=state.target?Math.hypot(state.target.x-me.position.x,state.target.y-me.position.y):null;ui.distance.textContent=distance===null?'—':distance>=1000?`${(distance/1000).toFixed(2)} km`:`${distance.toFixed(1)} m`;ui.camera.textContent=`${Math.hypot(state.camera.x-me.position.x,state.camera.y-me.position.y).toFixed(1)} m · ${lod()===2?'full':lod()===1?'medium':'light'} detail`;const w=state.weather;ui.weather.textContent=w?.isAvailable?`${w.condition} · ${w.temperatureCelsius.toFixed(1)} °C / ${(w.temperatureCelsius*9/5+32).toFixed(0)} °F`:'Unavailable';ui.sun.textContent=w?.sunriseUtc?`${clock(w.sunriseUtc)} / ${clock(w.sunsetUtc)}`:'—';ui.moon.textContent=w?.moonPhase?`${w.moonPhase} · ${Math.round(w.moonIllumination*100)}%`:'—';const hearts=Math.max(0,me.healthHearts??10),full=Math.floor(hearts),empty=Math.max(0,10-Math.ceil(hearts));ui.hearts.textContent=`${'♥'.repeat(full)}${hearts%1?'◒':''}${'♡'.repeat(empty)}  ${hearts.toFixed(2)}/10`;const stamina=Math.max(0,me.stamina??10),staminaFull=Math.floor(stamina),staminaEmpty=Math.max(0,10-Math.ceil(stamina));ui.stamina.textContent=`${'◆'.repeat(staminaFull)}${stamina%1?'◇':''}${'·'.repeat(staminaEmpty)}  ${stamina.toFixed(2)}/10`;}
  const title=value=>String(value||'').replace(/([A-Z])/g,' $1').trim().replace(/^./,c=>c.toUpperCase());
  const clock=value=>new Date(value).toLocaleTimeString([],{hour:'numeric',minute:'2-digit'});
  function updateMode(mode){document.querySelectorAll('[data-mode]').forEach(button=>button.classList.toggle('active',button.dataset.mode===String(mode||'walk').toLowerCase()));}

  function movementLoop(time){const me=state.players.get(state.playerId);if(me&&time-state.lastInput>28){let dx=0,dy=0;if(state.keys.has('w')||state.keys.has('arrowup'))dy+=1;if(state.keys.has('s')||state.keys.has('arrowdown'))dy-=1;if(state.keys.has('a')||state.keys.has('arrowleft'))dx-=1;if(state.keys.has('d')||state.keys.has('arrowright'))dx+=1;if(dx||dy){state.target=null;state.path=[];send({type:'moveRequest',x:dx,y:dy,sequence:++state.pathSequence});state.lastInput=time;}else if(state.target){while(state.path.length&&Math.hypot(state.path[0].x-me.position.x,state.path[0].y-me.position.y)<.45)state.path.shift();const waypoint=state.path[0]||state.target,tx=waypoint.x-me.position.x,ty=waypoint.y-me.position.y,d=Math.hypot(tx,ty);if(d<.4&&!state.path.length){state.target=null;}else if(d>.01){send({type:'moveRequest',x:tx/d,y:ty/d,sequence:state.pathSequence});state.lastInput=time;}}}requestAnimationFrame(movementLoop);}

  canvas.addEventListener('mousedown',event=>{if(event.button===2){state.pointer={down:true,dragged:false,x:event.clientX,y:event.clientY};ui.actionMenu.hidden=true;}});
  addEventListener('mousemove',event=>{if(!state.pointer.down)return;const dx=event.clientX-state.pointer.x,dy=event.clientY-state.pointer.y;if(Math.hypot(dx,dy)>2)state.pointer.dragged=true;const before=toWorld({x:innerWidth/2,y:innerHeight/2});state.camera.x-=dx/state.scale;state.camera.y+=dy/(state.scale*state.pitch);state.camera.x+=dy/state.scale*state.shear/state.pitch;state.pointer.x=event.clientX;state.pointer.y=event.clientY;state.follow=false;});
  addEventListener('mouseup',event=>{if(event.button!==2||!state.pointer.down)return;if(!state.pointer.dragged){state.actionPoint=toWorld({x:event.clientX,y:event.clientY});updateActionMenu();ui.actionMenu.style.left=`${Math.min(event.clientX,innerWidth-195)}px`;ui.actionMenu.style.top=`${Math.min(event.clientY,innerHeight-110)}px`;ui.actionMenu.hidden=false;}state.pointer.down=false;});
  canvas.addEventListener('contextmenu',event=>event.preventDefault());
  canvas.addEventListener('click',event=>{if(event.button!==0)return;ui.actionMenu.hidden=true;const target=toWorld({x:event.clientX,y:event.clientY});state.target=target;state.path=[];state.pathSequence++;send({type:'pathRequest',x:target.x,y:target.y,sequence:state.pathSequence});});
  canvas.addEventListener('wheel',event=>{event.preventDefault();const anchor=toWorld({x:event.clientX,y:event.clientY});state.scale=Math.max(1.2,Math.min(28,state.scale*Math.exp(-event.deltaY*.001)));const after=toWorld({x:event.clientX,y:event.clientY});state.camera.x+=anchor.x-after.x;state.camera.y+=anchor.y-after.y;state.follow=false;},{passive:false});
  addEventListener('keydown',event=>{if(event.target.matches('input,textarea'))return;const key=event.key.toLowerCase();if(['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright'].includes(key)){event.preventDefault();state.keys.add(key);state.follow=true;}});
  addEventListener('keyup',event=>state.keys.delete(event.key.toLowerCase()));
  ui.center.addEventListener('click',()=>state.follow=true);
  document.querySelectorAll('[data-mode]').forEach(button=>button.addEventListener('click',()=>send({type:'setTravelMode',mode:button.dataset.mode})));
  function updateActionMenu(){const canTeleport=ui.god.checked&&state.actionPoint;ui.teleport.hidden=!canTeleport;ui.noActions.hidden=!!canTeleport;}
  ui.god.addEventListener('change',()=>{ui.rebuild.disabled=!ui.god.checked;updateActionMenu();});
  ui.teleport.addEventListener('click',()=>{if(!ui.god.checked||!state.actionPoint)return;send({type:'teleport',x:state.actionPoint.x,y:state.actionPoint.y,godMode:true});ui.actionMenu.hidden=true;});
  ui.chatForm.addEventListener('submit',event=>{event.preventDefault();const message=ui.chatInput.value.trim();if(!message)return;send({type:'say',message});ui.chatInput.value='';ui.chatInput.focus();});
  ui.toggleChat.addEventListener('click',()=>{state.chatVisible=!state.chatVisible;ui.chatHistory.hidden=!state.chatVisible;ui.toggleChat.textContent=state.chatVisible?'Hide chat':'Show chat';if(state.chatVisible)renderChatHistory();});
  ui.rebuild.addEventListener('click',()=>{if(!ui.god.checked)return;if(confirm('Rebuild this area from its geographic source? All player-created area changes will be removed.'))send({type:'rebuildArea',godMode:true});});
  addEventListener('resize',resize);resize();connect();requestAnimationFrame(render);requestAnimationFrame(movementLoop);
})();
