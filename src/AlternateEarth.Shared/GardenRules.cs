namespace AlternateEarth.Shared;

public static class GardenRules
{
    public const double Width = 4, Height = 3, HomeRadius = 40, FarmIsolation = 1609.344 / 3;
    public static readonly string[] Crops = ["apple", "pear", "peach", "orange", "banana", "blackberry", "raspberry", "blueberry", "strawberry", "cranberry", "watermelon", "carrot", "potato", "corn", "beans", "spinach", "lettuce", "garlic", "onion", "peppers", "tomato", "pumpkin"];
    public static string Seed(string crop) => "seed:" + crop;
    public static bool IsGarden(CanonicalEntity entity) => entity.Properties.GetValueOrDefault("subtype") == "garden";
    public static double BuildChance(int level, CharacterStats stats, bool book) => level < 5 ? 0 : Math.Clamp(.25 + .015 * (level - 5) + .01 * Math.Max(0, stats.Intelligence - 1) + .005 * Math.Max(0, stats.Luck - 1) + (book ? .05 : 0), .01, .95);
    public static double HarvestBonus(CharacterStats stats) => Math.Clamp(.015 * Math.Max(0, stats.Perception - 1) + .015 * Math.Max(0, stats.Luck - 1) + .01 * Math.Max(0, stats.Opportunistic - 1), 0, .9);
    public static (int Produce, int Seeds) Yield(Random random, CharacterStats stats)
    {
        var bonus = HarvestBonus(stats); var produce = random.Next(1, 11); var seeds = random.Next(0, 6);
        if (random.NextDouble() < bonus) produce = Math.Max(produce, random.Next(1, 11));
        if (random.NextDouble() < bonus) seeds = Math.Max(seeds, random.Next(0, 6));
        return (produce, seeds);
    }
    public static GeometryPoint[] Footprint(WorldPosition p) => [new(p.X-2,p.Y-1.5), new(p.X+2,p.Y-1.5), new(p.X+2,p.Y+1.5), new(p.X-2,p.Y+1.5), new(p.X-2,p.Y-1.5)];
    public static CanonicalEntity Create(string id, WorldPosition p, string crop, string house, bool farm = false) => new(id, EntityKind.ResourceNode, p, Footprint(p), new Dictionary<string,string>{["subtype"]="garden",["itemType"]=crop,["displayName"]=crop+" garden",["buildingId"]=house,["farm"]=farm?"true":"false",["healthHearts"]="20",["maximumHealthHearts"]="20"});
}
public sealed record GardenBuildOption(string Crop, int Seeds, int Wood, int Fertilizer, double SuccessChance, bool CanBuild);
public sealed record GardenBuildState(WorldPosition Center, double Radius, int CraftingLevel, bool BookBonus, IReadOnlyList<GardenBuildOption> Options);
public sealed record BuildGardenRequest(string Crop, double X, double Y);
