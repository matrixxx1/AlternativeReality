import assert from 'node:assert/strict';
const base=process.argv[2]||'http://127.0.0.1:5097';
const username=process.argv[3]||'QueueTest';
const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username,password:'Queue-test-only-482!'})});
assert.equal(response.ok,true);const login=await response.json();
const ws=new WebSocket(base.replace(/^http/,'ws')+'/ws?session='+encodeURIComponent(login.sessionToken));
const messages=[];let index=0;
ws.addEventListener('message',event=>messages.push(JSON.parse(event.data)));
async function wait(predicate){const deadline=Date.now()+15000;while(Date.now()<deadline){const match=messages.slice(index).find(predicate);if(match){index=messages.indexOf(match)+1;return match;}const error=messages.slice(index).find(m=>m.type==='error');if(error)throw Error(error.message);await new Promise(r=>setTimeout(r,50));}throw Error('Timed out: '+messages.slice(index).map(m=>m.type).join(','));}
function send(message){index=messages.length;ws.send(JSON.stringify(message));}
try{
  const welcome=await wait(m=>m.type==='welcome');assert.equal(welcome.protocolVersion,60);
  assert.ok((await fetch(base+'/combat-presentation.js')).ok);
  send({type:'setGodMode',enabled:true});await wait(m=>m.type==='playerUpdated'&&m.player.godMode);
  if(!welcome.incursion)send({type:'startIncursion',eventType:'northPark'});const event=welcome.incursion||(await wait(m=>m.type==='incursionUpdated'&&m.incursion)).incursion;
  assert.equal(event.name,'North park Hydra invasion');assert.equal(event.goal,50);
  const batch=await wait(m=>m.type==='actorsMoved'&&m.actors.some(a=>a.subtype==='kenHydra'));
  const ken=batch.actors.find(a=>a.subtype==='kenHydra');assert.ok(ken.gear);assert.ok(ken.level>=1&&ken.level<=10);
  send({type:'teleport',x:ken.position.x-1,y:ken.position.y,force:true});await wait(m=>m.type==='playerTeleported');
  send({type:'inspectActor',actorId:ken.id});const inspection=(await wait(m=>m.type==='actorInspected')).inspection;
  assert.equal(inspection.actor.name,'Ken Hydra');assert.equal(inspection.allegiance,'Foe');assert.ok(inspection.resistances.physical>=0);
  assert.ok(messages.some(m=>m.type==='chatSaid'&&m.chat.username.includes('Hydra'))||messages.some(m=>m.type==='incursionUpdated'));
  console.log(JSON.stringify({protocol:60,event:event.name,startingTarget:ken.name,inspection:true,damageTypes:inspection.attack.map(e=>e.type),actorLevel:ken.level}));
}finally{ws.close();}
