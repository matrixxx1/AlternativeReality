const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function harness(){
  function element(){return {children:[],listeners:{},hidden:false,append(...items){this.children.push(...items);},replaceChildren(){this.children=[];},setAttribute(){},addEventListener(name,fn){this.listeners[name]=fn;}};}
  const nodes=new Map(),get=id=>{if(!nodes.has(id))nodes.set(id,element());return nodes.get(id);};
  const state={privateState:{progression:{level:2,experience:100,earnedTowardNextLevel:0,requiredForNextLevel:200,availablePoints:1,carryingCapacity:50,maximumStamina:10,experienceMultiplier:1,alignment:-.25,alignmentFirstEncounterBonus:-.00075,stats:{strength:1,perception:1,endurance:1,charisma:1,intelligence:1,agility:1,luck:1}}}};
  const c=vm.createContext({state,$:get,document:{createElement:element},title:s=>s[0].toUpperCase()+s.slice(1)});
  vm.runInContext(source.slice(source.indexOf('  const characterStatDescriptions='),source.indexOf("  $('#openProgression').addEventListener")),c);
  return {c,state,get};
}
test('stat allocation enforces the budget, permits zero, and preserves unsaved edits on XP refresh',()=>{
  const {c,state,get}=harness();c.renderProgression();assert.equal(get('#openProgression').textContent,'Level up · 1 point(s)');
  let row=get('#statAllocation').children[0];row.children[3].listeners.click();assert.equal(state.statDraft.strength,2);
  assert.equal(get('#statPointsRemaining').textContent,'0 point(s) available');assert.equal(get('#statAllocation').children[1].children[3].disabled,true);
  state.privateState.progression.experience=101;c.renderProgression();assert.equal(state.statDraft.strength,2);
  row=get('#statAllocation').children[0];row.children[1].listeners.click();get('#statAllocation').children[0].children[1].listeners.click();
  assert.equal(state.statDraft.strength,0);assert.equal(get('#statAllocation').children[0].children[1].disabled,true);
  assert.equal(get('#statPointsRemaining').textContent,'2 point(s) available');
});
test('alignment displays tiny changes and completed dungeons show their reward status',()=>{
  const {c,state,get}=harness();state.dungeon={isCompleted:true};c.renderProgression();
  assert.equal(get('#alignmentValue').textContent,'Evil 0.25');assert.equal(get('#dungeonComplete').hidden,false);
  state.privateState.progression.alignment=.25;c.renderProgression();assert.equal(get('#alignmentValue').textContent,'Good 0.25');
});
test('crafting table explosions render immediately and fully expire',()=>{
  const effects=require('../src/AlternateEarth.Client2D/combat-effects.js');
  const shot=effects.createShot({weapon:'craftingExplosion',start:{x:5,y:5},end:{x:5,y:5},hit:true},100,'home');
  assert.ok(shot);assert.equal(effects.phase(shot,100).name,'impact');assert.equal(effects.phase(shot,1900).name,'done');
});
