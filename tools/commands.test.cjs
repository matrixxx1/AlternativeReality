const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const commands=require('../src/AlternateEarth.Client2D/commands.js');
test('fear runs away from the attacker and respects dungeon bounds',()=>{
 assert.deepEqual(commands.fearDestination({x:10,y:10},{x:12,y:10}),{x:2,y:10});
 assert.deepEqual(commands.fearDestination({x:1,y:1},{x:2,y:1},{width:20,height:20}),{x:.6,y:1});
 const point=commands.fearDestination({x:1,y:1},{x:1,y:1});
 assert.ok(Number.isFinite(point.x)&&Number.isFinite(point.y));
});
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){return source.split('\n').find(line=>line.includes(`function ${name}(`));}
test('Timing accelerates ranged and burst cadence while preserving melee speed',()=>{
 const state={privateState:{progression:{shootingIntervalMultiplier:.5},serverConfiguration:{items:[{itemType:'rifle',attackIntervalSeconds:.4},{itemType:'sword',attackIntervalSeconds:.75}]}}};
 const interval=vm.runInNewContext(`(${implementation('equippedAttackInterval')})`,{state});
 assert.equal(interval({equippedWeapon:'rifle'}),200);
 assert.equal(interval({equippedWeapon:'ar15',ar15FireMode:'burst'}),500);
 assert.equal(interval({equippedWeapon:'sword'}),750);
});
function harness(mode='neutral'){
 const sent=[],routes=[],toasts=[];
 const me={id:'me',position:{x:0,y:0},equippedWeapon:'rifle',locationId:'outdoor'};
 const target={id:'npc',name:'NPC',position:{x:10,y:0},healthHearts:5,locationId:'outdoor'};
 const state={scale:1,playerId:'me',players:new Map([['me',me]]),probulatorBeams:new Map(),facings:new Map(),baseById:new Map(),keys:new Set(),path:[],pathSequence:7,actionMode:mode,chests:new Map(),loot:new Map(),followCommand:null};
 const context={BusTransit:require('../src/AlternateEarth.Client2D/transit.js'),Scuba:require('../src/AlternateEarth.Client2D/scuba.js'),Inversions:{click:()=>false,canvasClick:()=>false},state,QuestNavigation:require('../src/AlternateEarth.Client2D/quest-navigation.js'),toScreen:p=>p,PlayerCommands:commands,ui:{actionMenu:{}},sent,routes,toasts,me,target,
  send:m=>sent.push(m),stopTravel:()=>commands.cancel(state),showToast:m=>toasts.push(m),
  clearActionChoices:()=>{},actionTargetsAt:()=>[],isSleeping:()=>false,hazardWeaponTypes:new Set(['gasBottle']),clickedOwnUfo:()=>false,
  dungeonFeatureAt:()=>null,postOfficeAt:()=>null,combatTargets:()=>[target],nearPoint:items=>items[0],questForActor:()=>null,
  vehicleAt:()=>null,hoverBuildingAtScreen:()=>null,buildingAt:()=>null,choppableAt:()=>null,doorAtScreen:()=>null,renderList:()=>[],dynamicVisible:()=>true,
  navigateTo:p=>{routes.push(p);state.target=p;},setActionMode:m=>{commands.cancel(state);state.actionMode=m;},
  followTargetById:()=>target,equippedWeaponRange:()=>50,equippedAttackInterval:()=>400};
 vm.createContext(context);
 for(const name of ['actorAtScreen','busStopAt','beginFollowCommand','maintainScubaAttack','beginBusBoarding','maintainBusBoarding','maintainFollowCommand','maintainTreasurePickup','beginScubaTreasure','handlePrimaryClick'])vm.runInContext(implementation(name),context);
 return context;
}
test('new intent clears every pending interaction, held keys and late route generation',()=>{
 const state={keys:new Set(['w']),path:[{x:1,y:1}],pathSequence:11,commandSequence:4,target:{},followCommand:{}};
 for(const key of ['pendingDoor','pendingMerchant','pendingChest','pendingLoot','pendingDungeonAction','pendingChop','pendingPet'])state[key]={};
 commands.cancel(state);
 assert.equal(state.pathSequence,12);assert.equal(state.commandSequence,5);assert.equal(state.keys.size,0);assert.deepEqual(state.path,[]);
 for(const key of ['target','followCommand','pendingDoor','pendingMerchant','pendingChest','pendingDungeonAction','pendingChop','pendingPet'])assert.equal(state[key],null);
});
test('Defensive clicking an NPC never attacks, including an equipped gas container',()=>{
 for(const weapon of ['rifle','gasBottle']){const c=harness('defensive');c.me.equippedWeapon=weapon;c.handlePrimaryClick({x:10,y:0},10,0);assert.equal(c.sent.length,0);assert.equal(c.state.followCommand,null);}
});
test('Attack clicking an NPC repeats attacks at its cadence and a replacement stops them',()=>{
 const c=harness('attackReady');c.state.pendingDoor={};c.handlePrimaryClick({x:10,y:0},10,0);
 assert.equal(c.state.pendingDoor,null);assert.equal(c.state.followCommand.targetId,'npc');
 c.maintainFollowCommand(1000,c.me);c.maintainFollowCommand(1100,c.me);c.maintainFollowCommand(1500,c.me);
 assert.equal(c.sent.length,2);c.stopTravel();c.maintainFollowCommand(2100,c.me);assert.equal(c.sent.length,2);
});
test('building attack approaches its nearest wall, repeats in range, and stops at rubble',()=>{
 const c=harness('attackReady');const building={id:'building',kind:'building',position:{x:100,y:0},geometry:[{x:60,y:-5},{x:140,y:-5},{x:140,y:5},{x:60,y:5}],properties:{}};
 c.state.baseById.set(building.id,building);c.beginFollowCommand('attack',building,true);c.maintainFollowCommand(1000,c.me);
 assert.equal(c.routes[0].x,21);assert.equal(c.routes[0].y,0);assert.equal(c.sent.length,0);
 c.me.position.x=21;c.maintainFollowCommand(2000,c.me);assert.equal(c.sent[0].type,'attackWorldObject');
 building.properties.state='rubble';c.maintainFollowCommand(3000,c.me);assert.equal(c.state.followCommand,null);assert.equal(c.sent.length,1);
});

