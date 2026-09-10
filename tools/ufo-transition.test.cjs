const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('src/AlternateEarth.Client2D/app.js','utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`);return source.slice(start,source.indexOf('\n  function ',start+1));}
const frame=vm.runInNewContext('('+implementation('ufoTransitionFrame')+')');
test('UFO arrives empty before lifting the player, then hands off at full flight height',()=>{
 const event={to:'ufo',started:0};
 assert.equal(frame(event,0).lift,0);assert.equal(frame(event,0).occupied,false);
 assert.equal(frame(event,350).fly,0);assert.equal(frame(event,350).lift,0);
 assert.ok(frame(event,850).lift>0&&frame(event,850).lift<1);
 assert.equal(frame(event,1400).lift,1);assert.equal(frame(event,1400).personAlpha,0);assert.equal(frame(event,1400).occupied,true);
 assert.equal(frame(event,1550),null);
});
test('UFO hovers throughout release, then flies away only after the player is on the ground',()=>{
 const event={from:'ufo',to:'walk',started:0};
 for(const age of [0,200,500,800,1000])assert.equal(frame(event,age).fly,0);
 assert.equal(frame(event,0).lift,1);assert.equal(frame(event,1000).lift,0);
 assert.ok(frame(event,1250).fly>0);assert.equal(frame(event,1250).lift,0);assert.equal(frame(event,1250).occupied,false);
 assert.equal(frame(event,1500),null);
});
function harness(event){
 const calls=[],noop=()=>{},ctx=new Proxy({},{get:()=>noop,set:()=>true});
 const state={scale:22,vehicleTransitions:new Map([['me',event]]),facings:new Map(),probulatorBeams:new Map(),movingUntil:new Map(),burningCharacters:new Map()};
 const c=vm.createContext({state,ctx,Inversions:{swim:()=>false},toScreen:p=>p,viewportWidth:()=>1000,ufoFlightHeightPixels:()=>300,hash:()=>0,isSleeping:()=>false,abductionVisual:()=>null,drawAttachedFire:noop,drawUfoGroundShadow:noop,
 drawUfoCraft:(x,y,s,occupied)=>calls.push({type:'craft',x,y,s,occupied}),drawPersonShape:(x,y,skin,shirt,moving,now,phase,mode)=>calls.push({type:mode==='ufo'?'normalCraft':'person',x,y,mode})});
 for(const name of ['ufoTransitionFrame','drawUfoTransition','drawPlayer','flightCameraTarget'])vm.runInContext(implementation(name),c);
 c.performance={now:()=>0};state.pitch=.6;state.shear=.3;
 return {c,calls,state};
}
test('actual player renderer draws exactly one craft per frame and exit never lowers it',()=>{
 for(const entering of [true,false]){
  const event={from:entering?'walk':'ufo',to:entering?'ufo':'walk',started:0,origin:{x:500,y:550}};
  const h=harness(event),player={id:'me',position:event.origin,travelMode:event.to};let hoverY;
  for(const now of [0,200,350,650,900,1050,1400]){
   h.calls.length=0;h.c.drawPlayer(player,true,0,now);
   assert.equal(h.calls.filter(x=>x.type==='craft').length,1);assert.equal(h.calls.filter(x=>x.type==='normalCraft').length,0);
   const ship=h.calls.find(x=>x.type==='craft');
   if(!entering&&now<=1050){hoverY??=ship.y;assert.equal(ship.y,hoverY);}
   if(entering&&now>=350){assert.equal(ship.y,550-300-22*.46*.42);assert.equal(ship.s,22*.46*1.9);}
  }
  h.calls.length=0;h.c.drawPlayer(player,true,0,1600);
  assert.equal(h.state.vehicleTransitions.size,0);assert.equal(h.calls.length,1);assert.equal(h.calls[0].type,entering?'normalCraft':'person');
 }
});
test('camera follows beam lift instead of instantly centering on server UFO mode',()=>{
 const event={from:'walk',to:'ufo',started:0,origin:{x:0,y:0}},h=harness(event),player={id:'me',travelMode:'ufo',position:event.origin};
 assert.equal(h.c.flightCameraTarget(player).y,0);
 h.c.performance.now=()=>850;const midway=h.c.flightCameraTarget(player).y;
 h.c.performance.now=()=>1550;assert.ok(midway>0&&midway<h.c.flightCameraTarget(player).y);
});
