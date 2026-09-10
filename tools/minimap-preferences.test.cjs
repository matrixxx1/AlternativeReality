const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const MapMarkers=require('../src/AlternateEarth.Client2D/map-markers.js');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function implementation(name){const start=source.indexOf(`  function ${name}(`);return source.slice(start,source.indexOf('\n  function ',start+1));}
test('minimap categories and individual quest choices survive reload',()=>{
 let saved=null;const storage={getItem:()=>saved,setItem:(key,value)=>saved=value},prefs=MapMarkers.preferences(storage);
 for(const category of ['You','Store','Player','Grave','Flag','Home'])assert.equal(MapMarkers.categoryVisible(prefs,category),true);
 prefs.categories.store=false;prefs.categories.you=false;prefs.quests.a=false;MapMarkers.savePreferences(storage,prefs);
 const restored=MapMarkers.preferences(storage);assert.equal(MapMarkers.categoryVisible(restored,'Store'),false);assert.equal(MapMarkers.categoryVisible(restored,'Home'),true);
 assert.equal(MapMarkers.questVisible(restored,'a'),false);assert.equal(MapMarkers.questVisible(restored,'b'),true);
 saved='bad json';assert.equal(MapMarkers.questVisible(MapMarkers.preferences(storage),'a'),true);
});
test('unchecked categories and quests disappear from both rendered markers and hit targets',()=>{
 const me={id:'me',name:'Me',locationId:'outdoor',position:{x:0,y:0}};
 const state={lastMiniMapDraw:-Infinity,playerId:'me',mapPreferences:{categories:{},quests:{}},miniMapStores:[{id:'store',position:{x:50,y:0}}],
  privateState:{base:{position:{x:10,y:0}},quests:[{id:'q',title:'Quest',status:'active'}]},actors:new Map(),players:new Map([['me',me],['other',{id:'other',position:{x:20,y:0}}]]),
  graves:new Map([['grave',{position:{x:30,y:0}}]]),reality:new Map([['flag',{position:{x:40,y:0},properties:{objectType:'personalFlag',owner:'me'}}]])};
 const ctx=new Proxy({},{get:(o,k)=>o[k]||(()=>{}),set:(o,k,v)=>(o[k]=v,true)});
 const c=vm.createContext({state,MapMarkers,miniMapCtx:ctx,miniMapCanvas:{width:372,height:220},performance:{now:()=>1000},ui:{miniMapTeleportHome:{}},Inversions:{insideSmug:()=>false},title:s=>s,questStage:()=>({position:{x:60,y:0},location:'outdoor',name:'Target'}),updateMiniMapTooltip:()=>{}});
 vm.runInContext(implementation('drawMiniMap'),c);c.drawMiniMap(me);assert.equal(state.miniMapMarkers.length,7);
 for(const category of ['You','Store','Player','Grave','Flag','Home']){
  state.mapPreferences.categories[category.toLowerCase()]=false;state.lastMiniMapDraw=-Infinity;c.drawMiniMap(me);
  assert.ok(!state.miniMapMarkers.some(marker=>marker.type===category));
 }
 state.mapPreferences.quests.q=false;state.lastMiniMapDraw=-Infinity;c.drawMiniMap(me);assert.equal(state.miniMapMarkers.length,0);
 state.mapPreferences.quests.q=true;state.lastMiniMapDraw=-Infinity;c.drawMiniMap(me);assert.equal(state.miniMapMarkers[0].type,'Quest objective');
});
test('action menu opens above the first panel and scrolls the rail to the top',()=>{
 const first={},menu={hidden:true,classList:{contains:()=>false}},rail={children:[{matches:()=>false},first],scrollTop:900};first.matches=()=>true;
 const c=vm.createContext({ui:{actionMenu:menu,rightRail:rail},updateActionMenu(){},dockPanel(panel,persist,before){assert.equal(panel,menu);assert.equal(before,first);},setRightRailCollapsed(){},requestAnimationFrame:fn=>fn()});
 menu.scrollIntoView=()=>{};vm.runInContext(implementation('showActionMenuAt'),c);c.showActionMenuAt();assert.equal(menu.hidden,false);assert.equal(rail.scrollTop,0);
});
