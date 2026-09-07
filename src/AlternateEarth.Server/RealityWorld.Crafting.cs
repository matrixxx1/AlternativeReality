using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<(string Player, string Recipe), byte> _learnedRecipes = new();
    private readonly SemaphoreSlim _treasureInteractionLock = new(1, 1);
    private readonly SemaphoreSlim _craftingProgressLock = new(1, 1);
    private readonly ConcurrentDictionary<string, long> _craftingExperience = new();

    public CraftingSkillState GetCraftingSkill(string playerId)
    {
        var experience = _craftingExperience.GetValueOrDefault(playerId);
        var remaining = experience;
        var level = 1;
        var required = CraftingCatalog.ExperienceForLevel(level);
        while (remaining >= required)
        {
            remaining -= required;
            required = CraftingCatalog.ExperienceForLevel(++level);
        }
        return new(level, (int)(100d * remaining / required), experience, remaining, required);
    }

    private async Task<PlayerState> ReadCraftingSkillBookAsync(string playerId, CancellationToken cancellationToken)
    {
        EnsureNotProbulatorAbducted(playerId);
        if (IsGasAsleep(playerId)) throw new InvalidOperationException("You cannot read while asleep.");
        await _craftingProgressLock.WaitAsync(cancellationToken);
        try
        {
            if (InventoryQuantity(playerId, "craftingSkillBook") < 1) throw new InvalidOperationException("You need Building shit for dummies in your backpack.");
            var inventory = GetInventoryState(playerId);
            var next = inventory with { Items = inventory.Items.Select(item => item.ItemType.Equals("craftingSkillBook", StringComparison.OrdinalIgnoreCase) ? item with { Quantity = item.Quantity - 1 } : item).Where(item => item.Quantity > 0).ToArray() };
            // Adding this level's entire cost advances exactly one level and retains earned XP.
            var experience = checked(_craftingExperience.GetValueOrDefault(playerId) + GetCraftingSkill(playerId).RequiredForNextLevel);
            await _store.SaveInventoryAndCraftingProgressAsync(next, Configuration.Id, playerId, experience, cancellationToken);
            RemoveInventory(playerId, "craftingSkillBook", 1);
            _craftingExperience[playerId] = experience;
            await AwardExperienceAsync(playerId, 50, "Read a crafting skill book", cancellationToken: cancellationToken);
            return _players[playerId];
        }
        finally { _craftingProgressLock.Release(); }
    }

    private (PlayerState Player, string AccountId, CanonicalEntity Table) ValidateCraftingAccess(string playerId, string furnitureId)
    {
        EnsureNotProbulatorAbducted(playerId);
        if (IsGasAsleep(playerId)) throw new InvalidOperationException("You cannot craft while asleep.");
        if (!_players.TryGetValue(playerId, out var player) || !_dungeons.TryGetValue(player.LocationId, out var home) || !home.IsHome)
            throw new InvalidOperationException("Use a placed crafting table inside your Home.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId) || _baseBuildings.GetValueOrDefault(accountId) != home.BuildingId)
            throw new InvalidOperationException("Visitors cannot use another player's crafting supplies.");
        var table = home.Furnishings?.FirstOrDefault(item => item.Id == furnitureId && item.Properties.GetValueOrDefault("objectType") == "craftingTable" && !IsStoredFurniture(item))
            ?? throw new InvalidOperationException("Place your crafting table in Home before using it.");
        return (player, accountId, table);
    }

    public CraftingState RequestCrafting(string playerId, string furnitureId)
    {
        var access = ValidateCraftingAccess(playerId, furnitureId);
        var skill = GetCraftingSkill(playerId);
        var supplies = GetHomeItemStorage(access.AccountId).Items.ToDictionary(item => item.ItemType, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        var recipes = CraftingCatalog.Recipes.Where(recipe => recipe.StationType == "craftingTable" && _learnedRecipes.ContainsKey((playerId, recipe.Id)))
            .Select(recipe =>
            {
                var ingredients = recipe.Ingredients.Select(item => new CraftingIngredientState(item.ItemType,
                    InventoryDefinition(item.ItemType).DisplayName, item.Quantity, supplies.GetValueOrDefault(item.ItemType))).ToArray();
                var available = ingredients.Min(item => item.Available / item.Required);
                var study = GetRecipeStudy(playerId, recipe.Id) ?? new RecipeStudy(recipe.Id, 1, .01);
                return new CraftingRecipeState(recipe.Id, recipe.Name, recipe.OutputItemType, recipe.OutputQuantity, ingredients,
                    skill.Level >= recipe.RequiredLevel ? Math.Min(99, available) : 0, recipe.RequiredLevel, recipe.Difficulty, ProgressionRules.CraftSuccess(StatsFor(playerId), study.BaseChance), study.Count, study.BaseChance, study.NextBonus);
            }).ToArray();
        return new CraftingState(furnitureId, recipes, skill);
    }

    public async Task<CraftingResult> CraftItemAsync(string playerId, CraftItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity is < 1 or > 99) throw new InvalidOperationException("Craft between 1 and 99 batches at a time.");
        var access = ValidateCraftingAccess(playerId, request.FurnitureId);
        await EnsureHomeItemStorageAsync(access.AccountId, cancellationToken);
        await _craftingProgressLock.WaitAsync(cancellationToken);
        try
        {
            await _homeFurnitureLock.WaitAsync(cancellationToken);
            try
            {
                await _homeItemStorageLock.WaitAsync(cancellationToken);
                try
                {
                    access = ValidateCraftingAccess(playerId, request.FurnitureId);
                    var recipe = CraftingCatalog.Recipes.FirstOrDefault(item => item.Id == request.RecipeId && item.StationType == "craftingTable")
                        ?? throw new InvalidOperationException("Unknown recipe.");
                    if (!_learnedRecipes.ContainsKey((playerId, recipe.Id))) throw new InvalidOperationException("Find and collect this recipe in a dungeon treasure chest first.");
                    if (GetCraftingSkill(playerId).Level < recipe.RequiredLevel)
                        throw new InvalidOperationException($"{recipe.Name} requires crafting level {recipe.RequiredLevel}.");
                    var current = _homeItemStorage[access.AccountId];
                    Dictionary<string, int> next;
                    lock (current) next = new(current, StringComparer.OrdinalIgnoreCase);
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        var required = checked(ingredient.Quantity * request.Quantity);
                        if (next.GetValueOrDefault(ingredient.ItemType) < required)
                            throw new InvalidOperationException($"Home storage needs {required} × {InventoryDefinition(ingredient.ItemType).DisplayName}.");
                    }
                    var study = GetRecipeStudy(playerId, recipe.Id) ?? new RecipeStudy(recipe.Id, 1, .01);
                    var chance = ProgressionRules.CraftSuccess(StatsFor(playerId), study.BaseChance);
                    var succeeded = 0;
                    var failed = false;
                    for (var batch = 0; batch < request.Quantity; batch++)
                    {
                        foreach (var ingredient in recipe.Ingredients) next[ingredient.ItemType] -= ingredient.Quantity;
                        if (ProgressionRoll() >= chance) { failed = true; break; }
                        succeeded++;
                    }
                    var output = checked(recipe.OutputQuantity * succeeded);
                    if (output > 0) next[recipe.OutputItemType] = checked(next.GetValueOrDefault(recipe.OutputItemType) + output);
                    var saved = new InventoryState(HomeItemStorageOwnerId(access.AccountId), next.Where(item => item.Value > 0).Select(item => InventoryStack(item.Key, item.Value)).ToArray());
                    var experience = checked(_craftingExperience.GetValueOrDefault(playerId) + (succeeded + (failed ? 1 : 0)) * CraftingCatalog.ExperiencePerBatch);
                    var furniture = failed ? _homeFurniture[access.AccountId].Where(item => item.Id != request.FurnitureId).ToList() : null;
                    await _store.SaveCraftAttemptAsync(Configuration.Id, playerId, saved, experience, access.AccountId, furniture, cancellationToken);
                    _homeItemStorage[access.AccountId] = next;
                    _craftingExperience[playerId] = experience;
                    PlayerState? damaged = null;
                    CombatEvent? explosion = null;
                    if (failed)
                    {
                        _homeFurniture[access.AccountId] = furniture!;
                        RefreshHome(access.AccountId, _baseEntities[_baseBuildings[access.AccountId]]);
                        var player = _players[playerId];
                        var health = Math.Max(0, player.HealthHearts - 1);
                        damaged = player with { HealthHearts = health, Version = player.Version + 1 };
                        if (health <= 0) damaged = await DieAndResetPlayerAsync(damaged, cancellationToken);
                        await SavePlayerAsync(damaged, cancellationToken);
                        explosion = new CombatEvent(playerId, playerId, "craftingExplosion", access.Table.Position, access.Table.Position, true, 1, health <= 0,
                            "Crafting failed! The table exploded for 1 damage.", damaged.HealthHearts);
                    }
                    await AwardExperienceAsync(playerId, succeeded * (4 + recipe.RequiredLevel / 10d) + (failed ? 1 : 0),
                        failed ? "Crafting practice and a failed experiment" : "Crafted " + recipe.Name, cancellationToken: cancellationToken);
                    var message = failed
                        ? $"Craft failed! The table exploded and dealt 1 damage. Lost the failed batch's materials; {succeeded} earlier batch(es) succeeded. Unattempted materials remain in Home storage."
                        : $"Crafted {output} × {recipe.Name} into Home storage. +{succeeded} crafting XP; level {GetCraftingSkill(playerId).Level}.";
                    return new CraftingResult(failed ? new CraftingState(request.FurnitureId, Array.Empty<CraftingRecipeState>(), GetCraftingSkill(playerId), true) : RequestCrafting(playerId, request.FurnitureId),
                        GetPrivateState(playerId), message, damaged, explosion);

                }
                finally { _homeItemStorageLock.Release(); }
            }
            finally { _homeFurnitureLock.Release(); }
        }
        finally { _craftingProgressLock.Release(); }
    }
}

public sealed record CraftingResult(CraftingState Crafting, PlayerPrivateState PrivateState, string Message, PlayerState? Player = null, CombatEvent? Explosion = null);
