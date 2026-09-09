// Isolated fixture server and WebSocket transit smoke test; never touches the live world's data.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import assert from 'node:assert/strict';
import http from 'node:http';
const root=path.resolve(import.meta.dirname,'..'),data=path.join(root,'artifacts','transit-smoke-'+Date.now());
fs.mkdirSync(path.join(data,'world-cache'),{recursive:true});
fs.writeFileSync(path.join(data,'reality-location.json'),JSON.stringify({latitude:45.5,longitude:-122.5}));
const region={latitudeBand:45,longitudeBand:-123},position=(x,y)=>({region,x,y,z:0});
const points=[[-40,0],[40,0],[40,30],[-40,30],[-40,0]];
const features=points.slice(0,-1).map((p,i)=>({id:'smoke-road:'+i,kind:'road',position:position(...p),geometry:[{x:p[0],y:p[1],z:0},{x:points[i+1][0],y:points[i+1][1],z:0}],properties:{name:'Transit Test Loop',highway:'secondary',oneway:'yes',widthMeters:'8',osmNodeIds:`${i+1},${i===3?1:i+2}`},version:1,isBaseEntity:true}));
const area={center:{latitude:45.5,longitude:-122.5,elevationMeters:0},sizeMeters:1000};
const key=crypto.createHash('sha256').update('v2:transit-smoke:123:45:-123:45.500000:-122.500000:1000').digest('hex').toUpperCase().slice(0,20);
fs.writeFileSync(path.join(data,'world-cache',`world-${key}.json`),JSON.stringify({provider:'Generated canonical world (transit smoke fixture)',area,features,elevation:[{x:0,y:0,elevationMeters:0}],cachedAtUtc:new Date().toISOString()}));
const log=fs.openSync(path.join(data,'server.log'),'a');
// Hold geographic requests until the test ends, to prove a slow map cannot block commands.
let upstreamRequests=0;
const upstream=http.createServer(()=>{upstreamRequests++;});
await new Promise(resolve=>upstream.listen(0,'127.0.0.1',resolve));
const upstreamUrl=`http://127.0.0.1:${upstream.address().port}/`;
const server=spawn('dotnet',[process.env.SERVER_DLL||path.join(root,'artifacts/performance-release/AlternateEarth.Server.dll'),
  '--Server:Urls=http://127.0.0.1:5081',`--Server:DataDirectory=${data}`,'--Reality:Id=transit-smoke','--Reality:Seed=123','--Reality:SizeMeters=1000',
  '--Weather:Url=http://127.0.0.1:1/',`--Geo:OverpassUrl=${upstreamUrl}`,'--Geo:RemoteElevation=false'],
  {cwd:path.join(root,'src/AlternateEarth.Server'),stdio:['ignore',log,log],windowsHide:true});
