const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function harness(){
 const me={id:'me',locationId:'outdoor',position:{x:0,y:0}},state={lootingMode:'all',loot:new Map(),chests:new Map(),privateState:{inventory:{items:[],maximumWeightPounds:50}}};
 const sent=[],ui={treasureWindow:{hidden:true}},c=vm.createContext({state,me,ui,sent,Date,effectiveCarryingCapacity:()=>50,send:m=>sent.push(m),applyPrivate:()=>{},openTreasure:()=>assert.fail('Automatic pickup opened a window')});
 for(const name of ['advanceAutoTreasure','receiveAutoTreasure']){const start=source.indexOf(`  function ${name}(`);vm.runInContext(source.slice(start,source.indexOf('\n  function ',start+1)),c);}
 return c;
}
test('Auto take all checks nearby loot and chests without stopping movement',()=>{
 for(const chest of [false,true]){
  const c=harness();c.state.target={x:100,y:0};c.state.followCommand={targetId:'fish'};
  c.state[chest?'chests':'loot'].set('treasure',{id:'treasure',position:{x:3,y:0},locationId:'outdoor'});
  c.advanceAutoTreasure(1000,c.me);assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'autoTakeNearbyTreasure');assert.equal(c.sent[0].chest,chest);
  assert.equal(c.state.target.x,100);assert.equal(c.state.followCommand.targetId,'fish');
  c.advanceAutoTreasure(2000,c.me);assert.equal(c.sent.length,1);
  c.receiveAutoTreasure({});c.advanceAutoTreasure(3000,c.me);assert.equal(c.sent.length,1);
  c.state.privateState.inventory.items.push({itemType:'rock',quantity:1});c.advanceAutoTreasure(4000,c.me);assert.equal(c.sent.length,2);
 }
});
test('Selective, distant, expired, other-location and private rewards are not automatically collected',()=>{
 const c=harness(),item={id:'loot',position:{x:2,y:0},locationId:'outdoor'};
 for(const overrides of [{position:{x:5,y:0}},{locationId:'dive'},{expiresAtUtc:'2000-01-01'},{dropKind:'eventReward',ownerId:'other'}]){
  c.state.loot.set('loot',{...item,...overrides});c.state.nextAutoTreasureAt=0;c.advanceAutoTreasure(1000,c.me);assert.equal(c.sent.length,0);
 }
 c.state.loot.set('loot',item);c.state.lootingMode='selective';c.advanceAutoTreasure(5000,c.me);assert.equal(c.sent.length,0);
 c.state.lootingMode='all';c.advanceAutoTreasure(6000,c.me);assert.equal(c.sent.length,1);
});
test('automatic pickup includes own event rewards and defers to explicit pickup',()=>{
 const c=harness();c.me.locationId='dive';c.state.loot.set('reward',{id:'reward',dropKind:'eventReward',ownerId:'me',locationId:'dive',position:{x:1,y:0}});
 c.state.pendingLoot={id:'reward'};c.advanceAutoTreasure(1000,c.me);assert.equal(c.sent.length,0);
 c.state.pendingLoot=null;c.advanceAutoTreasure(2000,c.me);assert.equal(c.sent[0].sourceId,'reward');
});

test('Upgrades only requests authoritative filtering for nearby treasure',()=>{
 const c=harness();c.state.lootingMode='upgrades';
 c.state.loot.set('loot',{id:'loot',position:{x:1,y:0},locationId:'outdoor'});
 c.advanceAutoTreasure(1000,c.me);
 assert.equal(c.sent.length,1);assert.equal(c.sent[0].upgradesOnly,true);
});

test('guarded scuba treasure becomes eligible immediately after the guardian is removed',()=>{
 const c=harness();c.state.dungeon={underwater:{},actors:[{id:'guardian',factionId:'chest'}]};
 c.state.chests.set('chest',{id:'chest',position:{x:1,y:0}});
 c.advanceAutoTreasure(1000,c.me);assert.equal(c.sent.length,0);
 c.state.dungeon.actors=[];c.advanceAutoTreasure(2000,c.me);assert.equal(c.sent[0].sourceId,'chest');
});

test('looting mode retains old Manual preference and persists Upgrades only',()=>{
 const c=harness(),saved=[],description={},buttons=['all','selective','upgrades'].map(mode=>({dataset:{looting:mode},classList:{toggle(){}},setAttribute(name,value){this[name]=value;}}));
 c.document={querySelectorAll:()=>buttons};c.$=()=>description;c.localStorage={setItem:(...args)=>saved.push(args)};
 const start=source.indexOf('  function setLootingMode(');vm.runInContext(source.slice(start,source.indexOf('\n  function ',start+1)),c);
 c.setLootingMode('selective');assert.equal(c.state.lootingMode,'selective');
 c.setLootingMode('upgrades');assert.equal(c.state.lootingMode,'upgrades');assert.equal(buttons[2]['aria-pressed'],'true');
 assert.equal(saved.at(-1)[1],'upgrades');
 const html=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/index.html'),'utf8');
 assert.match(html,/data-looting="selective"[^>]*>Manual<\/button>/);
 assert.match(html,/data-looting="upgrades"[^>]*title="Automatically collect money and crafting items/);assert.doesNotMatch(html,/id="lootingDescription"/);
});
