namespace AlternateEarth.Shared;

public sealed record RecipeIngredient(string ItemType, int Quantity);
public sealed record CraftingRecipe(string Id, string Name, string OutputItemType, int OutputQuantity,
    IReadOnlyList<RecipeIngredient> Ingredients, string StationType = "craftingTable", int RequiredLevel = 1, string Difficulty = "Basic");
public sealed record CraftingIngredientState(string ItemType, string Name, int Required, int Available);
public sealed record CraftingRecipeState(string Id, string Name, string OutputItemType, int OutputQuantity,
    IReadOnlyList<CraftingIngredientState> Ingredients, int MaximumCraftable, int RequiredLevel = 1, string Difficulty = "Basic", double SuccessChance = 1,
    int StudyCount = 1, double BaseSuccessChance = .5, double NextStudyBonus = .05);
public sealed record RecipeStudy(string RecipeId, int Count, double InitialChance)
{
    public double BaseChance => InitialChance + .1 * (1 - Math.Pow(.5, Math.Max(0, Count - 1)));
    public double NextBonus => .05 * Math.Pow(.5, Math.Max(0, Count - 1));
}
public sealed record CraftingSkillState(int Level, int ProgressPercent, long Experience, long EarnedTowardNextLevel, long RequiredForNextLevel);
public sealed record CraftingState(string FurnitureId, IReadOnlyList<CraftingRecipeState> Recipes, CraftingSkillState? Skill = null, bool TableDestroyed = false);
public sealed record RequestCraftingRequest(string FurnitureId);
public sealed record CraftItemRequest(string FurnitureId, string RecipeId, int Quantity = 1);