server.on('error',error=>{throw error;});
const base='http://127.0.0.1:5081';
const deadline=Date.now()+30000;let ready=false;
while(Date.now()<deadline){try{const result=await fetch(base+'/api/status');if(result.ok){ready=true;break;}}catch{}await new Promise(r=>setTimeout(r,100));}
assert.ok(ready,'Isolated server did not start');
const response=await fetch(base+'/api/account/setup',{method:'POST',headers:{'content-type':'application/json','x-alternativereality-smoke-test':'1'},body:JSON.stringify({username:'BusSmoke',password:'TransitSmoke123!'})});
assert.ok(response.ok,await response.clone().text());const setup=await response.json();
const ws=new WebSocket(base.replace('http','ws')+'/ws?session='+setup.sessionToken),messages=[];
ws.addEventListener('message',event=>messages.push(JSON.parse(event.data)));
async function wait(predicate,after=0){const limit=Date.now()+60000;while(Date.now()<limit){const found=messages.slice(after).find(predicate);if(found)return found;const error=messages.slice(after).find(m=>m.type==='error');if(error)throw Error(error.message);await new Promise(r=>setTimeout(r,20));}throw Error('Timed out waiting for transit update');}
try{
 const welcome=await wait(m=>m.type==='welcome');assert.equal(welcome.protocolVersion,59);
 assert.equal((await fetch(base+'/api/reality-maintenance')).status,401);
 assert.equal((await fetch(base+'/api/reality-maintenance',{headers:{cookie:`alternative_reality_session=${setup.sessionToken}`}})).status,403);
 const id=welcome.playerId;ws.send(JSON.stringify({type:'setGodMode',enabled:true}));await wait(m=>m.type==='playerUpdated'&&m.player?.godMode);
 ws.send(JSON.stringify({type:'teleport',x:-30,y:-5.5,godMode:true}));await wait(m=>m.type==='playerTeleported');
 const transit=welcome.snapshot.transit.buses.length?welcome.snapshot.transit:(await wait(m=>m.type==='transitChanged'&&m.transit.buses.length)).transit;
 assert.ok(transit.stops.length);assert.ok(transit.routes.length);assert.ok(transit.buses.length<=2);
 assert.ok(transit.routes.every(r=>r.path.length===0));
 let mark=messages.length;
 const bus=transit.buses[0],route=transit.routes.find(r=>r.id===bus.routeId);
 ws.send(JSON.stringify({type:'requestBusRoute',routeId:route.id}));
 const detail=await wait(m=>m.type==='busRoute');assert.ok(detail.route.path.length>1);assert.ok(detail.stops.length);
 assert.ok(detail.buses.some(b=>b.id===bus.id));assert.ok(detail.buses.every(b=>b.routeId===route.id));
 const positionMark=messages.length;
 ws.send(JSON.stringify({type:'requestBusRoute',routeId:route.id,positionsOnly:true}));
 const positions=await wait(m=>m.type==='busRoutePositions',positionMark);
 assert.equal(positions.routeId,route.id);assert.ok(positions.buses.some(b=>b.id===bus.id));
 assert.ok(positions.buses.every(b=>b.routeId===route.id));assert.equal(positions.route,undefined);
 console.log('PASS: route details and lightweight position refresh include selected-route buses.');
 const stop=detail.stops.find(s=>s.direction==='eastbound');
 ws.send(JSON.stringify({type:'teleport',x:stop.position.x,y:stop.position.y,godMode:true}));await wait(m=>m.type==='playerTeleported',mark);
 mark=messages.length;ws.send(JSON.stringify({type:'waitForBus',stopId:stop.id}));
 const seated=await wait(m=>m.type==='playersUpdated'&&m.players.some(p=>p.id===id&&p.waitingAtBusStopId===stop.id),mark);
 assert.deepEqual(seated.players.find(p=>p.id===id).position,stop.benchPosition);
 await wait(m=>m.type==='playersUpdated'&&m.players.some(p=>p.id===id&&p.ridingBusId),mark);
 mark=messages.length;ws.send(JSON.stringify({type:'getOffBus'}));
 const drop=await wait(m=>m.type==='playersUpdated'&&m.players.some(p=>p.id===id&&!p.ridingBusId&&!p.waitingAtBusStopId),mark);
 assert.ok(drop.players.find(p=>p.id===id).position.y<0);
 const html=await(await fetch(base)).text();assert.ok(html.includes('View route'));assert.ok((await fetch(base+'/transit.js')).ok);
 mark=messages.length;ws.send(JSON.stringify({type:'requestArea',x:1000,y:0}));
 const loading=await wait(m=>m.type==='taskStatus',mark);assert.equal(loading.blocksMovement,false);
 const upstreamDeadline=Date.now()+5000;while(!upstreamRequests&&Date.now()<upstreamDeadline)await new Promise(r=>setTimeout(r,10));
 assert.ok(upstreamRequests,'Map generation must actually be waiting on the delayed provider');
 const probeStart=performance.now();ws.send(JSON.stringify({type:'ping',id:734}));
 await wait(m=>m.type==='pong'&&m.id===734,mark);const mapBlockedPingMs=performance.now()-probeStart;
 assert.ok(mapBlockedPingMs<1000,`Map load blocked ping for ${mapBlockedPingMs} ms`);
 ws.send(JSON.stringify({type:'setActionMode',mode:'neutral'}));await wait(m=>m.type==='actionModeChanged',mark);
 assert.ok(!messages.slice(mark).some(m=>m.type==='worldExpanded'),'Provider is still deliberately blocked');
 const maintenance=await(await fetch(base+'/api/reality-maintenance',{headers:{cookie:`alternative_reality_session=${setup.sessionToken}`}})).json();
 assert.ok(maintenance.storage.mapBlocks>=1);assert.ok(maintenance.storage.mapBytes>0);assert.equal(maintenance.reality.id,'transit-smoke');
 const diagnostics=await(await fetch(base+'/api/diagnostics',{headers:{cookie:`alternative_reality_session=${setup.sessionToken}`}})).json();
 assert.ok(diagnostics.actorRouteWorkers>=1&&diagnostics.actorRouteWorkers<=2);
 assert.ok(diagnostics.timings['command.ping'].count>0);assert.ok(diagnostics.timings['actors.tick'].count>0);
 console.log(JSON.stringify({result:'PASS',protocol:59,stops:transit.stops.length,routes:transit.routes.length,buses:transit.buses.length,routeOnDemand:true,benchSeating:true,mapBlockedPingMs,serverPid:server.pid,data,base,browserLogin:'BusSmoke'}));
}finally{ws.close();upstream.closeAllConnections();upstream.close();if(!process.argv.includes('--keep-running'))server.kill();}
if(process.argv.includes('--keep-running')){await new Promise(resolve=>server.on('exit',resolve));}else{server.kill();}
