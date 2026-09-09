const test=require('node:test');
const assert=require('node:assert/strict');
const retro=require('../src/AlternateEarth.Client2D/retro-battles.js');
test('missed enemies wrap to the right and defeated state is retained',()=>{
  assert.equal(retro.enemyX(10,20,40),30);
  assert.equal(retro.enemyX(10,60,40),30);
});
test('render prediction stops during an outage and freezes the victory scene',()=>{
  const run={scroll:2,scrollSpeed:10,trackLength:100,jumpHeight:1,jumpVelocity:3};
  assert.deepEqual(retro.view(run,30),retro.view(run,.1));
  assert.equal(retro.view(run,1,true).scroll,2);
  assert.equal(retro.view(run,1,true).height,1);
});
