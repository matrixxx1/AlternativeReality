const test=require('node:test'),assert=require('node:assert/strict');
const Gardens=require('../src/AlternateEarth.Client2D/gardens.js');
test('garden range uses the whole footprint rather than only its center',()=>{
 assert.equal(Gardens.withinHome({x:37,y:0},{x:0,y:0}),true);
 assert.equal(Gardens.withinHome({x:39,y:0},{x:0,y:0}),false);
 assert.equal(Gardens.withinHome({x:NaN,y:0},{x:0,y:0}),false);
});
test('garden crops, signs, rubble and hose draw finite geometry with balanced transforms',()=>{
 global.Survival={glyph:()=>'*'};let depth=0,draws=0;
 const ctx=new Proxy({}, {get(o,k){if(k==='save')return()=>depth++;if(k==='restore')return()=>{assert.ok(depth>0);depth--;};if(k==='measureText')return()=>({width:50});return o[k]??((...args)=>{draws++;for(const a of args)if(typeof a==='number')assert.ok(Number.isFinite(a),k);});},set(o,k,v){o[k]=v;return true;}});
 for(const scale of [4,26,96])for(const properties of [{subtype:'farmCow',state:'dead',productQuantity:'3'},{subtype:'farmCow',productQuantity:'5'},{subtype:'farmChicken',productQuantity:'3'},{subtype:'garden',itemType:'watermelon'},{subtype:'garden',itemType:'raspberry',readyAtUtc:'2999-01-01'},{subtype:'garden',state:'rubble'},{subtype:'gardenHose'}])assert.equal(Gardens.draw(ctx,{position:{x:10,y:12},properties},p=>({x:p.x*scale,y:p.y*scale*.69}),scale),true);
 assert.equal(depth,0);assert.ok(draws>50);
});
