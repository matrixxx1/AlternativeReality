using System.Globalization;

namespace AlternateEarth.Shared;

public sealed record GloveBenefits(string ItemType, string Name, double ShootingSpeedBonus = 0,
    double FireResistance = 0, double AcidResistance = 0)
{
    public string Description => string.Join("; ", new[]
    {
        ShootingSpeedBonus > 0 ? $"{(ShootingSpeedBonus * 100).ToString("0", CultureInfo.InvariantCulture)}% faster shooting" : null,
        FireResistance > 0 ? $"{(FireResistance * 100).ToString("0", CultureInfo.InvariantCulture)}% less fire damage" : null,
        AcidResistance > 0 ? $"{(AcidResistance * 100).ToString("0", CultureInfo.InvariantCulture)}% less acid damage" : null
    }.OfType<string>());
}

public static class GloveCatalog
{
    public static readonly IReadOnlyDictionary<string, GloveBenefits> All = Create();
    public static readonly ItemConfiguration[] Items = All.Values.Select(g => new ItemConfiguration(
        g.ItemType, g.Name, g.Description, 0, 0, 2000, 20000, Single: true, WeightPounds: .4, StorageSection: "gloves")).ToArray();

    // Each random perk combination has its own inventory ID, so stacking, trading,
    // dropping, and Home transfers preserve the exact bonuses without rerolling them.
    private static Dictionary<string, GloveBenefits> Create()
    {
        GloveBenefits[] basic = [new("quickdrawGloves", "Quickdraw gloves", .25),
            new("fireproofGloves", "Fireproof gloves", FireResistance: .60),
            new("acidproofGloves", "Acidproof gloves", AcidResistance: .75),
            new("gunslingerGloves", "Gunslinger gloves", .40, .10),
            new("hazmatGloves", "Hazmat gloves", .10, .35, .50),
            new("workGloves", "Work gloves", 0, .15, .15)];
        var result = basic.ToDictionary(g => g.ItemType, StringComparer.OrdinalIgnoreCase);
        var random = new Random(20260909);
        for (var i = 1; i <= 18; i++)
        {
            var perks = new double[3];
            var primary = random.Next(3); perks[primary] = random.Next(2, 9) * .05;
            if (random.Next(2) == 0) perks[(primary + random.Next(1, 3)) % 3] = random.Next(1, 7) * .05;
            var id = $"luckyGloves{i:00}";
            result[id] = new(id, $"Lucky gloves #{i}", perks[0], perks[1], perks[2]);
        }
        return result;
    }

    public static bool IsGlove(string? item) => item is not null && All.ContainsKey(item);
    public static double ShootingInterval(PlayerState player) => 1 / (1 + (All.GetValueOrDefault(player.EquippedGloves ?? "none")?.ShootingSpeedBonus ?? 0));
    public static double ReduceDamage(PlayerState player, double damage, string effect)
    {
        var glove = All.GetValueOrDefault(player.EquippedGloves ?? "none");
        var resistance = effect is "acidGas" or "acid" ? glove?.AcidResistance ?? 0 :
            effect is "fire" or "napalm" or "molotovCocktail" or "flamethrower" ? glove?.FireResistance ?? 0 : 0;
        return damage * (1 - resistance);
    }
}
