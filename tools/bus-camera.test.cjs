const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function setup(){
  const state={follow:false,pointer:{down:false},scale:12,transit:{buses:[{id:'bus',speedMetersPerSecond:4}]}};
  const update=new Function('state',source.slice(source.indexOf('  function updateBusCamera('),source.indexOf('  function render('))+'return updateBusCamera;')(state);
  return {state,update,me:{ridingBusId:'bus'}};
}
test('moving bus resumes camera follow after three seconds without changing zoom',()=>{
  const {state,update,me}=setup();update(me,0);update(me,2999);assert.equal(state.follow,false);
  update(me,3000);assert.equal(state.follow,true);assert.equal(state.scale,12);
  update(me,4000);state.follow=false;update(me,6999);assert.equal(state.follow,false);
  update(me,7000);assert.equal(state.follow,true);
});
test('camera waits until three seconds after dragging finishes',()=>{
  const {state,update,me}=setup();update(me,0);state.pointer.down=true;update(me,4000);
  assert.equal(state.follow,false);state.pointer.down=false;update(me,6999);assert.equal(state.follow,false);
  update(me,7000);assert.equal(state.follow,true);
});
test('stopping or leaving a bus cancels recentering, restarting gets a fresh delay',()=>{
  const {state,update,me}=setup();update(me,0);state.transit.buses[0].speedMetersPerSecond=0;
  update(me,4000);assert.equal(state.follow,false);assert.equal(state.busCameraRecenterAt,null);
  state.transit.buses[0].speedMetersPerSecond=4;update(me,5000);update(me,7999);assert.equal(state.follow,false);
  update({},8000);assert.equal(state.follow,false);assert.equal(state.busCameraRecenterAt,null);
});
