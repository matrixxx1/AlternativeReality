using AlternateEarth.Shared;
namespace AlternateEarth.Server;
public sealed partial class RealityWorld
{
    internal static PlayerState ApplyMeatDisease(PlayerState player,string item,DateTimeOffset now,double roll)
    {
        var disease=NutritionCatalog.MeatDisease(item);if(disease is null||roll>=.3)return player;
        var state=player.Survival??new();var illnesses=(state.Illnesses??[]).Where(i=>i.EndsAtUtc>now&&i.Name!=disease).ToList();
        illnesses.Add(new(disease,now.AddMinutes(15),now.AddMinutes(1),NextSyrupAtUtc:disease=="Mad Moose Flu"?now.AddMinutes(3):null));
        return player with{Survival=state with{Illnesses=illnesses}};
    }
    private static string? DiseaseCall(string name)=>name switch{"Mad Moose Flu"=>"Charge!!!","Mad cow disease"=>"Moo!","Swine flu"=>"Oink!","Avian flu"=>"Kakaw!",_=>null};
    internal async Task AdvanceAnimalIllnessesAsync(DateTimeOffset now,List<PlayerState> changed,CancellationToken token)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            foreach(var player in _players.Values.Where(p=>p.HealthHearts>0&&!p.GodMode).ToArray())
            {
                var due=(player.Survival?.Illnesses??[]).Where(i=>i.EndsAtUtc>now&&DiseaseCall(i.Name) is not null&&i.NextCallAtUtc<=now).ToArray();if(due.Length==0)continue;
                var updated=await SaveFixturePlayerAsync(player.Id,current=>current with{Survival=(current.Survival??new()) with{Illnesses=(current.Survival?.Illnesses??[]).Select(i=>due.Any(d=>d.Name==i.Name)?i with{NextCallAtUtc=now.AddMinutes(1),LastCallAtUtc=now,ChargeAngle=i.Name=="Mad Moose Flu"?ProgressionRoll()*Math.Tau:i.ChargeAngle,ChargeRemainingMeters=i.Name=="Mad Moose Flu"?91.44:0}:i).ToArray()}},null,token);changed.Add(updated);
                foreach(var illness in due)_questDialogue.Enqueue(new("disease:"+Guid.NewGuid().ToString("N"),player.Id,player.Name,DiseaseCall(illness.Name)!,now));
            }
        }
        finally{_treasureInteractionLock.Release();}
    }
    internal static bool HasRecentAnimalCall(PlayerState player,DateTimeOffset now)=>(player.Survival?.Illnesses??[]).Any(i=>i.EndsAtUtc>now&&DiseaseCall(i.Name) is not null&&i.LastCallAtUtc is {} last&&last<=now&&last.AddSeconds(20)>now);
    private void AttractAnimalDiseaseEnemies(DateTimeOffset now,TimeSpan elapsed,Dictionary<string,ActorState> changed)
    {
        foreach(var player in _players.Values.Where(p=>p.HealthHearts>0&&HasRecentAnimalCall(p,now)))
        foreach(var original in ActorsAtLocation(player.LocationId).Where(a=>a.HealthHearts>0&&a.Position.Distance2D(player.Position)<=150&&(Relationship(player.Id,a.Id)<0||IsEventPredator(a.Subtype))&&!IsGasAsleep(a.Id)&&!IsProbulatorAbducted(a.Id)&&!a.IsPassingThroughPortal(now)).ToArray())
        {
            var actor=changed.GetValueOrDefault(original.Id,original);var distance=actor.Position.Distance2D(player.Position);if(distance<=3)continue;
            var step=Math.Min(distance-3,Math.Max(0,elapsed.TotalSeconds)*2.5*SyrupSlow(actor.Position,actor.LocationId));var next=actor.Position with{X=actor.Position.X+(player.Position.X-actor.Position.X)/distance*step,Y=actor.Position.Y+(player.Position.Y-actor.Position.Y)/distance*step};
            if(player.LocationId=="outdoor"?!Navigation.CanTraverse(actor.Position,next,true):_dungeons.TryGetValue(player.LocationId,out var dungeon)&&dungeon.Walls.Any(w=>CrossesDungeonWall(actor.Position,next,w)))continue;
            if(player.LocationId=="outdoor")next=next with{Z=Navigation.ElevationAt(next.X,next.Y)};
            var updated=actor with{Position=next,IsMoving=true,Version=actor.Version+1};SetActor(player.LocationId,updated);changed[updated.Id]=updated;
        }
    }
}
