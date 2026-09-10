const test=require('node:test'),assert=require('node:assert/strict');
const audio=require('../src/AlternateEarth.Client2D/game-audio.js');
const me={id:'me',locationId:'outdoor',position:{x:0,y:0},travelMode:'walk'};
test('Musical Fruit plays each small cloud once and loops the louder finale until expiry',async()=>{
 const h=harness();await h.engine.unlock();
 const cloud={id:'bean-a',effect:'musicalFruit',position:{x:1,y:0},locationId:'outdoor',startedAtUtc:new Date(0).toISOString(),endsAtUtc:new Date(10000).toISOString()};
 h.scene.areaHazards=[cloud];h.engine.tick();h.engine.tick();assert.equal(h.started.length,1);assert.equal(h.engine.status().loops,0);
 assert.ok(['fartSqueaker','fartHonker'].includes(audio.fartSources(h.scene,0)[0].kind));
 h.scene.areaHazards=[{...cloud,id:'finale',effect:'musicalFruitFinale',endsAtUtc:new Date(20000).toISOString()}];h.engine.tick();h.engine.tick();
 assert.equal(h.engine.status().loops,1);assert.equal(h.started.length,2);assert.equal(h.started[1].loop,true);
 const cue=audio.fruitFinaleSources(h.scene,0)[0];assert.ok(cue.volume>.45);assert.equal(h.started[1].buffer.duration,4);
 h.time(19999);h.engine.tick();assert.equal(h.engine.status().loops,1);h.time(20000);h.engine.tick();assert.equal(h.engine.status().loops,0);
 h.time(1);h.engine.tick();h.scene.listener.locationId='home';h.engine.tick();assert.equal(h.engine.status().loops,0);
 for(const kind of ['fartSqueaker','fartHonker','fartFinale']){const data=audio.samples(kind);assert.ok(data.some(v=>Math.abs(v)>.1));assert.ok(data.every(Number.isFinite));}
});
test('sounds attenuate with distance, pan left/right, and never cross interiors or regions',()=>{
 assert.equal(audio.spatial(me,{x:0,y:0}).gain,1);
 assert.ok(audio.spatial(me,{x:10,y:0}).gain>audio.spatial(me,{x:30,y:0}).gain);
 assert.ok(audio.spatial(me,{x:-10,y:0}).pan<0);assert.equal(audio.spatial(me,{x:100,y:0}).gain,0);
 assert.equal(audio.spatial(me,{x:0,y:0},'home:other').gain,0);
 assert.equal(audio.spatial({...me,position:{...me.position,region:{latitudeBand:1,longitudeBand:2}}},{x:0,y:0,region:{latitudeBand:3,longitudeBand:2}}).gain,0);
});
test('animal speech selects the animal, not arbitrary player chat',()=>{
 for(const subtype of ['cow','chicken','pig','goat','sheep','dog','cat'])assert.equal(audio.animalSound({kind:'animal',subtype}),subtype);
 assert.equal(audio.animalSound({kind:'player',subtype:'cow'}),null);
});
test('gun bursts follow visual launch times and rockets explode at their impact, without splash duplicates',()=>{
 const event={weapon:'rocketLauncher',start:{x:1,y:0},end:{x:80,y:0},hit:true};
 const cues=audio.combatCues(event,{started:1000,duration:1400},1000);assert.deepEqual(cues.map(c=>[c.kind,c.delay]),[['rocket',0],['explosion',1400]]);
 assert.deepEqual(audio.combatCues({...event,weapon:'rocketExplosion'},null,1000),[]);
 assert.equal(audio.combatCues({...event,weapon:'ar15'},{started:1170,duration:240},1000)[0].delay,170);
 assert.ok(!audio.combatCues({...event,weapon:'sword',hit:false},null,0).some(c=>c.kind==='hit'));
 assert.ok(audio.combatCues({...event,weapon:'sword'},null,0).some(c=>c.kind==='hit'));
});
test('only active nearby vehicles produce motors and the loudest six are bounded',()=>{
 const vehicle={position:{x:3,y:0},healthHearts:100,locationId:'outdoor'};
 assert.equal(audio.vehicleSources({listener:me,buses:[{...vehicle,id:'parked',status:'out of service'}],vehicles:[{...vehicle,id:'car',properties:{occupied:'false'}}]}).length,0);
 const sources=audio.vehicleSources({listener:me,buses:[{...vehicle,id:'bus',status:'boarding'}],vehicles:[{...vehicle,id:'truck',properties:{occupied:'true',subtype:'haneyPickup'}}],players:[{...vehicle,id:'ufo',travelMode:'ufo'},{...vehicle,id:'passenger',travelMode:'motorcycle',ridingBusId:'bus'}]});
 assert.deepEqual(sources.map(s=>s.kind),['bus','truck','ufo']);
 assert.equal(audio.vehicleSources({listener:me,buses:Array.from({length:200},(_,i)=>({...vehicle,id:String(i),status:'driving'}))}).length,6);
});
test('every procedural sound has finite, non-silent bounded samples and motors loop continuously',()=>{
 for(const kind of Object.keys(audio.durations)){
  const samples=audio.samples(kind);let peak=0,energy=0;for(const value of samples){assert.ok(Number.isFinite(value));peak=Math.max(peak,Math.abs(value));energy+=value*value;}
  assert.ok(peak<=.9+1e-6&&energy/samples.length>.0001,kind);
  if(['engine','truck','bus','motorcycle','ufo','electric'].includes(kind))assert.ok(Math.abs(samples[0]-samples.at(-1))<.06,kind+' seamless loop');
 }
});
function harness(savedSettings){
 let time=0,isHidden=false;const saved=new Map(savedSettings?[['alternative-reality-audio',JSON.stringify(savedSettings)]]:[]),started=[];
 const param=()=>({value:0,setTargetAtTime(v){this.value=v;},cancelScheduledValues(){}});
 const node=()=>({connect(){},disconnect(){}});
 const context={state:'suspended',currentTime:0,destination:node(),resume(){this.state='running';return Promise.resolve();},createGain:()=>({...node(),gain:param()}),createStereoPanner:()=>({...node(),pan:param()}),createDynamicsCompressor:()=>({...node(),threshold:param(),knee:param(),ratio:param()}),createBuffer:(channels,length,rate)=>({duration:length/rate,copyToChannel(){}}),createBufferSource:()=>({...node(),playbackRate:param(),start(){started.push(this);},stop(){this.onended?.();}})};
 const scene={listener:structuredClone(me),buses:[]};
 const engine=audio.create({getScene:()=>scene,storage:{getItem:k=>saved.get(k),setItem:(k,v)=>saved.set(k,v)},contextFactory:()=>context,fetcher:()=>Promise.reject(Error('offline')),now:()=>time,wallNow:()=>time,hidden:()=>isHidden});
 return {engine,scene,started,saved,time:v=>time=v,hide:v=>isHidden=v};
}
test('audio unlocks on demand, deduplicates events, cancels delayed effects when the tab is hidden',async()=>{
 const h=harness();h.engine.local('gun');assert.equal(h.started.length,0);await h.engine.unlock();
 h.engine.local('cow','chat:1');h.engine.local('cow','chat:1');assert.equal(h.started.length,1);
 h.engine.play('explosion',me.position,'outdoor',{delay:1000});assert.equal(h.engine.status().queued,1);
 h.hide(true);h.time(2000);h.engine.tick();assert.equal(h.engine.status().queued,0);assert.equal(h.engine.status().voices,0);
 h.hide(false);h.engine.local('pour');assert.equal(h.started.length,2);
 h.hide(true);h.engine.tick();h.engine.local('gun');assert.equal(h.engine.status().voices,0);assert.equal(h.started.length,2);
});
test('motor sources are reused, fade out after parking, and stop on location change or disconnect',async()=>{
 const h=harness();await h.engine.unlock();h.scene.buses=[{id:'bus',position:{x:1,y:0},status:'driving',healthHearts:100}];
 h.engine.tick();h.engine.tick();assert.equal(h.started.length,1);assert.equal(h.engine.status().loops,1);
 h.scene.buses[0].status='out of service';h.engine.tick();assert.equal(h.engine.status().loops,0);
 h.scene.buses[0].status='driving';h.engine.tick();h.scene.listener.locationId='home';h.engine.tick();assert.equal(h.engine.status().voices,0);
 h.scene.listener=null;h.engine.tick();assert.equal(h.engine.status().loops,0);
});
test('recorded audio files exist and every bundled recording has explicit CC0 provenance',()=>{
 const fs=require('node:fs'),path=require('node:path'),folder=path.resolve(__dirname,'../src/AlternateEarth.Client2D/audio');const sources=JSON.parse(fs.readFileSync(path.join(folder,'sources.json'),'utf8'));
 for(const file of Object.values(audio.files)){assert.ok(fs.statSync(path.join(folder,file)).size>100);assert.equal(sources.find(s=>s.file===file)?.license,'CC0-1.0');}
});

