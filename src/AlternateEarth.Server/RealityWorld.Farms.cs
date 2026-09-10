using AlternateEarth.Shared;
using System.Globalization;
namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private DateTimeOffset _nextFarmTick;
    private CanonicalEntity FarmAnimal(string id)=>_baseEntities.TryGetValue(id,out var e)&&FarmRules.IsAnimal(e)?e:throw new InvalidOperationException("That farm animal is not available.");
    private static int FarmProducts(CanonicalEntity e)=>int.TryParse(e.Properties.GetValueOrDefault("productQuantity"),out var n)?Math.Clamp(n,0,FarmRules.MaximumProducts):0;
    public async Task<IReadOnlyList<CanonicalEntity>> AdvanceFarmsAsync(CancellationToken token=default)
    {
        var now=_probulatorClock.GetUtcNow();if(now<_nextFarmTick)return [];_nextFarmTick=now.AddSeconds(5);
        var changed=new List<CanonicalEntity>();await _treasureInteractionLock.WaitAsync(token);
        try
        {
            foreach(var animal in _baseEntities.Values.Where(FarmRules.IsAnimal).ToArray())
            {
                if(animal.Properties.GetValueOrDefault("state")=="dead"||FarmProducts(animal)>=FarmRules.MaximumProducts||!_players.Values.Any(p=>p.LocationId=="outdoor"&&p.HealthHearts>0&&p.Position.Distance2D(animal.Position)<=FarmRules.NearbyRange))continue;
                if(DateTimeOffset.TryParse(animal.Properties.GetValueOrDefault("nextProductionUtc"),out var next)&&next>now)continue;
                var amount=FarmProducts(animal)+(ProgressionRoll()<FarmRules.ProductionChance?1:0);
                var updated=animal with {Version=animal.Version+1,Properties=new Dictionary<string,string>(animal.Properties){["productQuantity"]=amount.ToString(CultureInfo.InvariantCulture),["nextProductionUtc"]=now.AddMinutes(1).ToString("O")}};
                await _store.SaveEntityAsync(Configuration.Id,updated,token);_baseEntities[animal.Id]=updated;changed.Add(updated);
            }
        }
        finally{_treasureInteractionLock.Release();}
        return changed;
    }
    private async Task<WorldCrimeResult> AttackFarmAnimalAsync(string id,string entityId,CancellationToken token)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            var player=GardenPlayer(id);var animal=FarmAnimal(entityId);if(animal.Properties.GetValueOrDefault("state")=="dead")throw new InvalidOperationException("That animal is already dead. Collect its raw meat.");
            var weapon=player.EquippedWeapon;var definition=InventoryDefinition(weapon);if(weapon=="none"||(!player.GodMode&&!OwnsWeapon(id,weapon)))throw new InvalidOperationException("Equip an owned weapon.");
            if(player.Position.Distance2D(animal.Position)>definition.RangeMeters)throw new InvalidOperationException("That animal is out of weapon range.");
            var now=_probulatorClock.GetUtcNow();if(_lastPlayerAttack.TryGetValue((id,weapon),out var prior)&&now-prior<TimeSpan.FromSeconds(Math.Clamp(definition.AttackIntervalSeconds,.05,10)))throw new InvalidOperationException("Your weapon is not ready yet.");
            var inventory=GetInventoryState(id);var ammo=WeaponDefinition(weapon).Ammo;
            if(!player.GodMode&&ammo is not null){if(!inventory.Items.Any(i=>i.ItemType==ammo&&i.Quantity>0))throw new InvalidOperationException($"You need {DisplayItem(ammo)}.");inventory=inventory with{Items=inventory.Items.Select(i=>i.ItemType==ammo?i with{Quantity=i.Quantity-1}:i).Where(i=>i.Quantity>0).ToArray()};}
            if(!player.GodMode&&weapon=="flamethrower"){if(player.FlamethrowerGasGallons<.2)throw new InvalidOperationException("The flamethrower needs at least 0.2 gallon of gas.");player=await SaveFixturePlayerAsync(id,current=>current with{FlamethrowerGasGallons=current.FlamethrowerGasGallons-.2},null,token);}
            var damage=WeaponDamageFor(id,weapon,Math.Max(.25,definition.Damage));var health=double.TryParse(animal.Properties.GetValueOrDefault("healthHearts"),NumberStyles.Float,CultureInfo.InvariantCulture,out var h)?h:FarmRules.IsCow(animal)?8:2;
            var dead=health<=damage;var props=new Dictionary<string,string>(animal.Properties){["healthHearts"]=Math.Max(0,health-damage).ToString(CultureInfo.InvariantCulture)};
            if(dead){props["state"]="dead";props["productQuantity"]=FarmRules.IsCow(animal)?"3":"1";}
            var updated=animal with{Version=animal.Version+1,Properties=props};await _store.SaveEntityAsync(Configuration.Id,updated,token,[inventory]);_baseEntities[animal.Id]=updated;_inventories[id]=inventory.Items.ToDictionary(i=>i.ItemType,i=>i.Quantity);_lastPlayerAttack[(id,weapon)]=now;
            var message=dead?$"Animal killed. Collect its {(FarmRules.IsCow(animal)?"raw cow":"raw chicken")} meat.":"Animal wounded.";
            return new(player,GetPrivateState(id),message,updated,null,new CombatEvent(id,animal.Id,weapon,player.Position,animal.Position,true,damage,dead,message));
        }
        finally{_treasureInteractionLock.Release();}
    }
    public async Task<(GardenResult Result,ChatMessage? Remark)> UseFarmAnimalAsync(string id,string animalId,string action,CancellationToken token=default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            var player=GardenPlayer(id);var animal=FarmAnimal(animalId);if(player.Position.Distance2D(animal.Position)>FarmRules.InteractionRange)throw new InvalidOperationException("Move within 4 meters of the animal.");
            var now=_probulatorClock.GetUtcNow();var props=new Dictionary<string,string>(animal.Properties);var inventory=GetInventoryState(id);var items=inventory.Items.ToDictionary(i=>i.ItemType);string message;ChatMessage? remark=null;
            var rewards=new List<ItemStack>();
            if(action=="collect")
            {
                var quantity=FarmProducts(animal);if(quantity<1)throw new InvalidOperationException("Nothing to collect yet. Spend some time near the farm.");
                var product=animal.Properties.GetValueOrDefault("state")=="dead"?(FarmRules.IsCow(animal)?"rawCow":"rawChicken"):FarmRules.IsCow(animal)?"fertilizer":"egg";rewards.Add(InventoryStack(product,quantity));props["productQuantity"]="0";message=$"Collected {quantity} {DisplayItem(product)}.";
            }
            else if(action=="milk"&&FarmRules.IsCow(animal)&&animal.Properties.GetValueOrDefault("state")!="dead")
            {
                if(DateTimeOffset.TryParse(props.GetValueOrDefault("milkReadyUtc"),out var ready)&&ready>now)throw new InvalidOperationException("This cow needs a rest. Try milking again in a few minutes.");
                var jar=items.TryGetValue("emptyGlassJar",out var jars)&&jars.Quantity>0;var empty=jar?"emptyGlassJar":"emptyGlassBottle";
                if(!items.TryGetValue(empty,out var container)||container.Quantity<1)throw new InvalidOperationException("You need an empty glass jar or bottle in your backpack. Jars are used first.");
                if(container.Quantity==1)items.Remove(empty);else items[empty]=container with {Quantity=container.Quantity-1};
                props["milkReadyUtc"]=now.AddMinutes(5).ToString("O");var kind=jar?"jar":"bottle";
                if(ProgressionRoll()>=FarmRules.MilkSuccessChance)message=$"The cow broke your {kind}. You lost one empty {kind}.";
                else
                {
                    var contents=ProgressionRoll()<FarmRules.WrongContentsChance?(ProgressionRoll()<.5?"Fertilizer":"Urine"):"Milk";
                    rewards.Add(InventoryStack((jar?"jarOf":"bottleOf")+contents,1));message=$"You got a {kind} of {contents.ToLowerInvariant()}!";
                    if(contents!="Milk")remark=new ChatMessage("milk:"+Guid.NewGuid().ToString("N"),id,player.Name,"Well... I screwed that up!",now);
                }
            }
            else throw new InvalidOperationException("Choose Collect, or Milk for a cow.");
            foreach(var reward in rewards)items[reward.ItemType]=items.TryGetValue(reward.ItemType,out var existing)?existing with {Quantity=existing.Quantity+reward.Quantity}:reward;
            var nextInventory=inventory with {Items=items.Values.ToArray()};
            var weight=nextInventory.Items.Where(i=>i.CarriedInBackpack).Sum(i=>i.UnitWeightPounds*i.Quantity);
            if(rewards.Count>0&&!player.GodMode&&weight>PlayerCarryingCapacity(id)+.0001)throw new InvalidOperationException("Your backpack is too heavy for this. Make room and try again; your container is unchanged.");
            var updatedAnimal=animal with {Version=animal.Version+1,Properties=props};
            await _store.SaveEntityAsync(Configuration.Id,updatedAnimal,token,[nextInventory]);_inventories[id]=items.ToDictionary(i=>i.Key,i=>i.Value.Quantity);_baseEntities[animal.Id]=updatedAnimal;
            return(new(message,GetPrivateState(id),updatedAnimal),remark);
        }
        finally{_treasureInteractionLock.Release();}
    }
}
