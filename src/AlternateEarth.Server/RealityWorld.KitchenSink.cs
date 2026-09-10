using AlternateEarth.Shared;
namespace AlternateEarth.Server;
public sealed partial class RealityWorld
{
    private async Task<PlayerState> SaveFixturePlayerAsync(string id,Func<PlayerState,PlayerState> change,InventoryState? inventory,CancellationToken token)
    {
        var gate=_playerSaveLocks.GetOrAdd(id,_=>new SemaphoreSlim(1,1));await gate.WaitAsync(token);
        try
        {
            var current=_players[id];var updated=change(current) with {Version=current.Version+1};
            await _store.SaveCharacterAsync(Configuration.Id,updated,token,inventory);
            if(inventory is not null)_inventories[id]=inventory.Items.ToDictionary(i=>i.ItemType,i=>i.Quantity);
            _players[id]=updated;return updated;
        }
        finally{gate.Release();}
    }

    public async Task<PlayerState> UseKitchenSinkAsync(string id,string sinkId,string action,CancellationToken token=default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            EnsureNotProbulatorAbducted(id);
            if(!_players.TryGetValue(id,out var player)||IsGasAsleep(id)||!_dungeons.TryGetValue(player.LocationId,out var home)||!home.IsHome||!_playerAccounts.TryGetValue(id,out var account)||_baseBuildings.GetValueOrDefault(account)!=home.BuildingId)throw new InvalidOperationException("Use the kitchen sink in your own Home.");
            var sink=home.Furnishings?.FirstOrDefault(e=>e.Id==sinkId&&e.Properties.GetValueOrDefault("objectType")=="kitchenSink"&&!IsStoredFurniture(e))??throw new InvalidOperationException("That kitchen sink is not available.");
            if(player.Position.Distance2D(sink.Position)>4)throw new InvalidOperationException("Move within 4 meters of the kitchen sink.");
            if(action=="drink") {return await SaveFixturePlayerAsync(id,current=>EatNutrition(current,NutritionCatalog.Foods["purifiedWater"],_probulatorClock.GetUtcNow(),1,1,ProgressionRoll()),null,token);}
            var (empty,filled)=action switch {"bottle"=>("emptyGlassBottle","bottledWater"),"jar"=>("emptyGlassJar","jarOfWater"),_=>throw new InvalidOperationException("Choose Drink, Fill bottle, or Fill jar.")};
            if(!RemoveInventory(id,empty,1))throw new InvalidOperationException($"You need an {DisplayItem(empty)} in your backpack.");
            try
            {
                var reward=InventoryStack(filled,1);if(!CanAddToBackpack(id,[reward],out var capacity))throw new InvalidOperationException(capacity);
                var inventory=GetInventoryState(id);var items=inventory.Items.ToDictionary(i=>i.ItemType);items[filled]=items.TryGetValue(filled,out var existing)?existing with {Quantity=existing.Quantity+1}:reward;
                var next=inventory with {Items=items.Values.ToArray()};await _store.SaveInventoryAsync(next,token);_inventories[id]=next.Items.ToDictionary(i=>i.ItemType,i=>i.Quantity);return player;
            }
            catch {AddInventory(id,empty,1);throw;}
        }
        finally{_treasureInteractionLock.Release();}
    }
    private async Task<PlayerState> DrinkContainerAsync(string id,string item,CancellationToken token)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            EnsureNotProbulatorAbducted(id);if(IsGasAsleep(id))throw new InvalidOperationException("You cannot drink while asleep.");
            var player=_players[id];var inventory=GetInventoryState(id);var items=inventory.Items.ToDictionary(i=>i.ItemType);
            if(!items.TryGetValue(item,out var full)||full.Quantity<1)throw new InvalidOperationException("You do not have that water container.");
            if(full.Quantity==1)items.Remove(item);else items[item]=full with {Quantity=full.Quantity-1};
            var empty=item=="jarOfWater"?"emptyGlassJar":"emptyGlassBottle";items[empty]=items.TryGetValue(empty,out var prior)?prior with {Quantity=prior.Quantity+1}:InventoryStack(empty,1);
            var next=inventory with {Items=items.Values.ToArray()};
            return await SaveFixturePlayerAsync(id,current=>EatNutrition(current,NutritionCatalog.Foods[item],_probulatorClock.GetUtcNow(),1,1,ProgressionRoll()),next,token);
        }
        finally{_treasureInteractionLock.Release();}
    }
}