test('explicit bus attack follows its moving hull and stops when disabled',()=>{
 const c=harness('neutral');c.BusTransit=require('../src/AlternateEarth.Client2D/transit.js');
 const bus={id:'bus:test',routeId:'route',routeName:'Test service',position:{x:10,y:0},headingRadians:0,healthHearts:100};
 c.followTargetById=()=>bus;c.beginFollowCommand('attack',bus);c.maintainFollowCommand(1000,c.me);
 assert.equal(c.sent[0].type,'attack');assert.equal(c.sent[0].targetId,bus.id);
 bus.position={x:100,y:0};c.maintainFollowCommand(2000,c.me);assert.ok(c.routes.length>0);
 bus.healthHearts=0;c.maintainFollowCommand(3000,c.me);assert.equal(c.state.followCommand,null);
});
test('explicit Attack from Neutral raises weapons and selects only the new target',()=>{
 const c=harness('neutral');c.state.followCommand={targetId:'old'};c.beginFollowCommand('attack',c.target);
 assert.equal(c.state.actionMode,'attackReady');assert.equal(c.state.followCommand.targetId,'npc');
});
test('target death and weapon fallback stop repeated attacks',()=>{
 for(const change of [c=>c.target.healthHearts=0,c=>c.me.equippedWeapon='fist']){
  const c=harness('attackReady');c.beginFollowCommand('attack',c.target);change(c);c.maintainFollowCommand(1000,c.me);
  assert.equal(c.state.followCommand,null);assert.equal(c.sent.length,0);
 }
});

test('Posturing cycles through all five modes',()=>{
 const modes=['timid','defensive','neutral','attackReady','aggressive'];
 modes.forEach((mode,i)=>assert.equal(commands.nextMode(mode),modes[(i+1)%modes.length]));
});
test('Neutral clicks do not start attacks',()=>{
 const c=harness('neutral');c.handlePrimaryClick({x:10,y:0},10,0);assert.equal(c.sent.length,0);assert.equal(c.state.followCommand,null);
});

