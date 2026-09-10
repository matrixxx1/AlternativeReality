using AlternateEarth.Shared;
namespace AlternateEarth.Server;
public sealed partial class RealityWorld
{
    private bool MooseCharging(PlayerState p)=>p.Survival?.Illnesses?.Any(i=>i.Name=="Mad Moose Flu"&&i.EndsAtUtc>_probulatorClock.GetUtcNow()&&i.ChargeRemainingMeters>0)==true;
    private double SyrupSlow(WorldPosition position,string location)=>_loot.Values.Any(l=>l.DropKind=="mooseFluSyrup"&&l.LocationId==location&&l.ExpiresAtUtc>_probulatorClock.GetUtcNow()&&l.Position.Distance2D(position)<=2.5)?.5:1;
    internal async Task AdvanceMadMooseAsync(DateTimeOffset now,TimeSpan elapsed,List<PlayerState> changed,List<CombatEvent> combat,CancellationToken token)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            foreach(var original in _players.Values.Where(p=>p.HealthHearts>0).ToArray())
            {
                var illness=original.Survival?.Illnesses?.FirstOrDefault(i=>i.Name=="Mad Moose Flu"&&i.EndsAtUtc>now);if(illness is null)continue;
                var player=original;
                if(illness.NextSyrupAtUtc<=now)
                {
                    var drop=new LootDropState($"moose-flu:{player.Id}:{illness.NextSyrupAtUtc!.Value.UtcTicks}",player.Position,player.LocationId,0,[InventoryStack("mapleSyrup",1)],now.AddSeconds(30),"mooseFluSyrup",OwnerId:player.Id);
                    await _store.SavePersistentLootAsync(Configuration.Id,drop,token);_loot[drop.Id]=drop;_deathDropAnnouncements.Enqueue(drop);
                    player=await SaveFixturePlayerAsync(player.Id,current=>current with{Survival=current.Survival! with{Illnesses=current.Survival!.Illnesses!.Select(i=>i.Name=="Mad Moose Flu"?i with{NextSyrupAtUtc=now.AddMinutes(3)}:i).ToArray()}},null,token);changed.Add(player);
                }
                if(illness.ChargeRemainingMeters<=0||IsProbulatorAbducted(player.Id)||IsGasAsleep(player.Id))continue;
                var remaining=illness.ChargeRemainingMeters;var meters=Math.Min(remaining,12*Math.Clamp(elapsed.TotalSeconds,0,1)*SyrupSlow(player.Position,player.LocationId));var position=player.Position;var collided=false;
                while(meters>.00001)
                {
                    var step=Math.Min(.2,meters);var next=position with{X=position.X+Math.Cos(illness.ChargeAngle)*step,Y=position.Y+Math.Sin(illness.ChargeAngle)*step};
                    if(player.LocationId=="outdoor")collided=!Navigation.CanTraverse(position,next,true)||BusBlocksMovement(position,next)||IsWater(Navigation.TerrainAt(next.X,next.Y));
                    else if(_dungeons.TryGetValue(player.LocationId,out var home))collided=next.X<.5||next.Y<.5||next.X>home.Width-.5||next.Y>home.Height-.5||home.Walls.Any(w=>CrossesDungeonWall(position,next,w))||(home.Furnishings??[]).Any(f=>!IsStoredFurniture(f)&&f.Position.Distance2D(next)<.6);
                    else collided=true;
                    if(collided){remaining=0;break;}position=next;meters-=step;remaining=Math.Max(0,remaining-step);
                }
                if(player.LocationId=="outdoor")position=position with{Z=Navigation.ElevationAt(position.X,position.Y)};
                var updated=await SaveFixturePlayerAsync(player.Id,current=>current with{Position=position,TravelMode=TravelMode.Run,RidingBusId=null,WaitingAtBusStopId=null,SpeedMetersPerSecond=remaining>0?12*SyrupSlow(position,current.LocationId):0,HealthHearts=Math.Max(PlayerCanDie(current.Id)?0:1,current.HealthHearts-(collided?3:0)),Survival=current.Survival! with{Illnesses=current.Survival!.Illnesses!.Select(i=>i.Name=="Mad Moose Flu"?i with{ChargeRemainingMeters=remaining}:i).ToArray()}},null,token);
                if(collided){combat.Add(new(player.Id,player.Id,"mooseCharge",player.Position,position,true,3,updated.HealthHearts<=0,"Charge collided with an obstacle: 3 damage.",updated.HealthHearts));}
                if(updated.HealthHearts<=0){updated=await DieAndResetPlayerAsync(updated,token);await SavePlayerAsync(updated,token);}changed.Add(updated);
            }
        }
        finally{_treasureInteractionLock.Release();}
    }
    private void AttractSyrupEnemies(DateTimeOffset now,TimeSpan elapsed,Dictionary<string,ActorState> changed)
    {
        foreach(var mound in _loot.Values.Where(l=>l.DropKind=="mooseFluSyrup"&&l.ExpiresAtUtc>now))
        foreach(var original in ActorsAtLocation(mound.LocationId).Where(a=>a.HealthHearts>0&&((mound.OwnerId is not null?Relationship(mound.OwnerId,a.Id):a.FriendRating)<0||IsEventPredator(a.Subtype))&&!IsGasAsleep(a.Id)&&!IsProbulatorAbducted(a.Id)&&!a.IsPassingThroughPortal(now)).ToArray())
        {
            var actor=changed.GetValueOrDefault(original.Id,original);var distance=actor.Position.Distance2D(mound.Position);if(distance<=1||distance>150)continue;
            var step=Math.Min(distance-1,2.5*Math.Clamp(elapsed.TotalSeconds,0,1)*SyrupSlow(actor.Position,actor.LocationId));var next=actor.Position with{X=actor.Position.X+(mound.Position.X-actor.Position.X)/distance*step,Y=actor.Position.Y+(mound.Position.Y-actor.Position.Y)/distance*step};
            if(actor.LocationId=="outdoor"?!Navigation.CanTraverse(actor.Position,next,true):_dungeons.TryGetValue(actor.LocationId,out var dungeon)&&dungeon.Walls.Any(w=>CrossesDungeonWall(actor.Position,next,w)))continue;
            var updated=actor with{Position=next,IsMoving=true,Version=actor.Version+1};SetActor(actor.LocationId,updated);changed[updated.Id]=updated;
        }
    }
    private async Task<LootTakeResult> BottleMooseSyrupAsync(string id,LootDropState loot,CancellationToken token)
    {
        var inventory=GetInventoryState(id);var items=inventory.Items.ToDictionary(i=>i.ItemType);var jar=items.TryGetValue("emptyGlassJar",out var jars)&&jars.Quantity>0;var empty=jar?"emptyGlassJar":"emptyGlassBottle";
        if(!items.TryGetValue(empty,out var container)||container.Quantity<1)throw new InvalidOperationException("Collecting this syrup needs an empty jar or bottle in your backpack. Jars are used first.");
        if(container.Quantity==1)items.Remove(empty);else items[empty]=container with{Quantity=container.Quantity-1};
        var full=jar?"jarOfMapleSyrup":"bottleOfMapleSyrup";items[full]=items.TryGetValue(full,out var prior)?prior with{Quantity=prior.Quantity+1}:InventoryStack(full,1);var next=inventory with{Items=items.Values.ToArray()};
        if(PlayerObeysBackpackWeight(id)&&next.Items.Where(i=>i.CarriedInBackpack).Sum(i=>i.UnitWeightPounds*i.Quantity)>PlayerCarryingCapacity(id)+.0001)throw new InvalidOperationException("Make room in your backpack before collecting syrup.");
        await _store.CollectPersistentLootAsync(loot.Id,next,token);_inventories[id]=items.ToDictionary(i=>i.Key,i=>i.Value.Quantity);_loot.TryRemove(loot.Id,out _);
        return new(_players[id],null,$"Collected a {(jar?"jar":"bottle")} of maple syrup.");
    }
}
