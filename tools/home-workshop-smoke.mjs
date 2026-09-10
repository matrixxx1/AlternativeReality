// Disposable local fixture; never uses the active world's database.
// Browser checks use localhost, not the live game's 127.0.0.1 hostname, to isolate login cookies.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn,spawnSync} from 'node:child_process';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'..'),data=path.join(root,'artifacts','home-workshop-smoke-'+Date.now());
fs.mkdirSync(path.join(data,'world-cache'),{recursive:true});
fs.writeFileSync(path.join(data,'reality-location.json'),JSON.stringify({latitude:45.5,longitude:-122.5}));
const region={latitudeBand:45,longitudeBand:-123},position=(x,y)=>({region,x,y,z:0}),features=[];
for(let i=0;i<1;i++){
  const x=140+i*30,id='retro-house-'+i;
  features.push({id,kind:'building',position:position(x,20),geometry:[{x:x-15,y:10},{x:x+15,y:10},{x:x+15,y:30},{x:x-15,y:30},{x:x-15,y:10}],properties:{building:'yes',questItem:'true'},version:1,isBaseEntity:true});
  features.push({id:id+':door',kind:'door',position:position(x,14),geometry:[],properties:{buildingId:id,questItem:'true'},version:1,isBaseEntity:true});
}
features.push({id:'scuba-lake',kind:'water',position:position(0,0),geometry:[{x:-90,y:-90},{x:90,y:-90},{x:90,y:90},{x:-90,y:90},{x:-90,y:-90}],properties:{natural:'water',name:'Sapphire Lake'},version:1,isBaseEntity:true});
const area={center:{latitude:45.5,longitude:-122.5,elevationMeters:0},sizeMeters:1000};
const key=crypto.createHash('sha256').update('v4:expansion-smoke:123:45:-123:45.500000:-122.500000:1000').digest('hex').toUpperCase().slice(0,20);
fs.writeFileSync(path.join(data,'world-cache',`world-${key}.json`),JSON.stringify({provider:'Retro smoke fixture',area,features,elevation:[{x:0,y:0,elevationMeters:0}],cachedAtUtc:new Date().toISOString()}));
const log=fs.openSync(path.join(data,'server.log'),'a'),base='http://127.0.0.1:5090';
const server=spawn('dotnet',[path.join(root,'src/AlternateEarth.Server/bin/Debug/net8.0/AlternateEarth.Server.dll'),`--Server:Urls=${base}`,`--Server:DataDirectory=${data}`,'--Reality:Id=expansion-smoke','--Reality:Seed=123','--Reality:SizeMeters=1000','--Weather:Url=http://127.0.0.1:1/','--Geo:OverpassUrl=http://127.0.0.1:1/','--Geo:RemoteElevation=false'],{cwd:path.join(root,'src/AlternateEarth.Server'),stdio:['ignore',log,log],windowsHide:true});
const keep=process.argv.includes('--keep-running');let ws;let succeeded=false;
try{
  for(let i=0;i<100;i++){try{if((await fetch(base)).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
  const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'HomeSmoke',password:'HomeSmoke-test-123!'})});
  assert.ok(response.ok,response.ok?undefined:await response.text());const setup=await response.json();
  const seedScript=`import sqlite3,sys,json
c=sqlite3.connect(sys.argv[1]); character=sys.argv[2]
account=c.execute('SELECT AccountId FROM AccountCharacters WHERE Id=?',(character,)).fetchone()[0]
owner='home-items:expansion-smoke:'+account
items=[('dirtyWater',12,'Crude'),('cloth',8,'Crude'),('paper',5,None),('waterFilter',1,None)]+[(v,1,None) for v in ['skateboard','bike','eBike','dirtBike','motorcycle','inflatableRaft','ufo','swimmies','scubaGear']]
c.executemany('INSERT INTO Inventories(OwnerId,Slot,ItemType,Quantity,MetadataJson) VALUES(?,?,?,?,?)',[(owner,100+i,t,q,json.dumps({'quality':quality})) for i,(t,q,quality) in enumerate(items)])
c.executemany('INSERT INTO Inventories(OwnerId,Slot,ItemType,Quantity,MetadataJson) VALUES(?,?,?,?,?)',[(character,800+i,'recipe:'+r,2,'{}') for i,r in enumerate(['water','waterFilter','bullet','knife','skateboard'])])
c.executemany('INSERT INTO Inventories(OwnerId,Slot,ItemType,Quantity,MetadataJson) VALUES(?,?,?,?,?)',[(character,900,'dirtyWater',3,json.dumps({'quality':'Crude'})),(character,901,'cloth',1,json.dumps({'quality':'Crude'}))])
c.execute('INSERT OR REPLACE INTO CraftingProgress(RealityId,PlayerId,Experience) VALUES(?,?,?)',('expansion-smoke',character,5000));c.commit()`;
  const seeded=spawnSync('python',['-c',seedScript,path.join(data,'reality.db'),setup.characterId],{encoding:'utf8',windowsHide:true});assert.equal(seeded.status,0,seeded.stderr);
  ws=new WebSocket(base.replace('http','ws')+'/ws?session='+encodeURIComponent(setup.sessionToken));const messages=[];
  ws.addEventListener('message',e=>messages.push(JSON.parse(e.data)));
  async function wait(predicate,after=0){const until=Date.now()+20000;while(Date.now()<until){const recent=messages.slice(after),error=recent.find(m=>m.type==='error');if(error)throw Error(error.message);const found=recent.find(predicate);if(found)return found;await new Promise(r=>setTimeout(r,10));}throw Error('Timed out');}
  async function command(request,type){const after=messages.length;ws.send(JSON.stringify(request));return wait(m=>m.type===type,after);}
  const welcome=await wait(m=>m.type==='welcome');assert.equal(welcome.privateState.recipeBook.find(r=>r.id==='water').level,1);
  await command({type:'setGodMode',enabled:true},'privateState');
  const home=welcome.privateState.base;await command({type:'teleport',x:home.position.x,y:home.position.y,godMode:true},'playerTeleported');
  const entered=await command({type:'enterDungeon',doorId:home.doorId},'dungeonEntered');
  const interior=entered.privateState.dungeon;assert.ok(interior.garage);assert.equal(interior.garage.vehicles.length,9);
  for(const station of ['stove','sewingTable','weaponsBench','garageWorkbench'])assert.ok(interior.furnishings.some(i=>i.properties.objectType===station),station);
  const stove=interior.furnishings.find(i=>i.properties.objectType==='stove');
  const crafting=await command({type:'requestCrafting',furnitureId:stove.id},'craftingOpened');
  assert.equal(crafting.crafting.recipes.find(r=>r.id==='water').successChance,.35);
  assert.equal(crafting.crafting.recipes.find(r=>r.id==='water').maximumCraftable,5);
  assert.ok(crafting.crafting.recipes.every(r=>r.maximumCraftable>0));
  assert.equal(entered.privateState.recipeBook.find(r=>r.id==='water').maximumCraftable,5);
  for(const upgradeId of ['spiceRack','waterPurifier','reloadingSet','welder'])await command({type:'buyHomeUpgrade',upgradeId},'homeWorkshopUpdated');
  const purified=await command({type:'useWaterPurifier',replaceFilter:false},'homeWorkshopUpdated');
  assert.equal(purified.privateState.homeWorkshop.progress.filterUsesRemaining,49);assert.equal(purified.sound,'pour');
  assert.equal(purified.privateState.homeItemStorage.items.find(i=>i.itemType==='purifiedWater').quantity,1);
  assert.equal(purified.privateState.recipeBook.find(r=>r.id==='water').bonuses.ingredientQuality,-.15);
  for(const asset of ['garage.js','recipe-book.js','home-workshop.js','game-audio.js','audio/cow.mp3','audio/chicken.mp3'])assert.equal((await fetch(base+'/'+asset)).status,200);
  fs.writeFileSync(path.join(data,'verified-home.json'),JSON.stringify(purified.privateState,null,2));
  console.log(JSON.stringify({result:'PASS',garageVehicles:9,defaultStations:4,starterWater:true,qualityAdjustedChance:true,permanentUpgrades:4,purifierUsesRemaining:49,recipeTabs:true}));
  succeeded=true;ws.close();ws=null;
}catch(error){console.error(error);throw error;}finally{ws?.close();if(keep&&succeeded){console.log(JSON.stringify({fixtureUrl:base.replace("127.0.0.1","localhost"),fixturePid:server.pid,data}));await new Promise(resolve=>{const hold=setInterval(()=>{},1000);process.once('SIGINT',()=>{clearInterval(hold);server.kill();resolve();});});}else server.kill();fs.closeSync(log);}
