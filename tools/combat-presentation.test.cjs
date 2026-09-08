const test=require('node:test'),assert=require('node:assert/strict');
const ui=require('../src/AlternateEarth.Client2D/combat-presentation.js');
test('gear and resistance labels expose level and each damage type',()=>{
  assert.equal(ui.gearText({gear:{level:20,quality:'Fine',resistances:{Physical:.1,Fire:.25}}}),'Level 20 · Fine · Physical 10.0% · Fire 25.0%');
  assert.equal(ui.gearText({}),'');
});
test('every event actor draws at small and large zoom without nonfinite geometry',()=>{
  for(const scale of [1,10,35])for(const subtype of ['kenHydra','northParkCitizen','alaneeEgg','alaneeCelebrity','pierceHawkeye','hawkeyeChicken']){
    const ctx=new Proxy({}, {get:(_,key)=>(...args)=>{for(const arg of args)if(typeof arg==='number')assert.ok(Number.isFinite(arg),key);},set:()=>true});
    assert.equal(ui.drawActor(ctx,{subtype,name:subtype,pouringUntilUtc:new Date(Date.now()+1000).toISOString()},{x:20,y:10},scale,0),true);
  }
});
test('status labels include remaining time and expire at zero',()=>{
  const text=ui.effectsText([{type:'Poison',damagePerSecond:.15,endsAtUtc:new Date(Date.now()-1000).toISOString()}]);
  assert.equal(text,'Poison: 0.15/s, 0s');
});
