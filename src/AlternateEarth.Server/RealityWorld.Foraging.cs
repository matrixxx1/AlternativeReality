using AlternateEarth.Geo;
using AlternateEarth.Shared;
using System.Text.Json;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    internal static double SandSearchChance(CharacterStats stats)=>Math.Clamp(.15+.025*stats.Luck+.025*stats.Perception+.02*stats.Opportunistic,.05,.95);
    private void PopulateForaging(GeographicDataset generated)
    {
        var bounds=generated.Area.Bounds;
        var random=new Random(StableInt($"forage:{Configuration.Seed}:{bounds.MinimumX:F2}:{bounds.MinimumY:F2}"));
        var buildings=_baseEntities.Values.Where(e=>e.Kind==EntityKind.Building).ToArray();
        bool Rural(WorldPosition p)=>!buildings.Any(b=>b.Position.Distance2D(p)<30 || b.Geometry.Count>0&&
            p.X>=b.Geometry.Min(v=>v.X)-20&&p.X<=b.Geometry.Max(v=>v.X)+20&&p.Y>=b.Geometry.Min(v=>v.Y)-20&&p.Y<=b.Geometry.Max(v=>v.Y)+20);
        void Node(string id,WorldPosition p,string subtype,string item="")
        {
            if(_removedBaseEntityIds.ContainsKey(id))return;
            _baseEntities.TryAdd(id,new(id,EntityKind.ResourceNode,p,[],new Dictionary<string,string>{["subtype"]=subtype,["itemType"]=item,["displayName"]=subtype=="sandLump"?"Sand mound — search":$"Wild {DisplayItem(item)}"}));
        }
        for(var i=0;i<38;i++)for(var attempt=0;attempt<35;attempt++)
        {
            var p=new WorldPosition(generated.Area.Region,bounds.MinimumX+random.NextDouble()*(bounds.MaximumX-bounds.MinimumX),bounds.MinimumY+random.NextDouble()*(bounds.MaximumY-bounds.MinimumY));
            if(Navigation.TerrainAt(p.X,p.Y) is not (TerrainType.Grass or TerrainType.Forest) || Navigation.IsBlocked(p.X,p.Y)||!Rural(p))continue;
            var id=$"forage:{bounds.MinimumX:F2}:{bounds.MinimumY:F2}:{i}";
            if(i<28)
            {
                var item=NutritionCatalog.Produce.Where(p=>p!="cranberry").ElementAt(random.Next(NutritionCatalog.Produce.Length-1));
                Node(id,p,item is "apple" or "pear" or "peach" or "orange" or "banana"?"fruitTree":"wildCrop",item);
            }
            else
            {
                var animal=NutritionCatalog.Livestock[(i-28)%NutritionCatalog.Livestock.Length];
                _actors.TryAdd(id,new(id,EntityKind.Animal,animal,char.ToUpperInvariant(animal[0])+animal[1..],p,HealthHearts:animal=="cow"?8:animal=="chicken"?2:4,MaximumHealthHearts:animal=="cow"?8:animal=="chicken"?2:4));
            }
            break;
        }
        foreach(var water in generated.Features.Where(e=>e.Kind==EntityKind.Water))
        {
            var rings=new[]{water.Geometry}.Concat(water.InteriorRings??[]);var index=0;
            foreach(var ring in rings)for(var segment=1;segment<ring.Count;segment++)
            {
                var a=ring[segment-1];var b=ring[segment];var dx=b.X-a.X;var dy=b.Y-a.Y;var length=Math.Sqrt(dx*dx+dy*dy);
                for(var offset=10d;offset<length;offset+=22,index++)
                {
                    if(random.NextDouble()>.45)continue;
                    var along=offset/length;
                    for(var side=-1;side<=1;side+=2)
                    {
                        var shoreOffset=WaterGeometry.IsPolygon(water)?1.5:Math.Max(.25,WaterGeometry.Width(water)/2+(index%2==0?1.5:-1.5));
                        var p=new WorldPosition(generated.Area.Region,a.X+dx*along-dy/length*shoreOffset*side,a.Y+dy*along+dx/length*shoreOffset*side);
                        if(!bounds.Contains(p.X,p.Y)||Navigation.IsBlocked(p.X,p.Y))continue;
                        var terrain=Navigation.TerrainAt(p.X,p.Y);var id=$"shore:{water.Id}:{index}:{side}";
                        if(terrain==TerrainType.Sand)Node(id,p,"sandLump");
                        else if(terrain==TerrainType.ShallowWater)
                        {
                            if(index%2==0)Node(id,p,"wildCrop","cranberry");
                            else {var animal=NutritionCatalog.Shellfish[index/2%NutritionCatalog.Shellfish.Length];_actors.TryAdd(id,new(id,EntityKind.Animal,animal,animal,p,HealthHearts:1,MaximumHealthHearts:1));}
                        }
                    }
                }
            }
        }
    }

    public async Task<(string Message,PlayerPrivateState PrivateState)> GatherWildAsync(string playerId,string entityId,CancellationToken token=default)
    {
        EnsureNotProbulatorAbducted(playerId);
        if(IsGasAsleep(playerId))throw new InvalidOperationException("You cannot gather or search while asleep.");
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            if(!_players.TryGetValue(playerId,out var player)||player.LocationId!="outdoor")throw new InvalidOperationException("Gather in the outdoor world.");
            if(!_baseEntities.TryGetValue(entityId,out var node)||node.Properties.GetValueOrDefault("subtype") is not ("wildCrop" or "fruitTree" or "sandLump"))throw new InvalidOperationException("That resource has already been gathered or searched.");
            if(player.Position.Distance2D(node.Position)>4)throw new InvalidOperationException("Move within 4 meters to gather or search.");
            var sand=node.Properties.GetValueOrDefault("subtype")=="sandLump";
            var item=node.Properties.GetValueOrDefault("itemType")!;
            if(!sand&&!CanAddToBackpack(playerId,[InventoryStack(item,1)],out var capacity))throw new InvalidOperationException(capacity);
            var success=!sand||ProgressionRoll()<SandSearchChance(StatsFor(playerId));
            await _store.RemoveEntityAsync(Configuration.Id,node,token);
            _removedBaseEntityIds[node.Id]=0;_baseEntities.TryRemove(node.Id,out _);
            string message;
            if(!sand){AddInventory(playerId,item,1);await SaveInventoryAsync(playerId,token);message=$"Gathered {DisplayItem(item)}.";}
            else if(success)
            {
                var id=node.Id+":chest";var rng=new Random(StableInt(id));
                _outdoorChests[id]=new(id,node.Position,"outdoor");
                _chestContents[id]=new(id,rng.Next(50,2001),[InventoryStack(rng.Next(2)==0?"ring":"soiledUnderwear",1)]);
                _realityEntities[id]=new(id,EntityKind.TreasureChest,node.Position,[],new Dictionary<string,string>{["subtype"]="buriedTreasure"},IsBaseEntity:false);
                await PersistBuriedChestAsync(id,false,token);message="Search successful! You uncovered a treasure chest containing coins and a buried item.";
            }
            else message="Search unsuccessful. Nothing valuable was buried in this mound.";
            return (message,GetPrivateState(playerId));
        }
        finally{_treasureInteractionLock.Release();}
    }

    private async Task PersistBuriedChestAsync(string id,bool removed,CancellationToken token)
    {
        if(!_realityEntities.TryGetValue(id,out var entity)||entity.Properties.GetValueOrDefault("subtype")!="buriedTreasure")return;
        if(removed){await _store.RemoveEntityAsync(Configuration.Id,entity,token);_realityEntities.TryRemove(id,out _);return;}
        var updated=entity with{Properties=new Dictionary<string,string>(entity.Properties){["contents"]=JsonSerializer.Serialize(_chestContents[id],SharedJson.Options)}};
        _realityEntities[id]=updated;await _store.SaveEntityAsync(Configuration.Id,updated,token);
    }
    private void RestoreBuriedChests()
    {
        foreach(var entity in _realityEntities.Values.Where(e=>e.Properties.GetValueOrDefault("subtype")=="buriedTreasure"))
        {
            _outdoorChests[entity.Id]=new(entity.Id,entity.Position,"outdoor");
            if(entity.Properties.GetValueOrDefault("contents") is { } json && JsonSerializer.Deserialize<ChestContentsState>(json,SharedJson.Options) is { } contents)_chestContents[entity.Id]=contents;
        }
    }
}
