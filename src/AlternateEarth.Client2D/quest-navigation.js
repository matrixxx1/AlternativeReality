(function(root){
  const directions=['N','NE','E','SE','S','SW','W','NW'];
  function bearing(from,to){const dx=to.x-from.x,dy=to.y-from.y;return `${Math.round(Math.hypot(dx,dy))} m ${directions[Math.round((Math.atan2(dx,dy)*180/Math.PI+360)/45)%8]}`;}
  function actorBounds(actor,scale){
    const profiles={tRex:[38,2.75,-2.8,2.1,-1.7,.85],brontosaurus:[42,3.65,-3.2,2.8,-2.5,.9],stegosaurus:[30,2.3,-2.85,2,-1.5,.8],raptor:[17,.8,-2.2,1.9,-1.2,.85],bear:[10,.53,-1.25,1.55,-1.4,.75],eventBear:[18,.78,-1.25,1.55,-1.4,.75],waterMonster:[16,1.3,-2,2,-1.3,.6],fish:[5,.28,-1.7,1.7,-.9,.5]};
    if(actor.subtype==='giant'){const h=Math.max(120,scale*15.24*.52);return {left:-h*.4,right:h*.4,top:-h*1.12,bottom:h*.12};}
    const p=profiles[actor.subtype];
    if(p){const s=Math.max(p[0],scale*p[1]),flip=actor.facing==='west';return {left:(flip?-p[3]:p[2])*s-5,right:(flip?-p[2]:p[3])*s+5,top:p[4]*s-5,bottom:p[5]*s+5};}
    return {left:-Math.max(12,scale*.55),right:Math.max(12,scale*.55),top:-Math.max(24,scale*1.45),bottom:Math.max(8,scale*.3)};
  }
  const api={bearing,actorBounds};root.QuestNavigation=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
