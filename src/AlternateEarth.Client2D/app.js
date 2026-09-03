(() => {
  'use strict';
  const $ = selector => document.querySelector(selector);
  const canvas = $('#world');
  const ctx = canvas.getContext('2d');
  const lightCanvas=document.createElement('canvas'),lightCtx=lightCanvas.getContext('2d');
  const ui = {
    status: $('#status'), dot: $('#connectionDot'), realityName: $('#realityName'), toast: $('#toast'),
    terrain: $('#terrainValue'), elevation: $('#elevationValue'), speed: $('#speedValue'), distance: $('#distanceValue'),
    camera: $('#cameraValue'), weather: $('#weatherValue'), sun: $('#sunValue'), moon: $('#moonValue'), hearts: $('#heartsValue'), stamina: $('#staminaValue'), water: $('#waterValue'), gas: $('#gasValue'), wallet: $('#walletValue'), effects: $('#effectsValue'),
    playerGps: $('#playerGpsValue'), destinationGps: $('#destinationGpsValue'), baseCompass: $('#baseCompassValue'), actionMenu: $('#actionMenu'),
    noActions: $('#noActions'), teleport: $('#teleportButton'), chatForm: $('#chatForm'), chatInput: $('#chatInput'),
    chatHistory: $('#chatHistory'), chatMessages: $('#chatMessages'), toggleChat: $('#toggleChatButton'),
    center: $('#centerButton'), god: $('#godMode'), gpsTeleport:$('#gpsTeleportButton'), rebuild: $('#rebuildButton'), inventory: $('#inventoryItems'), inventoryPanel:$('.inventory-panel'), inventoryContent:$('#inventoryContent'), toggleInventory:$('#toggleInventoryButton'), flashlight:$('#flashlightToggle'), lantern:$('#lanternToggle'),laser:$('#laserToggle'), equipmentHat:$('#equipmentHat'), equipmentShirt:$('#equipmentShirt'), equipmentGloves:$('#equipmentGloves'), equipmentPants:$('#equipmentPants'), equipmentShoes:$('#equipmentShoes'), equipmentWeapon:$('#equipmentWeapon'),
    trade: $('#tradeButton'), purchaseBase:$('#purchaseBaseButton'), tradeWindow: $('#tradeWindow'), tradeTitle: $('#tradeTitle'), tradeFriend: $('#tradeFriend'), tradeOffers: $('#tradeOffers'), tradeCancel: $('#tradeCancel'), tradeConfirm: $('#tradeConfirm'), tooltip: $('#actorTooltip'), accountSetup: $('#accountSetup'), accountForm: $('#accountForm'), accountUsername: $('#accountUsername'), accountPassword: $('#accountPassword'), accountError: $('#accountError'),characterPanel:$('#characterPanel'),characterList:$('#characterList'),characterForm:$('#characterForm'),characterName:$('#characterName'),realitySetup:$('#realitySetup'),realitySetupForm:$('#realitySetupForm'),realityLatitude:$('#realityLatitude'),realityLongitude:$('#realityLongitude'),realitySetupError:$('#realitySetupError'),useServerGps:$('#useServerGpsButton')
  };
  const state = {
    socket: null, playerId: null, snapshot: null, weather: null, base: [], lists: {},
    players: new Map(), actors: new Map(), reality: new Map(), doors: new Map(), facings: new Map(), movingUntil: new Map(),
    chat: [], speech: new Map(), chatVisible: false, privateState: null, dungeon: null, relationships: new Map(), chests: new Map(), loot: new Map(), seenChests: new Set(),
    camera: { x: 0, y: 0 }, scale: 26, pitch: .69, shear: .14, follow: true,
    keys: new Set(), path: [], target: null, pathSequence: 0, lastInput: 0, lastBlocked: 0,
    pointer: { down: false, dragged: false, button: null, startX: 0, startY: 0, x: 0, y: 0 }, suppressClick: false, actionPoint: null, actionActor: null, actionDoor:null, pendingDoor: null, pendingChest: null, pendingGpsTeleport: null, frame: 0, projectiles: [], outdoorFog: new Set(), areaLoading: false, godTogglePending: null
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
  async function bootstrap(){try{const setupResponse=await fetch('/api/reality/setup');const setup=await setupResponse.json();if(setup.required){ui.accountSetup.hidden=true;ui.realitySetup.hidden=false;return;}ui.realitySetup.hidden=true;const response=await fetch('/api/account/me');if(response.ok){ui.accountSetup.hidden=true;connect();}else ui.accountSetup.hidden=false;}catch{ui.accountSetup.hidden=false;ui.accountError.textContent='The local reality server is unavailable.';}}
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
    updateMode(me?.travelMode);syncGodControls(me);
  }

  function applyPrivate(privateState) {
    if (!privateState) return;
    state.privateState = privateState; state.dungeon = privateState.dungeon || null;
    state.relationships = new Map((privateState.relationships || []).map(item => [item.actorId, item.friendRating]));
    state.chests = new Map((privateState.chests || []).map(item => [item.id, item]));
    state.loot = new Map((privateState.loot || []).map(item => [item.id, item]));
    renderInventory(privateState.inventory);
    renderEquipment(state.players.get(state.playerId));
    ui.characterPanel.hidden=!state.dungeon?.isHome;if(state.dungeon?.isHome)loadCharacters();
  }

  async function loadCharacters(force=false){if(!force&&Date.now()-(state.lastCharacterLoad||0)<5000)return;state.lastCharacterLoad=Date.now();const response=await fetch('/api/account/characters');if(!response.ok)return;const body=await response.json();ui.characterList.replaceChildren();for(const character of body.characters){const row=document.createElement('div');row.className='character-row';const name=document.createElement('span');name.textContent=character.name+(character.id===body.activeCharacterId?' (active)':'');row.append(name);if(character.id!==body.activeCharacterId){const select=document.createElement('button');select.type='button';select.textContent='Play';select.addEventListener('click',async()=>{const r=await fetch(`/api/account/characters/${character.id}/select`,{method:'POST'});if(r.ok)location.reload();else showToast((await r.json()).message);});const remove=document.createElement('button');remove.type='button';remove.textContent='Remove';remove.addEventListener('click',async()=>{if(!confirm(`Remove ${character.name}? This deletes that character's saved state.`))return;const r=await fetch(`/api/account/characters/${character.id}`,{method:'DELETE'});if(r.ok)loadCharacters(true);else showToast((await r.json()).message);});row.append(select,remove);}ui.characterList.append(row);}}

  function handle(message) {
    switch (message.type) {
      case 'welcome':
        state.playerId = message.playerId; applySnapshot(message.snapshot); applyPrivate(message.privateState);
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
      case 'playerDied': updatePlayer(message.player);if(message.privateState){applyPrivate(message.privateState);state.dungeon=message.privateState.dungeon||null;}state.follow = true; stopTravel(message.reason); break;
      case 'playerTeleported': updatePlayer(message.player); if(message.player.id===state.playerId){state.follow=true;state.path=[];state.target=null;showToast('Teleported.');} break;
      case 'chatSaid': receiveChat(message.chat); break;
      case 'weatherChanged': state.weather = message.weather; break;
      case 'privateState': applyPrivate(message.privateState); break;
      case 'dungeonEntered': updatePlayer(message.player); state.path=[];state.target=null;applyPrivate(message.privateState);state.follow=true;showToast('Entered dungeon — unexplored rooms are hidden by fog.');break;
      case 'dungeonExited': {updatePlayer(message.player);applySnapshot(message.snapshot);applyPrivate(message.privateState);state.dungeon=null;state.follow=true;const gpsTarget=state.pendingGpsTeleport;state.pendingGpsTeleport=null;if(gpsTarget){showToast('Loading a safe GPS landing point…');send({type:'teleport',x:gpsTarget.x,y:gpsTarget.y,godMode:true});}else showToast('Returned to the world.');break;}
      case 'dungeonUpdated': state.dungeon=message.dungeon;break;
      case 'worldExpanded': { const previous=state.snapshot?.bounds;applySnapshot(message.snapshot);if(message.expanded!==false)markNewAreaFog(previous,message.snapshot.bounds);state.areaLoading=false;showToast(message.expanded===false?'Area already loaded.':'New area ready — explore to reveal it.');break; }
      case 'tradeQuote': openTrade(message.quote); break;
      case 'tradeCompleted': updatePlayer(message.player);applyPrivate({...state.privateState,inventory:message.inventory,relationships:[...(state.privateState?.relationships||[]).filter(x=>x.actorId!==message.relationship.actorId),message.relationship]});ui.tradeWindow.hidden=true;showToast('Trade completed.');break;
      case 'chestOpened': case 'lootCollected': applyPrivate(message.privateState);showToast(message.message);break;
      case 'rested': updatePlayer(message.player);applyPrivate(message.privateState);showToast('Fully rested: health, stamina, and water restored for five minutes.');break;
      case 'basePurchased': updatePlayer(message.player);applyPrivate(message.privateState);ui.actionMenu.hidden=true;showToast(`New base purchased for $${(message.priceCents/100).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}.`);break;
      case 'chestUpdated': state.chests.set(message.chest.id,message.chest);break;
      case 'combatEvent': receiveCombat(message.combat);break;
      case 'worldRebuilt': applySnapshot(message.snapshot);applyPrivate(message.privateState);state.path=[];state.target=null;state.dungeon=message.privateState?.dungeon||null;state.follow=true;showToast('The entire reality was reset and rebuilt.');break;
      case 'objectCreated': state.reality.set(message.entity.id, message.entity); break;
      case 'objectRemoved': state.reality.delete(message.entityId); break;
      case 'error': state.areaLoading=false;showToast(message.message); break;
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
    if (player.id === state.playerId) { updateMode(player.travelMode);syncGodControls(player);renderEquipment(player);if(state.privateState?.inventory)renderInventory(state.privateState.inventory); }
  }
  function syncGodControls(player){if(!player)return;if(state.godTogglePending!==null&&!!player.godMode!==state.godTogglePending)return;state.godTogglePending=null;ui.god.checked=!!player.godMode;ui.gpsTeleport.hidden=!player.godMode;ui.rebuild.disabled=!player.godMode;}
  function stopTravel(message) { state.path = []; state.target = null; if (message) showToast(message); }
  function setStatus(text, online) { ui.status.textContent = text; ui.dot.classList.toggle('online', online); }
  function showToast(text) { ui.toast.textContent = text || ''; ui.toast.classList.add('show'); clearTimeout(showToast.timer); showToast.timer = setTimeout(() => ui.toast.classList.remove('show'), 3200); }

  function resize() {
    const ratio = devicePixelRatio || 1; canvas.width = Math.round(innerWidth * ratio); canvas.height = Math.round(innerHeight * ratio);
    canvas.style.width = `${innerWidth}px`; canvas.style.height = `${innerHeight}px`; ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    lightCanvas.width=innerWidth;lightCanvas.height=innerHeight;
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
  const fogCellSize=32;
  function fogKey(x,y){return`${x},${y}`;}
  function markNewAreaFog(previous,next){
    if(!previous||!next)return;
    const minX=Math.floor(next.minimumX/fogCellSize),maxX=Math.ceil(next.maximumX/fogCellSize),minY=Math.floor(next.minimumY/fogCellSize),maxY=Math.ceil(next.maximumY/fogCellSize);
    for(let x=minX;x<maxX;x++)for(let y=minY;y<maxY;y++){const cx=(x+.5)*fogCellSize,cy=(y+.5)*fogCellSize;if(cx<previous.minimumX||cx>previous.maximumX||cy<previous.minimumY||cy>previous.maximumY)state.outdoorFog.add(fogKey(x,y));}
  }
  function drawOutdoorFog(view,me){
    if(!state.outdoorFog.size||!me)return;
    const px=Math.floor(me.position.x/fogCellSize),py=Math.floor(me.position.y/fogCellSize);for(let x=px-1;x<=px+1;x++)for(let y=py-1;y<=py+1;y++)state.outdoorFog.delete(fogKey(x,y));
    const minX=Math.floor(view.minX/fogCellSize),maxX=Math.ceil(view.maxX/fogCellSize),minY=Math.floor(view.minY/fogCellSize),maxY=Math.ceil(view.maxY/fogCellSize);ctx.save();ctx.fillStyle='rgba(14,20,25,.88)';
    for(let x=minX;x<maxX;x++)for(let y=minY;y<maxY;y++){if(!state.outdoorFog.has(fogKey(x,y)))continue;const corners=[toScreen({x:x*fogCellSize,y:y*fogCellSize}),toScreen({x:(x+1)*fogCellSize,y:y*fogCellSize}),toScreen({x:(x+1)*fogCellSize,y:(y+1)*fogCellSize}),toScreen({x:x*fogCellSize,y:(y+1)*fogCellSize})];ctx.beginPath();ctx.moveTo(corners[0].x,corners[0].y);for(const p of corners.slice(1))ctx.lineTo(p.x,p.y);ctx.closePath();ctx.fill();}
    ctx.restore();
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
    if(me?.locationId==='outdoor'&&!state.areaLoading&&state.snapshot.bounds&&Date.now()-(state.lastAreaRequest||0)>1200&&(state.camera.x<state.snapshot.bounds.minimumX+120||state.camera.x>state.snapshot.bounds.maximumX-120||state.camera.y<state.snapshot.bounds.minimumY+120||state.camera.y>state.snapshot.bounds.maximumY-120)){state.lastAreaRequest=Date.now();state.areaLoading=true;showToast('Loading and generating the next map area…');send({type:'requestArea',x:state.camera.x,y:state.camera.y});}
    if (me?.locationId !== 'outdoor' && state.dungeon) {
      drawDungeon(state.dungeon,view,detail,now); drawProjectiles(now);drawLaser(me); drawSpeechBubbles(view); updateTelemetry(me);syncLightControls(me); return;
    }
    drawGrass(view, detail);
    for (const e of state.lists.terrain||[]) if (visible(e,view)) drawTerrain(e,detail);
    drawSidewalkNetwork(state.lists.sidewalk||[],view,detail);
    drawRoadNetwork(state.lists.road||[],view,detail);
    for (const e of state.lists.water||[]) if (visible(e,view)) drawWater(e,detail,now);
    drawTarget(now);
    drawRaised(view,detail,now);
    drawStreetNames(state.lists.road||[],view,detail);
    drawChestsAndLoot(view,now);
    drawProjectiles(now);
    drawAtmosphere(me,detail,now);drawOutdoorFog(view,me);drawLaser(me);
    drawSpeechBubbles(view);
    updateTelemetry(me);syncLightControls(me);
  }

  function revealedAt(x,y){return !!state.dungeon?.revealedCells?.includes(`${Math.floor(x/3)},${Math.floor(y/3)}`);}
  function drawDungeon(dungeon,view,detail,now){
    ctx.fillStyle='#0b0e0d';ctx.fillRect(0,0,innerWidth,innerHeight);
    const corners=[toScreen({x:0,y:0}),toScreen({x:dungeon.width,y:0}),toScreen({x:dungeon.width,y:dungeon.height}),toScreen({x:0,y:dungeon.height})];
    ctx.fillStyle='#5b554b';ctx.strokeStyle='#b19a72';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(corners[0].x,corners[0].y);corners.slice(1).forEach(p=>ctx.lineTo(p.x,p.y));ctx.closePath();ctx.fill();ctx.stroke();
    ctx.strokeStyle='#2b211b';ctx.lineWidth=Math.max(4,state.scale*.35);ctx.lineCap='square';
    for(const wall of dungeon.walls||[]){const a=toScreen({x:wall.x1,y:wall.y1}),b=toScreen({x:wall.x2,y:wall.y2});ctx.beginPath();if(wall.doorStart>=0){const vertical=Math.abs(wall.x1-wall.x2)<.01;if(vertical){const d1=toScreen({x:wall.x1,y:wall.doorStart}),d2=toScreen({x:wall.x1,y:wall.doorEnd});ctx.moveTo(a.x,a.y);ctx.lineTo(d1.x,d1.y);ctx.moveTo(d2.x,d2.y);ctx.lineTo(b.x,b.y);}else{const d1=toScreen({x:wall.doorStart,y:wall.y1}),d2=toScreen({x:wall.doorEnd,y:wall.y1});ctx.moveTo(a.x,a.y);ctx.lineTo(d1.x,d1.y);ctx.moveTo(d2.x,d2.y);ctx.lineTo(b.x,b.y);}}else{ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);}ctx.stroke();}
    const exit=toScreen(dungeon.exit);ctx.fillStyle='#a9d6d0';ctx.fillRect(exit.x-8,exit.y-5,16,10);ctx.fillStyle='#10201d';ctx.font='8px monospace';ctx.textAlign='center';ctx.fillText('EXIT',exit.x,exit.y+3);
    for(const item of dungeon.furnishings||[])drawFurnishing(item,now);
    const actors=dungeon.actors||[];for(const actor of actors){if(revealedAt(actor.position.x,actor.position.y))drawActor(actor,detail,now);}
    drawChestsAndLoot(view,now);
    const me=state.players.get(state.playerId);if(me)drawPlayer(me,true,detail,now);
    if(!dungeon.isHome){const cell=3;for(let y=0;y<dungeon.height;y+=cell)for(let x=0;x<dungeon.width;x+=cell){if(revealedAt(x+1.5,y+1.5))continue;const p=[toScreen({x,y}),toScreen({x:x+cell,y}),toScreen({x:x+cell,y:y+cell}),toScreen({x,y:y+cell})];ctx.fillStyle='rgba(1,4,3,.94)';ctx.beginPath();ctx.moveTo(p[0].x,p[0].y);p.slice(1).forEach(q=>ctx.lineTo(q.x,q.y));ctx.closePath();ctx.fill();}}
  }
  function drawFurnishing(item,now){const p=toScreen(item.position),type=item.properties?.objectType,s=state.scale;if(type==='fireplace'){ctx.fillStyle='#5b3324';ctx.fillRect(p.x-12,p.y-15,24,15);ctx.fillStyle='#ffb238';ctx.beginPath();ctx.arc(p.x,p.y-7,5+Math.sin(now/130)*2,0,Math.PI*2);ctx.fill();const g=ctx.createRadialGradient(p.x,p.y-8,3,p.x,p.y-8,100);g.addColorStop(0,'rgba(255,170,65,.35)');g.addColorStop(1,'rgba(255,170,65,0)');ctx.fillStyle=g;ctx.beginPath();ctx.arc(p.x,p.y-8,100,0,Math.PI*2);ctx.fill();}else if(type==='bed'){ctx.fillStyle='#753d38';ctx.fillRect(p.x-s,p.y-s*.5,s*2,s);ctx.fillStyle='#e7dfbd';ctx.fillRect(p.x-s*.8,p.y-s*.4,s*.5,s*.8);}else if(type==='table'){ctx.fillStyle='#6f492c';ctx.beginPath();ctx.arc(p.x,p.y-s*.25,s*.75,0,Math.PI*2);ctx.fill();}else{ctx.fillStyle='#594028';ctx.fillRect(p.x-s*.35,p.y-s*.35,s*.7,s*.7);}}
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
  function drawStreetNames(entities,view,detail){if(detail===0)return;const occupied=[];for(const e of entities){const name=e.properties?.name;if(!name||!visible(e,view)||!e.geometry?.length)continue;let best=null;for(let i=0;i<e.geometry.length-1;i++){const a=toScreen(e.geometry[i]),b=toScreen(e.geometry[i+1]),length=Math.hypot(b.x-a.x,b.y-a.y);if(!best||length>best.length)best={a,b,length};}if(!best||best.length<55)continue;const x=(best.a.x+best.b.x)/2,y=(best.a.y+best.b.y)/2;if(x<45||x>innerWidth-45||y<25||y>innerHeight-25||occupied.some(p=>Math.abs(p.x-x)<120&&Math.abs(p.y-y)<26))continue;let angle=Math.atan2(best.b.y-best.a.y,best.b.x-best.a.x);if(angle>Math.PI/2)angle-=Math.PI;if(angle<-Math.PI/2)angle+=Math.PI;ctx.save();ctx.translate(x,y);ctx.rotate(angle);ctx.textAlign='center';ctx.textBaseline='middle';ctx.font=`700 ${detail===2?11:9}px monospace`;ctx.lineWidth=4;ctx.strokeStyle='rgba(22,24,22,.82)';ctx.strokeText(name,0,0,Math.max(70,best.length-12));ctx.fillStyle='#fff3bf';ctx.fillText(name,0,0,Math.max(70,best.length-12));ctx.restore();occupied.push({x,y});}}
  function drawWater(e,detail,now){const closed=e.geometry?.length>3;const wave=detail===2?Math.sin(now/400)*2:0;if(closed){drawGeometry(e,'#15516b','#65b3bf',Math.max(3,state.scale*5+wave),true);drawGeometry(e,null,'#2e839b',Math.max(2,state.scale*1.4),true);}else{drawGeometry(e,null,'#65b3bf',Math.max(4,state.scale*3));drawGeometry(e,null,'#1e6e8a',Math.max(2,state.scale*1.2));}}

  function drawRaised(view,detail,now){const items=[];
    for(const e of state.lists.building||[])if(visible(e,view))items.push({d:Math.max(...e.geometry.map(p=>toScreen(p).y)),f:()=>drawBuilding(e,detail)});
    if(detail>0) for(const kind of ['tree','bush','fence','vehicle','streetLight'])for(const e of state.lists[kind]||[])if(visible(e,view))items.push({d:toScreen(e.position).y,f:()=>drawObject(e,detail)});
    for(const actor of state.actors.values())if((actor.locationId||'outdoor')==='outdoor'&&pointVisible(actor.position,view)&&detail>0)items.push({d:toScreen(actor.position).y,f:()=>drawActor(actor,detail,now)});
    for(const player of state.players.values())if((player.locationId||'outdoor')==='outdoor'&&pointVisible(player.position,view))items.push({d:toScreen(player.position).y,f:()=>drawPlayer(player,player.id===state.playerId,detail,now)});
    items.sort((a,b)=>a.d-b.d);for(const item of items)item.f();
  }
  function pointVisible(p,v){return p.x>=v.minX&&p.x<=v.maxX&&p.y>=v.minY&&p.y<=v.maxY;}
  function drawBuilding(e,detail){const ground=e.geometry.map(toScreen);if(ground.length<3)return;const last=ground[ground.length-1];const count=ground[0].x===last.x&&ground[0].y===last.y?ground.length-1:ground.length;const levels=Math.max(1,Number(e.properties?.['building:levels']||e.properties?.levels||2));const height=Math.min(150,levels*3*state.scale*.52);const roof=ground.map(p=>({x:p.x,y:p.y-height}));
    if(detail===0){drawGeometry(e,'#665b50','#302b27',1,true);return;}
    const lightsOn=hash(`${e.id}:${Math.floor(Date.now()/900000)}`)>.48,isBase=state.privateState?.base?.buildingId===e.id;
    for(let i=0;i<count;i++){const j=(i+1)%count;ctx.fillStyle=isBase?(i%2?'#486a78':'#547d89'):(i%2?'#665044':'#755a49');ctx.beginPath();ctx.moveTo(roof[i].x,roof[i].y);ctx.lineTo(roof[j].x,roof[j].y);ctx.lineTo(ground[j].x,ground[j].y);ctx.lineTo(ground[i].x,ground[i].y);ctx.closePath();ctx.fill();ctx.strokeStyle='#382c26';ctx.stroke();if(detail===2)drawWindows(roof[i],roof[j],ground[i],ground[j],levels,lightsOn);}
    ctx.fillStyle='#8f755e';ctx.strokeStyle='#c09b75';ctx.lineWidth=1.5;ctx.beginPath();ctx.moveTo(roof[0].x,roof[0].y);for(let i=1;i<count;i++)ctx.lineTo(roof[i].x,roof[i].y);ctx.closePath();ctx.fill();ctx.stroke();
    if(detail===2){const a=roof[0],b=roof[Math.floor(count/2)];ctx.strokeStyle='#594434';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);ctx.stroke();const n=hash(e.id);const c=roof[Math.floor(n*count)%count];ctx.fillStyle='#3c3029';ctx.fillRect(c.x-3,c.y-9,6,10);}
    const door=state.doors.get(e.id);if(door)drawDoor(door);
    if(isBase&&detail>0){const c=roof.slice(0,count).reduce((a,p)=>({x:a.x+p.x/count,y:a.y+p.y/count}),{x:0,y:0}),poleHeight=state.scale*6.5,flagWidth=state.scale*6,flagHeight=state.scale*2.4,top=c.y-poleHeight;ctx.strokeStyle='#f5e8bd';ctx.lineWidth=Math.max(3,state.scale*.14);ctx.beginPath();ctx.moveTo(c.x,c.y+2);ctx.lineTo(c.x,top);ctx.stroke();ctx.fillStyle='#e2bd4e';ctx.strokeStyle='#5b421b';ctx.lineWidth=Math.max(2,state.scale*.08);ctx.beginPath();ctx.moveTo(c.x,top);ctx.lineTo(c.x+flagWidth,top+flagHeight*.25);ctx.lineTo(c.x+flagWidth*.88,top+flagHeight);ctx.lineTo(c.x,top+flagHeight*.72);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#362612';ctx.font=`700 ${Math.max(9,Math.min(18,state.scale*.48))}px monospace`;ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(`${state.privateState.base.ownerName}'s base`,c.x+flagWidth*.47,top+flagHeight*.52,flagWidth*.82);}
  }
  function drawWindows(a,b,ga,gb,levels,lightsOn){const length=Math.hypot(b.x-a.x,b.y-a.y);const columns=Math.max(1,Math.floor(length/32));for(let level=0;level<levels;level++)for(let c=1;c<=columns;c++){const t=c/(columns+1),baseY=(ga.y+(gb.y-ga.y)*t),roofY=(a.y+(b.y-a.y)*t),y=baseY-(baseY-roofY)*(level+.55)/levels,x=ga.x+(gb.x-ga.x)*t;ctx.fillStyle=state.weather?.isDay?'#9ec2bd':lightsOn?'#ffd96a':'#23353b';ctx.fillRect(x-5,y-6,10,10);ctx.strokeStyle='#302c29';ctx.strokeRect(x-5,y-6,10,10);}}
  function drawDoor(e){const p=toScreen(e.position),w=Math.max(9,state.scale*.95),h=Math.max(16,2.05*state.scale*.52);ctx.fillStyle='#302119';ctx.strokeStyle='#d4a453';ctx.lineWidth=2;ctx.fillRect(p.x-w/2,p.y-h,w,h);ctx.strokeRect(p.x-w/2,p.y-h,w,h);ctx.fillStyle='#efc965';ctx.beginPath();ctx.arc(p.x+w*.28,p.y-h*.48,2,0,Math.PI*2);ctx.fill();}
  function drawObject(e,detail){if(e.kind==='tree')drawTree(e,detail);else if(e.kind==='bush')drawBush(e);else if(e.kind==='fence'){drawGeometry(e,null,'#6d472b',Math.max(2,state.scale*.15));}else if(e.kind==='vehicle')drawVehicle(e);else if(e.kind==='streetLight')drawStreetLight(e);}
  function drawStreetLight(e){const p=toScreen(e.position),night=new Date().getHours()>=19||new Date().getHours()<7,h=Math.max(16,state.scale*2.2);ctx.strokeStyle='#252c29';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(p.x,p.y-h);ctx.lineTo(p.x+7,p.y-h);ctx.stroke();ctx.fillStyle=night?'#fff0a0':'#7f887d';ctx.beginPath();ctx.arc(p.x+8,p.y-h+1,4,0,Math.PI*2);ctx.fill();if(night){const g=ctx.createRadialGradient(p.x+8,p.y-h,2,p.x+8,p.y-h,45);g.addColorStop(0,'rgba(255,232,130,.38)');g.addColorStop(1,'rgba(255,232,130,0)');ctx.fillStyle=g;ctx.beginPath();ctx.arc(p.x+8,p.y-h,45,0,Math.PI*2);ctx.fill();}}
  function drawTree(e,detail){const p=toScreen(e.position),r=Math.max(3,state.scale*(detail===2?1.05:.65));ctx.fillStyle='#493322';ctx.fillRect(p.x-2,p.y-r*.35,4,r*.7);ctx.fillStyle='#173f29';ctx.beginPath();ctx.arc(p.x,p.y-r*.65,r,0,Math.PI*2);ctx.fill();ctx.fillStyle='#347044';ctx.beginPath();ctx.arc(p.x-r*.3,p.y-r*.85,r*.65,0,Math.PI*2);ctx.fill();}
  function drawBush(e){const p=toScreen(e.position),r=Math.max(2,state.scale*.42);ctx.fillStyle='#245d35';ctx.beginPath();ctx.arc(p.x-r*.4,p.y,r*.65,0,Math.PI*2);ctx.arc(p.x+r*.35,p.y,r*.72,0,Math.PI*2);ctx.fill();ctx.fillStyle='#5e9b45';ctx.fillRect(p.x-1,p.y-r*.45,2,2);}
  function drawVehicle(e){const p=toScreen(e.position),l=prop(e,'lengthMeters',4.5)*state.scale,w=prop(e,'widthMeters',1.9)*state.scale*state.pitch,a=prop(e,'rotationDegrees',0)*Math.PI/180,pa=Math.atan2(-Math.sin(a)*state.pitch,Math.cos(a)+Math.sin(a)*state.shear);ctx.save();ctx.translate(p.x,p.y);ctx.rotate(pa);ctx.fillStyle='#8d4e3e';ctx.fillRect(-l/2,-w/2,l,w);ctx.fillStyle='#a8c8c8';ctx.fillRect(-l*.14,-w*.38,l*.35,w*.76);ctx.strokeStyle='#242827';ctx.lineWidth=2;ctx.strokeRect(-l/2,-w/2,l,w);ctx.restore();}
  function drawActor(a,detail,now){const p=toScreen(a.position),moving=(a.isMoving||state.movingUntil.get(a.id)>now)&&detail===2,bob=moving?Math.sin(now/90+hash(a.id)*6)*2:0;const animal=a.kind==='animal';const colors={rabbit:'#dad4c5',dog:'#9b6b3e',cat:'#77736a',bird:'#6ba1a5',deer:'#9c7148',cougar:'#c29154',bear:'#4b3427'};ctx.save();ctx.translate(p.x,p.y+bob);if(animal){const size=Math.max(3,state.scale*(a.subtype==='bear'?.55:a.subtype==='deer'?.43:.3));ctx.fillStyle=colors[a.subtype]||'#c7a36a';ctx.beginPath();ctx.ellipse(0,-size*.5,size,size*.62,0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.arc(size*.7,-size*.75,size*.45,0,Math.PI*2);ctx.fill();}else{drawPersonShape(0,0,'#c9864f','#324e7a',moving,now,hash(a.id));ctx.fillStyle='#fff0ba';ctx.font='9px monospace';ctx.textAlign='center';ctx.fillText(a.name,0,-state.scale*1.35);}ctx.restore();}
  function drawPlayer(player,isMe,detail,now){const p=toScreen(player.position),moving=state.movingUntil.get(player.id)>now&&detail===2,facing=state.facings.get(player.id)||'south';ctx.save();ctx.translate(p.x,p.y);drawPersonShape(0,0,isMe?'#e6c86c':'#d68855',isMe?'#37689a':'#784f91',moving,now,hash(player.id),player.travelMode,facing);if(player.hatOn&&detail>0)drawWornHat(player.travelMode);if(detail>0){ctx.fillStyle=isMe?'#fff3a5':'#f0e8d1';ctx.font='700 9px monospace';ctx.textAlign='center';ctx.fillText(isMe?'YOU':player.name,0,-state.scale*1.55);}ctx.restore();}
  function drawWornHat(mode){const s=Math.max(5,state.scale*.46),y=['bike','dirtBike','motorcycle'].includes(mode)?-s*1.52:mode==='skateboard'?-s*1.22:-s*1.28;ctx.fillStyle='#69482f';ctx.fillRect(-s*.48,y-s*.12,s*.96,s*.2);ctx.beginPath();ctx.arc(0,y-s*.08,s*.34,Math.PI,0);ctx.fill();}
  function drawBikeRider(x,y,skin,shirt,moving,now,phase,facing,s){
    const direction=facing==='west'?-1:1,wheelY=s*.48,wheelRadius=s*.42,left=-s*.72,right=s*.72,spin=moving?now/38:0,cycle=Math.sin(now/105+phase*6);
    ctx.fillStyle='rgba(0,0,0,.28)';ctx.beginPath();ctx.ellipse(x,y+s*.86,s*1.35,s*.2,0,0,Math.PI*2);ctx.fill();
    ctx.strokeStyle='#202524';ctx.lineWidth=Math.max(1.5,s*.11);for(const center of [left,right]){ctx.beginPath();ctx.arc(x+center,y+wheelY,wheelRadius,0,Math.PI*2);ctx.stroke();if(moving){for(let spoke=0;spoke<4;spoke++){const a=spin+spoke*Math.PI/2;ctx.beginPath();ctx.moveTo(x+center,y+wheelY);ctx.lineTo(x+center+Math.cos(a)*wheelRadius,y+wheelY+Math.sin(a)*wheelRadius);ctx.stroke();}}}
    ctx.strokeStyle='#d14e42';ctx.lineWidth=Math.max(2,s*.15);ctx.lineJoin='round';ctx.beginPath();ctx.moveTo(x+left,y+wheelY);ctx.lineTo(x-s*.16,y-s*.02);ctx.lineTo(x+s*.2,y+wheelY);ctx.lineTo(x+left,y+wheelY);ctx.lineTo(x+s*.36,y-s*.02);ctx.lineTo(x+right,y+wheelY);ctx.moveTo(x+s*.36,y-s*.02);ctx.lineTo(x+s*.55,y-s*.27);ctx.lineTo(x+s*.72,y-s*.25);ctx.stroke();
    ctx.strokeStyle='#292421';ctx.lineWidth=Math.max(2,s*.17);ctx.lineCap='round';const pedal=moving?cycle*s*.31:0;ctx.beginPath();ctx.moveTo(x-s*.12,y-s*.34);ctx.lineTo(x-s*.18+pedal,y+s*.13);ctx.lineTo(x+s*.2,y+wheelY);ctx.moveTo(x+s*.05,y-s*.34);ctx.lineTo(x+s*.18-pedal,y+s*.13);ctx.lineTo(x-s*.03,y+wheelY);ctx.stroke();
    ctx.fillStyle=shirt;ctx.save();ctx.translate(x,y);ctx.rotate(.13*direction);ctx.fillRect(-s*.37,-s*1.08,s*.72,s*.7);ctx.strokeStyle=skin;ctx.lineWidth=Math.max(2,s*.13);ctx.beginPath();ctx.moveTo(s*.27,-s*.82);ctx.lineTo(s*.62*direction,-s*.33);ctx.stroke();ctx.fillStyle=skin;ctx.beginPath();ctx.arc(-s*.05*direction,-s*1.28,s*.32,0,Math.PI*2);ctx.fill();ctx.restore();
  }
  function drawDirtBikeRider(x,y,skin,shirt,moving,now,phase,facing,s){
    const direction=facing==='west'?-1:1,bounce=moving?Math.abs(Math.sin(now/72+phase*7))*s*.13:0,spin=moving?now/24:0,rear=-s*.82,front=s*.88,wheelY=s*.5-bounce,wheelRadius=s*.48;
    ctx.fillStyle='rgba(0,0,0,.28)';ctx.beginPath();ctx.ellipse(x,y+s*.9,s*1.55,s*.23,0,0,Math.PI*2);ctx.fill();
    ctx.save();ctx.translate(0,-bounce);
    for(const center of [rear,front]){ctx.strokeStyle='#171b19';ctx.lineWidth=Math.max(3,s*.2);ctx.beginPath();ctx.arc(x+center,y+wheelY,wheelRadius,0,Math.PI*2);ctx.stroke();ctx.strokeStyle='#9b8e72';ctx.lineWidth=1;for(let spoke=0;spoke<6;spoke++){const a=spin+spoke*Math.PI/3;ctx.beginPath();ctx.moveTo(x+center,y+wheelY);ctx.lineTo(x+center+Math.cos(a)*wheelRadius*.78,y+wheelY+Math.sin(a)*wheelRadius*.78);ctx.stroke();}ctx.strokeStyle='#2b302c';ctx.lineWidth=Math.max(1,s*.08);for(let knob=0;knob<10;knob++){const a=knob*Math.PI/5;ctx.beginPath();ctx.moveTo(x+center+Math.cos(a)*wheelRadius*.92,y+wheelY+Math.sin(a)*wheelRadius*.92);ctx.lineTo(x+center+Math.cos(a)*wheelRadius*1.12,y+wheelY+Math.sin(a)*wheelRadius*1.12);ctx.stroke();}}
    ctx.strokeStyle='#ec6a27';ctx.lineWidth=Math.max(2,s*.17);ctx.lineJoin='round';ctx.beginPath();ctx.moveTo(x+rear,y+wheelY);ctx.lineTo(x-s*.22,y-s*.06);ctx.lineTo(x+s*.2,y+wheelY);ctx.lineTo(x+front,y+wheelY);ctx.moveTo(x+s*.2,y+wheelY);ctx.lineTo(x+s*.48,y-s*.3);ctx.lineTo(x+s*.78,y-s*.4);ctx.stroke();
    ctx.fillStyle='#d84d20';ctx.beginPath();ctx.moveTo(x-s*.36,y-s*.37);ctx.lineTo(x+s*.36,y-s*.42);ctx.lineTo(x+s*.48,y-s*.13);ctx.lineTo(x-s*.18,y-s*.05);ctx.closePath();ctx.fill();ctx.fillStyle='#242725';ctx.fillRect(x-s*.48,y-s*.48,s*.58,s*.14);ctx.fillStyle='#555e59';ctx.fillRect(x-s*.18,y-s*.04,s*.45,s*.35);
    ctx.fillStyle=shirt;ctx.save();ctx.translate(x,y-bounce);ctx.rotate(.2*direction);ctx.fillRect(-s*.36,-s*1.14,s*.7,s*.66);ctx.strokeStyle='#292421';ctx.lineWidth=Math.max(2,s*.18);ctx.beginPath();ctx.moveTo(-s*.2,-s*.5);ctx.lineTo(-s*.55,s*.05);ctx.moveTo(s*.14,-s*.48);ctx.lineTo(s*.33,s*.12);ctx.stroke();ctx.strokeStyle=skin;ctx.lineWidth=Math.max(2,s*.13);ctx.beginPath();ctx.moveTo(s*.25,-s*.9);ctx.lineTo(s*.72*direction,-s*.42);ctx.stroke();ctx.fillStyle=skin;ctx.beginPath();ctx.arc(-s*.05*direction,-s*1.34,s*.31,0,Math.PI*2);ctx.fill();ctx.restore();ctx.restore();
    if(moving){ctx.fillStyle='rgba(179,145,91,.38)';for(let i=0;i<3;i++){const drift=(now/18+i*s*.55)%(s*1.5);ctx.beginPath();ctx.arc(x-direction*(s*.95+drift),y+s*(.65+i*.08),Math.max(1,s*(.13-i*.025)),0,Math.PI*2);ctx.fill();}}
  }
  function drawMotorcycleRider(x,y,skin,shirt,moving,now,phase,facing,s){
    const direction=facing==='west'?-1:1,vibration=moving?Math.sin(now/28+phase*9)*s*.025:0,spin=moving?now/15:0,rear=-s*.92,front=s*.98,wheelY=s*.5,wheelRadius=s*.5;
    ctx.fillStyle='rgba(0,0,0,.34)';ctx.beginPath();ctx.ellipse(x,y+s*.9,s*1.72,s*.25,0,0,Math.PI*2);ctx.fill();ctx.save();ctx.translate(0,vibration);
    for(const center of [rear,front]){ctx.fillStyle='#141716';ctx.beginPath();ctx.arc(x+center,y+wheelY,wheelRadius,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#8c9898';ctx.lineWidth=Math.max(1.5,s*.09);ctx.beginPath();ctx.arc(x+center,y+wheelY,wheelRadius*.62,0,Math.PI*2);ctx.stroke();if(moving){ctx.strokeStyle='rgba(210,220,218,.55)';for(let spoke=0;spoke<3;spoke++){const a=spin+spoke*Math.PI*2/3;ctx.beginPath();ctx.moveTo(x+center,y+wheelY);ctx.lineTo(x+center+Math.cos(a)*wheelRadius*.58,y+wheelY+Math.sin(a)*wheelRadius*.58);ctx.stroke();}}}
    ctx.fillStyle='#22282b';ctx.beginPath();ctx.moveTo(x+rear,y+wheelY);ctx.lineTo(x-s*.42,y-s*.05);ctx.lineTo(x+s*.35,y+s*.04);ctx.lineTo(x+front,y+wheelY);ctx.lineTo(x+s*.52,y-s*.35);ctx.lineTo(x-s*.12,y-s*.34);ctx.closePath();ctx.fill();ctx.fillStyle='#51616a';ctx.beginPath();ctx.ellipse(x+s*.16,y-s*.36,s*.48,s*.27,-.12,0,Math.PI*2);ctx.fill();ctx.fillStyle='#181a1a';ctx.fillRect(x-s*.55,y-s*.46,s*.7,s*.16);ctx.fillStyle='#aab6b5';ctx.fillRect(x-s*.42,y+s*.18,s*.78,s*.16);ctx.fillStyle='#fff0aa';ctx.beginPath();ctx.arc(x+front+s*.03,y-s*.15,s*.15,0,Math.PI*2);ctx.fill();
    ctx.save();ctx.translate(x,y+vibration);ctx.rotate(.24*direction);ctx.fillStyle=shirt;ctx.fillRect(-s*.34,-s*1.12,s*.72,s*.68);ctx.strokeStyle='#292421';ctx.lineWidth=Math.max(2,s*.18);ctx.beginPath();ctx.moveTo(-s*.18,-s*.48);ctx.lineTo(-s*.54,s*.12);ctx.moveTo(s*.12,-s*.46);ctx.lineTo(s*.35,s*.16);ctx.stroke();ctx.strokeStyle=skin;ctx.lineWidth=Math.max(2,s*.13);ctx.beginPath();ctx.moveTo(s*.27,-s*.88);ctx.lineTo(s*.72*direction,-s*.4);ctx.stroke();ctx.fillStyle=skin;ctx.beginPath();ctx.arc(-s*.02*direction,-s*1.34,s*.32,0,Math.PI*2);ctx.fill();ctx.restore();ctx.restore();
    if(moving){ctx.fillStyle='rgba(205,214,214,.25)';for(let i=0;i<2;i++){const drift=(now/13+i*s*.7)%(s*1.35);ctx.beginPath();ctx.arc(x-direction*(s*1.05+drift),y+s*(.18-i*.1),s*(.12+i*.04),0,Math.PI*2);ctx.fill();}}
  }
  function drawPersonShape(x,y,skin,shirt,moving,now,phase,mode='walk',facing='south'){
    const s=Math.max(5,state.scale*.46),cycle=Math.sin(now/105+phase*6);
    if(mode==='bike'){drawBikeRider(x,y,skin,shirt,moving,now,phase,facing,s);return;}
    if(mode==='dirtBike'){drawDirtBikeRider(x,y,skin,shirt,moving,now,phase,facing,s);return;}
    if(mode==='motorcycle'){drawMotorcycleRider(x,y,skin,shirt,moving,now,phase,facing,s);return;}
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

  function drawChestsAndLoot(view,now){
    for(const chest of state.chests.values()){if(!pointVisible(chest.position,view)||chest.isOpened||chest.expiresAtUtc&&Date.parse(chest.expiresAtUtc)<=Date.now())continue;if(state.dungeon&&!revealedAt(chest.position.x,chest.position.y))continue;const p=toScreen(chest.position),s=Math.max(7,state.scale*.5);ctx.fillStyle='#9a5f24';ctx.strokeStyle='#f2c75c';ctx.lineWidth=2;ctx.fillRect(p.x-s,p.y-s*.7,s*2,s*1.25);ctx.strokeRect(p.x-s,p.y-s*.7,s*2,s*1.25);ctx.fillStyle='#f2c75c';ctx.fillRect(p.x-2,p.y-s*.2,4,5);if(!state.dungeon&&!state.seenChests.has(chest.id)){state.seenChests.add(chest.id);send({type:'chestSeen',chestId:chest.id});}}
    for(const loot of state.loot.values()){if(!pointVisible(loot.position,view))continue;const p=toScreen(loot.position);ctx.fillStyle='#b6b0a2';ctx.beginPath();ctx.moveTo(p.x,p.y-15);ctx.lineTo(p.x+9,p.y);ctx.lineTo(p.x-9,p.y);ctx.closePath();ctx.fill();ctx.strokeStyle='#333';ctx.stroke();ctx.fillStyle='#ffd257';ctx.beginPath();ctx.arc(p.x+8,p.y-2+Math.sin(now/180)*2,4,0,Math.PI*2);ctx.fill();}
  }

  const weaponTypes=new Set(['fist','rock','slingshot','crossbow','pistol','rifle']);
  function renderInventory(inventory){
    ui.inventory.replaceChildren();
    const items=[{itemType:'fist',quantity:1},...(inventory?.items||[]).filter(item=>item.itemType!=='fist')],me=state.players.get(state.playerId);
    for(const item of items){
      const chip=document.createElement('span'),isWeapon=weaponTypes.has(item.itemType),label=title(item.itemType);
      chip.className=`inventory-item${isWeapon?' weapon-item':''}`;
      chip.textContent=item.itemType==='fist'?'✊ Fist · unlimited':`${label} ×${item.quantity}`;
      if(isWeapon){chip.draggable=true;chip.title=`Drag ${label} to the weapon slot`;chip.dataset.weapon=item.itemType;chip.addEventListener('dragstart',event=>{event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('text/weapon',item.itemType);event.dataTransfer.setData('text/plain',item.itemType);});}
      const wearable=item.itemType==='hat'||item.itemType==='magicHikingShoes'||item.itemType==='magicRunningShoes';
      if(item.itemType==='food'||item.itemType==='water'||item.itemType==='gallonOfGas'||wearable){const button=document.createElement('button');button.type='button';button.textContent=chip.textContent;chip.textContent='';if(wearable){const slot=item.itemType==='hat'?'hat':'shoes',equipped=item.itemType==='hat'?me?.hatOn:item.itemType==='magicHikingShoes'?me?.magicHikingShoesOn:me?.magicRunningShoesOn;button.title=equipped?'Unequip':'Equip';button.classList.toggle('equipped',!!equipped);button.addEventListener('click',()=>send({type:'setEquipment',slot,itemType:equipped?null:item.itemType}));}else{button.title=item.itemType==='gallonOfGas'?'Add to selected vehicle':'Consume';button.addEventListener('click',()=>send({type:'consumeItem',itemType:item.itemType}));}chip.append(button);}
      ui.inventory.append(chip);
    }
  }
  function setEquipmentSlot(button,label,item){button.querySelector('span').textContent=label;button.querySelector('em').textContent=item?title(item):'Empty';button.disabled=!item;button.classList.toggle('equipped',!!item);}
  function renderEquipment(me){if(!me)return;const shoes=me.magicHikingShoesOn?'magicHikingShoes':me.magicRunningShoesOn?'magicRunningShoes':null;setEquipmentSlot(ui.equipmentHat,'Hat',me.hatOn?'hat':null);setEquipmentSlot(ui.equipmentShoes,'Shoes',shoes);setEquipmentSlot(ui.equipmentPants,'Pants',null);setEquipmentSlot(ui.equipmentShirt,'Shirt',null);setEquipmentSlot(ui.equipmentGloves,'Gloves',null);ui.equipmentWeapon.querySelector('em').textContent=title(me.equippedWeapon||'fist');ui.equipmentWeapon.classList.toggle('equipped',true);ui.equipmentWeapon.title='Drop a backpack weapon here. Click to return to fists.';}
  function openTrade(quote){state.tradeQuote=quote;ui.tradeTitle.textContent=quote.merchantName;ui.tradeFriend.textContent=`Friend / foe: ${quote.friendRating.toFixed(1)} · better friendships lower prices`;ui.tradeOffers.replaceChildren();for(const offer of quote.offers){const row=document.createElement('label');row.className='trade-offer';const name=document.createElement('span');name.textContent=title(offer.itemType);const price=document.createElement('strong');price.textContent=`$${(offer.unitPriceCents/100).toFixed(2)} · ${offer.quantity} available`;const input=document.createElement('input');input.type='number';input.min='0';input.max=String(offer.quantity);input.value='0';input.dataset.item=offer.itemType;row.append(name,price,input);ui.tradeOffers.append(row);}ui.tradeWindow.hidden=false;}
  function receiveCombat(combat){state.projectiles.push({...combat,started:performance.now()});if(state.actors.has(combat.targetId)){if(combat.targetDied)state.actors.delete(combat.targetId);else if(combat.targetHealth!=null)state.actors.set(combat.targetId,{...state.actors.get(combat.targetId),healthHearts:combat.targetHealth});}else if(combat.targetHealth!=null&&state.players.has(combat.targetId))state.players.set(combat.targetId,{...state.players.get(combat.targetId),healthHearts:combat.targetHealth});if(combat.attackerId===state.playerId||combat.targetId===state.playerId)showToast(combat.message);if(combat.attackerId===state.playerId)send({type:'requestPrivateState'});}
  function drawProjectiles(now){state.projectiles=state.projectiles.filter(p=>now-p.started<700);for(const p of state.projectiles){const duration=p.weapon==='fist'?260:p.weapon==='rifle'||p.weapon==='pistol'?330:550,t=Math.min(1,(now-p.started)/duration),a=toScreen(p.start),target=toScreen(p.end),miss=!p.hit,missOffset=miss?(hash(`${p.attackerId}:${p.started}`)-.5)*50:0,b={x:target.x+missOffset,y:target.y-Math.abs(missOffset)*.25},x=a.x+(b.x-a.x)*t,y=a.y+(b.y-a.y)*t-Math.sin(Math.PI*t)*(p.weapon==='rock'?25:p.weapon==='crossbow'?5:8);if(p.weapon==='fist'){ctx.strokeStyle='#ffe0a2';ctx.lineWidth=3;ctx.beginPath();ctx.arc(b.x,b.y-8,5+10*t,0,Math.PI*2);ctx.stroke();}else if(p.weapon==='crossbow'){const angle=Math.atan2(b.y-a.y,b.x-a.x);ctx.save();ctx.translate(x,y);ctx.rotate(angle);ctx.strokeStyle='#d9c28a';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(-10,0);ctx.lineTo(8,0);ctx.stroke();ctx.fillStyle='#c7d5c5';ctx.beginPath();ctx.moveTo(8,0);ctx.lineTo(3,-3);ctx.lineTo(3,3);ctx.closePath();ctx.fill();ctx.restore();}else if(p.weapon==='pistol'||p.weapon==='rifle'){ctx.strokeStyle=p.weapon==='rifle'?'#ffe38b':'#ffd05e';ctx.lineWidth=p.weapon==='rifle'?3:2;ctx.beginPath();ctx.moveTo(x-(b.x-a.x)*.045,y-(b.y-a.y)*.045);ctx.lineTo(x,y);ctx.stroke();ctx.fillStyle='#fff4b0';ctx.beginPath();ctx.arc(x,y,2.2,0,Math.PI*2);ctx.fill();}else{ctx.fillStyle=p.weapon==='slingshot'?'#a8a8a8':'#6e5945';ctx.beginPath();ctx.arc(x,y,p.weapon==='slingshot'?3:5,0,Math.PI*2);ctx.fill();}if(p.attackerId===state.playerId){const me=state.players.get(state.playerId);if(me)state.movingUntil.set(me.id,now+250);}}}

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
  function drawAtmosphere(me,detail,now){const light=daylight();const moonBoost=!state.weather?.isDay?Math.max(0,state.weather?.moonIllumination||0)*.18:0;const darkness=Math.max(0,.7-light*.7-moonBoost);if(darkness>.02){lightCtx.clearRect(0,0,innerWidth,innerHeight);lightCtx.fillStyle=`rgba(8,18,29,${darkness})`;lightCtx.fillRect(0,0,innerWidth,innerHeight);if(me&&(me.lanternOn||me.flashlightOn)){const p=toScreen(me.position);lightCtx.globalCompositeOperation='destination-out';if(me.lanternOn){const radius=detail===0?65:145,g=lightCtx.createRadialGradient(p.x,p.y,15,p.x,p.y,radius);g.addColorStop(0,'rgba(0,0,0,.94)');g.addColorStop(.65,'rgba(0,0,0,.78)');g.addColorStop(1,'rgba(0,0,0,0)');lightCtx.fillStyle=g;lightCtx.beginPath();lightCtx.arc(p.x,p.y,radius,0,Math.PI*2);lightCtx.fill();}if(me.flashlightOn){const facing=state.facings.get(me.id)||'south',vectors={north:[0,-1],south:[0,1],east:[1,0],west:[-1,0]},v=vectors[facing],length=detail===0?125:245,width=length*.43;lightCtx.fillStyle='rgba(0,0,0,.88)';lightCtx.beginPath();lightCtx.moveTo(p.x,p.y);lightCtx.lineTo(p.x+v[0]*length-v[1]*width,p.y+v[1]*length+v[0]*width);lightCtx.lineTo(p.x+v[0]*length+v[1]*width,p.y+v[1]*length-v[0]*width);lightCtx.closePath();lightCtx.fill();}lightCtx.globalCompositeOperation='source-over';}ctx.drawImage(lightCanvas,0,0);}
    const code=state.weather?.weatherCode??0,rain=(code>=51&&code<=99)&&code<71||code>=80,snow=code>=71&&code<=77;if((rain||snow)&&detail>0){const count=detail===2?90:35;ctx.strokeStyle=rain?'rgba(169,211,232,.55)':'rgba(245,250,255,.8)';ctx.lineWidth=rain?1:2;for(let i=0;i<count;i++){const seed=hash(`${i}:${Math.floor(now/120)}`),x=(seed*innerWidth+(now*.28*(i%3+1)))%innerWidth,y=(hash(`${i}:y`)*innerHeight+now*.45)%innerHeight;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x-(rain?4:1),y+(rain?11:2));ctx.stroke();}}
  }
  function drawLaser(me){if(!me?.laserOn)return;const facing=state.facings.get(me.id)||'south',vectors={north:{x:0,y:1},south:{x:0,y:-1},east:{x:1,y:0},west:{x:-1,y:0}},direction=vectors[facing];const view=viewBounds(),corners=[{x:view.minX,y:view.minY},{x:view.maxX,y:view.minY},{x:view.maxX,y:view.maxY},{x:view.minX,y:view.maxY}],maximum=Math.max(...corners.map(point=>Math.hypot(point.x-me.position.x,point.y-me.position.y)))+20,distance=laserCollisionDistance(me.position,direction,maximum,me.id),end={x:me.position.x+direction.x*distance,y:me.position.y+direction.y*distance},a=toScreen(me.position),b=toScreen(end);ctx.save();ctx.lineCap='round';ctx.shadowColor='#ff2020';ctx.shadowBlur=12;ctx.strokeStyle='rgba(255,40,40,.35)';ctx.lineWidth=7;ctx.beginPath();ctx.moveTo(a.x,a.y-state.scale*.55);ctx.lineTo(b.x,b.y-state.scale*.15);ctx.stroke();ctx.shadowBlur=5;ctx.strokeStyle='#ff5b4d';ctx.lineWidth=2;ctx.stroke();if(distance<maximum-.01){ctx.fillStyle='#fff0d0';ctx.beginPath();ctx.arc(b.x,b.y-state.scale*.15,4,0,Math.PI*2);ctx.fill();}ctx.restore();}
  function laserCollisionDistance(origin,direction,maximum,ownerId){let nearest=maximum;const segment=(a,b)=>{const value=raySegment(origin,direction,a,b);if(value>.65&&value<nearest)nearest=value;};const circle=(center,radius)=>{const ox=center.x-origin.x,oy=center.y-origin.y,t=ox*direction.x+oy*direction.y;if(t<=.65||t>=nearest)return;const perpendicular=Math.abs(ox*direction.y-oy*direction.x);if(perpendicular<=radius){const offset=Math.sqrt(Math.max(0,radius*radius-perpendicular*perpendicular));nearest=Math.max(.65,t-offset);}};
    if(state.dungeon){for(const wall of state.dungeon.walls||[]){if(wall.doorStart>=0){if(Math.abs(wall.x1-wall.x2)<.01){segment({x:wall.x1,y:wall.y1},{x:wall.x1,y:wall.doorStart});segment({x:wall.x1,y:wall.doorEnd},{x:wall.x2,y:wall.y2});}else{segment({x:wall.x1,y:wall.y1},{x:wall.doorStart,y:wall.y1});segment({x:wall.doorEnd,y:wall.y1},{x:wall.x2,y:wall.y2});}}else segment({x:wall.x1,y:wall.y1},{x:wall.x2,y:wall.y2});}for(const item of state.dungeon.furnishings||[])circle(item.position,item.properties?.objectType==='table'?1.2:.75);for(const actor of state.dungeon.actors||[])circle(actor.position,.4);}else{for(const building of state.lists.building||[]){for(let i=0;i<(building.geometry?.length||0)-1;i++)segment(building.geometry[i],building.geometry[i+1]);}for(const fence of state.lists.fence||[]){for(let i=0;i<(fence.geometry?.length||0)-1;i++)segment(fence.geometry[i],fence.geometry[i+1]);}for(const kind of ['tree','bush','vehicle'])for(const item of state.lists[kind]||[])circle(item.position,kind==='tree'?prop(item,'collisionRadius',.85):kind==='vehicle'?2.4:.45);for(const actor of state.actors.values())circle(actor.position,.4);for(const player of state.players.values())if(player.id!==ownerId&&(player.locationId||'outdoor')==='outdoor')circle(player.position,.4);}return nearest;}
  function raySegment(origin,direction,a,b){const sx=b.x-a.x,sy=b.y-a.y,cross=direction.x*sy-direction.y*sx;if(Math.abs(cross)<1e-8)return Infinity;const qx=a.x-origin.x,qy=a.y-origin.y,t=(qx*sy-qy*sx)/cross,u=(qx*direction.y-qy*direction.x)/cross;return t>=0&&u>=0&&u<=1?t:Infinity;}
  function daylight(){if(!state.weather?.sunriseUtc||!state.weather?.sunsetUtc)return state.weather?.isDay?1:.12;const now=Date.now(),rise=Date.parse(state.weather.sunriseUtc),set=Date.parse(state.weather.sunsetUtc),twilight=45*60000;if(now<rise-twilight||now>set+twilight)return .08;if(now<rise)return .08+.92*(now-(rise-twilight))/twilight;if(now>set)return 1-.92*(now-set)/twilight;return 1;}

  function worldToGps(position){const region=position.region,lat0=(region.latitudeBand+.5)*Math.PI/180,lon0=(region.longitudeBand+.5)*Math.PI/180,R=6378137,e2=6.69437999014e-3,sin=Math.sin(lat0),den=Math.sqrt(1-e2*sin*sin),mLon=R*Math.cos(lat0)/den,mLat=R*(1-e2)/Math.pow(1-e2*sin*sin,1.5);return{latitude:(lat0+position.y/mLat)*180/Math.PI,longitude:(lon0+position.x/mLon)*180/Math.PI};}
  function gpsToWorld(latitude,longitude,region){const lat0=(region.latitudeBand+.5)*Math.PI/180,lon0=(region.longitudeBand+.5)*Math.PI/180,R=6378137,e2=6.69437999014e-3,sin=Math.sin(lat0),den=Math.sqrt(1-e2*sin*sin),mLon=R*Math.cos(lat0)/den,mLat=R*(1-e2)/Math.pow(1-e2*sin*sin,1.5);return{x:(longitude*Math.PI/180-lon0)*mLon,y:(latitude*Math.PI/180-lat0)*mLat};}
  function gpsText(position){if(!position)return'—';const g=worldToGps(position);return`${g.latitude.toFixed(6)}, ${g.longitude.toFixed(6)}`;}
  function updateTelemetry(me){if(!me)return;ui.playerGps.textContent=me.locationId==='outdoor'?gpsText(me.position):'Inside building';updateBaseCompass(me);ui.destinationGps.textContent=state.target?(me.locationId==='outdoor'?gpsText({...state.target,region:me.position.region}):`${state.target.x.toFixed(1)} m, ${state.target.y.toFixed(1)} m`):'—';ui.terrain.textContent=me.locationId==='outdoor'?title(me.terrain):'Dungeon floor';ui.elevation.textContent=`${me.position.z.toFixed(1)} m / ${(me.position.z*3.28084).toFixed(0)} ft`;ui.speed.textContent=`${(me.speedMetersPerSecond*2.23694).toFixed(1)} mph`;const distance=state.target?Math.hypot(state.target.x-me.position.x,state.target.y-me.position.y):null;ui.distance.textContent=distance===null?'—':distance>=1000?`${(distance/1000).toFixed(2)} km`:`${distance.toFixed(1)} m`;ui.camera.textContent=`${Math.hypot(state.camera.x-me.position.x,state.camera.y-me.position.y).toFixed(1)} m · ${lod()===2?'full':lod()===1?'medium':'light'} detail`;const w=state.weather;ui.weather.textContent=w?.isAvailable?`${w.condition} · ${w.temperatureCelsius.toFixed(1)} °C / ${(w.temperatureCelsius*9/5+32).toFixed(0)} °F`:'Unavailable';ui.sun.textContent=w?.sunriseUtc?`${clock(w.sunriseUtc)} / ${clock(w.sunsetUtc)}`:'—';ui.moon.textContent=w?.moonPhase?`${w.moonPhase} · ${Math.round(w.moonIllumination*100)}%`:'—';const hearts=Math.max(0,me.healthHearts??10),full=Math.floor(hearts),empty=Math.max(0,10-Math.ceil(hearts));ui.hearts.textContent=`${'♥'.repeat(full)}${hearts%1?'◒':''}${'♡'.repeat(empty)}  ${hearts.toFixed(2)}/10`;const stamina=Math.max(0,me.stamina??10),staminaFull=Math.floor(stamina),staminaEmpty=Math.max(0,10-Math.ceil(stamina));ui.stamina.textContent=`${'◆'.repeat(staminaFull)}${stamina%1?'◇':''}${'·'.repeat(staminaEmpty)}  ${stamina.toFixed(2)}/10`;const water=Math.max(0,me.water??10),waterFull=Math.floor(water),waterEmpty=Math.max(0,10-Math.ceil(water));ui.water.textContent=`${'●'.repeat(waterFull)}${water%1?'◐':''}${'·'.repeat(waterEmpty)}  ${water.toFixed(2)}/10`;ui.wallet.textContent=`$${((me.walletCents||0)/100).toFixed(2)}`;const effects=[];if(water<=0)effects.push('Dehydrated: ½ speed');if(me.foodProtectedUntilUtc&&Date.parse(me.foodProtectedUntilUtc)>Date.now())effects.push(`Fed ${countdown(me.foodProtectedUntilUtc)}`);if(me.waterProtectedUntilUtc&&Date.parse(me.waterProtectedUntilUtc)>Date.now())effects.push(`Hydrated ${countdown(me.waterProtectedUntilUtc)}`);if(me.magicHikingShoesOn)effects.push('Magic hiking shoes · 2× walk/run · ½ stamina drain');if(me.magicRunningShoesOn)effects.push('Magic running shoes · 3× walk/run · ½ stamina off roads/sidewalks');if(me.hatOn)effects.push('Hat · ½ water drain');if(me.godMode)effects.push('God Mode · 5× speed');ui.effects.textContent=effects.join(' · ')||'None';}
  function updateBaseCompass(me){const base=state.privateState?.base;if(!base){ui.baseCompass.textContent='Assigning…';return;}if(me.locationId?.startsWith('home:')){ui.baseCompass.textContent='⌂ Home';return;}const dx=base.position.x-me.position.x,dy=base.position.y-me.position.y,d=Math.hypot(dx,dy),angle=(Math.atan2(dx,dy)*180/Math.PI+360)%360,dirs=['N','NE','E','SE','S','SW','W','NW'];ui.baseCompass.textContent=`${dirs[Math.round(angle/45)%8]} · ${d>=1000?(d/1000).toFixed(2)+' km':d.toFixed(0)+' m'}`;}
  const countdown=value=>{const s=Math.max(0,Math.ceil((Date.parse(value)-Date.now())/1000));return`${Math.floor(s/60)}:${String(s%60).padStart(2,'0')}`;};
  function syncLightControls(me){if(!me)return;ui.gas.textContent=me.travelMode==='dirtBike'?`${(me.dirtBikeGasGallons||0).toFixed(2)} / 2.00 gal · 50 mpg`:me.travelMode==='motorcycle'?`${(me.motorcycleGasGallons||0).toFixed(2)} / 4.00 gal · 45 mpg`:'—';ui.flashlight.checked=!!me.flashlightOn;ui.lantern.checked=!!me.lanternOn;ui.laser.checked=!!me.laserOn;}
  const title=value=>value==='gallonOfGas'?'Gallon of gas':String(value||'').replace(/([A-Z])/g,' $1').trim().replace(/^./,c=>c.toUpperCase());
  const clock=value=>new Date(value).toLocaleTimeString([],{hour:'numeric',minute:'2-digit'});
  function updateMode(mode){document.querySelectorAll('[data-mode]').forEach(button=>button.classList.toggle('active',button.dataset.mode.toLowerCase()===String(mode||'walk').toLowerCase()));}

  function movementLoop(time){const me=state.players.get(state.playerId);if(me&&time-state.lastInput>28){if(state.pendingDoor&&Math.hypot(state.pendingDoor.position.x-me.position.x,state.pendingDoor.position.y-me.position.y)<5.5){send({type:'enterDungeon',doorId:state.pendingDoor.id});state.pendingDoor=null;stopTravel();}if(state.pendingChest&&Math.hypot(state.pendingChest.position.x-me.position.x,state.pendingChest.position.y-me.position.y)<3.7){send({type:'openChest',chestId:state.pendingChest.id});state.pendingChest=null;stopTravel();}let dx=0,dy=0;if(state.keys.has('w')||state.keys.has('arrowup'))dy+=1;if(state.keys.has('s')||state.keys.has('arrowdown'))dy-=1;if(state.keys.has('a')||state.keys.has('arrowleft'))dx-=1;if(state.keys.has('d')||state.keys.has('arrowright'))dx+=1;if(dx||dy){state.target=null;state.path=[];send({type:'moveRequest',x:dx,y:dy,sequence:++state.pathSequence});state.lastInput=time;}else if(state.target){while(state.path.length&&Math.hypot(state.path[0].x-me.position.x,state.path[0].y-me.position.y)<.45)state.path.shift();const waypoint=state.path[0]||state.target,tx=waypoint.x-me.position.x,ty=waypoint.y-me.position.y,d=Math.hypot(tx,ty);if(d<.4&&!state.path.length){state.target=null;}else if(d>.01){send({type:'moveRequest',x:tx/d,y:ty/d,sequence:state.pathSequence});state.lastInput=time;}}}requestAnimationFrame(movementLoop);}

  function actorsHere(){return state.dungeon?(state.dungeon.actors||[]):[...state.actors.values()];}
  function combatTargets(){const me=state.players.get(state.playerId),location=me?.locationId||'outdoor';return [...actorsHere(),...[...state.players.values()].filter(player=>player.id!==state.playerId&&(player.locationId||'outdoor')===location)];}
  function nearPoint(collection,world,meters=1.5){return collection.filter(item=>Math.hypot(item.position.x-world.x,item.position.y-world.y)<=meters).sort((a,b)=>Math.hypot(a.position.x-world.x,a.position.y-world.y)-Math.hypot(b.position.x-world.x,b.position.y-world.y))[0];}
  function navigateTo(target){state.target={x:target.x,y:target.y};state.path=[];state.pathSequence++;send({type:'pathRequest',x:target.x,y:target.y,sequence:state.pathSequence});}

  canvas.addEventListener('mousedown',event=>{if(event.button===0||event.button===2){state.pointer={down:true,dragged:false,button:event.button,startX:event.clientX,startY:event.clientY,x:event.clientX,y:event.clientY};ui.actionMenu.hidden=true;}});
  addEventListener('mousemove',event=>{const world=toWorld({x:event.clientX,y:event.clientY});const actor=nearPoint(combatTargets(),world,Math.max(1,14/state.scale));if(actor){const isPlayer=state.players.has(actor.id),rating=state.relationships.get(actor.id)??actor.friendRating??0;ui.tooltip.innerHTML=`<strong>${actor.name}</strong><br>Health: ${(actor.healthHearts??5).toFixed(1)} / ${(actor.maximumHealthHearts??5).toFixed(1)} ♥${isPlayer?'':`<br>Friend / foe: ${rating.toFixed(1)}${actor.isMerchant?'<br>Merchant':''}`}`;ui.tooltip.style.left=`${event.clientX+14}px`;ui.tooltip.style.top=`${event.clientY+14}px`;ui.tooltip.hidden=false;}else ui.tooltip.hidden=true;if(!state.pointer.down)return;const dx=event.clientX-state.pointer.x,dy=event.clientY-state.pointer.y;if(Math.hypot(event.clientX-state.pointer.startX,event.clientY-state.pointer.startY)>3)state.pointer.dragged=true;if(state.pointer.dragged){state.camera.x-=dx/state.scale;state.camera.y+=dy/(state.scale*state.pitch);state.camera.x+=dy/state.scale*state.shear/state.pitch;state.follow=false;}state.pointer.x=event.clientX;state.pointer.y=event.clientY;});
  addEventListener('mouseup',event=>{if(!state.pointer.down||event.button!==state.pointer.button)return;if(state.pointer.dragged&&event.button===0){state.suppressClick=true;setTimeout(()=>state.suppressClick=false,250);}if(!state.pointer.dragged&&event.button===2){state.actionPoint=toWorld({x:event.clientX,y:event.clientY});state.actionActor=nearPoint(actorsHere(),state.actionPoint,Math.max(1,16/state.scale))||null;state.actionDoor=state.dungeon?null:nearPoint(state.lists.door||[],state.actionPoint,Math.max(1.3,18/state.scale))||null;updateActionMenu();ui.actionMenu.style.left=`${Math.min(event.clientX,innerWidth-255)}px`;ui.actionMenu.style.top=`${Math.min(event.clientY,innerHeight-280)}px`;ui.actionMenu.hidden=false;}state.pointer.down=false;state.pointer.button=null;});
  canvas.addEventListener('contextmenu',event=>event.preventDefault());
  canvas.addEventListener('click',event=>{if(event.button!==0)return;if(state.suppressClick){state.suppressClick=false;return;}ui.actionMenu.hidden=true;const target=toWorld({x:event.clientX,y:event.clientY});const me=state.players.get(state.playerId);if(state.dungeon&&Math.hypot(target.x-state.dungeon.exit.x,target.y-state.dungeon.exit.y)<1.6){send({type:'exitDungeon'});return;}const furnishing=state.dungeon?nearPoint(state.dungeon.furnishings||[],target,1.4):null;if(furnishing?.properties?.objectType==='bed'){if(me&&Math.hypot(furnishing.position.x-me.position.x,furnishing.position.y-me.position.y)<=4)send({type:'restAtBed',bedId:furnishing.id});else{showToast('Move closer to your bed to rest.');navigateTo(furnishing.position);}return;}const combatTarget=nearPoint(combatTargets(),target,Math.max(1,16/state.scale));if(combatTarget){state.path=[];state.target=null;send({type:'attack',targetId:combatTarget.id,weapon:me?.equippedWeapon||'fist'});return;}const chest=nearPoint([...state.chests.values()],target,1.4);if(chest){state.pendingChest=chest;navigateTo(chest.position);return;}const loot=nearPoint([...state.loot.values()],target,1.4);if(loot){if(me&&Math.hypot(loot.position.x-me.position.x,loot.position.y-me.position.y)<3.8)send({type:'collectLoot',lootId:loot.id});else{showToast('Move within 4 meters to collect this treasure.');navigateTo(loot.position);}return;}if(!state.dungeon){const door=nearPoint([...(state.lists.door||[])],target,1.4);if(door){state.pendingDoor=door;const angle=prop(door,'facingDegrees',0)*Math.PI/180;navigateTo({x:door.position.x+Math.cos(angle)*3.2,y:door.position.y+Math.sin(angle)*3.2});return;}}navigateTo(target);});
  canvas.addEventListener('wheel',event=>{event.preventDefault();const anchor=toWorld({x:event.clientX,y:event.clientY});state.scale=Math.max(1.2,Math.min(40,state.scale*Math.exp(-event.deltaY*.001)));const after=toWorld({x:event.clientX,y:event.clientY});state.camera.x+=anchor.x-after.x;state.camera.y+=anchor.y-after.y;state.follow=false;},{passive:false});
  addEventListener('keydown',event=>{if(event.target.matches('input,textarea'))return;const key=event.key.toLowerCase();if(['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright'].includes(key)){event.preventDefault();state.keys.add(key);state.follow=true;}});
  addEventListener('keyup',event=>state.keys.delete(event.key.toLowerCase()));
  ui.center.addEventListener('click',()=>state.follow=true);
  document.querySelectorAll('[data-mode]').forEach(button=>button.addEventListener('click',()=>send({type:'setTravelMode',mode:button.dataset.mode})));
  function updateActionMenu(){const canTeleport=ui.god.checked&&state.actionPoint&&!state.dungeon,actor=state.actionActor,door=state.actionDoor,me=state.players.get(state.playerId),currentBase=state.privateState?.base?.buildingId,buildingId=door?.properties?.buildingId,price=me?.godMode?(state.privateState?.godModeBasePurchasePriceCents??1):(state.privateState?.basePurchasePriceCents??35000000),alreadyOwned=!!door&&buildingId===currentBase,affordable=(me?.walletCents??0)>=price;ui.teleport.hidden=!canTeleport;ui.trade.hidden=!(actor?.isMerchant);ui.purchaseBase.hidden=!door;ui.purchaseBase.disabled=!door||alreadyOwned||!affordable;ui.purchaseBase.textContent=alreadyOwned?'Current base':`Purchase as base — $${(price/100).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}`;ui.purchaseBase.title=alreadyOwned?'This is already your base.':affordable?'Purchase this building as your account base.':`You need $${(price/100).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}.`;ui.noActions.hidden=!!(canTeleport||actor?.isMerchant||door);}
  ui.god.addEventListener('pointerdown',()=>{const player=state.players.get(state.playerId);if(player)state.godTogglePending=!player.godMode;});
  ui.god.addEventListener('change',()=>{state.godTogglePending=ui.god.checked;send({type:'setGodMode',enabled:ui.god.checked});ui.gpsTeleport.hidden=!ui.god.checked;ui.rebuild.disabled=!ui.god.checked;updateActionMenu();clearTimeout(state.godToggleTimer);state.godToggleTimer=setTimeout(()=>{state.godTogglePending=null;syncGodControls(state.players.get(state.playerId));},3000);});
  ui.gpsTeleport.addEventListener('click',()=>{const me=state.players.get(state.playerId);if(!me?.godMode){showToast('Enable God Mode before using GPS teleport.');return;}if(!isSecureContext||!navigator.geolocation){showToast('GPS requires a secure HTTPS page or localhost.');return;}ui.gpsTeleport.disabled=true;ui.gpsTeleport.classList.add('locating');const finish=()=>{ui.gpsTeleport.disabled=false;ui.gpsTeleport.classList.remove('locating');};showToast('Requesting your device location…');navigator.geolocation.getCurrentPosition(position=>{finish();const latitude=position.coords.latitude,longitude=position.coords.longitude,region=me.position.region;if(Math.floor(latitude)!==region.latitudeBand||Math.floor(longitude)!==region.longitudeBand){showToast('Your GPS location is beyond this reality’s current geographic region.');return;}const target=gpsToWorld(latitude,longitude,region);showToast(`GPS found (about ${Math.max(1,Math.round(position.coords.accuracy))} m accuracy). Loading a safe landing point…`);if(me.locationId==='outdoor')send({type:'teleport',x:target.x,y:target.y,godMode:true});else{state.pendingGpsTeleport=target;showToast('GPS found. Leaving the building before teleporting…');send({type:'exitDungeon'});}},error=>{finish();const message=error.code===1?'Location permission was denied.':error.code===2?'Your device could not determine its location.':'The GPS request timed out.';showToast(message);},{enableHighAccuracy:true,timeout:15000,maximumAge:300000});});
  const sendLights=()=>send({type:'setLights',flashlightOn:ui.flashlight.checked,lanternOn:ui.lantern.checked,laserOn:ui.laser.checked});ui.flashlight.addEventListener('change',sendLights);ui.lantern.addEventListener('change',sendLights);ui.laser.addEventListener('change',sendLights);
  ui.equipmentHat.addEventListener('click',()=>send({type:'setEquipment',slot:'hat',itemType:null}));
  ui.equipmentShoes.addEventListener('click',()=>send({type:'setEquipment',slot:'shoes',itemType:null}));
  ui.equipmentWeapon.addEventListener('click',()=>send({type:'setEquipment',slot:'weapon',itemType:'fist'}));
  ui.equipmentWeapon.addEventListener('dragover',event=>{event.preventDefault();event.dataTransfer.dropEffect='move';ui.equipmentWeapon.classList.add('drag-ready');});
  ui.equipmentWeapon.addEventListener('dragleave',()=>ui.equipmentWeapon.classList.remove('drag-ready'));
  ui.equipmentWeapon.addEventListener('drop',event=>{event.preventDefault();ui.equipmentWeapon.classList.remove('drag-ready');const weapon=event.dataTransfer.getData('text/weapon')||event.dataTransfer.getData('text/plain');if(weaponTypes.has(weapon))send({type:'setEquipment',slot:'weapon',itemType:weapon});});
  ui.toggleInventory.addEventListener('click',()=>{const collapsed=ui.inventoryPanel.classList.toggle('collapsed');ui.toggleInventory.textContent=collapsed?'Show':'Hide';});
  ui.teleport.addEventListener('click',()=>{if(!ui.god.checked||!state.actionPoint)return;send({type:'teleport',x:state.actionPoint.x,y:state.actionPoint.y,godMode:true});ui.actionMenu.hidden=true;});
  ui.trade.addEventListener('click',()=>{if(state.actionActor)send({type:'requestTrade',merchantId:state.actionActor.id});ui.actionMenu.hidden=true;});
  ui.purchaseBase.addEventListener('click',()=>{if(state.actionDoor&&!ui.purchaseBase.disabled)send({type:'purchaseBase',doorId:state.actionDoor.id});});
  ui.tradeCancel.addEventListener('click',()=>ui.tradeWindow.hidden=true);
  ui.tradeConfirm.addEventListener('click',()=>{if(!state.tradeQuote)return;const purchases=[...ui.tradeOffers.querySelectorAll('input')].map(input=>({itemType:input.dataset.item,quantity:Number(input.value)||0})).filter(x=>x.quantity>0);send({type:'confirmTrade',merchantId:state.tradeQuote.merchantId,purchases});});
  ui.chatForm.addEventListener('submit',event=>{event.preventDefault();const message=ui.chatInput.value.trim();if(!message)return;send({type:'say',message});ui.chatInput.value='';ui.chatInput.focus();});
  ui.toggleChat.addEventListener('click',()=>{state.chatVisible=!state.chatVisible;ui.chatHistory.hidden=!state.chatVisible;ui.toggleChat.textContent=state.chatVisible?'Hide chat':'Show chat';if(state.chatVisible)renderChatHistory();});
  ui.rebuild.addEventListener('click',()=>{if(!ui.god.checked)return;if(confirm('Reset and rebuild the entire server reality? Generated regions, dungeons, chests, relationships, and player-created world changes will be reset. Accounts, characters, inventories, and base ownership will remain.'))send({type:'rebuildArea',godMode:true});});
  ui.accountForm.addEventListener('submit',async event=>{event.preventDefault();ui.accountError.textContent='';const username=ui.accountUsername.value.trim(),password=ui.accountPassword.value;try{const response=await fetch('/api/account/setup',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({username,password})});const body=await response.json();if(!response.ok)throw new Error(body.message||'Unable to create player.');ui.accountSetup.hidden=true;connect();}catch(error){ui.accountError.textContent=error.message;}});
  async function configureReality(latitude,longitude){ui.realitySetupError.textContent='Building the initial world…';const response=await fetch('/api/reality/setup',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({latitude,longitude})});const body=await response.json();if(!response.ok)throw new Error(body.message||'Unable to initialize this reality.');ui.realitySetup.hidden=true;ui.realitySetupError.textContent='';await bootstrap();}
  ui.realitySetupForm.addEventListener('submit',async event=>{event.preventDefault();try{await configureReality(Number(ui.realityLatitude.value),Number(ui.realityLongitude.value));}catch(error){ui.realitySetupError.textContent=error.message;}});
  ui.useServerGps.addEventListener('click',()=>{if(!isSecureContext||!navigator.geolocation){ui.realitySetupError.textContent='Browser location requires HTTPS or localhost. Enter coordinates below.';return;}ui.useServerGps.disabled=true;ui.realitySetupError.textContent='Requesting this device’s location…';navigator.geolocation.getCurrentPosition(async position=>{ui.useServerGps.disabled=false;ui.realityLatitude.value=position.coords.latitude.toFixed(6);ui.realityLongitude.value=position.coords.longitude.toFixed(6);try{await configureReality(position.coords.latitude,position.coords.longitude);}catch(error){ui.realitySetupError.textContent=error.message;}},error=>{ui.useServerGps.disabled=false;ui.realitySetupError.textContent=error.code===1?'Location permission was denied. Enter coordinates below.':'Location is unavailable. Enter coordinates below.';},{enableHighAccuracy:true,timeout:15000,maximumAge:300000});});
  ui.characterForm.addEventListener('submit',async event=>{event.preventDefault();const response=await fetch('/api/account/characters',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({name:ui.characterName.value.trim()})});if(response.ok){ui.characterName.value='';loadCharacters(true);}else showToast((await response.json()).message);});
  addEventListener('resize',resize);resize();if(matchMedia('(max-width:760px)').matches){ui.inventoryPanel.classList.add('collapsed');ui.toggleInventory.textContent='Show';}bootstrap();requestAnimationFrame(render);requestAnimationFrame(movementLoop);
})();
