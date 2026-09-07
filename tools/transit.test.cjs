const test=require('node:test');
const assert=require('node:assert/strict');
const transit=require('../src/AlternateEarth.Client2D/transit.js');
const markers=require('../src/AlternateEarth.Client2D/map-markers.js');
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
