const baseUrl = process.argv[2] || 'http://localhost:5080';
const socketUrl = baseUrl.replace(/^http/, 'ws') + '/ws';

async function connect(label) {
  const suffix = crypto.randomUUID().replaceAll('-', '').slice(0, 4);
  const username = `${label}${suffix}`.slice(0, 10);
  const response = await fetch(`${baseUrl}/api/account/setup`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ username, password: `Smoke-${suffix}-password` }) });
  const setup = await response.json();
  if (!response.ok) throw new Error(setup.message || 'Account setup failed.');
  const cookie = response.headers.get('set-cookie') || '';
  if (!/Max-Age=/i.test(cookie) || !/Path=\//i.test(cookie)) throw new Error('Login cookie is not persistent and site-wide.');
  const socket = new WebSocket(`${socketUrl}?session=${encodeURIComponent(setup.sessionToken)}`);
  const messages = [], waiters = [];
  socket.addEventListener('message', event => { const message = JSON.parse(event.data); messages.push(message);const messageIndex=messages.length-1; for (const waiter of [...waiters]) if(waiter.predicate(message,messageIndex)) { waiter.resolve(message); waiters.splice(waiters.indexOf(waiter), 1); } });
  const waitFor = (predicate, timeoutMs = 10000, afterIndex = -1) => new Promise((resolve, reject) => {
    const existing = messages.find((message,index)=>index>afterIndex&&predicate(message,index));
    if (existing) return resolve(existing);
    let timer;
    const waiter = { predicate:(message,index)=>index>afterIndex&&predicate(message,index), resolve:message=>{clearTimeout(timer);resolve(message);} };
    waiters.push(waiter);
    timer=setTimeout(() => { const index = waiters.indexOf(waiter); if (index >= 0) waiters.splice(index, 1); reject(new Error(`Timed out waiting for ${username}. Received: ${messages.map(message => message.type).join(', ')}`)); }, timeoutMs);
  });
  return { socket, waitFor, messageCount:()=>messages.length, username, sessionToken: setup.sessionToken };
}

