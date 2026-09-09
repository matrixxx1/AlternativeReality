(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.Scuba=api;})(globalThis,function(){
  'use strict';
  const melee=['fist','knife','sword','hockeyStick','iceSkate','zombieBite'];
  const canAttack=weapon=>melee.includes(weapon)||weapon==='spearGun';
  const surfaceY=height=>Math.min(220,height*.32);
  const depthScale=height=>(height-75-surfaceY(height))/19;
  function project(point,camera,scale,width,height){return{x:width/2+(point.x-camera.x)*scale,y:surfaceY(height)+(19.5-point.y)*depthScale(height)};}
  function unproject(point,camera,scale,width,height){return{x:camera.x+(point.x-width/2)/scale,y:19.5-(point.y-surfaceY(height))/depthScale(height)};}
  function gear(ctx,x,y,size){
    ctx.save();ctx.translate(x,y);ctx.scale(size/32,size/32);ctx.lineWidth=1.5;ctx.strokeStyle='#152f40';
    ctx.fillStyle='#e5bd49';ctx.beginPath();ctx.roundRect(-10,-12,12,25,5);ctx.fill();ctx.stroke();
    ctx.fillStyle='#5b7481';ctx.fillRect(-7,-16,6,5);ctx.fillStyle='#263e51';ctx.fillRect(-10,-5,12,3);ctx.fillRect(-10,6,12,3);
    ctx.strokeStyle='#294654';ctx.beginPath();ctx.moveTo(-2,-13);ctx.bezierCurveTo(15,-18,20,4,7,4);ctx.stroke();
    ctx.fillStyle='#70e5ec';ctx.strokeStyle='#ecf9f8';ctx.beginPath();ctx.roundRect(1,-3,17,10,3);ctx.fill();ctx.stroke();
    ctx.strokeStyle='#244451';ctx.beginPath();ctx.moveTo(9,-2);ctx.lineTo(9,6);ctx.stroke();ctx.restore();
  }
  function diver(ctx,p,scale,facing,now,weapon){
    const s=Math.max(13,scale*.6);ctx.save();ctx.translate(p.x,p.y);ctx.scale(facing==='west'?-1:1,1);
    const kick=Math.sin(now/180)*s*.14;
    ctx.strokeStyle='#122c41';ctx.lineWidth=s*.44;ctx.lineCap='round';ctx.beginPath();ctx.moveTo(-s*.8,0);ctx.lineTo(s*.6,0);ctx.stroke();
    ctx.lineWidth=s*.22;ctx.beginPath();ctx.moveTo(-s*.65,0);ctx.lineTo(-s*1.5,s*.2+kick);ctx.lineTo(-s*2,s*.1+kick);ctx.moveTo(-s*.65,0);ctx.lineTo(-s*1.5,-s*.2-kick);ctx.lineTo(-s*2,-s*.1-kick);ctx.stroke();
    ctx.strokeStyle='#edcb4c';ctx.lineWidth=s*.3;ctx.beginPath();ctx.moveTo(-s*2,s*.1+kick);ctx.lineTo(-s*2.5,s*.3+kick);ctx.moveTo(-s*2,-s*.1-kick);ctx.lineTo(-s*2.5,-s*.3-kick);ctx.stroke();
    ctx.fillStyle='#ffd1a1';ctx.beginPath();ctx.arc(s*.95,0,s*.34,0,Math.PI*2);ctx.fill();
    ctx.fillStyle='#59d9ef';ctx.strokeStyle='#e4fcff';ctx.lineWidth=2;ctx.fillRect(s*.98,-s*.23,s*.38,s*.25);ctx.strokeRect(s*.98,-s*.23,s*.38,s*.25);
    ctx.fillStyle='#e5bd49';ctx.beginPath();ctx.roundRect(-s*.8,-s*.65,s*1.15,s*.4,s*.17);ctx.fill();
    ctx.strokeStyle='#5fa9b8';ctx.lineWidth=s*.1;ctx.beginPath();ctx.moveTo(s*.3,-s*.48);ctx.quadraticCurveTo(s*1.7,-s*.8,s*1.27,s*.1);ctx.stroke();
    ctx.strokeStyle='#193c53';ctx.lineWidth=s*.16;ctx.beginPath();ctx.moveTo(s*.3,s*.12);ctx.lineTo(s*.65,s*.55);ctx.lineTo(s*1.3,s*.4);ctx.stroke();
    if(weapon==='spearGun'){ctx.strokeStyle='#d4e7e7';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(s*.9,s*.42);ctx.lineTo(s*2.8,s*.42);ctx.stroke();ctx.strokeStyle='#b28f50';ctx.lineWidth=5;ctx.beginPath();ctx.moveTo(s,s*.55);ctx.lineTo(s*2,s*.55);ctx.stroke();}
    else if(melee.includes(weapon)&&weapon!=='fist'){ctx.strokeStyle='#f2f9fc';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(s*1.3,s*.4);ctx.lineTo(s*(weapon==='knife'?1.8:2.5),s*.25);ctx.stroke();}
    ctx.strokeStyle='rgba(185,245,255,.7)';ctx.lineWidth=1;for(let i=0;i<4;i++){const age=(now/1000+i*.3)%1.5;ctx.beginPath();ctx.arc(s*1.4+age*s*.15,-age*s*1.8,2+age*2,0,Math.PI*2);ctx.stroke();}
    ctx.restore();
  }
  function creature(ctx,actor,p,scale,now){
    if(typeof Survival!=='undefined'&&Survival.drawAnimal(ctx,actor,p,scale))return;
    const type=actor.subtype,large=type.startsWith('large'),octopus=type.toLowerCase().includes('octopus'),eel=type==='morayEel',fish=type==='fish';
    const s=Math.max(fish?7:14,scale*(large?1.1:fish?.3:.6));ctx.save();ctx.translate(p.x,p.y);ctx.scale(actor.facing==='west'?-1:1,1);
    ctx.fillStyle=octopus?'#b878bc':eel?'#87a25d':fish?'#f1c664':type==='barracuda'?'#89cfd5':'#8ea9bf';
    ctx.strokeStyle=ctx.fillStyle;ctx.lineWidth=s*.19;
    if(octopus){for(let i=0;i<8;i++){ctx.beginPath();ctx.moveTo((i-3.5)*s*.13,0);ctx.quadraticCurveTo((i-3.5)*s*.45,s*(.6+Math.sin(now/260+i)*.2),(i-3.5)*s*.35,s*1.3);ctx.stroke();}ctx.beginPath();ctx.ellipse(0,-s*.3,s*.65,s*.8,0,0,Math.PI*2);ctx.fill();}
    else{ctx.beginPath();ctx.ellipse(0,0,s*(eel?1.7:1.2),s*(eel?.22:.45),0,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.moveTo(-s,0);ctx.lineTo(-s*1.7,-s*.6);ctx.lineTo(-s*1.7,s*.6);ctx.closePath();ctx.fill();if(!fish&&!eel){ctx.beginPath();ctx.moveTo(-s*.4,-s*.2);ctx.lineTo(-s*.2,-s*.95);ctx.lineTo(s*.5,-s*.25);ctx.fill();}}
    ctx.fillStyle='#152631';ctx.beginPath();ctx.arc(s*(octopus?.25:.75),-s*.1,Math.max(2,s*.07),0,Math.PI*2);ctx.fill();ctx.restore();
    if(!fish){ctx.fillStyle='#ecf7ff';ctx.font='11px monospace';ctx.textAlign='center';ctx.fillText(actor.name,p.x,p.y-s*1.35);ctx.fillStyle='#233545';ctx.fillRect(p.x-25,p.y-s*1.15,50,4);ctx.fillStyle=large?'#ffb651':'#e3899b';ctx.fillRect(p.x-25,p.y-s*1.15,50*Math.max(0,actor.healthHearts/actor.maximumHealthHearts),4);}
  }
  function draw(ctx,dungeon,player,width,height,project,scale,now,facing){
    const surface=surfaceY(height);
    const sea=ctx.createLinearGradient(0,surface,0,height);sea.addColorStop(0,'#267b94');sea.addColorStop(.4,'#124d6b');sea.addColorStop(1,'#071b35');ctx.fillStyle=sea;ctx.fillRect(0,0,width,height);
    ctx.save();ctx.globalAlpha=.05;ctx.fillStyle='#d5fbef';for(let i=0;i<8;i++){const x=(i*233+Math.sin(now/5000)*30)%width;ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x+150,height);ctx.lineTo(x+240,height);ctx.lineTo(x+25,0);ctx.fill();}ctx.restore();
    ctx.fillStyle='#9ccad4';ctx.fillRect(0,0,width,surface);ctx.strokeStyle='#d0f9f3';ctx.lineWidth=3;ctx.beginPath();for(let x=0;x<=width;x+=8){const y=surface+Math.sin(x/32+now/650)*2;x?ctx.lineTo(x,y):ctx.moveTo(x,y);}ctx.stroke();
    const bed=project({x:0,y:.5}).y;ctx.fillStyle='#314a4c';ctx.fillRect(0,bed,width,height-bed);
    for(let x=0;x<dungeon.width;x+=3){const p=project({x,y:.5});if(p.x < -40 || p.x > width+40)continue;ctx.strokeStyle=x%2?'#398a78':'#286b67';ctx.lineWidth=3;for(let i=0;i<3;i++){ctx.beginPath();ctx.moveTo(p.x+i*5,bed);ctx.quadraticCurveTo(p.x+Math.sin(now/1300+x+i)*14,bed-25,p.x+i*3+Math.sin(now/1000+x)*8,bed-40-(x%7)*4);ctx.stroke();}}
    for(const actor of dungeon.actors||[]){const p=project(actor.position);if(p.x>-100&&p.x<width+100)creature(ctx,actor,p,scale,now);}
    diver(ctx,project(player.position),scale,facing,now,player.equippedWeapon);
    ctx.textAlign='center';ctx.fillStyle='#163c52';ctx.font='bold 15px monospace';ctx.fillText(`${dungeon.underwater.name} · DIFFICULTY ${dungeon.difficulty}`,width/2,115);
    ctx.font='12px monospace';ctx.fillStyle='#23475a';ctx.fillText('← WEST     Swim: WASD / arrows or click     EAST →',width/2,137);
    ctx.fillText('Melee + spear gun · Defeat guardians to open treasure · Swim up or select Surface',width/2,height-36);
    const air=Math.max(0,player.air/player.maximumAir);ctx.fillStyle='#08263d';ctx.fillRect(width/2-100,151,200,9);ctx.fillStyle=air<.2?'#ff8a73':'#77e4d6';ctx.fillRect(width/2-100,151,200*air,9);
    ctx.fillStyle='#163c52';ctx.fillText(`AIR ${Math.ceil(air*100)}%`,width/2,181);
    for(const [x,shore,label]of[[.5,dungeon.underwater.westShore,'WEST SHORE'],[dungeon.width-.5,dungeon.underwater.eastShore,'EAST SHORE']]){const p=project({x,y:10});if(shore&&p.x>40&&p.x<width-40){ctx.fillStyle='#e8d1a0';ctx.fillRect(p.x-4,surface,8,Math.max(0,bed-surface));ctx.fillText(label,p.x,surface-10);}}
  }
  return{canAttack,project,unproject,gear,diver,creature,draw};
});
