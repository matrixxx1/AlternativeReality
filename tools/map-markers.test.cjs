const test=require('node:test');
const assert=require('node:assert/strict');
const markers=require('../src/AlternateEarth.Client2D/map-markers.js');
const origin={x:0,y:0};
test('casino and bus markers remain visible beyond nearby map geometry',()=>{
  for(const type of ['Casino','Bus'])for(const position of [{x:4000,y:800},{x:-1500,y:2200}]){
    const marker=markers.project(position,origin,372,220,type);
    assert.ok(marker.offMap);assert.ok(marker.x>16&&marker.x<356);assert.ok(marker.y>16&&marker.y<204);
    assert.equal(marker.distanceMeters,Math.hypot(position.x,position.y));
  }
  assert.equal(markers.project({x:4000,y:0},origin,372,220,'Store'),null);
});
test('casino and bus filters are independent of the store filter',()=>{
  const prefs={categories:{store:false}};
  assert.equal(markers.categoryVisible(prefs,'Store'),false);
  assert.equal(markers.categoryVisible(prefs,'Casino'),true);
  assert.equal(markers.categoryVisible(prefs,'Bus'),true);
  prefs.categories.bus=false;assert.equal(markers.categoryVisible(prefs,'Bus'),false);
  assert.equal(markers.categoryVisible(prefs,'Casino'),true);
});
test('bus marker moves with its latest position and north stays up',()=>{
  const first=markers.project({x:0,y:0},origin,372,220,'Bus');
  const next=markers.project({x:100,y:100},origin,372,220,'Bus');
  assert.equal(first.x,186);assert.equal(first.y,110);
  assert.ok(next.x>first.x);assert.ok(next.y<first.y);assert.equal(next.offMap,false);
});
