const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const recipes=require('../src/AlternateEarth.Client2D/recipe-book.js'),workshop=require('../src/AlternateEarth.Client2D/home-workshop.js'),garage=require('../src/AlternateEarth.Client2D/garage.js');
function harness(file){
  const element=()=>({children:[],listeners:{},append(...children){this.children.push(...children);},replaceChildren(){this.children=[];},addEventListener(type,fn){this.listeners[type]=fn;}});
  const context=vm.createContext({document:{createElement:element},module:{exports:{}}});
  vm.runInContext(fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/'+file),'utf8'),context);return{api:context.module.exports,element};
}
test('recipe tabs filter every category and show exact server success and skill requirements',()=>{
  assert.deepEqual(recipes.tabs,['Food/Water','Clothing','Weapons','Ammo','Vehicles','Misc']);
  const entries=recipes.tabs.map((category,i)=>({category,name:String(i),level:i?2:0,successChance:.4375,requiredCraftingLevel:10}));
  for(const category of recipes.tabs)assert.equal(recipes.rows(entries,category).length,1);
  assert.match(recipes.summary(entries[0],1),/level 0.*Not learned/);
  assert.match(recipes.summary(entries[1],1),/43.75% success.*Requires crafting level 10/);
});
test('recipe rows allow one-copy study only when copies are available',()=>{
  const {api,element}=harness('recipe-book.js'),container=element(),studied=[];
  api.render(container,[{id:'water',category:'Food/Water',name:'Water',level:1,successChance:.5,availableCopies:2},{id:'purifiedWater',category:'Food/Water',name:'Purified water',level:0,availableCopies:0}], 'Food/Water',1,element,id=>studied.push(id));
  assert.equal(container.children.length,2);
  const available=container.children[0].children[2],missing=container.children[1].children[2];
  assert.equal(missing.disabled,true);assert.equal(available.textContent,'Study (2)');available.listeners.click();
  assert.deepEqual(studied,['water']);assert.equal(available.disabled,true);
});
test('installed upgrades cannot be repurchased and purifier controls follow remaining uses',()=>{
  const {api,element}=harness('home-workshop.js'),container=element(),actions=[];
  const model={catalog:[{id:'waterPurifier',name:'Water purifier',stationType:'stove',priceCents:35000}],progress:{installed:['waterPurifier'],filterUsesRemaining:0},storedFilters:1};
  api.render(container,model,{walletCents:1e6},true,id=>actions.push(id),replace=>actions.push(replace));
  assert.equal(container.children[1].children[1].disabled,true);
  const controls=container.children[2];assert.equal(controls.children[1].disabled,true);assert.equal(controls.children[2].disabled,false);controls.children[2].listeners.click();assert.deepEqual(actions,[true]);
  model.progress.filterUsesRemaining=50;api.render(container,model,{walletCents:1e6},true,()=>{},()=>{});
  assert.equal(container.children[2].children[1].disabled,false);assert.equal(container.children[2].children[2].disabled,true);
});
test('ingredient quality and permanent upgrade adjustments are explained separately',()=>{
  assert.match(workshop.bonusSummary({success:.12,ingredientQuality:-.15,quantity:.23,materials:0}),/12.0 points.*-15.0 points.*23%/);
  assert.match(workshop.effects({stationType:'garageWorkbench',successBonus:.08,materialSavingChance:.1}),/8 percentage points.*10% chance to save/);
});
test('garage vehicle slots include scuba gear, stay inside the connected room, and exclude empty stacks',()=>{
  const room={x:32,y:4,width:18,height:12},vehicles=['bike','eBike','skateboard','motorcycle','dirtBike','inflatableRaft','ufo','swimmies'].map(itemType=>({itemType,quantity:1}));
  vehicles.push({itemType:'scubaGear',quantity:1});const slots=garage.slots({room,vehicles:[...vehicles,{itemType:'empty',quantity:0}]});assert.equal(slots.length,9);
  assert.equal(new Set(slots.map(v=>`${v.x},${v.y}`)).size,9);
  for(const p of slots){assert.ok(p.x>room.x&&p.x<room.x+room.width);assert.ok(p.y>room.y&&p.y<room.y+room.height);}
});


test('recipe book reports output quantities and batch counts from combined supplies',()=>{
 assert.match(recipes.summary({level:1,successChance:.5,maximumCraftable:3,outputQuantity:12},1),/Can make 36.*3 batches/);
 assert.match(recipes.summary({level:1,successChance:.5,maximumCraftable:0,outputQuantity:1},1),/Can make 0/);
});
