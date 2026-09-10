const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function fn(name){const start=source.indexOf(`  function ${name}(`),end=source.indexOf('\n  function ',start+1);return source.slice(start,end);}
test('water fills and clips island cutouts with even-odd paths',()=>{
  const moves=[],fills=[],clips=[];
  class Path2D{moveTo(x,y){moves.push([x,y]);}lineTo(){}closePath(){}}
  const ctx={translate(){},fill(path,rule){fills.push(rule);},stroke(){},save(){},restore(){},clip(path,rule){clips.push(rule);}};
  const draw=new Function('ctx','toScreen','state','Path2D',`${fn('waterOutline')}\n${fn('waterGeometryClosed')}\n${fn('drawWater')}\nreturn drawWater;`)(ctx,p=>p,{scale:1},Path2D);
  const rectangle=(a,b)=>[{x:a,y:a},{x:b,y:a},{x:b,y:b},{x:a,y:b},{x:a,y:a}];
  draw({geometry:rectangle(-100,100),interiorRings:[rectangle(-10,10)]},1,0);
  assert.deepEqual(fills,['evenodd']);assert.deepEqual(clips,['evenodd']);
  assert.deepEqual(moves,[[0,0],[90,90]]);
});

test('shoreline paint ends in shallow water without a dark band against the sand',()=>{
  for(const scale of [1,8,30])for(const detail of [0,1]){
    const paints=[];
    class Path2D{moveTo(){}lineTo(){}closePath(){}}
    const ctx={translate(){},save(){},restore(){},clip(){},
      fill(){paints.push({kind:'fill',color:this.fillStyle});},
      stroke(){paints.push({kind:'stroke',color:this.strokeStyle,width:this.lineWidth});}};
    const draw=new Function('ctx','toScreen','state','Path2D',`${fn('waterOutline')}\n${fn('waterGeometryClosed')}\n${fn('drawWater')}\nreturn drawWater;`)(ctx,p=>p,{scale},Path2D);
    draw({geometry:[{x:0,y:0},{x:100,y:0},{x:100,y:100},{x:0,y:100},{x:0,y:0}]},detail,0);
    assert.deepEqual(paints.map(p=>p.color),['#d3b86e','#073550','#55b7bd']);
    assert.ok(paints.at(-1).width>=scale*6,'Shallow water reaches the sand boundary');
  }
});
test('large river path survives camera movement and is rebuilt when projection or geography changes',()=>{
  let projections=0,paths=0;
  class Path2D{constructor(){paths++;}moveTo(){}lineTo(){}closePath(){}}
  const state={scale:1,camera:{x:0,y:0}},entity={geometry:[{x:0,y:0},{x:20,y:0},{x:20,y:20},{x:0,y:0}]};
  const outline=new Function('state','Path2D','toScreen',`${fn('waterOutline')};return waterOutline;`)(state,Path2D,p=>{projections++;return{x:p.x-state.camera.x,y:p.y-state.camera.y};});
  const first=outline(entity);state.camera.x=100;const moved=outline(entity);
  assert.equal(moved.path,first.path);assert.equal(moved.anchor.x,-100);assert.equal(projections,6);assert.equal(paths,1);
  for(const [key,value] of [['scale',2],['pitch',.8],['shear',.1],['elevationGrid',{}]]){state[key]=value;outline(entity);}
  entity.interiorRings=[entity.geometry];outline(entity);assert.equal(paths,6);
});
test('open rivers use their imported width without filling a polygon',()=>{
  const draws=[];
  const draw=new Function('ctx','state','drawGeometry','prop',`${fn('waterGeometryClosed')}\n${fn('drawWater')}\nreturn drawWater;`)({}, {scale:2},(...args)=>draws.push(args),(e,k,f)=>Number(e.properties?.[k]??f));
  draw({geometry:[{x:0,y:0},{x:100,y:0}],properties:{width:'80'}},1,0);
  assert.equal(draws[0][3],172);assert.equal(draws[0][2],'#d3b86e');assert.equal(draws[1][3],160);assert.ok(draws.every(d=>d[1]===null));
});
