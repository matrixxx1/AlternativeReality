using AlternateEarth.Shared;

namespace AlternateEarth.Server;

internal static class CraftingCatalog
{
    public const int ExperiencePerBatch = 1;
    public const int ExperiencePerNewRecipe = 25;
    public const double LevelExperienceGrowth = 1.005;

    public static long ExperienceForLevel(int level) => (long)Math.Min(long.MaxValue / 4d,
        Math.Ceiling(100 * Math.Pow(LevelExperienceGrowth, Math.Max(0, level - 1))));

    // Invented inventory-token costs for game balance, never physical measurements or instructions.
    private static readonly CraftingRecipe[] BaseRecipes =
    [
        new("gunpowder", "Gunpowder", "gunpowder", 1,
            [new("powderBase", 1), new("sparkBinder", 1)]),
        new("molotovCocktail", "Molotov cocktail", "molotovCocktail", 1,
            [new("emberGel", 1), new("emptyGlassBottle", 1), new("cloth", 1)]),
        new("bullet", "Bullets", "bullet", 6, [new("metal", 1), new("gunpowder", 1)]),
        new("arrow", "Arrows", "arrow", 4, [new("wood", 1), new("metal", 1), new("cloth", 1)]),
        new("ballBearing", "Ball bearings", "ballBearing", 10, [new("metal", 1)]),
        new("rocket", "Rocket", "rocket", 1, [new("metal", 2), new("weaponParts", 1), new("powerCore", 1)]),
        new("hockeyStick", "Hockey stick", "hockeyStick", 1, [new("wood", 3)]),
        new("mapleSyrup", "Maple syrup", "mapleSyrup", 1, [new("sugar", 2), new("water", 1)]),
        new("knife", "Knife", "knife", 1, [new("metal", 2), new("wood", 1)]),
        new("sword", "Sword", "sword", 1, [new("metal", 4), new("wood", 1), new("cloth", 1)]),
        new("slingshot", "Slingshot", "slingshot", 1, [new("wood", 1), new("rubber", 1), new("cloth", 1)]),
        new("crossbow", "Crossbow", "crossbow", 1, [new("wood", 3), new("metal", 2), new("mechanicalParts", 1)]),
        new("pistol", "Pistol", "pistol", 1, [new("metal", 3), new("weaponParts", 2), new("mechanicalParts", 1)]),
        new("rifle", "Rifle", "rifle", 1, [new("wood", 2), new("metal", 4), new("weaponParts", 3)]),
        new("grenade", "Grenade", "grenade", 1, [new("metal", 2), new("weaponParts", 1), new("powerCore", 1)]),
        new("shield", "Shield", "shield", 1, [new("metal", 3), new("wood", 2), new("cloth", 1)]),
        new("flashlight", "Flashlight", "flashlight", 1, [new("plastic", 2), new("electronics", 1), new("battery", 1)]),
        new("lantern", "Lantern", "lantern", 1, [new("metal", 2), new("glassScrap", 1), new("electronics", 1), new("battery", 1)]),
        new("candle", "Candles", "candle", 3, [new("wax", 1), new("cloth", 1)]),
        new("lockPickSet", "Lock pick set", "lockPickSet", 1, [new("metal", 2), new("mechanicalParts", 1)]),
        new("emptyGlassBottle", "Empty glass bottles", "emptyGlassBottle", 2, [new("glassScrap", 1)]),
        new("emptyGlassJar", "Empty glass jar", "emptyGlassJar", 1, [new("glassScrap", 2)]),
        new("salvageCloth", "Salvaged cloth", "cloth", 1, [new("crustySocks", 1)]),
        new("charcoal", "Charcoal briquettes", "charcoal", 1, [new("wood", 2)]),
        new("hat", "Hat", "hat", 1, [new("cloth", 2)]),
        new("tShirt", "T-shirt", "tShirt", 1, [new("cloth", 3)]),
        new("food", "Food rations", "food", 2, [new("flour", 2), new("cookingOil", 1), new("water", 1)]),
        new("skateboard", "Skateboard", "skateboard", 1, [new("wood", 6), new("plastic", 4), new("metal", 2), new("rubber", 2)]),
        new("rocketLauncher", "Rocket launcher", "rocketLauncher", 1, [new("metal", 20), new("weaponParts", 12), new("mechanicalParts", 8), new("powerCore", 2)]),
        new("motorcycle", "Motorcycle", "motorcycle", 1, [new("metal", 80), new("plastic", 35), new("rubber", 20), new("mechanicalParts", 12), new("electronics", 6), new("battery", 2)]),
        new("ufo", "UFO", "ufo", 1, [new("metal", 500), new("plastic", 200), new("electronics", 150), new("mechanicalParts", 100), new("powerCore", 50), new("kryptonite", 1)]),
        .. HazardCatalog.Recipes
    ];

