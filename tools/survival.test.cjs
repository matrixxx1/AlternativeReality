const {test}=require('node:test');
const assert=require('node:assert/strict');
const survival=require('../src/AlternateEarth.Client2D/survival.js');
const commands=require('../src/AlternateEarth.Client2D/commands.js');
test('hunger, illness expiry and temporary meal effects are shown accurately',()=>{
 const now=Date.now(),active=new Date(now+60000).toISOString(),expired=new Date(now-1).toISOString();
 const status=survival.status({survival:{hunger:100,illnesses:[{name:'Parasites',endsAtUtc:active},{name:'Old cold',endsAtUtc:expired}],buffs:[{stat:'strength',amount:2,endsAtUtc:active}]}},now);
 assert.match(status.text,/STARVING/);assert.equal(status.illnesses,'Parasites · 1 min');assert.match(status.buffs,/\+2 strength/);
 assert.equal(survival.status({}).hunger,0);
});
test('gathering travels into range, sends one request and cancels cleanly',()=>{
 const me={position:{x:0,y:0}},node={id:'berry',position:{x:10,y:0},properties:{subtype:'wildCrop',itemType:'blueberry'}};
 const state={players:new Map([['me',me]]),playerId:'me',scale:10,keys:new Set(),pathSequence:1};const sent=[],routes=[];
 assert.ok(survival.clickWild(node.position,state,()=>[node],m=>sent.push(m),p=>routes.push(p),()=>{}));
 assert.equal(sent.length,0);assert.equal(routes.length,1);assert.equal(state.pendingWild.id,node.id);
 me.position={x:8,y:0};survival.advanceGather(state,me,m=>sent.push(m),()=>commands.cancel(state));
 survival.advanceGather(state,me,m=>sent.push(m),()=>{});assert.deepEqual(sent,[{type:'gatherWild',entityId:'berry'}]);
 state.pendingWild=node;commands.cancel(state);assert.equal(state.pendingWild,null);
});
test('wild fruit trees, crops, sand mounds, livestock and shellfish render distinct art',()=>{
 let saves=0;const text=[];const ctx=new Proxy({}, {set(o,k,v){o[k]=v;return true;},get(o,k){if(k==='save')return()=>saves++;if(k==='restore')return()=>saves--;if(k==='fillText')return t=>text.push(t);return o[k]??((...args)=>args.filter(a=>typeof a==='number').forEach(a=>assert.ok(Number.isFinite(a))));}});
 for(const subtype of ['fruitTree','wildCrop','sandLump'])assert.ok(survival.drawNode(ctx,{properties:{subtype,itemType:'apple'}},{x:10,y:10},20));
 for(const subtype of ['chicken','pig','cow','sheep','goat','crab','clam','oyster','crayfish','lobster','shrimp'])assert.ok(survival.drawAnimal(ctx,{subtype,name:subtype},{x:10,y:10},20));
 assert.equal(saves,0);assert.ok(text.includes('Search sand'));assert.ok(text.includes('🐄'));assert.ok(text.includes('🦪'));
});
