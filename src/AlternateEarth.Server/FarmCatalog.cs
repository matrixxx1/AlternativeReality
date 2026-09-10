using AlternateEarth.Shared;
namespace AlternateEarth.Server;
internal static class FarmCatalog
{
    public static readonly ItemConfiguration[] Items = [
        .. new[]{"Urine","Fertilizer"}.SelectMany(contents=>new[]{false,true}.Select(jar=>new ItemConfiguration((jar?"jarOf":"bottleOf")+contents,(jar?"Jar":"Bottle")+" of "+contents.ToLowerInvariant(),contents=="Urine"?"Craft into potassium nitrate at a crafting table":"Craft into fertilizer for gardens",0,0,10,100,ForSale:false,WeightPounds:jar?6:1.3)))];
    // Abstract game inventory conversions, with no real chemical processing steps.
    public static readonly CraftingRecipe[] Recipes = [
        new("bottleUrineToNitrate","Process bottled urine","potassiumNitrate",1,[new("bottleOfUrine",1)]),
        new("jarUrineToNitrate","Process jar of urine","potassiumNitrate",5,[new("jarOfUrine",1)]),
        new("bottleFertilizer","Unpack bottled fertilizer","fertilizer",1,[new("bottleOfFertilizer",1)]),
        new("jarFertilizer","Unpack jar of fertilizer","fertilizer",5,[new("jarOfFertilizer",1)]),
        new("bottleMilk","Milk for cooking (bottle)","milk",1,[new("bottleOfMilk",1)]),
        new("jarMilk","Milk for cooking (jar)","milk",5,[new("jarOfMilk",1)])];
    public static string? ReturnedContainer(string recipe)=>Recipes.Any(r=>r.Id==recipe)?recipe.StartsWith("jar")?"emptyGlassJar":"emptyGlassBottle":null;
}
