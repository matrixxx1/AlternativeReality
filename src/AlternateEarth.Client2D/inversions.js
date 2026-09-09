(function(root){
 'use strict';
 let api,signature='',battleSignature='',questSignature='';const seenRewards=new Set();
 const make=(tag,text)=>{const e=document.createElement(tag);if(text!==undefined)e.textContent=text;return e;};
 function init(context){api=context;const panel=make('section');panel.id='adventurePanel';panel.className='panel';document.getElementById('rightRail').append(panel);}
 function view(){return api.state.inversions||api.state.privateState?.inversions;}
 function button(text,action){const b=make('button',text);b.type='button';b.addEventListener('click',action);return b;}
 function photograph(){
  const world=document.getElementById('world'),print=document.createElement('canvas');print.width=128;print.height=96;
  const me=api.state.players.get(api.state.playerId),subject=[...api.state.actors.values()].filter(a=>a.eventName&&(a.locationId||'outdoor')===(me.locationId||'outdoor')&&Math.hypot(a.position.x-me.position.x,a.position.y-me.position.y)<=40).sort((a,b)=>Math.hypot(a.position.x-me.position.x,a.position.y-me.position.y)-Math.hypot(b.position.x-me.position.x,b.position.y-me.position.y))[0];
  const p=api.toScreen(subject?.position||me.position),rect=world.getBoundingClientRect(),sx=world.width/(rect.width||world.width),sy=world.height/(rect.height||world.height);
  print.getContext('2d').drawImage(world,Math.max(0,p.x*sx-128),Math.max(0,p.y*sy-96),256,192,0,0,128,96);
  api.send({type:'photograph',subjectId:subject?.id,thumbnailDataUrl:print.toDataURL('image/png')});
 }
 function photoProgress(quest,items){const kind=quest.kind==='adventure:photo'?'event':'inspection';return Math.min(3,new Set(items.filter(i=>i.quantity>0&&i.photograph?.kind===kind).map(i=>i.photograph.evidence)).size);}
 function update(){
  if(!api)return;const me=api.state.players.get(api.state.playerId),v=view(),panel=document.getElementById('serverVotePanel');
  if(me){const air=document.getElementById('airValue');air.textContent=`${(me.air??10).toFixed(1)} / ${me.maximumAir??10}`;air.style.color=(me.air??10)<3?'#ff6258':'#bfeaff';}
  const e=v?.active,sig=JSON.stringify([!!me?.godMode,v?.vote,e?.id,e?.kills,e?.message,v?.completedDungeons,v?.queued,e?.type==='sender'?e.missiles.filter(m=>m.kind==='returnable').map(m=>m.id):null]);if(sig===signature)return;signature=sig;panel.replaceChildren();panel.hidden=!v?.vote&&!v?.active&&!v?.queued?.length;
  if(v?.vote){panel.append(make('h2',v.vote.round>1?'Server Vote — tie runoff':'Server Vote'));const time=make('strong');time.dataset.expires=v.vote.endsAtUtc;panel.append(time);
   for(const option of v.vote.options){const row=make('div');row.className='vote-option';row.append(make('strong',option.name),button('Vote',()=>api.send({type:'castServerVote',option:option.id})),make('small',option.voters.length?option.voters.join(', '):'No votes yet'));panel.append(row);}
   if(me?.godMode)panel.append(button('God mode: cancel vote',()=>api.send({type:'cancelServerVote'})));
  }
  if(v?.active){const e=v.active;panel.append(make('h3',e.name));const timer=make('strong');timer.dataset.expires=e.endsAtUtc;panel.append(timer,make('p',e.message));
   if(e.type==='smug')panel.append(make('p',`${e.kills} / 50 smug citizens defeated`));
   if(['retro','turns','cards'].includes(e.type)){panel.append(make('p',`${v.completedDungeons||0} / 2 dungeons completed`),button('Enter next event dungeon',()=>api.send({type:'enterEventDungeon'})));}
   if(e.type==='sender')for(const m of e.missiles.filter(m=>m.kind==='returnable'))panel.append(button('Return incoming projectile',()=>api.send({type:'reflectEventMissile',missileId:m.id})));
  }
  if(v?.queued?.length)panel.append(make('p','Queued: '+v.queued.join(' → ')));
 }
 function tick(){update();for(const e of document.querySelectorAll('[data-expires]'))e.textContent=Math.max(0,Math.ceil((Date.parse(e.dataset.expires)-Date.now())/1000))+' seconds';battle();adventures();
  for(const source of api.state.chestContents?.sources||[])seenRewards.add(source.id);
  if(!api.state.chestContents){const rewards=(api.state.privateState?.loot||[]).filter(r=>r.dropKind==='eventReward'),reward=rewards.find(r=>!seenRewards.has(r.id));if(reward){for(const r of rewards)if(r.id===reward.id||r.position&&reward.position&&r.locationId===reward.locationId&&Math.hypot(r.position.x-reward.position.x,r.position.y-reward.position.y)<=4)seenRewards.add(r.id);api.send({type:'openLoot',lootId:reward.id});}}
 }
 const choices={goose:['Offer food','Catch goose'],mimic:['Offer coins','Subdue chest'],deliveries:['Deliver order'],scream:['Begin escort','Offer food'],refund:['Negotiate refund','Exchange weapon','Fight bandit'],gnomes:['Inspect footprints','Question neighbor','Return gnomes','Support gnome army'],roll:['Give supplies','Split supplies','Trade supplies'],ufo:['Ask for clue','Inspect vehicle'],ghost:['Return mower','Buy replacement','Persuade ghost'],bait:['Offer food'],photo:['Submit photographs'],insurance:['Begin escort'],rescue:['Collect favorite mug','Begin escort'],inspection:['Submit inspection'],lost:['Search for property','Return property','Keep property'],race:['Start race','Check checkpoint'],watch:['Inspect clue']};
 function adventures(){const prizes=(api.state.privateState?.loot||[]).filter(l=>l.dropKind==='eventReward');const quests=(api.state.privateState?.quests||[]).filter(q=>q.kind.startsWith('adventure:')&&['active','ready'].includes(q.status)),me=api.state.players.get(api.state.playerId),sig=JSON.stringify(quests)+me?.equippedWeapon+JSON.stringify(api.state.privateState?.achievements)+JSON.stringify(prizes)+JSON.stringify(api.state.privateState?.inventory?.items);if(sig===questSignature)return;questSignature=sig;const panel=document.getElementById('adventurePanel');panel.replaceChildren();panel.hidden=!prizes.length&&!quests.length&&me?.equippedWeapon!=='camera'&&!api.state.privateState?.achievements?.length;panel.append(make('h2','Questionable errands'));
  if(prizes.length)panel.append(button('Collect your event treasure',()=>api.send({type:'openLoot',lootId:prizes[0].id})));
  if(me?.equippedWeapon==='camera')panel.append(button('Photograph · uses 1 film',photograph));
  for(const q of quests){panel.append(make('h3',q.title),make('p',q.description),make('small',['adventure:photo','adventure:inspection'].includes(q.kind)&&q.status==='active'?`Matching prints carried: ${photoProgress(q,api.state.privateState?.inventory?.items||[])} / 3. Submitting consumes three prints; vendors buy them instead.`:`Progress: ${q.progress}`));if(q.nextStagePosition)panel.append(button('Go to quest marker',()=>api.navigateTo(q.nextStagePosition)));if(q.status==='active')for(const choice of choices[q.kind.slice(10)]||[])panel.append(button(choice,()=>api.send({type:'adventureAction',questId:q.id,choice})));}
  for(const achievement of api.state.privateState?.achievements||[])panel.append(button('Title: '+achievement,()=>api.send({type:'setAchievementTitle',title:achievement})));
 }
 function battle(){
  const d=api.state.dungeon,b=d?.eventBattle,p=document.getElementById('eventBattlePanel'),sig=JSON.stringify(b)+d?.isCompleted;if(sig===battleSignature)return;battleSignature=sig;p.replaceChildren();p.hidden=!b||b.mode==='retro';if(p.hidden)return;
  p.append(make('h2',b.mode==='cards'?'Deal With It!':'Wait Your Turn, You Goblin!'),make('p',`Dungeon ${b.dungeonNumber} / 2 · ${b.enemyName}`),make('strong',`${b.enemyHealth.toFixed(1)} / ${b.maximumEnemyHealth} hearts`),make('p',b.message));
  p.append(button('Leave dungeon',()=>api.send({type:'exitDungeon'})));
  if(d.isCompleted){if(b.dungeonNumber<2)p.append(button('Enter the big boss dungeon',()=>api.send({type:'enterEventDungeon'})));else p.append(make('p','Both dungeons complete. Your private treasure is ready.'));}
  else for(const card of b.hand)p.append(button(card,()=>api.send({type:'eventBattleAction',action:card})));
  if(b.mode==='cards'&&b.turn===0){const selects=[];for(let i=0;i<6;i++){const select=make('select');select.setAttribute('aria-label',`Deck card ${i+1}`);for(const name of ['Strike','Guard','Second Wind','Heavy Blow','Riposte','Poison','Focus','Wild Card']){const o=make('option',name);o.value=name;select.append(o);}select.value=['Strike','Guard','Second Wind','Heavy Blow','Riposte','Poison'][i];selects.push(select);p.append(select);}p.append(button('Save six-card deck',()=>api.send({type:'configureEventDeck',cards:selects.map(s=>s.value)})));}
 }
 function ring(ctx,position,radius,fill,stroke){const points=[];for(let n=0;n<=48;n++){const a=n*Math.PI/24;points.push(api.toScreen({...position,x:position.x+Math.cos(a)*radius,y:position.y+Math.sin(a)*radius}));}ctx.beginPath();points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath();if(fill){ctx.fillStyle=fill;ctx.fill();}if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=4;ctx.stroke();}}
 function overlay(ctx,now){
  const e=view()?.active;if(!e||api.state.dungeon)return;const wall=Date.now();ctx.save();ring(ctx,e.center,e.radius,e.type==='smug'?'#9aa98b':'rgba(178,67,96,.10)','#ff3131');
  for(const p of e.patches){const age=(wall-Date.parse(p.changesAtUtc))/1000,water=['deepWater','shallowWater'].includes(p.kind),warning=age<0||water&&(age>=3&&age<6||Date.parse(p.endsAtUtc)-wall<3000),kind=water&&age>=6?(p.kind==='deepWater'?'shallowWater':'deepWater'):p.kind;if(p.kind.startsWith('wave')){const d=Number(p.kind.slice(-1)),axis=d%2,sign=d<2?1:-1,offset=Math.max(-e.radius,Math.min(e.radius,-e.radius+(wall-Date.parse(p.changesAtUtc))/1000*e.radius/5))*sign;const a={...e.center,x:e.center.x+(axis===0?offset:-e.radius),y:e.center.y+(axis===1?offset:-e.radius)},b={...e.center,x:e.center.x+(axis===0?offset:e.radius),y:e.center.y+(axis===1?offset:e.radius)};const x=api.toScreen(a),y=api.toScreen(b);ctx.strokeStyle=warning?'#fff4a8':'#8de8ff';ctx.lineWidth=warning?3:14;ctx.beginPath();ctx.moveTo(x.x,x.y);ctx.lineTo(y.x,y.y);ctx.stroke();continue;}
   ring(ctx,p.position,p.radius,warning?'rgba(255,214,85,.3)':['stink','canadianGas'].includes(p.kind)?'rgba(102,162,31,.23)':kind==='deepWater'?'rgba(12,62,173,.65)':kind==='shallowWater'?'rgba(49,167,223,.5)':p.kind==='lava'?'rgba(255,73,16,.7)':'rgba(235,230,191,.7)',warning?'#ffe176':null);
  }
  for(const m of e.missiles){const target=api.toScreen(m.target);ring(ctx,m.target,m.kind==='stomp'?5:m.kind==='bullet'?1:8,'rgba(255,65,24,.18)','#ffbe63');ctx.fillStyle='#fff1cc';ctx.font='bold 12px monospace';ctx.fillText(`${m.kind.toUpperCase()} ${Math.max(0,(Date.parse(m.arrivesAtUtc)-wall)/1000).toFixed(1)}s`,target.x,target.y);}
  if(e.type==='flood'){ctx.strokeStyle='rgba(164,207,255,.65)';for(let n=0;n<65;n++){const a=n*2.39996,r=(n/65)**.5*e.radius,p=api.toScreen({...e.center,x:e.center.x+Math.cos(a)*r,y:e.center.y+Math.sin(a)*r});const y=p.y+(now/9+n*17)%70-35;ctx.beginPath();ctx.moveTo(p.x,y);ctx.lineTo(p.x-4,y+12);ctx.stroke();}}
  if(e.type==='smug'){const boss=api.state.actors.get(e.bossId);if(boss)drawActor(ctx,boss,now,true);}
  ctx.restore();
 }
 function drawActor(ctx,a,now,above=false){
  if(NorthernExposure.drawCanadian(ctx,a,now,api))return true;
  const e=view()?.active;if(e?.type==='budget'&&Math.hypot(a.position.x-e.center.x,a.position.y-e.center.y)<=e.radius){const p=api.toScreen(a.position),s=Math.max(8,api.state.scale);ctx.save();ctx.fillStyle='#b58a58';ctx.fillRect(p.x-s*.4,p.y-s*1.2,s*.8,s*1.2);ctx.strokeStyle='#66513a';ctx.strokeRect(p.x-s*.4,p.y-s*1.2,s*.8,s*1.2);ctx.fillStyle='#242218';ctx.fillRect(p.x-s*.2,p.y-s,2,2);ctx.fillRect(p.x+s*.15,p.y-s,2,2);ctx.restore();return true;}if(['ufo','tRex','brontosaurus','stegosaurus','raptor','giant','eventBear'].includes(a.subtype))return false;if(!e||(!a.id.startsWith('inversion:')&&!['sunflower','rose','tulip','daisy'].includes(a.subtype)))return false;
  const p=api.toScreen(a.position),boss=a.id===e.bossId,s=Math.max(8,api.state.scale*(boss?2.8:.7));ctx.save();ctx.translate(p.x,p.y);ctx.strokeStyle='#1b2527';ctx.lineWidth=2;const plant=['sunflower','rose','tulip','daisy'].includes(a.subtype);
  if(e.type==='mech'){ctx.fillStyle='#697d7f';ctx.fillRect(-s*.6,-s*2,s*1.2,s*1.4);ctx.fillRect(-s,-s*1.6,s*.4,s);ctx.fillRect(s*.6,-s*1.6,s*.4,s);ctx.fillRect(-s*.6,-s*.6,s*.4,s*.6);ctx.fillRect(s*.2,-s*.6,s*.4,s*.6);ctx.fillStyle='#ff4e2f';ctx.fillRect(-s*.3,-s*2.2,s*.6,s*.3);}
  else if(e.type==='flood'){ctx.fillStyle='#865b36';ctx.beginPath();ctx.moveTo(-s*2,-s);ctx.lineTo(s*2,-s);ctx.lineTo(s,0);ctx.lineTo(-s,0);ctx.closePath();ctx.fill();ctx.fillStyle='#eee5c3';ctx.fillRect(-s*.4,-s*2,s*.8,s);ctx.fillStyle='#e8bb92';ctx.beginPath();ctx.arc(0,-s*2.3,s*.3,0,7);ctx.fill();ctx.fillStyle='#fff';ctx.fillRect(-s*.22,-s*2.2,s*.44,s*.5);}
  else if(plant){ctx.strokeStyle='#5fbe4c';ctx.lineWidth=5;ctx.beginPath();ctx.moveTo(0,0);ctx.lineTo(0,-s);ctx.stroke();ctx.fillStyle={sunflower:'#ffc928',rose:'#f34a65',tulip:'#be82ee',daisy:'#fff7da'}[a.subtype];for(let n=0;n<7;n++){ctx.beginPath();ctx.arc(Math.cos(n)*s*.45,-s+Math.sin(n)*s*.45,s*.35,0,7);ctx.fill();}ctx.fillStyle='#654719';ctx.beginPath();ctx.arc(0,-s,s*.27,0,7);ctx.fill();}
  else if(a.subtype==='supportBarrel'||e.type==='barrel'){ctx.fillStyle='#a97442';ctx.fillRect(-s*.5,-s,s,s);ctx.strokeRect(-s*.5,-s,s,s);ctx.fillStyle='#ec7c9e';ctx.font=`${s}px serif`;ctx.fillText('♥',-s*.4,-s*.3);}
  else if(e.type==='geese'){ctx.fillStyle='#eeeedd';ctx.beginPath();ctx.ellipse(0,-s*.45,s*.7,s*.4,0,0,7);ctx.fill();ctx.fillRect(s*.3,-s*1.3,s*.25,s);ctx.beginPath();ctx.arc(s*.48,-s*1.3,s*.25,0,7);ctx.fill();ctx.fillStyle='#e99a2e';ctx.fillRect(s*.6,-s*1.35,s*.3,s*.15);}
  else if(e.type==='normal'){ctx.fillStyle='#9a7050';ctx.fillRect(-s,-s,s*2,s*.6);ctx.fillRect(-s,-s*1.8,s*.35,s*1.8);ctx.fillRect(s*.65,-s*1.8,s*.35,s*1.8);ctx.fillStyle='#fff';ctx.fillRect(-s*.5,-s*.85,s*.2,s*.2);ctx.fillRect(s*.3,-s*.85,s*.2,s*.2);}
  else {ctx.fillStyle=e.type==='budget'?'#b98a59':e.type==='smug'?'#465865':'#97454e';ctx.fillRect(-s*.5,-s*1.4,s,s*1.4);ctx.fillStyle='#e0b18d';ctx.beginPath();ctx.arc(0,-s*1.8,s*.5,0,7);ctx.fill();if(e.type==='smug'){ctx.fillStyle='#f7f7ec';ctx.fillRect(-s*.25,-s*1.55,s*.5,s*.12);}}
  ctx.fillStyle='#fff1bd';ctx.font=`bold ${boss?14:10}px monospace`;ctx.textAlign='center';ctx.fillText(a.name,0,-s*2.5);ctx.restore();return true;
 }
 function swim(ctx,player,isMe,now){const active=view()?.active;if(active?.type==='budget'&&player.locationId==='outdoor'&&Math.hypot(player.position.x-active.center.x,player.position.y-active.center.y)<=active.radius)return drawActor(ctx,{...player,subtype:'cardboard'},now);if(player.travelMode!=='swim')return false;const p=api.toScreen(player.position),s=Math.max(10,api.state.scale),moving=player.speedMetersPerSecond>.01,stroke=moving?Math.sin(now/130)*s*.35:0;ctx.save();ctx.translate(p.x,p.y);ctx.fillStyle=isMe?'#37689a':'#784f91';ctx.fillRect(-s*.25,-s*.55,s*.5,s*.45);ctx.fillStyle='#e6bc8c';ctx.beginPath();ctx.arc(0,-s*.8,s*.23,0,7);ctx.fill();for(const sign of [-1,1]){ctx.strokeStyle='#e6bc8c';ctx.lineWidth=s*.13;ctx.beginPath();ctx.moveTo(sign*s*.2,-s*.45);ctx.lineTo(sign*s*.65,-s*.25+stroke*sign);ctx.stroke();ctx.fillStyle='#ff9b31';ctx.beginPath();ctx.ellipse(sign*s*.4,-s*.35+stroke*sign/2,s*.2,s*.16,0,0,7);ctx.fill();}ctx.strokeStyle='#a2e7ef';ctx.lineWidth=1.5;for(let n=0;n<3;n++){ctx.beginPath();ctx.ellipse(0,0,s*(.7+n*.2)+(moving?(now/30)%7:0),s*(.12+n*.08),0,0,7);ctx.stroke();}ctx.restore();return true;}
 function dungeon(ctx,d){for(const w of d.waterAreas||[]){const a=api.toScreen({x:w.x,y:w.y,z:0}),b=api.toScreen({x:w.x+w.width,y:w.y+w.height,z:0});ctx.fillStyle=w.deep?'rgba(20,79,178,.8)':'rgba(70,180,211,.6)';ctx.fillRect(Math.min(a.x,b.x),Math.min(a.y,b.y),Math.abs(b.x-a.x),Math.abs(b.y-a.y));}for(const b of d.barriers||[]){if(b.destroyed)continue;const p=api.toScreen({x:b.x+b.width/2,y:b.y+b.height/2,z:0});ctx.fillStyle=b.kind==='blast'?'#93979e':'#bd7c42';ctx.fillRect(p.x-13,p.y-20,26,20);ctx.fillStyle='#fff';ctx.font='12px monospace';ctx.fillText(b.kind==='blast'?'💥':'🔥',p.x-7,p.y-6);}}
 let sceneCards=[],lastSceneTurn=-1,lastSceneAction=0;
 const cardHints={'Strike':'Deal 6 damage','Guard':'Block 4 damage','Second Wind':'Recover 4 hearts','Heavy Blow':'Deal 10 damage','Riposte':'Deal 4, block 4','Poison':'2 damage + 3 poison turns','Focus':'+8 on your next strike','Wild Card':'Deal 2–12 damage'};
 function battleScene(ctx,width,height,now){
  const d=api.state.dungeon,b=d?.eventBattle,me=api.state.players.get(api.state.playerId);if(!b||b.mode==='retro')return false;
  if(lastSceneTurn!==b.turn){lastSceneTurn=b.turn;lastSceneAction=now;}
  const cards=b.mode==='cards',cx=width/2,ground=height*.64,u=Math.max(3,Math.min(width/150,height/170)),flash=now-lastSceneAction<350;
  ctx.save();ctx.fillStyle=cards?'#102c2a':'#181d3c';ctx.fillRect(0,0,width,height);
  ctx.strokeStyle=cards?'#244840':'#303457';ctx.lineWidth=1;for(let x=0;x<width;x+=48){ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x,height);ctx.stroke();}for(let y=0;y<height;y+=48){ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(width,y);ctx.stroke();}
  ctx.fillStyle=cards?'#28614b':'#333f60';ctx.beginPath();ctx.ellipse(cx,ground,width*.42,height*.19,0,0,7);ctx.fill();ctx.strokeStyle='#b9a265';ctx.lineWidth=3;ctx.stroke();
  function sprite(x,enemy){const size=enemy?(b.dungeonNumber===2?1.8:1.2):1,y=ground-30,px=u*size;ctx.save();ctx.translate(x+(enemy&&flash?Math.sin(now/25)*4:0),y);ctx.fillStyle='#111a29';ctx.fillRect(-6*px,0,12*px,2*px);ctx.fillStyle=enemy?'#966497':'#65bdde';ctx.fillRect(-5*px,-12*px,10*px,10*px);ctx.fillStyle=enemy?'#91bd76':'#e9c18d';ctx.fillRect(-4*px,-19*px,8*px,7*px);ctx.fillStyle='#e5be57';ctx.fillRect(-5*px,-21*px,10*px,3*px);if(enemy){for(let j=-4;j<=4;j+=4)ctx.fillRect(j*px,-24*px,2*px,4*px);}ctx.fillStyle='#192434';ctx.fillRect(-2*px,-17*px,px,px);ctx.fillRect(2*px,-17*px,px,px);ctx.fillStyle='#f2e8b8';ctx.fillRect(5*px,-12*px,2*px,10*px);ctx.fillStyle='#66558c';ctx.fillRect(-4*px,-3*px,3*px,4*px);ctx.fillRect(px,-3*px,3*px,4*px);ctx.restore();}
  sprite(width*.25,false);sprite(width*.72,true);
  function label(text,x,y,size=16){ctx.fillStyle='#f5edca';ctx.font=`bold ${size}px monospace`;ctx.textAlign='center';ctx.fillText(text,x,y);}
  label(cards?'DEAL WITH IT!':'WAIT YOUR TURN, YOU GOBLIN!',cx,height*.19,Math.min(25,width/26));
  label(`DUNGEON ${b.dungeonNumber} OF 2 · ${b.dungeonNumber===1?'SMALL BOSS':'BIG BOSS'}`,cx,height*.24,13);
  label(b.enemyName,cx,height*.30,Math.min(18,width/30));
  ctx.fillStyle='#17232a';ctx.fillRect(width*.2,height*.33,width*.6,14);ctx.fillStyle='#e97777';ctx.fillRect(width*.2,height*.33,width*.6*Math.max(0,b.enemyHealth/b.maximumEnemyHealth),14);
  label(`${b.enemyHealth.toFixed(1)} / ${b.maximumEnemyHealth} hearts`,cx,height*.39,13);
  label(me?.name||'You',width*.25,ground+u*4,14);label(`${(me?.healthHearts??10).toFixed(1)} hearts`,width*.25,ground+u*7,12);
  label(d.isCompleted?'BOSS DEFEATED!':`NEXT: ${b.turn%3===2?'HEAVY ATTACK · 5':'ATTACK · 2'}`,width*.72,ground+u*6,12);
  sceneCards=[];const hand=d.isCompleted?[]:b.hand,gap=12,cw=Math.min(150,(width-70)/3),ch=Math.min(100,height*.15),top=height*.80;
  hand.forEach((card,i)=>{const x=cx-(hand.length*cw+(hand.length-1)*gap)/2+i*(cw+gap);ctx.fillStyle=cards?'#e8ddbc':'#304263';ctx.fillRect(x,top,cw,ch);ctx.strokeStyle='#c3a95c';ctx.lineWidth=2;ctx.strokeRect(x,top,cw,ch);ctx.fillStyle=cards?'#283a39':'#fff1c8';ctx.textAlign='center';ctx.font='bold 13px monospace';ctx.fillText(card,x+cw/2,top+ch*.36);ctx.font='10px monospace';ctx.fillText(cardHints[card]||'',x+cw/2,top+ch*.67);sceneCards.push({x,y:top,width:cw,height:ch,card});});
  if(d.isCompleted)label(b.dungeonNumber===1?'ENTER THE SECOND DUNGEON TO QUALIFY':'YOUR PRIVATE TREASURE IS READY',cx,top+35,14);
  ctx.restore();return true;
 }
 function canvasClick(x,y){if(!api.state.dungeon?.eventBattle||api.state.dungeon.eventBattle.mode==='retro')return false;const rect=document.getElementById('world').getBoundingClientRect();const card=sceneCards.find(c=>x-rect.left>=c.x&&x-rect.left<=c.x+c.width&&y-rect.top>=c.y&&y-rect.top<=c.y+c.height);if(card)api.send({type:'eventBattleAction',action:card.card});return true;}
 function click(point){const barrier=api.state.dungeon?.barriers?.find(b=>!b.destroyed&&point.x>=b.x-1&&point.x<=b.x+b.width+1&&point.y>=b.y-1&&point.y<=b.y+b.height+1);if(!barrier)return false;api.send({type:'attackDungeonBarrier',barrierId:barrier.id});return true;}
 root.Inversions={init,tick,battleScene,canvasClick,overlay,drawActor,swim,dungeon,click,insideSmug:(p)=>{const e=view()?.active;return e?.type==='smug'&&Math.hypot(p.x-e.center.x,p.y-e.center.y)<=e.radius;}};
})(globalThis);