test('quiet engines rise in pitch with speed and rev down to a low idle',async()=>{
 for(const kind of ['truck','engine','motorcycle']){
  const idle=audio.motorMix(kind,0),slow=audio.motorMix(kind,3),fast=audio.motorMix(kind,25);
  assert.ok(idle.rate<slow.rate&&slow.rate<fast.rate);assert.ok(idle.rate<1);
  assert.ok(idle.volume>0&&fast.volume<=.04);assert.ok(fast.volume>idle.volume);
 }
 const h=harness();await h.engine.unlock();h.scene.buses=[{id:'bus',position:{x:0,y:0},status:'driving',speedMetersPerSecond:0}];
 h.engine.tick();const source=h.started[0],idle=source.playbackRate.value;
 h.scene.buses[0].speedMetersPerSecond=20;h.time(500);h.engine.tick();assert.ok(source.playbackRate.value>idle);
 h.scene.buses[0].speedMetersPerSecond=0;h.time(1000);h.engine.tick();assert.equal(source.playbackRate.value,idle);assert.equal(h.started.length,1);
});
test('cars infer speed between snapshots, coast between packets, idle when stopped, and ignore teleports',()=>{
 const cue={id:'car',kind:'engine',locationId:'outdoor',position:{x:0,y:0}};
 let motion=audio.motorMotion(null,cue,0);assert.equal(motion.speed,0);
 motion=audio.motorMotion(motion,{...cue,position:{x:5,y:0}},500);assert.equal(motion.speed,10);
 motion=audio.motorMotion(motion,{...cue,position:{x:5,y:0}},700);assert.equal(motion.speed,10);
 motion=audio.motorMotion(motion,{...cue,position:{x:7,y:0}},1000);assert.equal(motion.speed,4);
 motion=audio.motorMotion(motion,{...cue,position:{x:7,y:0}},1900);assert.equal(motion.speed,0);
 motion=audio.motorMotion(motion,{...cue,position:{x:200,y:0}},2000);assert.equal(motion.speed,0);
 motion=audio.motorMotion(motion,{...cue,locationId:'home',position:{x:201,y:0}},2100);assert.equal(motion.speed,0);
});

