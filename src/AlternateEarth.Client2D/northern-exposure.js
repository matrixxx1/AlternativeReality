(function(root){
 'use strict';
 function drawCanadian(ctx,a,now,api){
  if(drawWildlife(ctx,a,now,api))return true;
  if(!['canadian','canadianBoss'].includes(a.subtype))return false;
  const p=api.toScreen(a.position),boss=a.subtype==='canadianBoss',s=Math.max(9,api.state.scale*(boss?2.5:.75));
  const fart=Date.parse(a.fartUntilUtc||'')>Date.now(),legUp=fart&&a.fartPose==='legUp';
  ctx.save();ctx.translate(p.x,p.y);ctx.lineWidth=Math.max(1,s*.07);ctx.strokeStyle='#222b31';
  ctx.fillStyle='#182532';ctx.beginPath();ctx.ellipse(0,1,s*.65,s*.15,0,0,Math.PI*2);ctx.fill();
  // The feet stay planted while the torso hinges at the hips.
  ctx.strokeStyle=boss?'#838d97':'#263f60';ctx.lineWidth=s*.22;ctx.beginPath();ctx.moveTo(-s*.2,-s*.65);ctx.lineTo(-s*.3,0);ctx.moveTo(s*.2,-s*.65);ctx.lineTo(s*(legUp?.8:.3),legUp?-s*.75:0);ctx.stroke();
  ctx.save();ctx.translate(0,-s*.65);ctx.rotate(fart?(legUp?-.85:Math.PI/2):Math.sin(now/180)*.025);
  ctx.fillStyle=boss?'#a3acb5':'#c63238';ctx.fillRect(-s*.45,-s*.95,s*.9,s*.95);ctx.strokeStyle='#293340';ctx.lineWidth=s*.045;ctx.strokeRect(-s*.45,-s*.95,s*.9,s*.95);
  if(boss){ctx.fillStyle='#f57c33';ctx.fillRect(-s*.25,-s*.75,s*.5,s*.27);ctx.fillStyle='#fce388';ctx.fillRect(-s*.12,-s*.7,s*.08,s*.12);ctx.fillRect(s*.05,-s*.7,s*.08,s*.12);ctx.fillStyle='#728492';ctx.fillRect(-s*.66,-s*.88,s*.2,s*.6);ctx.fillRect(s*.46,-s*.88,s*.2,s*.6);}
  else {ctx.strokeStyle='#481f2a';ctx.lineWidth=s*.05;for(let n=0;n<3;n++){ctx.beginPath();ctx.moveTo(-s*.43,-s*.2-n*s*.27);ctx.lineTo(s*.43,-s*.2-n*s*.27);ctx.stroke();}ctx.beginPath();ctx.moveTo(-s*.2,-s*.95);ctx.lineTo(-s*.2,0);ctx.moveTo(s*.2,-s*.95);ctx.lineTo(s*.2,0);ctx.stroke();}
  // Two separate pieces make the floppy split head readable during speech.
  const flap=Math.sin(now/95)*s*(fart?.13:.045);ctx.fillStyle='#efc49b';ctx.fillRect(-s*.42,-s*1.18,s*.84,s*.22);
  ctx.save();ctx.translate(0,-s*1.2-Math.abs(flap));ctx.rotate(Math.sin(now/130)*.1);ctx.fillRect(-s*.42,-s*.48,s*.84,s*.47);ctx.fillStyle='#20232b';ctx.fillRect(-s*.21,-s*.28,s*.07,s*.08);ctx.fillRect(s*.15,-s*.28,s*.07,s*.08);ctx.fillStyle=boss?'#aab2bf':'#a92831';ctx.fillRect(-s*.46,-s*.58,s*.92,s*.15);ctx.restore();
  if(!boss&&a.equippedWeapon==='hockeyStick'){ctx.strokeStyle='#caa06b';ctx.lineWidth=s*.09;ctx.beginPath();ctx.moveTo(s*.45,-s*.75);ctx.lineTo(s*.8,s*.5);ctx.lineTo(s*1.15,s*.5);ctx.stroke();}
  if(!boss&&a.equippedWeapon==='iceSkate'){ctx.fillStyle='#f6f0df';ctx.fillRect(s*.42,-s*.5,s*.4,s*.25);ctx.strokeStyle='#bddef5';ctx.lineWidth=s*.045;ctx.beginPath();ctx.moveTo(s*.38,-s*.15);ctx.lineTo(s*.92,-s*.15);ctx.stroke();}
  ctx.restore();
  if(fart){ctx.fillStyle='rgba(118,194,43,.45)';for(let n=0;n<4;n++){ctx.beginPath();ctx.arc(-s*(.4+n*.3),-s*.6+Math.sin(now/140+n)*s*.15,s*(.18+n*.05),0,Math.PI*2);ctx.fill();}}
  ctx.textAlign='center';ctx.font=`bold ${boss?13:10}px monospace`;ctx.strokeStyle='#14201d';ctx.lineWidth=3;ctx.strokeText(a.name,0,-s*2.5);ctx.fillStyle='#fff1bd';ctx.fillText(a.name,0,-s*2.5);
  ctx.fillStyle='#392d30';ctx.fillRect(-s*.55,-s*2.3,s*1.1,s*.08);ctx.fillStyle='#d54c51';ctx.fillRect(-s*.55,-s*2.3,s*1.1*Math.max(0,a.healthHearts/a.maximumHealthHearts),s*.08);ctx.restore();return true;
 }
 function drawWildlife(ctx,a,now,api){
  if(!['angryMoose','helmetBeaver','tacticalGoose'].includes(a.subtype))return false;
  const moose=a.subtype==='angryMoose',beaver=a.subtype==='helmetBeaver',p=api.toScreen(a.position),s=Math.max(10,api.state.scale*(moose?1.6:beaver?.75:.8)),stride=a.isMoving?Math.sin(now/65)*s*.18:0;
  ctx.save();ctx.translate(p.x,p.y);ctx.save();if(a.facing==='west')ctx.scale(-1,1);
  ctx.fillStyle='rgba(15,24,20,.35)';ctx.beginPath();ctx.ellipse(0,0,s*.95,s*.2,0,0,Math.PI*2);ctx.fill();
  if(moose){
   ctx.strokeStyle='#463020';ctx.lineWidth=s*.14;for(const x of [-.55,-.25,.35,.65]){ctx.beginPath();ctx.moveTo(s*x,-s*.6);ctx.lineTo(s*x+stride*(x<0?1:-1),0);ctx.stroke();}
   ctx.fillStyle='#70503a';ctx.beginPath();ctx.ellipse(-s*.1,-s*.85,s*.8,s*.48,0,0,Math.PI*2);ctx.fill();ctx.fillRect(s*.4,-s*1.55,s*.35,s*.8);ctx.beginPath();ctx.ellipse(s*.7,-s*1.5,s*.45,s*.27,.2,0,Math.PI*2);ctx.fill();
   ctx.strokeStyle='#d0b389';ctx.lineWidth=s*.07;for(const sign of [-1,1]){ctx.beginPath();ctx.moveTo(s*.6,-s*1.7);ctx.lineTo(s*(.6+sign*.5),-s*2);ctx.lineTo(s*(.6+sign*.75),-s*2.1);for(let i=1;i<=3;i++){ctx.moveTo(s*(.6+sign*i*.2),-s*(1.77+i*.1));ctx.lineTo(s*(.6+sign*i*.22),-s*(2.05+i*.12));}ctx.stroke();}
   ctx.fillStyle='#ed3b30';ctx.fillRect(s*.65,-s*1.65,s*.11,s*.08);
  }else if(beaver){
   ctx.fillStyle='#533d2e';ctx.beginPath();ctx.ellipse(-s*.85,-s*.2,s*.5,s*.22,-.3,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#91765a';ctx.lineWidth=1;for(let n=0;n<4;n++){ctx.beginPath();ctx.moveTo(-s*(.65+n*.13),-s*.4);ctx.lineTo(-s*(.8+n*.13),-s*.04);ctx.stroke();}
   ctx.fillStyle='#966943';ctx.beginPath();ctx.ellipse(0,-s*.5,s*.65,s*.5,0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.arc(s*.48,-s*.9,s*.4,0,Math.PI*2);ctx.fill();ctx.fillStyle='#6f5736';ctx.beginPath();ctx.ellipse(s*.45,-s*1.17,s*.46,s*.3,0,Math.PI,Math.PI*2);ctx.fill();ctx.fillStyle='#a7ac75';ctx.fillRect(0,-s*1.16,s*.95,s*.1);ctx.fillStyle='#ffebc2';ctx.fillRect(s*.63,-s*.64,s*.1,s*.24);ctx.fillRect(s*.76,-s*.64,s*.1,s*.24);ctx.fillStyle='#231c17';ctx.fillRect(s*.65,-s*.97,s*.08,s*.08);ctx.fillRect(-s*.2+stride,-s*.08,s*.2,s*.13);ctx.fillRect(s*.25-stride,-s*.08,s*.2,s*.13);
  }else{
   ctx.strokeStyle='#e7a336';ctx.lineWidth=s*.08;for(const x of [-.25,.25]){ctx.beginPath();ctx.moveTo(s*x,-s*.4);ctx.lineTo(s*x+stride*(x<0?1:-1),0);ctx.lineTo(s*x+s*.2+stride*(x<0?1:-1),0);ctx.stroke();}
   ctx.fillStyle='#eeeddf';ctx.beginPath();ctx.ellipse(-s*.1,-s*.65,s*.65,s*.4,0,0,Math.PI*2);ctx.fill();ctx.fillRect(s*.2,-s*1.4,s*.23,s*.9);ctx.beginPath();ctx.arc(s*.42,-s*1.42,s*.28,0,Math.PI*2);ctx.fill();ctx.fillStyle='#455940';ctx.fillRect(-s*.5,-s*.95,s*.7,s*.56);ctx.fillStyle='#8b9567';for(const x of [-.4,-.1])ctx.fillRect(s*x,-s*.82,s*.18,s*.22);ctx.fillStyle='#526549';ctx.beginPath();ctx.ellipse(s*.42,-s*1.57,s*.34,s*.22,0,Math.PI,Math.PI*2);ctx.fill();ctx.fillRect(s*.08,-s*1.58,s*.7,s*.09);ctx.fillStyle='#e8a636';ctx.fillRect(s*.61,-s*1.4,s*.3,s*.12);ctx.fillStyle='#e84332';ctx.fillRect(s*.48,-s*1.49,s*.08,s*.07);
  }
  ctx.restore();const top=moose?-s*2.65:-s*2;ctx.textAlign='center';ctx.font='bold 10px monospace';ctx.strokeStyle='#18211b';ctx.lineWidth=3;ctx.strokeText(a.name,0,top);ctx.fillStyle='#ffe5ad';ctx.fillText(a.name,0,top);ctx.fillStyle='#3d2e2b';ctx.fillRect(-s*.55,top+5,s*1.1,3);ctx.fillStyle='#f75e49';ctx.fillRect(-s*.55,top+5,s*1.1*Math.max(0,a.healthHearts/a.maximumHealthHearts),3);ctx.restore();return true;
 }
 function drawSyrup(ctx,loot,toScreen,state){
  if(!['mapleSyrupPuddle','mooseFluSyrup'].includes(loot.dropKind))return false;
  const p=toScreen(loot.position),r=state.scale*2.5;
  ctx.save();ctx.fillStyle='rgba(185,104,19,.7)';ctx.strokeStyle='#e5ad47';ctx.lineWidth=2;
  ctx.beginPath();ctx.ellipse(p.x,p.y,r,r*state.pitch,0,0,Math.PI*2);ctx.fill();ctx.stroke();if(loot.dropKind==='mooseFluSyrup'){ctx.fillStyle='#784622';ctx.beginPath();ctx.ellipse(p.x,p.y-r*.15,r*.65,r*.4,0,0,Math.PI*2);ctx.fill();}
  ctx.font='bold 11px monospace';ctx.textAlign='center';ctx.fillStyle='#ffe8a2';ctx.fillText(loot.dropKind==='mooseFluSyrup'?'Maple syrup · ½ speed · empty jar/bottle required':'Maple syrup · ½ speed · collect',p.x,p.y-r*state.pitch-5);ctx.restore();return true;
 }
 function drawTruck(ctx,e,toScreen,state){
  if(e.properties?.subtype!=='haneyPickup')return false;
  const p=toScreen(e.position),l=4.8*state.scale,w=1.9*state.scale*state.pitch,a=Number(e.properties.rotationDegrees||0)*Math.PI/180;
  ctx.save();ctx.translate(p.x,p.y);ctx.rotate(Math.atan2(-Math.sin(a)*state.pitch,Math.cos(a)+Math.sin(a)*state.shear));
  ctx.fillStyle='#24251e';for(const x of [-.32,.32])for(const y of [-.5,.5])ctx.fillRect(l*x-l*.07,w*y-w*.1,l*.14,w*.2);
  ctx.fillStyle='#89825a';ctx.fillRect(-l*.5,-w*.5,l,w);ctx.strokeStyle='#433f30';ctx.lineWidth=2;ctx.strokeRect(-l*.5,-w*.5,l,w);
  ctx.fillStyle='#4c4734';ctx.fillRect(-l*.45,-w*.4,l*.44,w*.8);ctx.strokeStyle='#a48a63';ctx.lineWidth=1;for(let n=0;n<4;n++){ctx.beginPath();ctx.moveTo(-l*.44,-w*.3+n*w*.2);ctx.lineTo(-l*.03,-w*.3+n*w*.2);ctx.stroke();}
  ctx.fillStyle='#aab6a6';ctx.fillRect(l*.04,-w*.39,l*.22,w*.78);ctx.strokeStyle='#596353';ctx.beginPath();ctx.moveTo(l*.08,-w*.32);ctx.lineTo(l*.17,w*.12);ctx.lineTo(l*.12,w*.3);ctx.stroke();
  ctx.fillStyle='#9f512e';for(const [x,y] of [[-.44,-.38],[-.1,.38],[.35,.25],[.4,-.3]])ctx.fillRect(l*x,w*y,l*.09,w*.15);
  ctx.fillStyle='#dad19b';ctx.fillRect(l*.46,-w*.43,l*.04,w*.18);ctx.fillRect(l*.46,w*.25,l*.04,w*.18);ctx.fillStyle='#999687';ctx.fillRect(l*.49,-w*.52,l*.035,w*1.04);ctx.restore();return true;
 }
 root.NorthernExposure={drawCanadian,drawWildlife,drawTruck,drawSyrup};
})(typeof window!=='undefined'?window:globalThis);