function automatic(mode,extra={}){
 const me={id:'me',position:{x:0,y:0},equippedWeapon:'rifle'},npc={id:'npc',kind:'npc',position:{x:4,y:0},healthHearts:10},player={id:'player',position:{x:3,y:0},healthHearts:10},animal={id:'animal',kind:'animal',position:{x:5,y:0},healthHearts:10};
 return commands.automaticAction({mode,me,targets:[npc,player,animal],players:new Map([['player',player]]),relationships:new Map(),attackers:new Map(),now:1000,...extra});
}
test('Poised never auto-attacks; Aggressive chooses nearby characters and respects PvP',()=>{
 assert.equal(automatic('attackReady'),null);assert.equal(automatic('aggressive').target.id,'player');
 assert.equal(automatic('aggressive',{pvpEnabled:false}).target.id,'npc');
 assert.equal(automatic('aggressive',{pvpEnabled:false,avoid:new Map([['npc',2000]])}).target.id,'animal');
});
test('Defensive retaliates only against recent attackers, including animals',()=>{
 assert.equal(automatic('defensive'),null);assert.equal(automatic('defensive',{attackers:new Map([['animal',2000]])}).target.id,'animal');
 assert.equal(automatic('defensive',{attackers:new Map([['npc',900]])}),null);
});
test('Timid moves away from players regardless of rating and from negative-rated NPCs',()=>{
 const flee=automatic('timid');assert.equal(flee.kind,'flee');assert.ok(flee.destination.x<0);
 assert.equal(automatic('timid',{players:new Map()}),null);
 assert.ok(automatic('timid',{players:new Map(),relationships:new Map([['npc',-.01]])}).destination.x<0);
});
test('Timid handles overlapping threats without invalid coordinates',()=>{
 const flee=automatic('timid',{targets:[{id:'player',position:{x:0,y:0}}]});assert.ok(Number.isFinite(flee.destination.x));assert.ok(Number.isFinite(flee.destination.y));
});
test('manual cancellation drops automatic pursuit, flight and old retaliation targets',()=>{
 const state={keys:new Set(),path:[],pathSequence:1,autoFlee:true,followCommand:{automatic:true},defensiveThreats:new Map([['npc',2000]])};commands.cancel(state);
 assert.equal(state.autoFlee,false);assert.equal(state.followCommand,null);assert.equal(state.defensiveThreats.size,0);
});

test('a crowded attack click opens choices instead of attacking the first character',()=>{
 const c=harness('attackReady');let chosen;
 c.actionTargetsAt=()=>[{kind:'actor',id:'npc'},{kind:'loot',id:'treasure'}];
 c.openActionChoices=(point,x,y,targets)=>{chosen=targets;};
 c.handlePrimaryClick({x:10,y:0},10,0);
 assert.equal(chosen.length,2);assert.equal(c.state.followCommand,null);assert.equal(c.sent.length,0);
});

function block(name){const start=source.indexOf(`  function ${name}(`);return source.slice(start,source.indexOf('\n  function ',start+1));}

test('scuba target clicks pursue and line up before shooting regardless of posture',()=>{
 for(const mode of ['neutral','defensive','timid','attackReady']){
  const c=harness(mode);c.state.dungeon={underwater:{},width:180,height:20};
  c.me.locationId=c.target.locationId='dive';c.me.equippedWeapon='spearGun';c.me.position={x:5,y:3};c.target.position={x:100,y:9};
  c.actionTargetsAt=()=>[{kind:'actor',entity:c.target}];
  c.handlePrimaryClick(c.target.position,100,9);assert.equal(c.state.followCommand.targetId,'npc');
  c.maintainFollowCommand(1000,c.me);assert.equal(c.sent.length,0);assert.equal(c.routes[0].y,9);assert.equal(c.routes[0].x,61);
  c.me.position={x:61,y:8};c.maintainFollowCommand(2000,c.me);assert.equal(c.sent.length,0);
  c.me.position={x:61,y:9};c.maintainFollowCommand(2200,c.me);assert.equal(c.sent[0].type,'attack');assert.equal(c.sent[0].targetId,'npc');assert.equal(c.state.target,null);
  c.maintainFollowCommand(2300,c.me);assert.equal(c.sent.length,1);
  c.target.position.y=11;c.maintainFollowCommand(3000,c.me);assert.equal(c.sent.length,1);assert.equal(c.routes.at(-1).y,11);
  commands.cancel(c.state);c.maintainFollowCommand(4000,c.me);assert.equal(c.sent.length,1);
 }
});

test('scuba treasure clicks swim into range and open each source exactly once',()=>{
 for(const kind of ['loot','chest']){
  const c=harness();c.state.dungeon={underwater:{},width:180,height:20};c.me.locationId='dive';
  const item={id:'treasure',position:{x:50,y:6}};c.state[kind==='loot'?'loot':'chests'].set(item.id,item);
  c.actionTargetsAt=()=>[{kind,entity:item}];c.handlePrimaryClick(item.position,50,6);
  assert.equal(c.sent.length,0);assert.equal(c.routes[0],item.position);
  c.maintainTreasurePickup(c.me);assert.equal(c.sent.length,0);
  c.me.position={x:48,y:6};c.maintainTreasurePickup(c.me);c.maintainTreasurePickup(c.me);
  assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,kind==='loot'?'openLoot':'openChest');
  assert.equal(c.state.pendingLoot,null);assert.equal(c.state.pendingChest,null);
 }
});

