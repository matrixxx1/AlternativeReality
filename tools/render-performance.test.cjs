const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`);assert.ok(start>=0,name);const end=source.indexOf('\n  function ',start+1);return source.slice(start,end);}
test('rendering stays at 30 FPS across display refresh rates and leaves slow frames alone',()=>{
  for(const refresh of [15,30,60,120,144,240]){
    const state={},due=new Function('state',implementation('shouldRenderFrame')+';return shouldRenderFrame;')(state);
    let frames=0;for(let i=0;i<refresh*10;i++)if(due(i*1000/refresh))frames++;
    assert.equal(frames,Math.min(30,refresh)*10,`${refresh} Hz display`);
  }
});
test('resuming after a long stall draws once without a catch-up burst',()=>{
  const state={},due=new Function('state',implementation('shouldRenderFrame')+';return shouldRenderFrame;')(state);
  assert.equal(due(0),true);assert.equal(due(5000),true);
  assert.equal(due(5016),false);assert.equal(due(5033.34),true);
});
test('camera follow keeps the same speed per second at the capped frame rate',()=>{
  const amount=new Function(implementation('cameraFollowAmount')+';return cameraFollowAmount;')();
  assert.ok(Math.abs((1-amount(1000/30))-(1-amount(1000/60))**2)<1e-12);
});
function harness(){
  const state={camera:{x:0,y:0}},names=['buildElevationGrid','buildElevationTree','nearestElevation','elevationBracket','elevationAt','cameraElevation'];
  return {state,...new Function('state','roundedCoordinate',names.map(implementation).join('\n')+`;return {${names.join(',')}};`)(state,value=>Math.round(value*1000)/1000)};
}
// Independent reference: the previous bilinear interpolation / exhaustive fallback.
function reference(samples,x,y){
  if(!samples.length)return 0;
  const round=v=>Math.round(v*1000)/1000,axes=key=>[...new Set(samples.map(p=>round(p[key])))].sort((a,b)=>a-b);
  const bracket=a=>v=>[a.filter(n=>n<=v).at(-1)??a[0],a.find(n=>n>=v)??a.at(-1)];
  const [x0,x1]=bracket(axes('x'))(x),[y0,y1]=bracket(axes('y'))(y);
  const get=(a,b)=>{const s=samples.findLast(p=>round(p.x)===a&&round(p.y)===b);return s?Number(s.elevationMeters)||0:undefined;};
  const z=[get(x0,y0),get(x1,y0),get(x0,y1),get(x1,y1)];
  if(z.every(Number.isFinite)){const tx=x0===x1?0:(x-x0)/(x1-x0),ty=y0===y1?0:(y-y0)/(y1-y0),bottom=z[0]+(z[1]-z[0])*tx,top=z[2]+(z[3]-z[2])*tx;return bottom+(top-bottom)*ty;}
  let nearest=samples[0],distance=Infinity;
  for(const p of samples){const d=(p.x-x)**2+(p.y-y)**2;if(d<distance){nearest=p;distance=d;}}
  return Number(nearest.elevationMeters)||0;
}
test('terrain heights exactly match the old renderer across complete and sparse grids',()=>{
  const c=harness();let seed=813;const random=()=>((seed=Math.imul(seed,1664525)+1013904223>>>0)/4294967296);
  for(const sparse of [false,true]){
    const samples=[];for(let x=-5;x<=5;x++)for(let y=-5;y<=5;y++)if(!sparse||random()>.65)samples.push({x:x*8+.0004,y:y*8+.0003,elevationMeters:random()*140-25});
    c.buildElevationGrid(samples);
    for(let i=0;i<1500;i++){const x=random()*140-70,y=random()*140-70;assert.equal(c.elevationAt(x,y),reference(samples,x,y));}
  }
});
test('nearest elevation preserves equal-distance and duplicate sample ordering',()=>{
  const c=harness(),samples=[{x:-1,y:0,elevationMeters:20},{x:1,y:0,elevationMeters:80},{x:-1,y:0,elevationMeters:60},{x:0,y:10,elevationMeters:100}];
  c.buildElevationGrid(samples);assert.equal(c.nearestElevation(c.state.elevationGrid.tree,0,0),20);
  assert.equal(c.elevationAt(-1,0),60); // Last duplicate wins for complete grid corners.
});
test('memoized heights stay exact, bounded, and refresh when map elevation changes',()=>{
  const c=harness();c.buildElevationGrid([{x:0,y:0,elevationMeters:10}]);
  for(let i=0;i<33000;i++)assert.equal(c.elevationAt(i*.12345,0),10);
  assert.ok(c.state.elevationGrid.cache.size<=32768);
  assert.equal(c.cameraElevation(),10);c.buildElevationGrid([{x:0,y:0,elevationMeters:60}]);assert.equal(c.cameraElevation(),60);
  c.buildElevationGrid([]);assert.equal(c.elevationAt(0,0),0);assert.equal(c.cameraElevation(),0);
});
test('projection follows camera movement, elevation changes, zoom, and indoor transitions',()=>{
  const c=harness();c.buildElevationGrid([{x:0,y:0,elevationMeters:10},{x:10,y:0,elevationMeters:30}]);
  Object.assign(c.state,{scale:26,shear:.14,pitch:.69});
  const project=new Function('state','elevationAt','cameraElevation','viewportWidth','innerHeight','elevationPixelsPerMeter',implementation('toScreen')+';return toScreen;')(c.state,c.elevationAt,c.cameraElevation,()=>1600,900,()=>Math.min(3.2,Math.max(.12,c.state.scale*.055)));
  for(const cameraX of [0,4,10])for(const scale of [2,26,96])for(const indoor of [false,true]){
    c.state.camera.x=cameraX;c.state.scale=scale;c.state.dungeon=indoor?{}:null;
    const point={x:6,y:0},relief=indoor?0:(reference([{x:0,y:0,elevationMeters:10},{x:10,y:0,elevationMeters:30}],6,0)-c.elevationAt(cameraX,0))*Math.min(3.2,Math.max(.12,scale*.055));
    assert.deepEqual(project(point),{x:800+(6-cameraX)*scale,y:450-relief});
  }
});
test('drawing uses one layout measurement per frame and refreshes between frames',()=>{
  const state={},ui={rightRail:{getBoundingClientRect(){reads++;return{left:width};}}};let reads=0,width=1200;
  const viewport=new Function('state','ui','innerWidth',implementation('viewportWidth')+';return viewportWidth;')(state,ui,1600);
  state.frameViewportWidth=viewport();for(let i=0;i<10000;i++)assert.equal(viewport(),1200);assert.equal(reads,1);
  state.frameViewportWidth=null;width=1300;assert.equal(viewport(),1300);assert.equal(reads,2);
  assert.match(implementation('render'),/finally\s*\{state\.frameViewportWidth=null;/);
});
test('telemetry avoids redundant frame updates but player, inventory, and destination changes are immediate',()=>{
  const state={privateState:{},target:null},calls=[];
  const update=new Function('state','updateTelemetry','updateEnergyDrinkTelemetry',implementation('updateFrameTelemetry')+';return updateFrameTelemetry;')(state,me=>calls.push(me),()=>{});
  const me={id:'me'};update(me,0);update(me,16);assert.equal(calls.length,1);
  update(me,100);assert.equal(calls.length,2);
  update({...me},110);assert.equal(calls.length,3);
  state.privateState={};update(me,120);assert.equal(calls.length,4);
  state.target={x:10,y:0};update(me,130);assert.equal(calls.length,5);
});
