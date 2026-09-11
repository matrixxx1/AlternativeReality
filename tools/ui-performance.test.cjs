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
    vehicleTypes:new Set(),hazardWeaponTypes:new Set(),weaponTypes:new Set(['fist','pistol']),equipmentSlotByItem:{hat:'hat'},createItemArt:element,dropControls:element,
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
test('God Mode displays virtual weapons and ammunition and refreshes when toggled',()=>{
  const c=inventoryHarness(),inventory={items:[{itemType:'pistol',quantity:2}],weaponSlotsUsed:1};
  c.state.privateState.godModeLoadout=[{itemType:'pistol',quantity:1},{itemType:'rifle',quantity:1},{itemType:'bullet',quantity:1}];
  const text=node=>[node.textContent||'',...(node.children||[]).map(text)].join(' ');
  c.render(inventory);assert.doesNotMatch(text(c.ui.inventory),/Equip Rifle|bullet ×1/);
  c.state.players.get('me').godMode=true;c.render(inventory);
  assert.match(text(c.ui.inventory),/Rifle/);assert.match(text(c.ui.inventory),/bullet ×1/);
  assert.match(text(c.ui.inventory),/Pistol ×2/);
  assert.deepEqual(inventory.items,[{itemType:'pistol',quantity:2}]);
  c.state.players.get('me').godMode=false;c.render(inventory);
  assert.doesNotMatch(text(c.ui.inventory),/Rifle|bullet ×1/);
});

test('God Mode exposes all travel buttons without removing terrain restrictions',()=>{
  const buttons=['ufo','swim','scuba','raft','bike'].map(mode=>({dataset:{mode},classList:{toggle(){}},setAttribute(){}}));
  const player={godMode:true},state={playerId:'me',players:new Map([['me',player]]),privateState:{inventory:{items:[]}}};
  let synced=0;
  const update=new Function('state','document','syncTravelButtonState',implementation('updateMode')+';return updateMode;')(state,{querySelectorAll:()=>buttons},()=>synced++);
  update('walk');assert.ok(buttons.every(button=>!button.hidden));assert.equal(synced,5);
  player.godMode=false;update('walk');assert.ok(buttons.every(button=>button.hidden));
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

test('God Mode bypasses fog without erasing exploration, and disabling it restores fog',()=>{
 const fog=new Set(['5:5']),state={outdoorFog:fog,playerId:'me',players:new Map(),dungeon:{revealedCells:['0,0']}},me={godMode:true,position:{x:0,y:0}};
 let fills=0;const ctx=new Proxy({fill(){fills++;}},{get:(target,key)=>target[key]||(()=>{}),set:(target,key,value)=>(target[key]=value,true)});
 const draw=new Function('state','ctx','toScreen','fogCellSize','fogKey',implementation('drawOutdoorFog')+';return drawOutdoorFog;')(state,ctx,p=>p,10,(x,y)=>`${x}:${y}`);
 const revealed=new Function('state',implementation('revealedAt')+';return revealedAt;')(state);
 state.players.set('me',me);draw({minX:40,maxX:70,minY:40,maxY:70},me);assert.equal(fills,0);assert.ok(fog.has('5:5'));assert.equal(revealed(99,99),true);
 me.godMode=false;draw({minX:40,maxX:70,minY:40,maxY:70},me);assert.equal(fills,1);assert.equal(revealed(99,99),false);assert.equal(revealed(1,1),true);
});
test('World and Player Testing replace the God Mode switch in server configuration',()=>{
 const html=fs.readFileSync('src/AlternateEarth.Client2D/index.html','utf8');
 const stats=html.slice(html.indexOf('<section id="progressionWindow"'),html.indexOf('<section id="activeEventsPanel"'));
 const world=html.slice(html.indexOf('data-server-config-page="world"'),html.indexOf('data-server-config-page="player"'));
 const player=html.slice(html.indexOf('data-server-config-page="player"'),html.indexOf('data-server-config-page="vehicles"'));
 const actions=html.slice(html.indexOf('<section id="actionMenu"'),html.indexOf('<section id="serverConfigWindow"'));
 assert.doesNotMatch(stats,/id="godMode"|id="serverConfigButton"/);
 assert.doesNotMatch(html,/id="godMode"|God Mode: Off|God Mode: On/);assert.match(world,/data-test-character="npc"/);assert.match(world,/id="clearTestCharactersButton"/);assert.match(actions,/id="teleportButton"/);
 for(const key of ['cantTeleport','canDie','consumesAmmo','consumesCraftingMaterials','consumesAirWhenSwimming','consumesStaminaWhenMoving','mustMeetCraftingMaterialRequirements','canFailWhenCrafting','doesNormalDamage','getsNormalMovementSpeed','consumesVehicleFuel','obeysBackpackWeightLimit'])assert.match(player,new RegExp(`data-player-testing="${key}"`));
 assert.doesNotMatch(actions,/data-test-character|id="clearTestCharactersButton"/);
 assert.ok(html.indexOf('id="openProgression"')<html.indexOf('id="serverConfigButton"'));assert.ok(html.indexOf('id="serverConfigButton"')<html.indexOf('id="openInventoryButton"'));
});

test('Performance and Questionable Errands launch as floating minimizable windows beside Chat',()=>{
 const html=fs.readFileSync('src/AlternateEarth.Client2D/index.html','utf8'),css=fs.readFileSync('src/AlternateEarth.Client2D/styles.css','utf8');
 const actions=html.slice(html.indexOf('<div class="character-actions"'),html.indexOf('<div id="dungeonComplete"'));
 assert.ok(actions.indexOf('id="openChatButton"')<actions.indexOf('id="openErrandsButton"'));
 assert.ok(actions.indexOf('id="openErrandsButton"')<actions.indexOf('id="openPerformanceButton"'));
 assert.match(html,/id="performancePanel"[^>]*panel-popup[^>]*data-popup="Performance"[^>]*data-collapsible="Performance"/);
 assert.match(source,/\['performancePanel','openPerformanceButton','closePerformanceButton'\]/);
 assert.match(source,/\['adventurePanel','openErrandsButton','closeErrandsButton'\]/);
 assert.doesNotMatch(source,/panel\.classList\.contains\('panel-collapsed'\)\)return/);
 assert.match(css,/\.panel-popup\.panel-collapsed > \.panel-drag-handle[^}]*display:flex !important/);
});

