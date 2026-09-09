// Disposable local fixture; never uses the active world's database.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn,spawnSync} from 'node:child_process';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'..'),data=path.join(root,'artifacts','scuba-smoke-'+Date.now());
fs.mkdirSync(path.join(data,'world-cache'),{recursive:true});
fs.writeFileSync(path.join(data,'reality-location.json'),JSON.stringify({latitude:45.5,longitude:-122.5}));
const region={latitudeBand:45,longitudeBand:-123},position=(x,y)=>({region,x,y,z:0}),features=[];
for(let i=0;i<1;i++){
  const x=140+i*30,id='retro-house-'+i;
  features.push({id,kind:'building',position:position(x,20),geometry:[{x:x-5,y:15},{x:x+5,y:15},{x:x+5,y:25},{x:x-5,y:25},{x:x-5,y:15}],properties:{building:'yes',questItem:'true'},version:1,isBaseEntity:true});
  features.push({id:id+':door',kind:'door',position:position(x,14),geometry:[],properties:{buildingId:id,questItem:'true'},version:1,isBaseEntity:true});
}
features.push({id:'scuba-lake',kind:'water',position:position(0,0),geometry:[{x:-90,y:-90},{x:90,y:-90},{x:90,y:90},{x:-90,y:90},{x:-90,y:-90}],properties:{natural:'water',name:'Sapphire Lake'},version:1,isBaseEntity:true});
const area={center:{latitude:45.5,longitude:-122.5,elevationMeters:0},sizeMeters:1000};
const key=crypto.createHash('sha256').update('v3:expansion-smoke:123:45:-123:45.500000:-122.500000:1000').digest('hex').toUpperCase().slice(0,20);
fs.writeFileSync(path.join(data,'world-cache',`world-${key}.json`),JSON.stringify({provider:'Retro smoke fixture',area,features,elevation:[{x:0,y:0,elevationMeters:0}],cachedAtUtc:new Date().toISOString()}));
const log=fs.openSync(path.join(data,'server.log'),'a'),base='http://127.0.0.1:5088';
const server=spawn('dotnet',[path.join(root,'src/AlternateEarth.Server/bin/Debug/net8.0/AlternateEarth.Server.dll'),`--Server:Urls=${base}`,`--Server:DataDirectory=${data}`,'--Reality:Id=expansion-smoke','--Reality:Seed=123','--Reality:SizeMeters=1000','--Weather:Url=http://127.0.0.1:1/','--Geo:OverpassUrl=http://127.0.0.1:1/','--Geo:RemoteElevation=false'],{cwd:path.join(root,'src/AlternateEarth.Server'),stdio:['ignore',log,log],windowsHide:true});
const keep=process.argv.includes('--keep-running');let ws;
try{
  for(let i=0;i<100;i++){try{if((await fetch(base)).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
  const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'ScubaSmoke',password:'ScubaSmoke-test-123!'})});
  assert.ok(response.ok);const setup=await response.json();
  const seeded=spawnSync('python',['-c',"import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); items=[('scubaGear',1),('spearGun',1),('spear',40),('knife',1),('rifle',1),('bullet',20),('inflatableRaft',1)]; c.executemany('INSERT INTO Inventories(OwnerId,Slot,ItemType,Quantity,MetadataJson) VALUES(?,?,?,?,?)',[(sys.argv[2],9000+i,t,q,'{}') for i,(t,q) in enumerate(items)]); c.commit()",path.join(data,'reality.db'),setup.characterId],{encoding:'utf8',windowsHide:true});assert.equal(seeded.status,0,seeded.stderr);
  ws=new WebSocket(base.replace('http','ws')+'/ws?session='+encodeURIComponent(setup.sessionToken));const messages=[];ws.addEventListener('message',e=>messages.push(JSON.parse(e.data)));
  async function wait(predicate,after=0){const until=Date.now()+20000;while(Date.now()<until){const recent=messages.slice(after),error=recent.find(m=>m.type==='error');if(error)throw Error(error.message);const found=recent.find(predicate);if(found)return found;await new Promise(r=>setTimeout(r,10));}throw Error('Timed out');}
  async function command(request,type){const after=messages.length;ws.send(JSON.stringify(request));return wait(m=>m.type===type,after);}
  const welcome=await wait(m=>m.type==='welcome');
  const items=Object.fromEntries(welcome.privateState.serverConfiguration.items.map(i=>[i.itemType,i]));
  assert.ok(welcome.snapshot.baseEntities.some(e=>e.properties?.subtype==='wildCrop'));
  assert.ok(welcome.snapshot.actors.some(a=>a.subtype==='cow'));
  assert.equal(items.cheeseburger.nutrition.hunger,100);assert.equal(items.carrot.nutrition.hunger,7);assert.ok(items.fish.nutrition.raw);
  assert.equal(items.spearGun.damage,items.rifle.damage);assert.ok(items.spearGun.attackIntervalSeconds>items.rifle.attackIntervalSeconds);
  await command({type:'setGodMode',enabled:true},'playerUpdated');
  await command({type:'teleport',x:-88,y:0,godMode:true},'playerTeleported');
  await command({type:'setTravelMode',mode:'raft'},'privateState');
  for(let i=0;i<20;i++){await new Promise(r=>setTimeout(r,60));await command({type:'moveRequest',x:1,y:0,sequence:i+1},'playerMoved');}
  const entered=await command({type:'setTravelMode',mode:'scuba'},'privateState');
  assert.ok(entered.privateState.dungeon.underwater);assert.ok(entered.privateState.dungeon.actors.some(a=>a.subtype==='shark'));
  assert.ok(entered.privateState.dungeon.actors.some(a=>a.subtype==='octopus'));
  await command({type:'setEquipment',slot:'weapon',itemType:'spearGun'},'privateState');
  const target=entered.privateState.dungeon.actors.find(a=>a.subtype==='fish');
  const launch=await command({type:'attack',targetId:target.id,weapon:'spearGun'},'combatEvent');
  assert.equal(launch.combat.weapon,'spearGun');assert.equal(launch.combat.damage,0);
  await wait(m=>m.type==='combatEvent'&&m.combat.weapon==='spearImpact');
  const exited=await command({type:'exitDungeon'},'dungeonExited');assert.equal(exited.player.travelMode,'raft');assert.equal(exited.player.locationId,'outdoor');
  await command({type:'setTravelMode',mode:'scuba'},'privateState');
  await command({type:'setGodMode',enabled:false},'privateState');
  await new Promise(r=>setTimeout(r,1200));
  const final=await command({type:'requestPrivateState'},'privateState');assert.ok(final.privateState.dungeon.underwater);
  let surfaced;
  for(let i=0;i<25;i++){
    await new Promise(r=>setTimeout(r,65));
    surfaced=await command({type:'moveRequest',x:0,y:1,sequence:100+i},'playerMoved');
    if(surfaced.player.locationId==='outdoor')break;
  }
  assert.equal(surfaced.player.locationId,'outdoor');assert.equal(surfaced.player.travelMode,'raft');
  if(keep){await command({type:'setGodMode',enabled:true},'privateState');await command({type:'setTravelMode',mode:'scuba'},'privateState');}
  for(const asset of ['scuba.js','combat-effects.js','travel-icons.svg'])assert.equal((await fetch(base+'/'+asset)).status,200);
  console.log(JSON.stringify({result:'PASS',spearDelayedImpact:true,deepWaterPredators:true,surfaceMode:'raft',swimThroughTop:true,scubaAsset:true,wildFoods:true,rawMeat:true}));
  ws.close();ws=null;
}finally{ws?.close();if(keep){console.log(JSON.stringify({fixtureUrl:base,fixturePid:server.pid,data}));await new Promise(resolve=>{const hold=setInterval(()=>{},1000);process.once('SIGINT',()=>{clearInterval(hold);server.kill();resolve();});});}else server.kill();fs.closeSync(log);}
