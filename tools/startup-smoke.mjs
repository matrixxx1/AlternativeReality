// Run against an isolated local server, never the user's active world.
// node tools/startup-smoke.mjs http://127.0.0.1:5081
import assert from 'node:assert/strict';
const base=process.argv[2];
if(!base||!['localhost','127.0.0.1'].includes(new URL(base).hostname)||new URL(base).port!=='5081')throw Error('Use an isolated localhost server on port 5081.');
const setupResponse=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'Load'+crypto.randomUUID().slice(0,5),password:crypto.randomUUID()})});
assert.ok(setupResponse.ok,'Test account setup failed');const setup=await setupResponse.json();
async function visit(label){
  const started=performance.now(),messages=[],ws=new WebSocket(base.replace('http','ws')+'/ws?session='+setup.sessionToken);
  ws.addEventListener('message',event=>messages.push({...JSON.parse(event.data),received:performance.now()}));
  async function wait(predicate,after=0){const until=performance.now()+30000;while(performance.now()<until){const message=messages.slice(after).find(predicate);if(message)return message;const error=messages.slice(after).find(m=>m.type==='error');if(error)throw Error(error.message);await new Promise(resolve=>setTimeout(resolve,10));}throw Error('Readiness test timed out');}
  try{
    const welcome=await wait(m=>m.type==='welcome');assert.equal(welcome.protocolVersion,56);
    assert.equal(messages[0].type,'sessionLoading');assert.ok(messages.filter(m=>m.type==='sessionLoading').length>=2);
    const probeStarted=performance.now();ws.send(JSON.stringify({type:'ping',id:42}));
    const pong=await wait(m=>m.type==='pong'&&m.id===42);
    const commandStarted=performance.now();ws.send(JSON.stringify({type:'setActionMode',mode:'neutral'}));
    const ack=await wait(m=>m.type==='actionModeChanged');
    console.log(JSON.stringify({label,loadingIndicatorMs:Math.round(messages[0].received-started),welcomeMs:Math.round(welcome.received-started),readyProbeMs:Math.round(pong.received-probeStarted),commandResponseMs:Math.round(ack.received-commandStarted),localObjects:welcome.snapshot.baseEntities.length}));
  }finally{const closed=new Promise(resolve=>ws.addEventListener('close',resolve,{once:true}));ws.close();await closed;}
}
await visit('first entry');
await new Promise(resolve=>setTimeout(resolve,500));
await visit('returning player');
