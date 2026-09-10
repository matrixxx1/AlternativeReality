const test=require('node:test');
const assert=require('node:assert/strict');
const transit=require('../src/AlternateEarth.Client2D/transit.js');
const markers=require('../src/AlternateEarth.Client2D/map-markers.js');

test('bus hull targeting and stopped door-side boarding work at every heading',()=>{
  for(let h=0;h<Math.PI*2;h+=Math.PI/4){
    const bus={position:{x:20,y:30},headingRadians:h,healthHearts:100,status:'out of service',speedMetersPerSecond:0};
    const player={locationId:'outdoor',healthHearts:10,position:{x:20+Math.sin(h)*2,y:30-Math.cos(h)*2}};
    assert.ok(transit.canBoard(player,bus));
    const p=transit.attackPoint(bus,player.position);assert.ok(Math.abs(Math.hypot(p.x-player.position.x,p.y-player.position.y)-.75)<1e-8);
    assert.ok(!transit.canBoard(player,{...bus,speedMetersPerSecond:2}));
    assert.ok(!transit.canBoard(player,{...bus,healthHearts:0}));
    assert.ok(!transit.canBoard({...player,ridingBusId:'other'},bus));
    assert.ok(!transit.canBoard({...player,position:{x:20-Math.sin(h)*2,y:30+Math.cos(h)*2}},bus));
  }
});

test('route preview shows only selected-route buses, including distant buses',()=>{
  const route={id:'r1',path:[{x:0,y:0},{x:20000,y:10000}]};
  const buses=[{id:'far',routeId:'r1',position:{x:19000,y:9000},headingRadians:1},
    {id:'other',routeId:'r2',position:{x:0,y:0}},
    {id:'invalid',routeId:'r1',position:{x:NaN,y:0}}];
  assert.deepEqual(transit.routeBuses(route,buses).map(b=>b.id),['far']);
  const labels=[];
  const context=new Proxy({}, {get:(_,name)=>(...args)=>{if(name==='fillText')labels.push(args);for(const n of args)if(typeof n==='number')assert.ok(Number.isFinite(n),name);},set:()=>true});
  const canvas={width:800,height:480,getContext:()=>context};
  transit.drawRoute(canvas,route,[],null,buses);
  const label=labels.find(l=>l[0]==='BUS');assert.ok(label);assert.ok(label[1]>400);
  labels.length=0;transit.drawRoute(canvas,route,[],null,[{...buses[0],position:{x:500,y:100}}]);
  assert.ok(labels.find(l=>l[0]==='BUS')[1]<400,'refreshed bus position moves the marker');
  labels.length=0;transit.drawRoute(canvas,route,[],null,[]);
  assert.ok(!labels.some(l=>l[0]==='BUS'),'empty fleet removes old marker');
});

