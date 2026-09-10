(function(root){
  const tabs=['Food/Water','Clothing','Weapons','Ammo','Vehicles','Misc'];
  function rows(entries,tab){return (entries||[]).filter(entry=>entry.category===tab).sort((a,b)=>Number(b.level>0)-Number(a.level>0)||Number(b.availableCopies>0)-Number(a.availableCopies>0)||a.name.localeCompare(b.name));}
  function summary(entry,skillLevel){
    if(entry.id?.startsWith('cook')&&entry.outputItemType?.startsWith('cooked'))return `Basic stove cooking · No recipe needed · 100% success · Can make ${entry.maximumCraftable||0}`;
    if(!entry.level)return 'Recipe level 0 · Not learned · 0% success · Can make 0';
    const chance=`${(entry.successChance*100).toFixed(2)}% success`;
    return `Recipe level ${entry.level} · ${chance} · Can make ${(entry.maximumCraftable||0)*(entry.outputQuantity||1)}${entry.outputQuantity>1?` (${entry.maximumCraftable||0} batches)`:""}${skillLevel<entry.requiredCraftingLevel?` · Requires crafting level ${entry.requiredCraftingLevel}`:''}`;
  }
  function render(container,entries,tab,skillLevel,art,study){
    container.replaceChildren();
    for(const entry of rows(entries,tab)){
      const row=document.createElement('div'),label=document.createElement('div'),name=document.createElement('strong'),detail=document.createElement('small'),button=document.createElement('button');
      row.className='recipe-book-row';name.textContent=entry.name;detail.textContent=summary(entry,skillLevel);
      detail.title=typeof HomeWorkshop!=='undefined'?HomeWorkshop.bonusSummary(entry.bonuses):'';label.append(name,detail);button.type='button';button.disabled=entry.availableCopies<1;
      button.hidden=entry.id?.startsWith('cook')&&entry.outputItemType?.startsWith('cooked');
      button.textContent=entry.availableCopies?`Study (${entry.availableCopies})`:'No copies';
      button.title=entry.availableCopies?'Consume one recipe copy from your backpack to raise this recipe level.':'Collect a recipe copy to study it.';
      button.addEventListener('click',()=>{button.disabled=true;study(entry.id);});
      row.append(art(entry.outputItemType),label,button);container.append(row);
    }
    if(!container.children.length)container.textContent='No recipes in this category.';
  }
  const api={tabs,rows,summary,render};if(typeof module!=='undefined')module.exports=api;else root.RecipeBook=api;
})(typeof window!=='undefined'?window:globalThis);
