const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
function fixture(){
 const nodes=new Map(),sent=[],captures=[];function element(tag='div'){return {tag,width:800,height:600,getContext(){return {drawImage(...args){captures.push(args);}};},toDataURL(){return 'data:image/png;base64,test-frame';},children:[],dataset:{},style:{},listeners:{},append(...items){this.children.push(...items);},replaceChildren(){this.children=[];this.rebuilt=(this.rebuilt||0)+1;},addEventListener(name,fn){this.listeners[name]=fn;},setAttribute(){},getBoundingClientRect(){return {left:0,top:0};}};}
 const document={createElement:element,getElementById(id){if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);},querySelectorAll(){return [];}};
 const state={playerId:'p',players:new Map([['p',{id:'p',name:'Me',position:{x:0,y:0},air:10}]]),actors:new Map(),scale:10,privateState:{loot:[],quests:[]},inversions:{vote:{endsAtUtc:new Date(Date.now()+60000).toISOString(),round:1,options:[{id:'random',name:'Random',voters:['Me']},{id:'cards',name:'Deal With It!',voters:[]}]},queued:[]}};
 const c=vm.createContext({document,Date,Math,Map,Set});vm.runInContext(fs.readFileSync('src/AlternateEarth.Client2D/inversions.js','utf8'),c);c.Inversions.init({state,send:r=>sent.push(r),toScreen:p=>p,navigateTo:p=>{}});
 return {api:c.Inversions,state,sent,nodes,captures};
}
test('vote buttons send replacement ballots and unchanged updates preserve focusable controls',()=>{
 const {api,state,sent,nodes}=fixture();api.tick();const panel=nodes.get('serverVotePanel'),row=panel.children.find(e=>e.className==='vote-option');row.children[1].listeners.click();assert.equal(sent[0].type,'castServerVote');assert.equal(sent[0].option,'random');const first=panel.rebuilt;
 state.inversions=JSON.parse(JSON.stringify(state.inversions));api.tick();assert.equal(panel.rebuilt,first);
 state.inversions.vote.options[1].voters=['Me'];state.inversions.vote.options[0].voters=[];api.tick();assert.equal(panel.rebuilt,first+1);
});
test('God mode cancel vote appears immediately on enabling and sends an administrative action',()=>{
 const {api,state,sent,nodes}=fixture();api.tick();const panel=nodes.get('serverVotePanel');
 const cancel=()=>panel.children.find(e=>e.tag==='button'&&e.textContent==='God mode: cancel vote');
 assert.equal(cancel(),undefined);
 state.players.get('p').godMode=true;api.tick();assert.ok(cancel());cancel().listeners.click();assert.equal(sent.at(-1).type,'cancelServerVote');
 assert.equal(panel.children.filter(e=>e.className==='vote-option').length,2);
 state.players.get('p').godMode=false;api.tick();assert.equal(cancel(),undefined);
 state.players.get('p').godMode=true;state.inversions.vote=null;api.tick();assert.equal(cancel(),undefined);assert.equal(panel.hidden,true);
});
test('God mode cancel control is also available during a tie runoff',()=>{
 const {api,state,nodes}=fixture();state.players.get('p').godMode=true;state.inversions.vote.round=2;api.tick();
 assert.ok(nodes.get('serverVotePanel').children.some(e=>e.textContent==='God mode: cancel vote'));
});
test('private prizes open once and card canvas uses server-authoritative actions',()=>{
 const {api,state,sent}=fixture();state.privateState.loot=[{id:'private-prize',dropKind:'eventReward'}];api.tick();api.tick();assert.equal(sent.filter(r=>r.type==='openLoot').length,1);
 state.dungeon={eventBattle:{mode:'cards',turn:0,dungeonNumber:1,enemyName:'Boss',enemyHealth:24,maximumEnemyHealth:24,hand:['Strike','Guard','Second Wind']}};
 const ctx=new Proxy({}, {get:(t,k)=>t[k]||(()=>{}),set:(t,k,v)=>(t[k]=v,true)});assert.equal(api.battleScene(ctx,800,600,100),true);
 api.canvasClick(238,510);assert.equal(sent.at(-1).type,'eventBattleAction');assert.equal(sent.at(-1).action,'Strike');assert.equal(state.dungeon.eventBattle.enemyHealth,24);
});
test('water crossings and barriers render, with barrier clicks using their IDs',()=>{
 const {api,state,sent}=fixture();const ctx=new Proxy({}, {get:(t,k)=>t[k]||(()=>{}),set:(t,k,v)=>(t[k]=v,true)});
 state.dungeon={waterAreas:[{x:0,y:5,width:10,height:2,deep:true}],barriers:[{id:'b',x:2,y:2,width:2,height:1,kind:'blast'}]};api.dungeon(ctx,state.dungeon);assert.equal(api.click({x:3,y:2.5}),true);assert.equal(sent.at(-1).barrierId,'b');
});

test('camera captures a bounded preview and identifies the selected nearby event subject',()=>{
 const {api,state,sent,nodes,captures}=fixture();state.players.get('p').equippedWeapon='camera';state.actors.set('robot',{id:'robot',eventName:'Mechanized Warrior',position:{x:2,y:0}});
 api.tick();nodes.get('adventurePanel').children.find(e=>e.tag==='button'&&e.textContent.startsWith('Photograph')).listeners.click();
 assert.equal(sent.at(-1).type,'photograph');assert.equal(sent.at(-1).subjectId,'robot');assert.match(sent.at(-1).thumbnailDataUrl,/^data:image\/png;base64,/);assert.equal(captures[0].at(-2),128);assert.equal(captures[0].at(-1),96);
});
test('photo quest progress follows distinct carried prints and drops when one is sold',()=>{
 const {api,state,nodes}=fixture();state.privateState.quests=[{id:'q',kind:'adventure:photo',title:'Photos',description:'Bring prints',status:'active',progress:0}];
 state.privateState.inventory={items:[{quantity:1,photograph:{kind:'event',evidence:'a'}},{quantity:1,photograph:{kind:'event',evidence:'a'}},{quantity:1,photograph:{kind:'event',evidence:'b'}}]};
 api.tick();const panel=nodes.get('adventurePanel');assert.match(panel.children.find(e=>e.tag==='small').textContent,/2 \/ 3/);
 state.privateState.inventory.items.pop();api.tick();assert.match(panel.children.find(e=>e.tag==='small').textContent,/1 \/ 3/);
 panel.children.find(e=>e.tag==='button'&&e.textContent==='Submit photographs').listeners.click();
});
