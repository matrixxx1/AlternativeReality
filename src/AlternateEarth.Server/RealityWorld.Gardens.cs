using AlternateEarth.Shared;
using AlternateEarth.Geo;
using System.Globalization;
using System.Security.Cryptography;
namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private DateTimeOffset _nextGardenTick;
    private CanonicalEntity? Garden(string id) => _baseEntities.GetValueOrDefault(id) is { } b && GardenRules.IsGarden(b) ? b : _realityEntities.GetValueOrDefault(id) is { } r && GardenRules.IsGarden(r) ? r : null;
    private void PutGarden(CanonicalEntity entity) { _realityEntities.TryRemove(entity.Id,out _); _baseEntities[entity.Id]=entity; }
    private void RestoreGardens() { foreach(var entity in _realityEntities.Values.Where(e=>GardenRules.IsGarden(e)||FarmRules.IsAnimal(e)).ToArray()) PutGarden(entity); }
    private PlayerState GardenPlayer(string id)
    {
        EnsureNotProbulatorAbducted(id);
        if(!_players.TryGetValue(id,out var player)||player.LocationId!="outdoor"||player.HealthHearts<=0||IsGasAsleep(id)) throw new InvalidOperationException("Gardening requires an awake player outdoors.");
        return player;
    }
    private CanonicalEntity GardenHome(string id)
    {
        var player=GardenPlayer(id);
        if(!_playerAccounts.TryGetValue(id,out var account)||!_baseBuildings.TryGetValue(account,out var houseId)||!_baseEntities.TryGetValue(houseId,out var house)||player.Position.Distance2D(house.Position)>GardenRules.HomeRadius)
            throw new InvalidOperationException("Move within 40 meters of your own Home to build a garden.");
        return house;
    }
    public GardenBuildState RequestGardenBuild(string id)
    {
        var house=GardenHome(id);var player=_players[id];var level=GetCraftingSkill(id).Level;var supplies=CraftingSupplies(id);
        var book=player.Survival?.GardeningBookRead==true;var chance=GardenRules.BuildChance(level,StatsFor(id),book);
        return new(house.Position,GardenRules.HomeRadius,level,book,GardenRules.Crops.Select(c=>new GardenBuildOption(c,supplies.GetValueOrDefault(GardenRules.Seed(c)),supplies.GetValueOrDefault("wood"),supplies.GetValueOrDefault("fertilizer"),chance,level>=5&&supplies.GetValueOrDefault(GardenRules.Seed(c))>=50&&supplies.GetValueOrDefault("wood")>=20&&supplies.GetValueOrDefault("fertilizer")>=5)).ToArray());
    }
    public async Task<GardenResult> BuildGardenAsync(string id,BuildGardenRequest request,CancellationToken token=default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            var house=GardenHome(id);var account=_playerAccounts[id];await EnsureHomeItemStorageAsync(account,token);
            await _homeItemStorageLock.WaitAsync(token);
            try
            {
                var state=RequestGardenBuild(id);var option=state.Options.FirstOrDefault(o=>o.Crop==request.Crop)??throw new InvalidOperationException("Choose a fruit or vegetable crop.");
                if(!option.CanBuild)throw new InvalidOperationException("You need crafting level 5, 50 matching seeds, 20 wood and 5 fertilizer in your backpack and Home storage.");
                var position=house.Position with {X=request.X,Y=request.Y};
                if(GardenRules.Footprint(position).Any(p=>Math.Sqrt(Math.Pow(p.X-house.Position.X,2)+Math.Pow(p.Y-house.Position.Y,2))>GardenRules.HomeRadius))throw new InvalidOperationException("Keep the entire garden inside your Home's 40-meter range ring.");
                var area=_loadedAreas.Values.FirstOrDefault(b=>b.Contains(request.X-2.1,request.Y-1.6)&&b.Contains(request.X+2.1,request.Y+1.6));
                if(area is null||!GardenGenerator.Clear(position,area,_baseEntities.Values.Concat(_realityEntities.Values))||_players.Values.Any(p=>p.LocationId=="outdoor"&&Math.Abs(p.Position.X-position.X)<2.5&&Math.Abs(p.Position.Y-position.Y)<2)||_actors.Values.Any(a=>a.LocationId=="outdoor"&&a.HealthHearts>0&&Math.Abs(a.Position.X-position.X)<2.5&&Math.Abs(a.Position.Y-position.Y)<2))throw new InvalidOperationException("The whole garden and sign need unused grass, clear of buildings, paths, water and other objects.");
                position=position with {Z=Navigation.ElevationAt(position.X,position.Y)};
                var backpack=GetInventoryState(id);var home=GetHomeItemStorage(account);var pack=backpack.Items.ToDictionary(i=>i.ItemType,i=>i.Quantity);var stored=home.Items.ToDictionary(i=>i.ItemType,i=>i.Quantity);
                foreach(var (item,quantity) in new[]{(GardenRules.Seed(request.Crop),50),("wood",20),("fertilizer",5)}){var take=Math.Min(quantity,stored.GetValueOrDefault(item));stored[item]=stored.GetValueOrDefault(item)-take;pack[item]=pack.GetValueOrDefault(item)-(quantity-take);}
                var nextPack=backpack with {Items=backpack.Items.Where(i=>pack.GetValueOrDefault(i.ItemType)>0).Select(i=>i with {Quantity=pack[i.ItemType]}).ToArray()};
                var nextHome=home with {Items=home.Items.Where(i=>stored.GetValueOrDefault(i.ItemType)>0).Select(i=>i with {Quantity=stored[i.ItemType]}).ToArray()};
                var success=ProgressionRoll()<option.SuccessChance;
                CanonicalEntity? entity=success?GardenRules.Create("garden:player:"+Guid.NewGuid().ToString("N"),position,request.Crop,house.Id):null;
                if(entity is not null)await _store.SaveEntityAsync(Configuration.Id,entity,token,[nextPack,nextHome]);else await _store.SaveInventoriesAsync([nextPack,nextHome],token);
                _inventories[id]=pack;_homeItemStorage[account]=stored;
                if(entity is not null)PutGarden(entity);
                return new(success?"Garden built successfully! It is ready to harvest.":"Garden building failed. The attempt used 50 seeds, 20 wood and 5 fertilizer.",GetPrivateState(id),entity);
            }
            finally{_homeItemStorageLock.Release();}
        }
        finally{_treasureInteractionLock.Release();}
    }
    public async Task<GardenResult> HarvestGardenAsync(string id,string gardenId,CancellationToken token=default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            var player=GardenPlayer(id);var entity=Garden(gardenId)??throw new InvalidOperationException("That garden no longer exists.");
            if(entity.Properties.GetValueOrDefault("state")=="rubble")throw new InvalidOperationException("This garden was permanently destroyed.");
            if(player.Position.Distance2D(entity.Position)>4)throw new InvalidOperationException("Move within 4 meters to harvest.");
            var now=_probulatorClock.GetUtcNow();
            if(DateTimeOffset.TryParse(entity.Properties.GetValueOrDefault("readyAtUtc"),out var ready)&&ready>now)return new("Nothing available. Check again tomorrow.",GetPrivateState(id),entity);
            var crop=entity.Properties["itemType"];var (produce,seeds)=GardenRules.Yield(new Random(RandomNumberGenerator.GetInt32(int.MaxValue)),StatsFor(id));
            var rewards=new List<ItemStack>{InventoryStack(crop,produce)};if(seeds>0)rewards.Add(InventoryStack(GardenRules.Seed(crop),seeds));
            if(!CanAddToBackpack(id,rewards,out var capacity))throw new InvalidOperationException(capacity);
            var inventory=GetInventoryState(id);var items=inventory.Items.ToDictionary(i=>i.ItemType);
            foreach(var reward in rewards)items[reward.ItemType]=items.TryGetValue(reward.ItemType,out var existing)?existing with {Quantity=existing.Quantity+reward.Quantity}:reward;
            var next=inventory with {Items=items.Values.ToArray()};var updated=entity with {Version=entity.Version+1,Properties=new Dictionary<string,string>(entity.Properties){["readyAtUtc"]=now.AddHours(12).ToString("O")}};
            await _store.SaveEntityAsync(Configuration.Id,updated,token,[next]);_inventories[id]=next.Items.ToDictionary(i=>i.ItemType,i=>i.Quantity);PutGarden(updated);
            return new($"Harvested {produce} {DisplayItem(crop)} and {seeds} matching seeds. Check again tomorrow.",GetPrivateState(id),updated);
        }
        finally{_treasureInteractionLock.Release();}
    }
    private async Task<PlayerState> ReadGardeningBookAsync(string id,CancellationToken token)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            EnsureNotProbulatorAbducted(id);if(IsGasAsleep(id))throw new InvalidOperationException("You cannot read while asleep.");
            var player=_players[id];if(player.Survival?.GardeningBookRead==true)throw new InvalidOperationException("You already have the permanent gardening bonus.");
            if(InventoryQuantity(id,"gardeningBook")<1)throw new InvalidOperationException("You need Gardening for dumb shits.");
            var next=GetInventoryState(id);next=next with {Items=next.Items.Select(i=>i.ItemType=="gardeningBook"?i with {Quantity=i.Quantity-1}:i).Where(i=>i.Quantity>0).ToArray()};
            return await SaveFixturePlayerAsync(id,current=>current with {Survival=(current.Survival??new()) with {GardeningBookRead=true}},next,token);
        }
        finally{_treasureInteractionLock.Release();}
    }
    private async Task<CanonicalEntity> DamageGardenAsync(CanonicalEntity entity,double damage,CancellationToken token)
    {
        var health=double.TryParse(entity.Properties.GetValueOrDefault("healthHearts"),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:20;
        var props=new Dictionary<string,string>(entity.Properties){["healthHearts"]=Math.Max(0,health-damage).ToString(CultureInfo.InvariantCulture)};
        if(health<=damage){props["state"]="rubble";props["rubbleUntilUtc"]=_probulatorClock.GetUtcNow().AddMinutes(5).ToString("O");}
        var updated=entity with {Version=entity.Version+1,Properties=props};await _store.SaveEntityAsync(Configuration.Id,updated,token);PutGarden(updated);return updated;
    }
    private async Task<WorldCrimeResult> AttackGardenAsync(string id,string gardenId,CancellationToken token)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            var player=GardenPlayer(id);var entity=Garden(gardenId)??throw new InvalidOperationException("That garden no longer exists.");
            if(entity.Properties.GetValueOrDefault("state")=="rubble")throw new InvalidOperationException("That garden is already destroyed.");
            var weapon=player.EquippedWeapon;var definition=InventoryDefinition(weapon);
            if(weapon=="none"||(!player.GodMode&&!OwnsWeapon(id,weapon)))throw new InvalidOperationException("Equip an owned weapon.");
            if(player.Position.Distance2D(entity.Position)>definition.RangeMeters)throw new InvalidOperationException("That garden is out of weapon range.");
            var now=_probulatorClock.GetUtcNow();var interval=TimeSpan.FromSeconds(Math.Clamp(definition.AttackIntervalSeconds,.05,10));
            if(_lastPlayerAttack.TryGetValue((id,weapon),out var previous)&&now-previous<interval)throw new InvalidOperationException("Your weapon is not ready yet.");
            var ammo=WeaponDefinition(weapon).Ammo;if(!player.GodMode&&ammo is not null){if(!RemoveInventory(id,ammo,1))throw new InvalidOperationException($"You need {DisplayItem(ammo)}.");await SaveInventoryAsync(id,token);}
            _lastPlayerAttack[(id,weapon)]=now;var damage=WeaponDamageFor(id,weapon,Math.Max(.25,definition.Damage));var updated=await DamageGardenAsync(entity,damage,token);var dead=updated.Properties.GetValueOrDefault("state")=="rubble";
            var message=dead?"Garden permanently destroyed. Rubble clears in five minutes.":"Garden damaged.";
            return new WorldCrimeResult(player,GetPrivateState(id),message,updated,null,new CombatEvent(id,gardenId,weapon,player.Position,entity.Position,true,damage,dead,message));
        }
        finally{_treasureInteractionLock.Release();}
    }
    public async Task<(IReadOnlyList<CanonicalEntity> Changed,IReadOnlyList<string> Removed)> AdvanceGardensAsync(CancellationToken token=default)
    {
        var now=_probulatorClock.GetUtcNow();if(now<_nextGardenTick)return ([],[]);_nextGardenTick=now.AddSeconds(5);
        var changed=new List<CanonicalEntity>();var removed=new List<string>();
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            foreach(var entity in _baseEntities.Values.Where(GardenRules.IsGarden).ToArray())
            {
                if(entity.Properties.GetValueOrDefault("state")=="rubble")
                {
                    if(DateTimeOffset.TryParse(entity.Properties.GetValueOrDefault("rubbleUntilUtc"),out var expiry)&&now>=expiry){await _store.RemoveEntityAsync(Configuration.Id,entity,token);_removedBaseEntityIds[entity.Id]=0;_baseEntities.TryRemove(entity.Id,out _);_realityEntities.TryRemove(entity.Id,out _);removed.Add(entity.Id);}continue;
                }
                // Hostile NPCs/enemies can trample a nearby plot; ordinary friendly neighbors leave it alone.
                var attacker=_actors.Values.FirstOrDefault(a=>a.LocationId=="outdoor"&&a.HealthHearts>0&&a.Abduction is null&&!a.IsPassingThroughPortal(now)&&(a.AsleepUntilUtc is null||a.AsleepUntilUtc<=now)&&(a.FriendRating<0||a.EventName is not null)&&a.Position.Distance2D(entity.Position)<=2.5);
                if(attacker is not null)changed.Add(await DamageGardenAsync(entity,Math.Max(1,InventoryDefinition(attacker.EquippedWeapon).Damage),token));
            }
        }
        finally{_treasureInteractionLock.Release();}
        return (changed,removed);
    }
}
public sealed record GardenResult(string Message,PlayerPrivateState PrivateState,CanonicalEntity? Entity);
