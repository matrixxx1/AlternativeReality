const test=require('node:test');
const assert=require('node:assert/strict');
const {bearing,actorBounds}=require('../src/AlternateEarth.Client2D/quest-navigation.js');
test('quest bearings use world north and update with each stage position',()=>{
  assert.equal(bearing({x:0,y:0},{x:0,y:100}),'100 m N');
  assert.equal(bearing({x:0,y:0},{x:-30,y:-40}),'50 m SW');
});
test('large creatures include heads and tails at close and distant zoom, mirrored west',()=>{
  for(const scale of [1,26,40])for(const subtype of ['tRex','brontosaurus','stegosaurus','raptor','giant']){
    const east=actorBounds({subtype,facing:'east'},scale),west=actorBounds({subtype,facing:'west'},scale);
    assert.ok(east.left<0&&east.right>0&&east.top < -16);
    assert.equal(west.left,-east.right);assert.equal(west.right,-east.left);
  }
  assert.ok(actorBounds({subtype:'tRex'},26).right>100);
});
