using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public async Task CollectDirtyWaterAsync(string playerId, CancellationToken token = default)
    {
        EnsureNotProbulatorAbducted(playerId);
        if (IsGasAsleep(playerId)) throw new InvalidOperationException("You cannot collect water while asleep.");
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            if (!_players.TryGetValue(playerId, out var player) || !IsWater(TerrainFor(player)))
                throw new InvalidOperationException("Stand in shallow or deep water to collect dirty water.");
            if (!CanAddToBackpack(playerId, [InventoryStack("dirtyWater", 1)], out var capacity)) throw new InvalidOperationException(capacity);
            AddInventory(playerId, "dirtyWater", 1);
            try { await SaveInventoryAsync(playerId, token); }
            catch { RemoveInventory(playerId, "dirtyWater", 1); throw; }
        }
        finally { _treasureInteractionLock.Release(); }
    }

    internal static PlayerState StepHunger(PlayerState player, double seconds, DateTimeOffset now)
    {
        var state=player.Survival??new();
        var illnesses=(state.Illnesses??[]).Where(i=>i.EndsAtUtc>now).ToArray();
        var buffs=(state.Buffs??[]).Where(b=>b.EndsAtUtc>now).ToArray();
        seconds=Math.Clamp(seconds,0,2);
        if(player.GodMode)return player with{Survival=new(0,[],buffs)};
        var sick=illnesses.Length>0;
        var hunger=Math.Clamp(state.Hunger+seconds*(sick?.2:.05),0,100);
        var health=player.HealthHearts;var stamina=player.Stamina;
        if(hunger>=100)
        {
            var starvingSeconds=Math.Max(0,seconds-Math.Max(0,100-state.Hunger)/(sick?.2:.05));
            health-=Math.Max(0,starvingSeconds-stamina)*.15;
            stamina=Math.Max(0,stamina-starvingSeconds);
        }
        if(sick)health-=seconds*(player.Water<=0?.08:.0125);
        return player with{Survival=new(hunger,illnesses,buffs),Stamina=stamina,HealthHearts=Math.Max(0,health)};
    }

    internal static PlayerState EatNutrition(PlayerState player, Nutrition nutrition, DateTimeOffset now, double infectionRoll, double durationRoll, double cureRoll = 1)
    {
        var state=player.Survival??new();
        var illnesses=(state.Illnesses??[]).Where(i=>i.EndsAtUtc>now).ToList();
        if(cureRoll<nutrition.ParasiteCureChance)illnesses.RemoveAll(i=>i.Name=="Parasites");
        if(infectionRoll<(nutrition.ParasiteChance??(nutrition.Raw?.25:0)))
        {
            var until=now.AddSeconds(60+Math.Clamp(durationRoll,0,1)*10740);
            var existing=illnesses.FirstOrDefault(i=>i.Name=="Parasites");
            illnesses.RemoveAll(i=>i.Name=="Parasites");
            illnesses.Add(new("Parasites",existing is not null&&existing.EndsAtUtc>until?existing.EndsAtUtc:until));
        }
        var buffs=(state.Buffs??[]).Where(b=>b.EndsAtUtc>now).ToList();
        foreach(var bonus in nutrition.Bonuses??new Dictionary<string,int>())
        {
            var amount=Math.Max(bonus.Value,buffs.Where(b=>b.Stat==bonus.Key).Select(b=>b.Amount).DefaultIfEmpty().Max());
            buffs.RemoveAll(b=>b.Stat==bonus.Key);buffs.Add(new(bonus.Key,amount,now.AddMinutes(5)));
        }
        return player with{Survival=new(Math.Max(0,state.Hunger-nutrition.Hunger),illnesses,buffs),
            HealthHearts=Math.Min(player.MaximumHealthHearts,player.HealthHearts+nutrition.Health),
            Stamina=Math.Min(player.MaximumStamina,player.Stamina+nutrition.Stamina),Water=Math.Min(player.MaximumWater,player.Water+nutrition.Water),Version=player.Version+1};
    }

    private async Task<PlayerState> ConsumeNutritionAsync(PlayerState player,string itemType,CancellationToken token)
    {
        EnsureNotProbulatorAbducted(player.Id);
        if(IsGasAsleep(player.Id))throw new InvalidOperationException("You cannot eat or take medicine while asleep.");
        if(!RemoveInventory(player.Id,itemType,1))throw new InvalidOperationException($"You do not have any {DisplayItem(itemType)}.");
        var now=_probulatorClock.GetUtcNow();
        var updated=itemType.Equals("antibiotics",StringComparison.OrdinalIgnoreCase)
            ? player with{Survival=(player.Survival??new()) with{Illnesses=[]},Version=player.Version+1}
            : EatNutrition(player,NutritionCatalog.Foods[itemType],now,ProgressionRoll(),ProgressionRoll(),ProgressionRoll());
        if(itemType.Equals("water",StringComparison.OrdinalIgnoreCase))updated=updated with{Water=player.MaximumWater,WaterProtectedUntilUtc=now.AddMinutes(5)};
        await SaveInventoryAsync(player.Id,token);await SavePlayerAsync(updated,token);return updated;
    }

    private CharacterStats FoodStats(string playerId,CharacterStats stats)
    {
        var buffs=(_players.GetValueOrDefault(playerId)?.Survival?.Buffs??[]).Where(b=>b.EndsAtUtc>_probulatorClock.GetUtcNow());
        foreach(var b in buffs)stats=b.Stat switch
        {
            "strength"=>stats with{Strength=stats.Strength+b.Amount},"perception"=>stats with{Perception=stats.Perception+b.Amount},
            "endurance"=>stats with{Endurance=stats.Endurance+b.Amount},"charisma"=>stats with{Charisma=stats.Charisma+b.Amount},
            "intelligence"=>stats with{Intelligence=stats.Intelligence+b.Amount},"agility"=>stats with{Agility=stats.Agility+b.Amount},
            "luck"=>stats with{Luck=stats.Luck+b.Amount},"nutUp"=>stats with{NutUp=stats.NutUp+b.Amount},
            "opportunistic"=>stats with{Opportunistic=stats.Opportunistic+b.Amount},"timing"=>stats with{Timing=stats.Timing+b.Amount},_=>stats
        };
        return stats;
    }
}
