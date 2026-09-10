(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.Survival=api;})(globalThis,function(){
  const glyphs={apple:'🍎',pear:'🍐',peach:'🍑',orange:'🍊',banana:'🍌',blackberry:'🫐',raspberry:'🍓',blueberry:'🫐',strawberry:'🍓',cranberry:'🔴',watermelon:'🍉',carrot:'🥕',potato:'🥔',corn:'🌽',beans:'🫘',spinach:'🥬',lettuce:'🥬',garlic:'🧄',onion:'🧅',peppers:'🫑',tomato:'🍅',pumpkin:'🎃',cheeseburger:'🍔',cheese:'🧀',milk:'🥛',egg:'🥚',antibiotics:'💊',ring:'💍',chicken:'🐔',pig:'🐖',cow:'🐄',goat:'🐐',sheep:'🐑',crab:'🦀',clam:'🐚',oyster:'🦪',crayfish:'🦞',lobster:'🦞',shrimp:'🦐'};
  const wildlife=['chicken','pig','cow','goat','sheep','crab','clam','oyster','crayfish','lobster','shrimp'];
  function glyph(type){return glyphs[type]||(type.startsWith('raw')?'🥩':type.startsWith('cooked')?'🍖':'🍲');}
  function wild(e){return ['wildCrop','fruitTree','sandLump'].includes(e.properties?.subtype);}
  function drawNode(ctx,e,p,scale){
    if(!wild(e))return false;const s=Math.max(9,Math.min(30,scale*.75));ctx.save();ctx.translate(p.x,p.y);
    if(e.properties.subtype==='sandLump'){ctx.fillStyle='#a98847';ctx.beginPath();ctx.ellipse(0,-s*.2,s,s*.4,0,Math.PI,Math.PI*2);ctx.fill();ctx.strokeStyle='#f4db91';ctx.lineWidth=2;ctx.stroke();}
    else{if(e.properties.subtype==='fruitTree'){ctx.fillStyle='#725136';ctx.fillRect(-s*.13,-s*1.8,s*.26,s*1.8);ctx.fillStyle='#397d39';ctx.beginPath();ctx.arc(0,-s*2,s,0,Math.PI*2);ctx.fill();}else{ctx.fillStyle='#397537';ctx.beginPath();ctx.ellipse(0,-s*.2,s*.8,s*.3,-.3,0,Math.PI*2);ctx.fill();}
      ctx.font=`${s*1.2}px serif`;ctx.textAlign='center';ctx.fillText(glyph(e.properties.itemType),0,e.properties.subtype==='fruitTree'?-s*1.6:-s*.3);}
    if(scale>=8){ctx.fillStyle='#fff0bd';ctx.strokeStyle='#253123';ctx.lineWidth=3;ctx.font='11px sans-serif';ctx.textAlign='center';const label=e.properties.subtype==='sandLump'?'Search sand':e.properties.itemType;const labelY=e.properties.subtype==='sandLump'?-16:14;ctx.strokeText(label,0,labelY);ctx.fillText(label,0,labelY);}ctx.restore();return true;
  }
  function drawAnimal(ctx,a,p,scale){if(!wildlife.includes(a.subtype))return false;ctx.save();ctx.textAlign='center';ctx.font=`${Math.max(20,Math.min(48,scale*1.6))}px serif`;ctx.fillText(glyph(a.subtype),p.x,p.y);ctx.font='11px sans-serif';ctx.fillStyle='#fff0bd';ctx.strokeStyle='#26372b';ctx.lineWidth=3;ctx.strokeText(a.name,p.x,p.y+14);ctx.fillText(a.name,p.x,p.y+14);ctx.restore();return true;}
  function status(player,now=Date.now()){
    const s=player.survival||{},hunger=Math.max(0,Math.min(100,s.hunger||0));
    const illnesses=(s.illnesses||[]).filter(i=>Date.parse(i.endsAtUtc)>now).map(i=>`${i.name} · ${Math.ceil((Date.parse(i.endsAtUtc)-now)/60000)} min`);
    const buffs=(s.buffs||[]).filter(b=>Date.parse(b.endsAtUtc)>now).map(b=>`+${b.amount} ${b.stat} (${Math.ceil((Date.parse(b.endsAtUtc)-now)/60000)} min)`);
    return {hunger,text:`${hunger.toFixed(1)} / 100${hunger>=100?' · STARVING':''}`,illnesses:illnesses.join(', ')||'Healthy',buffs:buffs.join(' · ')};
  }
  function telemetry(player){const s=status(player),h=document.getElementById('hungerValue'),ill=document.getElementById('illnessValue');if(h){h.textContent=`${s.hunger.toFixed(1)} / 100`;h.title=s.text;h.style.color=s.hunger>=80?'#ff9773':'';}if(ill){ill.textContent=String((player.survival?.illnesses||[]).filter(i=>Date.parse(i.endsAtUtc)>Date.now()).length);ill.title=s.illnesses==='Healthy'?'No diseases':s.illnesses+'. Antibiotics cure illness.';}return s;}
  function clickWild(target,state,renderList,send,navigate,toast){
    if(state.dungeon)return false;const me=state.players.get(state.playerId);if(!me||me.abduction)return false;
    const e=renderList('resourceNode').filter(wild).find(e=>Math.hypot(e.position.x-target.x,e.position.y-target.y)<Math.max(1.2,18/state.scale));
    if(!e)return false;state.followCommand=null;
    if(Math.hypot(e.position.x-me.position.x,e.position.y-me.position.y)<3.8)send({type:'gatherWild',entityId:e.id});
    else{navigate(e.position);state.pendingWild=e;toast(e.properties.subtype==='sandLump'?'Moving closer to search the sand.':'Moving closer to gather food.');}return true;
  }
  function advanceGather(state,me,send,stop){if(state.pendingWild&&Math.hypot(state.pendingWild.position.x-me.position.x,state.pendingWild.position.y-me.position.y)<3.8){const id=state.pendingWild.id;stop();state.pendingWild=null;send({type:'gatherWild',entityId:id});}}
  return {glyph,wild,drawNode,drawAnimal,status,telemetry,clickWild,advanceGather};
});
