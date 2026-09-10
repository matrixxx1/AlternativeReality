namespace AlternateEarth.Shared;

public static class FarmRules
{
    public const double MilkSuccessChance=.8, WrongContentsChance=.15, ProductionChance=.3, NearbyRange=50, InteractionRange=4;
    public const int MaximumProducts=5;
    public static bool IsAnimal(CanonicalEntity e)=>e.Properties.GetValueOrDefault("subtype") is "farmCow" or "farmChicken";
    public static bool IsCow(CanonicalEntity e)=>e.Properties.GetValueOrDefault("subtype")=="farmCow";
    public static CanonicalEntity Create(string id,WorldPosition p,string house,bool cow)=>new(id,EntityKind.ResourceNode,p,
        [new(p.X-(cow?1:.4),p.Y-.5),new(p.X+(cow?1:.4),p.Y-.5),new(p.X+(cow?1:.4),p.Y+.5),new(p.X-(cow?1:.4),p.Y+.5)],
        new Dictionary<string,string>{["subtype"]=cow?"farmCow":"farmChicken",["buildingId"]=house,["displayName"]=cow?"Cow":"Chicken",["productQuantity"]="0"});
}
