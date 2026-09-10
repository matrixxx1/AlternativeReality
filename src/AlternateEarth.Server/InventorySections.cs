using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public static class InventorySections
{
    private static readonly HashSet<string> Materials = CraftingCatalog.Materials.Concat(HazardCatalog.Materials)
        .Select(i => i.ItemType).Concat(CraftingCatalog.Recipes.SelectMany(r => r.Ingredients).Select(i => i.ItemType))
        .Concat(["wood", "metal", "kindling", "craftingSkillBook"]).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string Section(ItemConfiguration item)
    {
        if (item.ItemType.StartsWith("seed:") || item.ItemType is "fertilizer" or "gardeningBook") return "crafting";
        if (GloveCatalog.IsGlove(item.ItemType)) return "gloves";
        if (item.Nutrition is not null || NutritionCatalog.Foods.ContainsKey(item.ItemType) ||
            item.ItemType is "food" or "water" or "dirtyWater" or "purifiedWater" or "energyDrink" or "mapleSyrup") return "food";
        if (item.ItemType.StartsWith("recipe:", StringComparison.OrdinalIgnoreCase) || Materials.Contains(item.ItemType)) return "crafting";
        return "misc";
    }
}