test('benches and seated players share a seat and project finite geometry at all headings',()=>{
  const context=new Proxy({}, {get:(_,name)=>(...args)=>{for(const n of args)if(typeof n==='number')assert.ok(Number.isFinite(n),name);},set:()=>true});
  for(const scale of [1,6,26,96])for(let heading=0;heading<Math.PI*2;heading+=Math.PI/4){
    const stop={position:{x:10,y:5},benchPosition:{x:11.6,y:4.5},headingRadians:heading};
    const project=p=>({x:(p.x+p.y*.25)*scale,y:-p.y*scale*.69});
    assert.deepEqual(transit.benchProjection(stop,project,scale)(0,0),project(stop.benchPosition));
    transit.drawStop(context,stop,project,scale);
    transit.drawWaitingPlayer(context,{position:stop.benchPosition,name:'Rider'},stop,project,scale,true);
  }
});
test('crowded minimap markers keep the nearest, space neighbors and cap ordinary places',()=>{
  const places=Array.from({length:1000},(_,i)=>({id:String(i),position:{x:i%40*20,y:Math.floor(i/40)*20}}));
  const selected=markers.nearby(places,{x:0,y:0},4,100);
  assert.equal(selected.length,4);assert.equal(selected[0].id,'0');
  for(const a of selected){assert.ok(Math.hypot(a.position.x,a.position.y)<=500);for(const b of selected)if(a!==b)assert.ok(Math.hypot(a.position.x-b.position.x,a.position.y-b.position.y)>=100);}
  assert.deepEqual(markers.nearby([...places].reverse(),{x:0,y:0},4,100),selected);
});
test('bus drawing projects finite geometry at every heading and zoom',()=>{
  let fills=0;
  const context=new Proxy({}, {get:(_,name)=>(...args)=>{if(name==='fill')fills++;for(const n of args)if(typeof n==='number')assert.ok(Number.isFinite(n),name);},set:()=>true});
  for(const scale of [1,6,26,40])for(let heading=0;heading<Math.PI*2;heading+=Math.PI/4)
    transit.drawBus(context,{position:{x:20,y:30},headingRadians:heading,healthHearts:100},p=>({x:(p.x+p.y*.25)*scale,y:-p.y*scale*.6}),scale);
  assert.ok(fills>1000);
});
test('waiting requires an outdoor player close to a stop who is not already riding',()=>{
  const stop={position:{x:10,y:-5}},player={position:{x:10,y:-5},locationId:'outdoor'};
  assert.equal(transit.canWait(player,stop),true);
  assert.equal(transit.canWait({...player,ridingBusId:'bus'},stop),false);
  assert.equal(transit.canWait({...player,locationId:'home'},stop),false);
  assert.equal(transit.canWait({...player,position:{x:10,y:5}},stop),false);
});
test('bus footprint rotates physical length and width with travel heading',()=>{
  const points=transit.footprint({position:{x:20,y:30},headingRadians:Math.PI/2});
  assert.equal(Math.max(...points.map(p=>p.y))-Math.min(...points.map(p=>p.y)),9);
  assert.equal(Math.max(...points.map(p=>p.x))-Math.min(...points.map(p=>p.x)),2.5);
});
test('route preview fits full itinerary without loading or moving the world camera',()=>{
  const path=[{x:-10000,y:0},{x:10000,y:0},{x:10000,y:5000}],project=transit.routeProjection(path,800,480);
  for(const p of path){const v=project(p);assert.ok(v.x>=35&&v.x<=765);assert.ok(v.y>=35&&v.y<=445);}
  assert.ok(project(path[2]).y<project(path[1]).y);
});


test('boarding approach walks around either end of the physical bus at every heading',()=>{
 for(let heading=0;heading<Math.PI*2;heading+=Math.PI/8)for(const [along,right] of [[0,-7],[-9,-3],[9,-3],[0,8],[-10,2],[10,2]]){
  const dx=Math.cos(heading),dy=Math.sin(heading),bus={position:{x:20,y:30},headingRadians:heading,healthHearts:100,status:'out of service',speedMetersPerSecond:0};
  const player={locationId:'outdoor',healthHearts:10,position:{x:20+dx*along+dy*right,y:30+dy*along-dx*right}};
  assert.ok(transit.canApproachBoarding(player,bus));assert.equal(transit.canBoard(player,bus),false);
  let ready=false;
  for(let step=0;step<300;step++){
   const plan=transit.boardingPlan(player,bus);if(plan.ready){ready=true;break;}
   assert.ok(plan.destination);const from=player.position,to=plan.destination,length=Math.hypot(to.x-from.x,to.y-from.y),fraction=Math.min(1,.15/length);
   player.position={x:from.x+(to.x-from.x)*fraction,y:from.y+(to.y-from.y)*fraction};
   const x=player.position.x-20,y=player.position.y-30,a=x*dx+y*dy,r=x*dy-y*dx;
   assert.ok(Math.abs(a)>4.85||Math.abs(r)>1.6,'approach never intersects the player-expanded bus hull');
  }
  assert.ok(ready,'approach reaches a valid boarding location');
 }
});
test('boarding rejects active buses even while stopped and unavailable players or wrecks',()=>{
 const player={locationId:'outdoor',healthHearts:10,position:{x:0,y:8}},bus={position:{x:0,y:0},headingRadians:0,healthHearts:100,speedMetersPerSecond:2};
 assert.deepEqual(transit.boardingPlan(player,bus),{unavailable:true});
 for(const status of ['driving','boarding','dropping off','yielding','blocked','pulling over','waiting for safe pull-over','rejoining route'])assert.equal(transit.canApproachBoarding(player,{...bus,status,speedMetersPerSecond:0}),false);
 Object.assign(bus,{status:'out of service',speedMetersPerSecond:0});assert.equal(transit.canApproachBoarding(player,bus),true);
 for(const changes of [{locationId:'home'},{ridingBusId:'bus'},{waitingAtBusStopId:'stop'},{healthHearts:0},{abduction:{}}])assert.equal(transit.canApproachBoarding({...player,...changes},bus),false);
 assert.equal(transit.canApproachBoarding(player,{...bus,healthHearts:0}),false);
});
