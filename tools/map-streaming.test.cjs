const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`),end=source.indexOf('\n  function ',start+1);return source.slice(start,end);}
const box=(x,y,r)=>({minimumX:x-r,minimumY:y-r,maximumX:x+r,maximumY:y+r});
function harness(){
  const sent=[],player={id:'me',position:{x:0,y:0},locationId:'outdoor'},players=new Map([['me',player]]),actors=new Map([['npc',{id:'npc',position:{x:2,y:2}}]]);
  const state={snapshot:{mapCoverage:[box(0,0,600)]},mapCoverage:[box(0,0,600)],players,actors,playerId:'me',socket:{readyState:1},lastMapWindowRequest:0,mapWindowSequence:0,base:[]};
  const context=vm.createContext({state,sent,WebSocket:{OPEN:1},send:message=>sent.push(message),setPerformanceTask:()=>{},buildElevationGrid:()=>{},spatialCellSize:128,spatialKey:(x,y)=>`${x}:${y}`});
  for(const name of ['boundsOf','buildSpatialIndex','applyMapWindow','maintainMapWindow'])vm.runInContext(implementation(name),context);
  return context;
}
test('replacing map detail releases old objects and indexes without rewinding live players or actors',()=>{
  const c=harness(),players=c.state.players,actors=c.state.actors;
  c.applyMapWindow({baseEntities:[{id:'old-door',kind:'door',position:{x:0,y:0},properties:{buildingId:'old'}}],elevation:[],coverage:[box(0,0,600)]});
  c.applyMapWindow({baseEntities:[{id:'new-store',kind:'pointOfInterest',position:{x:3000,y:0},properties:{merchantCategory:'food'}}],elevation:[],coverage:[box(3000,0,600)]});
  assert.equal(c.state.base.length,1);assert.equal(c.state.snapshot.baseEntities[0].id,'new-store');
  assert.equal(c.state.baseById.has('old-door'),false);assert.equal(c.state.doors.size,0);assert.equal(c.state.spatial.has('door'),false);
  assert.equal(c.state.miniMapStores[0].id,'new-store');assert.equal(c.state.players,players);assert.equal(c.state.actors,actors);
});
test('nearby views reuse their buffer, while panning requests fresh detail once',()=>{
  const c=harness(),me=c.state.players.get('me');
  c.maintainMapWindow({minX:-50,minY:-50,maxX:50,maxY:50},me,1000);assert.equal(c.sent.length,0);
  const view={minX:2900,minY:-50,maxX:3100,maxY:50};
  c.maintainMapWindow(view,me,1300);assert.equal(c.sent.length,1);assert.equal(c.sent[0].type,'requestMapWindow');
  c.maintainMapWindow(view,me,1700);assert.equal(c.sent.length,1);
  c.state.mapWindowPending=false;c.state.mapCoverage=[box(0,0,600),box(3000,0,440)];
  c.maintainMapWindow(view,me,1900);assert.equal(c.sent.length,1);
});
test('zooming back in discards an oversized retained camera window',()=>{
  const c=harness();c.state.mapCoverage=[box(0,0,600),box(0,0,2500)];
  c.maintainMapWindow({minX:-50,minY:-50,maxX:50,maxY:50},c.state.players.get('me'),1000);
  assert.equal(c.sent.length,1);assert.ok(c.sent[0].maximumX-c.sent[0].minimumX<300);
});
test('moving requests fresh mini-map coverage even when the camera stays behind',()=>{
  const c=harness();c.state.players.get('me').position.x=150;
  c.maintainMapWindow({minX:-50,minY:-50,maxX:50,maxY:50},c.state.players.get('me'),1000);
  assert.equal(c.sent.length,1);
});
test('a long crossing feature only indexes cells in the retained map windows',()=>{
  const c=harness();
  c.applyMapWindow({baseEntities:[{id:'long-road',kind:'road',position:{x:0,y:0},geometry:[{x:-100000,y:-100000},{x:100000,y:100000}]}],elevation:[],coverage:[box(0,0,600),box(0,0,600)]});
  const buckets=c.state.spatial.get('road');assert.ok(buckets.size<200);
  assert.ok([...buckets.values()].every(bucket=>bucket.length===1));
});
test('stale map responses are ignored and full snapshots invalidate pending requests',()=>{
  const c=harness();c.state.mapWindowSequence=3;c.state.mapWindowPending=true;
  const start=source.indexOf("case 'mapWindow':"),end=source.indexOf("case 'worldExpanded':",start);
  const handler=source.slice(start,end).replace(/^case 'mapWindow':/,'').replace(/break;\s*$/,'');
  c.message={sequence:2,map:{baseEntities:[],coverage:[]}};vm.runInContext(handler,c);
  assert.equal(c.state.mapWindowPending,true);assert.equal(c.state.mapCoverage.length,1);
  assert.match(implementation('applySnapshot'),/state\.mapWindowSequence\+\+/);
});
