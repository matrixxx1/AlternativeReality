(function(root){
  'use strict';
  function canWait(player,stop){return !!player&&!!stop&&player.locationId==='outdoor'&&!player.ridingBusId&&!player.abduction&&Math.hypot(player.position.x-stop.position.x,player.position.y-stop.position.y)<=3;}
  function footprint(bus){const dx=Math.cos(bus.headingRadians),dy=Math.sin(bus.headingRadians),p=bus.position;return[[-1,-1],[1,-1],[1,1],[-1,1]].map(([a,b])=>({x:p.x+dx*a*4.5-dy*b*1.25,y:p.y+dy*a*4.5+dx*b*1.25}));}
  function routeProjection(path,width,height){const xs=path.map(p=>p.x),ys=path.map(p=>p.y),minX=Math.min(...xs),maxX=Math.max(...xs),minY=Math.min(...ys),maxY=Math.max(...ys),scale=Math.min((width-70)/Math.max(1,maxX-minX),(height-70)/Math.max(1,maxY-minY));return p=>({x:width/2+(p.x-(minX+maxX)/2)*scale,y:height/2-(p.y-(minY+maxY)/2)*scale});}
  function drawRoute(canvas,route,stops,selectedStopId){
    const c=canvas.getContext('2d'),w=canvas.width,h=canvas.height;c.clearRect(0,0,w,h);c.fillStyle='#0c1b22';c.fillRect(0,0,w,h);if(!route.path.length)return;
    const project=routeProjection([...route.path,...stops.map(s=>s.position)],w,h),points=route.path.map(project);
    c.strokeStyle='#57bfee';c.lineWidth=3;c.lineJoin='round';c.beginPath();points.forEach((p,i)=>i?c.lineTo(p.x,p.y):c.moveTo(p.x,p.y));c.closePath();c.stroke();
    for(let i=1;i<points.length;i++){const a=points[i-1],b=points[i],length=Math.hypot(b.x-a.x,b.y-a.y);if(length<14)continue;const angle=Math.atan2(b.y-a.y,b.x-a.x);for(let d=length/2;d<length;d+=90){const t=d/length;c.save();c.translate(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);c.rotate(angle);c.fillStyle='#b5eeff';c.beginPath();c.moveTo(5,0);c.lineTo(-4,-4);c.lineTo(-4,4);c.closePath();c.fill();c.restore();}}
    stops.forEach((stop,index)=>{const p=project(stop.position);c.beginPath();c.arc(p.x,p.y,stop.id===selectedStopId?11:8,0,Math.PI*2);c.fillStyle=stop.id===selectedStopId?'#ffd05f':'#e9f7ff';c.fill();c.fillStyle='#10202b';c.font='bold 10px sans-serif';c.textAlign='center';c.textBaseline='middle';c.fillText(String(index+1),p.x,p.y);});
    c.fillStyle='#d9efff';c.font='bold 12px sans-serif';c.textAlign='left';c.fillText('N ↑',14,18);
  }
  const api={canWait,footprint,routeProjection,drawRoute};if(typeof module==='object')module.exports=api;else root.BusTransit=api;
})(globalThis);
