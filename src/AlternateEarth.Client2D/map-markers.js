(function(root){
  function nearby(items,origin,limit=6,spacing=100){
    const candidates=items.map(item=>({item,distance:Math.hypot(item.position.x-origin.x,item.position.y-origin.y)}))
      .filter(x=>x.distance<=500).sort((a,b)=>a.distance-b.distance||String(a.item.id).localeCompare(String(b.item.id)));
    const selected=[];
    for(const {item} of candidates){if(selected.some(other=>Math.hypot(other.position.x-item.position.x,other.position.y-item.position.y)<spacing))continue;selected.push(item);if(selected.length===limit)break;}
    return selected;
  }
  function preferences(storage){try{const value=JSON.parse(storage.getItem('alternative-reality-minimap')||'{}');return {categories:value.categories||{},quests:value.quests||{}};}catch{return {categories:{},quests:{}};}}
  function savePreferences(storage,value){try{storage.setItem('alternative-reality-minimap',JSON.stringify(value));}catch{}}
  function categoryVisible(preferences,type){return preferences?.categories?.[type.toLowerCase()]!==false;}
  function questVisible(preferences,id){return preferences?.quests?.[id]!==false;}
  function project(position,origin,width,height,type,range=500,pad=16){
    if(!position||!origin)return null;
    const dx=position.x-origin.x,dy=position.y-origin.y;
    if(!Number.isFinite(dx)||!Number.isFinite(dy))return null;
    const pinned=['Casino','Bus','Quest objective'].includes(type);
    if(!pinned&&(Math.abs(dx)>range||Math.abs(dy)>range))return null;
    const edge=pinned?Math.min(1,range*.94/Math.max(Math.abs(dx),Math.abs(dy))):1;
    return {x:width/2+dx*edge/range*(width/2-pad),y:height/2-dy*edge/range*(height/2-pad),offMap:edge<1,distanceMeters:Math.hypot(dx,dy)};
  }
  const api={nearby,preferences,savePreferences,categoryVisible,questVisible,project};root.MapMarkers=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
