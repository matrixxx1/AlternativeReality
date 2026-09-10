const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`);assert.ok(start>=0,name);return source.slice(start,source.indexOf('\n  function ',start+1));}
function harness(){
  function element(){return{children:[],dataset:{},listeners:{},classList:{contains:()=>false},append(...children){this.children.push(...children);},replaceChildren(){this.children=[];},setAttribute(){},addEventListener(type,listener){this.listeners[type]=listener;},focus(){}};}
  const heading=element(),ui={treasureItems:element(),treasureCash:element(),treasureWeight:element(),treasureWarning:element(),treasureTake:element(),treasureClose:element(),treasureWindow:{hidden:true,scrollHeight:500,classList:{contains:()=>false},querySelector:()=>heading}};
  for(const name of ['treasureTakeAll','treasureHint','treasureColumns']){
    assert.ok(source.includes(`${name}:$('#${name}')`),`${name} is bound to the actual control`);ui[name]=element();
  }
  ui.treasureItems.querySelectorAll=()=>ui.treasureItems.children.map(row=>row.children.at(-1)).filter(input=>input?.dataset?.item);
  const state={players:new Map([['me',{}]]),playerId:'me',loot:new Map(),chests:new Map(),privateState:{inventory:{items:[{itemType:'rock',quantity:98,category:'weapon',unitWeightPounds:.5}],weightPounds:49,maximumWeightPounds:50}}};
  const sent=[],c=vm.createContext({ui,state,sent,document:{createElement:element},itemDisplayName:type=>type,createItemArt:element,stackWeight:item=>item.quantity*item.unitWeightPounds,weightText:w=>w+' lb',effectiveCarryingCapacity:(me,capacity)=>me?.godMode?Infinity:capacity,send:message=>sent.push(message),applyPrivate:()=>{},showToast:()=>{},innerWidth:1200,innerHeight:800,viewportWidth:()=>900,floatPanel:()=>{}});
  for(const name of ['closeTreasure','receiveNearbyTreasure','openLootTreasure','openTreasure','refreshTreasure','treasureSelection','treasureCapacity','updateTreasureWeight','sendTreasureTake','tryAutomaticTreasure','recoverAutomaticTreasure','takeAllTreasure','takeTreasureSelection'])vm.runInContext(implementation(name),c);
  c.loot={id:'loot',moneyCents:123,items:[{itemType:'rock',quantity:10,category:'weapon',unitWeightPounds:.5}]};state.loot.set(c.loot.id,c.loot);
  return c;
}

test('Retro victory always opens a chest and closing it leaves exactly once',()=>{
  const c=harness();c.state.lootingMode='all';c.state.privateState.inventory.weightPounds=0;
  c.state.dungeon={isCompleted:true,retroBattle:{rewardChestId:'retro:chest'}};
  c.openTreasure({chestId:'retro:chest',items:[]},'All clear',false,true);
  assert.equal(c.ui.treasureWindow.hidden,false);assert.equal(c.sent.length,0);
  c.refreshTreasure();assert.equal(c.ui.treasureWindow.hidden,false);
  c.closeTreasure();c.closeTreasure();assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'exitDungeon');
});

test('empty private Retro reward stays open until the player closes it',()=>{
  const c=harness();c.state.dungeon={isCompleted:true,retroBattle:{},eventBattle:{dungeonNumber:2}};
  const reward={id:'private-reward',dropKind:'eventReward',moneyCents:0,items:[]};
  c.state.loot.set(reward.id,reward);c.openLootTreasure(reward);
  c.state.loot.delete(reward.id);c.refreshTreasure();
  assert.equal(c.sent.length,0);assert.equal(c.ui.treasureWindow.hidden,false);
  c.closeTreasure();assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'exitDungeon');
});
test('opening treasure takes nothing; quantities block overweight pickups but allow a smaller selection',()=>{
  const c=harness();c.openLootTreasure(c.loot);assert.equal(c.sent.length,0);
  const input=c.ui.treasureItems.querySelectorAll()[0];assert.equal(input.value,'0');input.value='10';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,true);
  c.takeTreasureSelection();assert.equal(c.sent.length,0);input.value='1';c.takeTreasureSelection();
  assert.equal(c.sent[0].type,'takeLootItems');assert.equal(c.sent[0].items[0].quantity,1);
});
test('backpack changes from Inventory refresh capacity without losing the treasure selection',()=>{
  const c=harness();c.openLootTreasure(c.loot);c.ui.treasureItems.querySelectorAll()[0].value='4';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,true);
  c.state.privateState.inventory.items[0].quantity=88;c.state.privateState.inventory.weightPounds=44;c.refreshTreasure();
  assert.equal(c.ui.treasureItems.querySelectorAll()[0].value,'4');assert.equal(c.ui.treasureTake.disabled,false);
});
test('cash can be collected alone and an existing item stack does not require a new slot',()=>{
  const c=harness();c.state.privateState.inventory.maximumWeaponSlots=1;c.openLootTreasure(c.loot);c.takeTreasureSelection();assert.equal(c.sent[0].items.length,0);
  c.ui.treasureItems.querySelectorAll()[0].value='1';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,false);
  c.openLootTreasure({...c.loot,items:[{itemType:'knife',quantity:1,category:'weapon',unitWeightPounds:.1}]});c.ui.treasureItems.querySelectorAll()[0].value='1';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,false);assert.equal(c.ui.treasureWarning.textContent,'');
});
test('chests use selective pickup too and stale ground quantities are clamped or closed',()=>{
  const c=harness();c.openTreasure({chestId:'chest',items:c.loot.items});c.ui.treasureItems.querySelectorAll()[0].value='1';c.takeTreasureSelection();assert.equal(c.sent[0].type,'takeChestItems');
  c.openLootTreasure(c.loot);c.ui.treasureItems.querySelectorAll()[0].value='5';c.state.loot.set('loot',{...c.loot,items:[{...c.loot.items[0],quantity:2}]});c.refreshTreasure();assert.equal(c.ui.treasureItems.querySelectorAll()[0].value,'2');
  c.state.loot.delete('loot');c.refreshTreasure();assert.equal(c.ui.treasureWindow.hidden,true);
});
test('Take all looting bypasses the window for both chests and loose treasure when everything fits',()=>{
  for(const kind of ['chest','loot']){
    const c=harness();c.state.lootingMode='all';c.state.privateState.inventory.weightPounds=1;c.state.privateState.inventory.items[0].quantity=2;
    if(kind==='loot')c.openLootTreasure(c.loot,null,false,true);else c.openTreasure({chestId:'chest',items:c.loot.items},null,false,true);
    assert.equal(c.ui.treasureWindow.hidden,true);assert.equal(c.sent.length,1);assert.equal(c.sent[0].items[0].quantity,10);
    assert.equal(c.sent[0].type,kind==='loot'?'takeLootItems':'takeChestItems');
  }
});
test('Upgrades only delegates clicked treasure to the server without an unfiltered take',()=>{
 const c=harness();c.state.lootingMode='upgrades';
 c.receiveNearbyTreasure({type:'nearbyTreasureOpened',contents:{anchorId:'loot',sources:[{id:'loot',chest:false}],items:c.loot.items}});
 assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'autoTakeNearbyTreasure');assert.equal(c.sent[0].upgradesOnly,true);
 assert.equal(c.ui.treasureWindow.hidden,true);assert.equal(c.state.autoTreasurePending,true);
});

test('Take all looting falls back to item selection when weight is full',()=>{
  for(const limit of ['weight']){
    const c=harness();c.state.lootingMode='all';
    if(limit==='slots'){c.state.privateState.inventory.weightPounds=1;c.state.privateState.inventory.maximumWeaponSlots=1;c.loot.items=[{itemType:'knife',quantity:1,category:'weapon',unitWeightPounds:.1}];}
    c.openLootTreasure(c.loot,null,false,true);assert.equal(c.ui.treasureWindow.hidden,false);assert.equal(c.sent.length,0);
    assert.equal(c.ui.treasureItems.querySelectorAll()[0].value,'0');
  }
});
test('Selective looting always opens a window even when every item fits',()=>{
  const c=harness();c.state.lootingMode='selective';c.state.privateState.inventory.weightPounds=1;
  c.openLootTreasure(c.loot,null,false,true);assert.equal(c.ui.treasureWindow.hidden,false);assert.equal(c.sent.length,0);
});
test('an authoritative rejection reopens automatic loot for selection without retrying',()=>{
  const c=harness();c.state.lootingMode='all';c.state.commandSequence=42;c.state.privateState.inventory.weightPounds=1;
  c.openLootTreasure(c.loot,null,false,true);assert.equal(c.ui.treasureWindow.hidden,true);
  c.recoverAutomaticTreasure({commandSequence:41,message:'Unrelated'});assert.equal(c.ui.treasureWindow.hidden,true);
  c.recoverAutomaticTreasure({commandSequence:42,message:'Your backpack is full.'});assert.equal(c.ui.treasureWindow.hidden,false);
  assert.equal(c.ui.treasureCash.textContent,'Your backpack is full.');assert.equal(c.sent.length,1);assert.equal(c.state.automaticTreasure,null);
});

test('quest and other stacks have no slot cap for selective and automatic pickup',()=>{
  for(const category of ['quest','other'])for(const automatic of [false,true]){
    const c=harness();c.state.lootingMode=automatic?'all':'selective';
    c.state.privateState.inventory={items:Array.from({length:30},(_,i)=>({itemType:`${category}:${i}`,quantity:1,category,unitWeightPounds:0})),weightPounds:0,maximumWeightPounds:50};
    c.loot.items=[{itemType:`${category}:new`,quantity:1,category,unitWeightPounds:.1}];
    c.openLootTreasure(c.loot,null,false,automatic);
    if(!automatic){c.ui.treasureItems.querySelectorAll()[0].value='1';c.takeTreasureSelection();}
    assert.equal(c.sent.length,1);assert.equal(c.sent[0].items[0].quantity,1);
  }
});


test('God mode ignores backpack weight and all weapon counts',()=>{
 const c=harness();c.state.players.set('me',{godMode:true});c.state.privateState.inventory.weightPounds=10000;
 c.openTreasure({nearby:true,anchorId:'a',sources:[{id:'a',chest:false}],items:[{itemType:'sword',quantity:100,unitWeightPounds:4,category:'weapon'}]});
 c.ui.treasureItems.querySelectorAll()[0].value='100';c.takeTreasureSelection();
 assert.equal(c.sent[0].type,'takeNearbyTreasure');assert.equal(c.sent[0].items[0].quantity,100);assert.match(c.ui.treasureWeight.textContent,/Unlimited/);
 c.state.players.set('me',{godMode:false});c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,true);
});
test('one nearby selection includes all source IDs and preserves quantity after pickup',()=>{
 const c=harness();const contents={nearby:true,anchorId:'a',sources:[{id:'a',chest:false},{id:'b',chest:true}],items:[{itemType:'rock',quantity:2,unitWeightPounds:.5}]};
 c.openTreasure(contents);c.ui.treasureItems.querySelectorAll()[0].value='1';c.takeTreasureSelection();
 assert.equal(c.sent[0].sources.length,2);assert.equal(c.sent[0].items.length,1);
 c.openTreasure({...contents,items:[{...contents.items[0],quantity:1}]},null,true);assert.equal(c.ui.treasureItems.querySelectorAll()[0].value,'1');
});

test('event victory follows Auto take all when rewards fit',()=>{
 const c=harness();c.state.lootingMode='all';c.state.privateState.inventory.weightPounds=0;
 c.openTreasure({nearby:true,isEventReward:true,anchorId:'reward',sources:[{id:'reward',chest:false}],items:c.loot.items},'Event complete. Collect your treasure.',false,true);
 assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'takeNearbyTreasure');assert.equal(c.ui.treasureWindow.hidden,true);
});

test('Retro Auto take all waits for the pickup acknowledgement before showing the exit',()=>{
 const c=harness();c.state.lootingMode='all';c.state.privateState.inventory.weightPounds=0;c.state.dungeon={retroBattle:{},isCompleted:true};
 c.state.chestContents={chestId:'old'};
 c.receiveNearbyTreasure({type:'nearbyTreasureOpened',contents:{anchorId:'reward',isEventReward:true,sources:[{id:'reward',chest:false}],items:c.loot.items}});
 assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'takeNearbyTreasure');assert.equal(c.ui.treasureWindow.hidden,true);
 c.receiveNearbyTreasure({type:'nearbyTreasureUpdated',contents:{anchorId:'reward',isEventReward:true,sources:[],items:[]}});
 assert.equal(c.ui.treasureWindow.hidden,false);assert.equal(c.sent.length,1);
 c.closeTreasure();assert.equal(c.sent[1].type,'exitDungeon');
});

test('automatic ordinary pickup does not reopen an empty window',()=>{
 const c=harness();c.state.lootingMode='all';c.state.privateState.inventory.weightPounds=0;
 c.receiveNearbyTreasure({type:'nearbyTreasureOpened',contents:{anchorId:'drop',sources:[{id:'drop',chest:false}],items:c.loot.items}});
 assert.equal(c.sent.length,1);assert.equal(c.ui.treasureWindow.hidden,true);
 c.receiveNearbyTreasure({type:'nearbyTreasureUpdated',contents:{anchorId:'drop',sources:[],items:[],message:'Everything collected.'}});
 assert.equal(c.ui.treasureWindow.hidden,true);assert.equal(c.state.chestContents,null);
});
test('event collection leaves explicit completion feedback and closing Retro exits once',()=>{
 const c=harness();c.state.dungeon={retroBattle:{},isCompleted:true};
 c.receiveNearbyTreasure({type:'nearbyTreasureUpdated',contents:{anchorId:'reward',isEventReward:true,sources:[],items:[],message:'Everything collected. Your treasure is in your inventory.'}});
 assert.equal(c.ui.treasureWindow.hidden,false);assert.equal(c.ui.treasureItems.querySelectorAll().length,0);
 assert.match(c.ui.treasureCash.textContent,/Everything collected/);assert.equal(c.ui.treasureTake.disabled,true);
 assert.equal(c.ui.treasureTakeAll.disabled,true);
 for(const name of ['treasureHint','treasureColumns','treasureWeight','treasureTakeAll','treasureTake'])assert.equal(c.ui[name].hidden,true,name);
 assert.equal(c.ui.treasureClose.textContent,'Close & leave dungeon');assert.equal(c.sent.length,0);
 c.closeTreasure();c.closeTreasure();assert.equal(c.sent.filter(m=>m.type==='exitDungeon').length,1);
});

test('taking all remaining ordinary or event treasure closes the selection window',()=>{
 for(const isEventReward of [false,true])for(const takeAll of [false,true]){
  const c=harness();c.state.privateState.inventory.weightPounds=0;
  const contents={anchorId:'reward',isEventReward,sources:[{id:'reward',chest:false}],items:c.loot.items};
  c.receiveNearbyTreasure({type:'nearbyTreasureOpened',contents});
  if(takeAll)c.takeAllTreasure();else{c.ui.treasureItems.querySelectorAll()[0].value='10';c.takeTreasureSelection();}
  assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'takeNearbyTreasure');
  c.receiveNearbyTreasure({type:'nearbyTreasureUpdated',contents:{...contents,items:[],sources:[],message:'Everything collected.'}});
  assert.equal(c.ui.treasureWindow.hidden,true);assert.equal(c.state.chestContents,null);
  c.receiveNearbyTreasure({type:'nearbyTreasureUpdated',contents:{...contents,items:[],sources:[]}});
  assert.equal(c.ui.treasureWindow.hidden,true);assert.equal(c.sent.length,1);
 }
});

test('partial event pickup keeps remaining rewards selectable and restores controls after completion',()=>{
 const c=harness();c.state.dungeon={retroBattle:{},isCompleted:true};
 const contents={anchorId:'reward',isEventReward:true,sources:[{id:'reward',chest:false}],items:c.loot.items};
 c.receiveNearbyTreasure({type:'nearbyTreasureUpdated',contents:{...contents,items:[],sources:[]}});
 c.closeTreasure();c.state.dungeon=null;
 c.receiveNearbyTreasure({type:'nearbyTreasureOpened',contents});
 c.ui.treasureItems.querySelectorAll()[0].value='1';c.takeTreasureSelection();
 c.receiveNearbyTreasure({type:'nearbyTreasureUpdated',contents:{...contents,items:[{...c.loot.items[0],quantity:9}]}});
 assert.equal(c.ui.treasureWindow.hidden,false);assert.equal(c.ui.treasureItems.querySelectorAll()[0].value,'1');
 for(const name of ['treasureHint','treasureColumns','treasureWeight','treasureTakeAll','treasureTake'])assert.equal(c.ui[name].hidden,false,name);
 assert.equal(c.ui.treasureTakeAll.disabled,false);assert.equal(c.ui.treasureClose.textContent,'Close');
});
