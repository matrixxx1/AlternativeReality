const InventorySections = (() => {
  function section(item, definition, vehicles, ammo, weapons, questItems) {
    const type=item?.itemType || '', category=String(item?.category || '').toLowerCase();
    if(vehicles.has(type)) return 'vehicle';
    if(ammo.has(type)) return 'ammo';
    if(weapons.has(type) || category==='weapon') return 'weapon';
    if(type.startsWith('quest:') || category==='quest' || questItems.has(type)) return 'quest';
    if(['gloves','food','crafting'].includes(definition?.storageSection)) return definition.storageSection;
    if(definition?.nutrition || ['food','water','dirtyWater','purifiedWater','energyDrink','mapleSyrup'].includes(type)) return 'food';
    if(type.startsWith('recipe:')) return 'crafting';
    return 'misc';
  }
  return {section};
})();
if(typeof module!=='undefined')module.exports=InventorySections;
