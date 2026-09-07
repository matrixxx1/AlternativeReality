// Run against an isolated local server: node tools/command-smoke.mjs http://localhost:5081
import assert from 'node:assert/strict';
const base=process.argv[2];
if(!base||!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw Error('Specify an isolated localhost server URL.');
const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'Cmd'+crypto.randomUUID().slice(0,5),password:crypto.randomUUID()})});
const setup=await response.json();assert.ok(response.ok,JSON.stringify(setup));
const ws=new WebSocket(base.replace('http','ws')+'/ws?session='+setup.sessionToken),messages=[];
ws.addEventListener('message',event=>messages.push(JSON.parse(event.data)));
async function wait(predicate,after=0){const deadline=Date.now()+15000;while(Date.now()<deadline){const result=messages.slice(after).find(predicate);if(result)return result;const error=messages.slice(after).find(m=>m.type==='error');if(error)throw Error(error.message);await new Promise(resolve=>setTimeout(resolve,10));}throw Error('Timed out waiting for server.');}
try{
 const welcome=await wait(m=>m.type==='welcome');assert.equal(welcome.protocolVersion,52);
 const player=welcome.snapshot.players.find(p=>p.id===welcome.playerId),after=messages.length;
 for(let sequence=1;sequence<=30;sequence++)ws.send(JSON.stringify({type:'pathRequest',x:player.position.x+20,y:player.position.y+20,sequence}));
 ws.send(JSON.stringify({type:'cancelCommand'}));
 ws.send(JSON.stringify({type:'ping'}));
 ws.send(JSON.stringify({type:'pathRequest',x:player.position.x,y:player.position.y,sequence:99,includeSnapshot:true}));
 await wait(m=>m.type==='pong',after);
 await wait(m=>m.type==='worldExpanded'&&m.sequence===99,after);
 const latest=await wait(m=>m.type==='pathResult'&&m.sequence===99,after);
 assert.ok(latest.waypoints.length);
 assert.ok(messages.slice(after).filter(m=>m.type==='taskStatus').every(m=>Number.isInteger(m.sequence)));
 console.log('PASS: 30 route replacements, cancellation, responsive ping, latest route, and sequenced area snapshot.');
}finally{ws.close();}
