// Disposable local fixture; never uses the active world's database.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn,spawnSync} from 'node:child_process';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'..'),data=path.join(root,'artifacts','northern-smoke-'+Date.now());
fs.mkdirSync(path.join(data,'world-cache'),{recursive:true});
fs.writeFileSync(path.join(data,'reality-location.json'),JSON.stringify({latitude:45.5,longitude:-122.5}));
const region={latitudeBand:45,longitudeBand:-123},position=(x,y)=>({region,x,y,z:0}),features=[];
for(let i=0;i<3;i++){
  const x=i*30,id='retro-house-'+i;
  features.push({id,kind:'building',position:position(x,20),geometry:[{x:x-5,y:15},{x:x+5,y:15},{x:x+5,y:25},{x:x-5,y:25},{x:x-5,y:15}],properties:{building:'yes',questItem:'true'},version:1,isBaseEntity:true});
  features.push({id:id+':door',kind:'door',position:position(x,14),geometry:[],properties:{buildingId:id,questItem:'true'},version:1,isBaseEntity:true});
}
const area={center:{latitude:45.5,longitude:-122.5,elevationMeters:0},sizeMeters:1000};
const key=crypto.createHash('sha256').update('v2:expansion-smoke:123:45:-123:45.500000:-122.500000:1000').digest('hex').toUpperCase().slice(0,20);
fs.writeFileSync(path.join(data,'world-cache',`world-${key}.json`),JSON.stringify({provider:'Retro smoke fixture',area,features,elevation:[{x:0,y:0,elevationMeters:0}],cachedAtUtc:new Date().toISOString()}));
const log=fs.openSync(path.join(data,'server.log'),'a'),base='http://127.0.0.1:5087';
const server=spawn('dotnet',[path.join(root,'src/AlternateEarth.Server/bin/Debug/net8.0/AlternateEarth.Server.dll'),`--Server:Urls=${base}`,`--Server:DataDirectory=${data}`,'--Reality:Id=expansion-smoke','--Reality:Seed=123','--Reality:SizeMeters=1000','--Weather:Url=http://127.0.0.1:1/','--Geo:OverpassUrl=http://127.0.0.1:1/','--Geo:RemoteElevation=false'],{cwd:path.join(root,'src/AlternateEarth.Server'),stdio:['ignore',log,log],windowsHide:true});
const keep=process.argv.includes('--keep-running');let ws;
try{
  for(let i=0;i<100;i++){try{if((await fetch(base)).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
  const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'NorthSmoke',password:'NorthSmoke-test-123!'})});
  assert.ok(response.ok);const setup=await response.json();
  const seeded=spawnSync('python',['-X','utf8','-c',"import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute(\"INSERT INTO Inventories(OwnerId,Slot,ItemType,Quantity,MetadataJson) VALUES(?,9999,'mapleSyrup',2,'{}')\",(sys.argv[2],)); c.commit()",path.join(data,'reality.db'),setup.characterId],{encoding:'utf8',windowsHide:true});assert.equal(seeded.status,0,seeded.stderr);
  ws=new WebSocket(base.replace('http','ws')+'/ws?session='+encodeURIComponent(setup.sessionToken));const messages=[];ws.addEventListener('message',e=>messages.push(JSON.parse(e.data)));
  async function wait(predicate,after=0){const until=Date.now()+20000;while(Date.now()<until){const recent=messages.slice(after),error=recent.find(m=>m.type==='error');if(error)throw Error(error.message);const found=recent.find(predicate);if(found)return found;await new Promise(r=>setTimeout(r,10));}throw Error('Timed out');}
  async function command(request,type){const after=messages.length;ws.send(JSON.stringify(request));return wait(m=>m.type===type,after);}
  const welcome=await wait(m=>m.type==='welcome');assert.equal(welcome.protocolVersion,62);
  const items=Object.fromEntries(welcome.privateState.serverConfiguration.items.map(i=>[i.itemType,i]));
  assert.ok(items.fist.damage<items.hockeyStick.damage&&items.hockeyStick.damage<items.sword.damage);
  assert.ok(items.knife.damage<items.iceSkate.damage&&items.iceSkate.damage<items.sword.damage);
  assert.equal(items.hockeyStick.ammoType,null);assert.equal(items.iceSkate.ammoType,null);
  assert.ok(items['recipe:hockeyStick']);assert.ok(items['recipe:mapleSyrup']);
  await command({type:'setGodMode',enabled:true},'playerUpdated');
  for(const itemType of ['hockeyStick','iceSkate'])await command({type:'setEquipment',slot:'weapon',itemType},'privateState');
  const after=messages.length;ws.send(JSON.stringify({type:'consumeItem',itemType:'mapleSyrup'}));
  const consumed=await wait(m=>m.privateState?.mapleSyrupUntilUtc,after);
  assert.equal(consumed.privateState.inventory.items.find(i=>i.itemType==='mapleSyrup').quantity,1);
  assert.ok(Date.parse(consumed.privateState.mapleSyrupUntilUtc)>Date.now()+290000);
  assert.ok(consumed.privateState.progression.visionMultiplier>welcome.privateState.progression.visionMultiplier);
  const html=await(await fetch(base)).text();assert.ok(html.includes('northern-exposure.js'));
  assert.ok((await(await fetch(base+'/northern-exposure.js')).text()).includes('drawCanadian'));
  const voting=await command({type:'startServerVote'},'inversionsUpdated');assert.ok(voting.inversions.vote);
  const canceled=await command({type:'cancelServerVote'},'inversionsUpdated');assert.equal(canceled.inversions.vote,null);assert.equal(canceled.inversions.active,null);assert.deepEqual(canceled.inversions.queued,[]);
  assert.ok((await command({type:'startServerVote'},'inversionsUpdated')).inversions.vote);
  await command({type:'setGodMode',enabled:false},'playerUpdated');
  const rejectionAfter=messages.length;ws.send(JSON.stringify({type:'cancelServerVote'}));
  for(let n=0;n<200&&!messages.slice(rejectionAfter).some(m=>m.type==='error');n++)await new Promise(r=>setTimeout(r,10));
  assert.match(messages.slice(rejectionAfter).find(m=>m.type==='error')?.message||'',/God mode must be enabled/);
  await command({type:'setGodMode',enabled:true},'playerUpdated');
  assert.equal((await command({type:'cancelServerVote'},'inversionsUpdated')).inversions.vote,null);
  console.log(JSON.stringify({result:'PASS',protocol:62,weapons:['hockeyStick','iceSkate'],mapleSyrupConsumed:1,temporaryVisionBoost:true,clientAssetHttp:200,godModeVoteCancellation:true,unauthorizedCancellationRejected:true}));
  ws.close();ws=null;
}finally{ws?.close();server.kill();fs.closeSync(log);}
