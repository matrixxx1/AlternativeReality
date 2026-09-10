using AlternateEarth.Shared;
using System.Collections.Concurrent;
namespace AlternateEarth.Server;
public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<string,DateTimeOffset> _lastHoseDrink=new();
    internal static readonly string[] DirtyHoseRemarks=[
        "Ewww, the last person used this to clean dog poop. I can taste the Labrador.",
        "Why does this taste like somebody pressure-washed a portable toilet?",
        "There's a hair in it. Please tell me the hose grew a beard.",
        "Lovely. Eau de dumpster juice, with a delicate note of expired shrimp.",
        "I just swallowed something that waved goodbye to its family.",
        "Who rinsed a litter box with this? My mouth is now a cat bathroom.",
        "That tasted like a compost heap burped directly into my face.",
        "Ah yes, warm worm soup. Just like nobody's grandma should make.",
        "Someone used this to unclog a sewer. The sewer would like its flavor back.",
        "I think that chunky bit was a slug. It was wearing yesterday's mayonnaise.",
        "Why does this water smell like a raccoon's hot-tub party?",
        "This tastes like the bottom of a trash can after a fish funeral.",
        "Who washed their gym socks in the nozzle? My tongue just filed for divorce."
    ];
    internal static PlayerState ApplyHoseWater(PlayerState player,bool dirty,DateTimeOffset now,double durationRoll) => EatNutrition(player,dirty?NutritionCatalog.Foods["dirtyWater"] with {ParasiteChance=1}:NutritionCatalog.Foods["purifiedWater"],now,dirty?0:1,durationRoll,dirty?1:durationRoll);
    public async Task<(PlayerState Player,ChatMessage? Remark)> DrinkHoseAsync(string id,string hoseId,CancellationToken token=default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            var player=GardenPlayer(id);if(!_baseEntities.TryGetValue(hoseId,out var hose)||hose.Properties.GetValueOrDefault("subtype")!="gardenHose")throw new InvalidOperationException("That hose is not available.");
            if(player.Position.Distance2D(hose.Position)>4)throw new InvalidOperationException("Move within 4 meters of the hose.");
            var now=_probulatorClock.GetUtcNow();if(_lastHoseDrink.TryGetValue(id,out var last)&&now-last<TimeSpan.FromSeconds(2))throw new InvalidOperationException("Give the hose a moment.");
            var dirty=ProgressionRoll()<.75;await SaveFixturePlayerAsync(id,current=>ApplyHoseWater(current,dirty,now,ProgressionRoll()),null,token);_lastHoseDrink[id]=now;
            var remark=dirty?new ChatMessage("hose:"+Guid.NewGuid().ToString("N"),id,player.Name,DirtyHoseRemarks[System.Security.Cryptography.RandomNumberGenerator.GetInt32(DirtyHoseRemarks.Length)],now):null;
            return (_players[id],remark);
        }
        finally{_treasureInteractionLock.Release();}
    }
}
