(function(root){
  'use strict';
  const resistanceText=values=>Object.entries(values||{}).map(([type,value])=>`${type} ${(value*100).toFixed(1)}%`).join(' · ')||'None';
  const gearText=item=>item.gear?`Level ${item.gear.level} · ${item.gear.quality} · ${resistanceText(item.gear.resistances)}`:'';
  const effectsText=effects=>(effects||[]).map(e=>`${e.type}: ${e.damagePerSecond.toFixed(2)}/s, ${Math.max(0,Math.ceil((Date.parse(e.endsAtUtc)-Date.now())/1000))}s`).join(' · ')||'None';
  function inspect(data){
    const panel=document.getElementById('actorInspection');if(!panel)return;
    panel.replaceChildren();panel.hidden=false;
    const heading=document.createElement('h2');heading.textContent=data.actor.name;
    const close=document.createElement('button');close.textContent='Close';close.onclick=()=>panel.hidden=true;panel.append(heading,close);
    for(const [label,value] of Object.entries({Level:data.level,Health:`${data.actor.healthHearts.toFixed(2)} / ${data.actor.maximumHealthHearts} hearts`,Status:`${data.allegiance} · Friend rating ${data.friendRating.toFixed(2)}`,Immunity:data.actor.damageImmune?'Immune to direct damage':'None',Resistances:resistanceText(data.resistances),Effects:effectsText(data.effects),Attack:`${data.actor.equippedWeapon}: ${(data.attack||[]).map(x=>x.type+(x.seconds?` (${x.seconds}s)`:'' )).join(' + ')}`})){
      const row=document.createElement('p');row.textContent=`${label}: ${value}`;panel.append(row);
    }
    for(const [slot,gear] of Object.entries(data.actor.gear||{})){const row=document.createElement('p');row.textContent=`${slot}: ${gearText({gear})}`;panel.append(row);}
  }
  function viewButton(actor,send){
    const menu=document.getElementById('actionMenu');if(!menu)return;
    let button=document.getElementById('inspectActorButton');if(!button){button=document.createElement('button');button.id='inspectActorButton';button.textContent='View character';menu.append(button);}
    button.hidden=!actor;button.onclick=()=>{if(actor)send({type:'inspectActor',actorId:actor.id});};
  }
  function update(state,me){
    const banner=document.getElementById('incursionBanner'),event=state.incursion;
    if(banner){banner.hidden=!event;if(event)banner.textContent=`${event.name} · ${event.kills}/${event.goal} · ${event.objective}`;}
    let effects=document.getElementById('typedCombatStatus');if(!effects){effects=document.createElement('p');effects.id='typedCombatStatus';document.getElementById('effectsValue')?.parentElement?.append(effects);}
    const p=state.privateState?.progression,boost=Date.parse(me.alcoholUntilUtc)>Date.now()?(me.alcoholNutUp||0):0;
    effects.textContent=`Nut Up ${(p?.stats?.nutUp||0)+boost}${boost?` (+${boost} alcohol)` : ''} · Equip through level ${(p?.level||1)+(p?.stats?.nutUp||0)+boost} · ${Date.parse(me.fearedUntilUtc)>Date.now()?'Fleeing in fear · ':''}${effectsText(me.effects)}`;
  }
  function drawActor(ctx,a,p,scale,now){
    if(!['kenHydra','northParkCitizen','alaneeEgg','alaneeCelebrity','pierceHawkeye','hawkeyeChicken'].includes(a.subtype))return false;
    const s=Math.max(10,scale),hood=a.subtype==='kenHydra',egg=a.subtype==='alaneeEgg',chicken=a.subtype==='hawkeyeChicken',boss=a.subtype==='pierceHawkeye';
    ctx.save();ctx.translate(p.x,p.y);ctx.lineWidth=1.5;ctx.strokeStyle='#25232e';
    if(egg){ctx.fillStyle='#e6e3ba';ctx.beginPath();ctx.ellipse(0,-s*.65,s*.4,s*.85,0,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.strokeStyle='#758756';ctx.beginPath();ctx.moveTo(-s*.25,-s*.7);ctx.lineTo(0,-s*.5);ctx.lineTo(s*.2,-s*.9);ctx.stroke();}
    else if(chicken){ctx.fillStyle='#fff3cf';ctx.beginPath();ctx.ellipse(0,-s*.25,s*.3,s*.22,0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.arc(s*.28,-s*.4,s*.13,0,Math.PI*2);ctx.fill();ctx.fillStyle='#ed463c';ctx.fillRect(s*.22,-s*.6,s*.15,s*.1);ctx.fillStyle='#e8ad2f';ctx.fillRect(s*.35,-s*.43,s*.14,s*.07);}
    else{
      ctx.fillStyle=hood?'#f58326':boss?'#849664':a.subtype==='northParkCitizen'?'#c14742':'#466487';
      ctx.beginPath();ctx.ellipse(0,-s*.38,s*.52,s*.45,0,0,Math.PI*2);ctx.fill();ctx.stroke();
      ctx.fillStyle=hood?'#e8781c':'#f1cfab';ctx.beginPath();ctx.arc(0,-s*.96,s*.43,0,Math.PI*2);ctx.fill();ctx.stroke();
      if(hood){ctx.fillStyle='#493325';ctx.beginPath();ctx.ellipse(0,-s*.96,s*.28,s*.32,0,0,Math.PI*2);ctx.fill();}
      ctx.fillStyle='#fff';ctx.beginPath();ctx.ellipse(-s*.12,-s*.98,s*.13,s*.16,0,0,Math.PI*2);ctx.ellipse(s*.12,-s*.98,s*.13,s*.16,0,0,Math.PI*2);ctx.fill();
      ctx.fillStyle='#202020';ctx.beginPath();ctx.arc(-s*.08,-s*.97,s*.035,0,Math.PI*2);ctx.arc(s*.08,-s*.97,s*.035,0,Math.PI*2);ctx.fill();
      if(boss){ctx.fillStyle='#354238';ctx.fillRect(s*.35,-s*.5,s*.75,s*.17);ctx.save();ctx.translate(-s*.55,-s*.55);if(Date.parse(a.pouringUntilUtc)>Date.now())ctx.rotate(-.85);ctx.strokeStyle='#dae7f3';ctx.beginPath();ctx.moveTo(-s*.16,-s*.22);ctx.lineTo(0,0);ctx.lineTo(s*.16,-s*.22);ctx.closePath();ctx.moveTo(0,0);ctx.lineTo(0,s*.2);ctx.moveTo(-s*.12,s*.2);ctx.lineTo(s*.12,s*.2);ctx.stroke();ctx.restore();}
    }
    ctx.fillStyle='#fff5d5';ctx.font='11px sans-serif';ctx.textAlign='center';ctx.fillText(a.name,0,-s*1.65);ctx.restore();return true;
  }
  function drawArea(ctx,event,toScreen,scale){if(!event)return;const p=toScreen(event.center);ctx.save();ctx.strokeStyle=event.type==='northPark'?'#ed872c':'#b9cf66';ctx.setLineDash([8,8]);ctx.lineWidth=2;ctx.beginPath();ctx.arc(p.x,p.y,event.radius*scale,0,Math.PI*2);ctx.stroke();ctx.restore();}
  const api={resistanceText,gearText,effectsText,inspect,viewButton,update,drawActor,drawArea};
  if(typeof module==='object'&&module.exports)module.exports=api;else root.CombatPresentation=api;
})(typeof globalThis==='object'?globalThis:this);
