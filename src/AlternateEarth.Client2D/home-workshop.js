(function(root){
  const stationNames={stove:'Kitchen',weaponsBench:'Weapons and ammo',garageWorkbench:'Vehicles',craftingTable:'Crafting table'};
  function effects(upgrade){
    const result=[];
    if(upgrade.successBonus)result.push(`+${(upgrade.successBonus*100).toFixed(0)} percentage points success`);
    if(upgrade.bonusQuantityChance)result.push(`${(upgrade.bonusQuantityChance*100).toFixed(0)}% chance of +1 ${upgrade.stationType==='stove'?'food/water item':'ammo'}`);
    if(upgrade.materialSavingChance)result.push(`${(upgrade.materialSavingChance*100).toFixed(0)}% chance to save ingredients`);
    if(upgrade.id==='waterPurifier')result.push('Dispenses purified water into Home storage · includes a 50-use filter');
    return result.join(' · ');
  }
  function bonusSummary(b){
    if(!b)return '';
    return `Home upgrades +${((b.success||0)*100).toFixed(1)} points · Ingredient quality ${b.ingredientQuality>=0?'+':''}${((b.ingredientQuality||0)*100).toFixed(1)} points`+
      (b.quantity?` · ${(b.quantity*100).toFixed(0)}% chance of +1 output`:'')+(b.materials?` · ${(b.materials*100).toFixed(0)}% chance to save ingredients`:'');
  }
  function render(container,workshop,player,canEdit,buy,use){
    container.replaceChildren();if(!workshop){container.textContent='Your Home upgrades will appear here.';return;}
    for(const station of ['stove','weaponsBench','garageWorkbench']){
      const heading=document.createElement('h3');heading.textContent=stationNames[station];container.append(heading);
      for(const upgrade of workshop.catalog.filter(u=>u.stationType===station)){
        const row=document.createElement('div'),label=document.createElement('div'),name=document.createElement('strong'),detail=document.createElement('small'),button=document.createElement('button');
        const installed=workshop.progress.installed.includes(upgrade.id);row.className='home-upgrade-row';name.textContent=upgrade.name;detail.textContent=effects(upgrade);label.append(name,detail);
        button.type='button';button.textContent=installed?'Installed':`Buy · $${(upgrade.priceCents/100).toLocaleString()}`;button.disabled=installed||!canEdit||(!player?.godMode&&(player?.walletCents||0)<upgrade.priceCents);
        button.addEventListener('click',()=>{button.disabled=true;buy(upgrade.id);});row.append(label,button);container.append(row);
        if(upgrade.id==='waterPurifier'&&installed){
          const controls=document.createElement('div'),count=document.createElement('span'),dispense=document.createElement('button'),replace=document.createElement('button');controls.className='purifier-controls';count.textContent=`Filter: ${workshop.progress.filterUsesRemaining} / 50 uses remaining · ${workshop.storedFilters} spare(s) in Home storage`;
          dispense.type=replace.type='button';dispense.textContent='Dispense purified water';replace.textContent='Replace filter';dispense.disabled=!canEdit||workshop.progress.filterUsesRemaining<1;replace.disabled=!canEdit||workshop.progress.filterUsesRemaining>0||workshop.storedFilters<1;
          dispense.addEventListener('click',()=>{dispense.disabled=true;use(false);});replace.addEventListener('click',()=>{replace.disabled=true;use(true);});controls.append(count,dispense,replace);container.append(controls);
        }
      }
    }
  }
  const api={effects,bonusSummary,render,stationNames};if(typeof module!=='undefined')module.exports=api;else root.HomeWorkshop=api;
})(typeof window!=='undefined'?window:globalThis);
