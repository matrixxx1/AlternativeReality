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
                if(FarmProducts(animal)>=FarmRules.MaximumProducts||!_players.Values.Any(p=>p.LocationId=="outdoor"&&p.HealthHearts>0&&p.Position.Distance2D(animal.Position)<=FarmRules.NearbyRange))continue;
                if(DateTimeOffset.TryParse(animal.Properties.GetValueOrDefault("nextProductionUtc"),out var next)&&next>now)continue;
                var amount=FarmProducts(animal)+(ProgressionRoll()<FarmRules.ProductionChance?1:0);
                var updated=animal with {Version=animal.Version+1,Properties=new Dictionary<string,string>(animal.Properties){["productQuantity"]=amount.ToString(CultureInfo.InvariantCulture),["nextProductionUtc"]=now.AddMinutes(1).ToString("O")}};
                await _store.SaveEntityAsync(Configuration.Id,updated,token);_baseEntities[animal.Id]=updated;changed.Add(updated);
            }
        }
        finally{_treasureInteractionLock.Release();}
        return changed;
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
                var product=FarmRules.IsCow(animal)?"fertilizer":"egg";rewards.Add(InventoryStack(product,quantity));props["productQuantity"]="0";message=$"Collected {quantity} {DisplayItem(product)}.";
            }
            else if(action=="milk"&&FarmRules.IsCow(animal))
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
