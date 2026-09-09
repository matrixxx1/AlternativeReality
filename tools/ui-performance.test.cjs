const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`);assert.ok(start>=0,name);return source.slice(start,source.indexOf('\n  function ',start+1));}

test('server settings populate and save after the panel moves into its popup document',()=>{
  const fields=new Map(),sent=[];
  const node=()=>({value:'',replaceChildren(){},querySelector(selector){if(!fields.has(selector))fields.set(selector,node());return fields.get(selector);}});
  const ui=new Proxy({}, {get(target,key){return target[key]??=node();}});
  const dependencies={ui,document:{querySelector:()=>null},$:()=>null,renderModifierInputs(){},modifierValues:()=>({}),serverNowMs:()=>Date.now(),localDateTimeValue:()=>'',updateServerTimePreview(){},send:message=>sent.push(message),showToast:message=>assert.fail(message)};
  const populate=new Function(...Object.keys(dependencies),implementation('populateServerConfiguration')+';return populateServerConfiguration;')(...Object.values(dependencies));
  populate({items:[],movement:{baseSpeedMph:3,baseVisibilityMeters:50},events:{wantedSwatThreshold:7,retroBattlesIntervalHours:24,retroBattlesDurationMinutes:10}});
  assert.equal(fields.get('#wantedSwatThresholdConfig').value,'7');
  fields.get('#wantedSwatThresholdConfig').value='9';ui.saveServerEventsConfig.onclick();
  assert.equal(sent[0].type,'updateServerEvents');assert.equal(sent[0].wantedSwatThreshold,9);
});
test('unchanged server configuration does not rebuild forms or discard edits on movement updates',()=>{
  const state={},populated=[],ui={serverConfigWindow:{hidden:true}};
  const render=new Function('state','ui','populateServerConfiguration',implementation('renderServerConfiguration')+';return renderServerConfiguration;')(state,ui,config=>populated.push(config));
  const config={items:[{itemType:'pistol',damage:1}],movement:{baseSpeedMph:3},events:{serverTimeMode:'automatic'}};
  render(config);assert.equal(populated.length,0);ui.serverConfigWindow.hidden=false;
  render(config);for(let i=0;i<120;i++)render(structuredClone(config));assert.equal(populated.length,1);
  config.items[0].damage=2;render(config);assert.equal(populated.length,2);
  config.movement.baseSpeedMph=4;render(config);assert.equal(populated.length,3);
  config.events.serverTimeMode='manual';render(config);assert.equal(populated.length,4);
  render(null);assert.equal(populated.length,4);
});
function inventoryHarness(){
  let created=0,summaries=0;
  const nodes=[];
  function element(){created++;const node={children:[],dataset:{},classList:{toggle(){},add(){}},append(...children){this.children.push(...children);},replaceChildren(...children){this.children=children;},setAttribute(name,value){this[name]=value;},addEventListener(){}};nodes.push(node);return node;}
  const state={players:new Map([['me',{id:'me',equippedWeapon:'fist'}]]),playerId:'me',inventoryTab:'weapon',privateState:{serverConfiguration:{items:[]}}};
  const ui={inventory:element(),weaponSlotCount:element(),questSlotCount:element(),otherSlotCount:element()};
  const dependencies={state,ui,document:{createElement:element,querySelectorAll:()=>[]},updateBackpackSummary:()=>summaries++,candleActive:me=>!!me?.candleOn,
    hazardWeaponTypes:new Set(),weaponTypes:new Set(['fist','pistol']),equipmentSlotByItem:{hat:'hat'},createItemArt:element,dropControls:element,
    itemDisplayName:type=>type,stackWeight:item=>item.quantity*(item.unitWeightPounds||0),weightText:String,title:String,itemCategory:item=>item.itemType==='hat'?'other':'weapon',send(){}};
  const render=new Function(...Object.keys(dependencies),['equippedItem','inventoryRenderKey','renderInventory'].map(implementation).join('\n')+';return renderInventory;')(...Object.values(dependencies));
  return {state,ui,nodes,render,get created(){return created;},get summaries(){return summaries;}};
}
test('movement preserves inventory DOM and updates the live wallet/weight summary',()=>{
  const c=inventoryHarness(),inventory={items:[{itemType:'pistol',quantity:1}],weaponSlotsUsed:1};c.render(inventory);
  const children=c.ui.inventory.children,created=c.created;
  for(let i=0;i<120;i++){c.state.players.set('me',{id:'me',equippedWeapon:'fist',walletCents:i,position:{x:i,y:0}});c.render(inventory);}
  assert.equal(c.created,created);assert.equal(c.ui.inventory.children,children);assert.equal(c.summaries,121);
  c.render(structuredClone(inventory));assert.equal(c.ui.inventory.children,children);
});
test('inventory changes, equipment changes, tab changes and renamed items refresh controls',()=>{
  const c=inventoryHarness(),inventory={items:[{itemType:'pistol',quantity:1},{itemType:'hat',quantity:1}]};c.render(inventory);
  function changes(action){const previous=c.ui.inventory.children;action();c.render(inventory);assert.notEqual(c.ui.inventory.children,previous);}
  changes(()=>inventory.items[0].quantity++);
  changes(()=>c.state.players.get('me').equippedWeapon='pistol');
  assert.ok(c.nodes.some(node=>node['aria-label']==='Equip Pistol'&&node.disabled));
  changes(()=>c.state.inventoryTab='other');
  changes(()=>c.state.players.get('me').hatOn=true);
  changes(()=>c.state.privateState.serverConfiguration.items.push({itemType:'hat',displayName:'Sun hat'}));
});
test('speech bubbles reuse measured text until content, width, or font changes',()=>{
  let measurements=0;const state={},ctx={measureText(text){measurements++;return {width:text.length*7};}};
  const layout=new Function('state','ctx',implementation('speechLayout')+implementation('wrapChat')+';return speechLayout;')(state,ctx);
  const speech={chat:{username:'Explorer',message:'A longer message that wraps into several lines without changing between frames.'}};
  const initial=layout(speech,220),count=measurements;assert.ok(initial.lines.length>1);assert.ok(initial.width<=220);
  for(let i=0;i<300;i++)assert.equal(layout(speech,220),initial);
  assert.equal(measurements,count);
  speech.chat.message='Changed';assert.notEqual(layout(speech,220),initial);
  const changed=layout(speech,220);assert.notEqual(layout(speech,120),changed);
  const narrow=layout(speech,120);state.speechFontRevision=1;assert.notEqual(layout(speech,120),narrow);
});
test('dungeon visibility matches discovered cells and refreshes on new dungeon snapshots',()=>{
  const state={dungeon:{revealedCells:Array.from({length:5000},(_,i)=>`${i%100},${Math.floor(i/100)}`)}};
  const revealed=new Function('state',implementation('revealedAt')+';return revealedAt;')(state);
  for(let x=-3;x<310;x+=7)for(let y=-3;y<160;y+=7)assert.equal(revealed(x,y),state.dungeon.revealedCells.includes(`${Math.floor(x/3)},${Math.floor(y/3)}`));
  const cached=state.revealedCellIndex;assert.equal(revealed(1,1),true);assert.equal(state.revealedCellIndex,cached);
  state.dungeon={revealedCells:['-1,-1']};assert.equal(revealed(1,1),false);assert.equal(revealed(-1,-1),true);
  state.dungeon.revealedCells.push('0,0');assert.equal(revealed(1,1),true);
  state.dungeon=null;assert.equal(revealed(1,1),false);assert.equal(state.revealedCellIndex,null);
});
