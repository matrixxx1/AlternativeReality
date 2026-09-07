const test=require('node:test');
const assert=require('node:assert/strict');
const transit=require('../src/AlternateEarth.Client2D/transit.js');
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
