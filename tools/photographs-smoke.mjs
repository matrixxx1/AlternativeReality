// Disposable local fixture; never uses the active world's database.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn,spawnSync} from 'node:child_process';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'..'),data=path.join(root,'artifacts','photographs-smoke-'+Date.now());
fs.mkdirSync(path.join(data,'world-cache'),{recursive:true});
fs.writeFileSync(path.join(data,'reality-location.json'),JSON.stringify({latitude:45.5,longitude:-122.5}));
const region={latitudeBand:45,longitudeBand:-123},position=(x,y)=>({region,x,y,z:0}),features=[];
for(let i=0;i<3;i++){
  const x=i*30,id='retro-house-'+i;
  features.push({id,kind:'building',position:position(x,20),geometry:[{x:x-5,y:15},{x:x+5,y:15},{x:x+5,y:25},{x:x-5,y:25},{x:x-5,y:15}],properties:{building:'yes',questItem:'true'},version:1,isBaseEntity:true});
  features.push({id:id+':door',kind:'door',position:position(x,14),geometry:[],properties:{buildingId:id,questItem:'true'},version:1,isBaseEntity:true});
}
const area={center:{latitude:45.5,longitude:-122.5,elevationMeters:0},sizeMeters:1000};
const key=crypto.createHash('sha256').update('v3:expansion-smoke:123:45:-123:45.500000:-122.500000:1000').digest('hex').toUpperCase().slice(0,20);
fs.writeFileSync(path.join(data,'world-cache',`world-${key}.json`),JSON.stringify({provider:'Retro smoke fixture',area,features,elevation:[{x:0,y:0,elevationMeters:0}],cachedAtUtc:new Date().toISOString()}));
const log=fs.openSync(path.join(data,'server.log'),'a'),base='http://127.0.0.1:5085';
const server=spawn('dotnet',[path.join(root,'src/AlternateEarth.Server/bin/Debug/net8.0/AlternateEarth.Server.dll'),`--Server:Urls=${base}`,`--Server:DataDirectory=${data}`,'--Reality:Id=expansion-smoke','--Reality:Seed=123','--Reality:SizeMeters=1000','--Weather:Url=http://127.0.0.1:1/','--Geo:OverpassUrl=http://127.0.0.1:1/','--Geo:RemoteElevation=false'],{cwd:path.join(root,'src/AlternateEarth.Server'),stdio:['ignore',log,log],windowsHide:true});
const keep=process.argv.includes('--keep-running');let ws;
try{
  for(let i=0;i<100;i++){try{if((await fetch(base)).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
  const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'PhotoSmoke',password:'PhotoSmoke-test-123!'})});
  assert.ok(response.ok);const setup=await response.json();
  const seeded=spawnSync('python',['-X','utf8','-c',"import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute(\"INSERT INTO Inventories(OwnerId,Slot,ItemType,Quantity,MetadataJson) VALUES(?,9999,'film',5,'{}')\",(sys.argv[2],)); c.commit()",path.join(data,'reality.db'),setup.characterId],{encoding:'utf8',windowsHide:true});assert.equal(seeded.status,0,seeded.stderr);
  ws=new WebSocket(base.replace('http','ws')+'/ws?session='+encodeURIComponent(setup.sessionToken));const messages=[];ws.addEventListener('message',e=>messages.push(JSON.parse(e.data)));
  async function wait(predicate,after=0){const until=Date.now()+20000;while(Date.now()<until){const recent=messages.slice(after),error=recent.find(m=>m.type==='error');if(error)throw Error(error.message);const found=recent.find(predicate);if(found)return found;await new Promise(r=>setTimeout(r,10));}throw Error('Timed out');}
  async function command(request,type){const after=messages.length;ws.send(JSON.stringify(request));return wait(m=>m.type===type,after);}
  await wait(m=>m.type==='welcome');await command({type:'setGodMode',enabled:true},'playerUpdated');await command({type:'setEquipment',slot:'weapon',itemType:'camera'},'privateState');
  const png='iVBORw0KGgoAAAANSUhEUgAAAIAAAABgCAIAAABaGO0eAAAAxElEQVR4nO3RQQ0AIAzAwClBBJrwrwEZe/SSCmhyc97VYrN+EA8AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2n2ZccHwKLyDoQAAAABJRU5ErkJggg==';
  const shot=await command({type:'photograph',thumbnailDataUrl:'data:image/png;base64,'+png},'questUpdated');const print=shot.privateState.inventory.items.find(i=>i.photograph);assert.ok(print);assert.equal(shot.privateState.inventory.items.find(i=>i.itemType==='film').quantity,4);
  assert.ok(!JSON.stringify(shot).includes(png));const url=base+print.photograph.thumbnailUrl;
  assert.equal((await fetch(url)).status,401);const image=await fetch(url,{headers:{cookie:response.headers.get('set-cookie').split(';')[0]}});assert.equal(image.status,200);assert.equal(image.headers.get('content-type'),'image/png');assert.deepEqual(Buffer.from(await image.arrayBuffer()),Buffer.from(png,'base64'));
  console.log(JSON.stringify({result:'PASS',photoItem:print.itemType,thumbnailHttp:200,anonymousHttp:401,data,base}));
  ws.close();ws=null;if(keep){console.log('PhotoSmoke / PhotoSmoke-test-123!');await new Promise(()=>{});}
}finally{ws?.close();server.kill();fs.closeSync(log);}
