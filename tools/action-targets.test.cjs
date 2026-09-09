const test=require('node:test'),assert=require('node:assert/strict'),targets=require('../src/AlternateEarth.Client2D/action-targets.js');
test('overlapping actors, doors and treasure all survive hit testing; building door and roof deduplicate',()=>{
 const box={left:100,right:110,top:100,bottom:110},candidates=[
 {kind:'actor',id:'one',bounds:box},{kind:'actor',id:'two',bounds:box},{kind:'loot',id:'loot',screen:{x:105,y:105}},
 {kind:'building',id:'house',bounds:box},{kind:'building',id:'house',polygons:[[{x:90,y:90},{x:120,y:90},{x:120,y:120},{x:90,y:120}]]}];
 assert.deepEqual(targets.collect(candidates,{x:105,y:105}).map(x=>x.id),['one','two','loot','house']);
});
test('padded silhouettes and small ground objects have forgiving but bounded click areas',()=>{
 const candidates=[{kind:'actor',id:'a',bounds:{left:100,right:110,top:100,bottom:110}},{kind:'loot',id:'l',screen:{x:200,y:200}}];
 assert.equal(targets.collect(candidates,{x:91,y:100}).length,1);assert.equal(targets.collect(candidates,{x:89,y:100}).length,0);
 assert.equal(targets.collect(candidates,{x:221,y:200}).length,1);assert.equal(targets.collect(candidates,{x:223,y:200}).length,0);
});
test('building edges can be selected without selecting distant ground',()=>{
 const p=[{x:0,y:0},{x:100,y:0},{x:100,y:100},{x:0,y:100}];
 assert.equal(targets.polygonHit({x:105,y:50},p),true);assert.equal(targets.polygonHit({x:120,y:50},p),false);
});