    public static readonly CraftingRecipe[] Recipes = BaseRecipes.Select(recipe =>
    {
        // Broad game difficulty and destructive potential; no real manufacturing details.
        var level = recipe.Id switch
        {
            "hockeyStick" or "mapleSyrup" or "molotovCocktail" or "salvageCloth" or "charcoal" or "food" or "candle" => 1,
            "hat" => 3,
            "gunpowder" or "tShirt" or "slingshot" or "ballBearing" => 5,
            "arrow" => 8,
            "knife" or "emptyGlassBottle" => 10,
            "emptyGlassJar" => 12,
            "bullet" or "flashlight" or "chloramineGasBottle" => 15,
            "lantern" or "shield" or "chloramineGasJar" or "chlorineGasBottle" or "peraceticAcidGasBottle" => 20,
            "sword" or "lockPickSet" or "skateboard" or "chlorineGasJar" or "peraceticAcidGasJar" or "napalmBottle" => 25,
            "chloroformGasBottle" or "napalmJar" => 30,
            "crossbow" or "grenade" or "chloroformGasJar" => 35,
            "pistol" => 40,
            "rifle" or "rocket" => 45,
            "rocketLauncher" => 50,
            "motorcycle" => 100,
            "ufo" => 5_000,
            _ => throw new InvalidOperationException($"Assign a crafting level to {recipe.Id}.")
        };
        var difficulty = level >= 5_000 ? "Extraterrestrial" : level >= 100 ? "Expert" : level >= 40 ? "Advanced" : level >= 15 ? "Intermediate" : "Basic";
        return recipe with { RequiredLevel = level, Difficulty = difficulty };
    }).ToArray();

    public static string RecipeItemType(string id) => $"recipe:{id}";
    public static CraftingRecipe? RecipeFromItem(string itemType) =>
        Recipes.FirstOrDefault(recipe => RecipeItemType(recipe.Id).Equals(itemType, StringComparison.OrdinalIgnoreCase));

    public static readonly string[] ChemicalItems =
        ["charcoal", "potassiumNitrate", "salt", "pepper", "sulfur", "bleach", "ammonia", "sulfuricAcid", "cookingOil", "flour", "sugar", "cookingSupplies", "laundryDetergent", .. HazardCatalog.FictionalReagents];
    public static readonly string[] LitterItems =
        ["pencil", "pen", "marker", "newspaper", "emptyGlassBottle", "emptyGlassJar", "emptyPlasticBottle", "crustySocks", "soiledUnderwear", "areaMap", "paper", "wood", "cloth", "plastic", "styrofoam", "metal", "drugs", "glassScrap", "rubber", "mechanicalParts", "electronics", "battery"];

    private static ItemConfiguration Material(string id, string name, double weight, long min, long max, bool forSale = true) =>
        new(id, name, "Scavenging and crafting inventory item", 0, 0, min, max, forSale, WeightPounds: weight);

    public static readonly ItemConfiguration[] Materials =
    [
        Material("kryptonite", "Kryptonite", .5, 500_000, 1_000_000, false),
        new("craftingSkillBook", "Building shit for dummies", "Read once to gain one full crafting level", 0, 0, 10_000, 30_000, false, WeightPounds: .25),
        Material("glassScrap", "Glass scraps", .2, 10, 75),
        Material("rubber", "Rubber scraps", .15, 25, 150),
        Material("mechanicalParts", "Mechanical parts", .3, 100, 600),
        Material("electronics", "Electronic parts", .2, 200, 1_000),
        Material("battery", "Battery", .2, 100, 500),
        Material("wax", "Wax", .15, 50, 250),
        Material("weaponParts", "Generic weapon parts", .5, 500, 2_000),
        new("powerCore", "Power core", "Fictional game component", 0, 0, 400, 1_500, WeightPounds: .25),
        Material("emptyGlassBottle", "Empty glass bottle", .4, 25, 100),
        Material("emptyPlasticBottle", "Empty plastic bottle", .05, 5, 25, false),
        Material("crustySocks", "Crusty socks", .15, 1, 10, false),
        Material("soiledUnderwear", "Poop-covered underwear", .2, 1, 5, false),
        Material("paper", "Scrap paper", .05, 5, 50),
        Material("cloth", "Cloth", .2, 25, 200),
        Material("plastic", "Scrap plastic", .15, 5, 100),
        Material("styrofoam", "Styrofoam", .05, 5, 50),
        Material("drugs", "Unidentified drugs", .1, 100, 1_000, false),
        Material("charcoal", "Charcoal briquettes", .25, 100, 600),
        Material("potassiumNitrate", "Potassium nitrate (saltpeter)", .25, 200, 900),
        Material("salt", "Salt", .1, 50, 200),
        Material("pepper", "Pepper", .1, 100, 400),
        Material("sulfur", "Sulfur", .25, 150, 700),
        Material("bleach", "Bleach", .25, 150, 500),
        Material("ammonia", "Ammonia", .25, 150, 500),
        Material("sulfuricAcid", "Sulfuric acid", .25, 500, 1_500),
        Material("cookingOil", "Cooking oil", .25, 150, 600),
        Material("flour", "Flour", .25, 100, 400),
        Material("sugar", "Sugar", .25, 100, 400),
        Material("cookingSupplies", "Cooking supplies", .5, 300, 1_200),
        new("craftingGas", "Gas supply", "Abstract crafting token, separate from vehicle fuel", 0, 0, 100, 400, WeightPounds: .25),
        new("gunpowder", "Gunpowder", "Crafted game resource for future recipes", 0, 0, 300, 1_000, false, WeightPounds: .25)
    ];

    public static IEnumerable<ItemConfiguration> RecipeItems => Recipes.Select(recipe =>
        new ItemConfiguration(RecipeItemType(recipe.Id), $"Recipe: {recipe.Name}", "Collect from dungeon treasure to learn permanently", 0, 0, 0, 0, false, WeightPounds: 0));
}
