/* CC0 recordings + original procedural effects. No third-party requests during play. */
(function(root,factory){const api=factory();if(typeof module==='object')module.exports=api;else root.GameAudio=api;})(globalThis,()=>{
  'use strict';
  const clamp=(n,a,b)=>Math.max(a,Math.min(b,n));
  const files={cow:'cow.mp3',chicken:'chicken.mp3',swing:'swing.ogg',cloth:'cloth.ogg',chop:'chop.ogg',coins:'coins.ogg',paper:'paper.ogg',door:'door.ogg',metal:'metal.ogg',hit:'hit.ogg',glass:'glass.ogg',stepGrass:'step-grass.ogg',stepHard:'step-hard.ogg'};
  const durations={gun:.28,rifle:.4,rocket:1,explosion:1.8,pour:1.7,splash:.6,scissors:.19,honk:.65,beep:.35,ahooga:1.2,engine:2,truck:2,motorcycle:2,electric:2,ufo:2,laser:.5,arrow:.2,teleport:.7,success:.55,error:.25,pig:.5,sheep:.95,goat:.8,cat:.8,dog:.5,bird:.5,growl:1.2,zombie:1.1,cow:1.3,chicken:.5,hit:.18,metal:.4,swing:.2,cloth:.3,chop:.2,coins:.3,paper:.3,door:.4,glass:.4,stepGrass:.13,stepHard:.13};
  const loopKinds=new Set(['engine','truck','bus','motorcycle','electric','ufo']);
  durations.bus=2;
  Object.assign(durations,{whoosh:.65,thwump:.42,fart:.8,fire:3,dinoRoar:1.8,dinoBellow:1.6,dinoRasp:.9});loopKinds.add('fire');
  Object.assign(durations,{fartSqueaker:.55,fartHonker:1.25,fartFinale:4});loopKinds.add('fartFinale');
  const dinosaurSounds={tRex:'dinoRoar',brontosaurus:'dinoBellow',stegosaurus:'dinoBellow',raptor:'dinoRasp'};
  function spatial(listener,position,locationId='outdoor',range=60){
    if(!listener||!position||(listener.locationId||'outdoor')!==locationId)return {gain:0,pan:0};
    const a=listener.position?.region,b=position.region;
    if(a&&b&&(a.latitudeBand!==b.latitudeBand||a.longitudeBand!==b.longitudeBand))return {gain:0,pan:0};
    const dx=position.x-listener.position.x,dy=position.y-listener.position.y,d=Math.hypot(dx,dy);
    if(!Number.isFinite(d)||d>=range)return {gain:0,pan:0};
    return {gain:(1-d/range)**2,pan:clamp(dx/Math.max(8,range*.45),-.85,.85)};
  }
  function animalSound(actor){if(actor?.kind!=='player'&&dinosaurSounds[actor?.subtype])return dinosaurSounds[actor.subtype];return actor?.kind==='animal'||['zombie','ufo'].includes(actor?.subtype)?({cow:'cow',chicken:'chicken',pig:'pig',sheep:'sheep',goat:'goat',cat:'cat',dog:'dog',bird:'bird',rabbit:'bird',deer:'pig',bear:'growl',cougar:'growl',tRex:'growl',brontosaurus:'growl',stegosaurus:'growl',raptor:'bird',zombie:'zombie',ufo:'laser'}[actor.subtype]||null):null;}
  function combatCues(combat,shot,now){
    const weapon=combat.weapon,start=Math.max(0,(shot?.started??now)-now),impact=start+(shot?.duration??0),cues=[];
    const add=(kind,position,delay=0,range=55,volume=.65)=>cues.push({kind,position,delay,range,volume});
    // Secondary damage events share the primary projectile's one explosion.
    if(weapon.endsWith('Explosion')&&weapon!=='craftingExplosion'||['areaHazard','molotovFire','spearImpact'].includes(weapon))return cues;
    if(['pistol','rifle','ar15','machineGun'].includes(weapon)){add(weapon==='pistol'?'gun':'rifle',combat.start,start,110,.6);if(combat.hit)add('hit',combat.end,impact,35,.25);}
    else if(weapon==='rocketLauncher'){add('rocket',combat.start,start,110);add('explosion',combat.end,impact,160,.85);}
    else if(['grenade','craftingExplosion'].includes(weapon)){if(weapon==='grenade')add('swing',combat.start,start);add('explosion',combat.end,impact,130,.75);}
    else if(weapon==='molotovCocktail'||/Gas(Bottle|Jar)$/.test(weapon)){add('swing',combat.start,start);add('glass',combat.end,impact);}
    else if(weapon==='probulator'){if(combat.statusEffect==='Probulator active')add('laser',combat.start,0,70,.4);}
    else if(weapon==='flamethrower')add('whoosh',combat.start,start,55,.5);
    else if(['bow','bowAndArrow','crossbow','spearGun','slingshot'].includes(weapon)){add('thwump',combat.start,start,50,.55);if(combat.hit)add('hit',combat.end,impact);}
    else if(['rock','ballBearing'].includes(weapon)){add('arrow',combat.start,start);if(combat.hit)add('hit',combat.end,impact);}
    else if(['canadianFart','stink'].includes(weapon))return cues; // The visible gas effect owns its one sound, not each damage tick.
    else if(/^(trex|brontosaurus|stegosaurus|raptor)/i.test(weapon)){add(/^trex/i.test(weapon)?'dinoRoar':/^raptor/i.test(weapon)?'dinoRasp':'dinoBellow',combat.start,0,95,.5);if(combat.hit)add('hit',combat.end,impact,45,.5);}
    else if(weapon==='busCollision'||weapon==='carCollision')add('metal',combat.end,0,80,.85);
    else {add('swing',combat.start,0,28,.3);if(combat.hit)add('hit',combat.end,65,35,.65);}
    return cues;
  }
  function motorMix(kind,speed=0){
    const velocity=Number.isFinite(Number(speed))?Math.max(0,Number(speed)):0;
    const throttle=clamp(velocity/25,0,1);
    // Pitch keeps responding above road speeds, including fast UFO flight.
    // Separate the pitch curve from volume so faster vehicles stay subtle.
    const revs=velocity/(velocity+(kind==='ufo'?35:kind==='electric'?6:12));
    // Motors sit well below action sounds, even at full speed and at the listener.
    if(kind==='bus')return {rate:.8+1.8*revs,volume:.08+throttle*.03};
    return kind==='ufo'||kind==='electric'?{rate:.85+1.2*revs,volume:.025+throttle*.01}:{rate:.65+2.3*revs,volume:.018+throttle*.022};
  }
  function vehicleSources({listener,players=[],actors=[],buses=[],vehicles=[]}){
    if(!listener)return [];
    const sources=[];
    const add=(id,kind,entity,speed)=>{if((entity.healthHearts??Number(entity.properties?.healthHearts??100))<=0)return;const locationId=entity.locationId||'outdoor',mix=spatial(listener,entity.position,locationId,75);if(mix.gain>.005)sources.push({id,kind,position:entity.position,locationId,range:75,speed,...motorMix(kind,speed),gain:mix.gain});};
    for(const bus of buses)if(bus.status&&!['out of service','disabled'].includes(bus.status))add(bus.id,'bus',bus,bus.speedMetersPerSecond);
    for(const vehicle of vehicles)if(vehicle.properties?.occupied==='true')add(vehicle.id,vehicle.properties?.subtype==='haneyPickup'?'truck':'engine',vehicle,vehicle.speedMetersPerSecond);
    for(const person of [...players,...actors]){if(person.ridingBusId||person.abduction)continue;const mode=person.travelMode&&person.travelMode!=='walk'?person.travelMode:person.subtype;if(mode==='ufo')add(person.id,'ufo',person,person.speedMetersPerSecond);else if(['motorcycle','dirtBike'].includes(mode))add(person.id,'motorcycle',person,person.speedMetersPerSecond);else if(mode==='eBike'&&(person.speedMetersPerSecond>.1||person.isMoving))add(person.id,'electric',person,person.speedMetersPerSecond);}
    return sources.sort((a,b)=>b.gain-a.gain||a.id.localeCompare(b.id)).slice(0,6);
  }
  function motorMotion(previous,cue,time){
    const p=cue.position,region=p.region,key=`${cue.locationId}:${region?.latitudeBand}:${region?.longitudeBand}`;
    let speed=0,changed=time,interval=500;
    if(previous&&previous.key===key&&previous.kind===cue.kind){
      const distance=Math.hypot(p.x-previous.x,p.y-previous.y),elapsed=time-previous.changed;
      interval=previous.interval;changed=previous.changed;speed=previous.speed;
      if(distance>.001&&elapsed>0){
        // Infer traffic speed from successive snapshots; retain it between packets.
        const measured=distance*1000/elapsed;
        speed=elapsed<=2000&&measured<=120?measured:0;
        interval=clamp(elapsed,100,1000);changed=time;
      }else if(elapsed>Math.max(650,interval*1.6))speed=0;
    }
    if(Number.isFinite(cue.speed))speed=Math.max(0,cue.speed);
    return {key,kind:cue.kind,x:p.x,y:p.y,changed,interval,speed};
  }
  function fireSources({listener,fires=[]},time,wall){
    if(!listener)return [];
    return fires.filter(f=>f.endsAt>wall&&(f.startedAt??0)<=time).map(f=>({...f,id:'fire:'+f.id,kind:'fire',range:40,rate:1,volume:.065,gain:spatial(listener,f.position,f.locationId||'outdoor',40).gain})).filter(f=>f.gain>.005).sort((a,b)=>b.gain-a.gain).slice(0,4);
  }
  function fartSources({listener,actors=[],patches=[],areaHazards=[]},wall){
    if(!listener)return [];
    return [...actors.filter(a=>Date.parse(a.fartUntilUtc)>wall).map(a=>({id:`fart:${a.id}:${a.fartUntilUtc}`,position:a.position,locationId:a.locationId||'outdoor'})),...patches.filter(p=>p.kind==='stink'&&Date.parse(p.changesAtUtc)<=wall&&Date.parse(p.endsAtUtc)>wall).map(p=>({id:'fart:'+p.id,position:p.position,locationId:'outdoor'})),...areaHazards.filter(p=>p.effect==='musicalFruit'&&Date.parse(p.startedAtUtc)<=wall&&Date.parse(p.endsAtUtc)>wall).map(p=>({id:'fart:'+p.id,position:p.position,locationId:p.locationId,kind:[...p.id].reduce((n,c)=>n+c.charCodeAt(0),0)%2?'fartSqueaker':'fartHonker'}))].map(f=>({...f,gain:spatial(listener,f.position,f.locationId,35).gain})).filter(f=>f.gain>.005).sort((a,b)=>b.gain-a.gain);
  }
  function fruitFinaleSources({listener,areaHazards=[]},wall){
    return areaHazards.filter(p=>p.effect==='musicalFruitFinale'&&Date.parse(p.startedAtUtc)<=wall&&Date.parse(p.endsAtUtc)>wall&&spatial(listener,p.position,p.locationId,55).gain>.005).map(p=>({id:'finale:'+p.id,kind:'fartFinale',position:p.position,locationId:p.locationId,range:55,rate:1,volume:.85}));
  }
  function samples(kind,rate=22050){
    const duration=durations[kind]||.3,data=new Float32Array(Math.ceil(duration*rate)),tau=Math.PI*2;let random=9137,phase=0,low=0;
    const tone=(f,t)=>Math.sin(tau*f*t);
    for(let i=0;i<data.length;i++){
      const t=i/rate,p=t/duration;random=(Math.imul(random,1664525)+1013904223)|0;const noise=random/2147483648;low=low*.88+noise*.12;
      const end=Math.min(1,t/.008,(duration-t)/.025),decay=Math.exp(-t*13);let v=0;
      if(kind==='gun'||kind==='rifle')v=(noise*.75+tone(90,t)*.7)*Math.exp(-t*(kind==='gun'?22:15))+low*.4*Math.exp(-t*7);
      else if(kind==='explosion')v=(low*2+tone(42,t)*.3)*Math.exp(-t*2.7)+noise*.35*Math.exp(-t*20);
      else if(kind==='rocket')v=(noise*.3+low+tone(160-70*p,t)*.14)*Math.sin(Math.PI*p)**.4;
      else if(kind==='whoosh')v=(low*1.7+noise*.2+tone(65,t)*.12)*Math.sin(Math.PI*p)**.65;
      else if(kind==='thwump'){phase+=tau*(65+210*Math.exp(-t*16))/rate;v=(Math.sin(phase)*.65+low*.6+noise*.12)*Math.exp(-t*10);}
      else if(kind==='fart'){phase+=tau*(65+40*(1-p)+10*tone(18,t))/rate;v=(Math.sin(phase)+.35*Math.sin(phase*2)+noise*.18)*(.55+.45*tone(27,t))*.55*Math.sin(Math.PI*p)**.5;}
      else if(kind.startsWith('fart')){const squeak=kind==='fartSqueaker',finale=kind==='fartFinale',f=squeak?240:finale?58:82;phase+=tau*(f*(1+.3*tone(finale?1:3,t))+(squeak?140:35)*(1-p))/rate;v=(Math.sin(phase)+.4*Math.sin(phase*2)+.2*Math.sin(phase*3)+noise*.2)*(.7+.3*tone(squeak?40:23,t))*.48*(finale?Math.min(1,t/.02,(duration-t)/.02):Math.sin(Math.PI*p)**.4);}
      else if(kind.startsWith('dino')){const rasp=kind==='dinoRasp',bellow=kind==='dinoBellow',f=rasp?310:bellow?55:78;phase+=tau*(f*(1+.55*Math.sin(Math.PI*p)+.06*tone(rasp?31:18,t)))/rate;v=(Math.sin(phase)*.4+Math.sin(phase*2)*.22+Math.sin(phase*3)*.12+low*(bellow?.3:1.3)+noise*(rasp?.13:.03))*(.7+.3*tone(rasp?24:11,t))*Math.sin(Math.PI*p)**.55;}
      else if(kind==='fire'){const edge=Math.min(1,t/.025,(duration-t)/.025),crackle=Math.max(0,noise-.965)*16;v=(low*.65+noise*.025+crackle)*edge;}
      else if(kind==='honk')v=(tone(220,t)+tone(277,t)*.7+tone(440,t)*.25)*.4;
      else if(kind==='beep')v=(tone(660,t)+tone(990,t)*.2)*.55;
      else if(kind==='ahooga'){phase+=tau*(t<.16?150+t*1100:t<.72?330+12*tone(22,t):330-(t-.72)*390)/rate;v=(Math.sin(phase)+.35*Math.sin(phase*2)+.2*noise)*(.7+.3*tone(28,t));}
      else if(loopKinds.has(kind)){
        if(kind==='ufo')v=(Math.sin(tau*180*t+5*Math.sin(tau*6*t))*.45+Math.sin(tau*360*t+8*Math.sin(tau*6*t))*.12)*(.7+.3*tone(12,t));
        else if(kind==='electric')v=tone(140,t)*.3+tone(420,t)*.1;
        // Audible diesel harmonics survive small speakers even at idle (56 Hz fundamental).
        else if(kind==='bus')v=(.35*tone(70,t)+.65*tone(140,t)+.45*tone(210,t)+.2*tone(420,t))*.38*(.85+.15*tone(10,t));
        else {const f=kind==='truck'?40:kind==='motorcycle'?80:60;v=(tone(f,t)+.5*tone(f*2,t)+.25*tone(f*3,t))*.3*(.7+.3*tone(kind==='motorcycle'?16:12,t));}
      }
      else if(kind==='pour'||kind==='splash'){phase+=tau*(480+170*tone(9,t)+120*tone(17,t))/rate;v=(noise*.3+low*.8+Math.sin(phase)*.13)*(.5+.5*Math.sin(Math.PI*p));}
      else if(kind==='scissors')v=noise*(t<.055?.65:Math.exp(-(t-.07)*45)*.25)+tone(2700,t)*decay*.12;
      else if(kind==='teleport'||kind==='laser'){phase+=tau*(kind==='teleport'?200+1200*p:900-750*p)/rate;v=Math.sin(phase)*.5+noise*.1;}
      else if(kind==='success')v=tone(t<.18?523:t<.36?659:784,t)*.4;
      else if(kind==='error')v=tone(140,t)*.4;
      else if(['cow','sheep','goat','cat','pig','dog','bird','growl','zombie','chicken'].includes(kind)){
        const f={cow:105,sheep:220,goat:270,cat:480,pig:170,dog:160,bird:2100,growl:65,zombie:85,chicken:600}[kind];
        phase+=tau*(f*(1+.17*Math.sin(Math.PI*p)+.04*tone(kind==='goat'?22:8,t)))/rate;
        const pulse=['dog','chicken','bird'].includes(kind)?Math.max(0,tone(kind==='dog'?5:9,t)):1;
        v=(Math.sin(phase)+.4*Math.sin(phase*2)+.18*Math.sin(phase*3)+low*.4)*.45*pulse*Math.sin(Math.PI*p);
      }
      else v=(noise*.5+tone(kind==='metal'?1700:150,t)*.4)*decay;
      data[i]=clamp(v*(loopKinds.has(kind)?1:end),-.9,.9);
    }
    return data;
  }
  function create({getScene,contextFactory=()=>new (globalThis.AudioContext||globalThis.webkitAudioContext)(),fetcher=globalThis.fetch,now=()=>performance.now(),wallNow=()=>Date.now(),hidden=()=>!!globalThis.document?.hidden}={}){
    let context,master,volume=.55,unlocked=false,lastStep=null,lastStepAt=0,sceneLocation=null;
    const buffers=new Map(),loops=new Map(),motion=new Map(),voices=new Set(),queue=[],seen=new Map();let activeFarts=new Set();
    function available(){return unlocked&&context?.state==='running'&&!hidden()&&volume>0;}
    function gain(){if(master)master.gain.setTargetAtTime(hidden()?0:volume,context.currentTime,.025);}
    function buffer(kind){if(buffers.has(kind))return buffers.get(kind);const data=samples(kind),b=context.createBuffer(1,data.length,22050);b.copyToChannel(data,0);buffers.set(kind,b);return b;}
    async function unlock(){
      try{
        if(!context){context=contextFactory();master=context.createGain();const limiter=context.createDynamicsCompressor();limiter.threshold.value=-12;limiter.knee.value=6;limiter.ratio.value=12;master.connect(limiter);limiter.connect(context.destination);
          for(const [kind,file]of Object.entries(files))Promise.resolve(fetcher('audio/'+file)).then(r=>{if(!r.ok)throw Error('Audio unavailable');return r.arrayBuffer();}).then(b=>context.decodeAudioData(b)).then(b=>{buffers.set(kind,b);}).catch(()=>{});
        }
        await context.resume();unlocked=true;gain();return true;
      }catch{return false;}
    }
    function mix(cue){const scene=getScene?.();return spatial(scene?.listener,cue.position,cue.locationId||'outdoor',cue.range||60);}
    function stopVoice(voice,immediate=false){if(!voices.has(voice))return;voices.delete(voice);try{voice.gain.gain.cancelScheduledValues(context.currentTime);voice.gain.gain.setTargetAtTime(0,context.currentTime,.035);voice.source.stop(context.currentTime+(immediate?0:.15));}catch{}}
    function start(cue,loop=false){
      if(!available())return null;const spatialMix=mix(cue);if(spatialMix.gain<=.005||voices.size>=24)return null;
      const source=context.createBufferSource(),g=context.createGain(),pan=context.createStereoPanner();source.buffer=buffer(cue.kind);source.loop=loop;source.playbackRate.value=cue.rate||1;g.gain.value=spatialMix.gain*(cue.volume??.6);pan.pan.value=spatialMix.pan;source.connect(g);g.connect(pan);pan.connect(master);
      const voice={source,gain:g,pan,kind:cue.kind};voices.add(voice);source.onended=()=>{voices.delete(voice);source.disconnect();g.disconnect();pan.disconnect();};source.start();return voice;
    }
    function play(kind,position,locationId='outdoor',options={}){
      if(!available()||!kind)return;const cue={kind,position,locationId,...options};if(mix(cue).gain<=.005)return;
      const key=options.id;if(key){if(seen.has(key))return;seen.set(key,now());if(seen.size>256)seen.delete(seen.keys().next().value);}
      if(kind==='craft'||kind==='craftMetal'){
        const sequence=kind==='craftMetal'?['metal','metal','metal','metal']:['scissors','cloth','scissors','hit','cloth'];
        sequence.forEach((sound,index)=>play(sound,position,locationId,{...options,id:undefined,delay:index*210,volume:.42,range:25}));return;
      }
      if(options.delay>0){if(queue.length<64)queue.push({...cue,due:now()+options.delay});return;}start(cue);
    }
    function clear(){queue.length=0;for(const voice of [...voices])stopVoice(voice,true);loops.clear();motion.clear();lastStep=null;activeFarts.clear();}
    function tick(){
      const scene=getScene?.();if(!available()||!scene?.listener){clear();return;}
      const location=scene.listener.locationId||'outdoor';if(sceneLocation!==null&&sceneLocation!==location)clear();sceneLocation=location;
      for(let i=queue.length-1;i>=0;i--)if(queue[i].due<=now()){const cue=queue.splice(i,1)[0];if(now()-cue.due<600)start(cue);}
      const farts=fartSources(scene,wallNow());let fartCount=0;for(const fart of farts)if(!activeFarts.has(fart.id)&&fartCount++<3)play(fart.kind||'fart',fart.position,fart.locationId,{id:fart.id,range:35,volume:.45});activeFarts=new Set(farts.map(f=>f.id));
      const wanted=[...fruitFinaleSources(scene,wallNow()),...vehicleSources(scene),...fireSources(scene,now(),wallNow())],ids=new Set(wanted.map(v=>v.id));for(const [id,voice]of loops)if(!ids.has(id)){stopVoice(voice);loops.delete(id);}for(const id of motion.keys())if(!ids.has(id))motion.delete(id);
      for(const cue of wanted){
        if(cue.kind!=='fire'&&cue.kind!=='fartFinale'){const movement=motorMotion(motion.get(cue.id),cue,now());motion.set(cue.id,movement);Object.assign(cue,motorMix(cue.kind,movement.speed));}
        let voice=loops.get(cue.id);if(voice&&voice.kind!==cue.kind){stopVoice(voice);loops.delete(cue.id);voice=null;}if(!voice){voice=start(cue,true);if(voice)loops.set(cue.id,voice);}
        if(voice){const m=mix(cue);voice.gain.gain.setTargetAtTime(m.gain*cue.volume,context.currentTime,.25);voice.pan.pan.setTargetAtTime(m.pan,context.currentTime,.1);voice.source.playbackRate.setTargetAtTime(cue.rate,context.currentTime,cue.rate>voice.source.playbackRate.value ? .18 : .4);}
      }
      const me=scene.listener,p=me.position,moved=lastStep?Math.hypot(p.x-lastStep.x,p.y-lastStep.y):0;
      if(!me.ridingBusId&&!me.waitingAtBusStopId&&['walk','run','swim','scuba'].includes(me.travelMode)&&moved>.15&&moved<15&&now()-lastStepAt>320){play(['swim','scuba'].includes(me.travelMode)||scene.underwater?'splash':location!=='outdoor'?'stepHard':'stepGrass',p,location,{volume:.12,range:15});lastStepAt=now();}lastStep={...p};
    }
    function speech(chat,actor){const kind=animalSound(actor);if(kind)play(kind,actor.position,actor.locationId||'outdoor',{id:chat.id,range:55,volume:.55});}
    function combat(event,shot,locationId){if(!locationId)return;for(const cue of combatCues(event,shot,now()))play(cue.kind,cue.position,locationId,cue);}
    function local(kind,id){const me=getScene?.()?.listener;if(me)play(kind,me.position,me.locationId||'outdoor',{id,volume:.5,range:25});}
    function bindLifecycle(){
      globalThis.addEventListener('pointerdown',unlock,{passive:true});globalThis.addEventListener('keydown',unlock,{passive:true});
      globalThis.document.addEventListener('visibilitychange',()=>{clear();gain();if(hidden())context?.suspend().catch(()=>{});else if(unlocked)context?.resume().catch(()=>{});});globalThis.addEventListener('pagehide',clear);
    }
    return {unlock,play,local,speech,combat,tick,clear,bindLifecycle,status:()=>({volume,unlocked,voices:voices.size,loops:loops.size,queued:queue.length})};
  }
  return {create,spatial,animalSound,combatCues,vehicleSources,motorMix,motorMotion,fireSources,fartSources,fruitFinaleSources,samples,files,durations};
});
