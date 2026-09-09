(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.ActionTargets=api;})(typeof globalThis!=='undefined'?globalThis:this,()=>{
  const padding=10;
  function polygonHit(point,polygon,pad=padding){
    let inside=false;
    for(let i=0,j=polygon.length-1;i<polygon.length;j=i++){
      const a=polygon[i],b=polygon[j];
      if((a.y>point.y)!==(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
      const dx=b.x-a.x,dy=b.y-a.y,t=Math.max(0,Math.min(1,((point.x-a.x)*dx+(point.y-a.y)*dy)/(dx*dx+dy*dy||1)));
      if(Math.hypot(point.x-a.x-t*dx,point.y-a.y-t*dy)<=pad)return true;
    }
    return inside;
  }
  function collect(candidates,point){
    const seen=new Set();return candidates.filter(c=>{
      const hit=c.bounds?point.x>=c.bounds.left-padding&&point.x<=c.bounds.right+padding&&point.y>=c.bounds.top-padding&&point.y<=c.bounds.bottom+padding:
        c.polygons?c.polygons.some(p=>polygonHit(point,p)):
        Math.hypot(point.x-c.screen.x,point.y-c.screen.y)<=Math.max(22,c.radius||0);
      const key=c.kind+':'+c.id;if(!hit||seen.has(key))return false;seen.add(key);return true;
    });
  }
  return{padding,polygonHit,collect};
});
