using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private Dictionary<string, int> CraftingSupplies(string playerId)
    {
        var supplies = GetInventoryState(playerId).Items.ToDictionary(i=>i.ItemType,i=>i.Quantity,StringComparer.OrdinalIgnoreCase);
        if (_playerAccounts.TryGetValue(playerId,out var account))
            foreach (var item in GetHomeItemStorage(account).Items) supplies[item.ItemType]=checked(supplies.GetValueOrDefault(item.ItemType)+item.Quantity);
        return supplies;
    }

    private int CraftableBatches(string playerId, CraftingRecipe recipe, IReadOnlyDictionary<string,int> supplies) =>
        (!NutritionCatalog.IsBasicCook(recipe)&&!_learnedRecipes.ContainsKey((playerId,recipe.Id))) || GetCraftingSkill(playerId).Level<recipe.RequiredLevel ? 0 :
        recipe.Ingredients.Min(i=>supplies.GetValueOrDefault(i.ItemType)/i.Quantity);

    private string? CraftingIngredientQuality(string playerId, string account, RecipeIngredient ingredient)
    {
        var stored=GetHomeItemStorage(account).Items.FirstOrDefault(i=>i.ItemType==ingredient.ItemType);
        var carried=GetInventoryState(playerId).Items.FirstOrDefault(i=>i.ItemType==ingredient.ItemType);
        var qualities=new List<string>();
        if(stored?.Quantity>0)qualities.Add(stored.Quality??"Common");
        if((stored?.Quantity??0)<ingredient.Quantity&&carried?.Quantity>0)qualities.Add(carried.Quality??"Common");
        return qualities.Count==0?null:string.Join(" / ",qualities.Distinct());
    }

    // Home ingredients are used first, then the backpack. Count quality only for the units actually used.
    private double IngredientQualityBonus(string playerId, CraftingRecipe recipe,
        IReadOnlyDictionary<string,int>? homeRemaining = null, IReadOnlyDictionary<string,int>? backpackRemaining = null)
    {
        var home = _playerAccounts.TryGetValue(playerId,out var account) ? GetHomeItemStorage(account).Items.ToDictionary(i=>i.ItemType,StringComparer.OrdinalIgnoreCase) : new Dictionary<string,ItemStack>();
        var backpack = GetInventoryState(playerId).Items.ToDictionary(i=>i.ItemType,StringComparer.OrdinalIgnoreCase);
        var total=0d;
        foreach(var ingredient in recipe.Ingredients)
        {
            var stored=home.GetValueOrDefault(ingredient.ItemType);var carried=backpack.GetValueOrDefault(ingredient.ItemType);
            var fromHome=Math.Min(ingredient.Quantity,homeRemaining?.GetValueOrDefault(ingredient.ItemType)??stored?.Quantity??0);
            var fromPack=Math.Min(ingredient.Quantity-fromHome,backpackRemaining?.GetValueOrDefault(ingredient.ItemType)??carried?.Quantity??0);
            total+=fromHome*IngredientQualityModifier(stored?.Quality)+fromPack*IngredientQualityModifier(carried?.Quality);
        }
        return total/recipe.Ingredients.Sum(i=>i.Quantity);
    }
}
