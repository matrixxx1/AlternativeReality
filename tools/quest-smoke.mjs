// Use an isolated fixture with water and a food merchant, never the live player world.
import assert from 'node:assert/strict';
const base=process.argv[2];
if(!base||new URL(base).hostname!=='127.0.0.1'||new URL(base).port!=='5082')throw Error('Use isolated localhost port 5082.');
const setupResponse=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'Q'+crypto.randomUUID().slice(0,7),password:crypto.randomUUID()})});
assert.ok(setupResponse.ok);const setup=await setupResponse.json();
const ws=new WebSocket(base.replace('http','ws')+'/ws?session='+setup.sessionToken),messages=[];
ws.addEventListener('message',e=>messages.push(JSON.parse(e.data)));
async function wait(type,after=0){const until=Date.now()+20000;while(Date.now()<until){const found=messages.slice(after).find(m=>m.type===type);if(found)return found;const error=messages.slice(after).find(m=>m.type==='error');if(error)throw Error(error.message);await new Promise(r=>setTimeout(r,20));}throw Error('Timed out: '+type);}
async function command(payload,type){const after=messages.length;ws.send(JSON.stringify(payload));return wait(type,after);}
try{
 const welcome=await wait('welcome');assert.equal(welcome.protocolVersion,58);
 assert.ok(!welcome.privateState?.quests?.some(q=>q.status==='offered'));
 await command({type:'setGodMode',enabled:true},'playerUpdated');
 const world=await (await fetch(base+'/api/world')).json();
 assert.ok(world.actors.some(a=>a.subtype==='fish'));assert.ok(world.actors.some(a=>a.subtype==='waterMonster'));
 assert.equal(world.baseEntities.filter(e=>e.properties?.subtype==='windFlag').length,1);
 assert.ok(world.weather.windSpeedKilometersPerHour>=8);
 const giver=world.actors.find(a=>a.id==='queue-giver');assert.ok(giver);
 await command({type:'teleport',x:giver.position.x,y:giver.position.y,godMode:true},'playerTeleported');
 const offer=(await command({type:'requestQuest',actorId:giver.id},'questInteraction')).interaction;
 assert.equal(offer.isOffer,true);assert.equal(offer.quest.deadlineUtc,null);assert.equal(offer.quest.rewardItems.length,3);
 const accepted=await command({type:'acceptQuest',questId:offer.quest.id},'questUpdated');
 assert.equal(accepted.quest.status,'active');assert.ok(Date.parse(accepted.quest.deadlineUtc)>Date.now()+19*60000);
 const quest=accepted.privateState.quests.find(q=>q.id===offer.quest.id);assert.ok(quest.nextStagePosition);assert.ok(quest.nextStageName);
 console.log(JSON.stringify({protocol:58,questOffer:true,acceptance:true,itemRewards:3,deadlineMinutes:quest.deliveryMinutes,questNavigation:true,windFlags:1,waterLife:true}));
}finally{ws.close();}
