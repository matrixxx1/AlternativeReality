const Casino = (() => {
  let state, send, stopTravel, toast, root, dialog, stations = [], locationId = null, round = null;
  let game = null, busy = false, pendingAt = 0, holds = new Set(), view = null, lastX = 2, lastTime = 0;
  let revealing = false, revealTimer = 0;
  const money = value => '$' + ((value || 0) / 100).toLocaleString(undefined, {minimumFractionDigits:2, maximumFractionDigits:2});
  const active = () => state?.dungeon?.storeCategory === 'casino';
  const rules = {
    blackjack:'Arcade payouts: blackjack returns 4x, other wins 3x, ties 1x. Beat the dealer without going over 21. Aces count as 1 or 11. Dealer stands on all 17s. No insurance, splitting, or doubling.',
    poker:'Hold cards and draw once. Arcade total returns: royal flush 499x, straight flush 99x, four of a kind 49x, full house 17x, flush 11x, straight 7x, three of a kind 5x, two pair or jacks or better 3x.',
    roulette:'European wheel, arcade payouts: a correct number returns 71x; color, odd/even, and low/high return 3x. Zero loses outside bets.',
    slots:'Three independent reels, five equally likely symbols. Arcade total returns: three SEVENs 39x; other triples 9x; exactly two CHERRYs 5x; one CHERRY 3x. Highest award only.',
    dice:'Two six-sided dice. Low wins on 2-6; high wins on 8-12. Seven loses. Arcade wins return 3.6x. All returns include your original stake.'
  };
  function init(options) {
    ({state, send, stopTravel, showToast:toast} = options);
    root = document.createElement('section'); root.id = 'casinoControls'; root.hidden = true;
    root.innerHTML = `<header><div><small>OPEN ALL NIGHT</small><strong>Lucky Lantern Casino</strong></div><span data-wallet></span><button data-leave>Leave casino</button></header><div data-stations></div><footer><button data-walk="a" aria-label="Walk left">&lt; Walk left</button><span>Walk with A / D or arrows. Tap the floor to walk.</span><button data-resume hidden>Resume hand</button><button data-rewards hidden>Bonus rewards</button><button data-walk="d" aria-label="Walk right">Walk right &gt;</button></footer>`;
    document.body.append(root);
    dialog = document.createElement('dialog'); dialog.id = 'casinoTable'; dialog.setAttribute('aria-labelledby','casinoGameName');
    dialog.innerHTML = `<div class="casino-table-heading"><h2 id="casinoGameName"></h2><button data-close aria-label="Return to casino floor">Back to floor</button></div><p data-table-wallet></p><p data-rules></p><form data-bet-form><label>Wager ($)<input data-wager type="number" inputmode="decimal" step="0.01" min="0.01" value="10" required></label><label data-choice-label hidden>Bet on<select data-choice></select></label><button type="submit" data-bet>Place wager</button></form><p class="casino-fine">The stake leaves your wallet when accepted. Returns include the stake. Winning bets of $1,000+ also award two items. Ties do not award items. Unfinished hands are saved when you leave.</p><div data-hand></div><p data-result role="status" aria-live="polite"></p><div data-actions></div><button data-refresh>Refresh saved hand</button>`;
    document.body.append(dialog);
    root.querySelector('[data-leave]').onclick = () => { close(); stopTravel(); send({type:'exitDungeon'}); };
    root.querySelector('[data-resume]').onclick = () => { if(round) open(round.game); };
    root.querySelector('[data-rewards]').onclick = () => {
      const reward = rewards()[0]; if(reward) { close(); send({type:'openLoot', lootId:reward.id}); }
    };
    for (const button of root.querySelectorAll('[data-walk]')) {
      button.onpointerdown = event => { event.preventDefault(); stopTravel(); state.keys.add(button.dataset.walk); button.setPointerCapture(event.pointerId); };
      button.onpointerup = button.onpointercancel = button.onlostpointercapture = () => state.keys.delete(button.dataset.walk);
    }
    dialog.querySelector('[data-close]').onclick = close;
    dialog.querySelector('[data-refresh]').onclick = () => { busy=false; request({type:'requestCasinoState'}); };
    dialog.querySelector('[data-bet-form]').onsubmit = event => {
      event.preventDefault(); if(busy || revealing) return;
      const amount = Number(dialog.querySelector('[data-wager]').value), cents = Math.round(amount * 100);
      if (!Number.isSafeInteger(cents) || cents <= 0 || cents > player().walletCents) { showError('Choose a wager within your wallet balance.'); return; }
      request({type:'casinoBet', roundId:uuid(), game, wagerCents:cents, choice:dialog.querySelector('[data-choice]').value});
    };
    window.addEventListener('keydown', event => {
      if(!active()) return;
      if(dialog.open) { if(!event.target.matches('input,select,textarea') && ['a','d','w','s','arrowleft','arrowright','arrowup','arrowdown'].includes(event.key.toLowerCase())) {event.preventDefault();event.stopImmediatePropagation();} return; }
      if(['ArrowUp','ArrowDown','w','s'].includes(event.key)) {event.preventDefault();event.stopImmediatePropagation();}
    }, true);
    for(const type of ['pointerdown','pointerup','click','dblclick','contextmenu']) window.addEventListener(type, event => {
      if(!active() || event.target.tagName !== 'CANVAS' || event.target.id !== 'world') return;
      event.preventDefault(); event.stopImmediatePropagation();
      if(type === 'pointerdown' && view && !dialog.open) {
        stopTravel(); state.target = {x:Math.max(.5,Math.min(55.5,view.left + event.clientX/view.ppm)), y:4}; state.path=[];
      }
    }, true);
    window.addEventListener('blur', () => { if(active()) state.keys.clear(); });
  }
  function uuid() {
    const bytes = crypto.getRandomValues(new Uint8Array(16)); bytes[6]=(bytes[6]&15)|64; bytes[8]=(bytes[8]&63)|128;
    const hex=[...bytes].map(n=>n.toString(16).padStart(2,'0')).join('');
    return `${hex.slice(0,8)}-${hex.slice(8,12)}-${hex.slice(12,16)}-${hex.slice(16,20)}-${hex.slice(20)}`;
  }
  function player() { return state.players.get(state.playerId) || {walletCents:0,position:{x:2}}; }
  function rewards() { return (state.privateState?.loot || []).filter(l=>l.id.startsWith('casino:') && l.ownerId===state.playerId); }
  function request(message) {
    if(state.socket?.readyState !== WebSocket.OPEN) { showError('Disconnected. Reconnect, then refresh your saved hand.'); return; }
    stopTravel(); busy=true; pendingAt=performance.now(); send(message); refreshTable();
  }
  function sync() {
    if(!root) return;
    root.hidden = !active();
    if(!active()) { if(locationId) {close();locationId=null;round=null;stations=[];busy=false;} return; }
    if(locationId !== state.dungeon.id) {
      locationId=state.dungeon.id; lastX=player().position.x; round=null; busy=false;
      queueMicrotask(()=>request({type:'requestCasinoState'}));
    }
    root.querySelector('[data-wallet]').textContent='Wallet '+money(player().walletCents);
    root.querySelector('[data-resume]').hidden=!round || round.phase==='complete';
    const count=rewards().length, rewardButton=root.querySelector('[data-rewards]');
    rewardButton.hidden=!count; rewardButton.textContent=`Bonus rewards (${count})`;
    if(busy && performance.now()-pendingAt>10000) { busy=false; showError('Still waiting. Refresh your saved hand before placing another wager.'); }
  }
  function receive(message) {
    const animate=busy && message.round?.phase==='complete' && (message.round.roundId!==round?.roundId || message.round.revision!==round?.revision);
    clearTimeout(revealTimer); revealing=animate && !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if(revealing) revealTimer=setTimeout(()=>{revealing=false;refreshTable();},2200);
    busy=false; holds=new Set(); round=message.round; stations=message.stations || stations;
    const container=root.querySelector('[data-stations]'); container.replaceChildren();
    for(const station of stations) {const button=document.createElement('button');button.dataset.game=station.game;button.textContent='Play '+station.name;button.onclick=()=>open(station.game);container.append(button);}
    if(round && round.phase!=='complete') game=round.game;
    refreshTable();
  }
  function showError(message) {
    busy=false;
    if(dialog?.open) {refreshTable();dialog.querySelector('[data-result]').textContent=message;}
    else toast?.(message);
  }
  function open(selected) {
    stopTravel(); game=round && round.phase!=='complete' ? round.game : selected;
    const select=dialog.querySelector('[data-choice]'); select.replaceChildren();
    const choices=game==='roulette' ? ['red','black','odd','even','low','high',...Array.from({length:37},(_,i)=>String(i))] : game==='dice' ? ['high','low'] : [];
    for(const value of choices) {const option=document.createElement('option');option.value=value;option.textContent=value==='low'&&game==='roulette'?'Low (1-18)':value==='high'&&game==='roulette'?'High (19-36)':value;select.append(option);}
    dialog.querySelector('[data-choice-label]').hidden=!choices.length;
    refreshTable(); if(!dialog.open) dialog.showModal();
  }
  function close() { if(dialog?.open) dialog.close(); state?.keys.clear(); }
  function act(action) { if(!busy && !revealing && round) request({type:'casinoAction',roundId:round.roundId,revision:round.revision,action,holds:[...holds]}); }
  function refreshTable() {
    if(!dialog || !game) return;
    const current=round?.game===game?round:null, playing=current && current.phase!=='complete';
    dialog.querySelector('h2').textContent=stations.find(s=>s.game===game)?.name || game.toUpperCase();
    dialog.querySelector('[data-rules]').textContent=rules[game];
    dialog.querySelector('[data-table-wallet]').textContent='Wallet '+money(player().walletCents)+(current?' | Stake '+money(current.wagerCents):'');
    const wager=dialog.querySelector('[data-wager]');wager.max=(player().walletCents/100).toFixed(2);
    const form=dialog.querySelector('[data-bet-form]');form.hidden=!!playing;
    for(const element of form.elements) element.disabled=busy || revealing;
    dialog.querySelector('[data-bet]').disabled=busy || revealing || player().walletCents<1;
    const hand=dialog.querySelector('[data-hand]');hand.replaceChildren();
    const addCards=(cards,label,holdable)=>{
      if(!cards?.length)return;const caption=document.createElement('p');caption.textContent=label;hand.append(caption);
      const row=document.createElement('div');row.className='casino-cards';
      cards.forEach((card,index)=>{const cell=document.createElement(holdable?'button':'span');cell.className='casino-card';paintCard(cell,card);cell.classList.toggle('red',/[HD]$/.test(card));
        if(holdable){cell.type='button';cell.disabled=busy;cell.setAttribute('aria-pressed',holds.has(index));cell.classList.toggle('held',holds.has(index));cell.title='Hold '+card;cell.onclick=()=>{holds.has(index)?holds.delete(index):holds.add(index);refreshTable();};}row.append(cell);});hand.append(row);
    };
    addCards(current?.dealerCards,'Dealer',false); addCards(current?.cards,game==='poker'&&playing?'Your hand: tap cards to HOLD':'Your hand',game==='poker'&&playing);
    drawGameDisplay(hand,current);
    const profit=current ? current.payoutCents-current.wagerCents : 0;
    dialog.querySelector('[data-result]').textContent=busy?'Wager / action pending...':revealing?'Playing...':current?current.message+(current.phase==='complete'?` Payout: ${money(current.payoutCents)}. ${profit>0?'NET PROFIT: '+money(profit):profit===0?'Push: stake refunded, no profit.':'Lost: '+money(-profit)}.`:''):'';
    const actions=dialog.querySelector('[data-actions]');actions.replaceChildren();
    function button(label,callback){const b=document.createElement('button');b.textContent=label;b.disabled=busy || revealing;b.onclick=callback;actions.append(b);}
    if(playing) {
      if(current.phase==='resolve') button(game==='slots'?'Spin reels':game==='roulette'?'Spin wheel':game==='dice'?'Roll dice':'Reveal hand',()=>act('resolve'));
      else if(game==='blackjack'){button('Hit',()=>act('hit'));button('Stand',()=>act('stand'));}
      else button('Draw unheld cards',()=>act('draw'));
    }
    if(current?.rewardLootId && rewards().some(l=>l.id===current.rewardLootId)) button('Collect bonus items',()=>{close();send({type:'openLoot',lootId:current.rewardLootId});});
  }
  function paintCard(cell,card) {
    if(card==='?'){cell.classList.add('card-back');cell.setAttribute('aria-label','Face-down card');return;}
    const rank=card.slice(0,-1),suit=card.slice(-1),symbol={H:'\u2665',D:'\u2666',C:'\u2663',S:'\u2660'}[suit] || '';
    cell.setAttribute('aria-label',`${rank} of ${{H:'hearts',D:'diamonds',C:'clubs',S:'spades'}[suit]}`);
    for(const corner of ['top','bottom']){const el=document.createElement('span');el.className='card-corner '+corner;el.textContent=rank+'\n'+symbol;cell.append(el);}
    const face=document.createElement('span');face.className='card-face';
    if(/^[JQK]$/.test(rank)){face.classList.add('court');face.textContent=({J:'\u265d',Q:'\u265b',K:'\u265a'}[rank])+'\n'+symbol;}
    else {
      const layouts={A:[[1,2]],2:[[1,0],[1,4]],3:[[1,0],[1,2],[1,4]],4:[[0,0],[2,0],[0,4],[2,4]],5:[[0,0],[2,0],[1,2],[0,4],[2,4]],6:[[0,0],[2,0],[0,2],[2,2],[0,4],[2,4]],7:[[0,0],[2,0],[1,1],[0,2],[2,2],[0,4],[2,4]],8:[[0,0],[2,0],[1,1],[0,2],[2,2],[1,3],[0,4],[2,4]],9:[[0,0],[2,0],[0,1],[2,1],[1,2],[0,3],[2,3],[0,4],[2,4]],10:[[0,0],[2,0],[1,.7],[0,1.3],[2,1.3],[0,2.7],[2,2.7],[1,3.3],[0,4],[2,4]]};
      for(const [x,y] of layouts[rank] || layouts.A){const pip=document.createElement('span');pip.textContent=symbol;pip.style.left=(15+x*35)+'%';pip.style.top=(10+y*20)+'%';face.append(pip);}
    }
    cell.append(face);
  }
  function drawGameDisplay(hand,current) {
    if(!['roulette','slots','dice'].includes(game))return;
    const display=document.createElement('div');display.className='casino-display '+game+(revealing?' revealing':'');
    display.setAttribute('role','img');
    const symbols=current?.symbols || [];
    if(game==='roulette') {
      const pockets=[0,32,15,19,4,21,2,25,17,34,6,27,13,36,11,30,8,23,10,5,24,16,33,1,20,14,31,9,22,18,29,7,28,12,35,3,26],step=360/37,index=Math.max(0,pockets.indexOf(Number(symbols[0] || 0)));
      const wheel=document.createElement('div');wheel.className='roulette-wheel';wheel.style.setProperty('--landing',(-index*step)+'deg');
      wheel.style.background=`conic-gradient(from ${-step/2}deg, ${pockets.map((n,i)=>`${n===0?'#18794f':i%2?'#b42d3d':'#172222'} ${i*step}deg ${(i+1)*step}deg`).join(',')})`;
      pockets.forEach((n,i)=>{const label=document.createElement('span');label.className='wheel-number';label.style.transform=`rotate(${i*step}deg)`;const value=document.createElement('b');value.textContent=n;label.append(value);wheel.append(label);});
      const hub=document.createElement('span');hub.className='wheel-hub';hub.textContent='LL';wheel.append(hub);display.append(wheel);
      const orbit=document.createElement('div');orbit.className='ball-orbit';orbit.innerHTML='<span class="roulette-ball"></span>';display.append(orbit);
      display.setAttribute('aria-label',revealing?'Roulette wheel spinning':symbols.length?'Roulette ball on '+symbols.join(' '):'European roulette wheel');
    } else if(game==='slots') {
      const title=document.createElement('div');title.className='slot-title';title.textContent='LUCKY LANTERN';display.append(title);
      const reels=document.createElement('div');reels.className='slot-windows';
      const icons={CHERRY:'\u{1f352}',LEMON:'\u{1f34b}',BELL:'\u{1f514}',SEVEN:'7',DIAMOND:'\u2666'},names=Object.keys(icons);
      for(let i=0;i<3;i++) {
        const reel=document.createElement('div');reel.className='slot-window';const strip=document.createElement('div');strip.className='slot-strip';strip.style.animationDuration=(1.4+i*.35)+'s';
        const target=symbols[i] || 'SEVEN';
        for(const name of [target,...Array.from({length:14},(_,j)=>names[(j+i)%5]),target]){const icon=document.createElement('span');icon.textContent=icons[name] || '?';strip.append(icon);}
        reel.append(strip);reels.append(reel);
      }
      display.append(reels);const tray=document.createElement('div');tray.className='slot-tray';tray.textContent='777 JACKPOT  /  39x';display.append(tray);
      const lever=document.createElement('span');lever.className='slot-lever';display.append(lever);
      display.setAttribute('aria-label',revealing?'Slot reels spinning':symbols.length?'Slot result: '+symbols.join(', '):'Three-reel slot machine');
    } else {
      for(let i=0;i<2;i++){
        const die=document.createElement('div');die.className='casino-die';die.style.animationDelay=(i*.08)+'s';const n=Number(symbols[i] || i+3);
        const spots={1:[4],2:[0,8],3:[0,4,8],4:[0,2,6,8],5:[0,2,4,6,8],6:[0,2,3,5,6,8]};
        for(let j=0;j<9;j++){const pip=document.createElement('span');pip.className=(spots[n] || []).includes(j)?'pip':'empty';die.append(pip);}display.append(die);
      }
      display.setAttribute('aria-label',revealing?'Dice rolling':symbols.length?'Dice: '+symbols.join(' and '):'Two six-sided dice');
    }
    hand.append(display);
  }
  function draw(ctx,width,height,now) {
    if(!active()) return false;
    sync();
    const me=player(), ppm=Math.max(32,Math.min(62,width/11)), left=Math.max(0,Math.min(56-width/ppm,me.position.x-width/ppm*.45));
    view={left,ppm};root.style.width=width+'px';
    const floor=Math.max(260,height*.73), scale=Math.max(.7,Math.min(1.35,height/720)), screen=x=>(x-left)*ppm;
    const background=ctx.createLinearGradient(0,140,0,floor);background.addColorStop(0,'#152b2b');background.addColorStop(1,'#33504a');ctx.fillStyle=background;ctx.fillRect(0,0,width,height);
    ctx.fillStyle='#8c293d';ctx.fillRect(0,floor,width,height-floor);ctx.fillStyle='#d5b96c';ctx.fillRect(0,floor-5,width,5);
    for(let x=Math.floor(left/2)*2;x<left+width/ppm+2;x+=2){ctx.fillStyle='#b14451';ctx.fillRect(screen(x),floor+30,25,4);ctx.fillRect(screen(x)+35,floor+70,25,4);}
    for(let x=0;x<56;x+=4.572){const sx=screen(x);if(sx<-200||sx>width+200)continue;
      ctx.fillStyle='#19312f';ctx.fillRect(sx-45,160,90,floor-160);ctx.strokeStyle='#aa8c4b';ctx.strokeRect(sx-45,160,90,floor-160);
      ctx.fillStyle='#661f35';ctx.fillRect(sx-52,155,18,floor-155);ctx.fillRect(sx+34,155,18,floor-155);
      ctx.fillStyle='#f6dd8b';ctx.fillRect(sx-14,175,28,11);ctx.fillStyle='#8d733e';ctx.fillRect(sx-1,135,2,40);
    }
    function person(x,y,shirt,seed,moving=false){const s=scale,bob=moving?Math.sin(now/95)*2:Math.sin(now/1100+seed)*.6;ctx.save();ctx.translate(x,y+bob);ctx.scale(s,s);
      ctx.fillStyle='#171c2a';const step=moving?Math.sin(now/85)*5:0;ctx.fillRect(-11,-23,8,23+step);ctx.fillRect(3,-23,8,23-step);
      ctx.fillStyle=shirt;ctx.fillRect(-14,-55,28,33);ctx.fillStyle='#d9a078';ctx.fillRect(-10,-76,20,21);ctx.fillRect(-19,-48,5,24);ctx.fillRect(14,-48,5,24);ctx.fillStyle='#302629';ctx.fillRect(-12,-80,24,9);ctx.restore();}
    const door=screen(2);ctx.fillStyle='#251e23';ctx.fillRect(door-32,floor-115*scale,64,115*scale);ctx.fillStyle='#e1ce88';ctx.font='bold 16px Georgia';ctx.textAlign='center';ctx.fillText('EXIT',door,floor-125*scale);
    for(const station of stations){const x=screen(station.x), tableY=floor-12*scale;
      const b=root.querySelector(`[data-game="${station.game}"]`);if(b){b.hidden=x<-110||x>width+110;b.style.left=(x-75)+'px';b.style.top=(floor+22)+'px';b.disabled=busy||Math.abs(me.position.x-station.x)>2.8;}
      if(x<-180||x>width+180)continue;
      person(x-62*scale,tableY,'#aa7953',station.x);person(x+62*scale,tableY,'#8297a1',station.x+1);person(x,tableY-18*scale,'#ede0bf',station.x+2);
      ctx.fillStyle='#c5a55e';ctx.fillRect(x-86*scale,tableY-50*scale,172*scale,13*scale);ctx.fillStyle='#174c43';ctx.fillRect(x-80*scale,tableY-45*scale,160*scale,36*scale);ctx.fillStyle='#372b27';ctx.fillRect(x-65*scale,tableY-9*scale,12*scale,25*scale);ctx.fillRect(x+53*scale,tableY-9*scale,12*scale,25*scale);
      if(station.game==='slots'){for(let i=-1;i<=1;i++){ctx.fillStyle='#cfab51';ctx.fillRect(x+i*46*scale-19*scale,tableY-104*scale,38*scale,64*scale);ctx.fillStyle='#142c2f';ctx.fillRect(x+i*46*scale-15*scale,tableY-94*scale,30*scale,34*scale);ctx.fillStyle='#f9da6c';ctx.font=`bold ${20*scale}px monospace`;ctx.fillText('7',x+i*46*scale,tableY-69*scale);}}
      else if(station.game==='roulette'){ctx.fillStyle='#9c313d';ctx.beginPath();ctx.ellipse(x,tableY-52*scale,32*scale,12*scale,0,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#dfc47d';ctx.stroke();}
      else {ctx.fillStyle='#f3e6be';for(let i=0;i<3;i++)ctx.fillRect(x+(i-1)*17,tableY-56*scale,12,8);}
      const signY=floor-190*scale;ctx.fillStyle='#1c2528';ctx.fillRect(x-101*scale,signY-29*scale,202*scale,42*scale);ctx.strokeStyle='#dfba60';ctx.strokeRect(x-101*scale,signY-29*scale,202*scale,42*scale);ctx.fillStyle='#fbe4a0';ctx.font=`bold ${15*scale}px Georgia`;ctx.fillText(station.name,x,signY-2*scale);
    }
    const moving=Math.abs(me.position.x-lastX)>.001 || (now-lastTime<140);if(Math.abs(me.position.x-lastX)>.001)lastTime=now;lastX=me.position.x;
    person(screen(me.position.x),floor+12*scale,'#50b7b0',0,moving);ctx.fillStyle='#fff1c6';ctx.font='bold 12px monospace';ctx.fillText('YOU',screen(me.position.x),floor-77*scale);
    return true;
  }
  return {init,sync,receive,draw,active,error:showError};
})();
