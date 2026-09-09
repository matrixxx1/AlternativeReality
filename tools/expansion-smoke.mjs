// Disposable local fixture; never uses the active world's database.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'..'),data=path.join(root,'artifacts','expansion-smoke-'+Date.now());
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
const log=fs.openSync(path.join(data,'server.log'),'a'),base='http://127.0.0.1:5084';
const server=spawn('dotnet',[path.join(root,'src/AlternateEarth.Server/bin/Debug/net8.0/AlternateEarth.Server.dll'),`--Server:Urls=${base}`,`--Server:DataDirectory=${data}`,'--Reality:Id=expansion-smoke','--Reality:Seed=123','--Reality:SizeMeters=1000','--Weather:Url=http://127.0.0.1:1/','--Geo:OverpassUrl=http://127.0.0.1:1/','--Geo:RemoteElevation=false'],{cwd:path.join(root,'src/AlternateEarth.Server'),stdio:['ignore',log,log],windowsHide:true});
const keep=process.argv.includes('--keep-running');let ws;
try{
  for(let i=0;i<100;i++){try{if((await fetch(base)).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
  const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'EventSmoke',password:'EventSmoke-test-123!'})});
  assert.ok(response.ok,'Disposable test account setup succeeded');
  const setup=await response.json();
  ws=new WebSocket(base.replace('http','ws')+'/ws?session='+encodeURIComponent(setup.sessionToken));const messages=[];
  ws.addEventListener('message',e=>messages.push(JSON.parse(e.data)));
  async function wait(predicate,after=0,timeout=20000){const until=Date.now()+timeout;while(Date.now()<until){const recent=messages.slice(after),error=recent.find(m=>m.type==='error');if(error)throw Error(error.message);const found=recent.find(predicate);if(found)return found;await new Promise(r=>setTimeout(r,10));}throw Error('Timed out: '+messages.slice(after).map(m=>m.type).join(','));}
  async function command(request,type){const after=messages.length;ws.send(JSON.stringify(request));return wait(m=>m.type===type,after);}
  const welcome=await wait(m=>m.type==='welcome');
  await command({type:'setGodMode',enabled:true},'playerUpdated');
  const door=welcome.snapshot.baseEntities.find(e=>e.kind==='door'&&e.properties.buildingId!==welcome.privateState.base.buildingId);
  assert.ok(door);
  await command({type:'teleport',x:door.position.x,y:door.position.y,confirm:true},'playerTeleported');
  const voting=await command({type:'startServerVote'},'inversionsUpdated');
  assert.equal(voting.inversions.vote.options.length,4);assert.equal(voting.inversions.vote.options[0].id,'random');
  const preferred=['mech','flood','plants','smug','cards','turns','retro'];
  const option=preferred.map(id=>voting.inversions.vote.options.find(o=>o.id===id)).find(Boolean)||voting.inversions.vote.options[1];
  await command({type:'castServerVote',option:option.id},'inversionsUpdated');
  console.log('Vote open; awaiting one-minute expiry: '+option.name);
  const active=await wait(m=>m.type==='inversionsUpdated'&&m.inversions.active,0,75000);
  assert.equal(active.inversions.active.type,option.id);assert.equal(active.inversions.vote,null);
  if(['cards','turns','retro'].includes(option.id)) {
    const entered=await command({type:'enterEventDungeon'},'dungeonEntered');assert.equal(entered.dungeon.eventBattle.dungeonNumber,1);
    assert.equal(entered.dungeon.chests.length,0);
    await command({type:'exitDungeon'},'dungeonExited');
  }
  const next=await command({type:'startServerVote'},'inversionsUpdated');assert.ok(next.inversions.vote);assert.equal(next.inversions.active.id,active.inversions.active.id);
  console.log(JSON.stringify({result:'PASS',voteOptions:4,oneMinuteExpiry:true,active:option.name,base,data}));
  ws.close();ws=null;
  if(keep){console.log('Fixture retained for browser inspection. Login: EventSmoke / EventSmoke-test-123!');await new Promise(()=>{});}
}finally{ws?.close();server.kill();fs.closeSync(log);}
