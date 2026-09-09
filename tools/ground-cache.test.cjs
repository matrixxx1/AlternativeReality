const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
const start=source.indexOf('  function drawCachedGround('),end=source.indexOf('\n  function ',start+1),implementation=source.slice(start,end);
function harness(terrain=[]){
 const state={camera:{x:0,y:0},base:[],elevationGrid:{},scale:26,pitch:.69,shear:.14},draws=[],builds=[],painted=[];
 const main={drawImage(...args){draws.push(args);}},buffer={setTransform(){},fillRect(){}};
 const document={createElement(){return{getContext(){return buffer;}}}};
 const project=p=>({x:500+(p.x-state.camera.x)*state.scale,y:400-(p.y-state.camera.y)*state.scale*state.pitch});
 const draw=new Function('state','canvas','document','viewportWidth','innerHeight','toScreen','renderListsFor','drawGrass','drawTerrain','drawSidewalkNetwork','drawRoadNetwork','main',`let ctx=main;${implementation};return drawCachedGround;`)(state,{width:1000},document,()=>1000,800,project,()=>({terrain,sidewalk:[],road:[]}),v=>builds.push(v),e=>painted.push(e.id),()=>{},()=>{},main);
 return{state,draws,builds,painted,draw:()=>draw({minX:-20,maxX:20,minY:-20,maxY:20},2)};
}
test('ground raster is reused while stationary and shifts with camera motion',()=>{
 const c=harness();c.draw();const image=c.draws[0][0];c.draw();assert.equal(c.builds.length,1);
 c.state.camera.x=1;c.draw();assert.equal(c.builds.length,1);assert.equal(c.draws.at(-1)[0],image);assert.equal(c.draws.at(-1)[1],-154);
 c.state.camera.x=4;c.draw();assert.equal(c.builds.length,2);assert.equal(c.draws.at(-1)[1],-128);
});
test('map replacement, elevation replacement, zoom and projection changes invalidate cached ground',()=>{
 const c=harness();c.draw();
 for(const [key,value] of [['base',[]],['elevationGrid',{}],['scale',6],['pitch',.8],['shear',.2]]){
  const before=c.builds.length;c.state[key]=value;c.draw();assert.equal(c.builds.length,before+1,key);
 }
});

test('render resolution reacts to sustained load, recovers slowly, and ignores hidden tabs',()=>{
 const start=source.indexOf('  function updateRenderResolution('),end=source.indexOf('\n  function ',start+1);
 const state={snapshot:{},performance:{fps:18,averageRenderMs:25}},document={hidden:false};let resizes=0;
 const update=new Function('state','document','devicePixelRatio','resize',source.slice(start,end)+';return updateRenderResolution;')(state,document,2,()=>resizes++);
 update(0);update(2400);assert.equal(resizes,0);update(2600);assert.equal(state.renderPixelRatio,1.25);
 update(5200);assert.equal(state.renderPixelRatio,1);update(10000);assert.equal(resizes,2);
 state.performance={fps:30,averageRenderMs:4};update(11000);update(30000);assert.equal(resizes,2);update(31000);assert.equal(state.renderPixelRatio,1.25);
 document.hidden=true;update(60000);assert.equal(resizes,3);
});

test("driveway pavement stays visible above underlying terrain regardless of snapshot order",()=>{
 const c=harness([{id:"driveway",properties:{subtype:"driveway"}},{id:"grass",properties:{terrain:"grass"}}]);
 c.draw();assert.deepEqual(c.painted,["grass","driveway"]);
});
