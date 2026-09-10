namespace AlternateEarth.Shared;

public sealed record HomeUpgradeDefinition(string Id, string Name, string StationType, long PriceCents,
    double SuccessBonus = 0, double BonusQuantityChance = 0, double MaterialSavingChance = 0);
public sealed record HomeWorkshopProgress(IReadOnlyList<string> Installed, int FilterUsesRemaining = 0);
public sealed record HomeWorkshopView(IReadOnlyList<HomeUpgradeDefinition> Catalog, HomeWorkshopProgress Progress, int StoredFilters);
public sealed record CraftingBonuses(double Success = 0, double Quantity = 0, double Materials = 0, double IngredientQuality = 0);
