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
  function drawBus(c,bus,project,scale,aboard=false){
    const o=project(bus.position),dx=Math.cos(bus.headingRadians),dy=Math.sin(bus.headingRadians),
      f=project({x:bus.position.x+dx,y:bus.position.y+dy}),r=project({x:bus.position.x-dy,y:bus.position.y+dx});
    const p=(x,y,z=0)=>({x:o.x+(f.x-o.x)*x+(r.x-o.x)*y,y:o.y+(f.y-o.y)*x+(r.y-o.y)*y-z*scale*.72});
    const polygon=(points,fill,stroke='#273137')=>{c.beginPath();points.forEach((v,i)=>{const q=p(...v);i?c.lineTo(q.x,q.y):c.moveTo(q.x,q.y);});c.closePath();c.fillStyle=fill;c.fill();if(stroke){c.strokeStyle=stroke;c.lineWidth=Math.max(.7,scale*.035);c.stroke();}};
    const side=(y,color)=>{
      polygon([[-4.5,y,.45],[4.5,y,.45],[4.5,y,2.7],[-4.5,y,2.7]],color);
      polygon([[-4.4,y,.7],[4.4,y,.7],[4.4,y,1.05],[-4.4,y,1.05]],'#245877',null);
      for(let x=-3.9;x<2.4;x+=1.3)polygon([[x,y,1.4],[x+1.08,y,1.4],[x+1.08,y,2.45],[x,y,2.45]],'#233d4b');
      if(y<0){polygon([[2.7,y,.5],[4.15,y,.5],[4.15,y,2.45],[2.7,y,2.45]],'#233d4b');polygon([[3.38,y,.5],[3.46,y,.5],[3.46,y,2.45],[3.38,y,2.45]],'#c2ced0',null);}
      for(const x of [-2.9,2.7]){const points=[];for(let i=0;i<16;i++){const a=i*Math.PI/8;points.push([x+Math.cos(a)*.48,y,.5+Math.sin(a)*.48]);}polygon(points,'#15191c');const hub=p(x,y,.5);c.fillStyle='#abb7bc';c.beginPath();c.arc(hub.x,hub.y,Math.max(1,scale*.16),0,Math.PI*2);c.fill();}
    };
    c.save();polygon([[-4.7,-1.4],[4.7,-1.4],[4.7,1.4],[-4.7,1.4]],'rgba(0,0,0,.3)',null);
    const sides=[-1.25,1.25].sort((a,b)=>p(0,a).y-p(0,b).y);for(const y of sides)side(y,bus.healthHearts<=0?'#806b58':y<0?'#efc54f':'#caa138');
    polygon([[4.5,-1.25,.45],[4.5,1.25,.45],[4.5,1.25,2.7],[4.5,-1.25,2.7]],'#e3b847');
    polygon([[4.51,-1.08,1.4],[4.51,1.08,1.4],[4.51,1.08,2.4],[4.51,-1.08,2.4]],'#263f4c');
    polygon([[4.52,-1.05,2.42],[4.52,1.05,2.42],[4.52,1.05,2.65],[4.52,-1.05,2.65]],'#182b28');
    polygon([[-4.5,-1.25,2.7],[4.5,-1.25,2.7],[4.5,1.25,2.7],[-4.5,1.25,2.7]],'#e6e6d7');
    polygon([[-1.2,-.65,2.74],[.9,-.65,2.74],[.9,.65,2.74],[-1.2,.65,2.74]],'#aab6b8');
    for(const y of [-.9,.9]){const head=p(4.53,y,.9);c.fillStyle='#fff1b4';c.beginPath();c.arc(head.x,head.y,Math.max(1.2,scale*.12),0,Math.PI*2);c.fill();}
    if(scale>=6){const label=p(0,0,3.05);c.font=`bold ${Math.max(10,Math.min(14,scale*.5))}px sans-serif`;c.textAlign='center';c.fillStyle=aboard?'#fff2a1':'#e7f6ff';c.strokeStyle='#10272b';c.lineWidth=3;c.strokeText(aboard?'BUS · YOU':'CITY BUS',label.x,label.y);c.fillText(aboard?'BUS · YOU':'CITY BUS',label.x,label.y);}
    c.restore();
  }
  const api={canWait,footprint,routeProjection,drawRoute,drawBus};if(typeof module==='object')module.exports=api;else root.BusTransit=api;
})(globalThis);
