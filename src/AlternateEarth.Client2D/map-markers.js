(function(root){
  function nearby(items,origin,limit=6,spacing=100){
    const candidates=items.map(item=>({item,distance:Math.hypot(item.position.x-origin.x,item.position.y-origin.y)}))
      .filter(x=>x.distance<=500).sort((a,b)=>a.distance-b.distance||String(a.item.id).localeCompare(String(b.item.id)));
    const selected=[];
    for(const {item} of candidates){if(selected.some(other=>Math.hypot(other.position.x-item.position.x,other.position.y-item.position.y)<spacing))continue;selected.push(item);if(selected.length===limit)break;}
    return selected;
  }
  const api={nearby};root.MapMarkers=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
