const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`);assert.ok(start>=0,name);return source.slice(start,source.indexOf('\n  function ',start+1));}
function harness(){
  function element(){return{children:[],dataset:{},listeners:{},classList:{contains:()=>false},append(...children){this.children.push(...children);},replaceChildren(){this.children=[];},setAttribute(){},addEventListener(type,listener){this.listeners[type]=listener;},focus(){}};}
  const heading=element(),ui={treasureItems:element(),treasureInventory:element(),treasureCash:element(),treasureWeight:element(),treasureWarning:element(),treasureTake:element(),treasureClose:element(),treasureWindow:{hidden:true,scrollHeight:500,classList:{contains:()=>false},querySelector:()=>heading}};
  ui.treasureItems.querySelectorAll=()=>ui.treasureItems.children.map(row=>row.children.at(-1));
  const state={players:new Map([['me',{}]]),playerId:'me',loot:new Map(),chests:new Map(),privateState:{inventory:{items:[{itemType:'rock',quantity:98,category:'weapon',unitWeightPounds:.5}],weightPounds:49,maximumWeightPounds:50}}};
  const sent=[],c=vm.createContext({ui,state,sent,document:{createElement:element},itemDisplayName:type=>type,createItemArt:element,stackWeight:item=>item.quantity*item.unitWeightPounds,weightText:w=>w+' lb',effectiveCarryingCapacity:(_,capacity)=>capacity,send:message=>sent.push(message),innerWidth:1200,innerHeight:800,viewportWidth:()=>900,floatPanel:()=>{}});
  for(const name of ['closeTreasure','openLootTreasure','openTreasure','refreshTreasure','renderTreasureInventory','treasureSelection','treasureCapacity','updateTreasureWeight','sendTreasureTake','tryAutomaticTreasure','recoverAutomaticTreasure','takeAllTreasure','takeTreasureSelection'])vm.runInContext(implementation(name),c);
  c.loot={id:'loot',moneyCents:123,items:[{itemType:'rock',quantity:10,category:'weapon',unitWeightPounds:.5}]};state.loot.set(c.loot.id,c.loot);
  return c;
}
test('opening treasure takes nothing; quantities block overweight pickups but allow a smaller selection',()=>{
  const c=harness();c.openLootTreasure(c.loot);assert.equal(c.sent.length,0);
  const input=c.ui.treasureItems.querySelectorAll()[0];assert.equal(input.value,'0');input.value='10';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,true);
  c.takeTreasureSelection();assert.equal(c.sent.length,0);input.value='1';c.takeTreasureSelection();
  assert.equal(c.sent[0].type,'takeLootItems');assert.equal(c.sent[0].items[0].quantity,1);
});
test('drop controls send exact quantities and an inventory refresh preserves the treasure selection',()=>{
  const c=harness();c.openLootTreasure(c.loot);c.ui.treasureItems.querySelectorAll()[0].value='4';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,true);
  const controls=c.ui.treasureInventory.children[0].children.at(-1);controls.children[0].value='10';controls.children[1].listeners.click();assert.equal(c.sent[0].type,'dropItem');assert.equal(c.sent[0].quantity,10);
  c.state.privateState.inventory.items[0].quantity=88;c.state.privateState.inventory.weightPounds=44;c.refreshTreasure();
  assert.equal(c.ui.treasureItems.querySelectorAll()[0].value,'4');assert.equal(c.ui.treasureTake.disabled,false);
});
test('cash can be collected alone and an existing item stack does not require a new slot',()=>{
  const c=harness();c.state.privateState.inventory.maximumWeaponSlots=1;c.openLootTreasure(c.loot);c.takeTreasureSelection();assert.equal(c.sent[0].items.length,0);
  c.ui.treasureItems.querySelectorAll()[0].value='1';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,false);
  c.openLootTreasure({...c.loot,items:[{itemType:'knife',quantity:1,category:'weapon',unitWeightPounds:.1}]});c.ui.treasureItems.querySelectorAll()[0].value='1';c.updateTreasureWeight();assert.equal(c.ui.treasureTake.disabled,true);assert.match(c.ui.treasureWarning.textContent,/slots/);
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
test('Take all looting falls back to item selection when weight or slots are full',()=>{
  for(const limit of ['weight','slots']){
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