test('server configuration gear matches the surrounding stats buttons',()=>{
 const css=fs.readFileSync('src/AlternateEarth.Client2D/styles.css','utf8');
 assert.match(css,/#serverConfigButton \{ width:38px;height:38px;padding:6px/);
});

test('Player Testing saves and renders server-confirmed rules after moving into a popup document',()=>{
 const inputs=[{dataset:{playerTesting:'canDie'},checked:true},{dataset:{playerTesting:'consumesAmmo'},checked:false}],status={},sent=[];
 const c=require('node:vm').createContext({ui:{serverConfigWindow:{querySelectorAll:()=>inputs,querySelector:selector=>selector==='#playerTestingStatus'?status:null}},$ :()=>null,send:m=>sent.push(m),Object});
 require('node:vm').runInContext(implementation('savePlayerTesting')+implementation('renderPlayerTesting'),c);
 c.savePlayerTesting();assert.deepEqual(JSON.parse(JSON.stringify(sent[0])),{type:'updatePlayerTesting',settings:{canDie:true,consumesAmmo:false}});assert.match(status.textContent,/Saving/);
 c.renderPlayerTesting(sent[0].settings);assert.equal(inputs[0].checked,true);assert.equal(inputs[1].checked,false);assert.match(status.textContent,/1 testing bypass is active/);
 c.renderPlayerTesting({canDie:true,consumesAmmo:true});assert.ok(inputs.every(input=>input.checked));assert.equal(status.textContent,'Normal gameplay is active.');
});
test('Effects combines active meal bonuses with other effects and removes expired meals',()=>{
 const vm=require('node:vm'),nodes=new Map(),node=()=>({style:{}}),get=id=>{if(!nodes.has(id))nodes.set(id,node());return nodes.get(id);};let now=1000;
 const ui=new Proxy({}, {get:(t,k)=>t[k]??=node()}),state={privateState:{}},c=vm.createContext({state,ui,document:{getElementById:get},Date:{now:()=>now,parse:Date.parse},renderActiveEvents(){},gpsText:()=>'',isSleeping:()=>false,candleActive:()=>false,countdown:()=>'',title:s=>s});
 vm.runInContext(fs.readFileSync('src/AlternateEarth.Client2D/survival.js','utf8'),c);vm.runInContext(source.split('\n').find(line=>line.startsWith('  function updateTelemetry(')),c);
 const me={godMode:true,locationId:'outdoor',position:{x:0,y:0,z:0},water:10,bodyHeat:50,survival:{buffs:[{amount:3,stat:'strength',endsAtUtc:new Date(61000).toISOString()},{amount:9,stat:'luck',endsAtUtc:new Date(500).toISOString()}]}};
 c.updateTelemetry(me);assert.match(ui.effects.textContent,/\+3 strength.*God Mode/);assert.doesNotMatch(ui.effects.textContent,/luck/);
 now=62000;c.updateTelemetry(me);assert.doesNotMatch(ui.effects.textContent,/strength/);assert.match(ui.effects.textContent,/God Mode/);
 assert.doesNotMatch(fs.readFileSync('src/AlternateEarth.Client2D/index.html','utf8'),/foodBuffValue|Meal effects/);
});
test('World Testing placement waits for a map click and cancels safely',()=>{
 const nodes={'#cancelGodPlacement':{}},sent=[],state={playerId:'me',players:new Map([['me',{godMode:true}]]),godPlacement:null},ui={actionMenu:{}},c={state,ui,$:id=>nodes[id],controlsPaused:()=>false,stopTravel(){},closeServerConfigWindow(){},showToast(){},window:{focus(){}},send:m=>sent.push(m),requestTeleport:p=>sent.push({type:'teleport',...p})};
 require('node:vm').runInNewContext(source.slice(source.indexOf('  function cancelGodPlacement('),source.indexOf("  for(const button of ui.serverConfigWindow.querySelectorAll('[data-test-character]'))")),c);
 c.beginGodPlacement('npc');assert.equal(sent.length,0);assert.equal(nodes['#cancelGodPlacement'].hidden,false);assert.equal(c.placeGodTool({x:12,y:34}),true);assert.equal(sent[0].kind,'npc');assert.equal(sent[0].x,12);assert.equal(c.placeGodTool({x:1,y:1}),false);
 c.beginGodPlacement('teleport');c.cancelGodPlacement();assert.equal(c.placeGodTool({x:1,y:1}),false);assert.equal(sent.length,1);
 c.beginGodPlacement('animal');state.players.get('me').godMode=false;c.placeGodTool({x:1,y:1});assert.equal(sent.length,2);assert.equal(sent[1].kind,'animal');
});

test('vote administration and rebuild live exclusively in World Testing',()=>{
 const html=fs.readFileSync('src/AlternateEarth.Client2D/index.html','utf8');const world=html.slice(html.indexOf('data-server-config-page="world"'),html.indexOf('data-server-config-page="player"'));
 for(const marker of ['data-world-event="vote"','id="cancelServerVoteButton"','id="rebuildButton"','id="rebuildMode"','id="realityInfo"']){assert.ok(world.includes(marker));assert.equal(html.split(marker).length,2);}
 assert.ok(!html.includes('data-server-config-tab="rebuild"'));
});
test('World Testing vote controls follow active votes in the adopted server popup',()=>{
 const controls={'#cancelServerVoteButton':{},'[data-world-event="vote"]':{}},state={playerId:'me',players:new Map([['me',{godMode:false}]]),godTogglePending:null,inversions:{vote:null}},c={state,ui:{serverConfigWindow:{querySelector:s=>controls[s]}}};
 require('node:vm').runInNewContext(source.slice(source.indexOf('  function syncGodVoteControls('),source.indexOf("  ui.serverConfigWindow.querySelector('#cancelServerVoteButton').addEventListener")),c);
 c.syncGodVoteControls();assert.ok(controls['#cancelServerVoteButton'].disabled);assert.equal(controls['[data-world-event="vote"]'].disabled,false);
 state.inversions.vote={round:2};c.syncGodVoteControls();assert.equal(controls['#cancelServerVoteButton'].disabled,false);assert.equal(controls['[data-world-event="vote"]'].disabled,true);
});