test('every motor keeps changing pitch at high speeds without raising the volume ceiling',()=>{
 for(const kind of ['engine','truck','bus','motorcycle','electric','ufo']){
  const mixes=[0,5,25,40,67,100].map(speed=>audio.motorMix(kind,speed));
  for(let i=1;i<mixes.length;i++)assert.ok(mixes[i].rate>mixes[i-1].rate,kind);
  assert.equal(mixes.at(-1).volume,mixes[2].volume,kind);
 }
});

test('fast UFO snapshots retain measured speed instead of falling back to idle',()=>{
 const cue={id:'ufo',kind:'ufo',locationId:'outdoor',position:{x:0,y:0}};
 let motion=audio.motorMotion(null,cue,0);
 motion=audio.motorMotion(motion,{...cue,position:{x:50,y:0}},1000);assert.equal(motion.speed,50);
 const cruising=audio.motorMix('ufo',motion.speed).rate;
 motion=audio.motorMotion(motion,{...cue,position:{x:117,y:0}},2000);assert.equal(motion.speed,67);
 assert.ok(audio.motorMix('ufo',motion.speed).rate>cruising);
 motion=audio.motorMotion(motion,{...cue,position:{x:117,y:0}},3700);assert.equal(motion.speed,0);
});

test('moving electric bikes without an explicit speed still produce a motor source',()=>{
 const rider={...me,id:'rider',travelMode:'eBike',isMoving:true};
 assert.equal(audio.vehicleSources({listener:me,players:[rider]}).at(0)?.kind,'electric');
});
test('flamethrower whooshes, string weapons thwump, and dinosaurs have distinct calls and attack sounds',()=>{
 const event={start:me.position,end:{x:8,y:0},hit:false};
 assert.equal(audio.combatCues({...event,weapon:'flamethrower'},null,0)[0].kind,'whoosh');
 for(const weapon of ['bow','crossbow','slingshot','spearGun'])assert.equal(audio.combatCues({...event,weapon},null,0)[0].kind,'thwump');
 for(const [subtype,weapon,kind]of [['tRex','trexBite','dinoRoar'],['brontosaurus','brontosaurusStomp','dinoBellow'],['stegosaurus','stegosaurusTail','dinoBellow'],['raptor','raptorBite','dinoRasp']]){
  assert.equal(audio.animalSound({kind:'animal',subtype}),kind);assert.equal(audio.combatCues({...event,weapon},null,0)[0].kind,kind);
 }
 assert.deepEqual(audio.combatCues({...event,weapon:'stink'},null,0),[]);
});
test('fire crackle follows visible burns, expires, stays local and is bounded',async()=>{
 const h=harness();await h.engine.unlock();h.scene.fires=[{id:'flame',position:me.position,locationId:'outdoor',startedAt:500,endsAt:2000}];
 h.engine.tick();assert.equal(h.engine.status().loops,0);
 h.time(500);h.engine.tick();h.engine.tick();assert.equal(h.started.length,1);assert.equal(h.engine.status().loops,1);
 h.time(2000);h.engine.tick();assert.equal(h.engine.status().loops,0);
 const fires=Array.from({length:50},(_,i)=>({id:i,position:me.position,endsAt:5000}));assert.equal(audio.fireSources({listener:me,fires},0,0).length,4);
 assert.equal(audio.fireSources({listener:me,fires:[{...fires[0],locationId:'home'}]},0,0).length,0);
});
test('each visible fart effect sounds once, not on every update or damage tick',async()=>{
 const h=harness();await h.engine.unlock();h.scene.actors=[{...me,id:'npc',fartUntilUtc:new Date(1200).toISOString()}];
 h.scene.patches=[{id:'patch',kind:'stink',position:me.position,changesAtUtc:new Date(500).toISOString(),endsAtUtc:new Date(2000).toISOString()}];
 h.engine.tick();h.engine.tick();assert.equal(h.started.length,1);
 h.time(500);h.engine.tick();h.engine.tick();assert.equal(h.started.length,2);
 h.time(2100);h.engine.tick();assert.equal(h.started.length,2);
 h.scene.actors[0].fartUntilUtc=new Date(4000).toISOString();h.engine.tick();assert.equal(h.started.length,3);
});