const [first, second] = await Promise.all([connect('SmokeA'), connect('SmokeB')]);
try {
  const [welcomeA, welcomeB] = await Promise.all([first.waitFor(message => message.type === 'welcome'), second.waitFor(message => message.type === 'welcome')]);
  let playerA = welcomeA.snapshot.players.find(player => player.id === welcomeA.playerId);
  if (welcomeA.protocolVersion !== 16) throw new Error(`Expected protocol 16, received ${welcomeA.protocolVersion}.`);
  if (!welcomeA.snapshot.loadedAreas?.length) throw new Error('Snapshot did not identify its exact loaded geographic areas.');
  if (!welcomeA.privateState?.base) throw new Error('Authenticated player did not receive a persistent base assignment.');
  if (playerA.locationId !== 'outdoor' || welcomeA.privateState?.dungeon) throw new Error('Brand-new account did not start at a random outdoor location.');
  if (welcomeA.snapshot.actors?.length < 40) throw new Error('Expected the 40 baseline authoritative wildlife/NPC actors.');
  first.socket.send(JSON.stringify({ type: 'setTravelMode', mode: 'run' }));
  const modeChanged = await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.travelMode === 'run');
  first.socket.send(JSON.stringify({ type: 'moveRequest', x: 1, y: 0, sequence: 1 }));
  const [seenByA, seenByB] = await Promise.all([first.waitFor(message => message.type === 'playerMoved' && message.player.id === welcomeA.playerId), second.waitFor(message => message.type === 'playerMoved' && message.player.id === welcomeA.playerId)]);
  if (seenByA.player.position.x !== seenByB.player.position.x || seenByA.player.position.x <= playerA.position.x) throw new Error('Authoritative movement did not synchronize.');
  if (seenByA.player.stamina >= playerA.stamina) throw new Error('Running did not drain stamina.');
  let path = null;
  const playerArea=welcomeA.snapshot.loadedAreas.find(area=>playerA.position.x>=area.minimumX&&playerA.position.x<=area.maximumX&&playerA.position.y>=area.minimumY&&playerA.position.y<=area.maximumY);
  if(!playerArea)throw new Error(`The starting player (${playerA.position.x}, ${playerA.position.y}) was not inside a loaded area: ${JSON.stringify(welcomeA.snapshot.loadedAreas)}.`);
  const pathOffsets = [[8, 0], [-8, 0], [0, 8], [0, -8]].filter(([offsetX,offsetY])=>playerA.position.x+offsetX>=playerArea.minimumX&&playerA.position.x+offsetX<=playerArea.maximumX&&playerA.position.y+offsetY>=playerArea.minimumY&&playerA.position.y+offsetY<=playerArea.maximumY);
  for (let index = 0; index < pathOffsets.length && !path; index++) {
    const sequence = 20 + index, [offsetX, offsetY] = pathOffsets[index];
    first.socket.send(JSON.stringify({ type: 'pathRequest', x: seenByA.player.position.x + offsetX, y: seenByA.player.position.y + offsetY, sequence }));
    const result = await first.waitFor(message => (message.type === 'pathResult' || message.type === 'pathUnavailable') && message.sequence === sequence, 60000);
    if (result.type === 'pathResult' && result.waypoints?.length) path = result;
  }
  if (!path) throw new Error('Pathfinding failed in all four cardinal directions.');
  first.socket.send(JSON.stringify({ type: 'setLights', flashlightOn: true, lanternOn: false }));
  await first.waitFor(message => message.type === 'error' && message.message.includes('flashlight'));
  first.socket.send(JSON.stringify({ type: 'setLights', flashlightOn: false, lanternOn: false, laserOn:true }));
  await first.waitFor(message => message.type === 'error' && message.message.includes('laser'));
  first.socket.send(JSON.stringify({ type: 'setMagicHikingShoes', enabled: true }));
  await first.waitFor(message => message.type === 'error' && message.message.includes('hiking shoes'));
  first.socket.send(JSON.stringify({ type: 'setMagicRunningShoes', enabled: true }));
  await first.waitFor(message => message.type === 'error' && message.message.includes('running shoes'));
  first.socket.send(JSON.stringify({ type: 'setEquipment', slot: 'hat', itemType: 'hat' }));
  await first.waitFor(message => message.type === 'error' && message.message.includes('hat'));
  first.socket.send(JSON.stringify({ type: 'setEquipment', slot: 'weapon', itemType: 'fist' }));
  await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.equippedWeapon === 'fist');
  first.socket.send(JSON.stringify({ type: 'setTravelMode', mode: 'dirtBike' }));
  await first.waitFor(message => message.type === 'error' && message.message.includes('dirt bike'));
  first.socket.send(JSON.stringify({ type: 'setTravelMode', mode: 'motorcycle' }));
  await first.waitFor(message => message.type === 'error' && message.message.includes('motorcycle'));
  first.socket.send(JSON.stringify({ type: 'setGodMode', enabled: true }));
  const god = await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.godMode);
  if (god.player.walletCents < 50000 || god.player.water < 10 || god.player.stamina < 10) throw new Error('God Mode resources were not enforced.');
  first.socket.send(JSON.stringify({ type: 'setEquipment', slot: 'weapon', itemType: 'rifle' }));
  const rifleEquipped=await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.equippedWeapon === 'rifle');
  first.socket.send(JSON.stringify({ type: 'setEquipment', slot: 'weapon', itemType: 'fist' }));
  await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.equippedWeapon === 'fist' && message.player.version>rifleEquipped.player.version);
  first.socket.send(JSON.stringify({ type: 'setLights', flashlightOn: true, lanternOn: true,laserOn:true }));
  await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.flashlightOn && message.player.lanternOn&&message.player.laserOn);
  first.socket.send(JSON.stringify({ type: 'setMagicHikingShoes', enabled: true }));
  await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.magicHikingShoesOn);
  first.socket.send(JSON.stringify({ type: 'setMagicRunningShoes', enabled: true }));
  await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.magicRunningShoesOn && !message.player.magicHikingShoesOn);
  first.socket.send(JSON.stringify({ type: 'setTravelMode', mode: 'dirtBike' }));
  const godDirtBike = await first.waitFor(message => message.type === 'playerUpdated' && message.player.id === welcomeA.playerId && message.player.travelMode === 'dirtBike');
  first.socket.send(JSON.stringify({ type: 'moveRequest', x: 1, y: 0, sequence: 3 }));
  const godRide = await first.waitFor(message => message.type === 'movementBlocked' || message.type === 'playerMoved' && message.player.id === welcomeA.playerId && message.player.travelMode === 'dirtBike');
  if (godRide.type === 'movementBlocked' && /gas/i.test(godRide.message)) throw new Error('God Mode did not bypass the dirt-bike gas check.');
  if (godRide.type === 'playerMoved' && godRide.player.dirtBikeGasGallons !== godDirtBike.player.dirtBikeGasGallons) throw new Error('God Mode consumed dirt-bike gas.');
  first.socket.send(JSON.stringify({ type: 'teleport', x: seenByA.player.position.x + 1, y: seenByA.player.position.y, godMode: false }));
  const teleported = await first.waitFor(message => message.type === 'playerTeleported' && message.player.id === welcomeA.playerId);
  const unavailableBuildings=new Set([welcomeA.privateState.base.buildingId,welcomeB.privateState.base.buildingId]);
  const candidateDoors=welcomeA.snapshot.baseEntities.filter(entity=>entity.kind==='door'&&!unavailableBuildings.has(entity.properties?.buildingId));
  let replacementDoor=null,basePurchase=null;
  for(const door of candidateDoors.slice(0,30)){
    let after=first.messageCount()-1;
    first.socket.send(JSON.stringify({type:'teleport',x:door.position.x,y:door.position.y,godMode:true}));
    await first.waitFor(message=>message.type==='playerTeleported'&&message.player.id===welcomeA.playerId&&Math.hypot(message.player.position.x-door.position.x,message.player.position.y-door.position.y)<100,10000,after);
    after=first.messageCount()-1;
    first.socket.send(JSON.stringify({type:'purchaseBase',doorId:door.id}));
    const outcome=await first.waitFor(message=>message.type==='basePurchased'||message.type==='error',10000,after);
    if(outcome.type==='basePurchased'){replacementDoor=door;basePurchase=outcome;break;}
    if(!/already another player/i.test(outcome.message))throw new Error(`Unexpected base-purchase rejection: ${outcome.message}`);
  }
  if(!replacementDoor||!basePurchase)throw new Error('No unassigned building door was available for the base-purchase test.');
  if(basePurchase.priceCents!==1||basePurchase.privateState?.base?.buildingId!==replacementDoor.properties.buildingId)throw new Error('God Mode base purchase did not cost one cent or persist the selected building.');
  const homeBase=basePurchase.privateState.base;
  first.socket.send(JSON.stringify({type:'teleport',x:homeBase.position.x,y:homeBase.position.y,godMode:true}));
  await first.waitFor(message=>message.type==='playerTeleported'&&message.player.id===welcomeA.playerId&&Math.hypot(message.player.position.x-homeBase.position.x,message.player.position.y-homeBase.position.y)<100);
  first.socket.send(JSON.stringify({type:'enterDungeon',doorId:homeBase.doorId}));
  const home=await first.waitFor(message=>message.type==='dungeonEntered'&&message.dungeon?.isHome);
  if((home.dungeon.actors||[]).length||!home.dungeon.furnishings?.some(item=>item.properties?.objectType==='bed')||!home.dungeon.furnishings?.some(item=>item.properties?.objectType==='wardrobe'))throw new Error('Private base was not generated as a safe furnished home.');
  const chair=home.dungeon.furnishings.find(item=>item.properties?.objectType==='diningChair');
  if(!chair)throw new Error('Home did not include movable starter furniture.');
  let after=first.messageCount()-1;first.socket.send(JSON.stringify({type:'rotateFurniture',furnitureId:chair.id}));const rotated=await first.waitFor(message=>message.type==='homeUpdated'&&message.dungeon?.furnishings?.some(item=>item.id===chair.id&&item.properties?.rotationDegrees==='90'),10000,after);
  after=first.messageCount()-1;first.socket.send(JSON.stringify({type:'storeFurniture',furnitureId:chair.id}));const stored=await first.waitFor(message=>message.type==='homeUpdated'&&message.privateState?.homeStorage?.some(item=>item.id===chair.id),10000,after);
  after=first.messageCount()-1;first.socket.send(JSON.stringify({type:'placeFurniture',furnitureId:chair.id,x:chair.position.x,y:chair.position.y,rotationDegrees:90}));await first.waitFor(message=>message.type==='homeUpdated'&&!message.privateState?.homeStorage?.some(item=>item.id===chair.id),10000,after);
  first.socket.send(JSON.stringify({type:'exitDungeon'}));const outdoors=await first.waitFor(message=>message.type==='dungeonExited');if(outdoors.privateState?.homeStorage!=null)throw new Error('Home storage leaked outside Home.');
  first.socket.send(JSON.stringify({ type: 'say', message: 'Hello from the smoke test' }));
  const [chatA, chatB] = await Promise.all([first.waitFor(message => message.type === 'chatSaid' && message.chat.playerId === welcomeA.playerId), second.waitFor(message => message.type === 'chatSaid' && message.chat.playerId === welcomeA.playerId)]);
  if (chatA.chat.message !== chatB.chat.message || chatA.chat.username !== first.username) throw new Error('Chat did not synchronize.');
  first.socket.send(JSON.stringify({ type: 'placeObject', objectType: 'must-be-rejected', x: teleported.player.position.x + 1, y: teleported.player.position.y, rotationDegrees: 0 }));
  const rejection = await first.waitFor(message => message.type === 'error' && message.message.includes('disabled'));
  console.log(JSON.stringify({ ok: true, protocol: welcomeA.protocolVersion, authenticatedAccounts: true, persistentCookie: true, randomOutdoorNewAccountSpawn: true, persistentBaseAssignment: true, oneCentGodModeBasePurchase:true, safeFurnishedHome:true, furnitureActionsAuthoritative:!!rotated&&!!stored,homeStoragePrivate:true,actors: welcomeA.snapshot.actors.length, travelMode: modeChanged.player.travelMode, weaponEquipmentAuthoritative:true, equipmentOwnershipEnforced: true, motorVehicleOwnershipEnforced: true, godModeFuelBypass: true, chatSynchronized: true, movementSynchronized: true, serverPathfinding: true, objectPlacementRejected: rejection.message }, null, 2));
} finally { first.socket.close(); second.socket.close(); }
