const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
const {screenWind,precipitationVelocity,precipitationParticle}=new Function(source.slice(source.indexOf('  function screenWind('),source.indexOf('  function drawWindFlags('))+'return {screenWind,precipitationVelocity,precipitationParticle};')();
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

test('rain positions and speeds vary independently instead of forming diagonal rows',()=>{
 const drops=Array.from({length:200},(_,i)=>precipitationParticle(i,10,1000,700,{x:60,y:450},false));
 const mean=key=>drops.reduce((s,p)=>s+p[key],0)/drops.length,mx=mean('x'),my=mean('y');
 const covariance=drops.reduce((s,p)=>s+(p.x-mx)*(p.y-my),0),variance=key=>drops.reduce((s,p)=>s+(p[key]-mean(key))**2,0);
 assert.ok(Math.abs(covariance/Math.sqrt(variance('x')*variance('y')))<.2);
 assert.ok(new Set(drops.map(p=>p.dy.toFixed(2))).size>100);
 assert.ok(drops.every(p=>p.dy>0&&Math.abs(p.dx)<p.dy*.12));
 assert.ok(drops.every(p=>p.x>=0&&p.x<1000&&p.y>=-20&&p.y<720));
});
test('each rainfall pass chooses a new horizontal position without per-frame jitter',()=>{
 const a=precipitationParticle(7,0,1000,700,{x:0,y:450},false),b=precipitationParticle(7,.001,1000,700,{x:0,y:450},false),c=precipitationParticle(7,10,1000,700,{x:0,y:450},false);
 assert.ok(Math.abs(a.x-b.x)<.1);assert.ok(b.y>a.y);assert.ok(Math.abs(a.x-c.x)>20);
});
