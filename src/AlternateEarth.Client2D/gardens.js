const Gardens = (() => {
  const isGarden=e=>e?.properties?.subtype==='garden';
  const footprint=p=>[{x:p.x-2,y:p.y-1.5},{x:p.x+2,y:p.y-1.5},{x:p.x+2,y:p.y+1.5},{x:p.x-2,y:p.y+1.5}];
  function withinHome(p,center,radius=40){return footprint(p).every(v=>Math.hypot(v.x-center.x,v.y-center.y)<=radius);}
  const isAnimal=e=>['farmCow','farmChicken'].includes(e?.properties?.subtype);
  function drawAnimal(ctx,e,project,scale){
    if(!isAnimal(e))return false;
    const p=project(e.position),cow=e.properties.subtype==='farmCow',t=Date.now()/700;
    ctx.save();ctx.translate(p.x,p.y);ctx.scale(scale,scale);
    const ellipse=(x,y,rx,ry,color)=>{ctx.fillStyle=color;ctx.beginPath();ctx.ellipse(x,y,rx,ry,0,0,Math.PI*2);ctx.fill();};
    ellipse(0,.3,cow?1.1:.45,.2,'#0004');
    if(cow){
      ctx.strokeStyle='#b9b5a3';ctx.lineWidth=.14;ctx.beginPath();for(const x of [-.65,-.4,.45,.7]){ctx.moveTo(x,0);ctx.lineTo(x,.48);}ctx.stroke();
      ellipse(0,-.25,.94,.5,'#f0eada');ellipse(-.35,-.38,.3,.25,'#363a36');ellipse(.4,-.1,.23,.22,'#363a36');
      ellipse(.84,-.48,.35,.36,'#f0eada');ellipse(1.01,-.31,.25,.15,'#d7a0a0');ellipse(.85,-.56,.045,.05,'#252827');
      ellipse(.53,-.69,.18,.08,'#c3b8a1');ellipse(1.07,-.72,.15,.07,'#c3b8a1');ellipse(.1,.22,.21,.13,'#d7a0a0');
      ctx.strokeStyle='#c3b8a1';ctx.lineWidth=.06;ctx.beginPath();ctx.moveTo(-.88,-.4);ctx.quadraticCurveTo(-1.24,-.2,-1.15+Math.sin(t)*.1,.2);ctx.stroke();
    }else{
      ctx.strokeStyle='#d7a148';ctx.lineWidth=.05;ctx.beginPath();for(const x of [-.12,.13]){ctx.moveTo(x,0);ctx.lineTo(x,.3);ctx.lineTo(x+.13,.33);}ctx.stroke();
      ellipse(0,-.12,.37,.28,'#f1e2bf');ellipse(-.08,-.1,.21,.16,'#c99855');const bob=Math.sin(t)*.03;
      ellipse(.27,-.38+bob,.18,.22,'#f1e2bf');ellipse(.27,-.6+bob,.11,.065,'#c84838');ellipse(.36,-.4+bob,.025,.025,'#282920');
      ctx.fillStyle='#e5ac43';ctx.beginPath();ctx.moveTo(.41,-.39+bob);ctx.lineTo(.55,-.33+bob);ctx.lineTo(.4,-.29+bob);ctx.fill();
      ctx.strokeStyle='#a87940';ctx.lineWidth=.12;ctx.beginPath();ctx.moveTo(-.26,-.16);ctx.lineTo(-.48,-.38);ctx.stroke();
    }
    const quantity=Number(e.properties.productQuantity)||0;
    for(let i=0;i<quantity;i++)ellipse(-.55+(i%3)*.2,.65+Math.floor(i/3)*.15,cow?.14:.075,cow?.08:.1,cow?'#785236':'#fff0c7');
    ctx.restore();if(quantity&&scale>9){ctx.save();ctx.font='11px system-ui';ctx.textAlign='center';ctx.fillStyle='#fff3c8';ctx.fillText(`${quantity} ${cow?'fertilizer':'eggs'}`,p.x,p.y+scale+12);ctx.restore();}return true;
  }
  function draw(ctx,e,project,scale){
    if(drawAnimal(ctx,e,project,scale))return true;
    if(e.properties?.subtype==='gardenHose'){const p=project(e.position);ctx.save();ctx.translate(p.x,p.y);ctx.shadowColor='#172f20';ctx.shadowBlur=2;ctx.strokeStyle='#76c780';ctx.lineWidth=Math.max(2,scale*.1);ctx.beginPath();ctx.ellipse(0,0,scale*.32,scale*.19,0,0,Math.PI*2);ctx.ellipse(0,-scale*.08,scale*.28,scale*.16,0,0,Math.PI*2);ctx.moveTo(scale*.28,0);ctx.quadraticCurveTo(scale*.5,scale*.3,scale*.05,scale*.35);ctx.stroke();ctx.strokeStyle='#a6bdc2';ctx.lineWidth=Math.max(2,scale*.13);ctx.beginPath();ctx.moveTo(-scale*.15,0);ctx.lineTo(-scale*.15,-scale*.6);ctx.lineTo(scale*.15,-scale*.6);ctx.lineTo(scale*.15,-scale*.45);ctx.stroke();ctx.strokeStyle='#c15443';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(-scale*.04,-scale*.7);ctx.lineTo(scale*.18,-scale*.7);ctx.stroke();ctx.restore();return true;}
    if(!isGarden(e))return false;const props=e.properties,dead=props.state==='rubble',points=footprint(e.position).map(project);
    ctx.save();ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath();ctx.fillStyle=dead?'#70665a':'#69452d';ctx.fill();ctx.strokeStyle=dead?'#9a8770':'#be925c';ctx.lineWidth=Math.max(1,scale*.07);ctx.stroke();
    const ready=!props.readyAtUtc||Date.parse(props.readyAtUtc)<=Date.now();
    for(let row=0;row<3;row++)for(let col=0;col<5;col++){
      const p=project({x:e.position.x-1.55+col*.64,y:e.position.y-1+row*.65});
      ctx.strokeStyle=dead?'#ad9b7f':'#3a873d';ctx.lineWidth=Math.max(1,scale*.08);ctx.beginPath();ctx.moveTo(p.x-scale*.1,p.y);ctx.lineTo(p.x,p.y-scale*(dead?.07:.22));ctx.lineTo(p.x+scale*.1,p.y-scale*.08);ctx.stroke();
      if(!dead&&ready&&scale>8){ctx.font=`${Math.max(8,scale*.3)}px serif`;ctx.textAlign='center';ctx.fillText(Survival.glyph(props.itemType),p.x,p.y-scale*.2);}
    }
    const sign=project({x:e.position.x+.25,y:e.position.y+1.12});ctx.strokeStyle='#ae8957';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(sign.x,sign.y);ctx.lineTo(sign.x,sign.y-scale*.55);ctx.stroke();
    if(scale>7){const label=dead?'Destroyed':props.itemType;ctx.font=`600 ${Math.max(9,Math.min(13,scale*.4))}px system-ui`;const width=Math.min(ctx.measureText(label).width+10,scale*3.1);ctx.fillStyle='#e5ce96';ctx.fillRect(sign.x-width/2,sign.y-scale*.55-14,width,18);ctx.fillStyle='#423423';ctx.textAlign='center';ctx.fillText(label,sign.x,sign.y-scale*.55,width-4);}
    ctx.restore();return true;
  }
  function create({state,send,stopTravel,showToast,navigateTo,beginFollowCommand,ui,project,ring}){
    let placement=null,preview=null,valid=false,dialog=null,busy=false,previousView=null;
    const build=document.createElement('button');build.id='buildGardenButton';build.type='button';build.textContent='Build garden';build.hidden=true;ui.actionMenu.append(build);build.onclick=()=>{stopTravel();send({type:'requestGardenBuild'});ui.actionMenu.hidden=true;};
    const hoseButton=document.createElement('button');hoseButton.id='drinkHoseButton';hoseButton.type='button';hoseButton.textContent='Drink from garden hose';hoseButton.hidden=true;ui.actionMenu.append(hoseButton);let nearbyHose=null;hoseButton.onclick=()=>{if(nearbyHose)send({type:'drinkHose',entityId:nearbyHose.id});ui.actionMenu.hidden=true;};
    const banner=document.createElement('div');banner.className='garden-placement-banner';banner.hidden=true;const info=document.createElement('span'),cancel=document.createElement('button');cancel.textContent='Cancel';banner.append(info,cancel);document.body.append(banner);
    function clear(){if(previousView){state.scale=previousView.scale;state.camera=previousView.camera;state.follow=previousView.follow;previousView=null;}placement=null;preview=null;banner.hidden=true;busy=false;}
    cancel.onclick=clear;addEventListener('keydown',event=>{if(event.key==='Escape')clear();});
    function open(title){dialog?.remove();dialog=document.createElement('dialog');dialog.className='garden-dialog';const h=document.createElement('h2');h.textContent=title;dialog.append(h);const close=document.createElement('button');close.textContent='Close';close.onclick=()=>dialog.close();dialog.append(close);document.body.append(dialog);dialog.showModal();return dialog;}
    function button(parent,label,run,disabled=false){const b=document.createElement('button');b.type='button';b.textContent=label;b.disabled=disabled;b.onclick=run;parent.append(b);return b;}
    function updateButton(){const me=state.players.get(state.playerId),house=state.baseById.get(state.privateState?.base?.buildingId);build.hidden=!!state.dungeon||!me||!house||Math.hypot(me.position.x-house.position.x,me.position.y-house.position.y)>40;nearbyHose=!state.dungeon&&me?state.base.filter(e=>e.properties?.subtype==='gardenHose'&&Math.hypot(e.position.x-me.position.x,e.position.y-me.position.y)<=4).sort((a,b)=>Math.hypot(a.position.x-me.position.x,a.position.y-me.position.y)-Math.hypot(b.position.x-me.position.x,b.position.y-me.position.y))[0]:null;hoseButton.hidden=!nearbyHose;}
    function options(data){clear();const d=open('Build a garden'),details=document.createElement('p');details.textContent=`Crafting level ${data.craftingLevel} (level 5 required). Each attempt uses 50 matching seeds, 20 wood and 5 fertilizer from backpack and Home storage, even on failure. ${data.bookBonus?'Permanent gardening book bonus: +5 percentage points.':''}`;d.append(details);
      for(const option of data.options){const row=document.createElement('div');row.className='garden-build-row';const text=document.createElement('span');text.textContent=`${option.crop}: seeds ${option.seeds}/50 · wood ${option.wood}/20 · fertilizer ${option.fertilizer}/5 · ${(option.successChance*100).toFixed(1)}% success`;row.append(text);button(row,'Choose location',()=>{d.close();stopTravel();previousView={scale:state.scale,camera:{...state.camera},follow:state.follow};state.camera={...data.center};state.follow=false;state.scale=Math.max(2,Math.min(state.scale,(Math.min(Math.max(300,innerWidth-400),innerHeight)-160)/(2*data.radius)));placement={...data,crop:option.crop};info.textContent=`Place ${option.crop} garden on clear grass inside the ring. Click to attempt construction.`;banner.hidden=false;},!option.canBuild);d.append(row);}
    }
    function click(point){
      if(placement){if(!busy){move(point);if(!valid){showToast('Choose unused grass with the entire plot inside the Home range ring.');return true;}busy=true;info.textContent='Building garden…';send({type:'buildGarden',crop:placement.crop,x:point.x,y:point.y});}return true;}
      if(state.dungeon)return false;
      const animal=state.base.find(e=>isAnimal(e)&&Math.hypot(e.position.x-point.x,e.position.y-point.y)<(e.properties.subtype==='farmCow'?1.5:1));
      if(animal){
        stopTravel();const cow=animal.properties.subtype==='farmCow',d=open(cow?'Farm cow':'Farm chicken'),me=state.players.get(state.playerId),near=Math.hypot(me.position.x-animal.position.x,me.position.y-animal.position.y)<=4;
        const quantity=Number(animal.properties.productQuantity)||0,text=document.createElement('p');text.textContent=`${quantity} ${cow?'fertilizer':'eggs'} waiting nearby. Animals may produce more while players stay within 50 meters.`;d.append(text);
        if(!near)button(d,'Walk to animal',()=>{d.close();navigateTo(animal.position);});
        button(d,`Collect ${cow?'fertilizer':'eggs'} (${quantity})`,()=>{d.close();send({type:'useFarmAnimal',entityId:animal.id,action:'collect'});},!near||!quantity);
        if(cow){const q=type=>(state.privateState?.inventory?.items||[]).find(i=>i.itemType===type)?.quantity||0,jar=q('emptyGlassJar')>0,has=jar||q('emptyGlassBottle')>0,ready=!(Date.parse(animal.properties.milkReadyUtc)>Date.now());const note=document.createElement('p');note.textContent='Milking: 80% success. Jars are used first; otherwise a bottle. Failure breaks the container. On success, 15% chance of fertilizer or urine instead of milk. Cow rests five minutes after each attempt.';d.append(note);
          button(d,!ready?'Cow resting':has?`Milk into ${jar?'jar':'bottle'}`:'Milk — empty jar or bottle required',()=>{d.close();send({type:'useFarmAnimal',entityId:animal.id,action:'milk'});},!near||!has||!ready);
        }return true;
      }
      const hose=state.base.find(e=>e.properties?.subtype==='gardenHose'&&Math.hypot(e.position.x-point.x,e.position.y-point.y)<1);if(hose){stopTravel();const d=open('Garden hose');const me=state.players.get(state.playerId),near=Math.hypot(me.position.x-hose.position.x,me.position.y-hose.position.y)<=4;button(d,near?'Drink from hose':'Walk to hose',()=>{d.close();if(near)send({type:'drinkHose',entityId:hose.id});else navigateTo(hose.position);});return true;}const entity=state.base.find(e=>isGarden(e)&&Math.abs(e.position.x-point.x)<=2.15&&Math.abs(e.position.y-point.y)<=1.65);if(!entity)return false;
      stopTravel();const d=open(entity.properties.itemType+' garden');const text=document.createElement('p');text.textContent=entity.properties.state==='rubble'?'Permanently destroyed. Rubble disappears after five minutes.':Date.parse(entity.properties.readyAtUtc)>Date.now()?'Nothing available. Check again tomorrow.':'Harvest 1–10 produce and 0–5 matching seeds. All players share the 12-hour harvest cooldown.';d.append(text);
      if(entity.properties.state!=='rubble'){
        button(d,'Harvest',()=>{d.close();const me=state.players.get(state.playerId);if(Math.hypot(me.position.x-entity.position.x,me.position.y-entity.position.y)<=3.8)send({type:'harvestGarden',entityId:entity.id});else{state.pendingGarden=entity.id;navigateTo(entity.position);}});
        button(d,'Attack garden',()=>{d.close();beginFollowCommand('attack',entity,true);});
      }return true;
    }
    function move(point){if(!placement)return;preview=point;valid=withinHome(point,placement.center,placement.radius)&&!state.dungeon;
      if(valid)valid=!state.base.some(e=>{if(['propertyBoundary','stateBoundary','airport'].includes(e.kind)||e.kind==='terrain'&&e.properties?.terrain?.toLowerCase()==='grass')return false;const g=e.geometry?.length?e.geometry:[e.position],pad=e.kind==='road'?Number(e.properties?.widthMeters||5)/2:e.kind==='sidewalk'?Number(e.properties?.widthMeters||3)/2:e.kind==='water'?5:e.kind==='tree'?1.5:e.kind==='bush'?1:e.kind==='vehicle'?2.5:.3;return Math.min(...g.map(p=>p.x))-pad<=point.x+2.1&&Math.max(...g.map(p=>p.x))+pad>=point.x-2.1&&Math.min(...g.map(p=>p.y))-pad<=point.y+1.6&&Math.max(...g.map(p=>p.y))+pad>=point.y-1.6;});
    }
    function overlay(ctx){if(!placement)return;if(state.dungeon){clear();return;}ring(placement.center,placement.radius,'#f8da75',[8,5]);if(!preview)return;const points=footprint(preview).map(project);ctx.save();ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath();ctx.fillStyle=valid?'rgba(115,240,142,.3)':'rgba(255,82,82,.4)';ctx.strokeStyle=valid?'#96efa5':'#ff6868';ctx.lineWidth=2;ctx.fill();ctx.stroke();ctx.restore();}
    function advance(me){if(!state.pendingGarden)return;const entity=state.baseById.get(state.pendingGarden);if(!entity||state.dungeon){state.pendingGarden=null;return;}if(Math.hypot(me.position.x-entity.position.x,me.position.y-entity.position.y)<=3.8){const id=entity.id;stopTravel();state.pendingGarden=null;send({type:'harvestGarden',entityId:id});}}
    function sink(item){const d=open('Kitchen sink');const p=document.createElement('p');p.textContent='Free purified water. Fill containers from your backpack; drinking a filled container returns it empty. A jar gives five times the benefits of a bottle.';d.append(p);const me=state.players.get(state.playerId),inRange=Math.hypot(me.position.x-item.position.x,me.position.y-item.position.y)<=4;const q=type=>(state.privateState?.inventory?.items||[]).find(i=>i.itemType===type)?.quantity||0;
      if(!inRange){p.textContent+=' Move within 4 meters to use the sink.';button(d,'Walk to sink',()=>{d.close();navigateTo(item.position);});}
      for(const [action,label,empty] of [['drink','Drink purified water',null],['bottle','Fill bottle','emptyGlassBottle'],['jar','Fill jar (5× benefits)','emptyGlassJar']])button(d,label+(empty?` · ${q(empty)} empty`:''),()=>{d.close();send({type:'useKitchenSink',sinkId:item.id,action});},!inRange||!!empty&&!q(empty));
    }
    return {click,options,updateButton,move,overlay,advance,sink,result:()=>clear(),error:()=>{busy=false;if(placement)info.textContent='Choose a valid location and click to try again, or cancel.';}};
  }
  return {isGarden,isAnimal,footprint,withinHome,draw,create};
})();
if(typeof module!=='undefined')module.exports=Gardens;
