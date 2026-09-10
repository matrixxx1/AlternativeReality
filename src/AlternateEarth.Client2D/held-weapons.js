/* Small, outlined canvas silhouettes shared by walking players and divers. */
const HeldWeapons = (() => {
  function draw(ctx, weapon, size, {now=0,moving=false,recoil=0,hands=true}={}) {
    ctx.save();ctx.scale(size/40,size/40);
    ctx.translate(-recoil*3,moving?Math.sin(now/140)*.8:0);ctx.rotate(-recoil*.07);
    ctx.lineJoin='round';ctx.lineCap='round';
    function shape(points,color){ctx.beginPath();points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.closePath();ctx.fillStyle=color;ctx.fill();ctx.strokeStyle='#16242c';ctx.lineWidth=1.2;ctx.stroke();}
    function line(points,color,width=1){ctx.beginPath();points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.strokeStyle=color;ctx.lineWidth=width;ctx.stroke();}
    if(weapon==='crossbow'){
      shape([[-10,-2],[15,-2],[18,0],[13,3],[-3,3],[-9,7],[-13,6]],'#99633e');
      line([[15,-13],[20,-8],[22,0],[20,8],[15,13]],'#182d38',3.5);
      line([[15,-13],[20,-8],[22,0],[20,8],[15,13]],'#91b7c3',1.3);
      line([[15,-13],[1,0],[15,13]],'#e2d8b6',.8);
      line([[-1,0],[26,0]],'#e4e9d8',1.4);shape([[28,0],[23,-2],[23,2]],'#c6e4ee');
    }else if(weapon==='spearGun'){
      shape([[-10,-2],[20,-2],[22,1],[15,3],[-3,3],[-5,8],[-9,7],[-7,2]],'#257b87');
      line([[-9,-4],[31,-4]],'#d9eef0',1.5);shape([[34,-4],[28,-7],[29,-3]],'#edf8f4');
      line([[20,-2],[2,-6],[-3,-2],[20,1]],'#d5ae53',1.4);
      ctx.strokeStyle='#8bcad0';ctx.lineWidth=1;ctx.beginPath();ctx.arc(5,6,3,0,Math.PI*2);ctx.stroke();
    }else if(['rifle','ar15','machineGun','pistol'].includes(weapon)){
      const pistol=weapon==='pistol',wood=weapon==='rifle';
      if(!pistol)shape([[-15,-3],[-4,-2],[1,1],[-6,4],[-15,6]],wood?'#986342':'#4d6167');
      shape([[-5,-3],[pistol?12:16,-3],[pistol?12:16,1],[3,2],[1,8],[-4,7],[-2,1]],'#43575e');
      if(!pistol){line([[13,-1],[29,-1]],'#1b2a32',3);line([[15,-2],[28,-2]],'#abc0c7',.9);shape([[3,2],[8,2],[7,9],[3,8]],'#293d45');}
      if(wood){shape([[-1,-7],[11,-7],[11,-4],[-1,-4]],'#253d48');line([[1,-7],[9,-7]],'#9ccbd6');line([[8,0],[17,0]],'#be8759',2);}
      else line([[-3,-4],[pistol?10:15,-4]],'#a5bec8',1);
      line([[-2,3],[2,3],[1,5],[-2,5]],'#c4b997',.8);
    }else if(['knife','sword'].includes(weapon)){
      shape([[0,-2],[weapon==='knife'?15:27,-1],[weapon==='knife'?19:32,0],[12,3],[0,2]],'#ccdde4');line([[-7,0],[0,0]],'#986342',3);line([[0,-4],[0,4]],'#d8bc72',2);
    }else {line([[-6,0],[15,0]],'#20363e',4);line([[-6,-1],[15,-1]],'#bfccd0',1.5);}
    if(hands){for(const x of [ -3,...(weapon==='pistol'||weapon==='knife'?[]:[11])]){ctx.fillStyle='#e8bd8c';ctx.strokeStyle='#674b39';ctx.lineWidth=.7;ctx.beginPath();ctx.ellipse(x,3,2.7,2.2,0,0,Math.PI*2);ctx.fill();ctx.stroke();}}
    ctx.restore();
  }
  return {draw};
})();
if(typeof module!=='undefined')module.exports=HeldWeapons;
