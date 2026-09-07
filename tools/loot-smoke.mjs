import assert from 'node:assert/strict';
const base=process.argv[2]||'http://localhost:5080';
const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'Loot'+crypto.randomUUID().slice(0,5),password:crypto.randomUUID()})});
assert.ok(response.ok,'Test account creation');const setup=await response.json();
const ws=new WebSocket(base.replace(/^http/,'ws')+'/ws?session='+encodeURIComponent(setup.sessionToken)),messages=[];
ws.addEventListener('message',event=>messages.push(JSON.parse(event.data)));
async function wait(predicate,after=0){const until=Date.now()+45000;while(Date.now()<until){const recent=messages.slice(after),error=recent.find(m=>m.type==='error');if(error)throw Error(error.message);const found=recent.find(predicate);if(found)return found;await new Promise(resolve=>setTimeout(resolve,20));}throw Error('Timeout: '+messages.slice(after).map(m=>m.type).join(', '));}
async function command(request,predicate){const after=messages.length;ws.send(JSON.stringify(request));return wait(typeof predicate==='string'?m=>m.type===predicate:predicate,after);}
try{
const welcome=await wait(m=>m.type==='welcome');assert.equal(welcome.protocolVersion,56);
await command({type:'setGodMode',enabled:true},m=>m.type==='playerUpdated'&&m.player.id===welcome.playerId&&m.player.godMode);
for(let i=0;i<2;i++)await command({type:'configureInventoryItem',itemType:'rock',action:'take'},'configuredInventoryAdjusted');
await command({type:'mapFastTravel',targetType:'home',targetId:welcome.privateState.base.buildingId},'playerTeleported');
const home=await command({type:'enterDungeon',doorId:welcome.privateState.base.doorId},'dungeonEntered');
const chest=home.privateState.dungeon.furnishings.find(item=>item.properties.objectType==='storageChest');assert.ok(chest);
await command({type:'transferHomeStorage',chestId:chest.id,itemType:'rock',quantity:2,toStorage:false},'homeStorageUpdated');
async function pickupCycle(label){
const dropped=await command({type:'dropItem',itemType:'rock',quantity:2},'inventoryItemDropped');
const loot=dropped.privateState.loot.find(item=>item.items.length===1&&item.items[0].itemType==='rock'&&item.items[0].quantity===2);
assert.ok(loot,label+' created drop');
const opened=await command({type:'openLoot',lootId:loot.id},'lootOpened');assert.equal(opened.loot.items[0].quantity,2);
const partial=await command({type:'takeLootItems',lootId:loot.id,items:[{itemType:'rock',quantity:1}]},'lootItemsTaken');assert.equal(partial.loot.items[0].quantity,1);
const legacy=await command({type:'collectLoot',lootId:loot.id},'lootOpened');assert.equal(legacy.loot.items[0].quantity,1);
const taken=await command({type:'takeLootItems',lootId:loot.id,items:[{itemType:'rock',quantity:1}]},'lootItemsTaken');assert.equal(taken.loot,null);assert.equal(taken.privateState.inventory.items.find(i=>i.itemType==='rock').quantity,2);
console.log(label+': open window, partial take, remainder, legacy request, final pickup passed.');
}
await pickupCycle('Interior');await command({type:'exitDungeon'},'dungeonExited');await pickupCycle('Outdoors');
console.log('Live protocol 56 treasure pickup passed with a disposable test account.');
}finally{ws.close();}
