const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`);return source.slice(start,source.indexOf('\n',start+1));}
for(const blocked of [true,false])test(`teleport requests honor restriction=${blocked} without God Mode`,()=>{
 const messages=[],tasks=[],state={privateState:{playerTesting:{cantTeleport:blocked}}};
 const context=vm.createContext({state,showToast:()=>{},pointInLoadedArea:()=>true,setWorldTask:t=>tasks.push(t),send:m=>messages.push(m),stopTravel:()=>{},ui:{miniMapTooltip:{}}});
 vm.runInContext(implementation('requestTeleport'),context);
 vm.runInContext(implementation('requestMiniMapFastTravel'),context);
 context.requestTeleport({x:20,y:30});context.requestMiniMapFastTravel('Home',{targetType:'home',targetId:'home'});
 assert.equal(messages.length,blocked?0:2);assert.equal(tasks.length,blocked?0:2);
 if(!blocked){assert.equal(messages[0].type,'teleport');assert.equal(messages[0].x,20);assert.equal(messages[1].type,'mapFastTravel');}
});
