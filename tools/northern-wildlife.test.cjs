const test=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const sandbox={};vm.runInNewContext(fs.readFileSync('src/AlternateEarth.Client2D/northern-exposure.js','utf8'),sandbox);
test('wildlife renders at near and far zoom facing either way with balanced canvas state',()=>{
 for(const subtype of ['angryMoose','helmetBeaver','tacticalGoose'])for(const scale of [1,28])for(const facing of ['east','west']){
  let depth=0,shapes=0,labels=[];
  const ctx=new Proxy({save(){depth++;},restore(){depth--;assert.ok(depth>=0);},fill(){shapes++;},fillRect(){shapes++;},fillText(text){labels.push(text);}},{get(target,key){return target[key]??(()=>{});}});
  assert.equal(sandbox.NorthernExposure.drawCanadian(ctx,{subtype,name:subtype,position:{x:0,y:0},facing,isMoving:true,healthHearts:8,maximumHealthHearts:10},1200,{state:{scale},toScreen:p=>p}),true);
  assert.equal(depth,0);assert.ok(shapes>5);assert.ok(labels.includes(subtype));
 }
});
test('wildlife renderer leaves ordinary animals to the existing renderer',()=>{
 assert.equal(sandbox.NorthernExposure.drawWildlife({}, {subtype:'rabbit'},0,{}),false);
});
