(function(root){
  function slots(garage){const r=garage.room,items=(garage.vehicles||[]).filter(v=>v.quantity>0),rows=Math.ceil(items.length/4);return items.map((vehicle,i)=>({...vehicle,x:r.x+3+(i%4)*(r.width-6)/3,y:r.y+3.5+Math.floor(i/4)*(r.height-7)/Math.max(1,rows-1)}));}
  function floor(ctx,garage,toScreen){
    const r=garage.room;
    for(const shape of [garage.passage,[{x:r.x,y:r.y},{x:r.x+r.width,y:r.y},{x:r.x+r.width,y:r.y+r.height},{x:r.x,y:r.y+r.height}]]){
      const points=shape.map(toScreen);ctx.fillStyle='#626b69';ctx.beginPath();ctx.moveTo(points[0].x,points[0].y);points.slice(1).forEach(p=>ctx.lineTo(p.x,p.y));ctx.closePath();ctx.fill();
    }
    const label=toScreen({x:r.x+r.width/2,y:r.y+1});ctx.fillStyle='#e9dfb7';ctx.font='bold 14px monospace';ctx.textAlign='center';ctx.fillText('GARAGE',label.x,label.y);
  }
  function vehicles(ctx,garage,toScreen,scale,pitch){
    for(const vehicle of slots(garage)){
      const p=toScreen(vehicle);ctx.save();ctx.translate(p.x,p.y);ctx.scale(Math.max(.28,scale/22*.7),Math.max(.28,scale/22*.7));
      ctx.strokeStyle='#ddcf83';ctx.lineWidth=1;ctx.strokeRect(-36,-28,72,57);ctx.lineWidth=3;ctx.fillStyle='#b99855';ctx.strokeStyle='#192628';
      const type=vehicle.itemType;
      if(type==='ufo'){ctx.fillStyle='#8bb7b1';ctx.beginPath();ctx.ellipse(0,0,28,11,0,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.fillStyle='#9ddcec';ctx.beginPath();ctx.ellipse(0,-7,13,10,0,Math.PI,Math.PI*2);ctx.fill();}
      else if(type==='inflatableRaft'||type==='swimmies'){ctx.fillStyle=type==='swimmies'?'#eda74e':'#dfba60';for(const x of type==='swimmies'?[-15,15]:[0]){ctx.beginPath();ctx.ellipse(x,0,type==='swimmies'?10:28,type==='swimmies'?16:18,0,0,Math.PI*2);ctx.fill();ctx.stroke();}if(type==='inflatableRaft'){ctx.fillStyle='#555d47';ctx.beginPath();ctx.ellipse(0,0,19,10,0,0,Math.PI*2);ctx.fill();}}
      else{const skate=type==='skateboard',motor=['motorcycle','dirtBike'].includes(type);for(const x of [-21,21]){ctx.fillStyle='#1a2226';ctx.beginPath();ctx.arc(x,skate?7:4,skate?5:10,0,Math.PI*2);ctx.fill();ctx.stroke();}ctx.strokeStyle=type==='eBike'?'#7fdae2':motor?'#d58954':'#e4c674';ctx.beginPath();ctx.moveTo(-21,4);ctx.lineTo(-6,skate?0:-12);ctx.lineTo(10,4);ctx.lineTo(-21,4);ctx.moveTo(10,4);ctx.lineTo(16,-16);ctx.lineTo(25,-16);ctx.stroke();if(motor){ctx.fillStyle='#b97543';ctx.fillRect(-10,-12,25,12);}if(skate){ctx.fillStyle='#c6a777';ctx.fillRect(-27,-4,54,7);}}
      ctx.fillStyle='#fff2c1';ctx.font='bold 13px sans-serif';ctx.textAlign='center';ctx.fillText(type.replace(/([A-Z])/g,' $1').replace(/^./,c=>c.toUpperCase())+(vehicle.quantity>1?` ×${vehicle.quantity}`:''),0,27);ctx.restore();
    }
  }
  const api={slots,floor,vehicles};if(typeof module!=='undefined')module.exports=api;else root.HomeGarage=api;
})(typeof window!=='undefined'?window:globalThis);
