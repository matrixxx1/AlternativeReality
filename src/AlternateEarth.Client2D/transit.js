(function(root){
  'use strict';
  function canWait(player,stop){return !!player&&!!stop&&player.locationId==='outdoor'&&!player.ridingBusId&&!player.abduction&&Math.hypot(player.position.x-stop.position.x,player.position.y-stop.position.y)<=3;}
  function attackPoint(bus,point){const dx=Math.cos(bus.headingRadians),dy=Math.sin(bus.headingRadians),x=point.x-bus.position.x,y=point.y-bus.position.y,a=Math.max(-4.5,Math.min(4.5,x*dx+y*dy)),b=Math.max(-1.25,Math.min(1.25,-x*dy+y*dx));return {...bus.position,x:bus.position.x+dx*a-dy*b,y:bus.position.y+dy*a+dx*b};}
  function canBoard(player,bus){if(!player||!bus||player.locationId!=='outdoor'||player.ridingBusId||player.abduction||player.healthHearts<=0||bus.healthHearts<=0||bus.speedMetersPerSecond>.1)return false;const p=attackPoint(bus,player.position),right=(player.position.x-bus.position.x)*Math.sin(bus.headingRadians)-(player.position.y-bus.position.y)*Math.cos(bus.headingRadians);return right>=1.25&&Math.hypot(p.x-player.position.x,p.y-player.position.y)<=3;}
  function footprint(bus){const dx=Math.cos(bus.headingRadians),dy=Math.sin(bus.headingRadians),p=bus.position;return[[-1,-1],[1,-1],[1,1],[-1,1]].map(([a,b])=>({x:p.x+dx*a*4.5-dy*b*1.25,y:p.y+dy*a*4.5+dx*b*1.25}));}
  function routeProjection(path,width,height){const xs=path.map(p=>p.x),ys=path.map(p=>p.y),minX=Math.min(...xs),maxX=Math.max(...xs),minY=Math.min(...ys),maxY=Math.max(...ys),scale=Math.min((width-70)/Math.max(1,maxX-minX),(height-70)/Math.max(1,maxY-minY));return p=>({x:width/2+(p.x-(minX+maxX)/2)*scale,y:height/2-(p.y-(minY+maxY)/2)*scale});}
  function routeBuses(route,buses=[]){return buses.filter(b=>b.routeId===route.id&&Number.isFinite(b.position?.x)&&Number.isFinite(b.position?.y));}
  function drawRoute(canvas,route,stops,selectedStopId,buses=[]){
    const c=canvas.getContext('2d'),w=canvas.width,h=canvas.height;c.clearRect(0,0,w,h);c.fillStyle='#0c1b22';c.fillRect(0,0,w,h);if(!route.path.length)return;
    const vehicles=routeBuses(route,buses),project=routeProjection([...route.path,...stops.map(s=>s.position),...vehicles.map(b=>b.position)],w,h),points=route.path.map(project);
    c.strokeStyle='#57bfee';c.lineWidth=3;c.lineJoin='round';c.beginPath();points.forEach((p,i)=>i?c.lineTo(p.x,p.y):c.moveTo(p.x,p.y));c.closePath();c.stroke();
    for(let i=1;i<points.length;i++){const a=points[i-1],b=points[i],length=Math.hypot(b.x-a.x,b.y-a.y);if(length<14)continue;const angle=Math.atan2(b.y-a.y,b.x-a.x);for(let d=length/2;d<length;d+=90){const t=d/length;c.save();c.translate(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);c.rotate(angle);c.fillStyle='#b5eeff';c.beginPath();c.moveTo(5,0);c.lineTo(-4,-4);c.lineTo(-4,4);c.closePath();c.fill();c.restore();}}
    stops.forEach((stop,index)=>{const p=project(stop.position);c.beginPath();c.arc(p.x,p.y,stop.id===selectedStopId?11:8,0,Math.PI*2);c.fillStyle=stop.id===selectedStopId?'#ffd05f':'#e9f7ff';c.fill();c.fillStyle='#10202b';c.font='bold 10px sans-serif';c.textAlign='center';c.textBaseline='middle';c.fillText(String(index+1),p.x,p.y);});
    for(const bus of vehicles){const p=project(bus.position);c.save();c.translate(p.x,p.y);c.rotate(-(bus.headingRadians||0));c.fillStyle='#ff7043';c.strokeStyle='#ffffff';c.lineWidth=2;c.beginPath();c.moveTo(12,0);c.lineTo(-8,-8);c.lineTo(-5,0);c.lineTo(-8,8);c.closePath();c.fill();c.stroke();c.restore();c.font='bold 12px sans-serif';c.textAlign='center';c.textBaseline='bottom';c.strokeStyle='#0c1b22';c.lineWidth=4;c.strokeText('BUS',p.x,p.y-12);c.fillStyle='#ffffff';c.fillText('BUS',p.x,p.y-12);}
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
  function benchPosition(stop){return stop.benchPosition||stop.position;}
  function benchProjection(stop,project,scale){
    const seat=benchPosition(stop),heading=stop.headingRadians||0,dx=Math.cos(heading),dy=Math.sin(heading);
    return(x,y,z=0)=>{const p=project({...seat,x:seat.x+dx*x-dy*y,y:seat.y+dy*x+dx*y});return{x:p.x,y:p.y-z*scale*.72};};
  }
  function drawStop(c,stop,project,scale){
    const p=benchProjection(stop,project,scale),line=(a,b,color,width)=>{a=p(...a);b=p(...b);c.strokeStyle=color;c.lineWidth=width;c.beginPath();c.moveTo(a.x,a.y);c.lineTo(b.x,b.y);c.stroke();};
    c.save();c.lineCap='round';
    for(const x of [-.8,.8]){line([x,-.2,0],[x,-.2,.95],'#303e3b',Math.max(2,scale*.07));line([x,.2,0],[x,.2,.48],'#303e3b',Math.max(2,scale*.07));}
    for(const y of [-.2,0,.2])line([-1,y,.48],[1,y,.48],'#b4864b',Math.max(2,scale*.14));
    for(const z of [.7,.9])line([-1,-.23,z],[1,-.23,z],'#a37642',Math.max(2,scale*.13));
    const sign=project(stop.position);c.strokeStyle='#c3e2e8';c.lineWidth=2;c.beginPath();c.moveTo(sign.x,sign.y);c.lineTo(sign.x,sign.y-22);c.stroke();c.fillStyle='#146593';c.fillRect(sign.x-8,sign.y-34,16,15);c.fillStyle='#fff';c.font='bold 11px sans-serif';c.textAlign='center';c.fillText('B',sign.x,sign.y-23);c.restore();
  }
  function drawWaitingPlayer(c,player,stop,project,scale,isMe){
    // Use the authoritative player position, including while a refreshed transit view is in flight.
    const p=benchProjection({...stop,benchPosition:player.position},project,scale);
    const line=(points,color,width)=>{c.strokeStyle=color;c.lineWidth=Math.max(2,scale*width);c.beginPath();points.map(q=>p(...q)).forEach((q,i)=>i?c.lineTo(q.x,q.y):c.moveTo(q.x,q.y));c.stroke();};
    c.save();c.lineCap='round';c.lineJoin='round';
    for(const x of [-.15,.15]){line([[x,0,.5],[x,.4,.48],[x,.45,.1]],'#30475c',.14);line([[x,.45,.07],[x,.6,.07]],'#272524',.13);}
    line([[0,0,.55],[0,-.04,1.08]],isMe?'#37689a':'#784f91',.48);
    for(const x of [-.25,.25])line([[x,-.03,1.02],[x,.14,.68],[x,.33,.57]],isMe?'#e6c86c':'#d68855',.11);
    const head=p(0,-.04,1.34);c.fillStyle=isMe?'#e6c86c':'#d68855';c.beginPath();c.arc(head.x,head.y,Math.max(3,scale*.2),0,Math.PI*2);c.fill();
    if(player.equippedHat&&player.equippedHat!=='none'||player.hatOn){c.fillStyle='#735333';c.fillRect(head.x-scale*.24,head.y-scale*.18,scale*.48,Math.max(2,scale*.1));}
    if(scale>=6){const label=p(0,0,1.75);c.font='700 9px monospace';c.textAlign='center';c.fillStyle=isMe?'#fff3a5':'#f0e8d1';c.fillText(isMe?'YOU · Waiting for bus':player.name,label.x,label.y);}
    c.restore();
  }
  const api={canWait,canBoard,attackPoint,footprint,routeProjection,routeBuses,drawRoute,drawBus,benchPosition,benchProjection,drawStop,drawWaitingPlayer};if(typeof module==='object')module.exports=api;else root.BusTransit=api;
})(globalThis);
