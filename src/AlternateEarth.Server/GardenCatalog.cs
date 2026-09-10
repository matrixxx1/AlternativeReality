using AlternateEarth.Shared;
namespace AlternateEarth.Server;
internal static class GardenCatalog
{
    public static readonly ItemConfiguration[] Items = [
        new("fertilizer","Fertilizer","Garden building material; five per garden",0,0,100,400,WeightPounds:.2),
        new("gardeningBook","Gardening for dumb shits","Consume once: permanent +5 percentage points to garden building success. Does not stack.",0,0,5000,15000,WeightPounds:.25),
        .. GardenRules.Crops.Select(c=>new ItemConfiguration(GardenRules.Seed(c),c+" seeds","50 matching seeds build one garden",0,0,5,50,WeightPounds:.005))];
}
