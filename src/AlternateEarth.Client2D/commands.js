/* Shared command state helpers, also exercised by the Node regression tests. */
(function(root,factory){if(typeof module==='object'&&module.exports)module.exports=factory();else root.PlayerCommands=factory();})(globalThis,()=>{
  const pending=['pendingDoor','pendingMerchant','pendingChest','pendingDungeonAction','pendingChop','pendingPet'];
  function cancel(state){
    state.path=[];state.target=null;state.followCommand=null;state.autoFlee=false;state.defensiveThreats?.clear();
    for(const key of pending)state[key]=null;
    state.keys.clear();state.pathSequence++;state.commandSequence=(state.commandSequence||0)+1;
    state.areaLoading=false;state.loadingArea=null;state.loadingOrigin=null;state.loadingStarted=0;
  }
  function attackPoint(target,origin){
    const geometry=target.kind==='building'?target.geometry:null;
    if(!geometry?.length)return target.position;
    let best=target.position,distance=Infinity;
    for(let i=0;i<geometry.length;i++){
      const a=geometry[i],b=geometry[(i+1)%geometry.length],dx=b.x-a.x,dy=b.y-a.y;
      const t=Math.max(0,Math.min(1,((origin.x-a.x)*dx+(origin.y-a.y)*dy)/(dx*dx+dy*dy||1)));
      const point={x:a.x+t*dx,y:a.y+t*dy},d=Math.hypot(point.x-origin.x,point.y-origin.y);
      if(d<distance){best=point;distance=d;}
    }
    return best;
  }
  function defeated(target){return !target||target.properties?.state==='rubble'||Number(target.properties?.healthHearts??target.healthHearts??1)<=0;}
  const modes=['timid','defensive','neutral','attackReady','aggressive'];
  function nextMode(mode){return modes[(modes.indexOf(mode)+1)%modes.length];}
  function clickAttacks(mode){return mode==='attackReady'||mode==='aggressive';}
  function automaticAction({mode,me,targets,players,relationships,attackers,now,pvpEnabled=true,avoid=new Map()}){
    const nearby=targets.filter(target=>target.id!==me.id&&!defeated(target)&&!target.abduction&&
      (target.locationId||'outdoor')===(me.locationId||'outdoor'))
      .map(target=>({target,distance:Math.hypot(target.position.x-me.position.x,target.position.y-me.position.y)}))
      .sort((a,b)=>a.distance-b.distance);
    if(mode==='timid'){
      const threats=nearby.filter(({target,distance})=>distance<12&&(players.has(target.id)||(relationships.get(target.id)??target.friendRating??0)<0));
      if(!threats.length)return null;
      let x=0,y=0;
      for(const {target,distance}of threats){const weight=1/Math.max(.5,distance)**2;x+=(me.position.x-target.position.x)*weight;y+=(me.position.y-target.position.y)*weight;}
      const length=Math.hypot(x,y);
      if(length<.001){x=me.position.x-threats[0].target.position.x;y=me.position.y-threats[0].target.position.y;if(Math.hypot(x,y)<.001)x=1;}
      const norm=Math.hypot(x,y);return {kind:'flee',destination:{x:me.position.x+x/norm*8,y:me.position.y+y/norm*8}};
    }
    if(!['aggressive','defensive'].includes(mode)||(me.equippedWeapon||'none')==='none')return null;
    const candidate=nearby.find(({target,distance})=>(players.has(target.id)?pvpEnabled:['npc','animal'].includes(target.kind))&&
      (avoid.get(target.id)||0)<=now&&(mode==='aggressive'?distance<=15:(attackers.get(target.id)||0)>now));
    return candidate?{kind:'attack',target:candidate.target}:null;
  }
  return {cancel,attackPoint,defeated,nextMode,clickAttacks,automaticAction};
});
