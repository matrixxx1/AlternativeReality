using AlternateEarth.Shared;

namespace AlternateEarth.Server;

internal sealed record HazardDefinition(string ItemType, string Name, string Effect, int DurationSeconds,
    double DamagePerSecond, int SleepSeconds, string Reagent, string Container);

internal static class HazardCatalog
{
    // Fictional reagents only: these are gameplay effects, not chemical preparation recipes.
    public static readonly HazardDefinition[] Weapons = BuildWeapons();
    private static HazardDefinition[] BuildWeapons()
    {
        var result = new List<HazardDefinition>();
        foreach (var jar in new[] { false, true })
        {
            var suffix = jar ? "Jar" : "Bottle";
            var container = jar ? "emptyGlassJar" : "emptyGlassBottle";
            var label = jar ? "Jar" : "Bottle";
            var duration = jar ? 20 : 10;
            result.Add(new($"chloramineGas{suffix}", $"{label} of chloramine gas", "gas", duration, 1, 0, "stingingEssence", container));
            result.Add(new($"chlorineGas{suffix}", $"{label} of chlorine gas", "gas", duration, 1, 0, "noxiousEssence", container));
            result.Add(new($"chloroformGas{suffix}", $"{label} of chloroform gas", "sleepGas", duration, 0, 10, "dreamEssence", container));
            result.Add(new($"peraceticAcidGas{suffix}", $"{label} of peracetic acid gas", "acidGas", duration, .5, 0, "causticEssence", container));
            result.Add(new($"napalm{suffix}", $"{label} of napalm", "napalm", jar ? 80 : 40, jar ? 8 : 4, 0, "emberGel", container));
        }
        return result.ToArray();
    }

    public static HazardDefinition? Find(string itemType) => Weapons.FirstOrDefault(item => item.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase));
    public static IEnumerable<ItemConfiguration> Items => Weapons.Select(item =>
        new ItemConfiguration(item.ItemType, item.Name, $"Throwable game effect: {item.DurationSeconds} seconds; {(item.SleepSeconds > 0 ? "10-second sleep, once per cloud" : $"{item.DamagePerSecond:0.##} hearts per second")}",
            0, 25, 500, 2_500, false, false, item.ItemType, WeightPounds: .75, Category: InventoryCategory.Weapon, Accuracy: 1, AttackIntervalSeconds: 1));
    public static IEnumerable<CraftingRecipe> Recipes => Weapons.Select(item =>
        new CraftingRecipe(item.ItemType, item.Name, item.ItemType, 1,
            [new(item.Reagent, 1), new(item.Effect == "napalm" ? "bindingResin" : "cloudBinder", 1), new(item.Container, 1)]));
    public static readonly string[] FictionalReagents = ["stingingEssence", "noxiousEssence", "dreamEssence", "causticEssence", "emberGel", "bindingResin", "cloudBinder", "powderBase", "sparkBinder"];
    public static IEnumerable<ItemConfiguration> Materials => FictionalReagents.Select(id =>
        new ItemConfiguration(id, FurnitureCatalog.Title(id), "Fictional game reagent", 0, 0, 100, 700, WeightPounds: .1))
        .Concat(new[] { new ItemConfiguration("emptyGlassJar", "Empty glass jar", "Crafting container", 0, 0, 50, 150, WeightPounds: .5),
            new ItemConfiguration("laundryDetergent", "Laundry detergent", "Household supply", 0, 0, 200, 600, WeightPounds: .25) });
}
