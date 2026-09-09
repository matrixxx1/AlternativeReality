const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
const {screenWind,precipitationVelocity}=new Function(source.slice(source.indexOf('  function screenWind('),source.indexOf('  function drawWindFlags('))+'return {screenWind,precipitationVelocity};')();
test('wind flags and precipitation share projected downwind bearings',()=>{
  for(const direction of [0,90,180,270])for(const snow of [true,false]){
    const weather={windDirectionDegrees:direction,windSpeedKilometersPerHour:18};
    const wind=screenWind(weather,.69,.14),v=precipitationVelocity(weather,.69,.14,snow);
    assert.ok(Math.abs(v.x-wind.x*36)<1e-8);
    assert.ok(Math.abs(v.y-(snow?130:450)-wind.y*36)<1e-8);
    assert.ok(v.y>0,'precipitation still falls');
    if(direction===90)assert.ok(v.x<0,'east wind blows west');
    if(direction===270)assert.ok(v.x>0,'west wind blows east');
  }
});
test('stronger wind increases drift and snow keeps falling in strong headwinds',()=>{
  const low=precipitationVelocity({windDirectionDegrees:270,windSpeedKilometersPerHour:2},.69,.14,true);
  const high=precipitationVelocity({windDirectionDegrees:270,windSpeedKilometersPerHour:20},.69,.14,true);
  assert.ok(high.x>low.x);
  assert.ok(precipitationVelocity({windDirectionDegrees:180,windSpeedKilometersPerHour:200},1,0,true).y>0);
});