test('bus diesel is audible above deep bass at idle and remains a restrained background sound',()=>{
 const data=audio.samples('bus'),rate=22050,idle=audio.motorMix('bus',0),fast=audio.motorMix('bus',25);
 const amplitude=f=>{let real=0,imaginary=0;for(let i=0;i<data.length;i++){const angle=2*Math.PI*f*i/rate;real+=data[i]*Math.cos(angle);imaginary+=data[i]*Math.sin(angle);}return 2*Math.hypot(real,imaginary)/data.length;};
 // The 112 Hz idle harmonic is stronger than the 56 Hz fundamental.
 assert.ok(amplitude(140)>amplitude(70));assert.ok(amplitude(210)>.1);
 const rms=Math.sqrt(data.reduce((sum,v)=>sum+v*v,0)/data.length);
 assert.ok(rms*idle.volume*.55>.005,'Audible default-volume idle');
 assert.ok(Math.max(...data.map(Math.abs))*fast.volume*.55<.05,'Engine stays below action effects');
 assert.ok(fast.rate>idle.rate);assert.ok(fast.volume<=.11);
 const listener={...me,ridingBusId:'bus'};
 for(const status of ['boarding','driving','dropping off','yielding'])assert.equal(audio.vehicleSources({listener,buses:[{id:'bus',position:me.position,status}]}).at(0)?.kind,'bus');
 for(const status of ['out of service','disabled'])assert.equal(audio.vehicleSources({listener,buses:[{id:'bus',position:me.position,status}]}).length,0);
});

test('removed sound controls cannot leave sounds suppressed by old saved settings',async()=>{
 for(const volume of [0,.01,1]){const h=harness({volume,muted:true});assert.equal(h.engine.status().volume,.55);await h.engine.unlock();h.engine.local('gun');assert.equal(h.started.length,1);}
 const html=require('node:fs').readFileSync('src/AlternateEarth.Client2D/index.html','utf8');assert.doesNotMatch(html,/soundVolume|soundMute|sound-controls/);
});
