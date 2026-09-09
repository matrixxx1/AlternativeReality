const {test}=require('node:test');
const assert=require('node:assert/strict');
const scuba=require('../src/AlternateEarth.Client2D/scuba.js');
const effects=require('../src/AlternateEarth.Client2D/combat-effects.js');

test('dive entry does not intercept swimming to the surface as a dungeon exit click',()=>{
  const source=require('node:fs').readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
  const context={state:{dungeon:{underwater:{},exit:{x:1,y:18},level:1,levelCount:1}}};
  const vm=require('node:vm');vm.createContext(context);
  for(const name of ['dungeonCanExit','dungeonFeatureAt'])vm.runInContext(source.split('\n').find(line=>line.includes(`function ${name}(`)),context);
  assert.equal(context.dungeonCanExit(),false);
  assert.equal(context.dungeonFeatureAt({x:1,y:19.5}),null);
  delete context.state.dungeon.underwater;
  assert.equal(context.dungeonCanExit(),true);
  assert.equal(context.dungeonFeatureAt({x:1,y:19.5}),'exit');
});

test('underwater allows every melee weapon and only the spear gun at range',()=>{
  for(const weapon of ['fist','knife','sword','hockeyStick','iceSkate','zombieBite','spearGun'])assert.ok(scuba.canAttack(weapon));
  for(const weapon of ['rifle','pistol','crossbow','rock','grenade','rocketLauncher','flamethrower','napalmBottle'])assert.equal(scuba.canAttack(weapon),false);
});
test('side view has reversible coordinates and a visible top surface at every viewport size',()=>{
  for(const [width,height]of [[1200,900],[600,600],[1800,1100]])for(const scale of [3,28,70]){
    const camera={x:53,y:18};
    const surface=scuba.project({x:53,y:19.5},camera,scale,width,height);
    assert.ok(surface.y>=150&&surface.y<height/2);
    for(const point of [{x:1,y:2},{x:53,y:18},{x:110,y:19.5}]){
      const roundtrip=scuba.unproject(scuba.project(point,camera,scale,width,height),camera,scale,width,height);
      assert.ok(Math.abs(roundtrip.x-point.x)<1e-8&&Math.abs(roundtrip.y-point.y)<1e-8);
    }
    assert.ok(scuba.unproject({x:width/2,y:surface.y-10},camera,scale,width,height).y>19.5);
  }
});
test('spear flies more slowly than rifle ammunition without a fire muzzle effect',()=>{
  const combat={start:{x:0,y:0},end:{x:100,y:0}};
  const spear=effects.createShot({...combat,weapon:'spearGun'},0),rifle=effects.createShot({...combat,weapon:'rifle'},0);
  assert.equal(spear.duration,2500);assert.ok(spear.duration>rifle.duration);assert.equal(spear.style.muzzle,undefined);
  assert.equal(effects.phase(spear,2499).name,'flight');assert.equal(effects.phase(spear,2500).name,'impact');
});
test('diver, tank, goggles and all aquatic enemy sprites render finite geometry and balance canvas saves',()=>{
  let saves=0;const labels=[];
  const ctx=new Proxy({}, {set(obj,key,value){obj[key]=value;return true;},get(obj,key){
    if(key==='createLinearGradient')return()=>({addColorStop(){}});
    if(key==='save')return()=>{saves++;};if(key==='restore')return()=>{saves--;assert.ok(saves>=0);};
    if(key==='fillText')return(text)=>labels.push(text);
    return obj[key]??((...args)=>{for(const arg of args)if(typeof arg==='number')assert.ok(Number.isFinite(arg),key);});
  }});
  const actors=['fish','shark','octopus','largeShark','largeOctopus','barracuda','morayEel'].map((subtype,i)=>({subtype,name:subtype,position:{x:45+i,y:4+i},facing:i%2?'west':'east',healthHearts:5,maximumHealthHearts:10}));
  scuba.draw(ctx,{width:180,height:20,difficulty:20,actors,underwater:{name:'Test lake',westShore:true,eastShore:true}},
    {position:{x:50,y:10},air:5,maximumAir:10,equippedWeapon:'spearGun'},1200,900,p=>scuba.project(p,{x:50},28,1200,900),28,1000,'west');
  scuba.gear(ctx,40,40,50);assert.equal(saves,0);assert.ok(labels.some(label=>label==='AIR 50%'));
});