test('scuba treasure already in range opens immediately and canceled pursuit takes nothing',()=>{
 const c=harness();c.state.dungeon={underwater:{},width:180,height:20};
 const item={id:'treasure',position:{x:2,y:0}};c.state.loot.set(item.id,item);c.actionTargetsAt=()=>[{kind:'loot',entity:item}];
 c.handlePrimaryClick(item.position,2,0);assert.equal(c.sent[0].type,'openLoot');assert.equal(c.routes.length,0);
 item.position.x=50;c.handlePrimaryClick(item.position,50,0);commands.cancel(c.state);c.me.position=item.position;c.maintainTreasurePickup(c.me);
 assert.equal(c.sent.length,1);
});
test('defeated dungeon actors are removed from follow targets and action choices',()=>{
 const state={actors:new Map([['dead',{id:'dead'}]]),dungeon:{actors:[{id:'dead'},{id:'alive'}]},actionActor:{id:'dead'},actionChoices:{targets:[{id:'dead'},{id:'alive'}]}};
 const remove=vm.runInNewContext(`(${block('removeCombatTarget')})`,{state});remove('dead');
 assert.equal(state.actors.has('dead'),false);assert.deepEqual(state.dungeon.actors.map(a=>a.id),['alive']);assert.equal(state.actionActor,null);assert.equal(state.actionChoices.targets[0].id,'alive');
});
test('old combat player snapshots cannot move a player back after a newer movement',()=>{
 const current={id:'me',version:12,position:{x:10,y:0}},state={players:new Map([['me',current]])};
 const update=vm.runInNewContext(`(${block('updatePlayer')})`,{state});update({...current,version:11,position:{x:0,y:0}});assert.equal(state.players.get('me'),current);
});
test('incoming fear never clears held movement keys or repeatedly restarts an escape',()=>{
 for(const moving of [true,false]){
  const state={playerId:'me',players:new Map([['me',{id:'me'}]]),keys:new Set(moving?['w']:[]),autoFlee:!moving};
  const receive=vm.runInNewContext(`(${block('receiveCombatFear')})`,{state,stopTravel:()=>assert.fail('Movement canceled')});
  receive({targetId:'me',fleeInFear:true});assert.equal(state.keys.size,moving?1:0);
 }
});


test('Board command approaches a parked bus, cancels if activated, boards once, and respects new intent',()=>{
 const c=harness();Object.assign(c.me,{healthHearts:10,equippedWeapon:'none',travelMode:'walk',position:{x:0,y:7}});
 Object.assign(c.target,{routeId:'route',headingRadians:0,position:{x:0,y:0},speedMetersPerSecond:0,status:'out of service'});
 c.beginBusBoarding(c.target);c.maintainFollowCommand(1000,c.me);
 assert.equal(c.state.followCommand.mode,'boardBus');assert.equal(c.sent.length,0);assert.equal(c.routes.length,1);
 c.target.status='boarding';c.maintainFollowCommand(2000,c.me);assert.equal(c.state.target,null);assert.equal(c.state.followCommand,null);assert.equal(c.sent.length,0);
 c.target.status='out of service';c.beginBusBoarding(c.target);
 c.target.speedMetersPerSecond=0;c.me.position={x:0,y:-2.4};c.maintainFollowCommand(3000,c.me);c.maintainFollowCommand(4000,c.me);
 assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'boardBus');assert.equal(c.state.followCommand,null);
 c.me.position={x:0,y:7};c.beginBusBoarding(c.target);c.stopTravel();c.maintainFollowCommand(5000,c.me);assert.equal(c.sent.length,1);
 c.beginBusBoarding(c.target);c.target.healthHearts=0;c.maintainFollowCommand(6000,c.me);assert.equal(c.state.followCommand,null);assert.equal(c.sent.length,1);
});

test('UFO Attack enables the Probulator immediately, including when its visible state is stale',()=>{
 for(const active of [false,true])for(const mode of ['neutral','attackReady']){
  const c=harness(mode);c.me.travelMode='ufo';c.me.equippedWeapon='probulator';c.target.position.x=100;
  if(active)c.state.probulatorBeams.set('me',{});
  c.beginFollowCommand('attack',c.target);assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'toggleProbulator');assert.equal(c.sent[0].enabled,true);assert.equal(c.state.followCommand.targetId,c.target.id);
 }
 const c=harness();c.me.travelMode='ufo';c.beginFollowCommand('stalk',c.target);assert.equal(c.sent.length,0);
 c.me.travelMode='walk';c.beginFollowCommand('attack',c.target);assert.equal(c.sent.length,0);
});
