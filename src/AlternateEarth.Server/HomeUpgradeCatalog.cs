using AlternateEarth.Shared;

namespace AlternateEarth.Server;

internal static class HomeUpgradeCatalog
{
    public static readonly HomeUpgradeDefinition[] All =
    [
        new("spiceRack", "Spice rack", "stove", 7500, .03, .05),
        new("hotWaterTap", "Hot water tap", "stove", 18000, .03, .05),
        new("blender", "Blender", "stove", 12000, .03, .05),
        new("cookwareSet", "Cookware set", "stove", 25000, .05, .08),
        new("waterPurifier", "Water purifier", "stove", 35000),
        new("benchVise", "Bench vise", "weaponsBench", 15000, .05, .05),
        new("reloadingSet", "Reloading set", "weaponsBench", 40000, .07, .10),
        new("gunsmithingTools", "Gunsmithing tools", "weaponsBench", 65000, .08, .10),
        new("rollingToolChest", "Rolling tool chest", "garageWorkbench", 50000, .04, MaterialSavingChance:.05),
        new("welder", "Welder", "garageWorkbench", 85000, .08, MaterialSavingChance:.10),
        new("compressor", "Compressor", "garageWorkbench", 45000, .04, MaterialSavingChance:.05),
        new("airTools", "Air tools", "garageWorkbench", 55000, .06, MaterialSavingChance:.10)
    ];
}
