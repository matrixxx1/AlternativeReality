(() => {
  'use strict';
  const $ = selector => document.querySelector(selector);
  const canvas = $('#world');
  let ctx = canvas.getContext('2d');
  const miniMapCanvas=$('#miniMap'),miniMapCtx=miniMapCanvas.getContext('2d');
  const lightCanvas=document.createElement('canvas'),lightCtx=lightCanvas.getContext('2d');
  const initialZoomScale=26,maximumZoomScale=96;
  const dinosaurWeapons=new Set(['trexBite','trexTail','brontosaurusTail','brontosaurusStomp','stegosaurusTail','raptorBite','giantStomp','gorillaSmash']);
  const ui = {
    connectionTask:$('#connectionTask'),connectionTaskMessage:$('#connectionTaskMessage'),connectionTaskElapsed:$('#connectionTaskElapsed'),status: $('#status'), dot: $('#connectionDot'), realityName: $('#realityName'), titlePanel:$('#titlePanel'), openHelpWindow:$('#openHelpWindowButton'), toast: $('#toast'), safeExit:$('#safeExitButton'), rightRail:$('#rightRail'), rightRailToggle:$('#rightRailToggle'),
    terrain: $('#terrainValue'), elevation: $('#elevationValue'), distance: $('#distanceValue'),
    performancePanel:$('#performancePanel'),performanceActivity:$('#performanceActivity'),performanceFps:$('#performanceFps'),performanceFrame:$('#performanceFrame'),performanceEntities:$('#performanceEntities'),performanceBlocks:$('#performanceBlocks'),performanceNetwork:$('#performanceNetwork'),performanceServer:$('#performanceServer'),performanceLoad:$('#performanceLoad'),
    hearts: $('#heartsValue'), stamina: $('#staminaValue'), water: $('#waterValue'), bodyHeat:$('#bodyHeatValue'), effects: $('#effectsValue'), miniMapPanel:$('#miniMapPanel'),miniMapTooltip:$('#miniMapTooltip'),miniMapTeleportHome:$('#miniMapTeleportHomeButton'), activeEventsPanel:$('#activeEventsPanel'),activeEventsList:$('#activeEventsList'),
    playerGps: $('#playerGpsValue'), destinationGps: $('#destinationGpsValue'), destinationGpsRow:$('#destinationGpsRow'),destinationDistanceRow:$('#destinationDistanceRow'), wanted:$('#wantedValue'), actionMenu: $('#actionMenu'),
    noActions: $('#noActions'), enterBuilding:$('#enterBuildingButton'), pickLock:$('#pickLockButton'),goUpstairs:$('#goUpstairsButton'), goDownstairs:$('#goDownstairsButton'), leaveDungeon:$('#leaveDungeonButton'), teleport: $('#teleportButton'), placeFlag:$('#placeFlagButton'), sprayPaintVehicle:$('#sprayPaintVehicleButton'),openPostalBox:$('#openPostalBoxButton'),postalWindow:$('#postalWindow'),postalItems:$('#postalItems'),postalClose:$('#postalClose'), chatForm: $('#chatForm'), chatInput: $('#chatInput'),
    chatMessages: $('#chatMessages'),
    center: $('#centerButton'), god: $('#godMode'), worldTask:$('#worldTask'), rebuild: $('#rebuildButton'), inventory: $('#inventoryItems'), backpackSummary:$('#backpackSummary'),weaponSlotCount:$('#weaponSlotCount'),questSlotCount:$('#questSlotCount'),otherSlotCount:$('#otherSlotCount'),inventoryPanel:$('.inventory-panel'), inventoryContent:$('#inventoryContent'),  equipmentGloves:$('#equipmentGloves'),equipmentHat:$('#equipmentHat'), equipmentShirt:$('#equipmentShirt'), equipmentOffhand:$('#equipmentOffhand'), equipmentPants:$('#equipmentPants'), equipmentShoes:$('#equipmentShoes'), equipmentWeapon:$('#equipmentWeapon'),
    trade: $('#tradeButton'), quest:$('#questButton'),capturePet:$('#capturePetButton'),questLog:$('#questLog'),questList:$('#questList'),questWindow:$('#questWindow'),questTitle:$('#questTitle'),questDescription:$('#questDescription'),questReward:$('#questReward'),questAccept:$('#questAccept'),questComplete:$('#questComplete'),questAbandon:$('#questAbandon'),questClose:$('#questClose'), stalk:$('#stalkButton'), continuousAttack:$('#continuousAttackButton'), purchaseBase:$('#purchaseBaseButton'), useFurniture:$('#useFurnitureButton'),openHomeShop:$('#openHomeShopButton'),moveFurniture:$('#moveFurnitureButton'),rotateFurniture:$('#rotateFurnitureButton'),storeFurniture:$('#storeFurnitureButton'),homeShopWindow:$('#homeShopWindow'),homeShopTitle:$('#homeShopTitle'),homeShopHint:$('#homeShopHint'),homeShopListings:$('#homeShopListings'),homeShopInventory:$('#homeShopInventory'),homeShopClose:$('#homeShopClose'),tradeWindow: $('#tradeWindow'), tradeTitle: $('#tradeTitle'), tradeFriend: $('#tradeFriend'), tradeOffers: $('#tradeOffers'), tradeCancel: $('#tradeCancel'), tradeConfirm: $('#tradeConfirm'),treasureWindow:$('#treasureWindow'),treasureCash:$('#treasureCash'),treasureItems:$('#treasureItems'),treasureWeight:$('#treasureWeight'),treasureWarning:$('#treasureWarning'),treasureClose:$('#treasureClose'),treasureTake:$('#treasureTake'),treasureTakeAll:$('#treasureTakeAll'),treasureHint:$('#treasureHint'),treasureColumns:$('#treasureColumns'), serverConfigButton:$('#serverConfigButton'),serverConfigWindow:$('#serverConfigWindow'),serverConfigClose:$('#serverConfigClose'),serverConfigVehicles:$('#serverConfigVehicles'),serverConfigWeapons:$('#serverConfigWeapons'),serverConfigAmmo:$('#serverConfigAmmo'),serverConfigQuestItems:$('#serverConfigQuestItems'),serverConfigMisc:$('#serverConfigMisc'),baseSpeedConfig:$('#baseSpeedConfig'),baseVisibilityConfig:$('#baseVisibilityConfig'),terrainSpeedConfig:$('#terrainSpeedConfig'),travelSpeedConfig:$('#travelSpeedConfig'),saveMovementConfig:$('#saveMovementConfig'),serverTimeModeConfig:$('#serverTimeModeConfig'),serverTimeConfig:$('#serverTimeConfig'),serverTimePreview:$('#serverTimePreview'),weatherModeConfig:$('#weatherModeConfig'),weatherTemperatureConfig:$('#weatherTemperatureConfig'),weatherRefreshConfig:$('#weatherRefreshConfig'),streetLightsOnConfig:$('#streetLightsOnConfig'),streetLightsOffConfig:$('#streetLightsOffConfig'),buildingLightsRefreshConfig:$('#buildingLightsRefreshConfig'),merchantRefreshConfig:$('#merchantRefreshConfig'),doorLockRefreshConfig:$('#doorLockRefreshConfig'),ufoEventNameConfig:$('#ufoEventNameConfig'),ufoIntervalConfig:$('#ufoIntervalConfig'),ufoDurationConfig:$('#ufoDurationConfig'),trexEventNameConfig:$('#trexEventNameConfig'),trexIntervalConfig:$('#trexIntervalConfig'),trexDurationConfig:$('#trexDurationConfig'),brontosaurusEventNameConfig:$('#brontosaurusEventNameConfig'),brontosaurusIntervalConfig:$('#brontosaurusIntervalConfig'),brontosaurusDurationConfig:$('#brontosaurusDurationConfig'),stegosaurusEventNameConfig:$('#stegosaurusEventNameConfig'),stegosaurusIntervalConfig:$('#stegosaurusIntervalConfig'),stegosaurusDurationConfig:$('#stegosaurusDurationConfig'),raptorEventNameConfig:$('#raptorEventNameConfig'),raptorIntervalConfig:$('#raptorIntervalConfig'),raptorDurationConfig:$('#raptorDurationConfig'),landOfGiantsEventNameConfig:$('#landOfGiantsEventNameConfig'),landOfGiantsIntervalConfig:$('#landOfGiantsIntervalConfig'),landOfGiantsDurationConfig:$('#landOfGiantsDurationConfig'),bearEventNameConfig:$('#bearEventNameConfig'),bearIntervalConfig:$('#bearIntervalConfig'),bearDurationConfig:$('#bearDurationConfig'),saveServerEventsConfig:$('#saveServerEventsConfig'),tooltip: $('#actorTooltip'),craftingWindow:$('#craftingWindow'),craftingRecipes:$('#craftingRecipes'),craftingRefresh:$('#craftingRefresh'),craftingClose:$('#craftingClose'), accountSetup: $('#accountSetup'), accountForm: $('#accountForm'), accountRoster:$('#accountRoster'),accountUsername: $('#accountUsername'), accountPassword: $('#accountPassword'), accountError: $('#accountError'),characterPanel:$('#characterPanel'),characterList:$('#characterList'),characterForm:$('#characterForm'),characterName:$('#characterName'),closeCharacters:$('#closeCharactersButton'),homeStoragePanel:$('#homeStoragePanel'),homeStorageItems:$('#homeStorageItems'),homeStorageCount:$('#homeStorageCount'),homeItemStorageSection:$('#homeItemStorageSection'),backpackStorageItems:$('#backpackStorageItems'),chestStorageItems:$('#chestStorageItems'),homeMoneyBalance:$('#homeMoneyBalance'),homeMoneyAmount:$('#homeMoneyAmount'),depositHomeMoney:$('#depositHomeMoney'),withdrawHomeMoney:$('#withdrawHomeMoney'),realitySetup:$('#realitySetup'),realitySetupForm:$('#realitySetupForm'),realityLatitude:$('#realityLatitude'),realityLongitude:$('#realityLongitude'),realitySetupError:$('#realitySetupError'),useServerGps:$('#useServerGpsButton')
  };
  const state = {
    socket: null, playerId: null, snapshot: null, weather: null, base: [], baseById:new Map(), lists: {}, spatial:new Map(), elevationGrid:null,currentRenderLists:{},miniMapStores:[],miniMapMarkers:[],graves:new Map(),lastMiniMapDraw:0,miniMapPointer:null,
    players: new Map(), actors: new Map(), reality: new Map(), doors: new Map(), doorLocks: new Map(), storeHours: new Map(), doorLockCycleEndsAtUtc: null, facings: new Map(), movingUntil: new Map(),
    chat: [], speech: new Map(), privateState: null, dungeon: null, relationships: new Map(), chests: new Map(), loot: new Map(), seenChests: new Set(),
    camera: { x: 0, y: 0 }, scale: initialZoomScale, pitch: .69, shear: .14, follow: true,
    actionMode:'neutral',postureSuppressedUntil:0,nextPostureAt:0,defensiveThreats:new Map(),postureAvoid:new Map(),autoFlee:false,fleeAttempt:0,commandSequence:0,keys: new Set(), path: [], target: null, pathSequence: 0, lastInput: 0, lastBlocked: 0, moveInFlight: false,
    pointer: { down: false, dragged: false, button: null, startX: 0, startY: 0, x: 0, y: 0 }, suppressClick: false, actionPoint: null, actionActor: null, actionDoor:null, actionFurniture:null, actionDungeonFeature:null, actionWorldObject:null,actionPostal:null, movingFurniture:null, furniturePreview:null, characterManagerOpen:false, inventoryTab:'weapon',homeStorageTab:'vehicle',storageChestOpen:false,storageChestId:null,chestContents:null,pendingDoor: null, pendingMerchant:null, pendingChest: null, pendingDungeonAction:null, pendingChop:null,pendingPet:null,followCommand:null, frame: 0, projectiles: [], fireZones:[], areaHazards:new Map(), burningCharacters:new Map(), actorAttacks:new Map(), abductions:new Map(), probulatorBeams:new Map(), vehicleTransitions:new Map(), damageIndicators:[], outdoorFog: new Set(), fogInitializedAreas:new Set(), revealedWorldAreas:new Set(), areaLoading: false, loadingArea:null, loadingOrigin:null, loadingStarted:0, prefetchRequested:new Set(), worldBusy:false, godTogglePending: null, rebuildPending:false,questInteraction:null,
    mapWindowPending:false,mapWindowSequence:0,lastMapWindowRequest:0,mapCoverage:[],lastOutdoorPosition:null,lastEventPanelUpdate:0,homeShop:null,
    performance:{frames:0,lastFpsAt:performance.now(),fps:0,averageRenderMs:0,maximumRenderMs:0,lastPanelAt:0,lastMessageAt:0,messages:0,lastMessageCount:0,lastNetworkAt:performance.now(),messagesPerSecond:0,bytesReceived:0,lastPayloadBytes:0,visibleEntities:0,tasks:new Map(),server:null}
  };
  function gameAudioScene(){
    const players=[...state.players.values()],actors=actorsHere();
    const fires=state.fireZones.map((f,i)=>({...f,id:`zone:${f.startedAt}:${f.position.x}:${f.position.y}`}));
    for(const zone of state.areaHazards.values())if(zone.effect==='napalm')fires.push({...zone,endsAt:Date.parse(zone.endsAtUtc)});
    for(const [id,endsAt]of state.burningCharacters){const entity=state.players.get(id)||actors.find(a=>a.id===id);if(entity)fires.push({id:'character:'+id,position:entity.position,locationId:entity.locationId,endsAt});}
    return {listener:state.connectionTask?null:state.players.get(state.playerId),players,actors,buses:state.transit?.buses||[],vehicles:state.lists.vehicle||[],underwater:!!state.dungeon?.underwater,fires,areaHazards:[...state.areaHazards.values()],patches:!state.dungeon?(state.inversions||state.privateState?.inversions)?.active?.patches||[]:[]};
  }
  const gameAudio=GameAudio.create({getScene:gameAudioScene});
  gameAudio.bindLifecycle();
  setInterval(()=>gameAudio.tick(),100);
  const combatEffects = CombatEffects.createRenderer({context:ctx, project:toScreen, scale:()=>state.scale,
    isVisible:position=>state.dungeon?state.dungeon.underwater||state.dungeon.isHome||state.dungeon.isStore||revealedAt(position.x,position.y):dynamicVisible(position)});
  let serverConfigPopup=null,helpPopup=null,floatingPanelZ=40;

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
    state.welcomeApplied=false;state.serverProbe=null;state.startupPrefetchAt=null;setConnectionTask('Connecting to the world…');
    const scheme = location.protocol === 'https:' ? 'wss:' : 'ws:';
    state.socket = new WebSocket(`${scheme}//${location.host}/ws?characterId=${encodeURIComponent(storedId)}&name=${encodeURIComponent(playerName)}`);
    state.socket.addEventListener('open', () => {setConnectionTask('Loading your character and surroundings…');setStatus('Connected — loading world',false);});
    state.socket.addEventListener('close', () => {state.automaticTreasure=null; state.welcomeApplied=false;state.serverProbe=null;setConnectionTask('Connection lost — reconnecting…');state.mapWindowPending=false;state.areaLoading=false;state.loadingArea=null;state.loadingStarted=0;state.path=[];state.target=null;state.moveInFlight=false;state.performance.tasks.clear();setWorldTask(null);setStatus('Disconnected — route cancelled, retrying', false);setTimeout(connect,1500); });
    state.socket.addEventListener('message', event => {recordNetworkMessage(event.data);handle(JSON.parse(event.data));});
  }
  async function loadAccountRoster(){try{const response=await fetch('/api/account/roster');if(!response.ok)return;const roster=await response.json();ui.accountRoster.replaceChildren();if(!roster.length){ui.accountRoster.innerHTML='<small>No players yet — create the first login below.</small>';return;}for(const account of roster){const button=document.createElement('button');button.type='button';const status=document.createElement('span');status.className=account.online?'online':'offline';status.textContent=account.online?'● Online':'○ Offline';const name=document.createElement('strong');name.textContent=account.username;const details=document.createElement('small');const seen=account.online?'Now':account.lastSeenUtc?new Date(account.lastSeenUtc).toLocaleString():'Never';details.textContent=`${(account.characters||[]).map(character=>character.label).join(' · ')||'No characters'} — Last seen: ${seen}`;button.append(name,status,details);button.addEventListener('click',()=>{ui.accountUsername.value=account.username;ui.accountPassword.focus();});ui.accountRoster.append(button);}}catch{ui.accountRoster.innerHTML='<small>Player list unavailable.</small>';}}
  async function bootstrap(){try{const setupResponse=await fetch('/api/reality/setup');const setup=await setupResponse.json();if(setup.required){ui.accountSetup.hidden=true;ui.realitySetup.hidden=false;return;}ui.realitySetup.hidden=true;const response=await fetch('/api/account/me');if(response.ok){ui.accountSetup.hidden=true;connect();}else{ui.accountSetup.hidden=false;await loadAccountRoster();}}catch{ui.accountSetup.hidden=false;ui.accountError.textContent='The local reality server is unavailable.';}}
  function send(message) { if(Casino.active()&&['attack','attackWorldObject','toggleProbulator','throwHazard','changeDungeonLevel','requestTrade'].includes(message.type))return false; if(state.dungeon?.retroBattle&&['move','requestPath','attack','attackWorldObject','toggleProbulator','changeDungeonLevel'].includes(message.type))return false; if(controlsPaused()&&!['ping','cancelCommand'].includes(message.type)){PlayerCommands.cancel(state);showToast('Please wait until the world is ready.');return false;}if(['setEquipment','setWeaponMode','setTravelMode','teleport','mapFastTravel','enterDungeon','exitDungeon','changeDungeonLevel','useFurniture','craftItem','acceptQuest','captureQuestPet','requestTrade','openChest','collectLoot','openLoot','takeLootItems','takeChestItems','chopVegetation','placeFurniture','moveFurniture','dropItem','useItem'].includes(message.type))stopTravel();message.commandSequence=state.commandSequence; if (state.socket?.readyState === WebSocket.OPEN) state.socket.send(JSON.stringify(message)); }
  function controlsPaused(){return !!state.connectionTask;}
  function setConnectionTask(message){
    if(message&&!state.connectionTask){state.connectionTaskStarted=performance.now();PlayerCommands.cancel(state);}
    state.connectionTask=message||null;ui.connectionTask.hidden=!message;
    ui.connectionTaskMessage.textContent=message||'';canvas.setAttribute('aria-busy',String(!!message));
    setPerformanceTask('connection',message);updateConnectionTaskClock();
  }
  function updateConnectionTaskClock(now=performance.now()){
    if(state.connectionTask)ui.connectionTaskElapsed.textContent=`Controls will resume automatically · ${Math.max(0,Math.floor((now-state.connectionTaskStarted)/1000))}s`;
  }
  function initializeWorldSession(message){
    state.playerId=message.playerId;renderActionMode('neutral');applySnapshot(message.snapshot);applyPrivate(message.privateState);
    // Loading frames have already advanced state.frame. Place the camera before any map requests.
    const me=state.players.get(state.playerId);if(me)state.camera=flightCameraTarget(me);state.follow=true;
    state.welcomeApplied=true;state.firstServerReady=false;setConnectionTask('Checking that the world is ready…');
    setStatus('World loaded — checking response',false);state.serverProbe=null;state.lastServerProbeAt=null;probeServer();
    if(message.homeNotice)showToast(message.homeNotice);
  }
  function probeServer(now=performance.now()){
    updateConnectionTaskClock(now);
    if(!state.welcomeApplied||state.socket?.readyState!==WebSocket.OPEN)return;
    if(state.serverProbe){
      if(now-state.serverProbe.started>=2500){setConnectionTask('The server is busy — waiting for it to respond…');setStatus('Server busy — controls paused',false);}
      return;
    }
    if(state.startupPrefetchAt!=null&&now>=state.startupPrefetchAt&&!controlsPaused()&&!state.worldBusy&&!state.areaLoading){
      state.startupPrefetchAt=null;const me=state.players.get(state.playerId);
      if(me?.locationId==='outdoor')requestNearbyPrefetch(areaForPoint(me.position),me.position);
    }
    if(state.lastServerProbeAt!=null&&now-state.lastServerProbeAt<1000)return;
    const id=(state.serverProbeSequence||0)+1;state.serverProbeSequence=id;state.serverProbe={id,started:now};state.lastServerProbeAt=now;
    state.socket.send(JSON.stringify({type:'ping',id}));
  }
  function acceptServerProbe(message){
    if(!state.welcomeApplied||!state.serverProbe||message.id!==state.serverProbe.id)return;
    state.performance.pingMilliseconds=performance.now()-state.serverProbe.started;state.serverProbe=null;state.lastServerProbeAt=performance.now();setConnectionTask(null);setStatus('Ready to play',true);
    if(!state.firstServerReady){state.firstServerReady=true;state.startupPrefetchAt=performance.now()+5000;send({type:'setActionMode',mode:'neutral'});}
  }
  function setPerformanceTask(key,message){if(!message){state.performance.tasks.delete(key);return;}const current=state.performance.tasks.get(key);state.performance.tasks.set(key,{message,startedAt:current?.message===message?current.startedAt:performance.now()});}
  function setWorldTask(message,blocksMovement=true){state.worldBusy=!!message&&blocksMovement;ui.worldTask.hidden=!message;ui.worldTask.textContent=message||'';setPerformanceTask('world',message);}

  function applySnapshot(snapshot) {
    state.mapWindowSequence++;state.mapWindowPending=false;setPerformanceTask('map-window',null);
    state.snapshot = snapshot;
    state.transit = snapshot.transit || {stops:[],buses:[],routes:[]};
    state.mapCoverage = snapshot.mapCoverage || [];
    applyMapWindow({baseEntities:snapshot.baseEntities,elevation:snapshot.elevation,coverage:state.mapCoverage});
    applyDoorLocks(snapshot.doorLocks);
    state.areaHazards=new Map((snapshot.areaHazards||[]).map(zone=>[zone.id,zone]));
    state.doorLockCycleEndsAtUtc = snapshot.doorLockCycleEndsAtUtc || null;
    state.reality = new Map((snapshot.realityEntities || []).map(x => [x.id, x]));
    state.publicBases = new Map((snapshot.publicBases || []).map(base=>[base.buildingId,base]));
    state.graves = new Map((snapshot.graves || []).map(grave=>[grave.id,grave]));
    state.players = new Map((snapshot.players || []).map(x => [x.id, x]));
    state.actors = new Map((snapshot.actors || []).map(x => [x.id, x]));
    state.weather = snapshot.weather;
    initializeFogForAreas(loadedAreas(snapshot));
    ui.realityName.textContent = snapshot.reality.name;
    const me = state.players.get(state.playerId);
    if(me?.locationId==='outdoor')state.lastOutdoorPosition=me.position;
    if (me && !Number.isFinite(state.camera.x)) state.camera = { x: me.position.x, y: me.position.y };
    if (me && state.frame === 0) state.camera = { x: me.position.x, y: me.position.y };
    updateMode(me?.travelMode);syncGodControls(me);
  }
  function applyMapWindow(map){
    if(!map||!state.snapshot)return;
    state.mapCoverage=map.coverage||[];
    state.snapshot={...state.snapshot,baseEntities:map.baseEntities||[],elevation:map.elevation||[],mapCoverage:state.mapCoverage};
    state.base = map.baseEntities || [];
    state.baseById = new Map(state.base.map(entity=>[entity.id,entity]));
    state.lists = Object.groupBy ? Object.groupBy(state.base, entity => entity.kind) : state.base.reduce((all, entity) => {
      (all[entity.kind] ||= []).push(entity); return all;
    }, {});
    {const seenStores=new Set();state.miniMapStores=[];for(const store of state.base){if(!['building','pointOfInterest'].includes(store.kind)||!store.properties?.merchantCategory)continue;const key=`${Math.round(store.position.x/8)}:${Math.round(store.position.y/8)}:${store.properties.merchantCategory}`;if(seenStores.has(key))continue;seenStores.add(key);state.miniMapStores.push(store);}}
    for (const entity of state.base) entity._bounds = boundsOf(entity);
    buildSpatialIndex();buildElevationGrid(map.elevation||[]);state.currentRenderLists={};
    state.doors = new Map((state.lists.door || []).filter(x => x.properties?.buildingId).map(x => [x.properties.buildingId, x]));
  }
  function maintainMapWindow(view,me,now){
    if(!me||!state.snapshot?.mapCoverage||state.areaLoading||state.socket?.readyState!==WebSocket.OPEN)return;
    if(state.mapWindowPending&&now-state.lastMapWindowRequest<5000)return;
    if(now-state.lastMapWindowRequest<250)return;
    const contains=(outer,inner)=>outer.minimumX<=inner.minimumX&&outer.minimumY<=inner.minimumY&&outer.maximumX>=inner.maximumX&&outer.maximumY>=inner.maximumY;
    const origin=me.locationId==='outdoor'?me.position:state.lastOutdoorPosition||state.privateState?.base?.position;
    const needed=[];
    if(me.locationId==='outdoor')needed.push({minimumX:view.minX-48,minimumY:view.minY-48,maximumX:view.maxX+48,maximumY:view.maxY+48});
    if(origin)needed.push({minimumX:origin.x-500,minimumY:origin.y-500,maximumX:origin.x+500,maximumY:origin.y+500});
    const cameraWindow=state.mapCoverage[1],wanted=needed[0];
    const retainedView=wanted&&{minimumX:wanted.minimumX-384,minimumY:wanted.minimumY-384,maximumX:wanted.maximumX+384,maximumY:wanted.maximumY+384};
    const excess=!!cameraWindow&&!!retainedView&&!contains(retainedView,cameraWindow);
    if(!excess&&needed.length&&needed.every(bounds=>state.mapCoverage.some(area=>contains(area,bounds))))return;
    const desired=needed[0];if(!desired)return;
    state.mapWindowPending=true;state.lastMapWindowRequest=now;state.mapWindowSequence++;
    setPerformanceTask('map-window','Loading nearby map detail');
    send({type:'requestMapWindow',...desired,sequence:state.mapWindowSequence});
  }
  function rebuildBaseIndexes(){state.baseById=new Map(state.base.map(entity=>[entity.id,entity]));state.lists=Object.groupBy?Object.groupBy(state.base,entity=>entity.kind):state.base.reduce((all,entity)=>{(all[entity.kind]||=[]).push(entity);return all;},{});for(const entity of state.base)entity._bounds=boundsOf(entity);state.doors=new Map((state.lists.door||[]).filter(entity=>entity.properties?.buildingId).map(entity=>[entity.properties.buildingId,entity]));buildSpatialIndex();state.currentRenderLists={};}

  function applyPrivate(privateState) {
    const previousLocation=state.dungeon?.id;
    const craftingSkill=privateState?.craftingSkill;if(craftingSkill)$('#craftingLevelValue').textContent=`${craftingSkill.level.toLocaleString()} (${craftingSkill.progressPercent}%)`;
    if (!privateState) return;
    state.privateState = privateState; state.dungeon = privateState.dungeon || null;if(previousLocation!==state.dungeon?.id){state.path=[];state.target=null;state.followCommand=null;state.moveInFlight=false;centerOnPlayer();if(state.dungeon?.underwater)state.scale=28;}if(!state.dungeon?.isHome){ui.craftingWindow.hidden=true;state.crafting=null;}
    state.relationships = new Map((privateState.relationships || []).map(item => [item.actorId, item.friendRating]));
    state.chests = new Map((privateState.chests || []).map(item => [item.id, item]));
    state.loot = new Map((privateState.loot || []).map(item => [item.id, item]));
    state.revealedWorldAreas = new Set(privateState.revealedWorldAreas || []);clearMappedFog();
    renderProgression();
    for(const item of privateState.serverConfiguration?.items||[])if(item.storageSection==='gloves'){equipmentSlotByItem[item.itemType]='gloves';itemGlyphs[item.itemType]='\u{1F9E4}';}
    renderInventory(privateState.inventory);
    renderRecipeBook();
    queueMicrotask(refreshOpenCrafting);
    renderHomeUpgrades();
    refreshTreasure();
    renderEquipment(state.players.get(state.playerId));
    updateMode(state.players.get(state.playerId)?.travelMode);
    renderServerConfiguration(privateState.serverConfiguration);
    renderQuests(privateState.quests||[]);
    const atHome=!!state.dungeon?.isHome,editableHome=atHome&&!!privateState.canEditHome,safeInterior=atHome||!!state.dungeon?.isStore||!!state.dungeon?.retroBattle||!!state.dungeon?.eventBattle||!!state.dungeon?.underwater;ui.safeExit.hidden=!safeInterior;ui.safeExit.disabled=false;ui.safeExit.textContent=state.dungeon?.underwater?'Surface':state.dungeon?.retroBattle||state.dungeon?.eventBattle?'Leave dungeon':atHome?'Leave Home':state.dungeon?.storeCategory==='casino'?'Leave casino':'Leave Store';$('#openHomeStorageButton').hidden=!editableHome;if(!editableHome)ui.homeStoragePanel.hidden=true;renderHomeStorage(editableHome?privateState.homeStorage:[]);if(!editableHome){state.characterManagerOpen=false;state.storageChestOpen=false;state.storageChestId=null;ui.characterPanel.hidden=true;}else ui.characterPanel.hidden=!state.characterManagerOpen;renderHomeItemStorage();
  }

  async function loadCharacters(force=false){if(!force&&Date.now()-(state.lastCharacterLoad||0)<5000)return;state.lastCharacterLoad=Date.now();const response=await fetch('/api/account/characters');if(!response.ok)return;const body=await response.json();ui.characterList.replaceChildren();for(const character of body.characters){const row=document.createElement('div');row.className='character-row';const name=document.createElement('span');name.textContent=character.name+(character.id===body.activeCharacterId?' (active)':'');row.append(name);if(character.id!==body.activeCharacterId){const select=document.createElement('button');select.type='button';select.textContent='Play';select.addEventListener('click',async()=>{const r=await fetch(`/api/account/characters/${character.id}/select`,{method:'POST'});if(r.ok)location.reload();else showToast((await r.json()).message);});const remove=document.createElement('button');remove.type='button';remove.textContent='Remove';remove.addEventListener('click',async()=>{if(!confirm(`Remove ${character.name}? This deletes that character's saved state.`))return;const r=await fetch(`/api/account/characters/${character.id}`,{method:'DELETE'});if(r.ok)loadCharacters(true);else showToast((await r.json()).message);});row.append(select,remove);}ui.characterList.append(row);}}

  function handle(message) {
    if(message.type==='casinoUpdated'){updatePlayer(message.player);applyPrivate(message.privateState);Casino.receive(message);return;}
    if(message.type==='error'&&Casino.active())Casino.error(message.message);
    playMessageSound(message);
    switch (message.type) {
      case 'retroEventChanged': showToast(message.message);break;
      case 'retroBattleUpdated': {
        const entering=state.dungeon?.id!==message.dungeon.id;
        updatePlayer(message.player);state.dungeon=message.dungeon;state.retroReceivedAt=performance.now();
        if(entering){state.path=[];state.target=null;state.followCommand=null;state.pendingDungeonAction=null;state.pendingChest=null;state.pendingDoor=null;state.pointer.down=false;ui.actionMenu.hidden=true;applyPrivate({...state.privateState,dungeon:message.dungeon,chests:message.dungeon.chests,loot:[]});}
        ui.safeExit.hidden=false;ui.safeExit.disabled=false;ui.safeExit.textContent='Leave dungeon';
        break;
      }
      case 'sessionLoading': if(!state.welcomeApplied)setConnectionTask(message.message);break;
      case 'welcome': state.autoTreasurePending=false;state.autoTreasureSignature=null; initializeWorldSession(message);break;
      case 'pong': acceptServerProbe(message);break;
      case 'actionModeChanged': renderActionMode(message.mode);break;
      case 'relationshipsChanged': for(const relationship of message.relationships)state.relationships.set(relationship.actorId,relationship.friendRating);break;
      case 'publicBasesChanged': state.publicBases=new Map((message.publicBases||[]).map(base=>[base.buildingId,base]));break;
      case 'playerJoined': case 'playerUpdated': updatePlayer(message.player); break;
      case 'playersUpdated': for(const player of message.players) updatePlayer(player); break;
      case 'areaHazardsChanged': state.areaHazards=new Map((message.areaHazards||[]).map(zone=>[zone.id,zone]));break;
      case 'doorLocksChanged': applyDoorLocks(message.doorLocks);state.doorLockCycleEndsAtUtc=message.doorLockCycleEndsAtUtc||null;if(state.pendingDoor&&doorIsLocked(state.pendingDoor)){state.pendingDoor=null;stopTravel('That door is now locked.');}break;
      case 'playerMoved': if(message.player.id===state.playerId)state.moveInFlight=false;updatePlayer(message.player, true); break;
      case 'playerLeft': state.players.delete(message.playerId);if(state.followCommand?.targetId===message.playerId)stopTravel('The target left the reality.');break;
      case 'actorsMoved': {for(const actor of message.actors)if((actor.locationId||'outdoor')==='outdoor')state.actors.set(actor.id,actor);if(state.dungeon){const local=new Map((state.dungeon.actors||[]).map(actor=>[actor.id,actor]));for(const actor of message.actors)if(actor.locationId===state.dungeon.id)local.set(actor.id,actor);state.dungeon={...state.dungeon,actors:[...local.values()]};}for(const actor of message.actors)if(actor.isMoving)state.movingUntil.set(actor.id,performance.now()+700);break;}
      case 'actorsRemoved': for(const id of message.ids){state.actors.delete(id);state.relationships.delete(id);state.abductions.delete(id);state.burningCharacters.delete(id);state.movingUntil.delete(id);}if(state.dungeon)state.dungeon={...state.dungeon,actors:(state.dungeon.actors||[]).filter(a=>!message.ids.includes(a.id))};if(message.ids.includes(state.followCommand?.targetId))stopTravel('Target left.');break;
      case 'testCharacterPlaced': ui.actionMenu.hidden=true;showToast(message.message);break;
      case 'testCharactersCleared': for(const id of message.ids){state.players.delete(id);state.actors.delete(id);state.abductions.delete(id);state.burningCharacters.delete(id);}if(message.ids.includes(state.followCommand?.targetId))stopTravel('Test target removed.');if(message.ownerId===state.playerId){ui.actionMenu.hidden=true;showToast(`Cleared ${message.ids.length} test characters.`);}break;
      case 'worldEventTriggered': for(const actor of message.actors||[message.actor])if(actor)state.actors.set(actor.id,actor);showToast(message.message);break;
      case 'pathResult': if(message.sequence!==state.pathSequence)break;state.path=message.waypoints||[];if(!state.areaLoading)setWorldTask(null);break;
      case 'pathUnavailable': if(message.sequence===state.pathSequence){const fleeing=state.autoFlee;if(fleeing)state.fleeAttempt++;stopTravel(message.message,fleeing);if(fleeing)state.nextPostureAt=performance.now()+150;}break;
      case 'movementBlocked': state.moveInFlight=false;if(message.sequence!=null&&message.sequence!==state.pathSequence)break;if (Date.now() - state.lastBlocked > 1200) { state.lastBlocked = Date.now(); stopTravel(message.message); } break;
      case 'movementNotice': state.moveInFlight=false;if(message.privateState)applyPrivate(message.privateState);stopTravel(message.message);break;
      case 'playerFell': state.moveInFlight=false;updatePlayer(message.player); stopTravel(message.message); break;
      case 'playerDied': state.moveInFlight=false;updatePlayer(message.player);if(message.privateState){applyPrivate(message.privateState);state.dungeon=message.privateState.dungeon||null;}centerOnPlayer();setWorldTask(null);stopTravel(message.reason); break;
      case 'playerTeleported': updatePlayer(message.player); if(message.player.id===state.playerId){state.moveInFlight=false;state.areaLoading=false;state.loadingArea=null;setWorldTask(null);centerOnPlayer();state.path=[];state.target=null;showToast('Teleported.');} break;
      case 'chatSaid': receiveChat(message.chat); break;
      case 'worldSound': receiveWorldSound(message.sound);break;
      case 'weatherChanged': state.weather = message.weather; break;
      case 'homeWorkshopUpdated': applyPrivate(message.privateState);if(state.crafting&&!state.crafting.tableDestroyed)send({type:'requestCrafting',furnitureId:state.crafting.furnitureId});showToast(message.message);break;
      case 'dirtyWaterCollected': applyPrivate(message.privateState);showToast(message.message);break;
      case 'privateState': applyPrivate(message.privateState); break;
      case 'dungeonEntered': updatePlayer(message.player);state.path=[];state.target=null;state.followCommand=null;state.pendingDoor=null;state.pendingMerchant=null;state.pendingChest=null;state.pendingDungeonAction=null;state.actionDoor=null;state.actionDungeonFeature=null;state.areaLoading=false;state.loadingArea=null;state.loadingStarted=0;setWorldTask(null);applyPrivate(message.privateState);centerOnPlayer();showToast(message.dungeon?.retroBattle?'Jump Now, Regret Later! Defeat the small boss, then complete the big boss dungeon.':message.dungeon?.isHome?'Entered Home.':message.dungeon?.storeCategory==='casino'?'Entered Lucky Lantern Casino. Walk left or right to play.':message.dungeon?.isStore?'Entered store.':message.dungeon?.difficulty>50?`Entered Stronghold — Difficulty ${message.dungeon.difficulty}.`:`Entered dungeon — Difficulty ${message.dungeon?.difficulty||1}; unexplored rooms are hidden by fog.`);break;
      case 'dungeonLevelChanged': updatePlayer(message.player);state.path=[];state.target=null;state.followCommand=null;state.pendingDungeonAction=null;state.areaLoading=false;state.loadingArea=null;state.loadingStarted=0;setWorldTask(null);applyPrivate(message.privateState);centerOnPlayer();showToast(`Entered ${message.dungeon?.difficulty>50?'Stronghold':'dungeon'} level ${message.dungeon?.level||1} of ${message.dungeon?.levelCount||1} — Difficulty ${message.dungeon?.difficulty||1}.`);break;
      case 'dungeonExited': {ui.craftingWindow.hidden=true;state.crafting=null;updatePlayer(message.player);state.path=[];state.target=null;state.followCommand=null;state.pendingDoor=null;state.pendingMerchant=null;state.pendingChest=null;state.pendingDungeonAction=null;state.actionDoor=null;state.actionDungeonFeature=null;state.storageChestOpen=false;state.storageChestId=null;applySnapshot(message.snapshot);applyPrivate(message.privateState);state.dungeon=null;ui.safeExit.hidden=true;ui.safeExit.disabled=false;centerOnPlayer();showToast('Returned to the world.');break;}
      case 'dungeonUpdated': state.dungeon=message.dungeon;break;
      case 'mapWindow': if(message.sequence===state.mapWindowSequence){state.mapWindowPending=false;setPerformanceTask('map-window',null);applyMapWindow(message.map);}break;
      case 'worldExpanded': if(message.sequence!=null&&message.sequence!==state.pathSequence)break;setWorldTask('Rendering area…');requestAnimationFrame(()=>setTimeout(()=>finishWorldExpansion(message),0));break;
      case 'taskStatus': if(message.sequence!=null&&message.sequence!==state.pathSequence)break;setWorldTask(message.task||'Working…',message.blocksMovement!==false);break;
      case 'tradeQuote': openTrade(message.quote); break;
      case 'homeShopOpened': openHomeShop(message.shop);break;
      case 'statsAssigned': state.statDraft=null;
      case 'progressionUpdated': if(message.privateState)applyPrivate(message.privateState);for(const notice of message.notices||[])showToast(notice.message);break;
      case 'craftingOpened': case 'craftingUpdated': if(message.privateState)applyPrivate(message.privateState);openCrafting(message.crafting);if(message.message)showToast(message.message);break;
      case 'homeShopUpdated': updatePlayer(message.player);applyPrivate(message.privateState);openHomeShop(message.shop);showToast(message.message);break;
      case 'tradeCompleted': updatePlayer(message.player);applyPrivate(message.privateState||{...state.privateState,inventory:message.inventory,relationships:[...(state.privateState?.relationships||[]).filter(x=>x.actorId!==message.relationship.actorId),message.relationship]});closeTradeWindow();showToast('Trade completed.');break;
      case 'questInteraction': openQuest(message.interaction);break;
      case 'gardenBuildOptions': gardenUI.options(message.garden);break;
      case 'gardenResult': gardenUI.result();if(message.privateState)applyPrivate(message.privateState);showToast(message.message);break;
      case 'questUpdated': if(message.privateState)applyPrivate(message.privateState);if(message.quest?.id===state.questInteraction?.quest?.id)closeQuestWindow();showToast(message.message);break;
      case 'crimeReported': if(message.privateState)applyPrivate(message.privateState);showToast(message.message);break;
      case 'lockPickResult': if(message.privateState)applyPrivate(message.privateState);if(message.success&&state.doorLocks.has(message.doorId))state.doorLocks.set(message.doorId,{...state.doorLocks.get(message.doorId),locked:false});showToast(message.message);break;
      case 'actorRemoved': state.actors.delete(message.actorId);if(state.dungeon)state.dungeon={...state.dungeon,actors:(state.dungeon.actors||[]).filter(actor=>actor.id!==message.actorId)};break;
      case 'vegetationChopped': state.base=state.base.filter(entity=>entity.id!==message.entityId);if(state.snapshot)state.snapshot.baseEntities=state.base;applySnapshot({...state.snapshot,baseEntities:state.base});break;
      case 'nearbyTreasureOpened': case 'nearbyTreasureUpdated': receiveNearbyTreasure(message);break;
      case 'autoTreasureUpdated': receiveAutoTreasure(message);break;
      case 'combatTargetUnavailable': removeCombatTarget(message.targetId);if(state.followCommand?.targetId===message.targetId)stopTravel('That target is no longer available.');break;
      case 'chestOpened': updatePlayer(message.player||state.players.get(state.playerId));applyPrivate(message.privateState);openTreasure(message.contents,message.message,false,true);break;
      case 'chestItemsTaken': state.automaticTreasure=null;applyPrivate(message.privateState);if(message.chestRemoved){state.chests.delete(message.chestId);if(state.dungeon?.retroBattle)openTreasure({chestId:message.chestId,items:[]},'Everything collected. Close this chest to leave the dungeon.');else{state.chestContents=null;ui.treasureWindow.hidden=true;}}else openTreasure(message.contents,message.message);showToast(message.message);break;
      case 'lootOpened': applyPrivate(message.privateState);openLootTreasure(message.loot,message.message,false,true);break;
      case 'lootItemsTaken': state.automaticTreasure=null;applyPrivate(message.privateState);if(message.loot)openLootTreasure(message.loot,message.message);else if(state.dungeon?.retroBattle&&state.dungeon.isCompleted)openTreasure({lootId:message.lootId,dropKind:'eventReward',items:[],moneyCents:0},'Everything collected. Close this treasure to leave the dungeon.');else closeTreasure();showToast(message.message);break;
      case 'lootCollected': applyPrivate(message.privateState);if(message.lootId)state.loot.delete(message.lootId);showToast(message.message);break;
      case 'lootCreated': {const me=state.players.get(state.playerId);if(message.loot?.dropKind==='tombstone')state.graves.set(message.loot.id,message.loot);if(me&&message.loot?.locationId===me.locationId)state.loot.set(message.loot.id,message.loot);refreshTreasure();break;}
      case 'lootRemoved': state.loot.delete(message.lootId);state.graves.delete(message.lootId);refreshTreasure();break;
      case 'inventoryItemDropped': applyPrivate(message.privateState);showToast(message.message);break;
      case 'rested': updatePlayer(message.player);applyPrivate(message.privateState);showToast('Fully rested: health, stamina, and water restored for five minutes.');break;
      case 'homeUpdated': state.dungeon=message.dungeon;applyPrivate(message.privateState);state.movingFurniture=null;state.furniturePreview=null;state.actionFurniture=null;showToast(message.message||'Home updated.');break;
      case 'homeStorageOpened': state.storageChestOpen=true;applyPrivate(message.privateState);showInteractionWindow(ui.homeStoragePanel,960);showToast('Storage chest opened.');break;
      case 'homeStorageUpdated': applyPrivate(message.privateState);showToast(message.message||'Storage chest updated.');break;
      case 'postalTransferCompleted': applyPrivate(message.privateState);renderPostalItems();showToast(message.message);break;
      case 'basePurchased': updatePlayer(message.player);applyPrivate(message.privateState);ui.actionMenu.hidden=true;showToast(`New base purchased for $${(message.priceCents/100).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}.`);break;
      case 'itemConfigurationUpdated': applyPrivate(message.privateState);showToast(`${message.item.displayName} server rules saved.`);break;
      case 'configuredInventoryAdjusted': applyPrivate(message.privateState);showToast(message.message);break;
      case 'movementConfigurationUpdated': applyPrivate(message.privateState);showToast('Movement and visibility rules saved.');break;
      case 'serverEventsUpdated': applyPrivate(message.privateState);showToast('Server event schedules saved.');break;
      case 'serverEventsChanged': if(state.privateState?.serverConfiguration){state.privateState={...state.privateState,serverConfiguration:{...state.privateState.serverConfiguration,events:message.events}};updateServerTimePreview();}break;
      case 'chestUpdated': state.chests.set(message.chest.id,message.chest);break;
      case 'inversionsUpdated': state.inversions=message.inversions;if(message.progression&&state.privateState){state.privateState.progression=message.progression;renderProgression();}Inversions.tick();break;
      case 'eventBattleUpdated': state.dungeon=message.dungeon;if(message.player)state.players.set(message.player.id,message.player);if(message.privateState)applyPrivate(message.privateState);Inversions.tick();break;
      case 'combatEvent': receiveCombat(message.combat);receiveCombatFear(message.combat);break;
      case 'worldRebuilt': reconnectAfterRebuild();break;
      case 'objectCreated': state.reality.set(message.entity.id, message.entity); break;
      case 'worldObjectUpdated': {if(state.mapCoverage.length&&!state.mapCoverage.some(area=>visible(message.entity,{minX:area.minimumX-50,minY:area.minimumY-50,maxX:area.maximumX+50,maxY:area.maximumY+50})))break;const index=state.base.findIndex(entity=>entity.id===message.entity.id);if(index>=0)state.base[index]=message.entity;else state.base.push(message.entity);rebuildBaseIndexes();break;}
      case 'busRoute': if(message.route?.id===$('#busRouteSelect').value){state.busRouteDetail=message;renderBusRoute();}break;
      case 'busRoutePositions': if($('#busRouteDialog').open&&message.routeId===state.busRouteDetail?.route?.id&&message.routeId===$('#busRouteSelect').value){state.busRouteDetail.buses=message.buses||[];renderBusRoute();}break;
      case 'transitChanged': state.transit=message.transit;state.lastMiniMapDraw=-Infinity;break;
      case 'busesMoved': state.transit={...(state.transit||{stops:[],routes:[]}),buses:message.buses};state.lastMiniMapDraw=-Infinity;break;
      case 'objectRemoved': state.reality.delete(message.entityId);if(state.baseById.has(message.entityId)){state.base=state.base.filter(entity=>entity.id!==message.entityId);rebuildBaseIndexes();}break;
      case 'flagPlaced': state.reality.set(message.entity.id,message.entity);applyPrivate(message.privateState);ui.actionMenu.hidden=true;showToast(`Flag placed: ${message.entity.properties?.label||'Flag'}`);break;
      case 'error': gardenUI.error();recoverAutomaticTreasure(message);if(message.commandSequence!=null&&message.commandSequence!==state.commandSequence)break;if(state.followCommand?.automatic)state.postureAvoid.set(state.followCommand.targetId,performance.now()+10000);if(state.followCommand)stopTravel();state.moveInFlight=false;state.areaLoading=false;state.loadingArea=null;state.loadingStarted=0;setWorldTask(null);if(state.dungeon?.isHome||state.dungeon?.isStore){ui.safeExit.disabled=false;ui.safeExit.textContent=state.dungeon.isHome?'Leave Home':state.dungeon.storeCategory==='casino'?'Leave casino':'Leave Store';}if(state.rebuildPending){state.rebuildPending=false;ui.rebuild.textContent='Rebuild Reality';syncGodControls(state.players.get(state.playerId));}showToast(message.message);state.homeUpgradeRenderKey=null;state.recipeBookRenderKey=null;renderHomeUpgrades();renderRecipeBook(); break;
    }
  }
  function finishWorldExpansion(message){if(message.sequence!=null&&message.sequence!==state.pathSequence)return;const previous=loadedAreas(),completedArea=state.loadingArea,origin=state.loadingOrigin;applySnapshot(message.snapshot);if(message.expanded!==false)markNewAreaFog(previous,loadedAreas(message.snapshot));state.areaLoading=false;state.loadingArea=null;state.loadingOrigin=null;state.loadingStarted=0;setWorldTask(null);refreshServerDiagnostics();if(completedArea)requestNearbyPrefetch(completedArea,origin);showToast(message.expanded===false?'Area already loaded.':'New area ready — explore to reveal it.');}
  function updatePlayer(player, moving = false) {
    const old = state.players.get(player.id);
    if(old && player.version != null && old.version != null && player.version < old.version)return;
    if (old) {
      const dx = player.position.x - old.position.x, dy = player.position.y - old.position.y;
      if (Math.abs(dx) > Math.abs(dy)) state.facings.set(player.id, dx > 0 ? 'east' : 'west');
      else if (!state.dungeon?.underwater && Math.abs(dy) > .001) state.facings.set(player.id, dy > 0 ? 'north' : 'south');
      if(old.locationId!==player.locationId||JSON.stringify(old.position.region)!==JSON.stringify(player.position.region))state.vehicleTransitions.delete(player.id);
      else if(old.travelMode!==player.travelMode&&(old.travelMode==='ufo'||player.travelMode==='ufo'))state.vehicleTransitions.set(player.id,{from:old.travelMode,to:player.travelMode,started:performance.now(),origin:old.position,locationId:player.locationId});
    }
    state.players.set(player.id, player);
    if (moving && player.speedMetersPerSecond > .01) state.movingUntil.set(player.id, performance.now() + 250);
    if (player.id === state.playerId) {if(old?.locationId!==player.locationId){state.path=[];state.target=null;state.followCommand=null;state.moveInFlight=false;send({type:'requestPrivateState'});}if(player.locationId==='outdoor')state.lastOutdoorPosition=player.position;updateMode(player.travelMode);syncGodControls(player);renderEquipment(player);if(state.privateState?.inventory)renderInventory(state.privateState.inventory); }
  }
  function syncGodControls(player){if(!player)return;if(state.godTogglePending!==null&&!!player.godMode!==state.godTogglePending){ui.god.disabled=true;ui.serverConfigButton.disabled=true;ui.performancePanel.hidden=true;ui.rebuild.disabled=true;return;}state.godTogglePending=null;ui.god.disabled=false;ui.god.setAttribute('aria-pressed',String(!!player.godMode));ui.god.textContent=player.godMode?'God Mode: On':'God Mode: Off';ui.serverConfigButton.disabled=!player.godMode;ui.serverConfigButton.title=player.godMode?'Server configuration':'Enable God Mode to open server configuration';ui.performancePanel.hidden=!player.godMode;if(!player.godMode){closeServerConfigWindow();}ui.rebuild.disabled=!player.godMode||state.rebuildPending;if(!ui.actionMenu.hidden)updateActionMenu();}
  function stopTravel(message,automatic=false) {
    clearTimeout(primaryClickTimer);primaryClickTimer=null;
    PlayerCommands.cancel(state);setWorldTask(null);if(!automatic)state.postureSuppressedUntil=performance.now()+2000;
    send({type:'cancelCommand'});
    if(message)showToast(message);
  }
  function setActionMode(mode){
    stopTravel();state.postureSuppressedUntil=0;renderActionMode(mode);send({type:'setActionMode',mode});
  }
  function renderActionMode(mode){
    state.actionMode=mode;
    for(const button of document.querySelectorAll('[data-posture]')){
      button.setAttribute('aria-pressed',String(button.dataset.posture===mode));
      button.title={neutral:'Safe clicks; no first-meeting adjustment.',attackReady:'Click to attack; automatically fires at attackers and foes in range without moving. First meeting: −0.05 friendship.',aggressive:'Pursues visible animals, NPCs, and players, even friendly ones, when idle. First meeting: −0.10 friendship.',defensive:'Retaliates against attackers when free to act. First meeting: +0.05 friendship.',timid:'Walks away from hostile characters and nearby players when idle. First meeting: +0.10 friendship.'}[button.dataset.posture]+' Once per server day for ordinary outdoor NPCs.';
    }
  }

  function flightCameraTarget(me){const transition=ufoTransitionFrame(state.vehicleTransitions.get(me.id),performance.now()),lift=transition?transition.lift*ufoFlightHeightPixels():me.travelMode==='ufo'?ufoFlightHeightPixels():me.abduction?(abductionVisual(me,performance.now())?.lift||0):0,offset=lift*.45/(state.scale*state.pitch);return{x:me.position.x-offset*state.shear,y:me.position.y+offset};}
  function centerOnPlayer(){const me=state.players.get(state.playerId);if(me){state.scale=maximumZoomScale;state.camera=flightCameraTarget(me);}state.follow=true;}
  function setStatus(text, online) { ui.status.textContent = text; ui.dot.classList.toggle('online', online); }
  function showToast(text) { ui.toast.textContent = text || ''; ui.toast.classList.add('show'); clearTimeout(showToast.timer); showToast.timer = setTimeout(() => ui.toast.classList.remove('show'), 3200); }
  function initializeCollapsiblePanels(){for(const panel of document.querySelectorAll('[data-collapsible]')){panel.classList.add('collapsible-panel');const key=`alternative-reality-panel:${panel.id}`,button=document.createElement('button');button.type='button';button.className='panel-collapse-button';button.title=`Collapse ${panel.dataset.collapsible}`;button.setAttribute('aria-label',button.title);const setCollapsed=(collapsed,persist=false)=>{panel.classList.toggle('panel-collapsed',collapsed);button.textContent=collapsed?'＋':'−';button.title=`${collapsed?'Expand':'Collapse'} ${panel.dataset.collapsible}`;button.setAttribute('aria-label',button.title);button.setAttribute('aria-expanded',String(!collapsed));if(persist)try{localStorage.setItem(key,collapsed?'collapsed':'expanded');}catch{}if(panel.classList.contains('floating-panel'))requestAnimationFrame(()=>clampFloatingPanel(panel,true));};let collapsed=false;try{collapsed=localStorage.getItem(key)==='collapsed';}catch{}setCollapsed(collapsed);button.addEventListener('click',event=>{event.preventDefault();event.stopPropagation();setCollapsed(!panel.classList.contains('panel-collapsed'),true);});panel.prepend(button);}}
  const panelLayoutKey=panel=>`alternative-reality-panel-layout:${panel.id}`;
  function savedPanelLayout(panel){try{const value=JSON.parse(localStorage.getItem(panelLayoutKey(panel))||'null');return value&&typeof value==='object'?value:null;}catch{return null;}}
  function savePanelLayout(panel){try{const rect=panel.getBoundingClientRect();localStorage.setItem(panelLayoutKey(panel),JSON.stringify({floating:panel.classList.contains('floating-panel'),left:Math.round(rect.left),top:Math.round(rect.top),width:Math.round(rect.width)}));}catch{}}
  const dockOrderStorageKey='alternative-reality-panel-order';
  let dockPanelOrder=[];
  function saveDockPanelOrder(){try{localStorage.setItem(dockOrderStorageKey,JSON.stringify(dockPanelOrder));}catch{}}
  function insertPanelInRail(panel,beforePanel=undefined){
    if(beforePanel!==undefined){
      const order=dockPanelOrder.filter(id=>id!==panel.id),index=beforePanel?order.indexOf(beforePanel.id):-1;
      order.splice(index<0?order.length:index,0,panel.id);dockPanelOrder=order;
      dockPanelOrder.forEach((id,index)=>{const item=document.getElementById(id);if(item)item.dataset.dockOrder=String(index);});
    }
    const order=Number(panel.dataset.dockOrder),before=[...ui.rightRail.children].find(child=>child!==panel&&child.matches?.('.panel')&&Number(child.dataset.dockOrder)>order)||ui.serverConfigWindow;
    if(before&&before.parentElement===ui.rightRail)ui.rightRail.insertBefore(panel,before);else ui.rightRail.append(panel);
  }
  function clampPanelPosition(panel,left,top){const margin=8,availableWidth=Math.max(1,innerWidth-margin*2),availableHeight=Math.max(1,innerHeight-margin*2),rect=panel.getBoundingClientRect(),width=Math.min(Math.max(1,rect.width||390),availableWidth),height=Math.min(Math.max(1,rect.height||40),availableHeight),maximumLeft=Math.max(margin,innerWidth-width-margin),maximumTop=Math.max(margin,innerHeight-height-margin),requestedLeft=Number.isFinite(Number(left))?Number(left):margin,requestedTop=Number.isFinite(Number(top))?Number(top):margin;return{left:Math.min(maximumLeft,Math.max(margin,requestedLeft)),top:Math.min(maximumTop,Math.max(margin,requestedTop)),width};}
  function floatPanel(panel,layout=null,persist=true){if(panel.ownerDocument!==document)return;const rect=panel.getBoundingClientRect(),requestedWidth=Number(layout?.width)||rect.width||390,availableWidth=Math.max(1,innerWidth-16),left=Number.isFinite(Number(layout?.left))?Number(layout.left):rect.left,top=Number.isFinite(Number(layout?.top))?Number(layout.top):rect.top;panel.classList.add('floating-panel');document.body.append(panel);panel.style.width=`${Math.min(Math.max(Math.min(280,availableWidth),requestedWidth),availableWidth)}px`;const position=clampPanelPosition(panel,left,top);panel.style.width=`${position.width}px`;panel.style.left=`${position.left}px`;panel.style.top=`${position.top}px`;panel.style.zIndex=String(++floatingPanelZ);if(persist)savePanelLayout(panel);requestAnimationFrame(resize);}
  function dockPanel(panel,persist=true,beforePanel=undefined){
    if(panel.ownerDocument!==document)return;
    panel.classList.remove('floating-panel','panel-dragging');
    for(const property of ['left','top','width','z-index'])panel.style.removeProperty(property);
    insertPanelInRail(panel,beforePanel);
    if(ui.rightRail.classList.contains('rail-collapsed'))setRightRailCollapsed(false,true);
    if(persist){savePanelLayout(panel);saveDockPanelOrder();}
    requestAnimationFrame(resize);
  }
  function pointOverRail(x,y){const rect=ui.rightRail.getBoundingClientRect();return x>=rect.left&&x<=rect.right&&y>=rect.top&&y<=rect.bottom;}
  function clampFloatingPanel(panel,persist=false){if(panel.hidden||!panel.classList.contains('floating-panel'))return;const rect=panel.getBoundingClientRect(),position=clampPanelPosition(panel,rect.left,rect.top);panel.style.width=`${position.width}px`;panel.style.left=`${position.left}px`;panel.style.top=`${position.top}px`;if(persist)savePanelLayout(panel);}
  function clampFloatingPanels(){for(const panel of document.querySelectorAll('.floating-panel'))clampFloatingPanel(panel);}
  function initializeDraggablePanels(){
    const panels=[...ui.rightRail.querySelectorAll(':scope > .panel'),...document.querySelectorAll('[data-popup]')].filter(panel=>!['titlePanel','serverConfigWindow'].includes(panel.id));
    const defaults=panels.filter(panel=>!panel.dataset.popup).map(panel=>panel.id);
    let savedOrder=[];try{const saved=JSON.parse(localStorage.getItem(dockOrderStorageKey)||'[]');if(Array.isArray(saved))savedOrder=saved;}catch{}
    dockPanelOrder=[...new Set([...savedOrder.filter(id=>defaults.includes(id)),...defaults])];
    for(const [index,id] of dockPanelOrder.entries()){
      const panel=document.getElementById(id);panel.dataset.dockOrder=String(index);
      ui.rightRail.insertBefore(panel,ui.serverConfigWindow);
    }
    const marker=document.createElement('div');marker.className='panel-drop-indicator';marker.hidden=true;marker.setAttribute('aria-hidden','true');document.body.append(marker);
    panels.forEach(panel=>{
      const handle=document.createElement('div'),label=document.createElement('span'),hint=document.createElement('small');
      const name=panel.dataset.popup||panel.dataset.collapsible||panel.querySelector('strong,h2')?.textContent?.trim()||'Window';
      handle.className='panel-drag-handle';handle.tabIndex=panel.dataset.popup?-1:0;handle.setAttribute('role','button');
      handle.setAttribute('aria-label','Drag '+name+(panel.dataset.popup?' window to move it':' to reorder, move or dock it'));
      label.textContent=name;hint.textContent='drag';handle.append(label,hint);panel.prepend(handle);
      let drag=null;
      const placeAtPointer=event=>{
        const position=clampPanelPosition(panel,event.clientX-drag.offsetX,event.clientY-drag.offsetY);
        panel.style.width=position.width+'px';panel.style.left=position.left+'px';panel.style.top=position.top+'px';
      };
      const previewDrop=()=>{
        const overRail=!panel.dataset.popup&&pointOverRail(drag.clientX,drag.clientY);
        ui.rightRail.classList.toggle('panel-dock-target',overRail);marker.hidden=!overRail;drag.beforePanel=undefined;
        if(!overRail)return;
        const candidates=[...ui.rightRail.children].filter(item=>item!==panel&&item.matches('.panel[data-dock-order]')&&!item.hidden&&item.getClientRects().length);
        drag.beforePanel=candidates.find(item=>{const rect=item.getBoundingClientRect();return drag.clientY<rect.top+rect.height/2;})||null;
        const rail=ui.rightRail.getBoundingClientRect(),heading=ui.rightRail.querySelector('.right-rail-heading').getBoundingClientRect();
        const top=drag.beforePanel?drag.beforePanel.getBoundingClientRect().top-5:candidates.length?candidates[candidates.length-1].getBoundingClientRect().bottom+4:heading.bottom+9;
        marker.style.left=(rail.left+10)+'px';marker.style.width=Math.max(0,rail.width-24)+'px';
        marker.style.top=Math.min(rail.bottom-8,Math.max(heading.bottom+3,top))+'px';
      };
      const scrollWhileDragging=()=>{
        if(!drag?.active)return;
        if(!panel.dataset.popup&&pointOverRail(drag.clientX,drag.clientY)){
          const rail=ui.rightRail.getBoundingClientRect(),heading=ui.rightRail.querySelector('.right-rail-heading').getBoundingClientRect();
          const top=heading.bottom+36,bottom=rail.bottom-36;
          const speed=drag.clientY<top?-Math.min(14,(top-drag.clientY)/3):drag.clientY>bottom?Math.min(14,(drag.clientY-bottom)/3):0;
          if(speed){ui.rightRail.scrollTop+=speed;previewDrop();}
        }
        drag.frame=requestAnimationFrame(scrollWhileDragging);
      };
      const finish=(event,cancelled=false)=>{
        if(!drag||event.pointerId!==drag.pointerId)return;
        cancelAnimationFrame(drag.frame);
        if(drag.active&&!cancelled){drag.clientX=event.clientX;drag.clientY=event.clientY;previewDrop();}
        drag.placeholder?.remove();marker.hidden=true;ui.rightRail.classList.remove('panel-dock-target');panel.classList.remove('panel-dragging');
        if(drag.active){
          if(cancelled){if(drag.wasFloating)floatPanel(panel,drag.rect,false);else dockPanel(panel,false);}
          else if(drag.beforePanel!==undefined)dockPanel(panel,true,drag.beforePanel);
          else{placeAtPointer(event);savePanelLayout(panel);}
          event.preventDefault();event.stopPropagation();
        }
        drag=null;
        try{handle.releasePointerCapture(event.pointerId);}catch{}
      };
      handle.addEventListener('pointerdown',event=>{
        if(event.button!==0||panel.ownerDocument!==document||panel.classList.contains('panel-collapsed'))return;
        const rect=panel.getBoundingClientRect();
        drag={pointerId:event.pointerId,startX:event.clientX,startY:event.clientY,clientX:event.clientX,clientY:event.clientY,offsetX:event.clientX-rect.left,offsetY:event.clientY-rect.top,rect,active:false,wasFloating:panel.classList.contains('floating-panel')};
        handle.setPointerCapture(event.pointerId);
        if(drag.wasFloating)panel.style.zIndex=String(++floatingPanelZ);
        event.preventDefault();event.stopPropagation();
      });
      handle.addEventListener('pointermove',event=>{
        if(!drag||event.pointerId!==drag.pointerId)return;
        drag.clientX=event.clientX;drag.clientY=event.clientY;
        if(!drag.active&&Math.hypot(event.clientX-drag.startX,event.clientY-drag.startY)<5)return;
        if(!drag.active){
          drag.active=true;
          if(!drag.wasFloating){
            const placeholder=document.createElement('div');placeholder.className='panel-drag-placeholder';placeholder.style.height=drag.rect.height+'px';placeholder.setAttribute('aria-hidden','true');
            ui.rightRail.insertBefore(placeholder,panel);drag.placeholder=placeholder;
            floatPanel(panel,{left:drag.rect.left,top:drag.rect.top,width:drag.rect.width},false);
          }else panel.style.zIndex=String(++floatingPanelZ);
          try{handle.setPointerCapture(event.pointerId);}catch{}
          panel.classList.add('panel-dragging');drag.frame=requestAnimationFrame(scrollWhileDragging);
        }
        placeAtPointer(event);previewDrop();event.preventDefault();event.stopPropagation();
      });
      handle.addEventListener('pointerup',event=>finish(event));
      handle.addEventListener('pointercancel',event=>finish(event,true));
      handle.addEventListener('dblclick',event=>{
        event.preventDefault();event.stopPropagation();if(panel.dataset.popup)return;
        if(panel.classList.contains('floating-panel'))dockPanel(panel);else floatPanel(panel);
      });
      handle.addEventListener('keydown',event=>{
        if(event.key!=='Enter'&&event.key!==' ')return;
        event.preventDefault();if(panel.dataset.popup)return;
        if(panel.classList.contains('floating-panel'))dockPanel(panel);else floatPanel(panel);
      });
      const saved=savedPanelLayout(panel);if(!panel.dataset.popup&&saved?.floating)floatPanel(panel,saved,false);
    });
  }
  function setRightRailCollapsed(collapsed,persist=false){ui.rightRail.classList.toggle('rail-collapsed',collapsed);ui.rightRailToggle.textContent=collapsed?'‹':'›';ui.rightRailToggle.title=collapsed?'Expand control rail':'Collapse control rail';ui.rightRailToggle.setAttribute('aria-label',ui.rightRailToggle.title);ui.rightRailToggle.setAttribute('aria-expanded',String(!collapsed));if(persist)try{localStorage.setItem('alternative-reality-control-rail',collapsed?'collapsed':'expanded');}catch{}requestAnimationFrame(resize);}
  function initializeRightRail(){let collapsed=matchMedia('(max-width: 900px)').matches;try{const saved=localStorage.getItem('alternative-reality-control-rail');if(saved)collapsed=saved==='collapsed';}catch{}setRightRailCollapsed(collapsed);ui.rightRailToggle.addEventListener('click',()=>setRightRailCollapsed(!ui.rightRail.classList.contains('rail-collapsed'),true));}

  function viewportWidth(){
    if(state.frameViewportWidth!=null)return state.frameViewportWidth;
    const railLeft=ui.rightRail?.getBoundingClientRect().left;
    return Math.max(1,Math.floor(Number.isFinite(railLeft)?railLeft:innerWidth));
  }
  function resize() {
    const width=viewportWidth(),ratio = Math.min(devicePixelRatio || 1,state.renderPixelRatio||1.5); canvas.width = Math.round(width * ratio); canvas.height = Math.round(innerHeight * ratio);
    canvas.style.width = `${width}px`; canvas.style.height = `${innerHeight}px`; ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    lightCanvas.width=width;lightCanvas.height=innerHeight;
    document.documentElement.style.setProperty('--active-rail-width',`${Math.max(0,innerWidth-width)}px`);
    clampFloatingPanels();
  }
  function toScreen(point) {
    if(state.dungeon?.underwater)return Scuba.project(point,state.camera,state.scale,viewportWidth(),innerHeight);
    const dx = point.x - state.camera.x, dy = point.y - state.camera.y;
    const relief=state.dungeon?0:(elevationAt(point.x,point.y)-cameraElevation())*elevationPixelsPerMeter();
    return { x: viewportWidth() / 2 + (dx + dy * state.shear) * state.scale, y: innerHeight / 2 - dy * state.scale * state.pitch-relief };
  }
  function toWorld(point) {
    if(state.dungeon?.underwater)return Scuba.unproject(point,state.camera,state.scale,viewportWidth(),innerHeight);
    let dy = -(point.y - innerHeight / 2) / (state.scale * state.pitch),x=state.camera.x + (point.x - viewportWidth() / 2) / state.scale - dy * state.shear;
    if(!state.dungeon&&state.elevationGrid)for(let iteration=0;iteration<3;iteration++){const relief=(elevationAt(x,state.camera.y+dy)-cameraElevation())*elevationPixelsPerMeter();dy=-(point.y-innerHeight/2+relief)/(state.scale*state.pitch);x=state.camera.x+(point.x-viewportWidth()/2)/state.scale-dy*state.shear;}
    return { x, y: state.camera.y + dy };
  }
  const elevationPixelsPerMeter=()=>Math.min(3.2,Math.max(.12,state.scale*.055));
  const lod = () => state.scale >= 9 ? 2 : state.scale >= 3.5 ? 1 : 0;
  function viewBounds(margin = 10) {
    const width=viewportWidth(),points = [toWorld({x:0,y:0}), toWorld({x:width,y:0}), toWorld({x:0,y:innerHeight}), toWorld({x:width,y:innerHeight})];
    return { minX: Math.min(...points.map(p=>p.x))-margin, maxX: Math.max(...points.map(p=>p.x))+margin, minY: Math.min(...points.map(p=>p.y))-margin, maxY: Math.max(...points.map(p=>p.y))+margin };
  }
  const fogCellSize=32;
  function fogKey(x,y){return`${x},${y}`;}
  function loadedAreas(snapshot=state.snapshot){if(!snapshot)return[];return snapshot.loadedAreas?.length?snapshot.loadedAreas:[snapshot.bounds];}
  function areaKey(area){return`${area.minimumX.toFixed(2)},${area.minimumY.toFixed(2)},${area.maximumX.toFixed(2)},${area.maximumY.toFixed(2)}`;}
  function areaCellKey(area){const base=state.snapshot?.reality?.area?.bounds,size=Number(state.snapshot?.reality?.area?.sizeMeters||2000);if(!base)return'0:0';return`${Math.round((area.minimumX-base.minimumX)/size)}:${Math.round((area.minimumY-base.minimumY)/size)}`;}
  function pointInLoadedArea(point){return loadedAreas().some(area=>point.x>=area.minimumX&&point.x<=area.maximumX&&point.y>=area.minimumY&&point.y<=area.maximumY);}
  function areaForPoint(point){const base=state.snapshot?.reality?.area?.bounds||loadedAreas()[0];const size=Number(state.snapshot?.reality?.area?.sizeMeters||2000);if(!base)return null;const x=Math.floor((point.x-base.minimumX)/size),y=Math.floor((point.y-base.minimumY)/size);return{minimumX:base.minimumX+x*size,minimumY:base.minimumY+y*size,maximumX:base.minimumX+(x+1)*size,maximumY:base.minimumY+(y+1)*size};}
  function beginAreaLoading(point,message='Loading and generating visible area…',blocksMovement=true){state.areaLoading=true;state.loadingArea=areaForPoint(point);state.loadingOrigin=state.players.get(state.playerId)?.position||state.camera;state.loadingStarted=performance.now();setWorldTask(message,blocksMovement);showToast(message);}
  async function requestNearbyPrefetch(area,origin){if(!area||state.dungeon)return;const key=areaCellKey(area);if(state.prefetchRequested.has(key))return;state.prefetchRequested.add(key);const taskKey=`area-prefetch:${key}`,center={x:(area.minimumX+area.maximumX)/2,y:(area.minimumY+area.maximumY)/2},from=origin||state.players.get(state.playerId)?.position||center;setPerformanceTask(taskKey,`Preparing nearby map blocks around ${key}`);try{const response=await fetch('/api/world/prefetch',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({x:center.x,y:center.y,originX:from.x,originY:from.y})});if(!response.ok)state.prefetchRequested.delete(key);else{const result=await response.json();if(result.alreadyRunning)state.prefetchRequested.delete(key);}}catch{state.prefetchRequested.delete(key);}finally{setPerformanceTask(taskKey,null);refreshServerDiagnostics();}}
  function fogArea(area){const minX=Math.floor(area.minimumX/fogCellSize),maxX=Math.ceil(area.maximumX/fogCellSize),minY=Math.floor(area.minimumY/fogCellSize),maxY=Math.ceil(area.maximumY/fogCellSize);for(let x=minX;x<maxX;x++)for(let y=minY;y<maxY;y++){const cx=(x+.5)*fogCellSize,cy=(y+.5)*fogCellSize;if(cx>=area.minimumX&&cx<=area.maximumX&&cy>=area.minimumY&&cy<=area.maximumY)state.outdoorFog.add(fogKey(x,y));}}
  function initializeFogForAreas(areas){for(const area of areas){const key=areaCellKey(area);if(state.fogInitializedAreas.has(key))continue;state.fogInitializedAreas.add(key);if(!state.revealedWorldAreas.has(key))fogArea(area);}clearMappedFog();}
  function clearMappedFog(){for(const area of loadedAreas()){if(!state.revealedWorldAreas.has(areaCellKey(area)))continue;const minX=Math.floor(area.minimumX/fogCellSize),maxX=Math.ceil(area.maximumX/fogCellSize),minY=Math.floor(area.minimumY/fogCellSize),maxY=Math.ceil(area.maximumY/fogCellSize);for(let x=minX;x<maxX;x++)for(let y=minY;y<maxY;y++)state.outdoorFog.delete(fogKey(x,y));}}
  function markNewAreaFog(previous,next){
    const existing=new Set(previous.map(areaKey));
    for(const area of next){if(existing.has(areaKey(area)))continue;state.fogInitializedAreas.add(areaCellKey(area));if(!state.revealedWorldAreas.has(areaCellKey(area)))fogArea(area);}clearMappedFog();
  }
  function drawOutdoorFog(view,me){
    if(!state.outdoorFog.size||!me||me.godMode)return;
    const px=Math.floor(me.position.x/fogCellSize),py=Math.floor(me.position.y/fogCellSize);for(let x=px-1;x<=px+1;x++)for(let y=py-1;y<=py+1;y++)state.outdoorFog.delete(fogKey(x,y));
    const minX=Math.floor(view.minX/fogCellSize),maxX=Math.ceil(view.maxX/fogCellSize),minY=Math.floor(view.minY/fogCellSize),maxY=Math.ceil(view.maxY/fogCellSize);ctx.save();ctx.fillStyle='rgba(6,9,11,.965)';
    for(let x=minX;x<maxX;x++)for(let y=minY;y<maxY;y++){if(!state.outdoorFog.has(fogKey(x,y)))continue;const corners=[toScreen({x:x*fogCellSize,y:y*fogCellSize}),toScreen({x:(x+1)*fogCellSize,y:y*fogCellSize}),toScreen({x:(x+1)*fogCellSize,y:(y+1)*fogCellSize}),toScreen({x:x*fogCellSize,y:(y+1)*fogCellSize})];ctx.beginPath();ctx.moveTo(corners[0].x,corners[0].y);for(const p of corners.slice(1))ctx.lineTo(p.x,p.y);ctx.closePath();ctx.fill();}
    ctx.restore();
  }
  function drawWorldBlocks(view){ctx.save();ctx.strokeStyle='rgba(238,224,173,.16)';ctx.lineWidth=1;ctx.setLineDash([8,12]);for(const area of loadedAreas()){if(area.maximumX<view.minX||area.minimumX>view.maxX||area.maximumY<view.minY||area.minimumY>view.maxY)continue;const points=[toScreen({x:area.minimumX,y:area.minimumY}),toScreen({x:area.maximumX,y:area.minimumY}),toScreen({x:area.maximumX,y:area.maximumY}),toScreen({x:area.minimumX,y:area.maximumY})];ctx.beginPath();ctx.moveTo(points[0].x,points[0].y);points.slice(1).forEach(point=>ctx.lineTo(point.x,point.y));ctx.closePath();ctx.stroke();}ctx.restore();}
  function firstUnloadedAreaInView(view){const base=state.snapshot?.reality?.area?.bounds,size=Number(state.snapshot?.reality?.area?.sizeMeters||2000);if(!base)return null;const loaded=loadedAreas();const minX=Math.floor((view.minX-base.minimumX)/size),maxX=Math.floor((view.maxX-base.minimumX)/size),minY=Math.floor((view.minY-base.minimumY)/size),maxY=Math.floor((view.maxY-base.minimumY)/size),candidates=[];for(let x=minX;x<=maxX;x++)for(let y=minY;y<=maxY;y++){const area={minimumX:base.minimumX+x*size,minimumY:base.minimumY+y*size,maximumX:base.minimumX+(x+1)*size,maximumY:base.minimumY+(y+1)*size};if(loaded.some(item=>areaCellKey(item)===`${x}:${y}`))continue;candidates.push(area);}return candidates.sort((a,b)=>Math.hypot((a.minimumX+a.maximumX)/2-state.camera.x,(a.minimumY+a.maximumY)/2-state.camera.y)-Math.hypot((b.minimumX+b.maximumX)/2-state.camera.x,(b.minimumY+b.maximumY)/2-state.camera.y))[0]||null;}
  function drawLoadingBoundary(now){
    const area=state.loadingArea;if(!state.areaLoading||!area)return;const corners=[{x:area.minimumX,y:area.minimumY},{x:area.maximumX,y:area.minimumY},{x:area.maximumX,y:area.maximumY},{x:area.minimumX,y:area.maximumY}],screen=corners.map(toScreen),pulse=.72+Math.sin(now/180)*.2,seconds=Math.max(0,Math.floor((now-state.loadingStarted)/1000)),label=`LOADING MAP · ${seconds}s`;
    ctx.save();ctx.fillStyle='rgba(18,24,26,.36)';ctx.beginPath();ctx.moveTo(screen[0].x,screen[0].y);for(const point of screen.slice(1))ctx.lineTo(point.x,point.y);ctx.closePath();ctx.fill();ctx.strokeStyle=`rgba(255,211,92,${pulse})`;ctx.lineWidth=4;ctx.setLineDash([18,10]);ctx.stroke();ctx.setLineDash([]);
    const viewport=viewportWidth();for(let edge=0;edge<4;edge++){const a=corners[edge],b=corners[(edge+1)%4],length=Math.hypot(b.x-a.x,b.y-a.y),steps=Math.max(1,Math.ceil(length/90));for(let step=0;step<=steps;step++){const amount=step/steps,p=toScreen({x:a.x+(b.x-a.x)*amount,y:a.y+(b.y-a.y)*amount});if(p.x<55||p.x>viewport-55||p.y<18||p.y>innerHeight-18)continue;const next=toScreen({x:a.x+(b.x-a.x)*Math.min(1,amount+.01),y:a.y+(b.y-a.y)*Math.min(1,amount+.01)}),angle=Math.atan2(next.y-p.y,next.x-p.x);ctx.save();ctx.translate(p.x,p.y);ctx.rotate(angle);ctx.font='700 11px monospace';ctx.textAlign='center';ctx.textBaseline='middle';const width=ctx.measureText(label).width+14;ctx.fillStyle='rgba(25,25,19,.9)';ctx.fillRect(-width/2,-10,width,20);ctx.strokeStyle='#f4cf62';ctx.lineWidth=1;ctx.strokeRect(-width/2,-10,width,20);ctx.fillStyle='#fff0a3';ctx.fillText(label,0,1);ctx.restore();}}
    ctx.restore();
  }
  function boundsOf(entity) {
    const points = entity.geometry?.length ? entity.geometry : [entity.position];
    return { minX: Math.min(...points.map(p=>p.x)), maxX: Math.max(...points.map(p=>p.x)), minY: Math.min(...points.map(p=>p.y)), maxY: Math.max(...points.map(p=>p.y)) };
  }
  function visible(entity, view) { const b = entity._bounds || boundsOf(entity); return b.maxX >= view.minX && b.minX <= view.maxX && b.maxY >= view.minY && b.minY <= view.maxY; }
  const spatialCellSize=128,spatialKey=(x,y)=>`${x}:${y}`,roundedCoordinate=value=>Math.round(value*1000)/1000;
  function buildSpatialIndex(){
    state.spatial=new Map();
    for(const entity of state.base){
      const kindIndex=state.spatial.get(entity.kind)||new Map();state.spatial.set(entity.kind,kindIndex);
      const b=entity._bounds||boundsOf(entity),coverage=state.mapCoverage?.length?state.mapCoverage:[{minimumX:b.minX,minimumY:b.minY,maximumX:b.maxX,maximumY:b.maxY}],seen=new Set();
      for(const area of coverage){
        const minX=Math.floor(Math.max(b.minX,area.minimumX-50)/spatialCellSize),maxX=Math.floor(Math.min(b.maxX,area.maximumX+50)/spatialCellSize),minY=Math.floor(Math.max(b.minY,area.minimumY-50)/spatialCellSize),maxY=Math.floor(Math.min(b.maxY,area.maximumY+50)/spatialCellSize);
        for(let x=minX;x<=maxX;x++)for(let y=minY;y<=maxY;y++){
          const key=spatialKey(x,y);if(seen.has(key))continue;seen.add(key);
          const bucket=kindIndex.get(key)||[];if(!kindIndex.has(key))kindIndex.set(key,bucket);bucket.push(entity);
        }
      }
    }
  }
  function visibleEntities(kind,view){const kindIndex=state.spatial.get(kind);if(!kindIndex)return[];const result=[],seen=new Set(),minX=Math.floor(view.minX/spatialCellSize),maxX=Math.floor(view.maxX/spatialCellSize),minY=Math.floor(view.minY/spatialCellSize),maxY=Math.floor(view.maxY/spatialCellSize);for(let x=minX;x<=maxX;x++)for(let y=minY;y<=maxY;y++)for(const entity of kindIndex.get(spatialKey(x,y))||[]){if(seen.has(entity.id)||!visible(entity,view))continue;seen.add(entity.id);result.push(entity);}return result;}
  function renderListsFor(view){const lists={};for(const kind of ['terrain','sidewalk','road','water','airport','stateBoundary','pointOfInterest','building','tree','bush','fence','vehicle','streetLight','resourceNode','door'])lists[kind]=visibleEntities(kind,view);return lists;}
  function renderList(kind){return state.currentRenderLists[kind]||state.lists[kind]||[];}
  function buildElevationGrid(samples){
    if(!samples.length){state.elevationGrid=null;return;}
    const xs=[...new Set(samples.map(sample=>roundedCoordinate(sample.x)))].sort((a,b)=>a-b),ys=[...new Set(samples.map(sample=>roundedCoordinate(sample.y)))].sort((a,b)=>a-b),values=new Map();
    for(const sample of samples){const x=roundedCoordinate(sample.x),y=roundedCoordinate(sample.y);if(!values.has(x))values.set(x,new Map());values.get(x).set(y,Number(sample.elevationMeters)||0);}
    state.elevationGrid={xs,ys,values,tree:buildElevationTree(samples.map((sample,index)=>({sample,index}))),cache:new Map()};
  }
  // Keep the original nearest-sample result, including input-order ties, in sparse areas.
  function buildElevationTree(entries,depth=0){
    if(!entries.length)return null;
    const axis=depth%2?'y':'x';entries.sort((a,b)=>a.sample[axis]-b.sample[axis]||a.index-b.index);
    const middle=entries.length>>1;
    return {...entries[middle],axis,left:buildElevationTree(entries.slice(0,middle),depth+1),right:buildElevationTree(entries.slice(middle+1),depth+1)};
  }
  function nearestElevation(tree,x,y){
    let best=null,distance=Infinity;
    function visit(node){
      if(!node)return;
      const sample=node.sample,d=(sample.x-x)**2+(sample.y-y)**2;
      if(d<distance||d===distance&&(!best||node.index<best.index)){best=node;distance=d;}
      const delta=(node.axis==='x'?x:y)-sample[node.axis],near=delta<=0?node.left:node.right,far=delta<=0?node.right:node.left;
      visit(near);if(delta*delta<=distance)visit(far);
    }
    visit(tree);return Number(best?.sample.elevationMeters)||0;
  }
  function elevationBracket(values,value){if(value<=values[0])return[values[0],values[0]];if(value>=values[values.length-1])return[values[values.length-1],values[values.length-1]];let low=0,high=values.length-1;while(high-low>1){const middle=(low+high)>>1;if(values[middle]<=value)low=middle;else high=middle;}return[values[low],values[high]];}
  function elevationAt(x,y){
    const grid=state.elevationGrid;if(!grid)return 0;
    // Exact coordinates: memoization must not flatten or quantize the terrain.
    const key=`${x}:${y}`,cached=grid.cache.get(key);if(cached!==undefined)return cached;
    const[x0,x1]=elevationBracket(grid.xs,x),[y0,y1]=elevationBracket(grid.ys,y),column0=grid.values.get(x0),column1=grid.values.get(x1),z00=column0?.get(y0),z10=column1?.get(y0),z01=column0?.get(y1),z11=column1?.get(y1);
    let height;
    if([z00,z10,z01,z11].every(Number.isFinite)){const tx=x1===x0?0:(x-x0)/(x1-x0),ty=y1===y0?0:(y-y0)/(y1-y0),bottom=z00+(z10-z00)*tx,top=z01+(z11-z01)*tx;height=bottom+(top-bottom)*ty;}
    else height=nearestElevation(grid.tree,x,y);
    // Bound memory as the player travels; replacing the elevation grid also clears it.
    if(grid.cache.size>=32768)grid.cache.clear();grid.cache.set(key,height);return height;
  }
  function cameraElevation(){
    const {x,y}=state.camera,grid=state.elevationGrid,cached=state.cameraElevation;
    if(cached&&cached.grid===grid&&cached.x===x&&cached.y===y)return cached.height;
    const height=elevationAt(x,y);state.cameraElevation={grid,x,y,height};return height;
  }
  function terrainHillshade(x,y,tile){if(!state.elevationGrid)return 0;const step=Math.max(8,tile),east=elevationAt(x+step,y)-elevationAt(x-step,y),north=elevationAt(x,y+step)-elevationAt(x,y-step);return Math.max(-1,Math.min(1,(north-east)/(step*.22)))*18;}
  function recordNetworkMessage(data){const perf=state.performance;perf.messages++;perf.lastMessageAt=performance.now();perf.lastPayloadBytes=typeof data==='string'?data.length:data?.byteLength||0;perf.bytesReceived+=perf.lastPayloadBytes;}
  function finishPerformanceFrame(start,now,visibleCount){const perf=state.performance,duration=performance.now()-start;perf.frames++;perf.averageRenderMs=perf.averageRenderMs?perf.averageRenderMs*.92+duration*.08:duration;perf.maximumRenderMs=Math.max(duration,perf.maximumRenderMs*.995);perf.visibleEntities=visibleCount;if(now-perf.lastFpsAt>=1000){perf.fps=perf.frames*1000/(now-perf.lastFpsAt);perf.frames=0;perf.lastFpsAt=now;}if(now-perf.lastNetworkAt>=1000){perf.messagesPerSecond=(perf.messages-perf.lastMessageCount)*1000/(now-perf.lastNetworkAt);perf.lastMessageCount=perf.messages;perf.lastNetworkAt=now;}if(now-perf.lastPanelAt>=500){perf.lastPanelAt=now;updatePerformancePanel(now);}}
  function updatePerformancePanel(now=performance.now()){
    const perf=state.performance,total=state.base.length,lastMessage=perf.lastMessageAt?Math.max(0,(now-perf.lastMessageAt)/1000):0,server=perf.server;
    const tasks=[...perf.tasks.values()].map(task=>`${task.message.replace(/…/g,'')} · ${Math.max(0,(now-task.startedAt)/1000).toFixed(1)}s`);
    const serverTasks=Array.isArray(server?.activeOperations)?server.activeOperations:server?.activeOperation&&server.activeOperation!=='Idle'?[server.activeOperation]:[];
    for(const task of serverTasks)if(!tasks.some(existing=>existing.startsWith(task)))tasks.push(task);
    ui.performanceActivity.replaceChildren(...(tasks.length?tasks:[state.socket?.readyState===WebSocket.OPEN?'Idle':'Disconnected']).map((task,index)=>{const item=document.createElement('li');item.textContent=task;if(!tasks.length)item.className='performance-idle';item.dataset.taskIndex=String(index);return item;}));
    ui.performanceFps.textContent=`${perf.fps.toFixed(0)} FPS`;ui.performanceFps.className=perf.fps&&perf.fps<24?'performance-bad':perf.fps<29?'performance-warn':'performance-good';ui.performanceFrame.textContent=`${perf.averageRenderMs.toFixed(1)} ms avg · ${perf.maximumRenderMs.toFixed(0)} max`;ui.performanceEntities.textContent=`${perf.visibleEntities.toLocaleString()} visible / ${total.toLocaleString()} loaded`;ui.performanceBlocks.textContent=`${loadedAreas().length} active${server?.preparedAreas?` · ${server.preparedAreas} newly prepared`:''}${state.areaLoading?' · 1 loading':''}`;ui.performanceNetwork.textContent=`${perf.messagesPerSecond.toFixed(1)} msg/s · ${(perf.bytesReceived/1048576).toFixed(1)} MB · ${lastMessage.toFixed(1)}s ago`;ui.performanceServer.textContent=server?`${server.workingSetMegabytes.toFixed(0)} MB process · ${server.managedMegabytes.toFixed(0)} MB managed`:'Checking…';ui.performanceLoad.textContent=serverTasks.length?serverTasks.join(' · '):server?.lastAreaLoadMilliseconds!=null?`${server.lastAreaLoadMilliseconds.toLocaleString()} ms activate · ${(server.lastAreaPrefetchMilliseconds||0).toLocaleString()} ms prep`:'—';
    updateRenderResolution(now);
    const slowest=Object.entries(perf.stages||{}).sort((a,b)=>b[1]-a[1])[0];if(slowest)ui.performanceFrame.textContent+=` · ${slowest[0]} ${slowest[1].toFixed(1)} ms`;
    if(server?.timings){const a=server.timings['actors.tick'],b=server.timings['transit.tick'];ui.performanceServer.title=ui.performanceServer.textContent;ui.performanceServer.textContent=`Actors ${(a?.lastMilliseconds||0).toFixed(1)} ms · buses ${(b?.lastMilliseconds||0).toFixed(1)} ms · ${server.actorRouteWorkers} route workers · ping ${(perf.pingMilliseconds||0).toFixed(0)} ms`;ui.performanceLoad.title=Object.entries(server.timings).map(([name,t])=>`${name}: last ${t.lastMilliseconds.toFixed(1)} ms, peak ${t.maximumMilliseconds.toFixed(1)} ms, ${t.slowCount} over 100 ms`).join('\n');}
  }
  async function refreshServerDiagnostics(){try{const response=await fetch('/api/diagnostics',{cache:'no-store'});state.performance.server=response.ok?await response.json():null;}catch{state.performance.server=null;}updatePerformancePanel();}
  function updateRenderResolution(now){
    if(document.hidden||!state.snapshot)return;
    const perf=state.performance,ceiling=Math.min(devicePixelRatio||1,1.5),current=state.renderPixelRatio||ceiling;
    const slow=perf.fps>0&&(perf.fps<24||perf.averageRenderMs>22),fast=perf.fps>=29&&perf.averageRenderMs<8;
    if(slow){state.renderFastSince=null;state.renderSlowSince??=now;if(now-state.renderSlowSince>=2500&&current>Math.min(1,ceiling)){state.renderPixelRatio=Math.max(Math.min(1,ceiling),current-.25);state.renderSlowSince=now;resize();}}
    else{state.renderSlowSince=null;if(fast){state.renderFastSince??=now;if(now-state.renderFastSince>=20000&&current<ceiling){state.renderPixelRatio=Math.min(ceiling,current+.25);state.renderFastSince=now;resize();}}else state.renderFastSince=null;}
  }
  function path(entity, close = false) {
    if (!entity.geometry?.length) return false; ctx.beginPath();
    entity.geometry.forEach((point, index) => { const p = toScreen(point); index ? ctx.lineTo(p.x,p.y) : ctx.moveTo(p.x,p.y); });
    if (close) {
      ctx.closePath();
      for(const ring of entity.interiorRings||[]){ring.forEach((point,index)=>{const p=toScreen(point);index?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y);});ctx.closePath();}
    }
    return true;
  }
  function drawGeometry(entity, fill, stroke, width = 1, close = false) {
    if (!path(entity, close)) return; if (fill) { ctx.fillStyle = fill; ctx.fill(entity.interiorRings?.length ? 'evenodd' : 'nonzero'); } if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = width; ctx.lineJoin='round'; ctx.lineCap='round'; ctx.stroke(); }
  }
  function prop(entity, name, fallback) { const value = Number(entity.properties?.[name]); return Number.isFinite(value) ? value : fallback; }
  function hash(value) { let h=2166136261; for (let i=0;i<String(value).length;i++) h=Math.imul(h^String(value).charCodeAt(i),16777619); return (h>>>0)/4294967295; }

  function shouldRenderFrame(now){
    const interval=1000/30;
    if(state.nextRenderAt==null)state.nextRenderAt=now;
    if(now+.1<state.nextRenderAt)return false;
    // Preserve pacing on high-refresh displays, but never catch up missed frames after a stall.
    state.nextRenderAt=now-state.nextRenderAt>=interval?now+interval:state.nextRenderAt+interval;
    state.renderDeltaMs=state.lastRenderAt==null?1000/60:Math.max(0,now-state.lastRenderAt);
    state.lastRenderAt=now;return true;
  }
  function cameraFollowAmount(deltaMs){return 1-Math.pow(.86,Math.min(250,deltaMs)/(1000/60));}
  function updateBusCamera(me,now){
    const bus=me?.ridingBusId?state.transit?.buses?.find(bus=>bus.id===me.ridingBusId):null;
    if(!bus||!(bus.speedMetersPerSecond>0)){state.busCameraRecenterAt=null;state.busCameraRideId=null;return;}
    if(state.busCameraRideId!==bus.id){state.busCameraRideId=bus.id;state.busCameraRecenterAt=now+3000;}
    // Let riders pan around, then smoothly resume following without changing their zoom.
    if(state.follow||state.pointer.down){state.busCameraRecenterAt=now+3000;return;}
    if(now>=state.busCameraRecenterAt){state.follow=true;state.busCameraRecenterAt=now+3000;}
  }
  function drawRetroBattle(now,width,height){
    state.path=[];state.target=null;state.followCommand=null;
    RetroBattles.draw(ctx,state.dungeon,width,height,(now-(state.retroReceivedAt??now))/1000,now);
  }
  function render(now) {
    requestAnimationFrame(render);if(!shouldRenderFrame(now))return;
    const renderStarted=performance.now();state.frame++;
    state.frameViewportWidth=viewportWidth();
    try {
    const viewport=viewportWidth();ctx.clearRect(0,0,viewport,innerHeight); ctx.fillStyle='#315a38'; ctx.fillRect(0,0,viewport,innerHeight);
    if (!state.snapshot){connecting();finishPerformanceFrame(renderStarted,now,0);return;}
    const me = state.players.get(state.playerId);Inversions.tick();
    updateBusControls(me);
    updateBusCamera(me,now);
    if (me && state.follow) { const target=flightCameraTarget(me),amount=cameraFollowAmount(state.renderDeltaMs);state.camera.x += (target.x-state.camera.x)*amount; state.camera.y += (target.y-state.camera.y)*amount; }
    const view = viewBounds(16), detail = lod();
    if(!controlsPaused())maintainMapWindow(view,me,now);
    const visibleUnloaded=!controlsPaused()&&me?.locationId==='outdoor'?firstUnloadedAreaInView(view):null;
    if(visibleUnloaded&&!state.areaLoading&&!state.worldBusy&&Date.now()-(state.lastAreaRequest||0)>1200){state.lastAreaRequest=Date.now();const point={x:(visibleUnloaded.minimumX+visibleUnloaded.maximumX)/2,y:(visibleUnloaded.minimumY+visibleUnloaded.maximumY)/2};beginAreaLoading(point,'Loading and generating visible area…',false);send({type:'requestArea',x:point.x,y:point.y});}
    Casino.sync();
    if(Casino.draw(ctx,viewport,innerHeight,now)){updateFrameTelemetry(me,now);drawMiniMap(me);finishPerformanceFrame(renderStarted,now,5);return;}
    if (Inversions.battleScene(ctx,viewport,innerHeight,now)) {updateFrameTelemetry(me,now);drawMiniMap(me);finishPerformanceFrame(renderStarted,now,2);return;}
    if (state.dungeon?.retroBattle) { drawRetroBattle(now,viewport,innerHeight);finishPerformanceFrame(renderStarted,now,state.dungeon.retroBattle.enemies.length);return; }
    if (me?.locationId !== 'outdoor' && state.dungeon) {
      const visibleActors=(state.dungeon.actors||[]).filter(actor=>pointVisible(actor.position,view)).length;drawDungeon(state.dungeon,view,detail,now); drawProjectiles(now);drawFlamethrowerStreams(now);drawProbulatorBeams(now);drawExplosiveEffects(now);drawAreaHazards(now);drawSleepMarkers(now);combatEffects.draw(now,me.locationId);drawDamageIndicators(now);drawLaser(me); drawSpeechBubbles(view); updateFrameTelemetry(me,now);drawMiniMap(me);syncLightControls(me);finishPerformanceFrame(renderStarted,now,visibleActors+(state.dungeon.furnishings||[]).length);return;
    }
    const lists=renderListsFor(view);state.currentRenderLists=lists;const visibleCount=Object.values(lists).reduce((count,entities)=>count+entities.length,0);
    measureRenderStage('Ground',()=>drawCachedGround(view,detail));
    for (const e of [...lists.water].sort((a,b)=>Number(waterGeometryClosed(a))-Number(waterGeometryClosed(b)))) drawWater(e,detail,now);
    drawSidewalkNetwork(lists.sidewalk.filter(e=>e.properties?.bridge&&e.properties.bridge!=='no'),view,detail);
    drawRoadNetwork(lists.road.filter(e=>e.properties?.bridge&&e.properties.bridge!=='no'),view,detail);
    drawAirports(lists.airport,view,detail);drawWorldBlocks(view);drawStateBoundaries(lists.stateBoundary,view,detail);drawPointsOfInterest(lists.pointOfInterest,view,detail);
    drawPlayerRanges(me);
    drawTarget(now);
    drawProbulatorBeams(now);
    measureRenderStage('Buildings and actors',()=>drawRaised(view,detail,now));
    drawBusTransit(view);
    drawWindFlags(view,now);
    drawQuestMarkers(view,now);
    drawStreetNames(lists.road,view,detail);
    drawChestsAndLoot(view,now);
    drawProjectiles(now);
    drawFlamethrowerStreams(now);
    drawExplosiveEffects(now);
    drawAreaHazards(now);drawSleepMarkers(now);
    drawDamageIndicators(now);
    measureRenderStage('Lighting and fog',()=>{drawAtmosphere(me,detail,now);drawOutdoorFog(view,me);});drawLoadingBoundary(now);drawLaser(me);combatEffects.draw(now,me?.locationId||'outdoor');
    Inversions.overlay(ctx,now);drawSpeechBubbles(view);
    updateFrameTelemetry(me,now);drawMiniMap(me);syncLightControls(me);finishPerformanceFrame(renderStarted,now,visibleCount);
    } finally {state.frameViewportWidth=null;}
  }

  function revealedAt(x,y){
    if(state.players?.get(state.playerId)?.godMode)return true;
    const cells=state.dungeon?.revealedCells;if(!cells){state.revealedCellIndex=null;return false;}
    let cached=state.revealedCellIndex;
    if(!cached||cached.source!==cells||cached.size!==cells.length)state.revealedCellIndex=cached={source:cells,size:cells.length,keys:new Set(cells)};
    return cached.keys.has(`${Math.floor(x/3)},${Math.floor(y/3)}`);
  }
  function screenWind(weather,pitch,shear){
    // Bearings name where wind comes from; project its downwind vector like the map.
    const radians=((weather?.windDirectionDegrees??0)+180)*Math.PI/180;
    const east=Math.sin(radians),north=Math.cos(radians);
    return {x:east+north*shear,y:-north*pitch};
  }
  function precipitationVelocity(weather,pitch,shear,snow){
    const wind=screenWind(weather,pitch,shear),speed=Math.min(60,Math.max(0,weather?.windSpeedKilometersPerHour??0))*2;
    return {x:wind.x*speed,y:wind.y*speed+(snow?130:450)};
  }
  function precipitationParticle(index,seconds,width,height,velocity,snow){
    const random=(channel,cycle=0)=>{let n=Math.imul(index+1,0x9e3779b1)^Math.imul(channel+1,0x85ebca6b)^Math.imul(cycle,0xc2b2ae35);n=Math.imul(n^(n>>>16),0x7feb352d);n=Math.imul(n^(n>>>15),0x846ca68b);return ((n^(n>>>16))>>>0)/4294967296;};
    const w=Math.max(1,width),h=Math.max(1,height)+40,vy=velocity.y*(.7+random(0)*.65),life=h/vy,age=seconds+random(1)*life,cycle=Math.floor(age/life),phase=age-cycle*life;
    const vx=velocity.x*(snow?1:.25)+(random(2,cycle)-.5)*(snow?24:12),sway=snow?Math.sin(seconds*1.5+random(3)*6)*9:0;
    const x=((random(4,cycle)*w+vx*phase+sway)%w+w)%w,y=phase*vy-20,trail=snow?.015:.009+random(5,cycle)*.015;
    return {x,y,dx:vx*trail,dy:vy*trail,alpha:.3+random(6,cycle)*.55,width:snow?1+random(7):.6+random(7)*.6};
  }
  function drawWindFlags(view,now){if(state.dungeon)return;const wind=screenWind(state.weather,state.pitch,state.shear),dx=wind.x,dy=wind.y;for(const flag of renderList('resourceNode')){if(flag.properties?.subtype!=='windFlag'||!pointVisible(flag.position,view))continue;const p=toScreen(flag.position),h=Math.max(25,state.scale*2.5),length=Math.max(20,state.scale*1.6),flutter=Math.sin(now/150+hash(flag.id)*10)*3;ctx.save();ctx.strokeStyle='#d8dedb';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(p.x,p.y-h);ctx.stroke();ctx.fillStyle='#f3b447';ctx.beginPath();ctx.moveTo(p.x,p.y-h);ctx.lineTo(p.x+dx*length+flutter,p.y-h+dy*length);ctx.lineTo(p.x+dx*length*.65,p.y-h+dy*length*.65+8);ctx.lineTo(p.x,p.y-h+9);ctx.closePath();ctx.fill();ctx.restore();}}
  function drawWaterCreature(a,p,now){const monster=a.subtype==='waterMonster',s=Math.max(monster?16:5,state.scale*(monster?1.3:.28)),surface=.65+.35*Math.sin(now/1200+hash(a.id)*6);ctx.save();ctx.translate(p.x,p.y);ctx.scale(a.facing==='west'?-1:1,1);ctx.globalAlpha=.6+surface*.4;ctx.strokeStyle='#b7e8ed';ctx.lineWidth=1;ctx.beginPath();ctx.ellipse(0,0,s*1.8,s*.4,0,0,Math.PI*2);ctx.stroke();ctx.fillStyle=monster?'#367f6d':'#abd4cc';ctx.beginPath();ctx.ellipse(0,-s*.25,s*1.2,s*.45,0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.moveTo(-s,0);ctx.lineTo(-s*1.65,-s*.65);ctx.lineTo(-s*1.65,s*.3);ctx.closePath();ctx.fill();if(monster){ctx.beginPath();ctx.ellipse(s*.75,-s*.55,s*.4,s*.65,-.3,0,Math.PI*2);ctx.fill();ctx.fillStyle='#ffda76';ctx.beginPath();ctx.arc(s*.85,-s*.85,s*.09,0,Math.PI*2);ctx.fill();}ctx.restore();}
  function drawQuestMarkers(view,now){const quests=(state.privateState?.quests||[]).filter(quest=>['active','ready'].includes(quest.status)),targets=new Set(quests.flatMap(quest=>[quest.targetActorId,quest.destinationActorId,quest.giverId]).filter(Boolean));for(const actor of state.actors.values())if((actor.isQuestGiver||targets.has(actor.id))&&pointVisible(actor.position,view)){const p=toScreen(actor.position);ctx.fillStyle=actor.isQuestGiver?'#63ee81':'#ffe36d';ctx.strokeStyle='rgba(4,15,8,.9)';ctx.lineWidth=3;ctx.font='900 18px monospace';ctx.textAlign='center';const glyph=actor.isQuestGiver?'$':'◆',y=p.y-state.scale*1.7+Math.sin(now/220)*2;ctx.strokeText(glyph,p.x,y);ctx.fillText(glyph,p.x,y);}for(const id of targets){const entity=state.baseById.get(id);if(!entity||!pointVisible(entity.position,view))continue;const p=toScreen(entity.position);ctx.fillStyle='#ffe36d';ctx.font='bold 18px monospace';ctx.textAlign='center';ctx.fillText('◆',p.x,p.y-state.scale*1.6+Math.sin(now/220)*2);}}
  function dungeonCanExit(dungeon=state.dungeon){return !!dungeon&&!dungeon.underwater&&(dungeon.isHome||Number(dungeon.level||1)===1||Number(dungeon.level||1)===Number(dungeon.levelCount||1));}
  function dungeonFeatureAt(point,meters=1.7){if(!state.dungeon||state.dungeon.underwater)return null;const stairs=state.dungeon.stairs;if(stairs&&Number(state.dungeon.levelCount||1)>1&&Math.hypot(point.x-stairs.x,point.y-stairs.y)<=meters)return'stairs';if(dungeonCanExit()&&Math.hypot(point.x-state.dungeon.exit.x,point.y-state.dungeon.exit.y)<=meters)return'exit';return null;}
  function drawDungeon(dungeon,view,detail,now){
    if(dungeon.underwater){const me=state.players.get(state.playerId);Scuba.draw(ctx,dungeon,me,viewportWidth(),innerHeight,toScreen,state.scale,now,state.facings.get(me.id));drawChestsAndLoot(view,now);drawTarget(now);return;}
    ctx.fillStyle='#0b0e0d';ctx.fillRect(0,0,viewportWidth(),innerHeight);
    if(!dungeon.isHome&&!dungeon.isStore){const stronghold=Number(dungeon.difficulty||1)>50,label=`${stronghold?'STRONGHOLD':'DUNGEON'} · DIFFICULTY ${dungeon.difficulty||1} · LEVEL ${dungeon.level||1}/${dungeon.levelCount||1}`;ctx.fillStyle=stronghold?'#ffbd78':'#fff0b0';ctx.font='700 12px monospace';ctx.textAlign='center';ctx.fillText(label,viewportWidth()/2,25);}
    const footprint=(dungeon.footprint?.length>=3?dungeon.footprint:[{x:0,y:0},{x:dungeon.width,y:0},{x:dungeon.width,y:dungeon.height},{x:0,y:dungeon.height}]).map(toScreen);
    ctx.fillStyle='#5b554b';ctx.strokeStyle='#b19a72';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(footprint[0].x,footprint[0].y);footprint.slice(1).forEach(p=>ctx.lineTo(p.x,p.y));ctx.closePath();ctx.fill();ctx.stroke();
    if(dungeon.isHome&&dungeon.garage)HomeGarage.floor(ctx,dungeon.garage,toScreen);
    ctx.strokeStyle='#2b211b';ctx.lineWidth=Math.max(4,state.scale*.35);ctx.lineCap='square';
    for(const wall of dungeon.walls||[]){const a=toScreen({x:wall.x1,y:wall.y1}),b=toScreen({x:wall.x2,y:wall.y2});ctx.beginPath();if(wall.doorStart>=0){const vertical=Math.abs(wall.x1-wall.x2)<.01;if(vertical){const d1=toScreen({x:wall.x1,y:wall.doorStart}),d2=toScreen({x:wall.x1,y:wall.doorEnd});ctx.moveTo(a.x,a.y);ctx.lineTo(d1.x,d1.y);ctx.moveTo(d2.x,d2.y);ctx.lineTo(b.x,b.y);}else{const d1=toScreen({x:wall.doorStart,y:wall.y1}),d2=toScreen({x:wall.doorEnd,y:wall.y1});ctx.moveTo(a.x,a.y);ctx.lineTo(d1.x,d1.y);ctx.moveTo(d2.x,d2.y);ctx.lineTo(b.x,b.y);}}else{ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);}ctx.stroke();}
    if(dungeonCanExit(dungeon)){const exit=toScreen(dungeon.exit),door=toScreen(dungeon.doorway||{x:dungeon.exit.x,y:0});ctx.strokeStyle='#dff7ef';ctx.lineWidth=Math.max(5,state.scale*.3);ctx.beginPath();ctx.moveTo(door.x,door.y);ctx.lineTo(exit.x,exit.y);ctx.stroke();ctx.fillStyle='#a9d6d0';ctx.beginPath();ctx.arc(exit.x,exit.y,Math.max(7,state.scale*.55),0,Math.PI*2);ctx.fill();ctx.strokeStyle='#dff7ef';ctx.lineWidth=2;ctx.stroke();ctx.fillStyle='#10201d';ctx.font='bold 9px monospace';ctx.textAlign='center';const exitLabel=dungeon.isHome?'HOME DOOR':Number(dungeon.level||1)===Number(dungeon.levelCount||1)&&Number(dungeon.levelCount||1)>1?'LEAVE DUNGEON':'EXIT TO MAP';ctx.fillText(exitLabel,exit.x,exit.y+3);}
    if(dungeon.stairs&&Number(dungeon.levelCount||1)>1){const stairs=toScreen(dungeon.stairs),size=Math.max(13,state.scale*1.15);ctx.fillStyle='#7d684a';ctx.strokeStyle='#e2c68b';ctx.lineWidth=2;ctx.fillRect(stairs.x-size/2,stairs.y-size*.36,size,size*.72);ctx.strokeRect(stairs.x-size/2,stairs.y-size*.36,size,size*.72);for(let step=1;step<5;step++){const y=stairs.y-size*.36+step*size*.72/5;ctx.beginPath();ctx.moveTo(stairs.x-size/2,y);ctx.lineTo(stairs.x+size/2,y);ctx.stroke();}ctx.fillStyle='#fff0ba';ctx.font='bold 9px monospace';ctx.textAlign='center';ctx.fillText(`STAIRS ${dungeon.level}/${dungeon.levelCount}`,stairs.x,stairs.y-size*.55);}
    for(const item of dungeon.furnishings||[]){if(state.movingFurniture?.id===item.id&&state.furniturePreview)continue;drawFurnishing(item,now);}
    if(state.movingFurniture&&state.furniturePreview){const preview={...state.movingFurniture,position:{...state.movingFurniture.position,x:state.furniturePreview.x,y:state.furniturePreview.y}};drawFurnishing(preview,now,true);}
    if(dungeon.isHome&&dungeon.garage)HomeGarage.vehicles(ctx,dungeon.garage,toScreen,state.scale,state.pitch);
    Inversions.dungeon(ctx,dungeon);
    const me=state.players.get(state.playerId);drawPlayerRanges(me);
    const actors=dungeon.actors||[];for(const actor of actors){if(!revealedAt(actor.position.x,actor.position.y)||!dynamicVisible(actor.position))continue;if(actor.subtype==='giantGorilla')drawGorillaActor(actor,toScreen(actor.position),actor.isMoving,now);else drawActor(actor,detail,now);}
    drawChestsAndLoot(view,now);
    if(me)drawPlayer(me,true,detail,now);
    if(!dungeon.isHome){const cell=3;for(let y=0;y<dungeon.height;y+=cell)for(let x=0;x<dungeon.width;x+=cell){if(revealedAt(x+1.5,y+1.5))continue;const p=[toScreen({x,y}),toScreen({x:x+cell,y}),toScreen({x:x+cell,y:y+cell}),toScreen({x,y:y+cell})];ctx.fillStyle='rgba(1,4,3,.94)';ctx.beginPath();ctx.moveTo(p[0].x,p[0].y);p.slice(1).forEach(q=>ctx.lineTo(q.x,q.y));ctx.closePath();ctx.fill();}}
  }
  const furnitureColors={oak:'#a87542',walnut:'#684229',white:'#ddd8c8',black:'#292d2c',navy:'#314d6b',sage:'#71856a',blue:'#477ba0',teal:'#3f7c79',red:'#a84f45',tan:'#b5926b',burgundy:'#783b48',gray:'#737a78',brown:'#694a34',gold:'#b99345',rose:'#a96c77',green:'#557b52',glass:'#90b9bd',brass:'#b38d42',silver:'#9ba7a8',brick:'#8b4737',stone:'#77746c',terracotta:'#a95f3f'};
  function furnitureSize(item){const rotation=Number(item.properties?.rotationDegrees||0),swapped=Math.abs(Math.round(rotation/90)%2)===1,w=Number(item.properties?.widthMeters||.8),d=Number(item.properties?.depthMeters||.8);return swapped?{w:d,d:w}:{w,d};}
  function drawFurnishing(item,now,preview=false){const p=toScreen(item.position),type=item.properties?.objectType||'chair',size=furnitureSize(item),w=Math.max(8,size.w*state.scale),d=Math.max(6,size.d*state.scale*state.pitch),color=furnitureColors[item.properties?.color]||'#785538',pattern=item.properties?.pattern||'solid';ctx.save();ctx.globalAlpha=preview?.62:1;ctx.translate(p.x,p.y);ctx.fillStyle=color;ctx.strokeStyle='#30251d';ctx.lineWidth=2;
    if(type==='rug'){ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);drawFurniturePattern(w,d,pattern);ctx.restore();return;}
    ctx.fillStyle='rgba(0,0,0,.25)';ctx.beginPath();ctx.ellipse(2,d*.35,w*.52,d*.35,0,0,Math.PI*2);ctx.fill();ctx.fillStyle=color;
    if(type==='fireplace'){ctx.fillRect(-w/2,-d*.75,w,d*1.1);ctx.strokeRect(-w/2,-d*.75,w,d*1.1);ctx.fillStyle='#2a201b';ctx.fillRect(-w*.22,-d*.5,w*.44,d*.62);ctx.fillStyle='#ffb238';ctx.beginPath();ctx.arc(0,-d*.13,Math.max(3,Math.min(w,d)*.18)+Math.sin(now/130)*1.5,0,Math.PI*2);ctx.fill();const g=ctx.createRadialGradient(0,-d*.15,3,0,-d*.15,100);g.addColorStop(0,'rgba(255,170,65,.35)');g.addColorStop(1,'rgba(255,170,65,0)');ctx.fillStyle=g;ctx.beginPath();ctx.arc(0,-d*.15,100,0,Math.PI*2);ctx.fill();}
    else if(type==='bed'||type==='bunkBed'){ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);ctx.fillStyle='#eee6cf';ctx.fillRect(-w*.43,-d*.4,w*.32,d*.8);ctx.fillStyle=color;ctx.fillRect(-w*.08,-d*.4,w*.46,d*.8);drawFurniturePattern(w*.46,d*.8,pattern,w*.15,0);if(type==='bunkBed'){ctx.strokeStyle='#ddd2ac';ctx.lineWidth=3;ctx.strokeRect(-w*.48,-d*.62,w*.96,d*1.1);}}
    else if(type==='sofa'||type==='loveseat'||type==='armchair'||type==='recliner'||type==='ottoman'||type==='bench'){ctx.fillRect(-w/2,-d*.35,w,d*.7);ctx.strokeRect(-w/2,-d*.35,w,d*.7);if(type!=='ottoman'){ctx.fillStyle=shade(color,-22);ctx.fillRect(-w/2,-d*.58,w,d*.27);ctx.fillStyle=shade(color,18);ctx.fillRect(-w*.38,-d*.28,w*.76,d*.4);drawFurniturePattern(w*.76,d*.4,pattern);}}
    else if(['diningTable','coffeeTable','sideTable','desk'].includes(type)){ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);drawFurniturePattern(w,d,pattern);ctx.fillStyle=shade(color,-35);ctx.fillRect(-w*.4,d*.38,w*.12,d*.28);ctx.fillRect(w*.28,d*.38,w*.12,d*.28);}
    else if(type==='floorLamp'||type==='tableLamp'){ctx.strokeStyle='#534830';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(0,d*.35);ctx.lineTo(0,-d*.4);ctx.stroke();ctx.fillStyle=color;ctx.beginPath();ctx.moveTo(-w*.42,-d*.35);ctx.lineTo(w*.42,-d*.35);ctx.lineTo(w*.27,d*.05);ctx.lineTo(-w*.27,d*.05);ctx.closePath();ctx.fill();ctx.fillStyle='rgba(255,229,128,.3)';ctx.beginPath();ctx.arc(0,-d*.2,Math.max(w,d),0,Math.PI*2);ctx.fill();}
    else if(type==='plant'){ctx.fillStyle=color;ctx.fillRect(-w*.3,0,w*.6,d*.42);ctx.fillStyle='#3d7b48';for(let i=0;i<5;i++){ctx.beginPath();ctx.ellipse((i-2)*w*.12,-d*.2-Math.abs(i-2)*d*.08,w*.15,d*.42,i*.35,0,Math.PI*2);ctx.fill();}}
    else if(type==='piano'){ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);ctx.fillStyle='#eee9d8';for(let i=0;i<10;i++)ctx.fillRect(-w*.42+i*w*.084,d*.05,w*.065,d*.28);}
    else if(type==='stove'){ctx.fillStyle='#deded3';ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);ctx.fillStyle='#282d2d';for(const x of [-.25,.25])for(const y of [-.25,.25]){ctx.beginPath();ctx.ellipse(w*x,d*y,w*.13,d*.17,0,0,Math.PI*2);ctx.fill();}ctx.fillRect(-w*.3,d*.5,w*.6,d*.25);}
    else if(type==='kitchenSink'){ctx.fillStyle='#c1cccb';ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);ctx.fillStyle='#487888';ctx.fillRect(-w*.3,-d*.3,w*.6,d*.55);ctx.strokeStyle='#edf9f7';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(0,-d*.5);ctx.lineTo(0,-d*.12);ctx.lineTo(w*.15,-d*.12);ctx.stroke();}
    else if(type==='craftingTable'||type==='garageWorkbench'||type==='weaponsBench'){ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);ctx.fillStyle='#e5dab5';ctx.fillRect(-w*.28,-d*.3,w*.35,d*.5);ctx.strokeStyle='#342e25';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(w*.16,-d*.2);ctx.lineTo(w*.34,d*.25);ctx.moveTo(w*.1,-d*.15);ctx.lineTo(w*.28,-d*.35);ctx.stroke();}
    else if(type==='storageChest'){ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);drawFurniturePattern(w,d,pattern);ctx.strokeStyle=shade(color,-55);ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(-w/2,0);ctx.lineTo(w/2,0);ctx.stroke();ctx.fillStyle='#d6b45f';ctx.fillRect(-Math.max(2,w*.06),-Math.max(2,d*.1),Math.max(4,w*.12),Math.max(4,d*.2));}
    else if(['dresser','wardrobe','nightstand','bookshelf','cabinet','vanity','recordCabinet','grandfatherClock'].includes(type)){ctx.fillRect(-w/2,-d*.55,w,d*1.05);ctx.strokeRect(-w/2,-d*.55,w,d*1.05);drawFurniturePattern(w,d,pattern);ctx.strokeStyle=shade(color,-45);const rows=type==='wardrobe'||type==='bookshelf'?3:2;for(let i=1;i<rows;i++){ctx.beginPath();ctx.moveTo(-w*.45,-d*.5+i*d/rows);ctx.lineTo(w*.45,-d*.5+i*d/rows);ctx.stroke();}if(type==='wardrobe'){ctx.beginPath();ctx.moveTo(0,-d*.5);ctx.lineTo(0,d*.5);ctx.stroke();}if(type==='grandfatherClock'){ctx.fillStyle='#e8d39a';ctx.beginPath();ctx.arc(0,-d*.18,Math.min(w,d)*.25,0,Math.PI*2);ctx.fill();}}
    else{ctx.fillRect(-w/2,-d/2,w,d);ctx.strokeRect(-w/2,-d/2,w,d);drawFurniturePattern(w,d,pattern);}
    if(preview){ctx.setLineDash([5,4]);ctx.strokeStyle='#fff2a1';ctx.lineWidth=3;ctx.strokeRect(-w/2-3,-d/2-3,w+6,d+6);}ctx.restore();}
  function shade(hex,amount){if(!hex?.startsWith('#'))return hex;const n=parseInt(hex.slice(1),16),r=Math.max(0,Math.min(255,(n>>16)+amount)),g=Math.max(0,Math.min(255,((n>>8)&255)+amount)),b=Math.max(0,Math.min(255,(n&255)+amount));return`rgb(${r},${g},${b})`;}
  function drawFurniturePattern(w,d,pattern,offsetX=0,offsetY=0){if(pattern==='solid'||pattern==='woodgrain'||pattern==='leather'||pattern==='masonry')return;ctx.save();ctx.translate(offsetX,offsetY);ctx.strokeStyle='rgba(255,255,255,.28)';ctx.lineWidth=1;if(pattern==='striped'||pattern==='plaid'){for(let x=-w/2;x<w/2;x+=6){ctx.beginPath();ctx.moveTo(x,-d/2);ctx.lineTo(x,d/2);ctx.stroke();}}if(pattern==='plaid'||pattern==='geometric'){for(let y=-d/2;y<d/2;y+=6){ctx.beginPath();ctx.moveTo(-w/2,y);ctx.lineTo(w/2,y);ctx.stroke();}}if(pattern==='floral'){for(let x=-w*.35;x<=w*.35;x+=10)for(let y=-d*.25;y<=d*.25;y+=9){ctx.beginPath();ctx.arc(x,y,2,0,Math.PI*2);ctx.stroke();}}ctx.restore();}
  function connecting() { ctx.fillStyle='#e7eadf'; ctx.textAlign='center'; ctx.font='700 15px monospace'; ctx.fillText('Resolving geographic reality…',viewportWidth()/2,innerHeight/2); }

  function measureRenderStage(name,draw){const start=performance.now();try{return draw();}finally{const stages=state.performance.stages||(state.performance.stages={}),ms=performance.now()-start;stages[name]=stages[name]==null?ms:stages[name]*.9+ms*.1;}}
  function drawCachedGround(view,detail) {
    const width=viewportWidth(),height=innerHeight,pad=128,ratio=canvas.width/width;
    let cache=state.groundCache;
    const offset=cache?toScreen(cache.anchor):null;
    const dx=offset?offset.x-cache.origin.x:0,dy=offset?offset.y-cache.origin.y:0;
    if(!cache||cache.base!==state.base||cache.grid!==state.elevationGrid||cache.scale!==state.scale||cache.pitch!==state.pitch||cache.shear!==state.shear||cache.detail!==detail||cache.width!==width||cache.height!==height||cache.ratio!==ratio||Math.abs(dx)>pad/2||Math.abs(dy)>pad/2){
      const buffer=cache?.canvas||document.createElement('canvas');buffer.width=Math.ceil((width+pad*2)*ratio);buffer.height=Math.ceil((height+pad*2)*ratio);
      const main=ctx,extra=pad*2/(state.scale*state.pitch),expanded={minX:view.minX-extra,maxX:view.maxX+extra,minY:view.minY-extra,maxY:view.maxY+extra};
      const anchor={...state.camera},origin=toScreen(anchor),lists=renderListsFor(expanded);
      try{
        ctx=buffer.getContext('2d');ctx.setTransform(ratio,0,0,ratio,pad*ratio,pad*ratio);
        ctx.fillStyle='#315a38';ctx.fillRect(-pad,-pad,width+pad*2,height+pad*2);
        drawGrass(expanded,detail);
        for(const e of lists.terrain)if(e.properties.subtype!=="driveway")drawTerrain(e,detail,expanded);
        for(const e of lists.terrain)if(e.properties.subtype==="driveway")drawTerrain(e,detail,expanded);
        drawSidewalkNetwork(lists.sidewalk,expanded,detail);drawRoadNetwork(lists.road,expanded,detail);
      }finally{ctx=main;}
      state.groundCache=cache={canvas:buffer,anchor,origin,base:state.base,grid:state.elevationGrid,scale:state.scale,pitch:state.pitch,shear:state.shear,detail,width,height,ratio};
      ctx.drawImage(buffer,-pad,-pad,width+pad*2,height+pad*2);
    }else ctx.drawImage(cache.canvas,dx-pad,dy-pad,width+pad*2,height+pad*2);
  }
  function drawGrass(view,detail) {
    const tile = detail===2?2:detail===1?6:18;
    for(let y=Math.floor(view.minY/tile)*tile;y<view.maxY;y+=tile) for(let x=Math.floor(view.minX/tile)*tile;x<view.maxX;x+=tile){
      const p=[toScreen({x,y}),toScreen({x:x+tile,y}),toScreen({x:x+tile,y:y+tile}),toScreen({x,y:y+tile})];
      const n=hash(`${Math.floor(x/tile)}:${Math.floor(y/tile)}`),base=n>.52?'#4f7c3e':'#477339';ctx.fillStyle=shade(base,terrainHillshade(x+tile/2,y+tile/2,tile));
      ctx.beginPath();ctx.moveTo(p[0].x,p[0].y);p.slice(1).forEach(q=>ctx.lineTo(q.x,q.y));ctx.closePath();ctx.fill();
      if(detail===2&&n>.68){const c=toScreen({x:x+tile*n,y:y+tile*(1-n)});ctx.strokeStyle='#8eae5d';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(c.x,c.y+3);ctx.lineTo(c.x-2,c.y);ctx.moveTo(c.x,c.y+3);ctx.lineTo(c.x+2,c.y);ctx.stroke();}
    }
  }
  function drawTerrain(e,detail,expandedView=null){
    const t=(e.properties?.terrain||'grass').toLowerCase(),colors={grass:['#568342','#84ad58'],forest:['#183e2a','#315f3b'],sand:['#d3b86e','#f0d58e'],mud:['#654630','#9a7050'],pavement:['#747a76','#adb2ad']},c=colors[t]||colors.grass;
    drawGeometry(e,c[0],detail?c[1]:null,detail?1.5:0,true);if(detail!==2||!e.geometry?.length)return;
    const view=expandedView||viewBounds(1),b=e._bounds||boundsOf(e),step=t==='forest'?3.2:t==='pavement'?2.5:2.2,minX=Math.max(view.minX,b.minX),maxX=Math.min(view.maxX,b.maxX),minY=Math.max(view.minY,b.minY),maxY=Math.min(view.maxY,b.maxY);ctx.save();path(e,true);ctx.clip();
    let marks=0;for(let y=Math.floor(minY/step)*step;y<=maxY&&marks<1400;y+=step)for(let x=Math.floor(minX/step)*step;x<=maxX&&marks<1400;x+=step){const n=hash(`${e.id}:${Math.floor(x/step)}:${Math.floor(y/step)}`);if(n<.42)continue;marks++;const p=toScreen({x:x+(n-.5)*step*.55,y:y+(hash(`${n}:y`)-.5)*step*.55});ctx.lineWidth=1;
      if(t==='sand'){ctx.fillStyle=n>.75?'rgba(255,239,171,.7)':'rgba(136,103,49,.35)';ctx.fillRect(p.x,p.y,1.5,1.5);}
      else if(t==='mud'){ctx.strokeStyle='rgba(48,31,24,.4)';ctx.beginPath();ctx.ellipse(p.x,p.y,3+n*4,1.5+n*2,0,0,Math.PI*2);ctx.stroke();}
      else if(t==='pavement'){ctx.strokeStyle='rgba(47,52,50,.24)';ctx.beginPath();ctx.moveTo(p.x-5,p.y);ctx.lineTo(p.x+4,p.y+2);ctx.lineTo(p.x+7,p.y-1);ctx.stroke();}
      else if(t==='forest'){ctx.fillStyle=n>.72?'#0f3421':'#28563a';ctx.beginPath();ctx.moveTo(p.x,p.y-6);ctx.lineTo(p.x-4,p.y+4);ctx.lineTo(p.x+4,p.y+4);ctx.closePath();ctx.fill();}
      else{ctx.strokeStyle='rgba(190,218,112,.55)';ctx.beginPath();ctx.moveTo(p.x,p.y+3);ctx.lineTo(p.x-2,p.y-2);ctx.moveTo(p.x,p.y+3);ctx.lineTo(p.x+2,p.y-1);ctx.stroke();}
    }ctx.restore();
  }
  function drawSidewalkNetwork(entities,view,detail){const roads=entities.filter(e=>visible(e,view));for(const e of roads){const width=prop(e,'widthMeters',8)*state.scale;drawGeometry(e,null,'#827f73',Math.max(3,width+2));}for(const e of roads){const width=prop(e,'widthMeters',8)*state.scale;drawGeometry(e,null,'#c3bdae',Math.max(2,width));}if(detail===2){ctx.save();ctx.setLineDash([state.scale*1.8,state.scale*.12]);for(const e of roads)drawGeometry(e,null,'rgba(92,88,79,.35)',1);ctx.restore();}}
  function drawRoadNetwork(entities,view,detail){const roads=entities.filter(e=>visible(e,view));for(const e of roads){const width=prop(e,'widthMeters',6)*state.scale,unpaved=e.properties?.surface==='unpaved';drawGeometry(e,null,unpaved?'#654b36':'#242a29',Math.max(4,width+3));}for(const e of roads){const width=prop(e,'widthMeters',6)*state.scale,unpaved=e.properties?.surface==='unpaved';drawGeometry(e,null,unpaved?'#806043':'#4b514f',Math.max(3,width));}if(detail){ctx.save();ctx.setLineDash([state.scale*2.2,state.scale*1.4]);for(const e of roads){const highway=e.properties?.highway||'',unpaved=e.properties?.surface==='unpaved';if(!['motorway','trunk','primary','secondary','tertiary'].includes(highway))continue;drawGeometry(e,null,unpaved?'#c9a96a':'#e8c854',Math.max(1,state.scale*.08));}ctx.restore();}}
  function drawStreetNames(entities,view,detail){if(detail===0)return;const occupied=[],viewport=viewportWidth();for(const e of entities){const name=e.properties?.name;if(!name||!visible(e,view)||!e.geometry?.length)continue;let best=null;for(let i=0;i<e.geometry.length-1;i++){const a=toScreen(e.geometry[i]),b=toScreen(e.geometry[i+1]),length=Math.hypot(b.x-a.x,b.y-a.y);if(!best||length>best.length)best={a,b,length};}if(!best||best.length<55)continue;const x=(best.a.x+best.b.x)/2,y=(best.a.y+best.b.y)/2;if(x<45||x>viewport-45||y<25||y>innerHeight-25||occupied.some(p=>Math.abs(p.x-x)<120&&Math.abs(p.y-y)<26))continue;let angle=Math.atan2(best.b.y-best.a.y,best.b.x-best.a.x);if(angle>Math.PI/2)angle-=Math.PI;if(angle<-Math.PI/2)angle+=Math.PI;ctx.save();ctx.translate(x,y);ctx.rotate(angle);ctx.textAlign='center';ctx.textBaseline='middle';ctx.font=`700 ${detail===2?11:9}px monospace`;ctx.lineWidth=4;ctx.strokeStyle='rgba(22,24,22,.82)';ctx.strokeText(name,0,0,Math.max(70,best.length-12));ctx.fillStyle='#fff3bf';ctx.fillText(name,0,0,Math.max(70,best.length-12));ctx.restore();occupied.push({x,y});}}
  function drawAirports(entities,view,detail){const viewport=viewportWidth();for(const e of entities){if(!visible(e,view))continue;if(e.geometry?.length>=3)drawGeometry(e,'rgba(91,99,94,.45)','#d4d8c9',Math.max(1,state.scale*.08),true);const p=toScreen(e.position),name=e.properties?.name||e.properties?.iata||'Airport';if(detail>0&&p.x>40&&p.x<viewport-40&&p.y>20&&p.y<innerHeight-20){ctx.save();ctx.textAlign='center';ctx.font='700 11px monospace';ctx.lineWidth=4;ctx.strokeStyle='#252b28';ctx.strokeText(`✈ ${name}`,p.x,p.y);ctx.fillStyle='#eef1dd';ctx.fillText(`✈ ${name}`,p.x,p.y);ctx.restore();}}}
  function drawStateBoundaries(entities,view,detail){const viewport=viewportWidth();ctx.save();ctx.setLineDash([18,8]);for(const e of entities){if(!visible(e,view))continue;drawGeometry(e,null,'rgba(255,219,91,.9)',3);if(detail===0)continue;const name=e.properties?.stateName||e.properties?.name;if(!name)continue;for(let i=0;i<(e.geometry?.length||0)-1;i++){const a=toScreen(e.geometry[i]),b=toScreen(e.geometry[i+1]),length=Math.hypot(b.x-a.x,b.y-a.y);if(length<140)continue;const repetitions=Math.max(1,Math.floor(length/260));for(let n=1;n<=repetitions;n++){const amount=n/(repetitions+1),x=a.x+(b.x-a.x)*amount,y=a.y+(b.y-a.y)*amount;if(x<50||x>viewport-50||y<20||y>innerHeight-20)continue;ctx.save();ctx.translate(x,y);let angle=Math.atan2(b.y-a.y,b.x-a.x);if(angle>Math.PI/2)angle-=Math.PI;if(angle<-Math.PI/2)angle+=Math.PI;ctx.rotate(angle);ctx.font='700 12px monospace';ctx.textAlign='center';ctx.lineWidth=4;ctx.strokeStyle='#25251d';ctx.strokeText(name,0,-7);ctx.fillStyle='#ffe68a';ctx.fillText(name,0,-7);ctx.restore();}}}ctx.restore();}
  function drawPointsOfInterest(entities,view,detail){if(detail===0)return;const viewport=viewportWidth();for(const e of entities){if(!visible(e,view))continue;const p=toScreen(e.position),category=e.properties?.merchantCategory,name=e.properties?.name||e.properties?.brand;if(!category||!name||p.x<30||p.x>viewport-30||p.y<20||p.y>innerHeight-20)continue;const icon=category==='gas'?'⛽':category==='clothing'?'▣':category==='food'?'◆':category==='furniture'?'▰':'●';ctx.save();ctx.textAlign='center';ctx.font='700 10px monospace';ctx.lineWidth=4;ctx.strokeStyle='#222620';ctx.strokeText(`${icon} ${name}`,p.x,p.y-8);ctx.fillStyle='#f6e7a9';ctx.fillText(`${icon} ${name}`,p.x,p.y-8);ctx.restore();}}
  function waterGeometryClosed(e){const geometry=e.geometry||[],first=geometry[0],last=geometry[geometry.length-1];return geometry.length>=4&&first.x===last.x&&first.y===last.y;}
  function waterOutline(e){
    const anchor=toScreen(e.geometry[0]),cached=e._waterOutline;
    if(cached&&cached.geometry===e.geometry&&cached.holes===e.interiorRings&&cached.scale===state.scale&&cached.pitch===state.pitch&&cached.shear===state.shear&&cached.elevation===state.elevationGrid&&cached.dungeon===state.dungeon)return{path:cached.path,anchor};
    const outline=new Path2D();
    for(const ring of [e.geometry,...(e.interiorRings||[])]){
      ring.forEach((point,index)=>{const p=toScreen(point);index?outline.lineTo(p.x-anchor.x,p.y-anchor.y):outline.moveTo(p.x-anchor.x,p.y-anchor.y);});outline.closePath();
    }
    e._waterOutline={path:outline,geometry:e.geometry,holes:e.interiorRings,scale:state.scale,pitch:state.pitch,shear:state.shear,elevation:state.elevationGrid,dungeon:state.dungeon};
    return{path:outline,anchor};
  }
  function drawWater(e,detail,now){
    if(!waterGeometryClosed(e)){const width=Math.max(.1,prop(e,'widthMeters',prop(e,'width',3)));drawGeometry(e,null,'#d3b86e',state.scale*(width+6));drawGeometry(e,null,'#72cbd0',Math.max(2,state.scale*width));drawGeometry(e,null,'#116886',Math.max(1,state.scale*width*.5));return;}
    const {path:outline,anchor}=waterOutline(e),wave=detail===2?Math.sin(now/450)*state.scale*.08:0;
    ctx.save();ctx.translate(anchor.x,anchor.y);ctx.strokeStyle='#d3b86e';ctx.lineWidth=state.scale*6;ctx.lineJoin='round';ctx.stroke(outline);ctx.clip(outline,'evenodd');
    ctx.fillStyle='#073550';ctx.fill(outline,'evenodd');ctx.lineJoin='round';ctx.lineCap='round';
    ctx.strokeStyle='#55b7bd';ctx.lineWidth=Math.max(5,state.scale*6+wave);ctx.stroke(outline);
    ctx.translate(-anchor.x,-anchor.y);
    if(detail===2){
      const view=viewBounds(1),b=e._bounds||boundsOf(e),step=3.5;
      ctx.strokeStyle='rgba(139,220,225,.42)';ctx.lineWidth=1.2;let marks=0;
      for(let y=Math.max(view.minY,b.minY);y<Math.min(view.maxY,b.maxY)&&marks<700;y+=step)for(let x=Math.max(view.minX,b.minX);x<Math.min(view.maxX,b.maxX)&&marks<700;x+=step){
        if(hash(`${e.id}:water:${Math.floor(x/step)}:${Math.floor(y/step)}`)<.58)continue;marks++;const p=toScreen({x,y});ctx.beginPath();ctx.moveTo(p.x-5,p.y+Math.sin(now/350+x)*1.5);ctx.quadraticCurveTo(p.x,p.y-2,p.x+6,p.y);ctx.stroke();
      }
    }
    ctx.restore();
  }
  const weaponRanges={fist:1.6,knife:1.6,sword:2.3,hockeyStick:2.2,iceSkate:1.8,rock:25,slingshot:60,crossbow:100,pistol:50,rifle:200,spearGun:200,ar15:200,machineGun:200,flamethrower:25,rocketLauncher:300,grenade:35,molotovCocktail:30,probulator:1.7};
  function candleActive(player,now=Date.now()){const until=Date.parse(player?.candleUntilUtc||'');return Number.isFinite(until)&&until>now;}
  function activeItemTypes(player){const items=[];const travel={skateboard:'skateboard',bike:'bike',eBike:'eBike',raft:'inflatableRaft',scuba:'scubaGear',swim:'swimmies',dirtBike:'dirtBike',motorcycle:'motorcycle',ufo:'ufo'}[player?.travelMode];if(travel)items.push(travel);if(player?.magicHikingShoesOn)items.push('magicHikingShoes');if(player?.magicRunningShoesOn)items.push('magicRunningShoes');if(player?.hatOn)items.push('hat');if(player?.flashlightOn)items.push('flashlight');if(player?.lanternOn)items.push('lantern');if(candleActive(player))items.push('candle');if(player?.laserOn)items.push('laser');if(player?.equippedWeapon&&player.equippedWeapon!=='none')items.push(player.equippedWeapon);return[...new Set(items)];}
  function visibleRange(player=state.players.get(state.playerId)){const config=state.privateState?.serverConfiguration;if(!player||!config)return 100;const base=Number(config.movement?.baseVisibilityMeters??100),outdoors=(player.locationId||'outdoor')==='outdoor',sun=outdoors?daylight():1,moon=Number(state.weather?.moonIllumination||0),precipitation=Number(state.weather?.precipitationMillimeters||0),environment=outdoors?Math.max(.08+moon*.12,Math.min(1,sun))*Math.max(.55,1-precipitation*.06):1;let meters=base*environment;for(const type of activeItemTypes(player)){const item=config.items?.find(entry=>entry.itemType===type);meters+=Number(item?.visibilityModifierMeters||0);}return Math.max(1,meters*(state.privateState?.mapleSyrupUntilUtc ? 1+.05*((state.privateState?.progression?.stats?.perception??1)-1)+(Date.parse(state.privateState.mapleSyrupUntilUtc)>Date.now()?.25:0) : Number(state.privateState?.progression?.visionMultiplier||1)));}
  function dynamicVisible(position){const me=state.players.get(state.playerId);return !!me&&!!position&&Math.hypot(position.x-me.position.x,position.y-me.position.y)<=visibleRange(me);}
  function drawWorldRing(position,radius,color,dash){if(!position||!radius)return;ctx.save();ctx.strokeStyle=color;ctx.lineWidth=1.5;ctx.setLineDash(dash);ctx.beginPath();for(let i=0;i<=72;i++){const angle=i/72*Math.PI*2,p=toScreen({x:position.x+Math.cos(angle)*radius,y:position.y+Math.sin(angle)*radius});i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y);}ctx.closePath();ctx.stroke();ctx.restore();}
  function drawPlayerRanges(player){if(!player)return;if(typeof gardenUI!=='undefined')gardenUI.overlay(ctx);drawWorldRing(player.position,6,'rgba(182,239,203,.28)',[5,5]);const weapon=player.id===state.playerId&&!['attackReady','aggressive','defensive'].includes(state.actionMode)?'none':player.equippedWeapon||'fist',configured=state.privateState?.serverConfiguration?.items?.find(item=>item.itemType===weapon);if(weapon==='probulator'){const point=toScreen(player.position),footprint=probulatorFootprintPixels(configured?.rangeMeters??weaponRanges.probulator);ctx.save();ctx.strokeStyle='rgba(255,218,112,.25)';ctx.setLineDash([10,7]);ctx.beginPath();ctx.ellipse(point.x,point.y,footprint.x,footprint.y,0,0,Math.PI*2);ctx.stroke();ctx.restore();}else if(weapon!=='none')drawWorldRing(player.position,configured?.rangeMeters??weaponRanges[weapon]??0,'rgba(255,218,112,.25)',[10,7]);}

  function drawRaised(view,detail,now){const items=[];
    for(const e of state.currentRenderLists.building||[])items.push({d:Math.max(...e.geometry.map(p=>toScreen(p).y)),f:()=>drawBuilding(e,detail,now)});
    if(detail>0) for(const kind of ['tree','bush','fence','vehicle','streetLight','resourceNode'])for(const e of state.currentRenderLists[kind]||[])items.push({d:toScreen(e.position).y,f:()=>drawObject(e,detail)});
    for(const flag of state.reality.values())if(flag.properties?.objectType==='personalFlag'&&pointVisible(flag.position,view))items.push({d:toScreen(flag.position).y,f:()=>drawPlacedFlag(flag,detail)});
    for(const actor of state.actors.values())if((actor.locationId||'outdoor')==='outdoor'&&actorVisible(actor,view)&&actorDynamicVisible(actor)&&detail>0)items.push({d:toScreen(actor.position).y+(actor.abduction?1e9:0),f:()=>drawActor(actor,detail,now)});
    for(const stop of state.transit?.stops||[])if(pointVisible(stop.position,view))items.push({d:toScreen(BusTransit.benchPosition(stop)).y-.01,f:()=>BusTransit.drawStop(ctx,stop,toScreen,state.scale)});
    for(const player of state.players.values())if((player.locationId||'outdoor')==='outdoor'&&pointVisible(player.position,view)&&(player.id===state.playerId||dynamicVisible(player.position)))items.push({d:toScreen(player.position).y+(player.travelMode==='ufo'||player.abduction?1e9:0),f:()=>drawPlayer(player,player.id===state.playerId,detail,now)});
    items.sort((a,b)=>a.d-b.d);for(const item of items)item.f();
  }
  function drawPlacedFlag(flag,detail){const p=toScreen(flag.position),height=Math.max(18,state.scale*2.8),width=Math.max(18,state.scale*2.2),bannerHeight=Math.max(10,state.scale*.85);ctx.save();ctx.strokeStyle='#eee5bd';ctx.lineWidth=Math.max(2,state.scale*.1);ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(p.x,p.y-height);ctx.stroke();ctx.fillStyle='#d75649';ctx.strokeStyle='#44251e';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(p.x,p.y-height);ctx.lineTo(p.x+width,p.y-height+bannerHeight*.2);ctx.lineTo(p.x+width*.82,p.y-height+bannerHeight);ctx.lineTo(p.x,p.y-height+bannerHeight*.72);ctx.closePath();ctx.fill();ctx.stroke();if(detail>0){const label=flag.properties?.label||'Flag',owner=flag.properties?.ownerName||'Explorer',textWidth=Math.max(92,Math.min(230,Math.max(label.length,owner.length+10)*7+14)),x=p.x+width*.5,y=p.y-height-28;ctx.fillStyle='rgba(20,25,20,.92)';ctx.strokeStyle='#e7cb78';ctx.lineWidth=1;ctx.fillRect(x-textWidth/2,y-18,textWidth,34);ctx.strokeRect(x-textWidth/2,y-18,textWidth,34);ctx.textAlign='center';ctx.textBaseline='middle';ctx.font='700 10px monospace';ctx.fillStyle='#fff0b0';ctx.fillText(label,x,y-7,textWidth-10);ctx.font='9px monospace';ctx.fillStyle='#d1ddcf';ctx.fillText(`Placed by ${owner}`,x,y+7,textWidth-10);}ctx.restore();}
  function pointVisible(p,v){return p.x>=v.minX&&p.x<=v.maxX&&p.y>=v.minY&&p.y<=v.maxY;}
  function actorVisualRadius(actor){return actor.subtype==='brontosaurus'?12:actor.subtype==='giant'?8:actor.subtype==='tRex'?7:actor.subtype==='stegosaurus'?5:actor.subtype==='giantGorilla'?3.5:actor.subtype==='raptor'?2:0;}
  function actorVisible(actor,view){const margin=actorVisualRadius(actor);return actor.position.x>=view.minX-margin&&actor.position.x<=view.maxX+margin&&actor.position.y>=view.minY-margin&&actor.position.y<=view.maxY+margin;}
  function actorDynamicVisible(actor){const me=state.players.get(state.playerId);return !!me&&Math.hypot(actor.position.x-me.position.x,actor.position.y-me.position.y)<=visibleRange(me)+actorVisualRadius(actor);}
  function drawBuilding(e,detail,now=performance.now()){const ground=e.geometry.map(toScreen);if(ground.length<3)return;if(e.properties?.state==='rubble'){ctx.save();ctx.fillStyle='#4b4137';ctx.strokeStyle='#231e1a';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(ground[0].x,ground[0].y);ground.slice(1).forEach(point=>ctx.lineTo(point.x,point.y));ctx.closePath();ctx.fill();ctx.stroke();const center=toScreen(e.position);for(let i=0;i<10;i++){const angle=hash(`${e.id}:rubble:${i}`)*Math.PI*2,radius=(.15+hash(`${e.id}:radius:${i}`)*.8)*Math.max(12,state.scale*1.5),x=center.x+Math.cos(angle)*radius,y=center.y+Math.sin(angle)*radius*.45;ctx.fillStyle=i%2?'#7b6857':'#5e5146';ctx.fillRect(x-4,y-3,8,6);}for(let i=0;i<5;i++){const x=center.x+(hash(`${e.id}:fire:${i}`)-.5)*state.scale*2.2,y=center.y+(hash(`${e.id}:firey:${i}`)-.5)*state.scale*.8,pulse=5+Math.sin(now/110+i)*2;ctx.fillStyle='rgba(255,99,27,.88)';ctx.beginPath();ctx.arc(x,y-pulse,pulse,0,Math.PI*2);ctx.fill();ctx.fillStyle='#ffd052';ctx.beginPath();ctx.arc(x,y-pulse,Math.max(2,pulse*.45),0,Math.PI*2);ctx.fill();}ctx.restore();return;}const last=ground[ground.length-1];const count=ground[0].x===last.x&&ground[0].y===last.y?ground.length-1:ground.length;const levels=Math.max(1,Number(e.properties?.['building:levels']||e.properties?.levels||2));const height=Math.min(150,levels*3*state.scale*.52);const roof=ground.map(p=>({x:p.x,y:p.y-height}));
    if(detail===0){drawGeometry(e,'#665b50','#302b27',1,true);return;}
    const buildingRefresh=Math.max(1,Number(state.privateState?.serverConfiguration?.events?.buildingLightsRefreshMinutes||15))*60000,lightsOn=hash(`${e.id}:${Math.floor(serverNowMs()/buildingRefresh)}`)>.48,publicBase=state.publicBases?.get(e.id),isBase=!!publicBase;
    const category=storeCategoryForBuilding(e),shopColors={casino:['#542537','#184d50'],gas:['#346b83','#43879e'],clothing:['#77527b','#8d6592'],food:['#59754c','#6d8c5d'],convenience:['#826744','#997b51'],furniture:['#71502f','#88623a'],weapons:['#654346','#7b5154'],vehicles:['#394f68','#49657e'],hardware:['#74562c','#8b6937'],sportingGoods:['#3f6f67','#4f887c'],general:['#665044','#755a49']},wallColors=isBase?['#486a78','#547d89']:(shopColors[category]||shopColors.general);for(let i=0;i<count;i++){const j=(i+1)%count;ctx.fillStyle=wallColors[i%2];ctx.beginPath();ctx.moveTo(roof[i].x,roof[i].y);ctx.lineTo(roof[j].x,roof[j].y);ctx.lineTo(ground[j].x,ground[j].y);ctx.lineTo(ground[i].x,ground[i].y);ctx.closePath();ctx.fill();ctx.strokeStyle='#382c26';ctx.stroke();if(detail===2)drawWindows(roof[i],roof[j],ground[i],ground[j],levels,lightsOn);}
    ctx.fillStyle=category==='casino'&&!isBase?'#203c40':'#8f755e';ctx.strokeStyle=category==='casino'&&!isBase?'#e9bd64':'#c09b75';ctx.lineWidth=1.5;ctx.beginPath();ctx.moveTo(roof[0].x,roof[0].y);for(let i=1;i<count;i++)ctx.lineTo(roof[i].x,roof[i].y);ctx.closePath();ctx.fill();ctx.stroke();
    if(detail===2){const a=roof[0],b=roof[Math.floor(count/2)];ctx.strokeStyle='#594434';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);ctx.stroke();const n=hash(e.id);const c=roof[Math.floor(n*count)%count];ctx.fillStyle='#3c3029';ctx.fillRect(c.x-3,c.y-9,6,10);}
    const door=state.doors.get(e.id);if(door)drawDoor(door);
    if(isBase&&detail>0){const c=roof.slice(0,count).reduce((a,p)=>({x:a.x+p.x/count,y:a.y+p.y/count}),{x:0,y:0}),poleHeight=state.scale*6.5,flagWidth=state.scale*6,flagHeight=state.scale*2.4,top=c.y-poleHeight;ctx.strokeStyle='#f5e8bd';ctx.lineWidth=Math.max(3,state.scale*.14);ctx.beginPath();ctx.moveTo(c.x,c.y+2);ctx.lineTo(c.x,top);ctx.stroke();ctx.fillStyle='#e2bd4e';ctx.strokeStyle='#5b421b';ctx.lineWidth=Math.max(2,state.scale*.08);ctx.beginPath();ctx.moveTo(c.x,top);ctx.lineTo(c.x+flagWidth,top+flagHeight*.25);ctx.lineTo(c.x+flagWidth*.88,top+flagHeight);ctx.lineTo(c.x,top+flagHeight*.72);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#362612';ctx.font=`700 ${Math.max(9,Math.min(18,state.scale*.48))}px monospace`;ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(`${publicBase.ownerName}'s Home`,c.x+flagWidth*.47,top+flagHeight*.52,flagWidth*.82);}
    else if(category==='casino'&&detail>0)drawCasinoExterior(roof,count,door);
    else if(category&&detail>0)drawStoreFlag(roof.slice(0,count).reduce((a,p)=>({x:a.x+p.x/count,y:a.y+p.y/count}),{x:0,y:0}),category);
  }
  function drawCasinoExterior(roof,count,door){
    const center=roof.slice(0,count).reduce((sum,p)=>({x:sum.x+p.x/count,y:sum.y+p.y/count}),{x:0,y:0});
    const width=Math.max(118,Math.min(188,state.scale*10)),height=60,left=center.x-width/2,top=center.y-height-18;
    ctx.save();
    ctx.strokeStyle='#e9bd64';ctx.lineWidth=3;
    ctx.beginPath();ctx.moveTo(roof[0].x,roof[0].y);
    for(let i=1;i<count;i++)ctx.lineTo(roof[i].x,roof[i].y);
    ctx.closePath();ctx.stroke();
    ctx.fillStyle='#ffdf88';
    for(let i=0;i<count;i++){
      const a=roof[i],b=roof[(i+1)%count],steps=Math.max(1,Math.min(60,Math.ceil(Math.hypot(b.x-a.x,b.y-a.y)/12)));
      for(let j=0;j<steps;j++){const t=j/steps;ctx.beginPath();ctx.arc(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,2,0,Math.PI*2);ctx.fill();}
    }
    ctx.strokeStyle='#d6ad60';ctx.lineWidth=4;
    for(const offset of [-width*.3,width*.3]){ctx.beginPath();ctx.moveTo(center.x+offset,center.y+3);ctx.lineTo(center.x+offset,top+height);ctx.stroke();}
    ctx.shadowColor='#edb94e';ctx.shadowBlur=12;
    ctx.fillStyle='#311a2b';ctx.strokeStyle='#ffd779';ctx.lineWidth=2;
    ctx.fillRect(left,top,width,height);ctx.strokeRect(left,top,width,height);ctx.shadowBlur=0;
    ctx.strokeStyle='#58ccc2';ctx.lineWidth=1;ctx.strokeRect(left+6,top+6,width-12,height-12);
    ctx.fillStyle='#fff2b3';
    for(let i=0;i<=10;i++){const x=left+3+(width-6)*i/10;for(const y of [top+3,top+height-3]){ctx.beginPath();ctx.arc(x,y,1.7,0,Math.PI*2);ctx.fill();}}
    ctx.textAlign='center';ctx.textBaseline='middle';ctx.font='700 9px monospace';ctx.fillStyle='#73e0d1';
    ctx.fillText('LUCKY LANTERN',center.x,top+16,width-20);
    ctx.font='900 24px Georgia';ctx.fillStyle='#ffe6a0';ctx.fillText('CASINO',center.x,top+36,width-18);
    if(door){
      const p=toScreen(door.position),doorHeight=Math.max(16,2.05*state.scale*.52),canopyWidth=Math.max(44,Math.min(86,state.scale*4)),y=p.y-doorHeight-17;
      ctx.fillStyle='#712c44';ctx.strokeStyle='#f1c563';ctx.lineWidth=2;
      ctx.beginPath();ctx.moveTo(p.x-canopyWidth*.4,y);ctx.lineTo(p.x+canopyWidth*.4,y);ctx.lineTo(p.x+canopyWidth*.5,y+14);ctx.lineTo(p.x-canopyWidth*.5,y+14);ctx.closePath();ctx.fill();ctx.stroke();
      ctx.font='700 10px monospace';ctx.fillStyle='#ffe9a9';ctx.fillText('7 7 7',p.x,y+8,canopyWidth-8);
    }
    ctx.restore();
  }
  function drawStoreFlag(anchor,category){const profiles={food:['🍎','FOOD','#477a43'],gas:['⛽','GAS','#327b97'],furniture:['🪑','FURNITURE','#80562f'],weapons:['⚔','WEAPONS','#7d4044'],vehicles:['🏍','VEHICLES','#3f6082'],clothing:['👕','CLOTHING','#7b4e82'],hardware:['🔨','HARDWARE','#8b642f'],sportingGoods:['🚲','SPORTING GOODS','#39796d'],convenience:['★','SUPPLIES','#8d6d34'],general:['◆','GENERAL STORE','#665044']},profile=profiles[category]||profiles.general,width=Math.max(64,Math.min(126,profile[1].length*8+31)),height=24,top=anchor.y-Math.max(38,Math.min(78,state.scale*2.8));ctx.save();ctx.strokeStyle='#eee2b4';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(anchor.x,anchor.y+2);ctx.lineTo(anchor.x,top);ctx.stroke();ctx.fillStyle=profile[2];ctx.strokeStyle='#332719';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(anchor.x,top);ctx.lineTo(anchor.x+width,top+3);ctx.lineTo(anchor.x+width-6,top+height);ctx.lineTo(anchor.x,top+height-2);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#fff4ca';ctx.font='700 10px monospace';ctx.textAlign='left';ctx.textBaseline='middle';ctx.fillText(profile[0],anchor.x+6,top+height/2);ctx.fillText(profile[1],anchor.x+26,top+height/2,width-30);ctx.restore();}
  function drawWindows(a,b,ga,gb,levels,lightsOn){const length=Math.hypot(b.x-a.x,b.y-a.y);const columns=Math.max(1,Math.floor(length/70));for(let level=0;level<levels;level++)for(let c=1;c<=columns;c++){const t=c/(columns+1),baseY=(ga.y+(gb.y-ga.y)*t),roofY=(a.y+(b.y-a.y)*t),y=baseY-(baseY-roofY)*(level+.55)/levels,x=ga.x+(gb.x-ga.x)*t;ctx.fillStyle=state.weather?.isDay?'#9ec2bd':lightsOn?'#ffd96a':'#23353b';ctx.fillRect(x-7,y-7,14,13);ctx.strokeStyle='#302c29';ctx.strokeRect(x-7,y-7,14,13);}}
  function drawDoor(e){const p=toScreen(e.position),w=Math.max(9,state.scale*.95),h=Math.max(16,2.05*state.scale*.52);ctx.fillStyle='#302119';ctx.strokeStyle='#d4a453';ctx.lineWidth=2;ctx.fillRect(p.x-w/2,p.y-h,w,h);ctx.strokeRect(p.x-w/2,p.y-h,w,h);ctx.fillStyle='#efc965';ctx.beginPath();ctx.arc(p.x+w*.28,p.y-h*.48,2,0,Math.PI*2);ctx.fill();if(doorIsLocked(e)){const x=p.x,y=p.y-h*.52,r=Math.max(8,Math.min(14,w*.75));ctx.strokeStyle='#f04449';ctx.lineWidth=Math.max(3,state.scale*.14);ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.moveTo(x-r*.72,y+r*.72);ctx.lineTo(x+r*.72,y-r*.72);ctx.stroke();}}
  function drawObject(e,detail){if(typeof Gardens!=='undefined'&&Gardens.draw(ctx,e,toScreen,state.scale))return;if(Survival.drawNode(ctx,e,toScreen(e.position),state.scale))return;if(e.kind==='tree')drawTree(e,detail);else if(e.kind==='bush')drawBush(e);else if(e.kind==='fence'){drawGeometry(e,null,'#6d472b',Math.max(2,state.scale*.15));}else if(e.kind==='vehicle')drawVehicle(e);else if(e.kind==='streetLight')drawStreetLight(e);else if(e.kind==='resourceNode'&&e.properties?.subtype==='mailbox')drawMailbox(e);else if(e.kind==='resourceNode'&&e.properties?.subtype==='postOfficeBox')drawPostalBox(e);}
  function drawMailbox(e){const p=toScreen(e.position),s=Math.max(7,state.scale*.32);ctx.fillStyle='#59402d';ctx.fillRect(p.x-2,p.y-s*1.4,4,s*1.4);ctx.fillStyle='#46617a';ctx.strokeStyle='#17242d';ctx.lineWidth=2;ctx.beginPath();ctx.roundRect(p.x-s*.65,p.y-s*2,s*1.3,s*.75,s*.25);ctx.fill();ctx.stroke();ctx.fillStyle='#d84d3e';ctx.fillRect(p.x+s*.55,p.y-s*2.05,2,s*.55);}
  function drawPostalBox(e){const p=toScreen(e.position),s=Math.max(8,state.scale*.38);ctx.fillStyle='rgba(0,0,0,.3)';ctx.beginPath();ctx.ellipse(p.x,p.y,s*.8,s*.22,0,0,Math.PI*2);ctx.fill();ctx.fillStyle='#244c80';ctx.strokeStyle='#d6e4ed';ctx.lineWidth=2;ctx.beginPath();ctx.roundRect(p.x-s*.62,p.y-s*1.9,s*1.24,s*1.75,s*.16);ctx.fill();ctx.stroke();ctx.fillStyle='#eff5f7';ctx.fillRect(p.x-s*.38,p.y-s*1.55,s*.76,s*.18);ctx.fillStyle='#d73e45';ctx.font=`700 ${Math.max(8,s*.55)}px monospace`;ctx.textAlign='center';ctx.fillText('POST',p.x,p.y-s*.72);}
  function drawStreetLight(e){const p=toScreen(e.position),events=state.privateState?.serverConfiguration?.events||{},hour=new Date(serverNowMs()).getUTCHours(),on=Number(events.streetLightsOnHour??19),off=Number(events.streetLightsOffHour??7),night=on<off?hour>=on&&hour<off:hour>=on||hour<off,h=Math.max(16,state.scale*2.2);ctx.strokeStyle='#252c29';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(p.x,p.y-h);ctx.lineTo(p.x+7,p.y-h);ctx.stroke();ctx.fillStyle=night?'#fff0a0':'#7f887d';ctx.beginPath();ctx.arc(p.x+8,p.y-h+1,4,0,Math.PI*2);ctx.fill();if(night){const g=ctx.createRadialGradient(p.x+8,p.y-h,2,p.x+8,p.y-h,45);g.addColorStop(0,'rgba(255,232,130,.38)');g.addColorStop(1,'rgba(255,232,130,0)');ctx.fillStyle=g;ctx.beginPath();ctx.arc(p.x+8,p.y-h,45,0,Math.PI*2);ctx.fill();}}
  function drawTree(e,detail){const p=toScreen(e.position),r=Math.max(3,state.scale*(detail===2?1.05:.65));ctx.fillStyle='#493322';ctx.fillRect(p.x-2,p.y-r*.35,4,r*.7);ctx.fillStyle='#173f29';ctx.beginPath();ctx.arc(p.x,p.y-r*.65,r,0,Math.PI*2);ctx.fill();ctx.fillStyle='#347044';ctx.beginPath();ctx.arc(p.x-r*.3,p.y-r*.85,r*.65,0,Math.PI*2);ctx.fill();}
  function drawBush(e){const p=toScreen(e.position),r=Math.max(2,state.scale*.42);ctx.fillStyle='#245d35';ctx.beginPath();ctx.arc(p.x-r*.4,p.y,r*.65,0,Math.PI*2);ctx.arc(p.x+r*.35,p.y,r*.72,0,Math.PI*2);ctx.fill();ctx.fillStyle='#5e9b45';ctx.fillRect(p.x-1,p.y-r*.45,2,2);}
  function drawVehicle(e){if(NorthernExposure.drawTruck(ctx,e,toScreen,state))return;const p=toScreen(e.position),l=prop(e,'lengthMeters',4.5)*state.scale,w=prop(e,'widthMeters',1.9)*state.scale*state.pitch,a=prop(e,'rotationDegrees',0)*Math.PI/180,pa=Math.atan2(-Math.sin(a)*state.pitch,Math.cos(a)+Math.sin(a)*state.shear);ctx.save();ctx.translate(p.x,p.y);ctx.rotate(pa);ctx.fillStyle=e.properties?.sprayPaintColor||'#8d4e3e';ctx.fillRect(-l/2,-w/2,l,w);ctx.fillStyle='#a8c8c8';ctx.fillRect(-l*.14,-w*.38,l*.35,w*.76);ctx.strokeStyle=Number(e.properties?.damage||0)>0?'#ffb142':'#242827';ctx.lineWidth=Number(e.properties?.damage||0)>0?4:2;ctx.strokeRect(-l/2,-w/2,l,w);ctx.restore();}
  function dinosaurAttack(a,now){const attack=state.actorAttacks.get(a.id);if(!attack)return{weapon:'',pulse:0};const age=now-attack.started;if(age>=850){state.actorAttacks.delete(a.id);return{weapon:'',pulse:0};}return{weapon:attack.weapon,pulse:Math.sin(Math.PI*Math.min(1,age/850))};}
  function drawDinosaurShadow(s,width=1.7){ctx.fillStyle='rgba(0,0,0,.36)';ctx.beginPath();ctx.ellipse(0,s*.24,s*width,s*.25,0,0,Math.PI*2);ctx.fill();}
  function drawGiantActor(a,p,moving,now){const height=Math.max(120,state.scale*15.24*.52),width=height*.23,direction=a.facing==='west'?-1:1,gait=moving?Math.sin(now/180+hash(a.id)*8):0,attack=dinosaurAttack(a,now),stomp=attack.weapon==='giantStomp'?attack.pulse:0;ctx.save();ctx.translate(p.x,p.y);ctx.fillStyle='rgba(0,0,0,.38)';ctx.beginPath();ctx.ellipse(0,height*.025,width*1.15,height*.045,0,0,Math.PI*2);ctx.fill();ctx.scale(direction,1);const leftLift=Math.max(0,-gait)*height*.025,rightLift=Math.max(0,gait)*height*.025+stomp*height*.16;ctx.fillStyle='#3a2d25';ctx.strokeStyle='#1d1815';ctx.lineWidth=Math.max(2,width*.035);ctx.beginPath();ctx.roundRect(-width*.82,-height*.08-leftLift,width*.72,height*.1,width*.18);ctx.fill();ctx.stroke();ctx.beginPath();ctx.roundRect(width*.12,-height*.08-rightLift,width*.82,height*.1,width*.18);ctx.fill();ctx.stroke();ctx.fillStyle='#6a4d38';ctx.beginPath();ctx.roundRect(-width*.67,-height*.53,width*.48,height*.48,width*.14);ctx.fill();ctx.stroke();ctx.beginPath();ctx.roundRect(width*.18,-height*.53-rightLift,width*.48,height*.48,width*.14);ctx.fill();ctx.stroke();ctx.fillStyle='#374f68';ctx.beginPath();ctx.moveTo(-width*.78,-height*.67);ctx.lineTo(width*.78,-height*.67);ctx.lineTo(width*.58,-height*.42);ctx.lineTo(-width*.56,-height*.42);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#8c493c';ctx.beginPath();ctx.roundRect(-width*.82,-height*.84,width*1.64,height*.28,width*.18);ctx.fill();ctx.stroke();ctx.strokeStyle='#b87d5a';ctx.lineWidth=Math.max(5,width*.23);ctx.lineCap='round';ctx.beginPath();ctx.moveTo(-width*.72,-height*.76);ctx.lineTo(-width*1.12,-height*.48+gait*height*.025);ctx.moveTo(width*.72,-height*.76);ctx.lineTo(width*1.12,-height*.48-gait*height*.025);ctx.stroke();ctx.fillStyle='#b87d5a';ctx.strokeStyle='#4a3024';ctx.lineWidth=Math.max(2,width*.04);ctx.beginPath();ctx.arc(0,-height*.93,width*.48,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.fillStyle='#4a3025';ctx.beginPath();ctx.arc(0,-height*.985,width*.5,Math.PI,0);ctx.fill();ctx.fillStyle='#eee0bd';for(const eyeX of[-.17,.17]){ctx.beginPath();ctx.arc(width*eyeX,-height*.95,Math.max(2,width*.045),0,Math.PI*2);ctx.fill();ctx.fillStyle='#21170f';ctx.beginPath();ctx.arc(width*eyeX,-height*.95,Math.max(1,width*.02),0,Math.PI*2);ctx.fill();ctx.fillStyle='#eee0bd';}ctx.strokeStyle='#6d3329';ctx.lineWidth=Math.max(2,width*.035);ctx.beginPath();ctx.arc(0,-height*.89,width*.18,.12,Math.PI-.12);ctx.stroke();if(stomp>.18){const footX=width*.55;ctx.strokeStyle=`rgba(255,218,124,${Math.min(1,stomp+.15)})`;ctx.lineWidth=Math.max(3,width*.045);ctx.beginPath();ctx.ellipse(footX,height*.015,width*(.7+stomp*.65),height*(.035+stomp*.035),0,0,Math.PI*2);ctx.stroke();ctx.fillStyle=`rgba(191,155,94,${stomp*.42})`;for(let dust=0;dust<5;dust++){const angle=dust*Math.PI*.4;ctx.beginPath();ctx.arc(footX+Math.cos(angle)*width*stomp, -Math.sin(angle)*height*.05*stomp,Math.max(2,width*.08),0,Math.PI*2);ctx.fill();}}ctx.restore();}
  function drawLargeTrexActor(a,p,moving,now){const s=Math.max(38,state.scale*2.75),direction=a.facing==='west'?-1:1,step=moving?Math.sin(now/115+hash(a.id)*8):0,attack=dinosaurAttack(a,now),bite=attack.weapon==='trexBite'?attack.pulse:0,tail=attack.weapon==='trexTail'?attack.pulse:0;ctx.save();ctx.translate(p.x,p.y);drawDinosaurShadow(s,1.9);ctx.scale(direction*s,s);ctx.translate(bite*.22,0);ctx.lineJoin='round';ctx.lineCap='round';ctx.fillStyle='#486d35';ctx.strokeStyle='#213c25';ctx.lineWidth=.055;ctx.beginPath();ctx.moveTo(-.25,-.86);ctx.bezierCurveTo(-.9,-1.02,-1.75,-.83,-2.55,-.28-tail*.5);ctx.bezierCurveTo(-1.5,-.55,-1.02,-.38,-.55,-.12);ctx.bezierCurveTo(-.16,.08,.34,.03,.62,-.2);ctx.bezierCurveTo(.82,-.38,.72,-.7,.48,-.88);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#5b8242';ctx.beginPath();ctx.ellipse(-.2,-.5,.84,.6,-.15,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.beginPath();ctx.moveTo(.28,-.88);ctx.bezierCurveTo(.55,-1.26,.9,-1.43,1.2,-1.34);ctx.lineTo(1.82,-1.24-bite*.07);ctx.bezierCurveTo(2.08,-1.14,2.03,-.91-bite*.16,1.72,-.84-bite*.14);ctx.lineTo(.96,-.75);ctx.bezierCurveTo(.68,-.67,.5,-.55,.4,-.38);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#cbbf85';ctx.beginPath();ctx.moveTo(.95,-.78);ctx.lineTo(1.78,-.82-bite*.08);ctx.lineTo(1.67,-.6+bite*.28);ctx.lineTo(1.01,-.65+bite*.12);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#fff0c8';for(let tooth=0;tooth<6;tooth++){const x=1.05+tooth*.12;ctx.beginPath();ctx.moveTo(x,-.8);ctx.lineTo(x+.05,-.66+bite*.2);ctx.lineTo(x+.09,-.8);ctx.fill();}ctx.fillStyle='#f1d44e';ctx.beginPath();ctx.arc(1.34,-1.12,.066,0,Math.PI*2);ctx.fill();ctx.fillStyle='#11170d';ctx.beginPath();ctx.arc(1.36,-1.12,.027,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#315126';ctx.lineWidth=.105;ctx.beginPath();ctx.moveTo(.49,-.68);ctx.lineTo(.88,-.47);ctx.lineTo(1.05,-.5);ctx.moveTo(.58,-.63);ctx.lineTo(.94,-.64);ctx.stroke();for(const side of[-1,1]){const phase=side*step*.16;ctx.strokeStyle=side<0?'#35562d':'#466f36';ctx.lineWidth=.23;ctx.beginPath();ctx.moveTo(-.33+side*.29,-.2);ctx.lineTo(-.24+phase,.37);ctx.lineTo(-.5+phase,.65);ctx.lineTo(-.12+phase,.65);ctx.stroke();}if(tail){ctx.strokeStyle='rgba(255,208,94,.75)';ctx.lineWidth=.06;ctx.beginPath();ctx.arc(-1.9,-.25,.72,-2.4,-.35);ctx.stroke();}ctx.restore();}
  function drawBrontosaurusActor(a,p,moving,now){const s=Math.max(42,state.scale*3.65),direction=a.facing==='west'?-1:1,step=moving?Math.sin(now/150+hash(a.id)*7):0,attack=dinosaurAttack(a,now),tail=attack.weapon==='brontosaurusTail'?attack.pulse:0,stomp=attack.weapon==='brontosaurusStomp'?attack.pulse:0;ctx.save();ctx.translate(p.x,p.y);drawDinosaurShadow(s,2.45);ctx.scale(direction*s,s);ctx.lineJoin='round';ctx.lineCap='round';ctx.fillStyle='#59775c';ctx.strokeStyle='#263e31';ctx.lineWidth=.045;ctx.beginPath();ctx.moveTo(-.9,-.58);ctx.bezierCurveTo(-1.55,-.62,-2.25,-.43,-3.05,-.1-tail*.55);ctx.bezierCurveTo(-2.2,-.2,-1.5,-.05,-.86,.16);ctx.closePath();ctx.fill();ctx.stroke();ctx.beginPath();ctx.ellipse(-.35,-.42,1.25,.62,-.04,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.beginPath();ctx.moveTo(.52,-.72);ctx.bezierCurveTo(.9,-1.1,1.2,-1.82,1.62,-2.02);ctx.lineTo(1.82,-1.88);ctx.bezierCurveTo(1.48,-1.45,1.38,-.7,1.12,-.28);ctx.lineTo(.55,-.18);ctx.closePath();ctx.fill();ctx.stroke();ctx.beginPath();ctx.ellipse(1.78,-2.01,.38,.23,-.08,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.fillStyle='#e7d76b';ctx.beginPath();ctx.arc(1.92,-2.09,.035,0,Math.PI*2);ctx.fill();for(const side of[-1,1]){const phase=side*step*.1,lift=side>0?stomp*.52:0;ctx.strokeStyle=side<0?'#405d48':'#59775c';ctx.lineWidth=.22;ctx.beginPath();ctx.moveTo(-.78+side*.72,-.08);ctx.lineTo(-.72+side*.72,.54-lift);ctx.lineTo(-.88+side*.72,.72-lift);ctx.stroke();}if(tail){ctx.strokeStyle='rgba(255,211,105,.78)';ctx.lineWidth=.055;ctx.beginPath();ctx.arc(-2.45,-.1,.78,-2.55,-.25);ctx.stroke();}if(stomp>.45){ctx.strokeStyle=`rgba(222,199,143,${stomp})`;ctx.lineWidth=.045;ctx.beginPath();ctx.arc(.02,.68,.25+stomp*.4,0,Math.PI*2);ctx.stroke();}ctx.restore();}
  function drawStegosaurusActor(a,p,moving,now){const s=Math.max(30,state.scale*2.3),direction=a.facing==='west'?-1:1,step=moving?Math.sin(now/135+hash(a.id)*7):0,attack=dinosaurAttack(a,now),tail=attack.weapon==='stegosaurusTail'?attack.pulse:0;ctx.save();ctx.translate(p.x,p.y);drawDinosaurShadow(s,2.05);ctx.scale(direction*s,s);ctx.lineJoin='round';ctx.fillStyle='#607846';ctx.strokeStyle='#283c25';ctx.lineWidth=.055;ctx.beginPath();ctx.moveTo(-.75,-.65);ctx.bezierCurveTo(-1.25,-.72,-1.9,-.42,-2.5,-.05-tail*.55);ctx.lineTo(-1.3,-.18);ctx.bezierCurveTo(-.7,.27,.48,.3,1.18,-.05);ctx.lineTo(1.9,-.08);ctx.lineTo(1.22,-.45);ctx.bezierCurveTo(.58,-.95,-.62,-1.0,-.75,-.65);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#b88b43';for(let plate=0;plate<7;plate++){const x=-.75+plate*.28,h=.34+Math.sin(plate*Math.PI/6)*.32;ctx.beginPath();ctx.moveTo(x,-.7);ctx.lineTo(x+.13,-.7-h);ctx.lineTo(x+.28,-.68);ctx.closePath();ctx.fill();ctx.stroke();}for(const side of[-1,1]){const phase=side*step*.08;ctx.strokeStyle=side<0?'#445a34':'#607846';ctx.lineWidth=.2;ctx.beginPath();ctx.moveTo(-.55+side*.7,-.05);ctx.lineTo(-.62+side*.7+phase,.57);ctx.lineTo(-.78+side*.7+phase,.68);ctx.stroke();}ctx.fillStyle='#d6bf77';for(const y of[-.34,-.12,.1]){ctx.beginPath();ctx.moveTo(-2.25,y-tail*.4);ctx.lineTo(-2.78,y-.18-tail*.62);ctx.lineTo(-2.5,y+.08-tail*.48);ctx.closePath();ctx.fill();ctx.stroke();}if(tail){ctx.strokeStyle='rgba(255,208,88,.8)';ctx.lineWidth=.06;ctx.beginPath();ctx.arc(-2.15,-.08,.8,-2.5,-.2);ctx.stroke();}ctx.fillStyle='#e8d66e';ctx.beginPath();ctx.arc(1.55,-.22,.04,0,Math.PI*2);ctx.fill();ctx.restore();}
  function drawRaptorActor(a,p,moving,now){const s=Math.max(17,state.scale*.8),direction=a.facing==='west'?-1:1,step=moving?Math.sin(now/82+hash(a.id)*9):0,attack=dinosaurAttack(a,now),bite=attack.weapon==='raptorBite'?attack.pulse:0;ctx.save();ctx.translate(p.x,p.y);drawDinosaurShadow(s,1.55);ctx.scale(direction*s,s);ctx.translate(bite*.24,0);ctx.lineJoin='round';ctx.lineCap='round';ctx.fillStyle='#7b633d';ctx.strokeStyle='#312a20';ctx.lineWidth=.065;ctx.beginPath();ctx.moveTo(-.2,-.7);ctx.bezierCurveTo(-.75,-.82,-1.5,-.45,-2.15,-.12);ctx.bezierCurveTo(-1.25,-.28,-.75-.02,-.28,.1);ctx.bezierCurveTo(.2,.24,.62,-.05,.58,-.42);ctx.closePath();ctx.fill();ctx.stroke();ctx.beginPath();ctx.moveTo(.3,-.6);ctx.lineTo(.76,-1.05);ctx.lineTo(1.38,-.98-bite*.08);ctx.lineTo(1.65,-.78-bite*.2);ctx.lineTo(.88,-.7+bite*.15);ctx.lineTo(.52,-.35);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#dfd19d';for(let tooth=0;tooth<4;tooth++){const x=.95+tooth*.13;ctx.beginPath();ctx.moveTo(x,-.84);ctx.lineTo(x+.05,-.68+bite*.18);ctx.lineTo(x+.09,-.83);ctx.fill();}ctx.strokeStyle='#5b472f';ctx.lineWidth=.15;for(const side of[-1,1]){const phase=side*step*.24;ctx.beginPath();ctx.moveTo(-.18+side*.25,-.05);ctx.lineTo(-.1+phase,.48);ctx.lineTo(-.4+phase,.7);ctx.lineTo(-.12+phase,.66);ctx.stroke();}ctx.strokeStyle='#5b472f';ctx.lineWidth=.09;ctx.beginPath();ctx.moveTo(.5,-.48);ctx.lineTo(.95,-.22);ctx.lineTo(1.05,-.34);ctx.stroke();ctx.fillStyle='#f1d44e';ctx.beginPath();ctx.arc(1.05,-.9,.055,0,Math.PI*2);ctx.fill();ctx.fillStyle='#111';ctx.beginPath();ctx.arc(1.07,-.9,.022,0,Math.PI*2);ctx.fill();ctx.restore();}
  function drawTrexActor(a,p,moving,now){const s=Math.max(20,state.scale*.92),direction=a.facing==='west'?-1:1,step=moving?Math.sin(now/115+hash(a.id)*8):0,jaw=moving?.12+.05*Math.sin(now/90):.07,start=Date.parse(a.eventStartedAtUtc||0),end=Date.parse(a.eventEndsAtUtc||0),portal=Date.now()-start<6000||end-Date.now()<6000;ctx.save();ctx.translate(p.x,p.y);if(portal){ctx.strokeStyle='#b659ff';ctx.lineWidth=Math.max(5,s*.11);ctx.shadowColor='#d78aff';ctx.shadowBlur=20;ctx.beginPath();ctx.ellipse(0,-s*.62,s*1.7,s*.72,0,0,Math.PI*2);ctx.stroke();ctx.shadowBlur=0;}ctx.fillStyle='rgba(0,0,0,.34)';ctx.beginPath();ctx.ellipse(0,s*.2,s*1.45,s*.22,0,0,Math.PI*2);ctx.fill();ctx.scale(direction*s,s);ctx.lineJoin='round';ctx.lineCap='round';ctx.fillStyle='#466a34';ctx.strokeStyle='#263f26';ctx.lineWidth=.055;ctx.beginPath();ctx.moveTo(-.15,-.82);ctx.bezierCurveTo(-.7,-1.02,-1.45,-.76,-2.05,-.38);ctx.bezierCurveTo(-1.35,-.55,-.92,-.42,-.5,-.18);ctx.bezierCurveTo(-.26,.02,.24,.04,.58,-.17);ctx.bezierCurveTo(.78,-.3,.78,-.57,.6,-.78);ctx.bezierCurveTo(.35-0,-.97,.08-1.02,-.15,-.82);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#587d3c';ctx.beginPath();ctx.ellipse(-.2,-.48,.78,.58,-.15,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.beginPath();ctx.moveTo(.3,-.85);ctx.bezierCurveTo(.52,-1.18,.84,-1.36,1.08,-1.28);ctx.lineTo(1.62,-1.22);ctx.bezierCurveTo(1.85,-1.16,1.84,-.94,1.6,-.86);ctx.lineTo(.92,-.77);ctx.bezierCurveTo(.66,-.7,.49,-.62,.4,-.45);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#d4c68a';ctx.beginPath();ctx.moveTo(.92,-.81);ctx.lineTo(1.64,-.84);ctx.bezierCurveTo(1.73,-.78,1.69,-.68,1.52,-.65);ctx.lineTo(.99,-.69);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#f4e8c0';for(let tooth=0;tooth<5;tooth++){const x=1.05+tooth*.12;ctx.beginPath();ctx.moveTo(x,-.82);ctx.lineTo(x+.05,-.7+jaw);ctx.lineTo(x+.09,-.83);ctx.fill();}ctx.fillStyle='#f3d458';ctx.beginPath();ctx.arc(1.28,-1.08,.065,0,Math.PI*2);ctx.fill();ctx.fillStyle='#10170d';ctx.beginPath();ctx.arc(1.3,-1.08,.026,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#315126';ctx.lineWidth=.11;ctx.beginPath();ctx.moveTo(.48,-.66);ctx.lineTo(.83,-.45);ctx.lineTo(1.02,-.48);ctx.moveTo(.55,-.61);ctx.lineTo(.88,-.62);ctx.stroke();for(const side of [-1,1]){const legPhase=side*step*.18;ctx.strokeStyle=side<0?'#35562d':'#456b35';ctx.lineWidth=.22;ctx.beginPath();ctx.moveTo(-.3+side*.28,-.2);ctx.lineTo(-.23+legPhase,.36);ctx.lineTo(-.48+legPhase,.62);ctx.lineTo(-.15+legPhase,.62);ctx.stroke();}ctx.strokeStyle='#6f944d';ctx.lineWidth=.035;for(let stripe=0;stripe<5;stripe++){ctx.beginPath();ctx.moveTo(-.72+stripe*.22,-.82);ctx.lineTo(-.62+stripe*.22,-.56);ctx.stroke();}ctx.restore();}
  function drawBearActor(a,p,moving,now){const event=a.subtype==='eventBear',s=Math.max(event?18:10,state.scale*(event?.78:.53)),direction=a.facing==='west'?-1:1,step=moving?Math.sin(now/120+hash(a.id)*7):0;ctx.save();ctx.translate(p.x,p.y);ctx.fillStyle='rgba(0,0,0,.34)';ctx.beginPath();ctx.ellipse(0,s*.16,s*1.18,s*.2,0,0,Math.PI*2);ctx.fill();ctx.scale(direction*s,s);ctx.lineJoin='round';ctx.strokeStyle='#24170f';ctx.lineWidth=.06;const fur=event?'#56331f':'#49352a',highlight=event?'#744729':'#675044';for(const side of [-1,1]){const phase=side*step*.1;ctx.fillStyle=side<0?'#35251e':fur;ctx.beginPath();ctx.roundRect(-.62+side*.48+phase,-.2,.36,.72,.16);ctx.fill();ctx.stroke();}ctx.fillStyle=fur;ctx.beginPath();ctx.ellipse(-.12,-.5,1.05,.68,-.08,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.fillStyle=highlight;ctx.beginPath();ctx.ellipse(-.2,-.72,.7,.25,-.1,0,Math.PI*2);ctx.fill();ctx.globalAlpha=.45;ctx.beginPath();ctx.ellipse(-.35,-.55,.5,.35,-.2,0,Math.PI*2);ctx.fill();ctx.globalAlpha=1;ctx.fillStyle=fur;ctx.beginPath();ctx.arc(.72,-.72,.55,0,Math.PI*2);ctx.fill();ctx.stroke();for(const earX of [.48,.9]){ctx.fillStyle='#3a251b';ctx.beginPath();ctx.arc(earX,-1.13,.18,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.fillStyle='#96664c';ctx.beginPath();ctx.arc(earX,-1.13,.09,0,Math.PI*2);ctx.fill();}ctx.fillStyle='#a27b61';ctx.beginPath();ctx.ellipse(1.04,-.66,.43,.3,0,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.fillStyle='#17110d';ctx.beginPath();ctx.ellipse(1.34,-.72,.14,.11,0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.arc(.9,-.86,.045,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#d9c8aa';ctx.lineWidth=.025;for(let claw=0;claw<3;claw++){ctx.beginPath();ctx.moveTo(-.43+claw*.1,.5);ctx.lineTo(-.39+claw*.1,.59);ctx.stroke();ctx.beginPath();ctx.moveTo(.52+claw*.1,.5);ctx.lineTo(.56+claw*.1,.59);ctx.stroke();}ctx.strokeStyle=event?'#d9aa82':'#7b5c49';ctx.lineWidth=.035;ctx.beginPath();ctx.moveTo(.62,-.98);ctx.lineTo(.82,-.78);ctx.moveTo(.72,-1.02);ctx.lineTo(.9,-.8);ctx.stroke();ctx.restore();}
  function drawGorillaActor(a,p,moving,now){const s=Math.max(16,state.scale*.9),swing=moving?Math.sin(now/105+hash(a.id)*6):0;ctx.save();ctx.translate(p.x,p.y);ctx.fillStyle='rgba(0,0,0,.38)';ctx.beginPath();ctx.ellipse(0,s*.12,s*.95,s*.22,0,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#171817';ctx.lineWidth=Math.max(2,s*.06);ctx.fillStyle='#303532';ctx.beginPath();ctx.ellipse(0,-s*.65,s*.62,s*.78,0,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.lineCap='round';ctx.lineWidth=s*.34;ctx.beginPath();ctx.moveTo(-s*.42,-s*.58);ctx.lineTo(-s*.78+swing*s*.12,s*.08);ctx.moveTo(s*.42,-s*.58);ctx.lineTo(s*.78-swing*s*.12,s*.08);ctx.stroke();ctx.fillStyle='#4d514c';ctx.beginPath();ctx.ellipse(0,-s*.77,s*.34,s*.38,0,0,Math.PI*2);ctx.fill();ctx.fillStyle='#918373';ctx.beginPath();ctx.ellipse(0,-s*.68,s*.25,s*.2,0,0,Math.PI*2);ctx.fill();ctx.fillStyle='#0d0f0e';ctx.beginPath();ctx.arc(-s*.11,-s*.83,s*.035,0,Math.PI*2);ctx.arc(s*.11,-s*.83,s*.035,0,Math.PI*2);ctx.fill();ctx.fillStyle='#fff0ba';ctx.font='9px monospace';ctx.textAlign='center';ctx.fillText(a.name,0,-s*1.3);ctx.restore();}
  function realityInversionPortal(actor,time=Date.now()){
    const start=Date.parse(actor.eventStartedAtUtc||''),end=Date.parse(actor.eventEndsAtUtc||''),duration=(actor.eventPortalDurationSeconds||6)*1000;
    if(!Number.isFinite(start)||!Number.isFinite(end))return null;
    if(time<start||time>=end)return{hidden:true};
    const entering=time<start+duration,leaving=time>=end-duration;
    if(!entering&&!leaving)return null;
    const phase=Math.max(0,Math.min(1,entering?(time-start)/duration:(time-(end-duration))/duration)),smooth=t=>t*t*(3-2*t),clamp=t=>Math.max(0,Math.min(1,t));
    return{entering,open:smooth(clamp(phase/.15))*smooth(clamp((1-phase)/.2)),reveal:smooth(clamp(entering?(phase-.15)/.65:(.8-phase)/.65))};
  }
  function inversionPortalBounds(a){
    const scale=state.scale;
    if(a.subtype==='giant'){const height=Math.max(120,scale*15.24*.52);return{width:height*.44,height:height*1.3,floor:height*.13};}
    if(a.subtype==='ufo'){const size=Math.max(18,scale*1.2);return{width:size*1.65,height:size*2,floor:-30+size*.8};}
    const sizes={tRex:[38,2.75],brontosaurus:[42,3.65],stegosaurus:[30,2.3],raptor:[17,.8],eventBear:[18,.78]},spec=sizes[a.subtype]||[18,1],size=Math.max(spec[0],scale*spec[1]);
    return{width:size*(a.subtype==='eventBear'?1.8:3.4),height:size*4,floor:size*.85};
  }
  function drawRealityInversionPortal(p,b,portal,now,front=false){
    const width=b.width*portal.open,height=Math.max(6,b.width*.2)*portal.open;if(width<.1)return;
    ctx.save();ctx.translate(p.x,p.y+b.floor);ctx.shadowColor='#bc6cff';ctx.shadowBlur=22;ctx.lineWidth=3;
    if(!front){const glow=ctx.createRadialGradient(0,0,0,0,0,width);glow.addColorStop(0,'#09051c');glow.addColorStop(.7,'#382270');glow.addColorStop(1,'#cb7bff');ctx.fillStyle=glow;ctx.beginPath();ctx.ellipse(0,0,width,height,0,0,Math.PI*2);ctx.fill();}
    ctx.strokeStyle=front?'#edbdff':'#9657ff';ctx.beginPath();ctx.ellipse(0,0,width,height,0,front?0:Math.PI,front?Math.PI:Math.PI*2);ctx.stroke();
    if(front)for(let i=0;i<10;i++){const angle=now/700+i*Math.PI/5;ctx.fillStyle=i%2?'#97f7ff':'#f6c8ff';ctx.beginPath();ctx.arc(Math.cos(angle)*width,Math.sin(angle)*height,2*portal.open,0,Math.PI*2);ctx.fill();}
    ctx.restore();
  }
  function drawActor(a,detail,now){
    const portal=realityInversionPortal(a);if(!portal){drawActorBody(a,detail,now);return;}if(portal.hidden)return;
    const p=toScreen(a.position),bounds=inversionPortalBounds(a);drawRealityInversionPortal(p,bounds,portal,now);
    ctx.save();ctx.beginPath();ctx.rect(p.x-bounds.width,p.y-bounds.height,bounds.width*2,bounds.height+bounds.floor);ctx.clip();
    ctx.translate(0,bounds.height*(1-portal.reveal));if(portal.reveal>0)drawActorBody(a,detail,now,true);ctx.restore();
    drawRealityInversionPortal(p,bounds,portal,now,true);
  }
  function drawActorBody(a,detail,now,inPortal=false){if(Inversions.drawActor(ctx,a,now))return;const abduction=abductionVisual(a,now),p=toScreen(abduction?.position||a.position),moving=!isSleeping(a)&&!inPortal&&!abduction&&(a.isMoving||state.movingUntil.get(a.id)>now)&&detail===2,bob=moving?Math.sin(now/90+hash(a.id)*6)*2:0;if(Survival.drawAnimal(ctx,a,p,state.scale))return;if(a.subtype==='fish'||a.subtype==='waterMonster'){drawWaterCreature(a,p,now);return;}if(a.subtype==='ufo'){drawUfoCraft(p.x,p.y-30,Math.max(18,state.scale*1.2),false,now);return;}if(a.subtype==='giant'){drawGiantActor(a,p,moving,now);drawAttachedFire(a.id,p,now);return;}if(a.subtype==='tRex'){drawLargeTrexActor(a,p,moving,now);drawAttachedFire(a.id,p,now);return;}if(a.subtype==='brontosaurus'){drawBrontosaurusActor(a,p,moving,now);drawAttachedFire(a.id,p,now);return;}if(a.subtype==='stegosaurus'){drawStegosaurusActor(a,p,moving,now);drawAttachedFire(a.id,p,now);return;}if(a.subtype==='raptor'){drawRaptorActor(a,p,moving,now);drawAttachedFire(a.id,p,now);return;}if(a.subtype==='bear'||a.subtype==='eventBear'){drawBearActor(a,p,moving,now);drawAttachedFire(a.id,p,now);return;}const animal=a.kind==='animal';const colors={rabbit:'#dad4c5',dog:'#9b6b3e',cat:'#77736a',bird:'#6ba1a5',deer:'#9c7148',cougar:'#c29154'};ctx.save();ctx.globalAlpha=abduction?.alpha??1;ctx.translate(p.x,p.y+bob-(abduction?.lift||0));if(a.subtype==='zombie'){drawPersonShape(0,0,'#9db77b','#594d6c',moving,now,hash(a.id));ctx.fillStyle='#b9e58b';ctx.beginPath();ctx.arc(-4,-state.scale*.52,2,0,Math.PI*2);ctx.arc(4,-state.scale*.52,2,0,Math.PI*2);ctx.fill();}else if(animal){const size=Math.max(3,state.scale*(a.subtype==='deer'?.43:.3));ctx.fillStyle=colors[a.subtype]||'#c7a36a';ctx.beginPath();ctx.ellipse(0,-size*.5,size,size*.62,0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.arc(size*.7,-size*.75,size*.45,0,Math.PI*2);ctx.fill();}else drawPersonShape(0,0,'#c9864f','#324e7a',moving,now,hash(a.id));ctx.fillStyle='#fff0ba';ctx.font='9px monospace';ctx.textAlign='center';ctx.fillText(a.name,0,-state.scale*1.35);ctx.restore();drawAttachedFire(a.id,p,now);}
  function abductionVisual(player,now){
    const capture=player.abduction;
    if(capture){const age=Math.max(0,(Date.now()-Date.parse(capture.startedAtUtc))/1000),pilot=state.players.get(capture.pilotId),ship=age<25&&pilot?.travelMode==='ufo'?pilot.position:capture.shipPosition,height=ufoFlightHeightPixels(),mix=(a,b,t)=>({...b,x:a.x+(b.x-a.x)*t,y:a.y+(b.y-a.y)*t});if(age<10){const t=age/10;return{position:mix(capture.origin,ship,t),lift:height*t,alpha:1};}if(age<25)return{position:ship,lift:height,alpha:0};const lowerSeconds=(Date.parse(capture.endsAtUtc)-Date.parse(capture.loweringAtUtc))/1000,t=Math.min(1,(age-25)/lowerSeconds);return{position:mix(capture.shipPosition,capture.dropPosition,t),lift:height*(1-t),alpha:1};}
    const event=state.abductions.get(player.id);if(!event)return null;const duration=1800,age=now-event.started;if(age>=duration){state.abductions.delete(player.id);return null;}const t=Math.max(0,Math.min(1,age/duration)),pickupEnd=.34,dropStart=.62,from=t<pickupEnd?event.origin:event.ufo,to=t<pickupEnd?event.ufo:event.destination,amount=t<pickupEnd?t/pickupEnd:t<dropStart?0:(t-dropStart)/(1-dropStart),position=t>=pickupEnd&&t<dropStart?event.ufo:{...to,x:from.x+(to.x-from.x)*amount,y:from.y+(to.y-from.y)*amount},lift=t<pickupEnd?30*(t/pickupEnd):t<dropStart?30:30*(1-amount),alpha=t>=pickupEnd&&t<dropStart?.18:1;return{position,lift,alpha};}
  function drawPlayer(player,isMe,detail,now){if(drawUfoTransition(player,now,isMe,detail))return;if(Inversions.swim(ctx,player,isMe,now))return;if(player.ridingBusId)return;if(player.waitingAtBusStopId&&!player.abduction&&!isSleeping(player)){const stop=(state.transit?.stops||[]).find(s=>s.id===player.waitingAtBusStopId)||{position:player.position};BusTransit.drawWaitingPlayer(ctx,player,stop,toScreen,state.scale,isMe);return;}const abduction=abductionVisual(player,now),p=toScreen(abduction?.position||player.position),moving=!isSleeping(player)&&!abduction&&state.movingUntil.get(player.id)>now&&detail===2,facing=state.facings.get(player.id)||'south',flying=player.travelMode==='ufo',flightHeight=flying?ufoFlightHeightPixels():0,probulatorOn=flying&&state.probulatorBeams.has(player.id);ctx.save();ctx.globalAlpha=abduction?.alpha??1;ctx.translate(p.x,p.y-(abduction?.lift||0));if(abduction){ctx.fillStyle='rgba(80,255,119,.24)';ctx.beginPath();ctx.arc(0,0,Math.max(10,state.scale*.8),0,Math.PI*2);ctx.fill();}if(flying)drawUfoGroundShadow(0,0,Math.max(18,state.scale*1.1));ctx.translate(0,-flightHeight);drawPersonShape(0,0,player.equippedWeapon==='zombieBite'?'#9db77b':isMe?'#e6c86c':'#d68855',isMe?'#37689a':'#784f91',moving,now,hash(player.id),player.travelMode,facing,probulatorOn);if(detail>0&&!flying&&!isSleeping(player)&&player.equippedWeapon&&!['none','fist'].includes(player.equippedWeapon)){const attack=heldWeaponAttacks.get(player.id),age=attack?now-attack.started:Infinity,aiming=age<700,ready=aiming||(isMe?['attackReady','aggressive','defensive'].includes(state.actionMode):true),left=facing==='west';let angle=ready?-.15:1.05;if(aiming){const target=toScreen(attack.end);angle=Math.atan2(target.y-p.y,target.x-p.x);}ctx.save();ctx.translate((left?-.2:.2)*state.scale,-state.scale*.65);if(aiming){ctx.rotate(angle);if(Math.cos(angle)<0)ctx.scale(1,-1);}else{ctx.scale(left?-1:1,1);ctx.rotate(angle);}HeldWeapons.draw(ctx,player.equippedWeapon,state.scale,{now,moving,recoil:age>=0&&age<180?Math.sin(age/180*Math.PI):0});ctx.restore();}if(player.shieldOn&&detail>0&&!flying)drawWornShield();const wornHat=player.equippedHat&&player.equippedHat!=='none'?player.equippedHat:player.hatOn?'hat':null;if(wornHat&&detail>0&&!flying)drawWornHat(player.travelMode,wornHat);if(detail>0){ctx.fillStyle=isMe?'#fff3a5':'#f0e8d1';ctx.font='700 9px monospace';ctx.textAlign='center';if(flying){const labelY=-Math.max(26,state.scale*.9)-18;ctx.fillText(isMe?'YOU':player.name,0,labelY);ctx.fillStyle=probulatorOn?'#69ff86':'#ff8b82';ctx.fillText(`PROBULATOR ${probulatorOn?'ON':'OFF'}`,0,labelY+14);}else ctx.fillText((isMe?'YOU':player.name)+(player.displayTitle?' · '+player.displayTitle:''),0,-state.scale*1.55);}ctx.restore();drawAttachedFire(player.id,flying?{x:p.x,y:p.y-flightHeight}:p,now);}
  function drawWornShield(){const s=Math.max(6,state.scale*.42);ctx.save();ctx.translate(-state.scale*.48,-state.scale*.18);ctx.fillStyle='#778992';ctx.strokeStyle='#e1d2a2';ctx.lineWidth=Math.max(1.5,state.scale*.08);ctx.beginPath();ctx.moveTo(-s*.62,-s*.65);ctx.lineTo(s*.62,-s*.65);ctx.lineTo(s*.48,s*.25);ctx.quadraticCurveTo(0,s,-s*.48,s*.25);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#d7b750';ctx.beginPath();ctx.arc(0,-s*.08,s*.17,0,Math.PI*2);ctx.fill();ctx.restore();}
  function drawWornHat(mode,itemType='hat'){const s=Math.max(5,state.scale*.46),y=['bike','dirtBike','motorcycle'].includes(mode)?-s*1.52:mode==='skateboard'?-s*1.22:-s*1.28;ctx.fillStyle=itemType==='coolingHat'?'#45a7bd':itemType==='warmHat'?'#9b493d':'#69482f';ctx.fillRect(-s*.48,y-s*.12,s*.96,s*.2);ctx.beginPath();ctx.arc(0,y-s*.08,s*.34,Math.PI,0);ctx.fill();}
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
  function drawRaftRider(x,y,skin,shirt,moving,now,phase,facing,s){
    const angle={north:0,south:Math.PI,east:Math.PI/2,west:-Math.PI/2}[facing]??0,bob=Math.sin(now/520+phase*3)*s*.025,row=moving?Math.sin(now/165+phase*7):0;
    ctx.save();ctx.translate(x,y+bob);ctx.rotate(angle);
    ctx.fillStyle='rgba(0,0,0,.28)';ctx.beginPath();ctx.ellipse(0,s*.2,s*1.02,s*1.58,0,0,Math.PI*2);ctx.fill();
    if(moving){ctx.strokeStyle='rgba(190,238,239,.55)';ctx.lineWidth=Math.max(1.5,s*.08);for(const side of[-1,1]){ctx.beginPath();ctx.moveTo(side*s*.48,s*1.25);ctx.quadraticCurveTo(side*s*(1.15+Math.abs(row)*.25),s*(1.7+row*.12),side*s*1.48,s*2.05);ctx.stroke();}}
    const hull=ctx.createLinearGradient(-s,0,s,0);hull.addColorStop(0,'#ad5b2d');hull.addColorStop(.48,'#ef9b4b');hull.addColorStop(1,'#8b431f');ctx.fillStyle=hull;ctx.strokeStyle='#40271c';ctx.lineWidth=Math.max(2,s*.13);ctx.beginPath();ctx.ellipse(0,0,s*.88,s*1.52,0,0,Math.PI*2);ctx.fill();ctx.stroke();
    ctx.fillStyle='#304d55';ctx.strokeStyle='#e2b06b';ctx.lineWidth=Math.max(1.5,s*.07);ctx.beginPath();ctx.ellipse(0,s*.04,s*.55,s*1.13,0,0,Math.PI*2);ctx.fill();ctx.stroke();
    ctx.fillStyle='#b9753d';for(const seatY of[-s*.34,s*.42])ctx.fillRect(-s*.55,seatY-s*.07,s*1.1,s*.14);
    for(const side of[-1,1]){const stroke=side*row*s*.34,gripX=side*s*.2,endX=side*s*1.55,endY=s*(.28+side*row*.38);ctx.strokeStyle='#79502d';ctx.lineWidth=Math.max(2,s*.09);ctx.lineCap='round';ctx.beginPath();ctx.moveTo(gripX,-s*.2);ctx.lineTo(endX,endY);ctx.stroke();ctx.fillStyle='#d8a45e';ctx.save();ctx.translate(endX,endY);ctx.rotate(-side*.62+row*.18);ctx.beginPath();ctx.ellipse(0,side*s*.18,s*.16,s*.42,0,0,Math.PI*2);ctx.fill();ctx.restore();}
    ctx.strokeStyle='#292421';ctx.lineWidth=Math.max(2,s*.18);ctx.lineCap='round';ctx.beginPath();ctx.moveTo(-s*.16,-s*.12);ctx.lineTo(-s*.38,s*.28);ctx.lineTo(-s*.18,s*.63);ctx.moveTo(s*.16,-s*.12);ctx.lineTo(s*.38,s*.28);ctx.lineTo(s*.18,s*.63);ctx.stroke();
    ctx.fillStyle=shirt;ctx.beginPath();ctx.roundRect(-s*.38,-s*.88,s*.76,s*.72,s*.16);ctx.fill();ctx.strokeStyle=skin;ctx.lineWidth=Math.max(2,s*.14);ctx.beginPath();ctx.moveTo(-s*.3,-s*.68);ctx.lineTo(-s*.2,-s*.22);ctx.moveTo(s*.3,-s*.68);ctx.lineTo(s*.2,-s*.22);ctx.stroke();ctx.fillStyle=skin;ctx.beginPath();ctx.arc(0,-s*1.1,s*.34,0,Math.PI*2);ctx.fill();ctx.restore();
  }
  const ufoFlightHeightPixels=()=>Math.max(240,Math.min(480,innerHeight*.46,state.scale*10));
  function drawUfoGroundShadow(x,y,s){ctx.save();ctx.translate(x,y);ctx.fillStyle='rgba(0,0,0,.18)';ctx.beginPath();ctx.ellipse(0,0,s*1.35,s*.38,0,0,Math.PI*2);ctx.fill();ctx.strokeStyle='rgba(95,255,128,.15)';ctx.lineWidth=2;ctx.stroke();ctx.restore();}
  function drawUfoCraft(x,y,s,occupied,now,drawShadow=true,probulatorOn=false){ctx.save();ctx.translate(x,y);if(drawShadow){ctx.fillStyle='rgba(0,0,0,.3)';ctx.beginPath();ctx.ellipse(0,s*.78,s*1.25,s*.25,0,0,Math.PI*2);ctx.fill();}const hull=ctx.createLinearGradient(-s,0,s,0);hull.addColorStop(0,'#667977');hull.addColorStop(.5,'#d0e3df');hull.addColorStop(1,'#4e6463');ctx.fillStyle=hull;ctx.strokeStyle=probulatorOn?'#70ff8c':'#d7fff2';ctx.lineWidth=probulatorOn?3:2;ctx.shadowColor=probulatorOn?'#55ff78':'transparent';ctx.shadowBlur=probulatorOn?15:0;ctx.beginPath();ctx.ellipse(0,0,s*1.25,s*.4,0,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.shadowBlur=0;ctx.fillStyle='rgba(102,220,255,.72)';ctx.beginPath();ctx.ellipse(0,-s*.25,s*.58,s*.43,0,Math.PI,0);ctx.fill();ctx.stroke();if(occupied){ctx.fillStyle='#e6c86c';ctx.beginPath();ctx.arc(0,-s*.32,s*.13,0,Math.PI*2);ctx.fill();}for(let i=0;i<6;i++){const a=i*Math.PI/3+now/900;ctx.fillStyle=probulatorOn?'#55ff78':i%2?'#ff4949':'#55ff78';ctx.beginPath();ctx.arc(Math.cos(a)*s*.92,Math.sin(a)*s*.22,s*.08,0,Math.PI*2);ctx.fill();}ctx.restore();}
  function ufoTransitionFrame(event,now){
    if(!event)return null;
    const age=Math.max(0,now-event.started),entering=event.to==='ufo',duration=entering?1550:1500;
    if(age>=duration)return null;
    const clamp=t=>Math.max(0,Math.min(1,t)),ease=t=>{t=clamp(t);return t*t*(3-2*t);};
    const lift=entering?ease((age-350)/1050):1-ease(age/1000);
    return {entering,lift,fly:entering?(clamp(age/350)-1)**3:ease((age-1050)/450),beam:entering?age>=350&&age<1400:age<1000,occupied:entering?lift>=.98:age<80,personAlpha:clamp((1-lift)/.12)};
  }
  function drawUfoTransition(player,now,isMe,detail){
    const event=state.vehicleTransitions.get(player.id),frame=ufoTransitionFrame(event,now);
    if(!frame){if(event)state.vehicleTransitions.delete(player.id);return false;}
    const ground=toScreen(frame.entering?player.position:event.origin),person=toScreen(player.position),height=ufoFlightHeightPixels(),body=Math.max(5,state.scale*.46),s=body*1.9;
    const x=ground.x+frame.fly*(viewportWidth()+s*3),y=ground.y-height-body*.42-Math.abs(frame.fly)*140;
    drawUfoGroundShadow(ground.x,ground.y,Math.max(18,state.scale*1.1)*(1-Math.abs(frame.fly)));
    if(frame.beam){ctx.save();ctx.fillStyle='rgba(82,255,112,.26)';ctx.beginPath();ctx.moveTo(x-s*.25,y+s*.25);ctx.lineTo(ground.x-s*.8,ground.y);ctx.lineTo(ground.x+s*.8,ground.y);ctx.lineTo(x+s*.25,y+s*.25);ctx.closePath();ctx.fill();ctx.restore();}
    if(frame.personAlpha>0){
      ctx.save();ctx.globalAlpha=frame.personAlpha;
      const mode=frame.lift>0?'walk':frame.entering?event.from:event.to;
      drawPersonShape(person.x,person.y-frame.lift*height,player.equippedWeapon==='zombieBite'?'#9db77b':isMe?'#e6c86c':'#d68855',isMe?'#37689a':'#784f91',false,now,hash(player.id),mode,state.facings.get(player.id)||'south');ctx.restore();
    }
    drawUfoCraft(x,y,s,frame.occupied,now,false,state.probulatorBeams.has(player.id));
    if(detail>0){ctx.save();ctx.fillStyle=isMe?'#fff3a5':'#f0e8d1';ctx.font='700 9px monospace';ctx.textAlign='center';ctx.fillText(isMe?'YOU':player.name,person.x,person.y-frame.lift*height-state.scale*1.55);ctx.restore();}
    drawAttachedFire(player.id,{x:person.x,y:person.y-frame.lift*height},now);
    return true;
  }
  function drawPersonShape(x,y,skin,shirt,moving,now,phase,mode='walk',facing='south',probulatorOn=false){
    const s=Math.max(5,state.scale*.46),cycle=Math.sin(now/105+phase*6);
    if(mode==='ufo'){drawUfoCraft(x,y-s*.42,s*1.9,true,now,false,probulatorOn);return;}
    if(mode==='bike'||mode==='eBike'){drawBikeRider(x,y,skin,mode==='eBike'?'#4ba6b8':shirt,moving,now,phase,facing,s);return;}
    if(mode==='dirtBike'){drawDirtBikeRider(x,y,skin,shirt,moving,now,phase,facing,s);return;}
    if(mode==='motorcycle'){drawMotorcycleRider(x,y,skin,shirt,moving,now,phase,facing,s);return;}
    if(mode==='raft'){drawRaftRider(x,y,skin,shirt,moving,now,phase,facing,s);return;}
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
    for(const chest of state.chests.values()){if(!pointVisible(chest.position,view)||!dynamicVisible(chest.position)||chest.isOpened||chest.expiresAtUtc&&Date.parse(chest.expiresAtUtc)<=Date.now())continue;if(state.dungeon&&!state.dungeon.underwater&&!revealedAt(chest.position.x,chest.position.y))continue;const p=toScreen(chest.position),s=Math.max(7,state.scale*.5)*(chest.isGrand?1.65:1);ctx.fillStyle=chest.isGrand?'#b78022':'#9a5f24';ctx.strokeStyle='#f2c75c';ctx.lineWidth=2;ctx.fillRect(p.x-s,p.y-s*.7,s*2,s*1.25);ctx.strokeRect(p.x-s,p.y-s*.7,s*2,s*1.25);ctx.fillStyle='#f2c75c';ctx.fillRect(p.x-2,p.y-s*.2,4,5);if(!state.dungeon&&!state.seenChests.has(chest.id)){state.seenChests.add(chest.id);send({type:'chestSeen',chestId:chest.id});}}
    for(const loot of state.loot.values()){if(Date.parse(loot.expiresAtUtc)<=Date.now())continue;if(!pointVisible(loot.position,view)||!dynamicVisible(loot.position))continue;if(NorthernExposure.drawSyrup(ctx,loot,toScreen,state))continue;const p=toScreen(loot.position);if(loot.dropKind==='tombstone'){const s=Math.max(9,state.scale*.46);ctx.fillStyle='#8d8a84';ctx.strokeStyle='#302f2d';ctx.lineWidth=2;ctx.beginPath();ctx.roundRect(p.x-s*.55,p.y-s*1.55,s*1.1,s*1.5,s*.28);ctx.fill();ctx.stroke();ctx.fillStyle='#343230';ctx.font=`700 ${Math.max(9,s*.42)}px monospace`;ctx.textAlign='center';ctx.fillText('RIP',p.x,p.y-s*.9);ctx.font=`700 ${Math.max(7,s*.28)}px monospace`;ctx.fillText(loot.ownerName||'Explorer',p.x,p.y-s*.48,s*.9);}else{ctx.fillStyle='#b6b0a2';ctx.beginPath();ctx.moveTo(p.x,p.y-15);ctx.lineTo(p.x+9,p.y);ctx.lineTo(p.x-9,p.y);ctx.closePath();ctx.fill();ctx.strokeStyle='#333';ctx.stroke();ctx.fillStyle='#ffd257';ctx.beginPath();ctx.arc(p.x+8,p.y-2+Math.sin(now/180)*2,4,0,Math.PI*2);ctx.fill();}}
  }

  const hazardWeaponTypes=new Set(['chloramineGasBottle','chloramineGasJar','chlorineGasBottle','chlorineGasJar','chloroformGasBottle','chloroformGasJar','peraceticAcidGasBottle','peraceticAcidGasJar','napalmBottle','napalmJar']);
  const weaponTypes=new Set([...hazardWeaponTypes,'camera','fist','knife','sword','hockeyStick','iceSkate','rock','slingshot','crossbow','pistol','rifle','spearGun','ar15','machineGun','flamethrower','rocketLauncher','grenade','molotovCocktail','probulator']);
  const ammoTypes=new Set(['rock','ballBearing','arrow','bullet','rocket','spear','film']);
  const travelVehicleTypes=new Set(['skateboard','bike','eBike','dirtBike','motorcycle','inflatableRaft','ufo']);
  const vehicleTypes=new Set(['skateboard','bike','eBike','dirtBike','motorcycle','inflatableRaft','ufo','gallonOfGas']);
  const questConfigurationItems=new Set(['rock','arrow','gallonOfGas','bike','skateboard','pencil','pen','marker','sprayPaint','book','calculator','cellPhone','lockPickSet']);
  function photographForItem(itemType){return [...(state.privateState?.inventory?.items||[]),...(state.privateState?.homeItemStorage?.items||[]),...(state.privateState?.loot||[]).flatMap(l=>l.items||[]),...(state.chestContents?.items||[])].find(i=>i.itemType===itemType)?.photograph;}
  function viewPhotograph(photo){const dialog=document.createElement('dialog'),heading=document.createElement('h2'),details=document.createElement('p'),close=document.createElement('button');heading.textContent=photo.subject;details.textContent=new Date(photo.takenAtUtc).toLocaleString()+' · '+(photo.kind==='event'?'Event creature evidence':photo.kind==='inspection'?'Dungeon inspection evidence':'Scenery')+'. Submitting or selling consumes this print.';close.textContent='Close photograph';close.type='button';dialog.className='photograph-dialog';dialog.append(heading);if(photo.thumbnailUrl?.startsWith('/api/photographs/')){const img=document.createElement('img');img.src=photo.thumbnailUrl;img.alt=photo.subject;img.width=256;img.height=192;dialog.append(img);}dialog.append(details,close);close.addEventListener('click',()=>dialog.close());dialog.addEventListener('close',()=>dialog.remove());document.body.append(dialog);dialog.showModal();}
  function itemDisplayName(itemType){if(itemType.startsWith('photograph:'))return 'Photograph: '+(photographForItem(itemType)?.subject||'Print');return state.privateState?.serverConfiguration?.items?.find(item=>item.itemType===itemType)?.displayName||title(itemType);}
  function itemSection(item){const definition=item?.storageSection?item:state.privateState?.serverConfiguration?.items?.find(def=>def.itemType===item?.itemType);return InventorySections.section(item,definition,vehicleTypes,ammoTypes,weaponTypes,questConfigurationItems,equipmentSlotByItem);}
  const itemGlyphs={...Object.fromEntries(['apple','pear','peach','orange','banana','blackberry','raspberry','blueberry','strawberry','cranberry','watermelon','carrot','potato','corn','beans','spinach','lettuce','garlic','onion','peppers','tomato','pumpkin','cheeseburger','cheese','milk','egg','antibiotics','ring'].map(t=>[t,Survival.glyph(t)])),scubaGear:'🤿',spearGun:'➶',spear:'➶',fish:'🐟',hockeyStick:'🏒',iceSkate:'⛸',mapleSyrup:'🍯',canadianMoney:'🍁',fist:'✊',openHand:'🖐',rock:'🪨',ballBearing:'●',knife:'🔪',sword:'⚔',slingshot:'Y',crossbow:'🏹',arrow:'➶',pistol:'🔫',rifle:'︻',ar15:'︻',machineGun:'︻',rocketLauncher:'🚀',rocket:'🚀',grenade:'●',molotovCocktail:'🔥',probulator:'⌁',bullet:'•',shield:'🛡',personalFlag:'⚑',skateboard:'🛹',bike:'🚲',eBike:'🚲',dirtBike:'🏍',motorcycle:'🏍',ufo:'🛸',gallonOfGas:'⛽',inflatableRaft:'🛶',flashlight:'🔦',lantern:'🏮',candle:'🕯',laser:'⌁',lockPickSet:'🗝',magicHikingShoes:'🥾',magicRunningShoes:'👟',hat:'👒',coolingHat:'🧢',warmHat:'🧢',tShirt:'👕',coolingShirt:'👕',longSleeveShirt:'👕',sweater:'🧥',lightJacket:'🧥',winterJacket:'🧥',fireproofJacket:'🧥',coolingShorts:'🩳',warmingPants:'👖',water:'💧',food:'🍎',energyDrink:'🥤',areaMap:'🗺',pencil:'✎',pen:'✒',marker:'▮',sprayPaint:'▣',book:'📕',calculator:'🧮',cellPhone:'📱',newspaper:'📰',wood:'🪵',kindling:'≋',metal:'⚙',bed:'🛏',dresser:'▤',wardrobe:'🚪',storageChest:'▣',homeShopCounter:'🏪',shop:'🏪',nightstand:'▣',chair:'🪑',armchair:'🪑',sofa:'▰',ottoman:'▰',table:'▱',desk:'▱',bookshelf:'▥',barstool:'♜',bench:'▬',cabinet:'▤',vanity:'◫',lamp:'💡',rug:'▧',fireplace:'🔥',plant:'🪴',clock:'🕰',piano:'🎹'};
  itemGlyphs.flamethrower='🔥';
  for(const weapon of hazardWeaponTypes)itemGlyphs[weapon]=weapon.startsWith('napalm')?'🔥':'☁';
  Object.assign(itemGlyphs,{kryptonite:'💎',craftingSkillBook:'📗',glassScrap:'◇',rubber:'◯',mechanicalParts:'⚙',electronics:'▥',battery:'🔋',wax:'▣',weaponParts:'⚙',powerCore:'✦',powderBase:'◈',sparkBinder:'✧',emptyGlassJar:'🫙',stingingEssence:'✦',noxiousEssence:'✦',dreamEssence:'✧',causticEssence:'✦',emberGel:'🔥',bindingResin:'◈',cloudBinder:'☁',laundryDetergent:'🧴'});
  Object.assign(itemGlyphs,{dirtyWater:'🟤',purifiedWater:'💧',stove:'♨',garageWorkbench:'🛠',weaponsBench:'⚒',waterFilter:'▧',craftingTable:'🛠',emptyGlassBottle:'🍾',emptyPlasticBottle:'🧴',crustySocks:'🧦',soiledUnderwear:'🩲',paper:'📄',cloth:'▧',plastic:'♳',styrofoam:'▤',drugs:'💊',charcoal:'◼',potassiumNitrate:'⚗',salt:'🧂',pepper:'🧂',sulfur:'🟡',bleach:'🧴',ammonia:'🧴',sulfuricAcid:'⚗',cookingOil:'🫙',flour:'▣',sugar:'▣',cookingSupplies:'🍳',craftingGas:'⛽',gunpowder:'◉','recipe:gunpowder':'📜','recipe:molotovCocktail':'📜'});
  const equipmentSlotByItem={hat:'hat',coolingHat:'hat',warmHat:'hat',tShirt:'shirt',coolingShirt:'shirt',longSleeveShirt:'shirt',sweater:'shirt',lightJacket:'shirt',winterJacket:'shirt',fireproofJacket:'shirt',coolingShorts:'pants',warmingPants:'pants',magicHikingShoes:'shoes',magicRunningShoes:'shoes',flashlight:'offhand',lantern:'offhand',candle:'offhand',laser:'offhand',shield:'offhand'};
  function equippedItem(me,slot){if(!me)return'none';if(slot==='hat')return me.equippedHat&&me.equippedHat!=='none'?me.equippedHat:me.hatOn?'hat':'none';if(slot==='gloves')return me.equippedGloves||'none';if(slot==='shirt')return me.equippedShirt||'none';if(slot==='pants')return me.equippedPants||'none';if(slot==='shoes')return me.magicHikingShoesOn?'magicHikingShoes':me.magicRunningShoesOn?'magicRunningShoes':'none';if(slot==='offhand')return me.shieldOn?'shield':me.laserOn?'laser':me.lanternOn?'lantern':me.flashlightOn?'flashlight':candleActive(me)?'candle':'none';return'none';}
  function createItemArt(itemType,properties={},imageKey=null){if(itemType.startsWith('photograph:')){const photo=properties.photograph||photographForItem(itemType);if(photo?.thumbnailUrl?.startsWith('/api/photographs/')){const img=document.createElement('img');img.className='item-art photograph-print';img.width=128;img.height=96;img.loading='lazy';img.src=photo.thumbnailUrl;img.alt='Photograph: '+photo.subject;img.title=photo.subject+' / '+new Date(photo.takenAtUtc).toLocaleString();return img;}imageKey='photograph';}const canvas=document.createElement('canvas');canvas.width=84;canvas.height=84;canvas.className='item-art';const c=canvas.getContext('2d'),key=imageKey||properties.imageKey||itemType,type=properties.furnitureType||properties.objectType||key,color=furnitureColors[properties.color]||'#365c46',pattern=properties.pattern||'solid';const gradient=c.createLinearGradient(0,0,84,84);gradient.addColorStop(0,shade(color,28));gradient.addColorStop(1,shade(color,-30));c.fillStyle=gradient;c.fillRect(0,0,84,84);c.strokeStyle='rgba(255,236,165,.45)';c.lineWidth=3;c.strokeRect(3,3,78,78);if(pattern==='striped'||pattern==='plaid')for(let x=-40;x<120;x+=14){c.beginPath();c.moveTo(x,0);c.lineTo(x+40,84);c.stroke();}if(pattern==='plaid')for(let y=10;y<84;y+=16){c.beginPath();c.moveTo(0,y);c.lineTo(84,y);c.stroke();}const glyph=itemGlyphs[key]||itemGlyphs[type]||itemGlyphs[itemType]||(itemType.startsWith('photograph:')?'🖼':itemType.startsWith('recipe:')?'📜':'◆');c.font=`${glyph.length>2?36:46}px "Segoe UI Emoji","Trebuchet MS",sans-serif`;c.textAlign='center';c.textBaseline='middle';c.fillStyle='#fff7d1';c.shadowColor='rgba(0,0,0,.7)';c.shadowBlur=4;if(itemType==='scubaGear')Scuba.gear(c,40,45,57);else c.fillText(glyph,42,44);canvas.title=properties.displayName||title(itemType);return canvas;}
  const itemCategory=item=>{const section=itemSection(item);return section==='misc'?'other':section;};
  const stackWeight=item=>(Number(item?.unitWeightPounds)||0)*(Number(item?.quantity)||0);
  const weightText=weight=>`${weight<.1&&weight>0?weight.toFixed(2):weight.toFixed(1)} lb`;
  function energyDrinkPhase(me,now=Date.now()){if(!me)return null;const boostUntil=Date.parse(me.energyDrinkBoostUntilUtc||''),crashUntil=Date.parse(me.energyDrinkCrashUntilUtc||'');if(Number.isFinite(boostUntil)&&boostUntil>now)return{phase:'boost',until:me.energyDrinkBoostUntilUtc};if(Number.isFinite(crashUntil)&&crashUntil>now)return{phase:'crash',until:me.energyDrinkCrashUntilUtc};return null;}
  function probedPhase(me,now=Date.now()){if(!me)return null;const until=Date.parse(me.probedUntilUtc||'');return Number.isFinite(until)&&until>now?{phase:'probed',until:me.probedUntilUtc}:null;}
  function effectiveCarryingCapacity(me,fallback=50){if(me?.godMode)return Infinity;const phase=energyDrinkPhase(me),base=Number(state.privateState?.progression?.carryingCapacity)||Number(fallback)||50;return phase?.phase==='boost'?base*2:phase?.phase==='crash'?base*.2:base;}
  function updateBackpackSummary(inventory,me){if(!inventory)return;const weight=Number(inventory.weightPounds)||0,maximumWeight=effectiveCarryingCapacity(me,inventory.maximumWeightPounds),penalty=me?.godMode?0:Math.min(50,weight);ui.backpackSummary.textContent=`Wallet $${((me?.walletCents||0)/100).toFixed(2)} · ${weightText(weight)} / ${Number.isFinite(maximumWeight)?maximumWeight.toFixed(0)+' lb':'Unlimited'}${me?.godMode?' · no God Mode speed penalty':penalty>0?` · ${penalty.toFixed(0)}% slower`:''}${me?.travelMode==='eBike'?` · E-bike battery ${Math.max(0,Number(me.eBikeRemainingMeters||0)/1609.344*100).toFixed(0)}%`:me?.travelMode==='dirtBike'?` · Dirt bike ${Number(me.dirtBikeGasGallons||0).toFixed(2)} gal`:me?.travelMode==='motorcycle'?` · Motorcycle ${Number(me.motorcycleGasGallons||0).toFixed(2)} gal`:me?.travelMode==='ufo'?` · UFO fuel ${(Number(me.ufoRemainingMeters||0)/1609.344).toFixed(2)} mi loaded`:''}`;ui.backpackSummary.classList.toggle('over-capacity',weight>maximumWeight+.001);}
  function updateEnergyDrinkTelemetry(me){updateBackpackSummary(state.privateState?.inventory,me);const effects=[],energy=energyDrinkPhase(me),probed=probedPhase(me);if(me?.abduction){const age=Math.max(0,(Date.now()-Date.parse(me.abduction.startedAtUtc))/1000),phase=age<10?'Lifting into UFO':age<25?'Aboard UFO':'Lowering to ground';effects.push(`${phase} · ${Math.max(0,Math.ceil((Date.parse(me.abduction.endsAtUtc)-Date.now())/1000))}s remaining`);}if(energy)effects.push(energy.phase==='boost'?`Energy drink: 2× speed and 100 lb capacity · no sleep · ${countdown(energy.until)}`:`Energy-drink crash: ⅕ speed and 10 lb capacity · sleep to recover · ${countdown(energy.until)}`);if(probed)effects.push(`Probed: ½ speed · walk only · ${countdown(probed.until)} · sleep to recover`);if(effects.length)ui.effects.textContent=ui.effects.textContent==='None'?effects.join(' · '):`${ui.effects.textContent} · ${effects.join(' · ')}`;syncProbedTravelControls(me,probed);}
  function syncProbedTravelControls(me,probed=probedPhase(me)){for(const button of document.querySelectorAll('[data-mode]'))syncTravelButtonState(button,me,probed);}
  function dropControls(itemType,quantity){const controls=document.createElement('span');controls.className='drop-controls';for(const amount of [1,10,50]){if(quantity<amount)continue;const button=document.createElement('button');button.type='button';button.className='drop-item';button.textContent=`Drop ${amount}`;button.title=`Drop ${Math.min(amount,quantity)} ${itemType.startsWith('photograph:')?'photograph':title(itemType).toLowerCase()} on the ground`;button.disabled=quantity<1;button.addEventListener('pointerdown',event=>event.stopPropagation());button.addEventListener('click',event=>{event.stopPropagation();send({type:'dropItem',itemType,quantity:Math.min(amount,quantity)});});controls.append(button);}return controls;}
  function inventoryRenderKey(inventory,me){
    return JSON.stringify([inventory,state.inventoryTab,me?.equippedWeapon,me?.godMode,state.privateState?.godModeLoadout,
      ['hat','shirt','pants','shoes','offhand'].map(slot=>equippedItem(me,slot)),
      state.privateState?.serverConfiguration?.items?.map(item=>[item.itemType,item.displayName])]);
  }
  function renderInventory(inventory){
    const me=state.players.get(state.playerId);updateBackpackSummary(inventory,me);
    const renderKey=inventoryRenderKey(inventory,me)+':'+(me?.equippedGloves||'none');if(state.inventoryRenderKey===renderKey)return;
    ui.inventory.replaceChildren();
    const items=(inventory?.items||[]).filter(item=>!vehicleTypes.has(item.itemType)),quantities=new Map();
    if(me?.godMode)for(const item of (state.privateState?.godModeLoadout||[]).filter(item=>!vehicleTypes.has(item.itemType))){
      if(!items.some(owned=>owned.itemType===item.itemType&&owned.quantity>0))items.push({...item,virtual:true});
    }
    for(const item of items)quantities.set(item.itemType,(quantities.get(item.itemType)||0)+item.quantity);
    ui.weaponSlotCount.textContent=`${inventory?.weaponSlotsUsed||0}`;ui.weaponSlotCount.title='Unlimited weapon slots';ui.questSlotCount.textContent=`${inventory?.questSlotsUsed||0}`;ui.otherSlotCount.textContent=String(items.filter(item=>itemCategory(item)==='other').length);
    document.querySelectorAll('[data-inventory-tab]').forEach(button=>button.classList.toggle('active',button.dataset.inventoryTab===state.inventoryTab));
    const count=itemType=>quantities.get(itemType)||0,weaponRows=[
      {weapon:'fist',label:'Fist',always:true},
      {weapon:'camera',label:'Camera',ammo:'film'},
      {weapon:'rock',label:'Throw rock',art:'openHand',ammo:'rock'},
      {weapon:'slingshot',label:'Slingshot',ammo:'ballBearing',showWithoutWeapon:count('ballBearing')>0},
      {weapon:'knife',label:'Knife'},
      {weapon:'sword',label:'Sword'},
      {weapon:'hockeyStick',label:'Hockey stick'},
      {weapon:'iceSkate',label:'Ice skate'},
      {weapon:'crossbow',label:'Crossbow',ammo:'arrow'},
      {weapon:'pistol',label:'Pistol',ammo:'bullet'},
      {weapon:'rifle',label:'Rifle',ammo:'bullet'},
      {weapon:'spearGun',label:'Spear gun',ammo:'spear'},
      {weapon:'ar15',label:'AR15',ammo:'bullet'},
      {weapon:'machineGun',label:'Machine gun',ammo:'bullet'},
      {weapon:'flamethrower',label:'Flamethrower',ammo:'gallonOfGas'},
      {weapon:'rocketLauncher',label:'Rocket launcher',ammo:'rocket'},
      {weapon:'grenade',label:'Grenade',ammo:'grenade'},
      {weapon:'molotovCocktail',label:'Molotov cocktail',ammo:'molotovCocktail'},
      ...[...hazardWeaponTypes].map(weapon=>({weapon,label:itemDisplayName(weapon),ammo:weapon}))
    ];
    if(state.inventoryTab==='weapon')for(const spec of weaponRows){
      const owned=spec.weapon==='fist'||count(spec.weapon)>0;
      if(!spec.always&&!owned&&!spec.showWithoutWeapon)continue;
      const row=document.createElement('span'),weaponSide=document.createElement('span'),weaponText=document.createElement('span'),uses=document.createElement('span'),ammoSide=document.createElement('span');
      row.className=`weapon-loadout${owned?' weapon-item':' unavailable'}`;
      weaponSide.className='weapon-side';weaponText.className='weapon-label';uses.className='weapon-consumes';ammoSide.className='weapon-ammo';uses.textContent='uses';
      if(owned){
        weaponSide.append(createItemArt(spec.weapon,{displayName:spec.label},spec.art));
        const weaponStack=items.find(item=>item.itemType===spec.weapon),weight=weaponStack?stackWeight(weaponStack):0;
        weaponText.textContent=spec.weapon==='fist'?`${spec.label} · unlimited`:`${weaponStack?.quality?`${weaponStack.quality} `:''}${count(spec.weapon)>1?`${spec.label} ×${count(spec.weapon)}`:spec.label}`;
        if(weaponStack){const weightDetail=document.createElement('small');weightDetail.className='item-weight';weightDetail.textContent=`${weightText(Number(weaponStack.unitWeightPounds)||0)} each · ${weightText(weight)} stack`;weaponText.append(document.createElement('br'),weightDetail);}
        row.draggable=true;row.title=`Drag ${spec.label.toLowerCase()} to the weapon slot${weight?` · ${weightText(Number(weaponStack.unitWeightPounds)||0)} each · ${weightText(weight)} stack`:''}`;row.dataset.weapon=spec.weapon;
        row.addEventListener('dragstart',event=>{event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('text/weapon',spec.weapon);event.dataTransfer.setData('text/plain',spec.weapon);});
      }else{
        const empty=document.createElement('span');empty.className='item-art empty-weapon-slot';empty.setAttribute('aria-label','Empty slingshot slot');weaponSide.append(empty);weaponText.textContent='Slingshot not found';row.title='Find a slingshot to use these ball bearings.';
      }
      weaponSide.append(weaponText);if(owned){const equip=document.createElement('button');equip.type='button';equip.textContent=me?.equippedWeapon===spec.weapon?'Equipped':'Equip';equip.disabled=me?.equippedWeapon===spec.weapon;equip.setAttribute('aria-label',`Equip ${spec.label}`);equip.addEventListener('click',()=>send({type:'setEquipment',slot:'weapon',itemType:spec.weapon}));weaponSide.append(equip);}if(owned&&spec.weapon!=='fist'&&!items.find(item=>item.itemType===spec.weapon)?.virtual)weaponSide.append(dropControls(spec.weapon,count(spec.weapon)));
      if(spec.ammo){ammoSide.append(createItemArt(spec.ammo));const ammoText=document.createElement('span');ammoText.textContent=`${itemDisplayName(spec.ammo)} ×${count(spec.ammo)}`;ammoSide.append(ammoText);}
      else{ammoSide.classList.add('no-ammo');ammoSide.textContent='Nothing';}
      row.append(weaponSide,uses,ammoSide);ui.inventory.append(row);
    }
    for(const item of items.filter(item=>itemCategory(item)===state.inventoryTab&&(state.inventoryTab==='ammo'||!weaponTypes.has(item.itemType)))){
      const chip=document.createElement('span'),label=itemDisplayName(item.itemType);
      chip.className='inventory-item';
      chip.append(createItemArt(item.itemType,{photograph:item.photograph}));if(item.photograph)chip.title='Captured '+new Date(item.photograph.takenAtUtc).toLocaleString()+'. Submit for matching quest proof or sell to a vendor.';const text=document.createElement('span'),name=document.createElement('span'),itemWeight=document.createElement('span');name.textContent=`${item.quality?`${item.quality} `:''}${label} ×${item.quantity}`;itemWeight.className='item-weight';itemWeight.textContent=item.carriedInBackpack===false?`${weightText(Number(item.unitWeightPounds)||0)} each · parked`:`${weightText(Number(item.unitWeightPounds)||0)} each · ${weightText(stackWeight(item))} stack`;text.append(name,itemWeight);chip.append(text);
      const definition=state.privateState?.serverConfiguration?.items?.find(def=>def.itemType===item.itemType);if(definition?.nutrition||definition?.storageSection==='gloves'||item.itemType==='antibiotics'){const benefit=document.createElement('small');benefit.className='item-weight';benefit.textContent=definition?.effect||'';benefit.style.whiteSpace='normal';benefit.style.maxWidth='340px';text.append(benefit);chip.title=definition?.effect||'';}
      const slot=equipmentSlotByItem[item.itemType],wearable=!!slot;
      if(item.photograph){const view=document.createElement('button');view.type='button';view.textContent='View photo';view.addEventListener('click',event=>{event.stopPropagation();viewPhotograph(item.photograph);});chip.append(view);}
      if(wearable){chip.classList.add('wearable-item');chip.draggable=true;chip.title=`${definition?.effect||''} Drag ${label.toLowerCase()} to the ${slot} slot`;chip.addEventListener('dragstart',event=>{event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('text/equipment',`${slot}:${item.itemType}`);event.dataTransfer.setData('text/plain',item.itemType);});}
      if(item.itemType==='scubaGear'){const button=document.createElement('button');button.type='button';button.textContent=state.dungeon?.underwater?'Surface':'Submerge';button.disabled=!state.dungeon?.underwater&&(!['shallowWater','deepWater'].includes(me?.terrain)||!!state.dungeon);button.addEventListener('click',()=>send({type:'setTravelMode',mode:state.dungeon?.underwater?'walk':'scuba'}));chip.append(button);}
      if(!item.virtual&&(state.privateState?.serverConfiguration?.items?.find(def=>def.itemType===item.itemType)?.nutrition||item.itemType==='antibiotics'||item.itemType==='fish'||item.itemType==='mapleSyrup'||item.itemType==='food'||item.itemType==='water'||item.itemType==='energyDrink'||item.itemType==='gallonOfGas'||item.itemType==='gardeningBook'||item.itemType==='craftingSkillBook'||item.itemType.startsWith('recipe:')||item.itemType.startsWith('quest:food:')||wearable)){const button=document.createElement('button');button.type='button';if(wearable){const equipped=equippedItem(me,slot)===item.itemType;button.textContent=item.itemType==='candle'?(equipped?'Extinguish':'Light'):'Equip / unequip';button.title=item.itemType==='candle'?(equipped?'Extinguish the candle; it is not returned to inventory':'Light for one minute'):(equipped?'Unequip':'Equip');button.classList.toggle('equipped',equipped);button.addEventListener('click',()=>send({type:'setEquipment',slot,itemType:equipped?null:item.itemType}));}else{button.textContent=item.itemType.startsWith('recipe:')?'Study recipe':item.itemType.startsWith('quest:food:')?'Eat delivery (crime)':item.itemType==='gardeningBook'?'Read (+5% garden success)':item.itemType==='craftingSkillBook'?'Read (+1 crafting level)':item.itemType==='gallonOfGas'?'Add gas':'Consume';button.title=item.itemType.startsWith('quest:food:')?'Fails the delivery immediately, reports food theft, and blocks deliveries for 24 hours':item.itemType==='craftingSkillBook'?'Consumes this book and grants one crafting level':item.itemType==='gallonOfGas'?'Add to selected vehicle':item.itemType==='energyDrink'?'Boost speed and carrying capacity for 15 minutes, followed by a 5-minute crash':'Consume';button.addEventListener('click',()=>send({type:'consumeItem',itemType:item.itemType}));}chip.append(button);}
      if(!item.virtual&&item.itemType!=='personalFlag'&&!item.itemType.startsWith('quest:food:'))chip.append(dropControls(item.itemType,item.quantity));
      ui.inventory.append(chip);
    }
    if(!ui.inventory.children.length){const empty=document.createElement('div');empty.className='inventory-empty';empty.textContent=state.inventoryTab==='quest'?'No quest items':'No items in this category';ui.inventory.append(empty);}
    state.inventoryRenderKey=renderKey;
  }
  function renderHomeStorage(items){ui.homeStorageItems.replaceChildren();const furniture=items||[];ui.homeStorageCount.textContent=`${furniture.length} item${furniture.length===1?'':'s'}`;if(!furniture.length){const empty=document.createElement('div');empty.className='home-storage-empty';empty.textContent='No stored furniture';ui.homeStorageItems.append(empty);return;}for(const item of furniture){const button=document.createElement('button');button.type='button';button.className='home-storage-item';button.append(createItemArt(item.id,item.properties,item.properties?.imageKey));const name=document.createElement('span');name.textContent=`${title(item.properties?.color||'')} ${title(item.properties?.pattern||'')} ${item.properties?.displayName||title(item.properties?.objectType||'furniture')}`.trim();button.append(name);button.addEventListener('click',()=>{state.movingFurniture=item;state.furniturePreview={x:state.players.get(state.playerId)?.position.x||2,y:state.players.get(state.playerId)?.position.y||2};showToast('Drag this item to an open floor position.');});ui.homeStorageItems.append(button);}}
  function storageTransferRow(item,toStorage){const row=document.createElement('div'),label=document.createElement('label'),input=document.createElement('input'),button=document.createElement('button');row.className='storage-transfer-row';row.title=state.privateState?.serverConfiguration?.items?.find(def=>def.itemType===item.itemType)?.effect||'';row.append(createItemArt(item.itemType));label.textContent=`${item.quality?item.quality+' ':''}${itemDisplayName(item.itemType)} ×${item.quantity} · ${weightText(stackWeight(item))}`;input.type='number';input.min='1';input.max=String(item.quantity);input.value='1';button.type='button';button.textContent=toStorage?'Store':'Take';button.addEventListener('click',()=>{const quantity=Math.max(1,Math.min(item.quantity,Math.floor(Number(input.value)||1)));send({type:'transferHomeStorage',chestId:state.storageChestId,itemType:item.itemType,quantity,toStorage});});if(travelVehicleTypes.has(item.itemType)){label.textContent+=' · Available in Travel';row.append(label);}else row.append(label,input,button);return row;}
  function renderHomeItemStorage(){ui.homeItemStorageSection.hidden=!state.storageChestOpen;$('#homeFurnitureSection').hidden=state.homeStorageTab!=='furniture';if(!state.storageChestOpen)return;if(ui.chestStorageItems.contains(ui.homeStorageItems))$('#homeFurnitureSection').append(ui.homeStorageItems);ui.backpackStorageItems.replaceChildren();ui.chestStorageItems.replaceChildren();document.querySelectorAll('[data-home-storage-tab]').forEach(button=>{const active=button.dataset.homeStorageTab===state.homeStorageTab;button.classList.toggle('active',active);button.setAttribute('aria-selected',String(active));});const inSection=item=>itemSection(item)===state.homeStorageTab,backpack=(state.privateState?.inventory?.items||[]).filter(item=>item.itemType!=='fist'&&item.itemType!=='personalFlag'&&!item.itemType.startsWith('quest:food:')&&inSection(item)),stored=(state.privateState?.homeItemStorage?.items||[]).filter(inSection),me=state.players.get(state.playerId),cash=Number(state.privateState?.homeStorageMoneyCents)||0,label={vehicle:'vehicles',weapon:'weapons',ammo:'ammunition',quest:'quest items',clothing:'clothing',offhand:'offhand items',food:'food or water',crafting:'crafting items',misc:'other items'}[state.homeStorageTab]||'items';ui.homeMoneyBalance.textContent=`Stored: $${(cash/100).toFixed(2)} · Wallet: $${((me?.walletCents||0)/100).toFixed(2)}`;ui.homeStorageCount.textContent=state.homeStorageTab==='furniture'?`${state.privateState?.homeStorage?.length||0} stored furniture`:`${stored.length} stored item types`;for(const item of backpack)ui.backpackStorageItems.append(storageTransferRow(item,true));for(const item of stored)ui.chestStorageItems.append(storageTransferRow(item,false));if(state.homeStorageTab==='furniture'){ui.chestStorageItems.append(ui.homeStorageItems);ui.homeStorageItems.hidden=false;}if(!backpack.length){const empty=document.createElement('div');empty.className='storage-transfer-empty';empty.textContent=`No ${label} on character`;ui.backpackStorageItems.append(empty);}if(!stored.length&&state.homeStorageTab!=='furniture'){const empty=document.createElement('div');empty.className='storage-transfer-empty';empty.textContent=`No ${label} in Home inventory`;ui.chestStorageItems.append(empty);}}
  function renderPostalItems(){ui.postalItems.replaceChildren();const items=(state.privateState?.inventory?.items||[]).filter(item=>item.itemType!=='fist'&&item.itemType!=='personalFlag'&&item.carriedInBackpack!==false);if(!items.length){ui.postalItems.innerHTML='<div class="storage-transfer-empty">No mailable items in your backpack.</div>';return;}for(const item of items){const row=document.createElement('div'),label=document.createElement('label'),input=document.createElement('input'),button=document.createElement('button');row.className='storage-transfer-row';row.title=state.privateState?.serverConfiguration?.items?.find(def=>def.itemType===item.itemType)?.effect||'';row.append(createItemArt(item.itemType));label.textContent=`${item.quality?item.quality+' ':''}${itemDisplayName(item.itemType)} ×${item.quantity} · ${weightText(stackWeight(item))}`;input.type='number';input.min='1';input.max=String(item.quantity);input.value='1';button.type='button';button.textContent='Send Home';button.addEventListener('click',()=>send({type:'transferPostOfficeItem',boxId:state.actionPostal?.id,itemType:item.itemType,quantity:Math.max(1,Math.min(item.quantity,Math.floor(Number(input.value)||1)))}));row.append(label,input,button);ui.postalItems.append(row);}}
  function restoreHelpPanel(popup=helpPopup){if(ui.titlePanel.ownerDocument!==document){document.adoptNode(ui.titlePanel);ui.rightRail.insertBefore(ui.titlePanel,$('#statsPanel'));}ui.titlePanel.hidden=true;if(helpPopup===popup)helpPopup=null;}
  function closeHelpWindow(){const popup=helpPopup;restoreHelpPanel(popup);if(popup&&!popup.closed)popup.close();}
  function openHelpWindow(){
    if(helpPopup?.closed)restoreHelpPanel(helpPopup);if(helpPopup&&!helpPopup.closed){helpPopup.focus();return;}
    const popup=window.open('','alternative-reality-help','popup=yes,width=600,height=560,resizable=yes,scrollbars=yes');if(!popup){showToast('Allow pop-ups for this site to open Help.');return;}helpPopup=popup;popup.document.documentElement.lang='en';const meta=popup.document.createElement('meta'),titleElement=popup.document.createElement('title'),stylesheet=popup.document.createElement('link'),host=popup.document.createElement('main'),controls=popup.document.createElement('section'),header=popup.document.createElement('div'),heading=popup.document.createElement('h2'),close=popup.document.createElement('button'),list=popup.document.createElement('div');meta.name='viewport';meta.content='width=device-width, initial-scale=1';titleElement.textContent='AlternativeReality — Help & Reality';stylesheet.rel='stylesheet';stylesheet.href=new URL('styles.css?v=60',location.href).href;host.className='help-popup-host';controls.className='panel help-popup-controls';header.className='help-popup-header';heading.textContent='Controls';close.type='button';close.textContent='Close';close.addEventListener('click',closeHelpWindow);header.append(heading,close);list.className='help-control-list';for(const [keys,description]of[['WASD / Arrows','Move'],['Left click','Travel / use weapon'],['Left / right drag','Pan'],['Right click / double-click','Actions'],['Wheel','Zoom']]){const row=popup.document.createElement('div'),key=popup.document.createElement('kbd'),text=popup.document.createElement('span');key.textContent=keys;text.textContent=description;row.append(key,text);list.append(row);}controls.append(header,list);popup.document.head.replaceChildren(meta,titleElement,stylesheet);popup.document.body.className='help-popup-body';popup.document.body.replaceChildren(host);if(ui.titlePanel.classList.contains('panel-collapsed'))ui.titlePanel.querySelector('.panel-collapse-button')?.click();popup.document.adoptNode(ui.titlePanel);ui.titlePanel.hidden=false;host.append(ui.titlePanel,controls);popup.addEventListener('beforeunload',()=>restoreHelpPanel(popup),{once:true});popup.focus();
  }
  function restoreServerConfigPanel(popup=serverConfigPopup){if(ui.serverConfigWindow.ownerDocument!==document){document.adoptNode(ui.serverConfigWindow);ui.rightRail.append(ui.serverConfigWindow);}ui.serverConfigWindow.hidden=true;if(serverConfigPopup===popup)serverConfigPopup=null;}
  function closeServerConfigWindow(){const popup=serverConfigPopup;restoreServerConfigPanel(popup);if(popup&&!popup.closed)popup.close();}
  function openServerConfigWindow(){
    if(serverConfigPopup?.closed)restoreServerConfigPanel(serverConfigPopup);
    if(serverConfigPopup&&!serverConfigPopup.closed){serverConfigPopup.focus();renderServerConfiguration(state.privateState?.serverConfiguration);return;}
    const popup=window.open('','alternative-reality-server-configuration','popup=yes,width=1240,height=850,resizable=yes,scrollbars=yes');
    if(!popup){showToast('Allow pop-ups for this site to open Server Config.');return;}
    serverConfigPopup=popup;popup.document.documentElement.lang='en';const meta=popup.document.createElement('meta'),titleElement=popup.document.createElement('title'),stylesheet=popup.document.createElement('link'),host=popup.document.createElement('main');meta.name='viewport';meta.content='width=device-width, initial-scale=1';titleElement.textContent='AlternativeReality — Server Config';stylesheet.rel='stylesheet';stylesheet.href=new URL('styles.css?v=60',location.href).href;host.id='serverConfigPopupHost';popup.document.head.replaceChildren(meta,titleElement,stylesheet);popup.document.body.className='server-config-popup-body';popup.document.body.replaceChildren(host);if(ui.serverConfigWindow.classList.contains('panel-collapsed'))ui.serverConfigWindow.querySelector('.panel-collapse-button')?.click();popup.document.adoptNode(ui.serverConfigWindow);host.append(ui.serverConfigWindow);ui.serverConfigWindow.hidden=false;renderServerConfiguration(state.privateState?.serverConfiguration);popup.addEventListener('beforeunload',()=>restoreServerConfigPanel(popup),{once:true});popup.focus();
  }
  function renderServerConfiguration(configuration){
    if(ui.serverConfigWindow.hidden)return;
    if(!configuration?.items||!configuration.movement||!configuration.events)return;
    const key=JSON.stringify(configuration);if(state.serverConfigRenderKey===key)return;
    populateServerConfiguration(configuration);state.serverConfigRenderKey=key;
  }
  function populateServerConfiguration(configuration){
    if(!configuration?.items||!configuration.movement||!configuration.events)return;const itemTargets={vehicle:ui.serverConfigVehicles,weapon:ui.serverConfigWeapons,ammo:ui.serverConfigAmmo,quest:ui.serverConfigQuestItems,misc:ui.serverConfigMisc};for(const target of Object.values(itemTargets))target.replaceChildren();
    const movement=configuration.movement;ui.baseSpeedConfig.value=String(movement.baseSpeedMph);ui.baseVisibilityConfig.value=String(movement.baseVisibilityMeters);renderModifierInputs(ui.terrainSpeedConfig,movement.terrainSpeedModifiersMph);renderModifierInputs(ui.travelSpeedConfig,movement.travelModeSpeedModifiersMph);
    ui.saveMovementConfig.onclick=()=>send({type:'updateMovementConfiguration',baseSpeedMph:Number(ui.baseSpeedConfig.value),baseVisibilityMeters:Number(ui.baseVisibilityConfig.value),terrainSpeedModifiersMph:modifierValues(ui.terrainSpeedConfig),travelModeSpeedModifiersMph:modifierValues(ui.travelSpeedConfig)});
    for(const item of configuration.items){
      const row=document.createElement('div'),info=document.createElement('div'),name=document.createElement('strong'),effect=document.createElement('small'),fields=document.createElement('div');row.className='server-config-row';row.dataset.item=item.itemType;info.className='server-config-item';name.textContent=item.displayName;effect.textContent=item.effect;info.append(name,effect);fields.className='server-config-fields';const damage=configNumber(item.damage,.25,0,100),range=configNumber(item.rangeMeters,.1,0,2000),accuracy=configNumber(item.accuracy??1,.01,0,1),attackInterval=configNumber(item.attackIntervalSeconds??.5,.05,.05,10),speed=configNumber(item.speedModifierMph||0,.1,-500,1000),vision=configNumber(item.visibilityModifierMeters||0,1,-5000,5000),minimum=configNumber(item.minimumPriceCents/100,.01,0,1e9),maximum=configNumber(item.maximumPriceCents/100,.01,0,1e9),fieldValues=[['Damage',damage],['Range m',range],['Accuracy',accuracy],['Attack sec',attackInterval],['Speed +/-',speed],['Vision +/-',vision],['Min price',minimum],['Max price',maximum]];for(const [labelText,input]of fieldValues){const label=document.createElement('label'),caption=document.createElement('span');label.className='server-config-field';caption.textContent=labelText;label.append(caption,input);fields.append(label);}const save=document.createElement('button');save.type='button';save.className='server-config-save';save.textContent='Save';save.addEventListener('click',()=>{const min=Math.round(Number(minimum.value)*100),max=Math.round(Number(maximum.value)*100);if(!Number.isFinite(min)||!Number.isFinite(max)||min>max){showToast('Minimum price must not exceed maximum price.');return;}send({type:'updateItemConfiguration',itemType:item.itemType,damage:Number(damage.value),rangeMeters:Number(range.value),accuracy:Number(accuracy.value),attackIntervalSeconds:Number(attackInterval.value),speedModifierMph:Number(speed.value),visibilityModifierMeters:Number(vision.value),minimumPriceCents:min,maximumPriceCents:max});});row.append(info,fields,save,configurationInventoryActions(item));(itemTargets[itemSection(item)]||itemTargets.misc).append(row);
    }
    const events=configuration.events,timeMode=events.serverTimeMode==='manual'?'manual':'auto',simulated=new Date(serverNowMs(events));ui.serverTimeModeConfig.value=timeMode;ui.serverTimeConfig.value=localDateTimeValue(simulated);ui.serverTimeConfig.disabled=timeMode!=='manual';ui.serverTimeModeConfig.onchange=()=>{const manual=ui.serverTimeModeConfig.value==='manual';ui.serverTimeConfig.disabled=!manual;if(manual&&timeMode==='auto')ui.serverTimeConfig.value=localDateTimeValue(new Date(serverNowMs(events)));updateServerTimePreview();};ui.weatherModeConfig.value=events.weatherMode||'live';ui.weatherTemperatureConfig.value=events.temperatureCelsius==null?'':String(events.temperatureCelsius);for(const [control,key]of [[ui.weatherRefreshConfig,'weatherRefreshMinutes'],[ui.streetLightsOnConfig,'streetLightsOnHour'],[ui.streetLightsOffConfig,'streetLightsOffHour'],[ui.buildingLightsRefreshConfig,'buildingLightsRefreshMinutes'],[ui.merchantRefreshConfig,'merchantRefreshMinutes'],[ui.doorLockRefreshConfig,'doorLockRefreshMinutes'],[ui.ufoIntervalConfig,'ufoIntervalHours'],[ui.ufoDurationConfig,'ufoDurationMinutes'],[ui.trexIntervalConfig,'trexIntervalHours'],[ui.trexDurationConfig,'trexDurationMinutes'],[ui.brontosaurusIntervalConfig,'brontosaurusIntervalHours'],[ui.brontosaurusDurationConfig,'brontosaurusDurationMinutes'],[ui.stegosaurusIntervalConfig,'stegosaurusIntervalHours'],[ui.stegosaurusDurationConfig,'stegosaurusDurationMinutes'],[ui.raptorIntervalConfig,'raptorIntervalHours'],[ui.raptorDurationConfig,'raptorDurationMinutes'],[ui.landOfGiantsIntervalConfig,'landOfGiantsIntervalHours'],[ui.landOfGiantsDurationConfig,'landOfGiantsDurationMinutes'],[ui.bearIntervalConfig,'bearIntervalHours'],[ui.bearDurationConfig,'bearDurationMinutes']])control.value=String(events[key]);for(const [control,key,fallback]of [[ui.ufoEventNameConfig,'ufoEventName','UFO Flyover'],[ui.trexEventNameConfig,'trexEventName','T-Rex Portal'],[ui.brontosaurusEventNameConfig,'brontosaurusEventName','Brontosaurus Portal'],[ui.stegosaurusEventNameConfig,'stegosaurusEventName','Stegosaurus Portal'],[ui.raptorEventNameConfig,'raptorEventName','Raptor Pack'],[ui.landOfGiantsEventNameConfig,'landOfGiantsEventName','Land of the Giants'],[ui.bearEventNameConfig,'bearEventName','The Great Bear']])control.value=events[key]||fallback;
    ui.serverConfigWindow.querySelector('#wantedSwatThresholdConfig').value=String(events.wantedSwatThreshold??5);ui.serverConfigWindow.querySelector('#retroBattlesIntervalConfig').value=String(events.retroBattlesIntervalHours??24);ui.serverConfigWindow.querySelector('#retroBattlesDurationConfig').value=String(events.retroBattlesDurationMinutes??10);
    ui.weatherTemperatureConfig.disabled=ui.weatherModeConfig.value==='live';ui.weatherModeConfig.onchange=()=>{ui.weatherTemperatureConfig.disabled=ui.weatherModeConfig.value==='live';};updateServerTimePreview();
    ui.saveServerEventsConfig.onclick=()=>{const mode=ui.serverTimeModeConfig.value,chosen=Date.parse(`${ui.serverTimeConfig.value}Z`),serverLocalNow=Date.now()+Number(events.serverUtcOffsetMinutes||0)*60000,temperature=ui.weatherTemperatureConfig.value.trim();if(mode==='manual'&&!Number.isFinite(chosen)){showToast('Choose a manual server date and time.');return;}send({type:'updateServerEvents',retroBattlesIntervalHours:Number(ui.serverConfigWindow.querySelector('#retroBattlesIntervalConfig').value),retroBattlesDurationMinutes:Number(ui.serverConfigWindow.querySelector('#retroBattlesDurationConfig').value),weatherRefreshMinutes:Number(ui.weatherRefreshConfig.value),streetLightsOnHour:Number(ui.streetLightsOnConfig.value),streetLightsOffHour:Number(ui.streetLightsOffConfig.value),buildingLightsRefreshMinutes:Number(ui.buildingLightsRefreshConfig.value),merchantRefreshMinutes:Number(ui.merchantRefreshConfig.value),doorLockRefreshMinutes:Number(ui.doorLockRefreshConfig.value),ufoEventName:ui.ufoEventNameConfig.value.trim(),ufoIntervalHours:Number(ui.ufoIntervalConfig.value),ufoDurationMinutes:Number(ui.ufoDurationConfig.value),trexEventName:ui.trexEventNameConfig.value.trim(),trexIntervalHours:Number(ui.trexIntervalConfig.value),trexDurationMinutes:Number(ui.trexDurationConfig.value),brontosaurusEventName:ui.brontosaurusEventNameConfig.value.trim(),brontosaurusIntervalHours:Number(ui.brontosaurusIntervalConfig.value),brontosaurusDurationMinutes:Number(ui.brontosaurusDurationConfig.value),stegosaurusEventName:ui.stegosaurusEventNameConfig.value.trim(),stegosaurusIntervalHours:Number(ui.stegosaurusIntervalConfig.value),stegosaurusDurationMinutes:Number(ui.stegosaurusDurationConfig.value),raptorEventName:ui.raptorEventNameConfig.value.trim(),raptorIntervalHours:Number(ui.raptorIntervalConfig.value),raptorDurationMinutes:Number(ui.raptorDurationConfig.value),landOfGiantsEventName:ui.landOfGiantsEventNameConfig.value.trim(),landOfGiantsIntervalHours:Number(ui.landOfGiantsIntervalConfig.value),landOfGiantsDurationMinutes:Number(ui.landOfGiantsDurationConfig.value),bearEventName:ui.bearEventNameConfig.value.trim(),bearIntervalHours:Number(ui.bearIntervalConfig.value),bearDurationMinutes:Number(ui.bearDurationConfig.value),wantedSwatThreshold:Number(ui.serverConfigWindow.querySelector('#wantedSwatThresholdConfig').value),serverTimeMode:mode,serverTimeOffsetMinutes:mode==='manual'?Math.round((chosen-serverLocalNow)/60000):0,weatherMode:ui.weatherModeConfig.value,temperatureCelsius:temperature===''?null:Number(temperature)});};
  }
  function configurationInventoryActions(item){const actions=document.createElement('div'),take=document.createElement('button'),give=document.createElement('button');actions.className='server-item-actions';take.type=give.type='button';take.textContent='Take 1';give.textContent='Give 1';take.title=`Put one ${item.displayName} in Home inventory`;give.title=`Remove one ${item.displayName} from Home inventory first, then the backpack`;take.addEventListener('click',()=>send({type:'configureInventoryItem',itemType:item.itemType,action:'take'}));give.addEventListener('click',()=>send({type:'configureInventoryItem',itemType:item.itemType,action:'give'}));actions.append(take,give);return actions;}
  function localDateTimeValue(date){const pad=value=>String(value).padStart(2,'0');return`${date.getUTCFullYear()}-${pad(date.getUTCMonth()+1)}-${pad(date.getUTCDate())}T${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`;}
  function updateServerTimePreview(){const events=state.privateState?.serverConfiguration?.events;if(!events||!ui.serverTimePreview)return;const value=new Date(serverNowMs(events)).toLocaleString([],{timeZone:'UTC',dateStyle:'medium',timeStyle:'medium'}),mode=events.serverTimeMode==='manual'?'Manual clock, advancing normally':'Auto, following the server host clock';ui.serverTimePreview.textContent=`Current: ${value} — ${mode}`;}
  async function refreshRealityInfo(){
    const panel=ui.serverConfigWindow,status=panel.querySelector('#realityInfoStatus'),list=panel.querySelector('#realityInfo'),button=panel.querySelector('#refreshRealityInfo');
    if(button.disabled)return;button.disabled=true;status.textContent='Reading server storage…';
    try{
      const response=await fetch('/api/reality-maintenance',{cache:'no-store'});if(!response.ok)throw new Error(response.status===403?'God Mode is required.':'Could not load server information.');
      const info=await response.json(),r=info.reality,s=info.storage;
      const bytes=n=>n<1024?`${n} B`:n<1048576?`${(n/1024).toFixed(1)} KiB`:n<1073741824?`${(n/1048576).toFixed(1)} MiB`:`${(n/1073741824).toFixed(2)} GiB`;
      const rows=[['Reality',r.name],['Server starting location',`${r.area.center.latitude.toFixed(6)}, ${r.area.center.longitude.toFixed(6)}`],['Map block size',`${r.area.sizeMeters.toLocaleString()} × ${r.area.sizeMeters.toLocaleString()} m`],['Saved map blocks',s.mapBlocks.toLocaleString()],['Active map blocks',info.loadedBlocks.toLocaleString()],['Map storage',bytes(s.mapBytes)],['Database, including journal',bytes(s.databaseBytes)],['Legacy source storage',bytes(s.legacySourceBytes)],['Total server data, including logs',bytes(s.totalBytes)],['Loaded world objects',info.baseEntities.toLocaleString()],['Actors / connected players',`${info.actors} / ${info.players}`],['Generation seed',String(r.seed)],['Geographic source',info.source],['Current operation',info.operation||'Idle']];
      list.replaceChildren();for(const [label,value] of rows){const dt=list.ownerDocument.createElement('dt'),dd=list.ownerDocument.createElement('dd');dt.textContent=label;dd.textContent=value;list.append(dt,dd);}
      status.textContent=`Updated ${new Date(info.checkedAtUtc).toLocaleTimeString()}. ${s.complete?'Storage reflects files currently on disk.':'Some files could not be read; storage totals are partial.'}`;
    }catch(error){list.replaceChildren();status.textContent=error.message;}finally{button.disabled=false;}
  }
  function setServerConfigTab(tab){if(tab==='rebuild')refreshRealityInfo();for(const button of ui.serverConfigWindow.querySelectorAll('[data-server-config-tab]')){const active=button.dataset.serverConfigTab===tab;button.classList.toggle('active',active);button.setAttribute('aria-selected',String(active));}for(const page of ui.serverConfigWindow.querySelectorAll('[data-server-config-page]'))page.hidden=page.dataset.serverConfigPage!==tab;}
  function renderModifierInputs(container,values){container.replaceChildren();for(const [key,value]of Object.entries(values||{})){const label=document.createElement('label'),name=document.createElement('span'),input=configNumber(value,.1,-200,200);name.textContent=title(key);input.dataset.key=key;label.append(name,input);container.append(label);}}
  function modifierValues(container){return Object.fromEntries([...container.querySelectorAll('input')].map(input=>[input.dataset.key,Number(input.value)]));}
  function configNumber(value,step,min,max){const input=document.createElement('input');input.type='number';input.value=String(value);input.step=String(step);input.min=String(min);input.max=String(max);return input;}
  function setEquipmentSlot(button,label,item){button.querySelector('span').textContent=label;button.querySelector('em').textContent=item&&item!=='none'?title(item):'Empty';button.disabled=false;button.classList.toggle('equipped',!!item&&item!=='none');button.title=item&&item!=='none'?'Click to unequip, or drop another item here.':'Drop a compatible backpack item here.';}
  function renderEquipment(me){if(!me)return;setEquipmentSlot(ui.equipmentHat,'Hat',equippedItem(me,'hat'));setEquipmentSlot(ui.equipmentGloves,'Gloves',equippedItem(me,'gloves'));if(me.equippedGloves&&me.equippedGloves!=='none')ui.equipmentGloves.title=(state.privateState?.serverConfiguration?.items?.find(i=>i.itemType===me.equippedGloves)?.effect||'')+' Click to unequip.';setEquipmentSlot(ui.equipmentShoes,'Shoes',equippedItem(me,'shoes'));setEquipmentSlot(ui.equipmentPants,'Pants',equippedItem(me,'pants'));setEquipmentSlot(ui.equipmentShirt,'Shirt',equippedItem(me,'shirt'));setEquipmentSlot(ui.equipmentOffhand,'Offhand',equippedItem(me,'offhand'));const weapon=me.equippedWeapon||'fist',equipped=weapon!=='none',locked=me.travelMode==='ufo',probulatorOn=locked&&state.probulatorBeams.has(me.id),mode=weapon==='ar15'?` · ${title(me.ar15FireMode||'single')}`:locked?` · ${probulatorOn?'ON':'OFF'}`:'';ui.equipmentWeapon.querySelector('em').textContent=equipped?`${title(weapon)}${mode}`:'Empty';ui.equipmentWeapon.classList.toggle('equipped',equipped);ui.equipmentWeapon.title=locked?'The Probulator is always equipped. Click your UFO in the world to switch it on or off.':weapon==='ar15'?'Click to toggle single/burst mode. Drag the open hand here to unequip.':equipped?'Click to unequip. Drop a backpack weapon here.':'Drop a backpack weapon here.';}
  function showInteractionWindow(panel,width){const opening=panel.hidden||!panel.classList.contains('floating-panel');panel.hidden=false;if(panel.classList.contains('panel-collapsed'))panel.querySelector('.panel-collapse-button')?.click();if(opening)floatPanel(panel,{width,left:Math.max(8,(viewportWidth()-width)/2),top:60},false);}
  let heldNpcConversations=new Set();
  function refreshNpcConversations(){
    const active=new Set();
    if(!ui.tradeWindow.hidden&&state.tradeQuote?.merchantId)active.add(state.tradeQuote.merchantId);
    if(!ui.questWindow.hidden&&state.questInteraction?.interactionActorId)active.add(state.questInteraction.interactionActorId);
    for(const actorId of heldNpcConversations)if(!active.has(actorId))send({type:'conversationStatus',actorId,active:false});
    for(const actorId of active)send({type:'conversationStatus',actorId,active:true});
    heldNpcConversations=active;
  }
  function closeTradeWindow(){ui.tradeWindow.hidden=true;refreshNpcConversations();}
  function closeQuestWindow(){ui.questWindow.hidden=true;state.questInteraction=null;refreshNpcConversations();}
  function openTrade(quote){state.tradeQuote=quote;ui.tradeTitle.textContent=quote.merchantName;ui.tradeFriend.textContent=`Friend / foe: ${quote.friendRating.toFixed(1)} · better friendships lower prices`;ui.tradeOffers.replaceChildren();const section=(heading,offers,kind)=>{const h=document.createElement('h3');h.textContent=heading;ui.tradeOffers.append(h);if(!offers?.length){const empty=document.createElement('small');empty.textContent=kind==='sell'?'Nothing in your backpack can be sold here.':'No stock available.';ui.tradeOffers.append(empty);return;}for(const offer of offers){const row=document.createElement('label');row.className='trade-offer';const properties=offer.properties||{};const artButton=document.createElement('button');artButton.type='button';artButton.className='trade-item-icon';artButton.setAttribute('aria-label',`${kind==='sell'?'Sell':'Buy'} one ${offer.displayName||title(offer.itemType)}`);artButton.append(createItemArt(offer.itemType,properties,offer.imageKey));row.append(artButton);const name=document.createElement('span');name.className='trade-offer-name';const strong=document.createElement('strong');strong.textContent=[properties.quality,offer.displayName||title(offer.itemType)].filter(Boolean).join(' ');const detail=document.createElement('small');detail.textContent=[[properties.color,properties.pattern].filter(Boolean).join(' · '),properties.description].filter(Boolean).join(' — ');name.append(strong,detail);const price=document.createElement('strong');price.textContent=`$${(offer.unitPriceCents/100).toFixed(2)} · ${offer.quantity} ${kind==='sell'?'owned':'available'}`;const input=document.createElement('input');input.type='number';input.min='0';input.max=String(offer.quantity);input.value='0';input.dataset.item=offer.itemType;input.dataset.tradeKind=kind;input.step='1';input.setAttribute('aria-label',`${kind==='sell'?'Sell':'Buy'} quantity: ${offer.displayName||title(offer.itemType)}`);artButton.disabled=offer.quantity<1;artButton.addEventListener('click',event=>{event.preventDefault();input.value=String(Math.min(offer.quantity,Math.max(0,Math.floor(Number(input.value)||0))+1));input.dispatchEvent(new Event('input',{bubbles:true}));});row.append(name,price,input);ui.tradeOffers.append(row);}};section('Buy from merchant',quote.offers,'buy');section('Sell to merchant',quote.buyOffers||[],'sell');showInteractionWindow(ui.tradeWindow,820);refreshNpcConversations();}
  const characterStatDescriptions={
    nutUp:'N — Nut up ability: resists running in fear from stronger attacking opponents.',
    opportunistic:'O — Opportunistic: increases the chance of an extra strike and damage without extra ammo.',
    timing:'T — Timing: increases shooting speed.',
    strength:'Increases weapon damage and carrying capacity.',
    perception:'Improves weapon accuracy, lockpicking, and vision range.',
    endurance:'Increases maximum stamina.',
    charisma:'Improves NPC friendliness at your first meeting.',
    intelligence:'Increases all XP earned and crafting success.',
    agility:'Reduces stamina drain and the distance at which NPCs see you.',
    luck:'Improves lockpicking and crafting; lowers the chance of a crime being witnessed.'
  };
  function alignmentLabel(value){return Math.abs(value)<.005?'Neutral':`${value>0?'Good':'Evil'} ${Math.abs(value).toFixed(2)}`;}
  function renderProgression(){
    const p=state.privateState?.progression;if(!p)return;
    $('#playerLevelValue').textContent=String(p.level);
    $('#playerExperience').max=p.requiredForNextLevel;$('#playerExperience').value=p.earnedTowardNextLevel;
    $('#playerExperienceText').textContent=`${p.earnedTowardNextLevel.toFixed(2)} / ${p.requiredForNextLevel.toLocaleString()} XP`;
    $('#alignmentValue').textContent=(p.alignment>0?'+':'')+(p.alignment||0).toFixed(2);$('#alignmentValue').title=alignmentLabel(p.alignment||0);
    const statsButton=$('#openProgression'),points=p.availablePoints>0;
    statsButton.classList.toggle('points-available',points);
    statsButton.title=points?`Character stats · ${p.availablePoints} point(s) available`:'Character stats';
    statsButton.setAttribute('aria-label',statsButton.title);
    $('#dungeonComplete').hidden=!state.dungeon?.isCompleted;
    $('#progressionSummary').textContent=`Level ${p.level} · ${p.experience.toFixed(2)} total XP · Carry ${p.carryingCapacity} lb · Stamina ${p.maximumStamina} · XP ×${p.experienceMultiplier.toFixed(2)}`;
    $('#alignmentSummary').textContent=`${alignmentLabel(p.alignment||0)} · First-meeting effect ${(p.alignmentFirstEncounterBonus||0)>=0?'+':''}${(p.alignmentFirstEncounterBonus||0).toFixed(3)}. Quests add a little Good; crimes add Evil, more when witnessed.`;
    if($('#progressionWindow').hidden)return;
    if(!state.statDraft)state.statDraft=Object.fromEntries(Object.keys(characterStatDescriptions).map(key=>[key,p.stats[key]??1]));
    const remaining=10+p.level-1-Object.values(state.statDraft).reduce((sum,n)=>sum+n,0);
    $('#statPointsRemaining').textContent=`${remaining} point(s) available`;
    $('#saveStats').disabled=remaining<0;
    const list=$('#statAllocation');list.replaceChildren();
    for(const [key,description] of Object.entries(characterStatDescriptions)){
      const row=document.createElement('div'),label=document.createElement('div'),heading=document.createElement('strong'),hint=document.createElement('small'),minus=document.createElement('button'),value=document.createElement('output'),plus=document.createElement('button');
      row.className='stat-allocation-row';heading.textContent=key==='nutUp'?'Nut up ability':title(key);hint.textContent=description;label.append(heading,hint);
      value.textContent=String(state.statDraft[key]);value.setAttribute('aria-label',`${title(key)} points`);
      minus.type=plus.type='button';minus.textContent='−';plus.textContent='+';
      minus.setAttribute('aria-label',`Decrease ${title(key)}`);plus.setAttribute('aria-label',`Increase ${title(key)}`);
      minus.disabled=state.statDraft[key]<=0;plus.disabled=remaining<=0;
      minus.addEventListener('click',()=>{state.statDraft[key]--;renderProgression();});
      plus.addEventListener('click',()=>{state.statDraft[key]++;renderProgression();});
      row.append(label,minus,value,plus);list.append(row);
    }
  }
  $('#openProgression').addEventListener('click',()=>{const panel=$('#progressionWindow');state.statDraft=null;panel.hidden=false;if(panel.classList.contains('panel-collapsed'))panel.querySelector('.panel-collapse-button')?.click();renderProgression();floatPanel(panel,{width:560,left:Math.max(8,(innerWidth-560)/2),top:60},false);});
  $('#closeProgression').addEventListener('click',()=>{$('#progressionWindow').hidden=true;state.statDraft=null;});
  $('#saveStats').addEventListener('click',()=>{if(state.statDraft)send({type:'assignStats',stats:state.statDraft});});
  function craftingSuppliesKey(){return JSON.stringify([state.privateState?.inventory?.items,state.privateState?.homeItemStorage?.items,state.privateState?.recipeBook]);}
  function refreshOpenCrafting(){if(!state.crafting||state.crafting.tableDestroyed||ui.craftingWindow.hidden||!state.dungeon?.isHome)return;const key=craftingSuppliesKey();if(key===state.craftingSuppliesKey)return;state.craftingSuppliesKey=key;send({type:'requestCrafting',furnitureId:state.crafting.furnitureId});}
  function openCrafting(crafting){
    $('#craftingTitle').textContent=crafting.stationType==='stove'?'Stove':crafting.stationType==='garageWorkbench'?'Garage workbench':crafting.stationType==='weaponsBench'?'Weapons bench':'Crafting table';
    state.craftingSuppliesKey=craftingSuppliesKey();state.crafting=crafting;ui.craftingRefresh.disabled=!!crafting.tableDestroyed;ui.craftingRecipes.replaceChildren();
    const skill=crafting.skill||state.privateState?.craftingSkill||{level:1,progressPercent:0};
    $('#craftingSkillSummary').textContent=`Crafting level ${skill.level.toLocaleString()} · ${skill.progressPercent}% toward next level. Available recipes use your backpack + Home supplies. Select an item for details.`;
    const recipes=(crafting?.recipes||[]).filter(recipe=>recipe.learned!==false && recipe.maximumCraftable>0 && skill.level>=recipe.requiredLevel);
    const overview=document.createElement('div');ui.craftingRecipes.append(overview);
    if(!recipes.length){const empty=document.createElement('p');empty.textContent=crafting.tableDestroyed?'The station exploded. Place a new station to try again. Unattempted materials remain in your backpack and Home storage.':'No recipes available to craft at this station with your learned recipes, crafting level, and current backpack + Home supplies.';overview.append(empty);}
    for(const recipe of recipes){
      const card=document.createElement('article'),heading=document.createElement('h3'),output=document.createElement('div'),list=document.createElement('ul'),quantity=document.createElement('input'),button=document.createElement('button'),controls=document.createElement('div');
      card.className='crafting-recipe';heading.textContent=`${recipe.name} · level ${recipe.requiredLevel.toLocaleString()}`;heading.title=recipe.difficulty;output.append(createItemArt(recipe.outputItemType));output.append(document.createTextNode(`Makes ${recipe.outputQuantity} per batch · Can craft ${recipe.maximumCraftable} batches with backpack + Home supplies`));
      const entry=document.createElement('button'),entryName=document.createElement('strong'),entryInfo=document.createElement('span');
      entry.type='button';entry.className='crafting-recipe';entry.style.cssText='display:grid;width:100%;gap:6px;text-align:left;margin-bottom:8px';entryName.textContent=recipe.name;
      entryInfo.textContent=`${(recipe.successChance*100).toFixed(2)}% success per batch | Up to ${(recipe.maximumCraftable*recipe.outputQuantity).toLocaleString()} items (${recipe.maximumCraftable} batches)`;
      entry.append(createItemArt(recipe.outputItemType),entryName,entryInfo);overview.append(entry);card.hidden=true;heading.tabIndex=-1;
      const back=document.createElement('button');back.type='button';back.textContent='Back to available recipes';
      entry.addEventListener('click',()=>{overview.hidden=true;card.hidden=false;heading.focus({preventScroll:true});card.scrollIntoView({block:'nearest'});});
      back.addEventListener('click',()=>{card.hidden=true;overview.hidden=false;entry.focus({preventScroll:true});});card.append(back);
      for(const ingredient of recipe.ingredients){const line=document.createElement('li');line.textContent=`${ingredient.quality?ingredient.quality+' ':''}${ingredient.name} — need ${ingredient.required}, have ${ingredient.available}`;line.className=ingredient.available<ingredient.required?'crafting-missing':'crafting-ready';list.append(line);}
      quantity.type='number';quantity.min='1';quantity.max=String(Math.max(1,recipe.maximumCraftable));quantity.value='1';quantity.setAttribute('aria-label',`${recipe.name} batches`);quantity.disabled=recipe.maximumCraftable<1;
      const blockers=[];if(recipe.learned===false)blockers.push('Learn this recipe from dungeon loot first');if(skill.level<recipe.requiredLevel)blockers.push(`Requires crafting level ${recipe.requiredLevel.toLocaleString()}`);if(recipe.ingredients.some(ingredient=>ingredient.available<ingredient.required))blockers.push('Missing supplies (see ingredients below)');
      button.type='button';button.textContent=recipe.maximumCraftable>0?'Craft':recipe.learned===false?'Recipe not learned':skill.level<recipe.requiredLevel?`Requires level ${recipe.requiredLevel.toLocaleString()}`:'Missing supplies';button.disabled=recipe.maximumCraftable<1;button.title=blockers.join('. ');
      if(blockers.length){const status=document.createElement('p');status.className='crafting-missing';status.textContent=blockers.join('. ')+'.';card.append(status);}
      button.addEventListener('click',()=>{const amount=Number(quantity.value);if(!Number.isInteger(amount)||amount<1||amount>recipe.maximumCraftable){showToast(`Choose 1–${recipe.maximumCraftable} batches.`);return;}send({type:'craftItem',furnitureId:crafting.furnitureId,recipeId:recipe.id,quantity:amount});});
      const description=document.createElement('p');description.textContent=state.privateState?.serverConfiguration?.items?.find(item=>item.itemType===recipe.outputItemType)?.effect||`Craft ${recipe.name} into Home storage.`;
      const detail=document.createElement('p');detail.className='crafting-effect';detail.textContent=`${(recipe.successChance*100).toFixed(2)}% success for the next batch · Studied ${recipe.studyCount} time(s) · Base ${(recipe.baseSuccessChance*100).toFixed(2)}% · Next copy +${(recipe.nextStudyBonus*100).toFixed(3)} percentage points. ${recipe.difficulty}.`;detail.textContent+=` ${HomeWorkshop.bonusSummary(recipe.bonuses)}`;
      const materials=document.createElement('p');materials.textContent='Materials per batch (Home supplies used first, then backpack):';
      const warning=document.createElement('p');warning.textContent='Quantities are potential output, not guaranteed successes. Each attempted batch earns 1 crafting XP. A failed batch destroys the station, consumes its materials, and deals 1 damage. Unattempted materials are kept.';
      controls.className='crafting-controls';controls.append(quantity,button);card.append(heading,description,output,detail,materials,list,warning,controls);ui.craftingRecipes.append(card);
    }
    ui.craftingWindow.hidden=false;setRightRailCollapsed(false);if(ui.craftingWindow.classList.contains('panel-collapsed'))ui.craftingWindow.querySelector('.panel-collapse-button')?.click();ui.craftingWindow.scrollIntoView({block:'nearest'});
  }
  function renderHomeUpgrades(){
    if($('#homeUpgradesWindow').hidden)return;
    const owner=state.players.get(state.playerId),key=JSON.stringify([state.privateState?.homeWorkshop,owner?.walletCents,owner?.godMode,state.privateState?.canEditHome]);if(state.homeUpgradeRenderKey===key)return;state.homeUpgradeRenderKey=key;
    HomeWorkshop.render($('#homeUpgradeRows'),state.privateState?.homeWorkshop,state.players.get(state.playerId),state.privateState?.canEditHome,
      upgradeId=>send({type:'buyHomeUpgrade',upgradeId}),replaceFilter=>send({type:'useWaterPurifier',replaceFilter}));
  }
  function openHomeUpgrades(){const panel=$('#homeUpgradesWindow');panel.hidden=false;if(panel.classList.contains('panel-collapsed'))panel.querySelector('.panel-collapse-button')?.click();renderHomeUpgrades();floatPanel(panel,{width:740,left:Math.max(8,(innerWidth-740)/2),top:40},false);}
  $('#openHomeUpgradesButton').addEventListener('click',openHomeUpgrades);
  $('#craftingUpgrades').addEventListener('click',openHomeUpgrades);
  $('#closeHomeUpgrades').addEventListener('click',()=>{$('#homeUpgradesWindow').hidden=true;});
  function renderRecipeBook(){
    if($('#recipesWindow').hidden)return;
    const skill=state.privateState?.craftingSkill?.level||1,tab=state.recipeBookTab||'Food/Water';
    const key=JSON.stringify([skill,tab,state.privateState?.recipeBook]);if(state.recipeBookRenderKey===key)return;state.recipeBookRenderKey=key;
    $('#recipeBookSkill').textContent=`Crafting level ${skill}. Rates include your stats, Home upgrades, and ingredient qualities, using Home supplies first, then your backpack. Quantities include both inventories. Study copies from your backpack to raise recipe levels.`;
    document.querySelectorAll('[data-recipe-tab]').forEach(button=>{const selected=button.dataset.recipeTab===tab;button.classList.toggle('active',selected);button.setAttribute('aria-selected',String(selected));});
    RecipeBook.render($('#recipeBookRows'),state.privateState?.recipeBook,tab,skill,createItemArt,id=>send({type:'consumeItem',itemType:'recipe:'+id}));
  }
  $('#openRecipesButton').addEventListener('click',()=>{const panel=$('#recipesWindow');panel.hidden=false;$('#openRecipesButton').setAttribute('aria-expanded','true');if(panel.classList.contains('panel-collapsed'))panel.querySelector('.panel-collapse-button')?.click();renderRecipeBook();floatPanel(panel,{width:680,left:Math.max(8,(innerWidth-680)/2),top:60},false);});
  $('#closeRecipesButton').addEventListener('click',()=>{$('#recipesWindow').hidden=true;$('#openRecipesButton').setAttribute('aria-expanded','false');});
  document.querySelectorAll('[data-recipe-tab]').forEach(button=>button.addEventListener('click',()=>{state.recipeBookTab=button.dataset.recipeTab;renderRecipeBook();}));
  $('#collectDirtyWaterButton').addEventListener('click',()=>send({type:'collectDirtyWater'}));
  ui.craftingClose.addEventListener('click',()=>{ui.craftingWindow.hidden=true;state.crafting=null;});
  ui.craftingRefresh.addEventListener('click',()=>{if(state.crafting&&!state.crafting.tableDestroyed)send({type:'requestCrafting',furnitureId:state.crafting.furnitureId});});
  function openHomeShop(shop){state.homeShop=shop;ui.homeShopTitle.textContent=shop.isOwner?'Manage your Home shop':`${shop.ownerName}'s Home shop`;ui.homeShopHint.textContent=shop.isOwner?'Listed stock is held safely by the shop. Set quantity to zero to return it to your backpack.':'This shop stays open even while its owner is offline.';ui.homeShopListings.replaceChildren();ui.homeShopInventory.replaceChildren();const listings=shop.listings||[];if(!listings.length)ui.homeShopListings.innerHTML='<small>This shop has no items for sale yet.</small>';for(const item of listings){const row=document.createElement('div');row.className='home-shop-listing';row.append(createItemArt(item.itemType,{quality:item.quality},item.itemType));const label=document.createElement('span');label.innerHTML=`<strong>${item.quality?`${item.quality} `:''}${item.displayName}</strong><small>${item.unitWeightPounds.toFixed(2)} lb each · ${item.quantity} available</small>`;const price=document.createElement('strong');price.textContent=`$${(item.unitPriceCents/100).toFixed(2)}`;const quantity=document.createElement('input');quantity.type='number';quantity.min='0';quantity.max=String(item.quantity);quantity.value=shop.isOwner?String(item.quantity):'0';const action=document.createElement('button');action.type='button';action.textContent=shop.isOwner?'Update':'Buy';action.addEventListener('click',()=>{const amount=Math.max(0,Number(quantity.value)||0);if(shop.isOwner){const nextPrice=Math.max(1,Math.round(Number(priceInput.value)*100)||item.unitPriceCents);send({type:'setHomeShopListing',furnitureId:shop.furnitureId,itemType:item.itemType,quantity:amount,unitPriceCents:nextPrice});}else if(amount>0)send({type:'purchaseHomeShop',furnitureId:shop.furnitureId,itemType:item.itemType,quantity:amount});});let priceInput=null;if(shop.isOwner){priceInput=document.createElement('input');priceInput.type='number';priceInput.min='.01';priceInput.step='.01';priceInput.value=(item.unitPriceCents/100).toFixed(2);row.append(label,quantity,priceInput,action);}else row.append(label,price,quantity,action);ui.homeShopListings.append(row);}if(shop.isOwner){const heading=document.createElement('h3');heading.textContent='Add backpack item';ui.homeShopInventory.append(heading);const listed=new Set(listings.map(item=>item.itemType));const inventory=(shop.ownerInventory?.items||[]).filter(item=>!listed.has(item.itemType)&&!['fist','personalFlag'].includes(item.itemType)&&item.category!=='quest');if(!inventory.length)ui.homeShopInventory.append(Object.assign(document.createElement('small'),{textContent:'No additional sellable items in your backpack.'}));for(const item of inventory){const row=document.createElement('div');row.className='home-shop-listing';row.append(createItemArt(item.itemType,{quality:item.quality},item.itemType));const label=document.createElement('span');label.innerHTML=`<strong>${item.quality?`${item.quality} `:''}${title(item.itemType)}</strong><small>${item.quantity} owned · ${item.unitWeightPounds.toFixed(2)} lb each</small>`;const quantity=document.createElement('input');quantity.type='number';quantity.min='1';quantity.max=String(item.quantity);quantity.value='1';const price=document.createElement('input');price.type='number';price.min='.01';price.step='.01';price.value='1.00';const action=document.createElement('button');action.type='button';action.textContent='List';action.addEventListener('click',()=>send({type:'setHomeShopListing',furnitureId:shop.furnitureId,itemType:item.itemType,quantity:Number(quantity.value)||1,unitPriceCents:Math.max(1,Math.round((Number(price.value)||1)*100))}));row.append(label,quantity,price,action);ui.homeShopInventory.append(row);}}ui.homeShopWindow.hidden=false;}
  function closeTreasure(){const leave=!!state.dungeon?.retroBattle&&state.dungeon.isCompleted&&!!state.chestContents;state.chestContents=null;ui.treasureWindow.hidden=true;if(leave)send({type:'exitDungeon'});}
  function advanceAutoTreasure(time,me){
    if(!['all','upgrades'].includes(state.lootingMode)||state.autoTreasurePending||state.automaticTreasure||!ui.treasureWindow.hidden||state.pendingLoot||state.pendingChest||time<(state.nextAutoTreasureAt||0))return;
    state.nextAutoTreasureAt=time+750;
    const nearby=[];
    for(const [chest,entries] of [[false,state.loot],[true,state.chests]])for(const item of entries.values()){
      if(item.expiresAtUtc&&Date.parse(item.expiresAtUtc)<=Date.now())continue;
      if(chest&&state.dungeon?.underwater&&state.dungeon.actors?.some(actor=>actor.factionId===item.id))continue;
      if(!chest&&(item.locationId!==me.locationId||item.dropKind==='eventReward'&&item.ownerId!==me.id))continue;
      if(Math.hypot(item.position.x-me.position.x,item.position.y-me.position.y)<=3.7)nearby.push({chest,item});
    }
    if(!nearby.length){state.autoTreasureSignature=null;return;}
    nearby.sort((a,b)=>a.item.id.localeCompare(b.item.id));
    const signature=JSON.stringify([me.locationId,me.godMode,state.privateState?.inventory?.items,effectiveCarryingCapacity(me,state.privateState?.inventory?.maximumWeightPounds),nearby.map(({chest,item})=>[chest,item.id,item.items,item.moneyCents])]);
    if(signature===state.autoTreasureSignature)return;
    state.autoTreasureSignature=signature;state.autoTreasurePending=true;
    if(send({type:'autoTakeNearbyTreasure',sourceId:nearby[0].item.id,chest:nearby[0].chest,upgradesOnly:state.lootingMode==='upgrades'})===false){state.autoTreasurePending=false;state.autoTreasureSignature=null;}
  }
  function receiveAutoTreasure(message){
    state.autoTreasurePending=false;applyPrivate(message.privateState);
    if(message.contents&&!message.contents.items?.length&&state.dungeon?.retroBattle&&state.dungeon.isCompleted&&ui.treasureWindow.hidden)
      openTreasure({...message.contents,nearby:true},'Everything collected. Close this treasure to leave the dungeon.');
  }
  function receiveNearbyTreasure(message){
    state.automaticTreasure=null;applyPrivate(message.privateState);
    if(message.type==='nearbyTreasureUpdated'&&!message.contents.items?.length&&!(state.dungeon?.retroBattle&&state.dungeon.isCompleted)){closeTreasure();showToast(message.contents.message||'Treasure collected.');return;}
    openTreasure({...message.contents,nearby:true},message.contents.message,message.type==='nearbyTreasureUpdated',message.type==='nearbyTreasureOpened');
  }
  function openLootTreasure(loot,message,preserve=false,allowAutomatic=false){
    if(!loot)return;
    openTreasure({lootId:loot.id,items:loot.items,moneyCents:loot.moneyCents,dropKind:loot.dropKind},message||`Cash: $${((loot.moneyCents||0)/100).toFixed(2)} · Collected when you take items or cash.`,preserve,allowAutomatic);
  }
  function openTreasure(contents,message,preserve=false,allowAutomatic=false){
    if(!contents)return;
    if(allowAutomatic&&tryAutomaticTreasure(contents))return;
    const wasHidden=ui.treasureWindow.hidden,selection=preserve?new Map(treasureSelection().map(item=>[item.itemType,item.quantity])):new Map();
    state.chestContents=contents;
    const hasItems=!!contents.items?.length,hasCash=!!contents.lootId&&contents.moneyCents>0;
    const completed=!hasItems&&!hasCash,leaveDungeon=!!state.dungeon?.retroBattle&&state.dungeon.isCompleted;
    ui.treasureHint.hidden=completed;ui.treasureColumns.hidden=completed;ui.treasureWeight.hidden=completed;
    ui.treasureTakeAll.hidden=completed;ui.treasureTake.hidden=completed;
    ui.treasureClose.textContent=leaveDungeon?'Close & leave dungeon':'Close';
    ui.treasureWindow.querySelector('h2').textContent=contents.isEventReward||contents.dropKind==='eventReward'?'Event treasure':contents.nearby?'Nearby treasure':contents.lootId?(contents.dropKind==='tombstone'?'Tombstone':'Treasure'):'Treasure chest';
    ui.treasureCash.textContent=message||'Cash was collected automatically.';
    ui.treasureItems.replaceChildren();
    if(!contents.items?.length){const empty=document.createElement('p');empty.textContent='No items remain to select. Check your inventory for collected treasure.';ui.treasureItems.append(empty);}
    ui.treasureTakeAll.disabled=!hasItems;
    for(const item of contents.items||[]){
      const row=document.createElement('label'),details=document.createElement('span'),name=document.createElement('strong'),weight=document.createElement('small'),input=document.createElement('input');
      row.className='treasure-item';row.append(createItemArt(item.itemType));
      name.textContent=`${item.quality?`${item.quality} `:''}${itemDisplayName(item.itemType)} ×${item.quantity}`;
      weight.textContent=`${weightText(Number(item.unitWeightPounds)||0)} each · ${weightText(stackWeight(item))} available`;
      details.append(name,weight);input.type='number';input.min='0';input.max=String(item.quantity);input.step='1';
      input.value=String(Math.min(item.quantity,selection.get(item.itemType)||0));input.dataset.item=item.itemType;
      input.dataset.weight=String(item.carriedInBackpack===false?0:item.unitWeightPounds||0);
      input.setAttribute('aria-label',`Take quantity: ${itemDisplayName(item.itemType)}`);
      input.addEventListener('input',updateTreasureWeight);row.append(details,input);ui.treasureItems.append(row);
    }
    ui.treasureWindow.hidden=false;if(ui.treasureWindow.classList.contains('panel-collapsed'))ui.treasureWindow.querySelector('.panel-collapse-button').click();updateTreasureWeight();
    if(wasHidden){const width=Math.min(640,innerWidth-16);floatPanel(ui.treasureWindow,{width,left:Math.max(8,(viewportWidth()-width)/2),top:Math.max(8,(innerHeight-Math.min(ui.treasureWindow.scrollHeight,innerHeight-32))/2)},false);ui.treasureClose.focus({preventScroll:true});}
  }
  function refreshTreasure(){
    const contents=state.chestContents;if(!contents||ui.treasureWindow.hidden)return;
    if(contents.nearby){updateTreasureWeight();return;}
    if(contents.lootId){
      const loot=state.loot.get(contents.lootId);
      if(!loot){if(state.dungeon?.retroBattle&&state.dungeon.isCompleted&&contents.dropKind==='eventReward')return;closeTreasure();return;}
      if(JSON.stringify(contents.items)!==JSON.stringify(loot.items)||contents.moneyCents!==loot.moneyCents){openLootTreasure(loot,null,true);return;}
    }else if(!state.chests.has(contents.chestId)){if(state.dungeon?.retroBattle&&state.dungeon.isCompleted)return;closeTreasure();return;}
    updateTreasureWeight();
  }
  function treasureSelection(){return[...ui.treasureItems.querySelectorAll('input[data-item]')].map(input=>({itemType:input.dataset.item,quantity:Math.max(0,Math.min(Number(input.max),Math.floor(Number(input.value)||0))),unitWeight:Number(input.dataset.weight)||0})).filter(item=>item.quantity>0);}
  function treasureCapacity(selection=treasureSelection(),contents=state.chestContents){
    const inventory=state.privateState?.inventory,current=Number(inventory?.weightPounds)||0,maximum=effectiveCarryingCapacity(state.players.get(state.playerId),inventory?.maximumWeightPounds),selected=selection.reduce((sum,item)=>sum+item.quantity*item.unitWeight,0),total=current+selected;
    let warning=selection.length&&total>maximum+.0001?`Too heavy by ${weightText(total-maximum)}. Take fewer items or manage your backpack in Inventory.`:'';
    return{current,maximum,selected,total,warning};
  }
  function updateTreasureWeight(){
    const {current,maximum,selected,total,warning}=treasureCapacity();
    ui.treasureWeight.textContent=`Selected ${weightText(selected)} · backpack ${weightText(current)} → ${weightText(total)} / ${Number.isFinite(maximum)?maximum.toFixed(0)+' lb':'Unlimited'}`;
    ui.treasureWarning.hidden=!warning;ui.treasureWarning.textContent=warning;
    ui.treasureTake.disabled=!!warning||(!treasureSelection().length&&!(state.chestContents?.lootId&&state.chestContents.moneyCents>0));
    ui.treasureTake.textContent=state.chestContents?.lootId&&state.chestContents.moneyCents>0?'Take selected & cash':'Take selected';
  }
  function setLootingMode(mode){
    state.lootingMode=['all','upgrades'].includes(mode)?mode:'selective';
    for(const button of document.querySelectorAll('[data-looting]')){const selected=button.dataset.looting===state.lootingMode;button.classList.toggle('active',selected);button.setAttribute('aria-pressed',String(selected));}
    state.autoTreasureSignature=null;
    try{localStorage.setItem('alternative-reality-looting',state.lootingMode);}catch{}
  }
  function initializeLooting(){
    let mode='selective';try{mode=localStorage.getItem('alternative-reality-looting')||mode;}catch{}
    setLootingMode(mode);
    for(const button of document.querySelectorAll('[data-looting]'))button.addEventListener('click',()=>setLootingMode(button.dataset.looting));
  }
  function sendTreasureTake(contents,items){
    if(contents.nearby)return send({type:'takeNearbyTreasure',anchorId:contents.anchorId,sources:contents.sources.map(({id,chest})=>({id,chest})),items});
    return send(contents.lootId?{type:'takeLootItems',lootId:contents.lootId,items}:{type:'takeChestItems',chestId:contents.chestId,items});
  }
  function tryAutomaticTreasure(contents){
    if(state.lootingMode==='upgrades'&&contents.nearby&&contents.items?.length&&!state.autoTreasurePending){
      const source=contents.sources.find(source=>source.id===contents.anchorId)||contents.sources[0];
      if(!source||send({type:'autoTakeNearbyTreasure',sourceId:source.id,chest:source.chest,upgradesOnly:true})===false)return false;
      state.autoTreasurePending=true;state.chestContents=null;ui.treasureWindow.hidden=true;return true;
    }
    if(state.lootingMode!=='all'||state.automaticTreasure||!contents.items?.length)return false;
    const selection=(contents.items||[]).filter(item=>item.quantity>0).map(item=>({itemType:item.itemType,quantity:item.quantity,unitWeight:item.carriedInBackpack===false?0:Number(item.unitWeightPounds)||0}));
    if(treasureCapacity(selection,contents).warning)return false;
    const result=sendTreasureTake(contents,selection.map(({itemType,quantity})=>({itemType,quantity})));
    if(result===false)return false;
    state.automaticTreasure={contents,commandSequence:state.commandSequence};state.chestContents=null;ui.treasureWindow.hidden=true;return true;
  }
  function recoverAutomaticTreasure(message){
    const pending=state.automaticTreasure;
    if(!pending||(message.commandSequence!=null&&message.commandSequence!==pending.commandSequence))return;
    state.automaticTreasure=null;
    openTreasure(pending.contents,message.message);
  }
  function takeAllTreasure(){
    if(!state.chestContents)return;
    for(const input of ui.treasureItems.querySelectorAll('input[data-item]'))input.value=input.max;
    takeTreasureSelection();
  }
  function takeTreasureSelection(){
    if(!state.chestContents)return;updateTreasureWeight();if(ui.treasureTake.disabled)return;
    const items=treasureSelection().map(({itemType,quantity})=>({itemType,quantity}));
    sendTreasureTake(state.chestContents,items);
  }
  function deliveryTimeLabel(quest){if(!quest.deadlineUtc)return'';const seconds=Math.max(0,Math.ceil((Date.parse(quest.deadlineUtc)-Date.now())/1000));return seconds?`Time left: ${Math.floor(seconds/60)}:${String(seconds%60).padStart(2,'0')}`:'Quest expired';}
  function updateDeliveryTimers(){updateQuestNavigation();for(const element of ui.questList.querySelectorAll('[data-delivery-id]')){const quest=(state.privateState?.quests||[]).find(q=>q.id===element.dataset.deliveryId);if(quest)element.textContent=deliveryTimeLabel(quest);}}
  function questStage(quest){
    const id=quest.status==='ready'?quest.giverId:quest.objectiveActorIds?.find(id=>state.actors.has(id))||quest.destinationActorId||quest.targetActorId;
    const candidate=id&&(state.actors.get(id)||state.dungeon?.actors?.find(a=>a.id===id)),actor=candidate&&(!quest.nextStageLocationId||candidate.locationId===quest.nextStageLocationId)?candidate:null,entity=id&&state.baseById.get(id);
    return {position:actor?.position||entity?.position||quest.nextStagePosition||quest.deliveryRecipient?.position,name:quest.nextStageName||quest.targetName||quest.destinationName||quest.giverName,location:actor?.locationId||quest.nextStageLocationId||'outdoor'};
  }
  function questNavigationText(quest){const stage=questStage(quest),me=state.players.get(state.playerId);if(!stage.position||!me)return stage.name||'Search for the objective';if(stage.location!==me.locationId){const origin=state.lastOutdoorPosition||me.position;return `${stage.name} · ${QuestNavigation.bearing(origin,stage.position)} from outside · Exit the building first`;}return `${stage.name} · ${QuestNavigation.bearing(me.position,stage.position)} (straight line)`;}
  function updateQuestNavigation(){for(const el of ui.questList.querySelectorAll('[data-quest-navigation]')){const q=(state.privateState?.quests||[]).find(q=>q.id===el.dataset.questNavigation);if(q)el.textContent=questNavigationText(q);}if(!ui.questWindow.hidden&&state.questInteraction)$('#questNavigation').textContent=questNavigationText(state.questInteraction.quest);const me=state.players.get(state.playerId),button=$('#travelProbulator'),on=state.probulatorBeams.has(state.playerId);button.hidden=me?.travelMode!=='ufo';button.textContent=`Probulator: ${on?'ON':'OFF'}`;button.setAttribute('aria-pressed',String(on));}
  function questRewardText(quest){return `Reward: $${(quest.rewardCents/100).toFixed(2)}${(quest.rewardItems||[]).map(item=>` · ${item.quantity} × ${item.displayName||title(item.itemType)}`).join('')}`;}
  function renderQuests(quests){
    ui.questList.replaceChildren();const active=quests.filter(q=>['active','ready'].includes(q.status));ui.questLog.hidden=!active.length;
    for(const quest of active){const card=document.createElement('div');card.className='quest-card';const name=document.createElement('b');name.textContent=`${quest.status==='ready'?'✓ ':''}${quest.title}`;const detail=document.createElement('span');detail.textContent=quest.description;const reward=document.createElement('span');reward.textContent=questRewardText(quest);const navigation=document.createElement('span');navigation.dataset.questNavigation=quest.id;navigation.textContent=questNavigationText(quest);const timer=document.createElement('b');timer.dataset.deliveryId=quest.id;timer.textContent=deliveryTimeLabel(quest);card.append(detail,navigation,timer,reward);
      if(quest.foodDamaged){const warning=document.createElement('strong');warning.textContent='Food damaged — customer will refuse it';card.append(warning);}
      card.prepend(createQuestMapToggle(quest));ui.questList.append(card);
    }
  }
  function questPortrait(canvas,actor){const c=canvas.getContext('2d');c.clearRect(0,0,100,110);const seed=hash(actor?.id||'player');c.fillStyle='#19332f';c.fillRect(0,0,100,110);c.fillStyle=`hsl(${Math.floor(seed*360)},35%,42%)`;c.beginPath();c.roundRect(22,55,56,55,14);c.fill();c.fillStyle='#c9864f';c.beginPath();c.arc(50,34,22,0,Math.PI*2);c.fill();c.fillStyle='#25231f';c.fillRect(40,30,4,4);c.fillRect(56,30,4,4);c.fillRect(43,44,14,2);}
  function openQuest(interaction){
    stopTravel();state.questInteraction=interaction;const quest=interaction.quest,me=state.players.get(state.playerId),giver=state.actors.get(interaction.interactionActorId)||state.dungeon?.actors?.find(a=>a.id===interaction.interactionActorId);ui.questTitle.textContent=quest.title;ui.questDescription.textContent=quest.description;ui.questReward.textContent=questRewardText(quest)+` · ${quest.deadlineUtc?deliveryTimeLabel(quest):`${quest.deliveryMinutes||60} minutes after accepting`}`;
    const questions=['What do you need help with?','Have you got a job for me?','Tell me about this quest. What needs doing?','What is the task, and what is the reward?','Anything I can help you take care of?'];let pick=Math.floor(Math.random()*questions.length);if(pick===state.lastQuestQuestion)pick=(pick+1)%questions.length;state.lastQuestQuestion=pick;
    $('#questPlayerName').textContent=me?.name||'You';$('#questGiverName').textContent=giver?.name||quest.giverName;$('#questQuestion').textContent=interaction.isOffer?questions[pick]:'How is my quest going?';questPortrait($('#questPlayerPortrait'),me);questPortrait($('#questGiverPortrait'),giver||{id:quest.giverId});$('#questOfferPrompt').textContent=interaction.isOffer?'Will you accept this quest?':'';ui.questClose.textContent=interaction.isOffer?'Decline':'Close';ui.questAccept.hidden=!interaction.isOffer;ui.questComplete.hidden=interaction.isOffer||!interaction.canComplete;ui.questAbandon.hidden=interaction.isOffer||!['active','ready'].includes(quest.status);ui.questWindow.hidden=false;ui.questWindow.classList.remove('panel-collapsed');floatPanel(ui.questWindow,{left:(innerWidth-580)/2,top:Math.max(16,(innerHeight-470)/2),width:580},false);updateQuestNavigation();ui.questAccept.hidden?ui.questClose.focus():ui.questAccept.focus();refreshNpcConversations();
  }
  function questForActor(actorId){return(state.privateState?.quests||[]).find(quest=>['active','ready'].includes(quest.status)&&(quest.giverId===actorId||quest.destinationActorId===actorId||quest.targetActorId===actorId));}
  const heldWeaponAttacks=new Map();
  function receiveCombat(combat){if(CombatEffects.profile(combat.weapon)){heldWeaponAttacks.set(combat.attackerId,{started:performance.now(),end:combat.end});if(heldWeaponAttacks.size>128)heldWeaponAttacks.delete(heldWeaponAttacks.keys().next().value);}PlayerCommands.rememberAttacker(state,combat,performance.now());const attacker=state.players.get(combat.attackerId)||actorsHere().find(actor=>actor.id===combat.attackerId)||state.actors.get(combat.attackerId),attackerName=attacker?.name||(combat.attackerId===state.playerId?'You':'Unknown attacker'),started=performance.now(),effectOnly=combat.weapon.endsWith?.('Explosion')||combat.weapon==='molotovFire'||combat.weapon==='areaHazard'||combat.weapon==='spearImpact',visualShot=combatEffects.enqueue(combat,started,attacker?.locationId||'outdoor');trackExplosiveCombat(combat,visualShot);gameAudio.combat(combat,visualShot,attacker?.locationId||(state.players.get(combat.targetId)||actorsHere().find(a=>a.id===combat.targetId))?.locationId||((state.transit?.buses||[]).some(b=>b.id===combat.attackerId)?'outdoor':null));if(combat.weapon==='probulator'&&!combat.hit&&combat.statusEffect==='Probulator inactive')state.probulatorBeams.delete(combat.attackerId);else if(combat.weapon==='probulator'&&!combat.hit&&combat.statusEffect==='Probulator active'&&combat.statusEffectUntilUtc){const radius=Math.max(.001,Math.hypot(combat.end.x-combat.start.x,combat.end.y-combat.start.y));state.probulatorBeams.set(combat.attackerId,{radius,endsAt:Date.parse(combat.statusEffectUntilUtc)});}else if(dinosaurWeapons.has(combat.weapon)||combat.weapon==='zombieBite')state.actorAttacks.set(combat.attackerId,{weapon:combat.weapon,started,end:combat.end});else if(combat.weapon!=='probulator'&&!effectOnly&&!visualShot)state.projectiles.push({...combat,started});if(combat.weapon==='greenBeam'&&combat.hit&&combat.relocatedTo&&(state.players.has(combat.targetId)||state.actors.has(combat.targetId))){state.abductions.set(combat.targetId,{started,origin:combat.end,ufo:combat.start,destination:combat.relocatedTo});if(combat.targetId===state.playerId){state.path=[];state.target=null;state.followCommand=null;state.moveInFlight=false;}}if(combat.weapon==='probulator'&&combat.statusEffect==='Abducted'&&combat.targetId===state.playerId){stopTravel();state.moveInFlight=false;}if(combat.dialogue){const speaker=state.players.get(combat.targetId)||state.actors.get(combat.targetId);receiveChat({id:`combat:${combat.attackerId}:${combat.targetId}:${Date.now()}`,playerId:combat.targetId,username:speaker?.name||'Abductee',message:combat.dialogue,saidAtUtc:new Date().toISOString()});}if(combat.hit&&combat.damage>0)state.damageIndicators.push({position:combat.end,damage:combat.damage,attackerName,started:visualShot?visualShot.started+visualShot.duration:started,targetDied:combat.targetDied,abducteeId:combat.weapon==='probulator'?combat.targetId:null});if(combat.targetDied)removeCombatTarget(combat.targetId);else if(state.dungeon?.actors?.some(a=>a.id===combat.targetId)&&combat.targetHealth!=null)state.dungeon={...state.dungeon,actors:state.dungeon.actors.map(a=>a.id===combat.targetId?{...a,healthHearts:combat.targetHealth}:a)};if(state.actors.has(combat.targetId)){if(combat.targetDied)state.actors.delete(combat.targetId);else if(combat.targetHealth!=null)state.actors.set(combat.targetId,{...state.actors.get(combat.targetId),healthHearts:combat.targetHealth});}else if(combat.targetHealth!=null&&state.players.has(combat.targetId))state.players.set(combat.targetId,{...state.players.get(combat.targetId),healthHearts:combat.targetHealth});if(combat.targetDied&&state.followCommand?.targetId===combat.targetId)stopTravel();if((combat.attackerId===state.playerId||combat.targetId===state.playerId)&&!(combat.weapon==='probulator'&&combat.statusEffect==='Probulator damage'))showToast(combat.message);if(combat.attackerId===state.playerId){renderEquipment(state.players.get(state.playerId));send({type:'requestPrivateState'});}}
  function probulatorFootprintPixels(radius){const x=Math.max(18,state.scale*1.1)*1.35*(Math.max(.001,radius)/1.485);return{x,y:x*.38/1.35};}
  function drawProbulatorBeams(now){for(const [playerId,beam]of state.probulatorBeams){if(Date.now()>=beam.endsAt){state.probulatorBeams.delete(playerId);continue;}const pilot=state.players.get(playerId);if(!pilot||pilot.travelMode!=='ufo'){state.probulatorBeams.delete(playerId);continue;}const ground=toScreen(pilot.position),height=ufoFlightHeightPixels(),topY=ground.y-height-state.scale*.08,radius=Math.max(.001,beam.radius??1.7),footprint=probulatorFootprintPixels(radius),bottomX=footprint.x,bottomY=footprint.y,topX=Math.max(5,state.scale*.3),pulse=.72+Math.sin(now/85)*.12,gradient=ctx.createLinearGradient(ground.x,topY,ground.x,ground.y);gradient.addColorStop(0,'rgba(175,255,190,.78)');gradient.addColorStop(.55,'rgba(82,255,118,.32)');gradient.addColorStop(1,'rgba(46,240,91,.16)');ctx.save();ctx.globalAlpha=pulse;ctx.fillStyle=gradient;ctx.shadowColor='#65ff83';ctx.shadowBlur=Math.min(8,bottomX*.12);ctx.beginPath();ctx.moveTo(ground.x-topX,topY);ctx.lineTo(ground.x-bottomX,ground.y);ctx.bezierCurveTo(ground.x-bottomX,ground.y+bottomY,ground.x+bottomX,ground.y+bottomY,ground.x+bottomX,ground.y);ctx.lineTo(ground.x+topX,topY);ctx.closePath();ctx.fill();ctx.shadowBlur=Math.min(6,bottomX*.1);ctx.fillStyle='rgba(72,255,108,.22)';ctx.strokeStyle='rgba(146,255,165,.82)';ctx.lineWidth=Math.max(2,state.scale*.13);ctx.beginPath();ctx.ellipse(ground.x,ground.y,bottomX,bottomY,0,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.globalAlpha=.3+Math.sin(now/120)*.1;ctx.beginPath();ctx.ellipse(ground.x,ground.y,bottomX*.67,bottomY*.67,0,0,Math.PI*2);ctx.stroke();ctx.restore();}}
  function trackExplosiveCombat(combat,shot){
    const startedAt=shot?shot.started+shot.duration:performance.now(),locationId=state.players.get(combat.attackerId)?.locationId||state.actors.get(combat.attackerId)?.locationId||'outdoor';
    if(combat.weapon==='molotovCocktail'&&combat.hit)state.fireZones.push({position:combat.end,radius:6,locationId,startedAt,endsAt:Date.now()+10000});
    if(combat.weapon==='flamethrower'&&combat.hit)state.fireZones.push({position:combat.end,radius:3,locationId,startedAt,endsAt:Date.now()+10000});
    if(combat.statusEffect==='Burning'&&combat.statusEffectUntilUtc)state.burningCharacters.set(combat.targetId,Date.parse(combat.statusEffectUntilUtc));
  }
  function drawExplosiveEffects(now){
    state.fireZones=state.fireZones.filter(zone=>zone.endsAt>Date.now());
    for(const zone of state.fireZones){if(now<zone.startedAt||zone.locationId!==(state.players.get(state.playerId)?.locationId||'outdoor'))continue;const p=toScreen(zone.position),radius=Math.max(8,zone.radius*state.scale);ctx.save();const glow=ctx.createRadialGradient(p.x,p.y,2,p.x,p.y,radius);glow.addColorStop(0,'rgba(255,219,84,.55)');glow.addColorStop(.42,'rgba(255,89,28,.34)');glow.addColorStop(1,'rgba(100,17,4,0)');ctx.fillStyle=glow;ctx.beginPath();ctx.ellipse(p.x,p.y,radius,radius*state.pitch,0,0,Math.PI*2);ctx.fill();for(let index=0;index<10;index++){const seed=hash(`${zone.position.x}:${zone.position.y}:${index}`),angle=seed*Math.PI*2,distance=radius*(.15+.65*hash(`${index}:fire`)),x=p.x+Math.cos(angle)*distance,y=p.y+Math.sin(angle)*distance*state.pitch,flame=5+8*Math.abs(Math.sin(now/125+index));ctx.fillStyle=index%2?'#ff6a20':'#ffd04f';ctx.beginPath();ctx.moveTo(x-flame*.35,y+4);ctx.quadraticCurveTo(x-flame*.5,y-flame*.45,x,y-flame);ctx.quadraticCurveTo(x+flame*.5,y-flame*.45,x+flame*.35,y+4);ctx.closePath();ctx.fill();}ctx.restore();}
  }
  function drawAttachedFire(targetId,position,now){const endsAt=state.burningCharacters.get(targetId);if(!endsAt)return;if(endsAt<=Date.now()){state.burningCharacters.delete(targetId);return;}ctx.save();for(let index=0;index<5;index++){const phase=now/115+index*1.7,x=position.x+(index-2)*Math.max(2,state.scale*.16),height=Math.max(8,state.scale*(.6+.2*Math.sin(phase)));ctx.fillStyle=index%2?'rgba(255,80,18,.86)':'rgba(255,203,54,.9)';ctx.beginPath();ctx.moveTo(x-state.scale*.13,position.y+3);ctx.quadraticCurveTo(x-state.scale*.25,position.y-height*.5,x,position.y-height);ctx.quadraticCurveTo(x+state.scale*.25,position.y-height*.5,x+state.scale*.13,position.y+3);ctx.closePath();ctx.fill();}ctx.restore();}
  function drawFlamethrowerStreams(now){for(const flame of state.projectiles.filter(projectile=>projectile.weapon==='flamethrower'&&now-projectile.started<600)){const start=toScreen(flame.start),end=toScreen(flame.end),progress=Math.min(1,(now-flame.started)/260),tip={x:start.x+(end.x-start.x)*progress,y:start.y+(end.y-start.y)*progress};ctx.save();ctx.lineCap='round';ctx.globalAlpha=Math.max(0,1-(now-flame.started)/700);for(let width=18;width>=5;width-=6){ctx.strokeStyle=width>12?'rgba(255,72,16,.35)':width>6?'#ff8b22':'#ffe064';ctx.lineWidth=width;ctx.shadowColor='#ff6d18';ctx.shadowBlur=14;ctx.beginPath();ctx.moveTo(start.x,start.y-7);ctx.quadraticCurveTo((start.x+tip.x)/2,(start.y+tip.y)/2-8+Math.sin(now/45)*5,tip.x,tip.y);ctx.stroke();}ctx.restore();}}
  function drawProjectiles(now){
    state.projectiles=state.projectiles.filter(p=>now-p.started<700);
    for(const p of state.projectiles){
      if(p.weapon==='flamethrower')continue;
      const t=Math.min(1,(now-p.started)/(p.weapon==='greenBeam'?550:280)),a=toScreen(p.start),b=toScreen(p.end);
      ctx.save();
      if(p.weapon==='mooseSyrup'){ctx.strokeStyle='#d69b28';ctx.lineWidth=Math.max(2,state.scale*.12);ctx.globalAlpha=Math.max(0,1-(now-p.started)/700);ctx.setLineDash([5,3]);ctx.beginPath();ctx.moveTo(a.x,a.y-state.scale*.8);ctx.quadraticCurveTo((a.x+b.x)/2,Math.min(a.y,b.y)-state.scale*1.3,b.x,b.y);ctx.stroke();}
      else if(p.weapon==='greenBeam'){ctx.globalAlpha=.25+.55*(1-t);ctx.strokeStyle='#50ff77';ctx.lineWidth=Math.max(8,30*(1-t));ctx.shadowColor='#7aff9c';ctx.shadowBlur=22;ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);ctx.stroke();}
      else if(p.weapon==='fist'){ctx.strokeStyle='#ffe0a2';ctx.lineWidth=3;ctx.beginPath();ctx.arc(b.x,b.y-8,5+10*t,0,Math.PI*2);ctx.stroke();}
      else if(['knife','sword','hockeyStick','iceSkate'].includes(p.weapon)){const radius=p.weapon==='sword'?30:18,start=-1.25,end=start+Math.PI*1.3*t;ctx.translate(a.x,a.y-8);ctx.strokeStyle=p.weapon==='sword'?'rgba(235,244,239,.9)':'rgba(205,218,215,.88)';ctx.lineWidth=p.weapon==='sword'?4:2.5;ctx.beginPath();ctx.arc(0,0,radius,start,end);ctx.stroke();ctx.strokeStyle='rgba(255,222,126,.55)';ctx.lineWidth=1;ctx.beginPath();ctx.arc(0,0,radius+4,start,end);ctx.stroke();}
      ctx.restore();
    }
  }
  function isSleeping(entity){return !!entity?.asleepUntilUtc&&Date.parse(entity.asleepUntilUtc)>Date.now();}
  function drawSleepMarkers(now){const me=state.players.get(state.playerId);for(const entity of [...state.players.values(),...actorsHere()]){if(!isSleeping(entity)||(entity.locationId||'outdoor')!==(me?.locationId||'outdoor'))continue;const p=toScreen(entity.position);ctx.save();ctx.font='bold 16px monospace';ctx.textAlign='center';ctx.strokeStyle='#152322';ctx.lineWidth=4;ctx.fillStyle='#c5efff';const y=p.y-Math.max(22,state.scale*1.5)-Math.sin(now/300)*3;ctx.strokeText('Z z z',p.x,y);ctx.fillText('Z z z',p.x,y);ctx.restore();}}
  function drawAreaHazards(now){
    const me=state.players.get(state.playerId);if(!me)return;
    for(const zone of state.areaHazards.values()){
      const left=Date.parse(zone.endsAtUtc)-Date.now();if(left<=0||zone.locationId!==me.locationId)continue;
      if(me.locationId==='outdoor'&&Math.hypot(zone.position.x-me.position.x,zone.position.y-me.position.y)>visibleRange(me)+zone.radiusMeters)continue;
      if(state.dungeon&&!state.dungeon.isHome&&!state.dungeon.isStore&&!revealedAt(zone.position.x,zone.position.y))continue;
      const fire=zone.effect==='napalm',fruit=zone.effect.startsWith('musicalFruit'),radius=zone.radiusMeters,fade=Math.min(1,left/700);ctx.save();
      for(let i=0;i<24;i++){
        const seed=hash(`${zone.id}:${i}`),angle=i*2.399963+hash(zone.id)*Math.PI*2+now/6000,spread=Math.sqrt(zone.effect==='musicalFruitFinale'?(now/2200+i/24)%1:(i+.5)/24)*radius*.88;
        const point=toScreen({...zone.position,x:zone.position.x+Math.cos(angle)*spread,y:zone.position.y+Math.sin(angle)*spread}),size=(fruit?Math.max(3,state.scale*radius*.25):Math.max(12,state.scale*(fire?.6:1.3)))*(.8+.2*Math.sin(now/230+i)),lift=(fire?16:8)+Math.sin(now/450+i)*6;
        if(fire){ctx.fillStyle=`rgba(255,${90+Math.floor(seed*90)},25,${.72*fade})`;ctx.beginPath();ctx.moveTo(point.x-size*.45,point.y);ctx.quadraticCurveTo(point.x-size*.6,point.y-size,point.x+Math.sin(now/140+i)*size*.2,point.y-size*2-lift);ctx.quadraticCurveTo(point.x+size*.65,point.y-size,point.x+size*.45,point.y);ctx.fill();}
        else{const gradient=ctx.createRadialGradient(point.x,point.y-lift,0,point.x,point.y-lift,size);gradient.addColorStop(0,`rgba(151,239,68,${.36*fade})`);gradient.addColorStop(.65,`rgba(79,181,41,${.28*fade})`);gradient.addColorStop(1,'rgba(65,145,35,0)');ctx.fillStyle=gradient;ctx.beginPath();ctx.ellipse(point.x,point.y-lift,size,size*.75,0,0,Math.PI*2);ctx.fill();}
      }
      const center=toScreen(zone.position);ctx.font='bold 11px monospace';ctx.textAlign='center';ctx.strokeStyle='#132015';ctx.lineWidth=3;ctx.fillStyle=fire?'#ffcb73':'#c2f697';const label=`${zone.name} · ${Math.ceil(left/1000)}s`;ctx.strokeText(label,center.x,center.y-34);ctx.fillText(label,center.x,center.y-34);ctx.restore();
    }
  }
  function drawDamageIndicators(now){state.damageIndicators=state.damageIndicators.filter(indicator=>now-indicator.started<2400);for(const indicator of state.damageIndicators){if(now<indicator.started)continue;const age=now-indicator.started,t=Math.min(1,age/2400),target=indicator.abducteeId&&(state.players.get(indicator.abducteeId)||state.actors.get(indicator.abducteeId)),visual=target?abductionVisual(target,now):null,p=toScreen(visual?.position||indicator.position),y=p.y-(visual?.lift||0)-34-t*36,alpha=Math.min(1,age/120)*(1-Math.max(0,(t-.62)/.38));ctx.save();ctx.globalAlpha=alpha;ctx.textAlign='center';ctx.lineJoin='round';ctx.font='700 15px monospace';const damage=`-${indicator.damage.toFixed(indicator.damage%1?2:0)} ♥`;ctx.lineWidth=4;ctx.strokeStyle='rgba(30,8,8,.9)';ctx.strokeText(damage,p.x,y);ctx.fillStyle=indicator.targetDied?'#ffe077':'#ff6b63';ctx.fillText(damage,p.x,y);ctx.font='700 10px monospace';const source=`from ${indicator.attackerName}`;ctx.lineWidth=3;ctx.strokeText(source,p.x,y+14);ctx.fillStyle='#fff0c2';ctx.fillText(source,p.x,y+14);ctx.restore();}}

  function receiveWorldSound(sound){if(!sound)return;gameAudio.play(sound.sound,sound.position,sound.locationId,{id:sound.id,range:120,volume:.7});if(sound.text&&sound.speakerId)state.speech.set(sound.speakerId,{chat:{username:'Bus',message:sound.text},position:sound.position,locationId:sound.locationId,expiresAt:Date.now()+2200});}
  function playMessageSound(message){const cues={dirtyWaterCollected:'pour',chestItemsTaken:'coins',lootItemsTaken:'coins',lootCollected:'coins',inventoryItemDropped:'cloth',dungeonEntered:'door',dungeonExited:'door',playerTeleported:'teleport',recipeStudied:'paper',furnitureUpdated:'metal',questCompleted:'success'};const kind=message.type==='craftingUpdated'||message.type==='homeWorkshopUpdated'?message.sound:cues[message.type];if(kind)gameAudio.local(kind);}
  function receiveChat(chat){
    state.speech.set(chat.playerId,{chat,expiresAt:Date.now()+10000});
    const speaker=state.players.get(chat.playerId)||state.actors.get(chat.playerId)||actorsHere().find(a=>a.id===chat.playerId);
    gameAudio.speech(chat,speaker);
    if(chat.playerId===state.playerId||(speaker&&pointVisible(speaker.position,viewBounds())&&dynamicVisible(speaker.position))) {
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
    const viewport=viewportWidth();for(const [speakerId,speech] of state.speech){if(speech.expiresAt<=now){state.speech.delete(speakerId);continue;}const speaker=state.players.get(speakerId)||state.actors.get(speakerId)||(state.transit?.buses||[]).find(b=>b.id===speakerId)||(speech.position?{position:speech.position,locationId:speech.locationId}:null);if(speech.locationId&&(state.players.get(state.playerId)?.locationId||'outdoor')!==speech.locationId)continue;if(!speaker||!pointVisible(speaker.position,view)||speakerId!==state.playerId&&!dynamicVisible(speaker.position))continue;const visual=abductionVisual(speaker,performance.now()),anchor=toScreen(visual?.position||speaker.position),aboard=speaker.abduction&&Date.now()-Date.parse(speaker.abduction.startedAtUtc)>=10000&&Date.now()-Date.parse(speaker.abduction.startedAtUtc)<25000,siblings=aboard?[...state.players.values(),...state.actors.values()].filter(target=>target.abduction?.pilotId===speaker.abduction.pilotId).sort((a,b)=>a.id.localeCompare(b.id)):[],bubbleIndex=aboard?Math.max(0,siblings.findIndex(target=>target.id===speakerId)):0,maxWidth=220;anchor.y-=visual?.lift||0;if(aboard){anchor.x+=(bubbleIndex%2?1:-1)*100;anchor.y+=Math.floor(bubbleIndex/2)*65;}ctx.save();const {lines,width,height}=speechLayout(speech,maxWidth);const x=Math.max(5,Math.min(viewport-width-5,anchor.x-width/2)),y=Math.max(5,anchor.y-state.scale*1.7-height);ctx.fillStyle='rgba(255,248,218,.96)';ctx.strokeStyle='#4a3520';ctx.lineWidth=2;ctx.beginPath();ctx.roundRect(x,y,width,height,6);ctx.fill();ctx.stroke();const tipX=Math.max(x+12,Math.min(x+width-12,anchor.x));ctx.beginPath();ctx.moveTo(tipX-7,y+height);ctx.lineTo(tipX+7,y+height);ctx.lineTo(anchor.x,anchor.y-state.scale*.75);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillStyle='#5a3b1f';ctx.font='700 10px "Trebuchet MS",sans-serif';ctx.textAlign='left';ctx.fillText(speech.chat.username,x+9,y+14);ctx.fillStyle='#1d211e';ctx.font='12px "Trebuchet MS",sans-serif';lines.forEach((line,index)=>ctx.fillText(line,x+9,y+29+index*15));ctx.restore();}
  }
  function speechLayout(speech,maxWidth){
    const {username,message}=speech.chat,revision=state.speechFontRevision||0,cached=speech.layout;
    if(cached&&cached.username===username&&cached.message===message&&cached.maxWidth===maxWidth&&cached.revision===revision)return cached;
    ctx.font='12px "Trebuchet MS",sans-serif';const lines=wrapChat(`“${message}”`,maxWidth-18);
    ctx.font='700 10px "Trebuchet MS",sans-serif';const nameWidth=ctx.measureText(username).width;
    ctx.font='12px "Trebuchet MS",sans-serif';const textWidth=Math.max(nameWidth,...lines.map(line=>ctx.measureText(line).width));
    return speech.layout={username,message,maxWidth,revision,lines,width:Math.min(maxWidth,Math.max(72,textWidth+18)),height:24+lines.length*15};
  }
  function wrapChat(text,maxWidth){const words=text.split(/\s+/),lines=[];let line='';for(const word of words){const test=line?`${line} ${word}`:word;if(ctx.measureText(test).width<=maxWidth||!line)line=test;else{lines.push(line);line=word;}}if(line)lines.push(line);return lines.slice(0,6);}

  function drawTarget(now){if(!state.target)return;const p=toScreen(state.target),r=8+Math.sin(now/180)*2;ctx.strokeStyle='#fff09a';ctx.lineWidth=2;ctx.beginPath();ctx.arc(p.x,p.y,r,0,Math.PI*2);ctx.stroke();}
  function drawAtmosphere(me,detail,now){const viewport=viewportWidth(),light=daylight();const moonBoost=light<.2?Math.max(0,state.weather?.moonIllumination||0)*.18:0;const darkness=Math.max(0,.7-light*.7-moonBoost);if(darkness>.02){lightCtx.clearRect(0,0,viewport,innerHeight);lightCtx.fillStyle=`rgba(8,18,29,${darkness})`;lightCtx.fillRect(0,0,viewport,innerHeight);const candle=candleActive(me,now);if(me&&(me.lanternOn||me.flashlightOn||candle)){const p=toScreen(me.position);lightCtx.globalCompositeOperation='destination-out';if(me.lanternOn||candle){const radius=me.lanternOn?(detail===0?65:145):(detail===0?33:73),inner=me.lanternOn?15:8,g=lightCtx.createRadialGradient(p.x,p.y,inner,p.x,p.y,radius);g.addColorStop(0,me.lanternOn?'rgba(0,0,0,.94)':'rgba(0,0,0,.72)');g.addColorStop(.65,me.lanternOn?'rgba(0,0,0,.78)':'rgba(0,0,0,.5)');g.addColorStop(1,'rgba(0,0,0,0)');lightCtx.fillStyle=g;lightCtx.beginPath();lightCtx.arc(p.x,p.y,radius,0,Math.PI*2);lightCtx.fill();}if(me.flashlightOn){const facing=state.facings.get(me.id)||'south',vectors={north:[0,-1],south:[0,1],east:[1,0],west:[-1,0]},v=vectors[facing],length=detail===0?125:245,width=length*.43;lightCtx.fillStyle='rgba(0,0,0,.88)';lightCtx.beginPath();lightCtx.moveTo(p.x,p.y);lightCtx.lineTo(p.x+v[0]*length-v[1]*width,p.y+v[1]*length+v[0]*width);lightCtx.lineTo(p.x+v[0]*length+v[1]*width,p.y+v[1]*length-v[0]*width);lightCtx.closePath();lightCtx.fill();}lightCtx.globalCompositeOperation='source-over';}ctx.drawImage(lightCanvas,0,0);}
    const code=state.weather?.weatherCode??0,snow=(code>=71&&code<=77)||code===85||code===86,rain=(code>=51&&code<=67)||(code>=80&&code<=82)||code>=95;
    if((rain||snow)&&detail>0){
      const count=detail===2?90:35,velocity=precipitationVelocity(state.weather,state.pitch,state.shear,snow),seconds=now/1000;
      ctx.strokeStyle=rain?'rgba(169,211,232,.55)':'rgba(245,250,255,.8)';ctx.lineWidth=rain?1:2;
      for(let i=0;i<count;i++){
        const p=precipitationParticle(i,seconds,viewport,innerHeight,velocity,snow);
        ctx.save();ctx.globalAlpha=p.alpha;ctx.lineWidth=p.width;ctx.beginPath();ctx.moveTo(p.x-p.dx,p.y-p.dy);ctx.lineTo(p.x,p.y);ctx.stroke();ctx.restore();
      }
    }
  }
  function drawLaser(me){if(!me?.laserOn)return;const facing=state.facings.get(me.id)||'south',vectors={north:{x:0,y:1},south:{x:0,y:-1},east:{x:1,y:0},west:{x:-1,y:0}},direction=vectors[facing];const view=viewBounds(),corners=[{x:view.minX,y:view.minY},{x:view.maxX,y:view.minY},{x:view.maxX,y:view.maxY},{x:view.minX,y:view.maxY}],maximum=Math.max(...corners.map(point=>Math.hypot(point.x-me.position.x,point.y-me.position.y)))+20,distance=laserCollisionDistance(me.position,direction,maximum,me.id),end={x:me.position.x+direction.x*distance,y:me.position.y+direction.y*distance},a=toScreen(me.position),b=toScreen(end);ctx.save();ctx.lineCap='round';ctx.shadowColor='#ff2020';ctx.shadowBlur=12;ctx.strokeStyle='rgba(255,40,40,.35)';ctx.lineWidth=7;ctx.beginPath();ctx.moveTo(a.x,a.y-state.scale*.55);ctx.lineTo(b.x,b.y-state.scale*.15);ctx.stroke();ctx.shadowBlur=5;ctx.strokeStyle='#ff5b4d';ctx.lineWidth=2;ctx.stroke();if(distance<maximum-.01){ctx.fillStyle='#fff0d0';ctx.beginPath();ctx.arc(b.x,b.y-state.scale*.15,4,0,Math.PI*2);ctx.fill();}ctx.restore();}
  function laserCollisionDistance(origin,direction,maximum,ownerId){let nearest=maximum;const segment=(a,b)=>{const value=raySegment(origin,direction,a,b);if(value>.65&&value<nearest)nearest=value;};const circle=(center,radius)=>{const ox=center.x-origin.x,oy=center.y-origin.y,t=ox*direction.x+oy*direction.y;if(t<=.65||t>=nearest)return;const perpendicular=Math.abs(ox*direction.y-oy*direction.x);if(perpendicular<=radius){const offset=Math.sqrt(Math.max(0,radius*radius-perpendicular*perpendicular));nearest=Math.max(.65,t-offset);}};
    if(state.dungeon){for(const wall of state.dungeon.walls||[]){if(wall.doorStart>=0){if(Math.abs(wall.x1-wall.x2)<.01){segment({x:wall.x1,y:wall.y1},{x:wall.x1,y:wall.doorStart});segment({x:wall.x1,y:wall.doorEnd},{x:wall.x2,y:wall.y2});}else{segment({x:wall.x1,y:wall.y1},{x:wall.doorStart,y:wall.y1});segment({x:wall.doorEnd,y:wall.y1},{x:wall.x2,y:wall.y2});}}else segment({x:wall.x1,y:wall.y1},{x:wall.x2,y:wall.y2});}for(const item of state.dungeon.furnishings||[])circle(item.position,item.properties?.objectType==='table'?1.2:.75);for(const actor of state.dungeon.actors||[])circle(actor.position,.4);}else{for(const building of renderList('building')){for(let i=0;i<(building.geometry?.length||0)-1;i++)segment(building.geometry[i],building.geometry[i+1]);}for(const fence of renderList('fence')){for(let i=0;i<(fence.geometry?.length||0)-1;i++)segment(fence.geometry[i],fence.geometry[i+1]);}for(const kind of ['tree','bush','vehicle'])for(const item of renderList(kind))circle(item.position,kind==='tree'?prop(item,'collisionRadius',.85):kind==='vehicle'?2.4:.45);for(const actor of state.actors.values())circle(actor.position,.4);for(const player of state.players.values())if(player.id!==ownerId&&(player.locationId||'outdoor')==='outdoor')circle(player.position,.4);}return nearest;}
  function raySegment(origin,direction,a,b){const sx=b.x-a.x,sy=b.y-a.y,cross=direction.x*sy-direction.y*sx;if(Math.abs(cross)<1e-8)return Infinity;const qx=a.x-origin.x,qy=a.y-origin.y,t=(qx*sy-qy*sx)/cross,u=(qx*direction.y-qy*direction.x)/cross;return t>=0&&u>=0&&u<=1?t:Infinity;}
  function serverClockOffsetMinutes(events=state.privateState?.serverConfiguration?.events){if(!events)return 0;const hostOffset=Number(events.serverUtcOffsetMinutes||0),manual=events.serverTimeMode==='manual'?Number(events.serverTimeOffsetMinutes||0):0;return hostOffset+manual;}
  function serverNowMs(events=state.privateState?.serverConfiguration?.events){return Date.now()+serverClockOffsetMinutes(events)*60000;}
  function daylight(){const events=state.privateState?.serverConfiguration?.events;if(events?.serverTimeMode==='manual'){const now=new Date(serverNowMs(events)),hour=now.getUTCHours()+now.getUTCMinutes()/60;if(hour<6.25||hour>19.75)return.08;if(hour<7)return.08+.92*(hour-6.25)/.75;if(hour>19)return 1-.92*(hour-19)/.75;return 1;}if(!state.weather?.sunriseUtc||!state.weather?.sunsetUtc)return state.weather?.isDay?1:.12;const now=Date.now(),rise=Date.parse(state.weather.sunriseUtc),set=Date.parse(state.weather.sunsetUtc),twilight=45*60000;if(now<rise-twilight||now>set+twilight)return .08;if(now<rise)return .08+.92*(now-(rise-twilight))/twilight;if(now>set)return 1-.92*(now-set)/twilight;return 1;}

  function worldToGps(position){const region=position.region,lat0=(region.latitudeBand+.5)*Math.PI/180,lon0=(region.longitudeBand+.5)*Math.PI/180,R=6378137,e2=6.69437999014e-3,sin=Math.sin(lat0),den=Math.sqrt(1-e2*sin*sin),mLon=R*Math.cos(lat0)/den,mLat=R*(1-e2)/Math.pow(1-e2*sin*sin,1.5);return{latitude:(lat0+position.y/mLat)*180/Math.PI,longitude:(lon0+position.x/mLon)*180/Math.PI};}
  function gpsToWorld(latitude,longitude,region){const lat0=(region.latitudeBand+.5)*Math.PI/180,lon0=(region.longitudeBand+.5)*Math.PI/180,R=6378137,e2=6.69437999014e-3,sin=Math.sin(lat0),den=Math.sqrt(1-e2*sin*sin),mLon=R*Math.cos(lat0)/den,mLat=R*(1-e2)/Math.pow(1-e2*sin*sin,1.5);return{x:(longitude*Math.PI/180-lon0)*mLon,y:(latitude*Math.PI/180-lat0)*mLat};}
  function gpsText(position){if(!position)return'—';const g=worldToGps(position);return`${g.latitude.toFixed(2)}, ${g.longitude.toFixed(2)}`;}
  function renderActiveEvents(me){
    const now=Date.now();if(now-state.lastEventPanelUpdate<250)return;state.lastEventPanelUpdate=now;
    const groups=new Map();
    for(const actor of state.actors.values()){
      const endsAt=Date.parse(actor.eventEndsAtUtc||'');if(!Number.isFinite(endsAt)||endsAt<=now)continue;
      const key=`${actor.subtype}:${actor.eventStartedAtUtc||''}:${actor.eventEndsAtUtc}`;
      const group=groups.get(key)||{name:actor.eventName||title(actor.subtype),endsAt,actors:[]};group.actors.push(actor);groups.set(key,group);
    }
    const events=[...groups.values()].sort((a,b)=>a.endsAt-b.endsAt);const wasHidden=ui.activeEventsPanel.hidden;ui.activeEventsPanel.hidden=!events.length;if(!events.length){ui.activeEventsList.replaceChildren();return;}
    if(wasHidden&&ui.activeEventsPanel.classList.contains('panel-collapsed'))ui.activeEventsPanel.querySelector('.panel-collapse-button')?.click();
    const origin=me?.locationId==='outdoor'?me.position:state.lastOutdoorPosition;ui.activeEventsList.replaceChildren();
    for(const event of events){
      const position={x:event.actors.reduce((sum,actor)=>sum+actor.position.x,0)/event.actors.length,y:event.actors.reduce((sum,actor)=>sum+actor.position.y,0)/event.actors.length};
      const seconds=Math.max(0,Math.ceil((event.endsAt-now)/1000)),hours=Math.floor(seconds/3600),minutes=Math.floor(seconds%3600/60),clockText=hours?`${hours}:${String(minutes).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`:`${minutes}:${String(seconds%60).padStart(2,'0')}`;
      const card=document.createElement('article'),heading=document.createElement('div'),name=document.createElement('strong'),badge=document.createElement('span'),time=document.createElement('div'),bearing=document.createElement('div');card.className='active-event-card';heading.className='active-event-title';name.textContent=event.name;const portal=realityInversionPortal(event.actors[0]);badge.textContent=portal?(portal.entering?'ARRIVING':'DEPARTING'):'ACTIVE';heading.append(name,badge);time.className='active-event-countdown';time.textContent=`${clockText} remaining`;bearing.className='active-event-bearing';
      if(origin){const dx=position.x-origin.x,dy=position.y-origin.y,distance=Math.hypot(dx,dy),angle=(Math.atan2(dx,dy)*180/Math.PI+360)%360,dirs=['N','NE','E','SE','S','SW','W','NW'],arrows=['↑','↗','→','↘','↓','↙','←','↖'],index=Math.round(angle/45)%8;bearing.textContent=`${arrows[index]} ${dirs[index]} · ${distance>=1000?(distance/1000).toFixed(2)+' km':distance.toFixed(0)+' m'}`;}else bearing.textContent='Direction unavailable';
      card.append(heading,time,bearing);ui.activeEventsList.append(card);
    }
  }
  function updateFrameTelemetry(me,now){
    if(state.lastFrameTelemetryAt!=null&&now-state.lastFrameTelemetryAt<100&&state.telemetryPlayer===me&&state.telemetryPrivateState===state.privateState&&state.telemetryTarget===state.target)return;
    state.lastFrameTelemetryAt=now;state.telemetryPlayer=me;state.telemetryPrivateState=state.privateState;state.telemetryTarget=state.target;
    updateTelemetry(me);updateEnergyDrinkTelemetry(me);
  }
  function updateTelemetry(me){if(!me)return;const mealStatus=Survival.telemetry(me);renderActiveEvents(me);ui.playerGps.textContent=me.locationId==='outdoor'?gpsText(me.position):state.dungeon?.underwater?state.dungeon.underwater.name:state.dungeon?.isHome?'Inside Home':state.dungeon?.isStore?'Inside Store':`Inside dungeon · level ${state.dungeon?.level||1}/${state.dungeon?.levelCount||1}`;ui.destinationGpsRow.hidden=!state.target;ui.destinationDistanceRow.hidden=!state.target;ui.destinationGps.textContent=state.target?(me.locationId==='outdoor'?gpsText({...state.target,region:me.position.region}):`${state.target.x.toFixed(1)} m, ${state.target.y.toFixed(1)} m`):'—';ui.terrain.textContent=me.locationId==='outdoor'?title(me.terrain):state.dungeon?.underwater?'Submerged · difficulty '+state.dungeon.difficulty:state.dungeon?.isHome?'Home floor':state.dungeon?.isStore?'Store floor':`Dungeon level ${state.dungeon?.level||1} of ${state.dungeon?.levelCount||1}`;ui.elevation.textContent=`${me.position.z.toFixed(1)} m / ${(me.position.z*3.28084).toFixed(0)} ft`;ui.playerGps.title='GPS: '+ui.playerGps.textContent;ui.terrain.title='Terrain: '+ui.terrain.textContent;ui.elevation.title='Elevation: '+ui.elevation.textContent;const distance=state.target?Math.hypot(state.target.x-me.position.x,state.target.y-me.position.y):null;ui.distance.textContent=distance===null?'—':distance>=1000?`${(distance/1000).toFixed(2)} km`:`${distance.toFixed(1)} m`;const hearts=Math.max(0,me.healthHearts??10);ui.hearts.textContent=`${hearts.toFixed(2)} / ${me.maximumHealthHearts??10}`;const stamina=Math.max(0,me.stamina??10);ui.stamina.textContent=`${stamina.toFixed(2)} / ${me.maximumStamina??10}`;const water=Math.max(0,me.water??10);ui.water.textContent=`${water.toFixed(2)} / ${me.maximumWater??10}`;const bodyHeat=Math.max(0,Math.min(me.maximumBodyHeat||100,me.bodyHeat??50)),thermalState=bodyHeat<=15?'Freezing':bodyHeat<40?'Cold':bodyHeat>=90?'Overheating':bodyHeat>65?'Hot':'Comfortable';ui.bodyHeat.textContent=`${bodyHeat.toFixed(0)}%`;ui.bodyHeat.title=thermalState;ui.bodyHeat.style.color=bodyHeat<=15?'#78c8ff':bodyHeat<40?'#a8dfff':bodyHeat>=90?'#ff6258':bodyHeat>65?'#ffae4c':'#ffc45f';const wanted=me.wantedLevel||0;ui.wanted.textContent=String(wanted);ui.wanted.style.color=wanted?'#ff6860':'';const effects=mealStatus.buffs?[mealStatus.buffs]:[];if(Date.parse(state.privateState?.mapleSyrupUntilUtc||'')>Date.now())effects.push(`Maple syrup: +25% speed, +5 perception - ${countdown(state.privateState.mapleSyrupUntilUtc)}`);if(isSleeping(me))effects.push(`Asleep · ${countdown(me.asleepUntilUtc)}`);if((me.speedMetersPerSecond||0)>.01)effects.push(`Moving ${(me.speedMetersPerSecond*2.23694).toFixed(1)} mph`);if(water<=0)effects.push('Dehydrated: ½ speed');if(bodyHeat<=0)effects.push('Freezing: losing hearts');else if(bodyHeat>=85)effects.push('Overheated: rapid stamina drain');if(me.foodProtectedUntilUtc&&Date.parse(me.foodProtectedUntilUtc)>Date.now())effects.push(`Fed ${countdown(me.foodProtectedUntilUtc)}`);if(me.waterProtectedUntilUtc&&Date.parse(me.waterProtectedUntilUtc)>Date.now())effects.push(`Hydrated ${countdown(me.waterProtectedUntilUtc)}`);if(me.magicHikingShoesOn)effects.push('Magic hiking shoes · additive speed · ½ stamina drain');if(me.magicRunningShoesOn)effects.push('Magic running shoes · additive speed · ½ stamina off roads/sidewalks');if(me.hatOn)effects.push('Sun hat · ½ water drain');if(candleActive(me))effects.push(`Candle lit · ${countdown(me.candleUntilUtc)}`);if(me.godMode)effects.push('God Mode · 5× speed · climate protected');ui.effects.textContent=effects.join(' · ')||'None';}
  function initializeMiniMapFilters(){
    state.mapPreferences=MapMarkers.preferences(localStorage);
    for(const input of document.querySelectorAll('[data-minimap-filter]')){
      input.checked=MapMarkers.categoryVisible(state.mapPreferences,input.dataset.minimapFilter);
      input.addEventListener('change',()=>{state.mapPreferences.categories[input.dataset.minimapFilter]=input.checked;MapMarkers.savePreferences(localStorage,state.mapPreferences);state.lastMiniMapDraw=-Infinity;});
    }
  }
  function createQuestMapToggle(quest){
    const label=document.createElement('label'),input=document.createElement('input');label.className='quest-map-toggle';
    input.type='checkbox';input.dataset.questMarker=quest.id;input.checked=MapMarkers.questVisible(state.mapPreferences,quest.id);
    input.addEventListener('change',()=>{state.mapPreferences.quests[quest.id]=input.checked;MapMarkers.savePreferences(localStorage,state.mapPreferences);state.lastMiniMapDraw=-Infinity;
      for(const other of document.querySelectorAll('[data-quest-marker]'))if(other.dataset.questMarker===quest.id)other.checked=input.checked;});
    label.append(input,document.createTextNode(quest.title));label.title='Show or hide this quest objective on the minimap';return label;
  }
  function drawMiniMap(me){
    const now=performance.now();if(now-state.lastMiniMapDraw<200)return;state.lastMiniMapDraw=now;const c=miniMapCtx,w=miniMapCanvas.width,h=miniMapCanvas.height,pad=16,range=500;state.miniMapMarkers=[];c.clearRect(0,0,w,h);c.fillStyle='#07120f';c.fillRect(0,0,w,h);c.strokeStyle='rgba(225,202,118,.22)';c.lineWidth=1;for(const radius of [.25,.5,.75,1]){c.beginPath();c.arc(w/2,h/2,Math.min(w,h)/2*radius-pad*.4,0,Math.PI*2);c.stroke();}c.beginPath();c.moveTo(w/2,pad);c.lineTo(w/2,h-pad);c.moveTo(pad,h/2);c.lineTo(w-pad,h/2);c.stroke();c.fillStyle='#e9d485';c.font='700 10px monospace';c.textAlign='center';c.fillText('N',w/2,12);c.fillStyle='#98a99e';c.textAlign='right';c.fillText('1 km across',w-7,h-7);
    if(!me){updateMiniMapTooltip();return;}const indoors=me.locationId!=='outdoor',origin=indoors?(state.lastOutdoorPosition||state.privateState?.base?.position):me.position;if(!origin){updateMiniMapTooltip();return;}
    if(indoors){c.fillStyle='#9aa89f';c.font='700 9px monospace';c.textAlign='left';c.fillText('MAIN MAP · VIEW FROM OUTSIDE',8,h-7);}
    const plot=(position,color,glyph,size=5,type='Marker',label=type,fastTravel=null)=>{if(!position||!MapMarkers.categoryVisible(state.mapPreferences,type)||Inversions.insideSmug(position)&&type!=='You')return;const point=MapMarkers.project(position,origin,w,h,type,range,pad);if(!point)return;const {x,y}=point,radius=Math.max(7,size*1.7);if(point.offMap)label+=' (off map)';c.fillStyle='rgba(4,10,8,.88)';c.strokeStyle=color;c.lineWidth=1.5;c.beginPath();c.arc(x,y,radius,0,Math.PI*2);c.fill();c.stroke();c.fillStyle=color;c.strokeStyle='rgba(7,12,10,.9)';c.lineWidth=2;c.font=`700 ${size*2.6}px "Segoe UI Symbol",monospace`;c.textAlign='center';c.textBaseline='middle';c.strokeText(glyph,x,y);c.fillText(glyph,x,y);if(type==='Casino'){c.font='700 10px monospace';const tx=Math.max(34,Math.min(w-34,x)),ty=y>h/2?y-radius-9:y+radius+9;c.strokeText('CASINO',tx,ty);c.fillText('CASINO',tx,ty);}state.miniMapMarkers.push({x,y,radius,type,label:fastTravel?`${label} · Click to teleport`:label,color,fastTravel,distanceMeters:point.distanceMeters,distanceReference:indoors?'from the outside map position':'away'});};
    for(const stop of MapMarkers.nearby(state.transit?.stops||[],origin,4,100))plot(stop.position,'#73d7ff','B',3,'Bus stop',`${stop.name} · ${stop.direction}`);
    for(const bus of state.transit?.buses||[])plot(bus.position,'#f9ce56','B',5,'Bus',`Bus · ${bus.routeName} · ${bus.status}`);
    for(const store of MapMarkers.nearby(state.miniMapStores.filter(s=>s.properties?.merchantCategory!=='casino'),origin,6,110)){const category=title(store.properties?.merchantCategory||'general'),name=store.properties?.name||store.properties?.brand||`${category} store`;plot(store.position,'#f2ce65','◆',4,'Store',`${category} store · ${name}`);}
    const casino=state.privateState?.casino||state.miniMapStores.find(s=>s.properties?.merchantCategory==='casino');
    if(casino)plot(casino.position,'#68e0bb','C',6,'Casino',`Casino · ${casino.name||casino.properties?.name||'Casino'}`);
    for(const actor of MapMarkers.nearby([...state.actors.values()].filter(a=>a.kind==='npc'&&a.isQuestGiver&&(a.locationId||'outdoor')==='outdoor'),origin,4,110))plot(actor.position,'#63ee81','$',3,'Quest giver',`Quest giver · ${actor.name}`);
    for(const quest of state.privateState?.quests||[])if(['active','ready'].includes(quest.status)&&MapMarkers.questVisible(state.mapPreferences,quest.id)){const stage=questStage(quest);if(stage.location==='outdoor')plot(stage.position,'#ffe36d','!',4,'Quest objective',`${quest.title} · ${stage.name}`);}
    for(const player of state.players.values())if(player.id!==state.playerId&&(player.locationId||'outdoor')==='outdoor')plot(player.position,'#65d9ef','●',4,'Player',`Player · ${player.name}`);
    for(const grave of state.graves.values())plot(grave.position,'#d7d0c4','✝',5,'Grave',`${grave.ownerName||'Unknown player'}'s tombstone · contains all carried items and cash`);
    for(const flag of state.reality.values())if(flag.properties?.objectType==='personalFlag'&&flag.properties?.owner===state.playerId)plot(flag.position,'#ef6258','⚑',6,'Flag',`Flag · ${flag.properties?.label||'Unnamed'} · Placed by ${flag.properties?.ownerName||me.name}`,{targetType:'flag',targetId:flag.id});
    const home=state.privateState?.base;ui.miniMapTeleportHome.disabled=!home||indoors;ui.miniMapTeleportHome.title=!home?'Waiting for your Home to be assigned.':indoors?'Leave the building before teleporting Home.':'Teleport directly to your Home.';plot(home?.position,'#8ff09f','⌂',6,'Home',`Home · ${home?.ownerName||me.name}'s base`,home?{targetType:'home',targetId:home.buildingId}:null);
    if(!indoors)plot(me.position,'#fff5a6','●',4,'You',`You · ${me.name}`);
    updateMiniMapTooltip();
  }
  function miniMapMarkerAt(event){const rect=miniMapCanvas.getBoundingClientRect(),x=(event.clientX-rect.left)*miniMapCanvas.width/Math.max(1,rect.width),y=(event.clientY-rect.top)*miniMapCanvas.height/Math.max(1,rect.height);return state.miniMapMarkers.filter(marker=>Math.hypot(marker.x-x,marker.y-y)<=marker.radius+4).sort((a,b)=>Math.hypot(a.x-x,a.y-y)-Math.hypot(b.x-x,b.y-y))[0]||null;}
  function miniMapDistanceText(distance){return distance>=1000?(distance/1000).toFixed(2)+' km':Math.round(distance)+' m';}
  function updateMiniMapTooltip(){
    const event=state.miniMapPointer,marker=event?miniMapMarkerAt(event):null;
    ui.miniMapTooltip.hidden=!marker;miniMapCanvas.style.cursor=marker?.fastTravel?'pointer':marker?'help':'default';
    if(!marker)return;
    ui.miniMapTooltip.textContent=marker.label+' · '+miniMapDistanceText(marker.distanceMeters)+' '+marker.distanceReference;
    ui.miniMapTooltip.style.color=marker.color;ui.miniMapTooltip.style.borderColor=marker.color;
    const panel=ui.miniMapPanel.getBoundingClientRect();
    ui.miniMapTooltip.style.left=Math.max(8,Math.min(panel.width-ui.miniMapTooltip.offsetWidth-8,event.clientX-panel.left+10))+'px';
    ui.miniMapTooltip.style.top=Math.max(30,event.clientY-panel.top-ui.miniMapTooltip.offsetHeight-8)+'px';
  }
  miniMapCanvas.addEventListener('mousemove',event=>{state.miniMapPointer={clientX:event.clientX,clientY:event.clientY};updateMiniMapTooltip();});
  miniMapCanvas.addEventListener('mouseleave',()=>{state.miniMapPointer=null;updateMiniMapTooltip();});
  function requestMiniMapFastTravel(type,fastTravel){if(!fastTravel)return;stopTravel();ui.miniMapTooltip.hidden=true;setWorldTask(`Teleporting to ${type}…`);send({type:'mapFastTravel',...fastTravel});}
  miniMapCanvas.addEventListener('click',event=>{const marker=miniMapMarkerAt(event);if(marker?.fastTravel)requestMiniMapFastTravel(marker.type,marker.fastTravel);});
  ui.miniMapTeleportHome.addEventListener('click',()=>{const home=state.privateState?.base;if(!home){showToast('Your Home is still being assigned.');return;}requestMiniMapFastTravel('Home',{targetType:'home',targetId:home.buildingId});});
  const countdown=value=>{const s=Math.max(0,Math.ceil((Date.parse(value)-Date.now())/1000));return`${Math.floor(s/60)}:${String(s%60).padStart(2,'0')}`;};
  function syncLightControls(me){if(!me)return;}
  const title=value=>String(value||'').startsWith('quest:food:')?`Food order for ${(state.privateState?.quests||[]).find(q=>'quest:food:'+q.id===value)?.destinationName||'delivery'}`:value==='gallonOfGas'?'Gallon of gas':value==='areaMap'?'Map of this block':String(value||'').replace(/([A-Z])/g,' $1').trim().replace(/^./,c=>c.toUpperCase());
  const clock=value=>new Date(value).toLocaleTimeString([],{hour:'numeric',minute:'2-digit'});
  function initializeTravelButtons(){
    const descriptions={scuba:'Submerge in water. Air drains slowly. Select again to surface.',swim:'Swim with Swimmies; high stamina cost. Rest before exhaustion.',walk:'Travel on foot.',run:'Run on foot; uses stamina.',skateboard:'Ride your skateboard on paved surfaces; uses stamina.',bike:'Ride your bicycle; uses stamina.',eBike:'Ride your electric bike; uses battery power.',dirtBike:'Ride your dirt bike; 76 miles per gallon of gas. Select it, then use a gallon of gas in Inventory to refuel.',motorcycle:'Ride your motorcycle; 57 miles per gallon of gas. Select it, then use a gallon of gas in Inventory to refuel.',raft:'Paddle your raft through water.',ufo:'Fly your UFO with its built-in Probulator; automatically uses 1 Kryptonite from Inventory per 10 miles.'};
    for(const button of document.querySelectorAll('[data-mode]')){
      const mode=button.dataset.mode,label=button.textContent.trim(),art=document.createElementNS('http://www.w3.org/2000/svg','svg'),icon=document.createElementNS('http://www.w3.org/2000/svg','use');
      art.classList.add('travel-mode-icon');art.setAttribute('viewBox','0 0 32 32');art.setAttribute('focusable','false');
      icon.setAttribute('href','travel-icons.svg?v=3#'+mode);art.append(icon);
      button.dataset.description=label+' — '+descriptions[mode]+(travelVehicleTypes.has(mode)||mode==='raft'?' Stored at Home; available from any owned inventory.':'');
      button.setAttribute('aria-label',label);button.setAttribute('aria-pressed',String(mode==='walk'));button.title=button.dataset.description;
      art.removeAttribute('title');art.setAttribute('aria-hidden','true');button.replaceChildren(art);
      const fuel={dirtBike:'gas',motorcycle:'gas',eBike:'electric',ufo:'kryptonite'}[mode];
      if(fuel){
        const badge=document.createElementNS('http://www.w3.org/2000/svg','svg'),symbol=document.createElementNS('http://www.w3.org/2000/svg','use');
        badge.classList.add('travel-fuel-icon');badge.setAttribute('viewBox','0 0 16 16');badge.setAttribute('aria-hidden','true');badge.setAttribute('focusable','false');
        symbol.setAttribute('href','travel-icons.svg?v=3#fuel-'+fuel);badge.append(symbol);button.append(badge);
      }
      button.classList.toggle('active',mode==='walk');
    }
  }
  function syncTravelButtonState(button,player,probed=probedPhase(player)){
    const key=button.dataset.mode.toLowerCase(),indoors=(player?.locationId||'outdoor')!=='outdoor';
    const blocked=!!probed&&key!=='walk',blockedInside=indoors&&['bike','ebike','dirtbike','motorcycle','ufo'].includes(key);
    button.disabled=blocked||blockedInside||(key==='scuba'&&!state.dungeon?.underwater&&(!['shallowWater','deepWater'].includes(player?.terrain)||indoors));
    const reason=blocked?'Probed: only walking is available for '+countdown(probed.until)+', or until you sleep.':blockedInside?'This travel mode cannot be used inside a building.':'';
    const inventory=state.privateState?.inventory?.items||[],crystals=inventory.filter(item=>item.itemType==='kryptonite').reduce((total,item)=>total+item.quantity,0);
    const range=key==='dirtbike'?Number(player?.dirtBikeGasGallons||0)*76:key==='motorcycle'?Number(player?.motorcycleGasGallons||0)*57:key==='ebike'?Number(player?.eBikeRemainingMeters||0)/1609.344:key==='ufo'?Number(player?.ufoRemainingMeters||0)/1609.344+crystals*10:null;
    const fuelStatus=range===null?'':player?.godMode?' God Mode: no fuel consumption.':` Range remaining: ${range.toFixed(2)} miles.`;
    button.title=button.dataset.description+fuelStatus+(reason?' '+reason:'');
  }
  function updateMode(mode){
    const player=state.players.get(state.playerId),owned=new Set([...(state.privateState?.inventory?.items||[]).filter(item=>item.quantity>0).map(item=>item.itemType.toLowerCase()),...(state.privateState?.ownedVehicles||[]).map(item=>item.toLowerCase())]);
    const required={skateboard:'skateboard',bike:'bike',ebike:'ebike',dirtbike:'dirtbike',motorcycle:'motorcycle',raft:'inflatableraft',swim:'swimmies',scuba:'scubagear',ufo:'ufo'};
    document.querySelectorAll('[data-mode]').forEach(button=>{
      const key=button.dataset.mode.toLowerCase(),item=required[key],active=key===String(mode||'walk').toLowerCase();
      button.classList.toggle('active',active);button.setAttribute('aria-pressed',String(active));button.hidden=!!item&&!player?.godMode&&!owned.has(item);
      syncTravelButtonState(button,player);
    });
  }

function movementLoop(time){const me=state.players.get(state.playerId);if(me&&!controlsPaused()&&!me.abduction&&!me.ridingBusId&&!me.waitingAtBusStopId&&!isSleeping(me)&&time-state.lastInput>28&&!state.worldBusy){advanceAutoTreasure(time,me);Survival.advanceGather(state,me,send,stopTravel);gardenUI.advance(me);maintainPosturing(time,me);maintainFollowCommand(time,me);if(state.pendingMerchant){const merchant=actorsHere().find(actor=>actor.id===state.pendingMerchant.id);if(!merchant||merchant.locationId!==me.locationId)state.pendingMerchant=null;else if(Math.hypot(merchant.position.x-me.position.x,merchant.position.y-me.position.y)<=4.8){state.pendingMerchant=null;state.path=[];state.target=null;send({type:'requestTrade',merchantId:merchant.id});}}if(state.pendingDoor&&Math.hypot(state.pendingDoor.position.x-me.position.x,state.pendingDoor.position.y-me.position.y)<5.5){const door=state.pendingDoor;state.pendingDoor=null;if(doorIsLocked(door))stopTravel('That door is locked.');else{send({type:'enterDungeon',doorId:door.id});stopTravel();}}maintainTreasurePickup(me);if(state.pendingChop&&Math.hypot(state.pendingChop.position.x-me.position.x,state.pendingChop.position.y-me.position.y)<=2.3){send({type:'chopVegetation',entityId:state.pendingChop.id});state.pendingChop=null;stopTravel();}if(state.pendingPet&&Math.hypot(state.pendingPet.position.x-me.position.x,state.pendingPet.position.y-me.position.y)<=3){send({type:'captureQuestPet',actorId:state.pendingPet.id});state.pendingPet=null;stopTravel();}if(state.pendingDungeonAction&&Math.hypot(state.pendingDungeonAction.target.x-me.position.x,state.pendingDungeonAction.target.y-me.position.y)<2.7){const action=state.pendingDungeonAction.action;state.pendingDungeonAction=null;if(action==='exit')send({type:'exitDungeon'});else send({type:'changeDungeonLevel',direction:action==='up'?-1:1});stopTravel();}if(!state.moveInFlight){let dx=0,dy=0;if(state.keys.has('w')||state.keys.has('arrowup'))dy+=1;if(state.keys.has('s')||state.keys.has('arrowdown'))dy-=1;if(state.keys.has('a')||state.keys.has('arrowleft'))dx-=1;if(state.keys.has('d')||state.keys.has('arrowright'))dx+=1;if(dx||dy){state.followCommand=null;state.pendingMerchant=null;state.target=null;state.path=[];state.moveInFlight=true;send({type:'moveRequest',x:dx,y:dy,sequence:++state.pathSequence});state.lastInput=time;}else if(state.target&&(!state.areaLoading||pointInLoadedArea(state.target))){while(state.path.length&&Math.hypot(state.path[0].x-me.position.x,state.path[0].y-me.position.y)<.45)state.path.shift();const waypoint=state.path[0]||state.target,tx=waypoint.x-me.position.x,ty=waypoint.y-me.position.y,d=Math.hypot(tx,ty);if(d<(state.dungeon?.underwater ? .001 : .2)&&!state.path.length){state.target=null;}else if(d>(state.dungeon?.underwater ? .000001 : .01)){state.moveInFlight=true;send({type:'moveRequest',x:tx/d,y:ty/d,sequence:state.pathSequence,maxDistanceMeters:d,destinationX:waypoint.x,destinationY:waypoint.y});state.lastInput=time;}}}}requestAnimationFrame(movementLoop);}

  function removeCombatTarget(id){state.actors.delete(id);if(state.dungeon)state.dungeon={...state.dungeon,actors:(state.dungeon.actors||[]).filter(a=>a.id!==id)};if(state.actionActor?.id===id)state.actionActor=null;state.actionChoices&&(state.actionChoices.targets=state.actionChoices.targets.filter(c=>c.id!==id));}
  function actorsHere(){const actors=state.dungeon?(state.dungeon.actors||[]):[...state.actors.values()];return actors.filter(actor=>!actor.eventEndsAtUtc||Date.parse(actor.eventEndsAtUtc)>Date.now());}
  function combatTargets(){const me=state.players.get(state.playerId),location=me?.locationId||'outdoor';return [...actorsHere().filter(actor=>!actor.abduction),...[...state.players.values()].filter(player=>!player.abduction&&player.id!==state.playerId&&(player.locationId||'outdoor')===location)].filter(target=>dynamicVisible(target.position));}
  function choppableAt(point){return nearPoint([...renderList('tree'),...renderList('bush'),...renderList('resourceNode').filter(item=>item.properties?.subtype==='mailbox')],point,Math.max(1.1,16/state.scale));}
  function vehicleAt(point){return nearPoint(renderList('vehicle'),point,Math.max(1.4,20/state.scale));}
  function postOfficeAt(point){return nearPoint(renderList('resourceNode').filter(item=>item.properties?.subtype==='postOfficeBox'),point,Math.max(1.2,18/state.scale));}
  function nearPoint(collection,world,meters=1.5){return collection.filter(item=>Math.hypot(item.position.x-world.x,item.position.y-world.y)<=meters).sort((a,b)=>Math.hypot(a.position.x-world.x,a.position.y-world.y)-Math.hypot(b.position.x-world.x,b.position.y-world.y))[0];}
  function applyDoorLocks(doors){state.doorLocks=new Map((doors||[]).map(item=>[item.doorId,item]));state.storeHours=new Map((doors||[]).filter(item=>item.storeHours).map(item=>[item.buildingId,item.storeHours]));}
  function storeHoursLabel(buildingId){const hours=state.storeHours.get(buildingId);if(!hours)return'';return`${String(hours.openHour).padStart(2,'0')}:00–${String(hours.closeHour).padStart(2,'0')}:00 daily${hours.closeHour<hours.openHour?' (overnight)':''} · server time`;}
  function storeIsOpen(buildingId){const hours=state.storeHours.get(buildingId);if(!hours)return false;const clock=new Date(serverNowMs()),hour=clock.getUTCHours()+clock.getUTCMinutes()/60;return(hour-hours.openHour+24)%24<12;}
  function lockedDoorMessage(door){const hours=storeHoursLabel(door?.properties?.buildingId);return hours?`Store closed. Open ${hours}.`:'This door is locked. Locks refresh on the configured server schedule.';}
  function hoverBuildingAtScreen(x,y){
    const point={x,y},door=doorAtScreen(x,y);if(door)return state.baseById.get(door.properties?.buildingId);
    return renderList('building').filter(building=>{
      const ground=(building.geometry||[]).map(toScreen);if(ground.length<3)return false;
      if(polygonContains(point,ground))return true;if(lod()===0||building.properties?.state==='rubble')return false;
      const levels=Math.max(1,Number(building.properties?.['building:levels']||building.properties?.levels||2)),height=Math.min(150,levels*3*state.scale*.52),roof=ground.map(p=>({x:p.x,y:p.y-height}));
      return polygonContains(point,roof)||ground.some((p,i)=>{const next=(i+1)%ground.length;return polygonContains(point,[p,ground[next],roof[next],roof[i]]);});
    }).sort((a,b)=>toScreen(b.position).y-toScreen(a.position).y)[0];
  }
  function doorIsLocked(door){if(!door)return false;const buildingId=door.properties?.buildingId;if(buildingId&&(buildingId===state.privateState?.base?.buildingId||state.publicBases?.has(buildingId)))return false;return !!state.doorLocks.get(door.id)?.locked;}
  function doorAtScreen(clientX,clientY){return renderList('door').map(door=>{const p=toScreen(door.position),w=Math.max(9,state.scale*.95),h=Math.max(16,2.05*state.scale*.52),inside=clientX>=p.x-w/2-10&&clientX<=p.x+w/2+10&&clientY>=p.y-h-10&&clientY<=p.y+12;return{door,inside,distance:Math.hypot(clientX-p.x,clientY-(p.y-h/2))};}).filter(item=>item.inside).sort((a,b)=>a.distance-b.distance)[0]?.door;}
  function polygonContains(point,geometry){let inside=false;for(let i=0,j=geometry.length-1;i<geometry.length;j=i++){const a=geometry[i],b=geometry[j];if((a.y>point.y)!==(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/((b.y-a.y)||Number.EPSILON)+a.x)inside=!inside;}return inside;}
  function buildingAt(point){return renderList('building').filter(building=>building.geometry?.length>=3&&polygonContains(point,building.geometry)).sort((a,b)=>(a._bounds.maxX-a._bounds.minX)*(a._bounds.maxY-a._bounds.minY)-(b._bounds.maxX-b._bounds.minX)*(b._bounds.maxY-b._bounds.minY))[0];}
  function storeCategoryForBuilding(building){if(!building)return null;if(building.properties?.merchantCategory)return building.properties.merchantCategory;const poi=renderList('pointOfInterest').find(item=>item.properties?.merchantCategory&&building.geometry?.length>=3&&polygonContains(item.position,building.geometry));return poi?.properties?.merchantCategory||null;}
  function buildingSquareFeet(building){if(!building?.geometry?.length)return 800;let twiceArea=0;for(let i=0;i<building.geometry.length;i++){const a=building.geometry[i],b=building.geometry[(i+1)%building.geometry.length];twiceArea+=a.x*b.y-b.x*a.y;}return Math.max(1,Math.abs(twiceArea)/2*10.7639104167);}
  function buildingDifficulty(building){const squareFeet=buildingSquareFeet(building);return squareFeet<=2000?1:Math.max(1,Math.min(100,Math.round(1+(squareFeet-2000)*49/8000)));}
  function buildingPriceCents(building){const squareFeet=buildingSquareFeet(building),dollars=Math.min(50000000,Math.max(350000,350000*Math.exp(Math.max(0,squareFeet-800)/5000)));return Math.round(dollars/500)*50000;}
  function furnitureAt(point){return(state.dungeon?.furnishings||[]).filter(item=>{const size=furnitureSize(item);return Math.abs(point.x-item.position.x)<=size.w/2+.25&&Math.abs(point.y-item.position.y)<=size.d/2+.25;}).sort((a,b)=>Math.hypot(a.position.x-point.x,a.position.y-point.y)-Math.hypot(b.position.x-point.x,b.position.y-point.y))[0];}
  function addressFor(building){const p=building?.properties||{},line=[p['addr:housenumber'],p['addr:street']].filter(Boolean).join(' '),place=[p['addr:city'],p['addr:state'],p['addr:postcode']].filter(Boolean).join(', ');return[line,place].filter(Boolean).join('<br>');}
  function showActionMenuAt(){updateActionMenu();ui.actionMenu.hidden=false;const first=[...ui.rightRail.children].find(panel=>panel!==ui.actionMenu&&panel.matches('.panel'));dockPanel(ui.actionMenu,false,first);setRightRailCollapsed(false);if(ui.actionMenu.classList.contains('panel-collapsed'))ui.actionMenu.querySelector('.panel-collapse-button')?.click();requestAnimationFrame(()=>{ui.rightRail.scrollTop=0;ui.actionMenu.scrollIntoView({block:'start'});});}
  function navigateTo(target,preserveFollow=false,quiet=false){if(state.players.get(state.playerId)?.ridingBusId||state.players.get(state.playerId)?.waitingAtBusStopId){showToast('Get off the bus or cancel waiting before moving.');return;}if(state.dungeon?.underwater)target=Scuba.clampPosition(state.dungeon,target);if(!preserveFollow)state.followCommand=null;state.target={x:target.x,y:target.y};state.path=[];state.pathSequence++;if(!state.dungeon&&!pointInLoadedArea(target))beginAreaLoading(target,'Loading area and finding route…');else if(!quiet)setWorldTask('Finding route…');send({type:'pathRequest',x:target.x,y:target.y,sequence:state.pathSequence,includeSnapshot:state.areaLoading});}
  function requestTeleport(target){state.followCommand=null;if(!pointInLoadedArea(target))beginAreaLoading(target,'Loading teleport destination…');else setWorldTask('Preparing teleport…');send({type:'teleport',x:target.x,y:target.y,godMode:true});}
  function beginTrade(actor){if(!actor?.isMerchant)return;state.followCommand=null;const merchant=actorsHere().find(item=>item.id===actor.id)||actor,me=state.players.get(state.playerId);if(!me)return;if(me.locationId!==merchant.locationId){showToast('That merchant is not in this area.');return;}if(Math.hypot(me.position.x-merchant.position.x,me.position.y-merchant.position.y)<=4.8){state.pendingMerchant=null;state.path=[];state.target=null;send({type:'requestTrade',merchantId:merchant.id});}else{state.pendingMerchant=merchant;navigateTo(merchant.position);showToast(`Walking to ${merchant.name} to trade…`);}}

  function maintainPosturing(time,me){
    if(time<state.postureSuppressedUntil||time<state.nextPostureAt||state.keys.size||state.followCommand||state.moveInFlight)return;
    if((state.target&&!state.autoFlee)||state.pendingDoor||state.pendingMerchant||state.pendingChest||state.pendingDungeonAction||state.pendingChop||state.pendingPet||state.movingFurniture)return;
    state.nextPostureAt=time+600;
    const action=PlayerCommands.automaticAction({mode:state.actionMode,me,targets:combatTargets(),players:state.players,relationships:state.relationships,attackers:state.defensiveThreats,now:time,pvpEnabled:state.snapshot?.reality?.pvpEnabled!==false,avoid:state.postureAvoid,range:equippedWeaponRange(me),canFire:target=>!state.dungeon?.underwater||(Scuba.canAttack(me.equippedWeapon)&&(me.equippedWeapon!=='spearGun'||Scuba.attackPlan(me.position,target.position,equippedWeaponRange(me),state.dungeon).ready))});
    if(action?.kind==='fire'){fireFromPosition(time,me,action.target);return;}
    if(action?.kind==='attack'){beginFollowCommand('attack',action.target,false,true);return;}
    if(action?.kind==='flee'){
      // Try a different outward angle after an obstructed route, without turning toward the threat.
      const angles=[0,Math.PI/4,-Math.PI/4,Math.PI/2,-Math.PI/2,Math.PI/3,-Math.PI/3],angle=angles[state.fleeAttempt%angles.length],dx=action.destination.x-me.position.x,dy=action.destination.y-me.position.y;
      const destination={x:me.position.x+dx*Math.cos(angle)-dy*Math.sin(angle),y:me.position.y+dx*Math.sin(angle)+dy*Math.cos(angle)};
      if(state.dungeon){destination.x=Math.max(.6,Math.min(state.dungeon.width-.6,destination.x));destination.y=Math.max(.6,Math.min(state.dungeon.height-.6,destination.y));}
      if(!state.dungeon&&!pointInLoadedArea(destination))return;
      if(state.target&&Math.hypot(state.target.x-destination.x,state.target.y-destination.y)<3)return;
      if(me.travelMode!=='walk'){send({type:'setTravelMode',mode:'walk'});return;}
      state.autoFlee=true;navigateTo(destination,true);return;
    }
    if(state.autoFlee)stopTravel(undefined,true);
  }
  function fireFromPosition(time,me,target){if(time<(state.nextStationaryAttackAt||0))return;state.nextStationaryAttackAt=time+equippedAttackInterval(me)+75;const point=target.position;state.facings.set(me.id,point.x<me.position.x?'west':'east');send(hazardWeaponTypes.has(me.equippedWeapon)?{type:'throwHazard',x:point.x,y:point.y}:{type:'attack',targetId:target.id,weapon:me.equippedWeapon});}
  function receiveCombatFear(combat){
    const me=state.players.get(state.playerId);
    if(!me||state.keys.size||state.autoFlee||combat.targetId!==me.id||!combat.fleeInFear||combat.targetDied||me.godMode||isSleeping(me)||me.abduction||me.ridingBusId||me.waitingAtBusStopId)return;
    const destination=PlayerCommands.fearDestination(me.position,combat.start,state.dungeon);
    stopTravel();state.autoFlee=true;state.postureSuppressedUntil=performance.now()+2000;
    navigateTo(destination,false,true);showToast('Running in fear! Nut up ability helps you stand your ground.');
  }

  function followTargetById(targetId){return actorsHere().find(target=>target.id===targetId)||[...state.players.values()].find(target=>target.id===targetId&&target.id!==state.playerId)||(state.transit?.buses||[]).find(target=>target.id===targetId);}
  function equippedWeaponRange(me){const configured=state.privateState?.serverConfiguration?.items?.find(item=>item.itemType===me?.equippedWeapon);if(configured)return Number(configured.rangeMeters)||1.6;return weaponRanges[me?.equippedWeapon]||1.6;}
  function equippedAttackInterval(me){const configured=state.privateState?.serverConfiguration?.items?.find(item=>item.itemType===me?.equippedWeapon),base=me?.equippedWeapon==='ar15'&&me.ar15FireMode==='burst'?1000:Math.max(50,(Number(configured?.attackIntervalSeconds)||.5)*1000);return base*(['fist','knife','sword','hockeyStick','iceSkate'].includes(me?.equippedWeapon)?1:(state.privateState?.progression?.shootingIntervalMultiplier??1)*(me?.gloveShootingIntervalMultiplier??1));}
  function beginFollowCommand(mode,target,worldObject=false,automatic=false){stopTravel(undefined,automatic);const me=state.players.get(state.playerId);if(!me||!target||target.id===me.id)return;if(mode==='attack'&&state.dungeon?.underwater&&!Scuba.canAttack(me.equippedWeapon)){showToast('Equip a spear gun or melee weapon to attack underwater.');return;}if(mode==='attack'&&(me.equippedWeapon||'none')==='none'){showToast('Equip a weapon before choosing Attack.');return;}state.pendingMerchant=null;state.pendingDoor=null;state.pendingChest=null;state.pendingDungeonAction=null;state.path=[];state.target=null;if(mode==='attack'&&!automatic&&!['attackReady','aggressive','defensive'].includes(state.actionMode))setActionMode('attackReady');state.followCommand={mode,worldObject,automatic,targetId:target.id,targetName:target.name||target.routeName||target.properties?.name||(worldObject?target.kind:'target'),weapon:me.equippedWeapon,nextRouteAt:0,nextAttackAt:0,lastTarget:null};ui.actionMenu.hidden=true;if(mode==='attack'&&me.travelMode==='ufo')send({type:'toggleProbulator',directionX:0,directionY:0,enabled:true});showToast(mode==='attack'?`Attacking ${state.followCommand.targetName} until stopped or defeated.`:`Stalking ${target.name} until another command is given.`);}
  function maintainScubaAttack(time,me,target,command){const plan=Scuba.attackPlan(me.position,target.position,equippedWeaponRange(me),state.dungeon);if(plan.ready){state.path=[];state.target=null;state.facings.set(me.id,plan.facing);if(time>=command.nextAttackAt){command.nextAttackAt=time+equippedAttackInterval(me)+75;send({type:'attack',targetId:target.id,weapon:me.equippedWeapon});}return;}const moved=!command.lastTarget||Math.hypot(target.position.x-command.lastTarget.x,target.position.y-command.lastTarget.y)>.2;if(time>=command.nextRouteAt&&(moved||!state.target)){command.nextRouteAt=time+200;command.lastTarget={...target.position};navigateTo(plan.destination,true,true);}}
  function beginBusBoarding(bus){stopTravel();const me=state.players.get(state.playerId);if(!BusTransit.canApproachBoarding(me,bus)||isSleeping(me))return;if(me.travelMode!=='walk')send({type:'setTravelMode',mode:'walk'});state.followCommand={mode:'boardBus',targetId:bus.id,targetName:'Bus',nextRouteAt:0};clearActionChoices();ui.actionMenu.hidden=true;showToast('Walking to the parked bus to board and start service.');}
  function maintainBusBoarding(time,me,bus,command){const plan=BusTransit.boardingPlan(me,bus);if(plan.unavailable){stopTravel('This bus is no longer available.');return;}if(plan.ready){stopTravel();send({type:'boardBus',busId:bus.id});return;}if(state.moveInFlight)return;if(time>=command.nextRouteAt&&(!state.target||Math.hypot(state.target.x-plan.destination.x,state.target.y-plan.destination.y)>.5)){command.nextRouteAt=time+500;navigateTo(plan.destination,true,true);}}
  function maintainFollowCommand(time,me){const command=state.followCommand;if(!command)return;const target=command.worldObject?state.baseById.get(command.targetId):followTargetById(command.targetId);if(command.automatic&&target&&!dynamicVisible(target.position)){stopTravel(undefined,true);return;}if(PlayerCommands.defeated(target)||(target.locationId||'outdoor')!==(me.locationId||'outdoor')||(target.healthHearts??1)<=0){stopTravel(`${command.targetName} is no longer available.`);return;}if(command.mode==='boardBus'){maintainBusBoarding(time,me,target,command);return;}if(command.mode==='attack'&&((me.equippedWeapon||'none')==='none'||me.equippedWeapon!==command.weapon)){stopTravel('Attack stopped because the equipped weapon changed.');return;}if(state.dungeon?.underwater&&command.mode==='attack'&&me.equippedWeapon==='spearGun'){maintainScubaAttack(time,me,target,command);return;}const point=command.worldObject?PlayerCommands.attackPoint(target,me.position):target.routeId?BusTransit.attackPoint(target,me.position):target.position,dx=point.x-me.position.x,dy=point.y-me.position.y,distance=command.mode==='attack'&&me.equippedWeapon==='probulator'?Math.hypot(dx+dy*state.shear,dy*state.pitch/(.38/1.35)):Math.hypot(dx,dy),range=command.mode==='attack'?equippedWeaponRange(me):2.4;if(command.mode==='attack'&&distance<=range&&time>=command.nextAttackAt){command.nextAttackAt=time+equippedAttackInterval(me)+75;send(command.worldObject?{type:'attackWorldObject',entityId:target.id}:hazardWeaponTypes.has(me.equippedWeapon)?{type:'throwHazard',x:point.x,y:point.y}:{type:'attack',targetId:target.id,weapon:me.equippedWeapon});}const holdDistance=command.mode==='attack'?Math.max(.9,range*.78):2.4;if(distance<=holdDistance){state.path=[];state.target=null;return;}const destination={x:point.x-dx/distance*holdDistance,y:point.y-dy/distance*holdDistance},moved=!command.lastTarget||Math.hypot(target.position.x-command.lastTarget.x,target.position.y-command.lastTarget.y)>1;if(time>=command.nextRouteAt&&(moved||!state.target)){command.nextRouteAt=time+800;command.lastTarget={x:target.position.x,y:target.position.y};navigateTo(destination,true,true);}}

  let contextHoldTimer=null, suppressHoldRelease=false;
  function cancelContextHold(){clearTimeout(contextHoldTimer);contextHoldTimer=null;}
  function resetContextHold(){cancelContextHold();state.pointer.down=false;state.pointer.button=null;}
  addEventListener('blur',resetContextHold);
  document.addEventListener('visibilitychange',()=>{if(document.hidden)resetContextHold();});
  // Capture before ordinary map controls: clicking is the only gameplay input in Retro battles.
  for(const type of ['pointerdown','mousedown','mouseup','click','dblclick','contextmenu'])canvas.addEventListener(type,event=>{
    if(!state.dungeon?.retroBattle)return;
    event.preventDefault();event.stopImmediatePropagation();
    if(type==='click'&&event.button===0&&!state.dungeon.isCompleted)send({type:'retroJump'});
  },true);
  canvas.addEventListener('mouseleave',cancelContextHold);
  addEventListener('click',event=>{if(!suppressHoldRelease)return;suppressHoldRelease=false;event.preventDefault();event.stopImmediatePropagation();},true);
  canvas.addEventListener('mousedown',()=>{state.actionPostal=null;state.actionWorldObject=null;});
  canvas.addEventListener('mousedown',event=>{cancelContextHold();suppressHoldRelease=false;if(event.button===0||event.button===2){state.pointer={down:true,dragged:false,button:event.button,startX:event.clientX,startY:event.clientY,x:event.clientX,y:event.clientY,furnitureMove:!!state.movingFurniture&&event.button===0};if(state.pointer.furnitureMove)state.furniturePreview=toWorld({x:event.clientX,y:event.clientY});ui.actionMenu.hidden=true;if(event.button===0&&!state.pointer.furnitureMove){contextHoldTimer=setTimeout(()=>{contextHoldTimer=null;if(!state.pointer.down||state.pointer.dragged)return;state.pointer.longPressed=true;clearTimeout(primaryClickTimer);primaryClickTimer=null;openActionChoices(toWorld({x:state.pointer.x,y:state.pointer.y}),state.pointer.x,state.pointer.y);},600);}}});
  addEventListener('mousemove',event=>{const world=toWorld({x:event.clientX,y:event.clientY});if(typeof gardenUI!=='undefined')gardenUI.move(world);if(state.pointer.down&&state.pointer.furnitureMove){state.pointer.dragged=true;state.furniturePreview=world;return;}const furniture=state.dungeon?.isHome?furnitureAt(world):null,actor=!furniture?actorAtScreen(event.clientX,event.clientY):null,building=!actor&&!furniture&&!state.dungeon?hoverBuildingAtScreen(event.clientX,event.clientY):null;if(furniture){ui.tooltip.innerHTML=`<strong>${furniture.properties?.displayName||title(furniture.properties?.objectType||'Furniture')}</strong><br>${title(furniture.properties?.color||'')} · ${title(furniture.properties?.pattern||'')}`;}else if(actor){const isPlayer=state.players.has(actor.id),rating=state.relationships.get(actor.id)??actor.friendRating??0;ui.tooltip.innerHTML=`<strong>${actor.name}</strong><br>Health: ${(actor.healthHearts??5).toFixed(1)} / ${(actor.maximumHealthHearts??5).toFixed(1)} ♥<br>Weapon: ${title(actor.equippedWeapon||'fist')}${isPlayer?'':`<br>Friend / foe: ${rating.toFixed(2)}${actor.isMerchant?`<br>${title(actor.merchantCategory||'general')} merchant`:''}`}`;}else if(building){const address=addressFor(building),name=building.properties?.name||building.properties?.brand||'Building',difficulty=buildingDifficulty(building),category=storeCategoryForBuilding(building),claim=state.publicBases?.get(building.id),classification=claim?`${claim.ownerName}'s Home`:category==='casino'?'Casino':category?'Store':difficulty>50?`Stronghold · Difficulty ${difficulty}`:`Dungeon · Difficulty ${difficulty}`,health=building.properties?.healthHearts?`<br>Health: ${Number(building.properties.healthHearts).toFixed(0)} / 5000 ♥`:'';ui.tooltip.innerHTML=`<strong>${name}</strong><br>${classification}${claim?'<br>Always unlocked':state.storeHours.has(building.id)?`<br>${storeIsOpen(building.id)?'Open now':'Closed now'}<br>${storeHoursLabel(building.id)}`:''}${health}${address?`<br>${address}`:''}`;}ui.tooltip.hidden=!(furniture||actor||building);if(furniture||actor||building){ui.tooltip.style.left=`${Math.max(8,Math.min(event.clientX+14,viewportWidth()-ui.tooltip.offsetWidth-8))}px`;ui.tooltip.style.top=`${Math.max(8,Math.min(event.clientY+14,innerHeight-ui.tooltip.offsetHeight-8))}px`;}if(!state.pointer.down||state.pointer.longPressed)return;const dx=event.clientX-state.pointer.x,dy=event.clientY-state.pointer.y;if(Math.hypot(event.clientX-state.pointer.startX,event.clientY-state.pointer.startY)>3){state.pointer.dragged=true;cancelContextHold();}if(state.pointer.dragged){state.camera.x-=dx/state.scale;state.camera.y+=dy/(state.scale*state.pitch);state.camera.x+=dy/state.scale*state.shear/state.pitch;state.follow=false;}state.pointer.x=event.clientX;state.pointer.y=event.clientY;});
  addEventListener('mouseup',event=>{if(!state.pointer.down||event.button!==state.pointer.button)return;cancelContextHold();if(state.pointer.longPressed){suppressHoldRelease=true;setTimeout(()=>suppressHoldRelease=false,0);state.pointer.down=false;state.pointer.button=null;return;}if(state.pointer.furnitureMove&&state.movingFurniture){const point=toWorld({x:event.clientX,y:event.clientY}),stored=state.movingFurniture.properties?.stored==='true',rotation=Number(state.movingFurniture.properties?.rotationDegrees||0);send({type:stored?'placeFurniture':'moveFurniture',furnitureId:state.movingFurniture.id,x:point.x,y:point.y,rotationDegrees:rotation});state.suppressClick=true;setTimeout(()=>state.suppressClick=false,250);state.pointer.down=false;state.pointer.button=null;return;}if(state.pointer.dragged&&event.button===0){state.suppressClick=true;setTimeout(()=>state.suppressClick=false,250);}if(!state.pointer.dragged&&event.button===2){openActionChoices(toWorld({x:event.clientX,y:event.clientY}),event.clientX,event.clientY);}state.pointer.down=false;state.pointer.button=null;});
  canvas.addEventListener('contextmenu',event=>event.preventDefault());
  let primaryClickTimer=null;
  function clickedOwnUfo(me,clientX,clientY){if(!me||me.travelMode!=='ufo'||state.dungeon)return false;const ground=toScreen(me.position),s=Math.max(5,state.scale*.46),craftSize=s*1.9,centerY=ground.y-ufoFlightHeightPixels()-s*.42,rx=Math.max(26,craftSize*1.35),ry=Math.max(18,craftSize*.62);return((clientX-ground.x)/rx)**2+((clientY-centerY)/ry)**2<=1;}
  function toggleOwnProbulator(){send({type:'toggleProbulator',directionX:0,directionY:0});}
  function actorAtScreen(clientX,clientY){const candidates=combatTargets().filter(a=>{const p=toScreen(a.position),b=state.dungeon?.underwater?Scuba.actorBounds(a,state.scale):QuestNavigation.actorBounds(a,state.scale);return clientX>=p.x+b.left-10&&clientX<=p.x+b.right+10&&clientY>=p.y+b.top-10&&clientY<=p.y+b.bottom+10;});return candidates.sort((a,b)=>a.position.y-b.position.y)[0]||null;}
  function maintainTreasurePickup(me){for(const [key,collection,chest] of [['pendingLoot',state.loot,false],['pendingChest',state.chests,true]]){if(!state[key])continue;const item=collection.get(state[key].id);if(!item){state[key]=null;continue;}if(Math.hypot(item.position.x-me.position.x,item.position.y-me.position.y)<3.7){state[key]=null;send(chest?{type:'openChest',chestId:item.id}:{type:'openLoot',lootId:item.id});stopTravel();return;}}}
  function beginScubaTreasure(kind,item,me){if(!item||!me)return;if(Math.hypot(item.position.x-me.position.x,item.position.y-me.position.y)<3.7){send(kind==='chest'?{type:'openChest',chestId:item.id}:{type:'openLoot',lootId:item.id});return;}if(kind==='chest')state.pendingChest=item;else state.pendingLoot=item;navigateTo(item.position);}
  function handlePrimaryClick(target,clientX,clientY,beamWasActive=state.probulatorBeams.has(state.playerId)){if(typeof gardenUI!=='undefined'&&gardenUI.click(target))return;if(Inversions.canvasClick(clientX,clientY)||Inversions.click(target))return;clearActionChoices();const choices=actionTargetsAt(clientX,clientY);if(choices.length>0&&!(choices.length===1&&((choices[0].kind==='actor'&&(state.dungeon?.underwater||PlayerCommands.clickAttacks(state.actionMode)))||(state.dungeon?.underwater&&['loot','chest'].includes(choices[0].kind))))){openActionChoices(target,clientX,clientY,choices);return;}const busStop=busStopAt(target);if(busStop){showBusStopMenu(busStop);return;}if(state.players.get(state.playerId)?.ridingBusId){showToast('Use Get off bus now before moving.');return;}stopTravel();ui.actionMenu.hidden=true;const me=state.players.get(state.playerId);if(isSleeping(me)){showToast('You are asleep until the gas effect wears off.');return;}if(me?.abduction){showToast('You are being abducted. Wait until the UFO puts you back down.');return;}if(state.dungeon?.underwater&&choices.length===1&&['loot','chest'].includes(choices[0].kind)){beginScubaTreasure(choices[0].kind,choices[0].entity,me);return;}if(PlayerCommands.clickAttacks(state.actionMode)&&clickedOwnUfo(me,clientX,clientY)){state.followCommand=null;state.path=[];state.target=null;if(!beamWasActive)toggleOwnProbulator(me);return;}const feature=dungeonFeatureAt(target);if(feature){state.followCommand=null;state.actionPoint=target;state.actionDungeonFeature=feature;state.actionFurniture=null;state.actionActor=null;state.actionDoor=null;showActionMenuAt(clientX,clientY);return;}const furnishing=state.dungeon?.isHome?furnitureAt(target):null;if(furnishing){state.followCommand=null;state.actionPoint=furnishing.position;state.actionFurniture=furnishing;state.actionActor=null;state.actionDoor=null;state.actionDungeonFeature=null;showActionMenuAt(clientX,clientY);return;}const postal=!state.dungeon?postOfficeAt(target):null;if(postal){state.followCommand=null;state.actionPoint=postal.position;state.actionPostal=postal;state.actionActor=null;state.actionDoor=null;state.actionFurniture=null;state.actionDungeonFeature=null;showActionMenuAt(clientX,clientY);return;}if(typeof Survival!=='undefined'&&Survival.clickWild(target,state,renderList,send,navigateTo,showToast))return;const combatTarget=actorAtScreen(clientX,clientY);const petQuest=combatTarget&&questForActor(combatTarget.id);if(petQuest?.kind==='missingPet'&&petQuest.status==='active'){if(me&&Math.hypot(combatTarget.position.x-me.position.x,combatTarget.position.y-me.position.y)<=3)send({type:'captureQuestPet',actorId:combatTarget.id});else{state.pendingPet=combatTarget;navigateTo(combatTarget.position);}return;}if(combatTarget?.isMerchant&&(!PlayerCommands.clickAttacks(state.actionMode)||me?.equippedWeapon==='none')){if(combatTarget.offersFoodDelivery){state.actionPostal=null;state.actionWorldObject=null;state.actionPoint=combatTarget.position;state.actionActor=combatTarget;state.actionDoor=null;state.actionFurniture=null;state.actionDungeonFeature=null;showActionMenuAt(clientX,clientY);}else beginTrade(combatTarget);return;}if(combatTarget&&(state.dungeon?.underwater||PlayerCommands.clickAttacks(state.actionMode))&&me?.equippedWeapon!=='none'){beginFollowCommand('attack',combatTarget);return;}if(!state.dungeon&&PlayerCommands.clickAttacks(state.actionMode)&&me?.equippedWeapon!=='none'){const worldObject=vehicleAt(target)||hoverBuildingAtScreen(clientX,clientY)||buildingAt(target);if(worldObject){beginFollowCommand('attack',worldObject,true);return;}}if(PlayerCommands.clickAttacks(state.actionMode)&&hazardWeaponTypes.has(me?.equippedWeapon)){stopTravel();send({type:'throwHazard',x:target.x,y:target.y});return;}if(!state.dungeon&&me?.equippedWeapon==='sword'){const vegetation=choppableAt(target);if(vegetation){const range=2.3;if(Math.hypot(vegetation.position.x-me.position.x,vegetation.position.y-me.position.y)<=range)send({type:'chopVegetation',entityId:vegetation.id});else{state.pendingChop=vegetation;navigateTo(vegetation.position);}return;}}const chest=nearPoint([...state.chests.values()].filter(item=>dynamicVisible(item.position)),target,1.4);if(chest){state.pendingChest=chest;navigateTo(chest.position);return;}const loot=nearPoint([...state.loot.values()].filter(item=>dynamicVisible(item.position)),target,1.4);if(loot){state.followCommand=null;if(me&&Math.hypot(loot.position.x-me.position.x,loot.position.y-me.position.y)<3.8)send({type:'openLoot',lootId:loot.id});else{showToast('Move within 4 meters to collect this treasure.');navigateTo(loot.position);}return;}if(!state.dungeon){const door=doorAtScreen(clientX,clientY)||nearPoint(renderList('door'),target,Math.max(1.4,12/state.scale));if(door){state.followCommand=null;state.actionPoint=door.position;state.actionDoor=door;state.actionActor=null;state.actionFurniture=null;state.actionDungeonFeature=null;showActionMenuAt(clientX,clientY);return;}}navigateTo(target);}
  canvas.addEventListener('click',event=>{if(event.button!==0)return;if(state.suppressClick){state.suppressClick=false;return;}const beamWasActive=state.probulatorBeams.has(state.playerId);stopTravel();const target=toWorld({x:event.clientX,y:event.clientY}),clientX=event.clientX,clientY=event.clientY;clearTimeout(primaryClickTimer);primaryClickTimer=setTimeout(()=>{primaryClickTimer=null;handlePrimaryClick(target,clientX,clientY,beamWasActive);},220);});
  canvas.addEventListener('dblclick',event=>{if(event.button!==0)return;event.preventDefault();clearTimeout(primaryClickTimer);primaryClickTimer=null;openActionChoices(toWorld({x:event.clientX,y:event.clientY}),event.clientX,event.clientY);});
  canvas.addEventListener('wheel',event=>{event.preventDefault();const anchor=toWorld({x:event.clientX,y:event.clientY});state.scale=Math.max(1.2,Math.min(maximumZoomScale,state.scale*Math.exp(-event.deltaY*.001)));const after=toWorld({x:event.clientX,y:event.clientY});state.camera.x+=anchor.x-after.x;state.camera.y+=anchor.y-after.y;state.follow=false;},{passive:false});
  addEventListener('keydown',event=>{if(controlsPaused()||state.dungeon?.retroBattle)return;if(event.target.matches('input,textarea'))return;const key=event.key.toLowerCase();if(key==='escape'){stopTravel('Action cancelled.');return;}if(['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright'].includes(key)){event.preventDefault();if(!state.keys.has(key)){const held=[...state.keys];stopTravel();for(const old of held)state.keys.add(old);}state.keys.add(key);state.follow=true;}});
  addEventListener('keyup',event=>{if(typeof event.key==='string')state.keys.delete(event.key.toLowerCase());});
  ui.center.addEventListener('click',centerOnPlayer);
  document.querySelectorAll('[data-posture]').forEach(button=>button.addEventListener('click',()=>{if(button.dataset.posture!==state.actionMode)setActionMode(button.dataset.posture);}));
  renderActionMode(state.actionMode);
  ui.actionMenu.addEventListener('click',event=>{if(event.target.closest('button:not(:disabled)'))stopTravel();},true);
  addEventListener('blur',()=>stopTravel());
  document.querySelectorAll('[data-mode]').forEach(button=>button.addEventListener('click',()=>{state.followCommand=null;send({type:'setTravelMode',mode:button.dataset.mode==='scuba'&&state.dungeon?.underwater?'walk':button.dataset.mode});}));
  function busStopAt(point){if(state.dungeon||!point)return null;const stops=state.transit?.stops||[],radius=Math.max(1.5,14/state.scale);return nearPoint(stops,point,radius)||stops.find(stop=>{const seat=BusTransit.benchPosition(stop);return Math.hypot(seat.x-point.x,seat.y-point.y)<=radius;})||null;}
  function showBusStopMenu(stop){stopTravel();state.actionPoint=stop.position;state.actionActor=null;state.actionDoor=null;state.actionWorldObject=null;state.actionFurniture=null;state.actionPostal=null;state.actionDungeonFeature=null;showActionMenuAt();}
  function updateBusControls(me){const signature=JSON.stringify([me?.ridingBusId,me?.waitingAtBusStopId,state.transit?.buses?.find(b=>b.id===me?.ridingBusId)?.status]);if(signature===state.busControlsSignature)return;state.busControlsSignature=signature;const panel=$('#busRidePanel'),aboard=!!me?.ridingBusId,waiting=!!me?.waitingAtBusStopId;panel.hidden=!aboard&&!waiting;$('#getOffBusButton').hidden=!aboard;$('#cancelBusWaitButton').hidden=!waiting;if(aboard||waiting){const bus=state.transit?.buses?.find(b=>b.id===me.ridingBusId),stop=state.transit?.stops?.find(s=>s.id===me.waitingAtBusStopId);$('#busRideStatus').textContent=aboard?`Riding ${bus?.routeName||'bus'} · ${bus?.status||'travelling'}`:`Waiting · ${stop?.name||'bus stop'} · ${stop?.direction||''}`;}}
  function drawBusTransit(view){
    for(const bus of state.transit?.buses||[]){if(!pointVisible(bus.position,view))continue;BusTransit.drawBus(ctx,bus,toScreen,state.scale,bus.id===state.players.get(state.playerId)?.ridingBusId);}
  }
  function openBusRoute(stopId=null,routeId=null){
    const transit=state.transit||{routes:[],stops:[]},routes=transit.routes.filter(route=>routeId?route.id===routeId:route.stopIds.includes(stopId));
    if(!routes.length){showToast('No route is available for this stop yet.');return;}
    state.busRouteStopId=stopId;const select=$('#busRouteSelect');select.replaceChildren();for(const route of routes){const option=document.createElement('option');option.value=route.id;option.textContent=route.name;select.append(option);}$('#busRouteDialog').showModal();requestBusRoute();
  }
  function requestBusRoute(){state.busRouteDetail=null;$('#busRouteSummary').textContent='Loading route…';$('#busRouteStops').replaceChildren();const canvas=$('#busRouteCanvas');canvas.getContext('2d').clearRect(0,0,canvas.width,canvas.height);send({type:'requestBusRoute',routeId:$('#busRouteSelect').value});}
  function renderBusRoute(){
    const route=state.busRouteDetail?.route;if(!route||route.id!==$('#busRouteSelect').value)return;const stops=state.busRouteDetail.stops||[],selected=state.transit.stops.find(s=>s.id===state.busRouteStopId);
    const buses=BusTransit.routeBuses(route,state.busRouteDetail.buses||[]);
    $('#busRouteTitle').textContent=route.name+' — bus route';$('#busRouteSummary').textContent=`${stops.length} directional stops · ${buses.length?`${buses.length} bus${buses.length===1?'':'es'} shown in orange · positions refresh every 2 seconds`:'No bus currently reported on this route'} · arrows show travel direction.`;
    BusTransit.drawRoute($('#busRouteCanvas'),route,stops,state.busRouteStopId,buses);
    const list=$('#busRouteStops');list.replaceChildren();for(const stop of stops){const item=document.createElement('li');item.textContent=`${stop.name} · ${stop.direction}${stop.id===state.busRouteStopId?' · selected stop':''}`;list.append(item);}
    $('#routeWaitForBusButton').hidden=!selected;$('#routeWaitForBusButton').disabled=!BusTransit.canWait(state.players.get(state.playerId),selected);
  }
  function waitAtSelectedBusStop(){const stop=state.actionBusStop||busStopAt(state.actionPoint);if(stop){stopTravel();send({type:'waitForBus',stopId:stop.id});ui.actionMenu.hidden=true;}}
  $('#waitForBusButton').addEventListener('click',waitAtSelectedBusStop);
  $('#viewBusRouteButton').addEventListener('click',()=>{const stop=state.actionBusStop||busStopAt(state.actionPoint);if(stop)openBusRoute(stop.id);});
  $('#cancelBusWaitButton').addEventListener('click',()=>send({type:'cancelBusWait'}));
  $('#getOffBusButton').addEventListener('click',()=>{stopTravel();send({type:'getOffBus'});});
  $('#rideViewRouteButton').addEventListener('click',()=>{const me=state.players.get(state.playerId),bus=state.transit?.buses.find(b=>b.id===me?.ridingBusId);openBusRoute(me?.waitingAtBusStopId,bus?.routeId);});
  $('#busRouteSelect').addEventListener('change',requestBusRoute);
  setInterval(()=>{if($('#busRouteDialog').open&&!document.hidden&&!controlsPaused()&&state.socket?.readyState===WebSocket.OPEN&&state.busRouteDetail?.route?.id===$('#busRouteSelect').value)send({type:'requestBusRoute',routeId:$('#busRouteSelect').value,positionsOnly:true});},2000);
  $('#closeBusRouteButton').addEventListener('click',()=>$('#busRouteDialog').close());
  $('#routeWaitForBusButton').addEventListener('click',()=>{if(state.busRouteStopId){stopTravel();send({type:'waitForBus',stopId:state.busRouteStopId});$('#busRouteDialog').close();}});
  function clearActionChoices(){state.actionChoices=null;state.actionChoiceKind=null;state.actionBusStop=null;ui.actionMenu.classList.remove('multi-choice');const list=ui.actionMenu.querySelector('#actionChoices');if(list)list.hidden=true;}
  function actionTargetsAt(x,y){
    const candidates=[],point={x,y},add=(kind,e,extra={})=>candidates.push({kind,id:e.id,entity:e,screen:toScreen(e.position),...extra});
    for(const e of combatTargets()){const p=toScreen(e.position),b=state.dungeon?.underwater?Scuba.actorBounds(e,state.scale):QuestNavigation.actorBounds(e,state.scale);add('actor',e,{bounds:{left:p.x+b.left,right:p.x+b.right,top:p.y+b.top,bottom:p.y+b.bottom}});}
    for(const kind of ['loot','chest'])for(const e of state[kind==='loot'?'loot':'chests'].values())if((!e.expiresAtUtc||Date.parse(e.expiresAtUtc)>Date.now())&&dynamicVisible(e.position))add(kind,e,{radius:state.scale*1.4});
    if(!state.dungeon){
      for(const e of renderList('door')){const p=toScreen(e.position),w=Math.max(9,state.scale*.95),h=Math.max(16,2.05*state.scale*.52);add('building',e,{id:e.properties?.buildingId||e.id,door:e,bounds:{left:p.x-w/2,right:p.x+w/2,top:p.y-h,bottom:p.y+2}});}
      for(const e of renderList('building')){
        const ground=(e.geometry||[]).map(toScreen);if(ground.length<3)continue;
        const height=lod()===0||e.properties?.state==='rubble'?0:Math.min(150,Math.max(1,Number(e.properties?.['building:levels']||e.properties?.levels||2))*3*state.scale*.52),roof=ground.map(p=>({x:p.x,y:p.y-height}));
        add('building',e,{polygons:[ground,roof,...ground.map((p,i)=>[p,ground[(i+1)%ground.length],roof[(i+1)%ground.length],roof[i]])]});
      }
      for(const e of renderList('vehicle'))add('vehicle',e,{radius:Math.max(24,state.scale*2.5)});
      for(const e of renderList('resourceNode').filter(e=>e.properties?.subtype==='postOfficeBox'))add('postal',e);
      for(const e of state.transit?.stops||[]){add('bus',e);add('bus',e,{screen:toScreen(BusTransit.benchPosition(e))});}
      for(const e of state.transit?.buses||[]){if(!dynamicVisible(e.position))continue;const ground=BusTransit.footprint(e).map(toScreen),roof=ground.map(p=>({x:p.x,y:p.y-state.scale*2.7*.72}));add('transitBus',e,{polygons:[ground,roof,...ground.map((p,i)=>[p,ground[(i+1)%4],roof[(i+1)%4],roof[i]])]});}
      if(state.players.get(state.playerId)?.equippedWeapon==='sword')for(const e of [...renderList('tree'),...renderList('bush')])add('vegetation',e,{radius:state.scale*1.1});
    }else{
      if(state.dungeon.isHome)for(const e of state.dungeon.furnishings||[]){const p=toScreen(e.position),size=furnitureSize(e);add('furniture',e,{bounds:{left:p.x-size.w*state.scale/2,right:p.x+size.w*state.scale/2,top:p.y-size.d*state.scale,bottom:p.y+size.d*state.scale/2}});}
      if(dungeonCanExit())add('feature',{id:'exit',position:state.dungeon.exit});
      if(state.dungeon.stairs&&state.dungeon.levelCount>1)add('feature',{id:'stairs',position:state.dungeon.stairs});
    }
    const hits=ActionTargets.collect(candidates,point),treasure=hits.find(c=>c.kind==='loot'||c.kind==='chest');
    return hits.filter(c=>!(c.kind==='loot'||c.kind==='chest')||c===treasure);
  }
  function resolveActionChoice(choice){
    const {kind,id}=choice;
    if(kind==='point')return choice.entity;
    if(kind==='actor')return combatTargets().find(e=>e.id===id);
    if(kind==='loot'||kind==='chest')return state[kind==='loot'?'loot':'chests'].get(id);
    if(kind==='bus')return state.transit?.stops?.find(e=>e.id===id);
    if(kind==='transitBus')return state.transit?.buses?.find(e=>e.id===id);
    if(kind==='furniture')return state.dungeon?.furnishings?.find(e=>e.id===id);
    if(kind==='feature')return state.dungeon&&choice.entity;
    return state.baseById.get(id);
  }
  function applyActionChoice(choice){
    const e=resolveActionChoice(choice);if(!e)return null;
    state.actionChoiceKind=choice.kind;state.actionPoint=e.position;state.actionActor=null;state.actionDoor=null;state.actionWorldObject=null;state.actionFurniture=null;state.actionDungeonFeature=null;state.actionPostal=null;state.actionBusStop=null;
    if(choice.kind==='actor')state.actionActor=e;
    if(choice.kind==='building'){state.actionWorldObject=e;state.actionDoor=choice.door&&state.baseById.get(choice.door.id)||state.doors.get(e.id)||null;}
    if(choice.kind==='vehicle')state.actionWorldObject=e;
    if(choice.kind==='postal')state.actionPostal=e;
    if(choice.kind==='furniture')state.actionFurniture=e;
    if(choice.kind==='feature')state.actionDungeonFeature=e.id;
    if(choice.kind==='bus')state.actionBusStop=e;
    return e;
  }
  function openActionChoices(point,x,y,targets=actionTargetsAt(x,y)){
    const me=state.players.get(state.playerId);stopTravel();if(isSleeping(me)||me?.abduction){showToast('Wait until you can act again.');return;}if(me?.ridingBusId){showToast('Get off the bus before interacting.');return;}
    const list=ui.actionMenu.querySelector('#actionChoices');if(list)delete list.dataset.signature;state.actionChoices={targets:[...targets,{kind:'point',id:'point',entity:{position:point}}],location:me?.locationId};showActionMenuAt();
  }
  function updateActionMenu(){if(state.actionChoices)renderActionChoices();else updateSingleActionMenu();}
  function renderActionChoices(){
    const choices=state.actionChoices;if(!choices)return;
    if(choices.location!==state.players.get(state.playerId)?.locationId){clearActionChoices();ui.actionMenu.hidden=true;return;}
    let list=ui.actionMenu.querySelector('#actionChoices');if(!list){list=document.createElement('div');list.id='actionChoices';ui.actionMenu.append(list);}const fragment=document.createDocumentFragment();list.hidden=false;ui.actionMenu.classList.add('multi-choice');
    const originals=[...ui.actionMenu.querySelectorAll(':scope > button:not(.panel-collapse-button)')],globalIds=new Set(['teleportButton','placeFlagButton','clearTestCharactersButton','collectDirtyWaterButton','openHomeUpgradesButton','buildGardenButton','drinkHoseButton']);
    for(const choice of choices.targets){
      const e=applyActionChoice(choice);if(!e)continue;updateSingleActionMenu();
      const group=document.createElement('section'),heading=document.createElement('strong');group.className='action-choice-group';
      const name=e.name||e.properties?.name||e.properties?.displayName||e.properties?.label;
      const label={loot:e.dropKind==='tombstone'?'Tombstone':'Treasure',chest:'Treasure chest',building:'Building',actor:title(e.kind||'Character'),vehicle:'Vehicle',postal:'Postal box',bus:'Bus stop',transitBus:'Bus',feature:title(e.id),furniture:title(e.properties?.objectType||'Furniture'),vegetation:title(e.kind),point:'This location'}[choice.kind];
      heading.textContent=`${name?`${name} · `:''}${label}`;group.append(heading);
      const addButton=(text,run,disabled=false,reason='')=>{const button=document.createElement('button');button.type='button';button.textContent=text;button.disabled=disabled;button.title=reason;button.addEventListener('click',()=>{const current=applyActionChoice(choice);if(!current){showToast('That target is no longer available.');renderActionChoices();return;}run(current);});group.append(button);};
      if(choice.kind==='transitBus'){const me=state.players.get(state.playerId);addButton(`Attack bus · ${Math.ceil(e.healthHearts)} hearts`,current=>{clearActionChoices();beginFollowCommand('attack',current);},e.healthHearts<=0||(me?.equippedWeapon||'none')==='none','Equip a weapon and move into range.');addButton('Board parked bus / start service',current=>beginBusBoarding(current),!BusTransit.canApproachBoarding(me,e)||isSleeping(me),'Board a parked, out-of-service bus. For buses already running, wait at a bus stop.');addButton('View route',current=>{clearActionChoices();ui.actionMenu.hidden=true;openBusRoute(null,current.routeId);});}
      if(choice.kind==='loot'||choice.kind==='chest')addButton('Pick up treasure',current=>{clearActionChoices();ui.actionMenu.hidden=true;const me=state.players.get(state.playerId);if(Math.hypot(current.position.x-me.position.x,current.position.y-me.position.y)<3.7)send(choice.kind==='loot'?{type:'openLoot',lootId:current.id}:{type:'openChest',chestId:current.id});else{if(choice.kind==='loot')state.pendingLoot=current;else state.pendingChest=current;navigateTo(current.position);}});
      else if(choice.kind==='vegetation')addButton('Chop',current=>{clearActionChoices();ui.actionMenu.hidden=true;state.pendingChop=current;navigateTo(current.position);});
      else for(const original of originals){
        const global=globalIds.has(original.id)||!!original.dataset.testCharacter;if(original.hidden||global!==(choice.kind==='point'))continue;
        addButton(original.textContent,()=>{updateSingleActionMenu();if(original.disabled||original.hidden){showToast(original.title||'This action is no longer available.');renderActionChoices();return;}state.actionChoices=null;original.click();clearActionChoices();},original.disabled,original.title);
      }
      if(choice.kind==='point')addButton('Move here',current=>{clearActionChoices();ui.actionMenu.hidden=true;navigateTo(current.position);});
      if(group.children.length>1)fragment.append(group);
    }
    const signature=JSON.stringify([choices.targets.map(c=>[c.kind,c.id]),[...fragment.querySelectorAll('strong,button')].map(e=>[e.textContent,e.disabled,e.title])]);
    if(list.dataset.signature!==signature){const focus=document.activeElement,listHadFocus=list.contains(focus),label=focus?.textContent;list.replaceChildren(fragment);list.dataset.signature=signature;if(listHadFocus)[...list.querySelectorAll('button')].find(b=>b.textContent===label&&!b.disabled)?.focus({preventScroll:true});}
  }
  function updateSingleActionMenu(){
    if(typeof gardenUI!=='undefined')gardenUI.updateButton();
    const busStop=state.actionChoiceKind?(state.actionChoiceKind==='bus'?state.actionBusStop:null):busStopAt(state.actionPoint);$('#waitForBusButton').hidden=!busStop;$('#viewBusRouteButton').hidden=!busStop;
    if(busStop){const me=state.players.get(state.playerId);$('#waitForBusButton').disabled=!BusTransit.canWait(me,busStop);$('#waitForBusButton').title='Move within 3 meters to sit on the bench and wait.';ui.actionMenu.querySelectorAll(':scope > button:not(.panel-collapse-button)').forEach(button=>{if(!['waitForBusButton','viewBusRouteButton','buildGardenButton','drinkHoseButton'].includes(button.id))button.hidden=true;});ui.noActions.hidden=true;return;}
    const actor=state.actionActor,door=state.actionDoor,furniture=state.actionFurniture,feature=state.actionDungeonFeature,worldObject=state.actionWorldObject||(state.actionDoor?state.baseById.get(state.actionDoor.properties?.buildingId):null),postal=state.actionPostal,me=state.players.get(state.playerId),canTeleport=!!me?.godMode&&state.actionPoint&&!state.dungeon,currentBase=state.privateState?.base?.buildingId,buildingId=door?.properties?.buildingId,building=buildingId?state.baseById.get(buildingId):null,publicClaim=buildingId?state.publicBases?.get(buildingId):null,storeCategory=storeCategoryForBuilding(building),difficulty=buildingDifficulty(building),price=buildingPriceCents(building),alreadyOwned=!!door&&buildingId===currentBase,locked=!!door&&doorIsLocked(door),affordable=!!me?.godMode||(me?.walletCents??0)>=price,closeEnough=!!door&&!!me&&Math.hypot(me.position.x-door.position.x,me.position.y-door.position.y)<=6,level=Number(state.dungeon?.level||1),levelCount=Number(state.dungeon?.levelCount||1),flagCount=[...state.reality.values()].filter(entity=>entity.properties?.objectType==='personalFlag'&&entity.properties?.owner===state.playerId).length,availableFlags=(state.privateState?.inventory?.items||[]).find(item=>item.itemType==='personalFlag')?.quantity||0,flagCloseEnough=!!me&&!!state.actionPoint&&Math.hypot(me.position.x-state.actionPoint.x,me.position.y-state.actionPoint.y)<=6;
    for(const button of document.querySelectorAll('[data-test-character]'))button.hidden=!canTeleport;
    $('#clearTestCharactersButton').hidden=!me?.godMode;
    ui.enterBuilding.hidden=!door;ui.enterBuilding.disabled=locked;ui.enterBuilding.textContent=alreadyOwned?'Enter Home':storeCategory?'Enter store':difficulty>50?`Enter Stronghold — Difficulty ${difficulty}`:`Enter building — Difficulty ${difficulty}`;ui.enterBuilding.title=locked?lockedDoorMessage(door):storeHoursLabel(buildingId);
    const ownsLockPicks=!!me?.godMode||(state.privateState?.inventory?.items||[]).some(item=>item.itemType==='lockPickSet'&&item.quantity>0);ui.pickLock.hidden=!door||!locked;ui.pickLock.disabled=!ownsLockPicks||!closeEnough;ui.pickLock.title=!ownsLockPicks?'Buy a lock pick set first.':!closeEnough?'Move within 3 meters of the door.':'15% success chance; a witnessed success has a 10% police-call chance.';
    const tauntButton=$('#tauntButton'),canTaunt=actor&&(actor.kind==='npc'||state.players.has(actor.id));tauntButton.hidden=!canTaunt;tauntButton.textContent=actor?`Taunt ${actor.name}`:'Taunt';tauntButton.disabled=!canTaunt||!me||Math.hypot(me.position.x-actor.position.x,me.position.y-actor.position.y)>10;tauntButton.title='Taunt within 10 meters. Adds 0.10 hostility; 3-second cooldown.';
    const targetIsPlayer=!!actor&&state.players.has(actor.id),pvpAllowed=!targetIsPlayer||state.snapshot?.reality?.pvpEnabled!==false,hasWeapon=(me?.equippedWeapon||'none')!=='none';
    const actorQuest=actor?questForActor(actor.id):null;ui.goUpstairs.hidden=feature!=='stairs'||level<=1;ui.goDownstairs.hidden=feature!=='stairs'||level>=levelCount;ui.leaveDungeon.hidden=feature!=='exit';ui.leaveDungeon.textContent=state.dungeon?.isHome?'Leave Home':level===levelCount&&levelCount>1?'Leave dungeon':'Exit to map';ui.teleport.hidden=!canTeleport;ui.trade.hidden=!(actor?.isMerchant);ui.quest.hidden=!(actor?.isQuestGiver||actorQuest&&actorQuest.kind!=='missingPet');ui.quest.textContent=actorQuest?'Discuss quest':actor?.offersFoodDelivery?'Take a delivery job':'Ask about quest';ui.capturePet.hidden=!(actorQuest?.kind==='missingPet'&&actorQuest.status==='active');ui.stalk.hidden=!actor;const attackableObject=worldObject&&['building','vehicle'].includes(worldObject.kind);ui.continuousAttack.hidden=!actor&&!attackableObject;ui.continuousAttack.disabled=(!actor&&!attackableObject)||!hasWeapon||!pvpAllowed||PlayerCommands.defeated(actor||worldObject)||!!(attackableObject&&state.publicBases?.has(worldObject.id));ui.continuousAttack.textContent=actor?`Attack ${actor.name}`:worldObject?.kind==='building'?'Attack building':'Attack vehicle';ui.continuousAttack.title=attackableObject&&state.publicBases?.has(worldObject.id)?'Claimed Homes are protected from damage.':!hasWeapon?'Equip a weapon first.':!pvpAllowed?'Player-versus-player combat is disabled on this server.':'Attack until a new command, target destruction, or target death.';ui.stalk.textContent=actor?`Stalk ${actor.name}`:'Stalk';
    ui.purchaseBase.hidden=!door;ui.purchaseBase.disabled=!door||alreadyOwned||!!publicClaim||!!storeCategory||!affordable||!closeEnough;ui.purchaseBase.textContent=alreadyOwned?'Current Home':publicClaim?`${publicClaim.ownerName}'s Home — not for sale`:storeCategory?`${title(storeCategory)} store — not for sale`:`Purchase as Home — $${(price/100).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}`;ui.purchaseBase.title=alreadyOwned?'This is already your Home.':publicClaim?'Claimed Homes cannot be purchased or altered.':storeCategory?'Stores are commercial properties and cannot be purchased as a Home.':!closeEnough?'Move within 6 meters to purchase this building.':!affordable?`You need $${(price/100).toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:2})}.`:'Purchase this building as your account Home.';
    ui.placeFlag.hidden=!!state.dungeon;ui.placeFlag.disabled=!!state.dungeon||availableFlags<1||flagCount>=5||!flagCloseEnough;ui.placeFlag.textContent=flagCount>=5?'All 5 flags placed':`Place flag (${availableFlags} available)`;ui.placeFlag.title=flagCount>=5?'You already have five placed flags.':availableFlags<1?'You have no personal flags available.':!flagCloseEnough?'Move within 6 meters of this point.':'Place a named flag visible while you are online.';
    const isVehicle=worldObject?.kind==='vehicle',hasSpray=!!me?.godMode||(state.privateState?.inventory?.items||[]).some(item=>item.itemType==='sprayPaint'&&item.quantity>0),carClose=!!worldObject&&!!me&&Math.hypot(me.position.x-worldObject.position.x,me.position.y-worldObject.position.y)<=4;ui.sprayPaintVehicle.hidden=!isVehicle;ui.sprayPaintVehicle.disabled=!hasSpray||!carClose;ui.sprayPaintVehicle.title=!hasSpray?'You need a can of spray paint.':!carClose?'Move within 4 meters of the parked car.':'This is a crime if a human NPC sees you.';
    const postalClose=!!postal&&!!me&&Math.hypot(me.position.x-postal.position.x,me.position.y-postal.position.y)<=4;ui.openPostalBox.hidden=!postal;ui.openPostalBox.disabled=!postalClose;ui.openPostalBox.title=postalClose?'Send carried items directly to your Home storage.':'Move within 4 meters of the postal drop box.';
    $('#openHomeUpgradesButton').hidden=!state.privateState?.canEditHome;
    $('#collectDirtyWaterButton').hidden=!state.dungeon?.underwater&&!['shallowWater','deepWater'].includes(me?.terrain);
    const type=furniture?.properties?.objectType,energy=energyDrinkPhase(me),probed=probedPhase(me);ui.useFurniture.hidden=!['bed','wardrobe','storageChest','kitchenSink','craftingTable','stove','garageWorkbench','weaponsBench'].includes(type);ui.openHomeShop.hidden=type!=='homeShopCounter';ui.openHomeShop.textContent=state.privateState?.canEditHome?'Manage Home shop':'Browse Home shop';ui.useFurniture.textContent=type==='bed'&&energy?.phase==='boost'?`Too energized to sleep · ${countdown(energy.until)}`:type==='bed'?'Take a nap':type==='wardrobe'?'Change characters':type==='storageChest'?'Open storage chest':type==='kitchenSink'?'Use kitchen sink':['craftingTable','stove','garageWorkbench','weaponsBench'].includes(type)?'Review recipes & craft':'Use item';ui.useFurniture.disabled=type==='bed'&&energy?.phase==='boost';ui.useFurniture.title=ui.useFurniture.disabled?'Sleep becomes available when the energy-drink boost ends.':type==='bed'&&probed?'Take a nap to clear Probed.':type==='bed'&&energy?.phase==='crash'?'Take a nap to clear the energy-drink crash.':'';ui.moveFurniture.hidden=!furniture||!state.privateState?.canEditHome;ui.rotateFurniture.hidden=!furniture||!state.privateState?.canEditHome;ui.storeFurniture.hidden=!furniture||!state.privateState?.canEditHome;ui.storeFurniture.disabled=furniture?.properties?.builtIn==='true'&&['fireplace','storageChest','kitchenSink'].includes(type);ui.noActions.hidden=!!(door||canTeleport||actor||furniture||feature||worldObject||postal||!state.dungeon);
  }
  function toggleGodMode(){const player=state.players.get(state.playerId);if(!player||ui.god.disabled)return;const enabled=!player.godMode;state.godTogglePending=enabled;ui.god.disabled=true;ui.serverConfigButton.disabled=true;ui.performancePanel.hidden=true;ui.rebuild.disabled=true;send({type:'setGodMode',enabled});showToast(enabled?'Enabling God Mode…':'Disabling God Mode…');updateActionMenu();clearTimeout(state.godToggleTimer);state.godToggleTimer=setTimeout(()=>{state.godTogglePending=null;syncGodControls(state.players.get(state.playerId));},3000);}
  ui.god.addEventListener('click',toggleGodMode);
  document.querySelectorAll('[data-world-event]').forEach(button=>button.addEventListener('click',()=>{const player=state.players.get(state.playerId);if(!player?.godMode){showToast('Enable God Mode before triggering world events.');return;}showToast('Starting Server Vote…');send({type:'startServerVote'});}));
  ui.equipmentGloves.addEventListener('click',()=>send({type:'setEquipment',slot:'gloves',itemType:null}));ui.equipmentHat.addEventListener('click',()=>send({type:'setEquipment',slot:'hat',itemType:null}));
  ui.equipmentShirt.addEventListener('click',()=>send({type:'setEquipment',slot:'shirt',itemType:null}));
  ui.equipmentPants.addEventListener('click',()=>send({type:'setEquipment',slot:'pants',itemType:null}));
  ui.equipmentOffhand.addEventListener('click',()=>send({type:'setEquipment',slot:'offhand',itemType:null}));
  ui.equipmentShoes.addEventListener('click',()=>send({type:'setEquipment',slot:'shoes',itemType:null}));
  ui.equipmentWeapon.addEventListener('click',()=>{const me=state.players.get(state.playerId);if(me?.travelMode==='ufo'){showToast('The UFO weapon slot is fixed to its Probulator.');return;}if(me?.equippedWeapon==='ar15'){send({type:'setWeaponMode',mode:me.ar15FireMode==='burst'?'single':'burst'});return;}send({type:'setEquipment',slot:'weapon',itemType:null});});
  ui.equipmentWeapon.addEventListener('dragover',event=>{event.preventDefault();event.dataTransfer.dropEffect='move';ui.equipmentWeapon.classList.add('drag-ready');});
  ui.equipmentWeapon.addEventListener('dragleave',()=>ui.equipmentWeapon.classList.remove('drag-ready'));
  ui.equipmentWeapon.addEventListener('drop',event=>{event.preventDefault();ui.equipmentWeapon.classList.remove('drag-ready');if(state.players.get(state.playerId)?.travelMode==='ufo'){showToast('The UFO weapon slot is fixed to its Probulator.');return;}const weapon=event.dataTransfer.getData('text/weapon')||event.dataTransfer.getData('text/plain');if(weaponTypes.has(weapon))send({type:'setEquipment',slot:'weapon',itemType:weapon});});
  for(const [slot,button]of Object.entries({hat:ui.equipmentHat,gloves:ui.equipmentGloves,shirt:ui.equipmentShirt,pants:ui.equipmentPants,shoes:ui.equipmentShoes,offhand:ui.equipmentOffhand})){button.addEventListener('dragover',event=>{const transfer=event.dataTransfer.getData('text/equipment');if(!transfer||transfer.startsWith(`${slot}:`)){event.preventDefault();event.dataTransfer.dropEffect='move';button.classList.add('drag-ready');}});button.addEventListener('dragleave',()=>button.classList.remove('drag-ready'));button.addEventListener('drop',event=>{event.preventDefault();button.classList.remove('drag-ready');const [sourceSlot,itemType]=(event.dataTransfer.getData('text/equipment')||'').split(':');if(sourceSlot===slot&&equipmentSlotByItem[itemType]===slot)send({type:'setEquipment',slot,itemType});});}
  document.querySelectorAll('[data-inventory-tab]').forEach(button=>button.addEventListener('click',()=>{state.inventoryTab=button.dataset.inventoryTab;renderInventory(state.privateState?.inventory);}));
  document.querySelectorAll('[data-home-storage-tab]').forEach(button=>button.addEventListener('click',()=>{state.homeStorageTab=button.dataset.homeStorageTab;renderHomeItemStorage();}));
  ui.serverConfigWindow.querySelectorAll('[data-server-config-tab]').forEach(button=>button.addEventListener('click',()=>setServerConfigTab(button.dataset.serverConfigTab)));
  function openPanelPopup(panel,opener){
    const wasHidden=panel.hidden;panel.hidden=false;
    const width=Math.min(panel.id==='inventoryPanel'?680:420,innerWidth-16);
    const saved=savedPanelLayout(panel);
    const layout=saved?.floating?saved:{width,left:Math.max(8,(viewportWidth()-width)/2),top:Math.max(8,(innerHeight-Math.min(panel.scrollHeight,innerHeight-32))/2)};
    if(wasHidden)floatPanel(panel,layout,false);else panel.style.zIndex=String(++floatingPanelZ);
    opener.setAttribute('aria-expanded','true');
    (panel.id==='chatPanel'?ui.chatInput:panel.querySelector('.popup-close')).focus({preventScroll:true});
  }
  function closePanelPopup(panel,opener){
    savePanelLayout(panel);panel.hidden=true;opener.setAttribute('aria-expanded','false');opener.focus({preventScroll:true});
  }
  function initializePanelPopups(){
    for(const [panelId,openId,closeId] of [['inventoryPanel','openInventoryButton','closeInventoryButton'],['chatPanel','openChatButton','closeChatButton']]){
      const panel=$('#'+panelId),opener=$('#'+openId);
      opener.addEventListener('click',()=>openPanelPopup(panel,opener));
      $('#'+closeId).addEventListener('click',()=>closePanelPopup(panel,opener));
      panel.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();event.stopPropagation();closePanelPopup(panel,opener);}});
      panel.addEventListener('pointerdown',()=>{panel.style.zIndex=String(++floatingPanelZ);});
    }
  }
  ui.openHelpWindow.addEventListener('click',openHelpWindow);
  ui.serverConfigButton.addEventListener('click',()=>{if(!state.players.get(state.playerId)?.godMode){showToast('God Mode must be enabled to edit server configuration.');return;}openServerConfigWindow();});
  ui.serverConfigClose.addEventListener('click',closeServerConfigWindow);
  ui.enterBuilding.addEventListener('click',()=>{if(!state.actionDoor)return;if(doorIsLocked(state.actionDoor)){showToast(lockedDoorMessage(state.actionDoor));return;}const me=state.players.get(state.playerId);if(me&&Math.hypot(me.position.x-state.actionDoor.position.x,me.position.y-state.actionDoor.position.y)<=5.5){state.pendingDoor=null;send({type:'enterDungeon',doorId:state.actionDoor.id});}else{state.pendingDoor=state.actionDoor;const angle=prop(state.actionDoor,'facingDegrees',0)*Math.PI/180;navigateTo({x:state.actionDoor.position.x+Math.cos(angle)*3.2,y:state.actionDoor.position.y+Math.sin(angle)*3.2});}ui.actionMenu.hidden=true;});
  ui.pickLock.addEventListener('click',()=>{if(state.actionDoor&&!ui.pickLock.disabled)send({type:'pickLock',doorId:state.actionDoor.id});ui.actionMenu.hidden=true;});
  function beginDungeonAction(action){if(!state.dungeon)return;const target=action==='exit'?state.dungeon.exit:state.dungeon.stairs;if(!target)return;const me=state.players.get(state.playerId),run=()=>action==='exit'?send({type:'exitDungeon'}):send({type:'changeDungeonLevel',direction:action==='up'?-1:1});if(me&&Math.hypot(me.position.x-target.x,me.position.y-target.y)<2.7)run();else{state.pendingDungeonAction={action,target};navigateTo(target);}ui.actionMenu.hidden=true;}
  ui.goUpstairs.addEventListener('click',()=>beginDungeonAction('up'));
  ui.goDownstairs.addEventListener('click',()=>beginDungeonAction('down'));
  ui.leaveDungeon.addEventListener('click',()=>beginDungeonAction('exit'));
  ui.safeExit.addEventListener('click',()=>{if(!state.dungeon?.isHome&&!state.dungeon?.isStore&&!state.dungeon?.retroBattle&&!state.dungeon?.underwater)return;state.followCommand=null;state.pendingDungeonAction=null;state.path=[];state.target=null;ui.safeExit.disabled=true;ui.safeExit.textContent='Leaving…';send({type:'exitDungeon'});});
  for(const button of document.querySelectorAll('[data-test-character]'))button.addEventListener('click',()=>{const me=state.players.get(state.playerId);if(!me?.godMode||state.dungeon||!state.actionPoint)return;send({type:'placeTestCharacter',kind:button.dataset.testCharacter,x:state.actionPoint.x,y:state.actionPoint.y});});
  $('#clearTestCharactersButton').addEventListener('click',()=>{if(state.players.get(state.playerId)?.godMode)send({type:'clearTestCharacters'});});
  ui.teleport.addEventListener('click',()=>{if(!state.players.get(state.playerId)?.godMode||!state.actionPoint){showToast('Wait for God Mode to finish enabling.');return;}requestTeleport(state.actionDoor?.position||state.actionPoint);ui.actionMenu.hidden=true;});
  ui.placeFlag.addEventListener('click',()=>{if(ui.placeFlag.disabled||!state.actionPoint)return;const label=prompt('What should this flag say?','');if(label===null)return;const cleaned=label.replace(/[\u0000-\u001f\u007f]/g,'').trim();if(!cleaned){showToast('Enter a name for the flag.');return;}if(cleaned.length>60){showToast('Flag names are limited to 60 characters.');return;}send({type:'placeFlag',x:state.actionPoint.x,y:state.actionPoint.y,label:cleaned});ui.placeFlag.disabled=true;ui.placeFlag.textContent='Placing flag…';});
  ui.sprayPaintVehicle.addEventListener('click',()=>{if(state.actionWorldObject&&!ui.sprayPaintVehicle.disabled)send({type:'sprayPaintVehicle',entityId:state.actionWorldObject.id});ui.actionMenu.hidden=true;});
  ui.openPostalBox.addEventListener('click',()=>{if(!state.actionPostal||ui.openPostalBox.disabled)return;renderPostalItems();ui.postalWindow.hidden=false;ui.actionMenu.hidden=true;});
  ui.postalClose.addEventListener('click',()=>ui.postalWindow.hidden=true);
  ui.trade.addEventListener('click',()=>{if(state.actionActor)beginTrade(state.actionActor);ui.actionMenu.hidden=true;});
  ui.quest.addEventListener('click',()=>{if(state.actionActor)send({type:'requestQuest',actorId:state.actionActor.id});ui.actionMenu.hidden=true;});
  ui.capturePet.addEventListener('click',()=>{if(state.actionActor)send({type:'captureQuestPet',actorId:state.actionActor.id});ui.actionMenu.hidden=true;});
  $('#tauntButton').addEventListener('click',()=>{if(state.actionActor&&!$('#tauntButton').disabled){send({type:'taunt',targetId:state.actionActor.id});ui.actionMenu.hidden=true;}});
  ui.stalk.addEventListener('click',()=>beginFollowCommand('stalk',state.actionActor));
  ui.continuousAttack.addEventListener('click',()=>{if(!ui.continuousAttack.disabled)beginFollowCommand('attack',state.actionActor||state.actionWorldObject||state.baseById.get(state.actionDoor?.properties?.buildingId),!state.actionActor);});
  ui.purchaseBase.addEventListener('click',()=>{state.followCommand=null;if(state.actionDoor&&!ui.purchaseBase.disabled)send({type:'purchaseBase',doorId:state.actionDoor.id});});
  ui.useFurniture.addEventListener('click',()=>{state.followCommand=null;const item=state.actionFurniture,type=item?.properties?.objectType;if(!item)return;if(type==='bed')send({type:'restAtBed',bedId:item.id});if(type==='wardrobe'){state.characterManagerOpen=true;ui.characterPanel.hidden=false;loadCharacters(true);}if(type==='kitchenSink')gardenUI.sink(item);if(['craftingTable','stove','garageWorkbench','weaponsBench'].includes(type))send({type:'requestCrafting',furnitureId:item.id});if(type==='storageChest'){state.storageChestId=item.id;send({type:'openHomeStorage',chestId:item.id});}ui.actionMenu.hidden=true;});
  ui.openHomeShop.addEventListener('click',()=>{if(state.actionFurniture)send({type:'requestHomeShop',furnitureId:state.actionFurniture.id});ui.actionMenu.hidden=true;});
  function transferHomeMoney(toStorage){const amountCents=Math.round((Number(ui.homeMoneyAmount.value)||0)*100);if(amountCents<1){showToast('Enter at least $0.01.');return;}send({type:'transferHomeMoney',chestId:state.storageChestId,amountCents,toStorage});}
  ui.depositHomeMoney.addEventListener('click',()=>transferHomeMoney(true));
  ui.withdrawHomeMoney.addEventListener('click',()=>transferHomeMoney(false));
  ui.moveFurniture.addEventListener('click',()=>{state.followCommand=null;if(!state.actionFurniture)return;state.movingFurniture=state.actionFurniture;state.furniturePreview={x:state.actionFurniture.position.x,y:state.actionFurniture.position.y};ui.actionMenu.hidden=true;showToast('Drag the item to an open floor position.');});
  ui.rotateFurniture.addEventListener('click',()=>{state.followCommand=null;if(state.actionFurniture)send({type:'rotateFurniture',furnitureId:state.actionFurniture.id});ui.actionMenu.hidden=true;});
  ui.storeFurniture.addEventListener('click',()=>{state.followCommand=null;if(state.actionFurniture&&!ui.storeFurniture.disabled)send({type:'storeFurniture',furnitureId:state.actionFurniture.id});ui.actionMenu.hidden=true;});
  ui.closeCharacters.addEventListener('click',()=>{state.characterManagerOpen=false;ui.characterPanel.hidden=true;});
  $('#closeHomeStorageButton').addEventListener('click',()=>{ui.homeStoragePanel.hidden=true;state.storageChestOpen=false;});
  $('#openHomeStorageButton').addEventListener('click',()=>{const chest=state.dungeon?.furnishings?.find(e=>e.properties?.objectType==='storageChest');if(chest){state.storageChestId=chest.id;send({type:'openHomeStorage',chestId:chest.id});}else showInteractionWindow(ui.homeStoragePanel,960);});
  setInterval(refreshNpcConversations,5000);
  ui.tradeCancel.addEventListener('click',closeTradeWindow);
  ui.homeShopClose.addEventListener('click',()=>ui.homeShopWindow.hidden=true);
  ui.tradeConfirm.addEventListener('click',()=>{if(!state.tradeQuote)return;const lines=kind=>[...ui.tradeOffers.querySelectorAll(`input[data-trade-kind="${kind}"]`)].map(input=>({itemType:input.dataset.item,quantity:Number(input.value)||0})).filter(x=>x.quantity>0),purchases=lines('buy'),sales=lines('sell');if(purchases.length&&sales.length){showToast('Buy and sell in separate transactions.');return;}send({type:'confirmTrade',merchantId:state.tradeQuote.merchantId,purchases,sales});});
  ui.treasureClose.addEventListener('click',closeTreasure);
  ui.treasureTake.addEventListener('click',takeTreasureSelection);
  $('#treasureTakeAll').addEventListener('click',takeAllTreasure);
  ui.treasureWindow.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();event.stopPropagation();closeTreasure();}});
  ui.questClose.addEventListener('click',closeQuestWindow);
  $('#travelProbulator').addEventListener('click',()=>{toggleOwnProbulator(state.players.get(state.playerId));});
  ui.questAccept.addEventListener('click',()=>{if(state.questInteraction)send({type:'acceptQuest',questId:state.questInteraction.quest.id});});
  ui.questComplete.addEventListener('click',()=>{if(state.questInteraction)send({type:'completeQuest',questId:state.questInteraction.quest.id,actorId:state.questInteraction.interactionActorId});});
  ui.questAbandon.addEventListener('click',()=>{if(state.questInteraction&&confirm('Abandon this quest?'))send({type:'abandonQuest',questId:state.questInteraction.quest.id});});
  initializeMiniMapFilters();
  Casino.init({state,send,stopTravel,showToast});
  Inversions.init({state,send,toScreen,navigateTo,createQuestMapToggle});
  const gardenUI=Gardens.create({state,send,stopTravel,showToast,navigateTo,beginFollowCommand,ui,project:toScreen,ring:drawWorldRing});
  document.querySelectorAll('[data-world-event]').forEach(b=>{b.hidden=b.dataset.worldEvent!=='vote';b.textContent='Trigger Server Vote · 1 minute';});
  ui.chatForm.addEventListener('submit',event=>{event.preventDefault();const message=ui.chatInput.value.trim();if(!message)return;send({type:'say',message});ui.chatInput.value='';ui.chatInput.focus();});
  ui.serverConfigWindow.querySelector('#refreshRealityInfo').addEventListener('click',refreshRealityInfo);
  ui.serverConfigWindow.querySelector('#rebuildMode').addEventListener('change',event=>{ui.serverConfigWindow.querySelector('#rebuildDescription').textContent=event.target.value==='fresh'?'Downloads fresh geography for the starting block, discards all saved map blocks, and applies current generation rules, including driveways. Other blocks regenerate as explored. Requires internet access.':'Resets gameplay changes and reloads the starting block from its saved map. Saved geography remains available.';});
  function confirmRealityRebuild(fromScratch) {
    return new Promise(resolve=>{
      const dialog=document.createElement('dialog');
      dialog.className='rebuild-confirmation';
      dialog.setAttribute('aria-labelledby','rebuildConfirmationTitle');
      dialog.setAttribute('aria-describedby','rebuildConfirmationDescription');
      dialog.innerHTML='<h2 id="rebuildConfirmationTitle">Rebuild Reality?</h2><p id="rebuildConfirmationDescription"></p><p>World changes, gardens, dungeons, chests, and relationships will be reset. Accounts, characters, inventories, Home ownership, and purchased maps will remain. Everyone connected will reload and reconnect when the rebuild finishes.</p><form method="dialog"><button value="cancel" autofocus>Cancel</button><button value="rebuild" class="danger-action">Confirm rebuild</button></form>';
      dialog.querySelector('#rebuildConfirmationDescription').textContent=fromScratch?'All saved map blocks will be discarded and generated again using the current rules. Internet access is required.':'Gameplay will reset using the saved map blocks. Existing map geography will remain.';
      dialog.addEventListener('close',()=>{const confirmed=dialog.returnValue==='rebuild';dialog.remove();resolve(confirmed);},{once:true});
      document.body.append(dialog);dialog.showModal();
    });
  }
  async function requestRealityRebuild() {
    if(state.rebuildPending||state.rebuildConfirming)return;
    if(!state.players.get(state.playerId)?.godMode){showToast('Wait for God Mode to finish enabling.');return;}
    const fromScratch=ui.serverConfigWindow.querySelector('#rebuildMode').value==='fresh';
    state.rebuildConfirming=true;
    let confirmed;
    try {confirmed=await confirmRealityRebuild(fromScratch);} finally {state.rebuildConfirming=false;}
    if(!confirmed)return;
    if(!state.players.get(state.playerId)?.godMode||controlsPaused()||state.socket?.readyState!==WebSocket.OPEN){showToast('Wait for the connection and God Mode to be ready, then try again.');return;}
    state.rebuildPending=true;ui.rebuild.disabled=true;ui.rebuild.textContent='Rebuilding world…';
    setWorldTask('Rebuilding reality…');showToast(fromScratch?'Rebuilding from fresh geography…':'Resetting the world using saved map blocks…');
    send({type:'rebuildArea',godMode:true,fromScratch});
  }
  function reconnectAfterRebuild() {
    if(state.rebuildReloading)return;
    state.rebuildReloading=true;state.rebuildPending=false;
    setConnectionTask('Reality rebuilt — reconnecting…');
    // A new page discards stale map, dungeon, and action state and reconnects with the saved character.
    location.reload();
  }
  ui.rebuild.addEventListener('click',requestRealityRebuild);
  ui.accountForm.addEventListener('submit',async event=>{event.preventDefault();ui.accountError.textContent='';const username=ui.accountUsername.value.trim(),password=ui.accountPassword.value;try{const response=await fetch('/api/account/setup',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({username,password})});const body=await response.json();if(!response.ok)throw new Error(body.message||'Unable to create player.');ui.accountSetup.hidden=true;connect();}catch(error){ui.accountError.textContent=error.message;}});
  async function configureReality(latitude,longitude){ui.realitySetupError.textContent='Building the initial world…';const response=await fetch('/api/reality/setup',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({latitude,longitude})});const body=await response.json();if(!response.ok)throw new Error(body.message||'Unable to initialize this reality.');ui.realitySetup.hidden=true;ui.realitySetupError.textContent='';await bootstrap();}
  ui.realitySetupForm.addEventListener('submit',async event=>{event.preventDefault();try{await configureReality(Number(ui.realityLatitude.value),Number(ui.realityLongitude.value));}catch(error){ui.realitySetupError.textContent=error.message;}});
  ui.useServerGps.addEventListener('click',()=>{if(!isSecureContext||!navigator.geolocation){ui.realitySetupError.textContent='Browser location requires HTTPS or localhost. Enter coordinates below.';return;}ui.useServerGps.disabled=true;ui.realitySetupError.textContent='Requesting this device’s location…';navigator.geolocation.getCurrentPosition(async position=>{ui.useServerGps.disabled=false;ui.realityLatitude.value=position.coords.latitude.toFixed(6);ui.realityLongitude.value=position.coords.longitude.toFixed(6);try{await configureReality(position.coords.latitude,position.coords.longitude);}catch(error){ui.realitySetupError.textContent=error.message;}},error=>{ui.useServerGps.disabled=false;ui.realitySetupError.textContent=error.code===1?'Location permission was denied. Enter coordinates below.':'Location is unavailable. Enter coordinates below.';},{enableHighAccuracy:true,timeout:15000,maximumAge:300000});});
  ui.characterForm.addEventListener('submit',async event=>{event.preventDefault();const response=await fetch('/api/account/characters',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({name:ui.characterName.value.trim()})});if(response.ok){ui.characterName.value='';loadCharacters(true);}else showToast((await response.json()).message);});
  document.fonts?.addEventListener('loadingdone',()=>{state.speechFontRevision=(state.speechFontRevision||0)+1;});
  addEventListener('pagehide',()=>{if(serverConfigPopup&&!serverConfigPopup.closed)serverConfigPopup.close();if(helpPopup&&!helpPopup.closed)helpPopup.close();});addEventListener('resize',resize);initializeRightRail();resize();initializeCollapsiblePanels();initializeDraggablePanels();initializePanelPopups();initializeTravelButtons();initializeLooting();bootstrap();refreshServerDiagnostics();setInterval(refreshServerDiagnostics,4000);setInterval(probeServer,500);setInterval(updateServerTimePreview,1000);setInterval(updateDeliveryTimers,1000);requestAnimationFrame(render);requestAnimationFrame(movementLoop);
})();
