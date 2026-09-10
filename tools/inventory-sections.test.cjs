const test=require('node:test');
const assert=require('node:assert/strict');
const {section}=require('../src/AlternateEarth.Client2D/inventory-sections.js');
const vehicles=new Set(['bike','eBike','skateboard','motorcycle','dirtBike','inflatableRaft','ufo','swimmies']);
const ammo=new Set(['bullet','arrow','rock']);
const weapons=new Set(['pistol','rock']);
const quests=new Set(['questToken']);
const classify=(itemType,definition={},category='other')=>section({itemType,category},definition,vehicles,ammo,weapons,quests);
test('vehicles stay out of backpack categories even if metadata says crafting',()=>{
  for(const vehicle of vehicles)assert.equal(classify(vehicle,{storageSection:'crafting'}),'vehicle');
});
test('clothing, food and water, crafting, and Other are separate categories',()=>{
  assert.equal(classify('quickdrawGloves',{storageSection:'gloves'}),'clothing');
  assert.equal(classify('water'),'food');assert.equal(classify('apple',{nutrition:{hunger:5}}),'food');
  assert.equal(classify('wood',{storageSection:'crafting'}),'crafting');
  assert.equal(classify('recipe:gunpowder'),'crafting');assert.equal(classify('calculator'),'misc');
});
test('ammo and quest identity take precedence over ingredients and nutrition',()=>{
  assert.equal(classify('rock',{storageSection:'crafting'}),'ammo');
  assert.equal(classify('quest:food:1',{nutrition:{hunger:10}}),'quest');
  assert.equal(classify('questToken'),'quest');assert.equal(classify('pistol'),'weapon');
});

test('equipment slots separate all clothing and offhand items',()=>{
 for(const slot of ['hat','shirt','pants','shoes','gloves','offhand']){
  assert.equal(section({itemType:'example'},{},vehicles,ammo,weapons,quests,{example:slot}),slot==='offhand'?'offhand':'clothing');
 }
});
