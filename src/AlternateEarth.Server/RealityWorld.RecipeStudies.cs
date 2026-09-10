using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<(string Player, string Recipe), RecipeStudy> _recipeStudies = new();

    private RecipeStudy CreateRecipeStudy(CraftingRecipe recipe)
    {
        var item = InventoryDefinition(recipe.OutputItemType);
        var utility = Math.Max(item.Damage / 10, item.MaximumPriceCents / 100_000d) + (VehicleItems.Contains(recipe.OutputItemType) ? 3 : 0);
        var ceiling = .01 + .49 / (1 + utility);
        return new(recipe.Id, 1, Math.Round(.01 + ProgressionRoll() * (ceiling - .01), 5));
    }

    public RecipeStudy? GetRecipeStudy(string playerId, string recipeId) => _recipeStudies.GetValueOrDefault((playerId, recipeId));

    public IReadOnlyList<RecipeBookEntry> GetRecipeBook(string playerId)
    {

        var supplies = CraftingSupplies(playerId);
        return CraftingCatalog.Recipes.OrderBy(recipe => recipe.Name).Select(recipe =>
        {
            var study = GetRecipeStudy(playerId, recipe.Id) ?? (NutritionCatalog.IsBasicCook(recipe)?new RecipeStudy(recipe.Id,0,1):null);
            var category = recipe.StationType == "stove" ? "Food/Water" : recipe.StationType == "sewingTable" ? "Clothing" : VehicleItems.Contains(recipe.OutputItemType) ? "Vehicles" :
                IsCraftingAmmo(recipe.OutputItemType) ? "Ammo" : InventoryDefinition(recipe.OutputItemType).Category == InventoryCategory.Weapon ? "Weapons" : "Misc";
            return new RecipeBookEntry(recipe.Id, recipe.Name, recipe.OutputItemType, category, recipe.StationType,
                study?.Count ?? 0, InventoryQuantity(playerId, CraftingCatalog.RecipeItemType(recipe.Id)),
                study is null ? 0 : CraftChance(playerId, recipe, study), recipe.RequiredLevel, study?.NextBonus ?? 0, CraftBonuses(playerId, recipe), CraftableBatches(playerId, recipe, supplies), recipe.OutputQuantity);
        }).ToArray();
    }

    // Caller holds the crafting lock. Inventory consumption and study count commit together.
    private async Task StudyRecipeCoreAsync(string playerId, CraftingRecipe recipe, int quantity, InventoryState? consumedInventory, CancellationToken token)
    {
        var prior = GetRecipeStudy(playerId, recipe.Id);
        var study = prior is null ? CreateRecipeStudy(recipe) with { Count = quantity } : prior with { Count = checked(prior.Count + quantity) };
        var experience = checked(_craftingExperience.GetValueOrDefault(playerId) + BoostCraftingExperience(playerId, quantity * CraftingCatalog.ExperiencePerNewRecipe));
        await _store.SaveRecipeStudyAsync(Configuration.Id, playerId, study, experience, consumedInventory, token);
        _recipeStudies[(playerId, recipe.Id)] = study;
        _learnedRecipes[(playerId, recipe.Id)] = 0;
        _craftingExperience[playerId] = experience;
        await AwardExperienceAsync(playerId, quantity * 30, $"Studied {recipe.Name} (copy {study.Count})", cancellationToken: token);
    }

    private async Task<PlayerState> ReadRecipeAsync(string playerId, CraftingRecipe recipe, CancellationToken token)
    {
        EnsureNotProbulatorAbducted(playerId);
        if (IsGasAsleep(playerId)) throw new InvalidOperationException("You cannot study while asleep.");
        await _craftingProgressLock.WaitAsync(token);
        try
        {
            var itemType = CraftingCatalog.RecipeItemType(recipe.Id);
            if (InventoryQuantity(playerId, itemType) < 1) throw new InvalidOperationException("You need a copy of that recipe in your inventory.");
            var inventory = GetInventoryState(playerId);
            var next = inventory with { Items = inventory.Items.Select(item => item.ItemType == itemType ? item with { Quantity = item.Quantity - 1 } : item).Where(item => item.Quantity > 0).ToArray() };
            await StudyRecipeCoreAsync(playerId, recipe, 1, next, token);
            RemoveInventory(playerId, itemType, 1);
            return _players[playerId];
        }
        finally { _craftingProgressLock.Release(); }
    }
}
