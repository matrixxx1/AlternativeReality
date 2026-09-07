const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
const commands=require('../src/AlternateEarth.Client2D/commands.js');
function implementation(name){const start=source.indexOf(`  function ${name}(`);assert.ok(start>=0,name);return source.slice(start,source.indexOf('\n  function ',start+1));}
function harness(){
  let now=0;const sent=[],prefetch=[],state={frame:90,camera:{x:0,y:0},keys:new Set(),players:new Map(),path:[],pathSequence:0,commandSequence:0,performance:{tasks:new Map()},socket:{readyState:1,send:json=>sent.push(JSON.parse(json))}};
  const ui={connectionTask:{hidden:true},connectionTaskMessage:{},connectionTaskElapsed:{}},attributes={};
  const dependencies={state,ui,canvas:{setAttribute:(key,value)=>attributes[key]=value},performance:{now:()=>now},WebSocket:{OPEN:1},PlayerCommands:commands,
    setStatus:(text,online)=>{state.status={text,online};},setPerformanceTask:()=>{},showToast:()=>{},stopTravel:()=>commands.cancel(state),
    applySnapshot:snapshot=>{state.snapshot=snapshot;state.players=new Map(snapshot.players.map(player=>[player.id,player]));},applyPrivate:()=>{},
    renderActionMode:mode=>state.actionMode=mode,flightCameraTarget:me=>({...me.position}),areaForPoint:point=>({...point}),requestNearbyPrefetch:(area,origin)=>prefetch.push({area,origin})};
  const names=['controlsPaused','setConnectionTask','updateConnectionTaskClock','initializeWorldSession','probeServer','acceptServerProbe','send'];
  const api=new Function(...Object.keys(dependencies),names.map(implementation).join('\n')+`;return {${names.join(',')}};`)(...Object.values(dependencies));
  const welcome={playerId:'me',snapshot:{players:[{id:'me',position:{x:-3122,y:11915},locationId:'outdoor'}]},privateState:{}};
  return {state,ui,sent,prefetch,attributes,api,welcome,time:value=>now=value};
}
test('startup places the camera on the character despite many loading frames and waits for a matching server reply',()=>{
  const c=harness();c.api.setConnectionTask('Connecting');c.api.initializeWorldSession(c.welcome);
  assert.deepEqual(c.state.camera,c.welcome.snapshot.players[0].position);assert.equal(c.state.follow,true);
  assert.equal(c.api.controlsPaused(),true);assert.equal(c.attributes['aria-busy'],'true');assert.deepEqual(c.sent,[{type:'ping',id:1}]);
  c.api.acceptServerProbe({id:999});assert.equal(c.api.controlsPaused(),true);
  c.api.acceptServerProbe({id:1});assert.equal(c.api.controlsPaused(),false);assert.equal(c.state.status.online,true);
  assert.equal(c.attributes['aria-busy'],'false');assert.equal(c.prefetch.length,0);
});
test('a busy server pauses controls, clears pending movement, and resumes on its reply',()=>{
  const c=harness();c.api.initializeWorldSession(c.welcome);c.api.acceptServerProbe({id:1});
  c.time(1000);c.api.probeServer();c.state.keys.add('w');c.state.target={x:2,y:3};
  c.time(3600);c.api.probeServer();assert.equal(c.api.controlsPaused(),true);assert.match(c.ui.connectionTaskMessage.textContent,/busy/);
  assert.equal(c.state.keys.size,0);assert.equal(c.state.target,null);
  const count=c.sent.length;c.api.send({type:'setTravelMode',mode:'run'});assert.equal(c.sent.length,count);
  c.time(12000);c.api.probeServer();assert.equal(c.sent.length,count);assert.match(c.ui.connectionTaskElapsed.textContent,/8s/);
  c.api.acceptServerProbe({id:2});assert.equal(c.api.controlsPaused(),false);assert.equal(c.ui.connectionTask.hidden,true);
});
test('prefetch waits until after readiness and does not launch during blocking work',()=>{
  const c=harness();c.api.initializeWorldSession(c.welcome);c.time(5000);c.api.probeServer();assert.equal(c.prefetch.length,0);
  c.api.acceptServerProbe({id:1});c.time(10000);c.state.worldBusy=true;c.api.probeServer();assert.equal(c.prefetch.length,0);
  c.api.acceptServerProbe({id:2});c.state.worldBusy=false;c.time(11000);c.api.probeServer();assert.equal(c.prefetch.length,1);
});
test('stale replies after disconnect cannot unlock the game',()=>{
  const c=harness();c.api.initializeWorldSession(c.welcome);c.state.welcomeApplied=false;c.state.serverProbe=null;
  c.api.setConnectionTask('Reconnecting');c.api.acceptServerProbe({id:1});assert.equal(c.api.controlsPaused(),true);
});
test('rendering and movement do not request work while the readiness overlay is active',()=>{
  assert.match(implementation('render'),/if\(!controlsPaused\(\)\)maintainMapWindow/);
  assert.match(implementation('render'),/visibleUnloaded=!controlsPaused\(\)/);
  assert.match(source,/if\(me&&!controlsPaused\(\)&&!me\.abduction/);
});
