const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const commands=require('../src/AlternateEarth.Client2D/commands.js');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){return source.split('\n').find(line=>line.includes(`function ${name}(`));}
function harness(mode='neutral'){
 const sent=[],routes=[],toasts=[];
 const me={id:'me',position:{x:0,y:0},equippedWeapon:'rifle',locationId:'outdoor'};
 const target={id:'npc',name:'NPC',position:{x:10,y:0},healthHearts:5,locationId:'outdoor'};
 const state={scale:1,playerId:'me',players:new Map([['me',me]]),probulatorBeams:new Map(),baseById:new Map(),keys:new Set(),path:[],pathSequence:7,actionMode:mode,chests:new Map(),loot:new Map(),followCommand:null};
 const context={state,QuestNavigation:require('../src/AlternateEarth.Client2D/quest-navigation.js'),toScreen:p=>p,PlayerCommands:commands,ui:{actionMenu:{}},sent,routes,toasts,me,target,
  send:m=>sent.push(m),stopTravel:()=>commands.cancel(state),showToast:m=>toasts.push(m),
  isSleeping:()=>false,hazardWeaponTypes:new Set(['gasBottle']),clickedOwnUfo:()=>false,
  dungeonFeatureAt:()=>null,postOfficeAt:()=>null,combatTargets:()=>[target],nearPoint:items=>items[0],questForActor:()=>null,
  vehicleAt:()=>null,hoverBuildingAtScreen:()=>null,buildingAt:()=>null,choppableAt:()=>null,doorAtScreen:()=>null,renderList:()=>[],dynamicVisible:()=>true,
  navigateTo:p=>{routes.push(p);state.target=p;},setActionMode:m=>{commands.cancel(state);state.actionMode=m;},
  followTargetById:()=>target,equippedWeaponRange:()=>50,equippedAttackInterval:()=>400};
 vm.createContext(context);
 for(const name of ['actorAtScreen','busStopAt','beginFollowCommand','maintainFollowCommand','handlePrimaryClick'])vm.runInContext(implementation(name),context);
 return context;
}
test('new intent clears every pending interaction, held keys and late route generation',()=>{
 const state={keys:new Set(['w']),path:[{x:1,y:1}],pathSequence:11,commandSequence:4,target:{},followCommand:{}};
 for(const key of ['pendingDoor','pendingMerchant','pendingChest','pendingDungeonAction','pendingChop','pendingPet'])state[key]={};
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
