const test=require('node:test'),assert=require('node:assert/strict');
const survival=require('../src/AlternateEarth.Client2D/survival.js');
test('Musical Fruit displays its countdown, twenty-second finale, then expires',()=>{
 const player={survival:{musicalFruit:{endsAtUtc:new Date(300000).toISOString()}}};
 assert.match(survival.status(player,0).buffs,/Musical Fruit · 5:00/);
 assert.match(survival.status(player,299000).buffs,/0:01/);
 assert.match(survival.status(player,300000).buffs,/Grand toot finale \(20s\)/);
 assert.match(survival.status(player,319000).buffs,/\(1s\)/);
 assert.equal(survival.status(player,320000).buffs,'');
 assert.equal(survival.status({},0).buffs,'');
});
