(function(root){
  'use strict';
  const name='Jump Now, Regret Later!';
  function enemyX(x,scroll,length){return ((x-scroll+3)%length+length)%length-3;}
  function view(run,elapsed,completed=false){
    const dt=completed?0:Math.max(0,Math.min(.1,elapsed));
    return {scroll:(run.scroll+run.scrollSpeed*dt)%run.trackLength,height:Math.max(0,run.jumpHeight+run.jumpVelocity*dt-10*dt*dt)};
  }
  function draw(ctx,dungeon,width,height,elapsed,now){
    const run=dungeon.retroBattle,scene=view(run,elapsed,dungeon.isCompleted),unit=Math.min(width/32,height/12),ground=height*.76;
    ctx.save();ctx.imageSmoothingEnabled=false;
    ctx.fillStyle='#100f22';ctx.fillRect(0,0,width,height);
    const glow=ctx.createLinearGradient(0,0,0,ground);glow.addColorStop(0,'#17162d');glow.addColorStop(1,'#39304b');ctx.fillStyle=glow;ctx.fillRect(0,0,width,ground);
    for(let layer=0;layer<2;layer++){
      const spacing=(layer?6:10)*unit,offset=scene.scroll*unit*(layer?.6:.25)%spacing;
      for(let x=-spacing-offset;x<width;x+=spacing){
        ctx.fillStyle=layer?'#44394f':'#26243b';ctx.fillRect(x,ground-(layer?7:9)*unit,unit*.65,(layer?7:9)*unit);
        ctx.fillRect(x-unit*.2,ground-7*unit,unit*1.05,unit*.4);
        if(layer){ctx.fillStyle='#151628';ctx.fillRect(x+unit*1.4,ground-unit*5,unit*2,unit*3.7);ctx.fillStyle='#ed944e';ctx.fillRect(x+unit*.9,ground-unit*3,unit*.18,unit*.65);ctx.fillStyle='#ffe38a';ctx.fillRect(x+unit*.8,ground-unit*3.4,unit*.35,unit*.5);}
      }
    }
    ctx.fillStyle='#8b7795';ctx.fillRect(0,ground,width,unit*.25);ctx.fillStyle='#312a40';ctx.fillRect(0,ground+unit*.25,width,height-ground);
    const tileOffset=scene.scroll*unit%(2*unit);ctx.strokeStyle='#554762';ctx.lineWidth=2;
    for(let x=-tileOffset;x<width;x+=2*unit){ctx.strokeRect(x,ground+unit*.3,2*unit,unit);ctx.strokeRect(x+unit,ground+unit*1.3,2*unit,unit);}
    for(const enemy of run.enemies){
      if(enemy.defeated)continue;const x=enemyX(enemy.x,scene.scroll,run.trackLength)*unit;if(x<-unit||x>width+unit)continue;
      const u=unit/10*(enemy.isBoss?1.7:1),bob=Math.sin(now*.007+enemy.id)*u;
      if(enemy.isBoss){ctx.fillStyle="#fff1bf";ctx.font="bold 13px monospace";ctx.textAlign="center";ctx.fillText(`BOSS · ${enemy.hearts} stomps`,x,ground-14*u);}
      ctx.fillStyle='#141020';ctx.fillRect(x-5*u,ground-u,10*u,u);
      ctx.fillStyle=enemy.id%2?'#f07970':'#ab86f4';ctx.fillRect(x-5*u,ground-8*u+bob,10*u,6*u);ctx.fillRect(x-3*u,ground-10*u+bob,6*u,2*u);
      ctx.fillStyle='#fff1c6';ctx.fillRect(x-3*u,ground-7*u+bob,2*u,2*u);ctx.fillRect(x+u,ground-7*u+bob,2*u,2*u);
      ctx.fillStyle='#21172f';ctx.fillRect(x-3*u,ground-6*u+bob,u,u);ctx.fillRect(x+u,ground-6*u+bob,u,u);
      ctx.fillStyle='#78528b';ctx.fillRect(x-5*u,ground-2*u,3*u,2*u);ctx.fillRect(x+2*u,ground-2*u,3*u,2*u);
    }
    const x=6*unit,y=ground-scene.height*unit,u=unit/10;
    ctx.fillStyle='#171327';ctx.fillRect(x-5*u,ground,10*u,u);
    ctx.fillStyle='#f4c572';ctx.fillRect(x-3*u,y-17*u,7*u,5*u);ctx.fillStyle='#f05f75';ctx.fillRect(x-4*u,y-19*u,8*u,3*u);ctx.fillRect(x+3*u,y-17*u,3*u,u);
    ctx.fillStyle='#20203c';ctx.fillRect(x+2*u,y-16*u,u,u);ctx.fillStyle='#65cde0';ctx.fillRect(x-4*u,y-12*u,8*u,7*u);ctx.fillStyle='#f4c572';ctx.fillRect(x+4*u,y-11*u,2*u,5*u);
    ctx.fillStyle='#595ea5';ctx.fillRect(x-3*u,y-5*u,3*u,4*u);ctx.fillRect(x+u,y-5*u,3*u,4*u);ctx.fillStyle='#fff1c6';ctx.fillRect(x-4*u,y-u,4*u,u);ctx.fillRect(x+u,y-u,5*u,u);
    if(dungeon.isCompleted){ctx.fillStyle='#d29b40';ctx.fillRect(9*unit,ground-unit*1.4,unit*1.7,unit*1.4);ctx.fillStyle='#ffe296';ctx.fillRect(9*unit,ground-unit*.8,unit*1.7,unit*.2);}
    ctx.textAlign='center';ctx.fillStyle='#fff1bf';ctx.font=`bold ${Math.max(16,Math.min(30,width/28))}px monospace`;ctx.fillText(name,width/2,Math.max(100,height*.19));
    const killed=run.enemies.filter(e=>e.defeated).length;ctx.font='bold 15px monospace';ctx.fillStyle='#b5edf0';ctx.fillText(`LEVEL ${dungeon.difficulty}   ·   ${killed} / ${run.enemies.length} DEFEATED   ·   ${run.scrollSpeed.toFixed(1)} m/s`,width/2,Math.max(126,height*.19+30));
    ctx.font='14px monospace';ctx.fillStyle='#eee2f3';ctx.fillText(dungeon.isCompleted?(dungeon.eventBattle?.dungeonNumber===1?'SMALL BOSS CLEAR! Enter the second dungeon from the event panel.':'ALL CLEAR! Your private event reward is ready.'):'Click / tap to jump. Land on heads. Missed enemies loop back.',width/2,height*.91);
    ctx.restore();
  }
  const api={name,enemyX,view,draw};if(typeof module==='object'&&module.exports)module.exports=api;else root.RetroBattles=api;
})(typeof globalThis!=='undefined'?globalThis:this);
