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
            throw new InvalidOperationException("Use a placed stove, crafting table, or garage workbench inside your Home.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId) || _baseBuildings.GetValueOrDefault(accountId) != home.BuildingId)
            throw new InvalidOperationException("Visitors cannot use another player's crafting supplies.");
        var table = home.Furnishings?.FirstOrDefault(item => item.Id == furnitureId && item.Properties.GetValueOrDefault("objectType") is ("craftingTable" or "stove" or "garageWorkbench" or "weaponsBench") && !IsStoredFurniture(item))
            ?? throw new InvalidOperationException("Place your crafting station in Home before using it.");
        if (table.Properties.GetValueOrDefault("objectType") is ("garageWorkbench" or "weaponsBench") && !InsideGarage(home.Garage, table.Position.X, table.Position.Y))
            throw new InvalidOperationException("Vehicle crafting requires a workbench in the garage.");
        return (player, accountId, table);
    }

    public CraftingState RequestCrafting(string playerId, string furnitureId)
    {
        var access = ValidateCraftingAccess(playerId, furnitureId);
        var skill = GetCraftingSkill(playerId);
        var supplies = CraftingSupplies(playerId);
        var recipes = CraftingCatalog.Recipes.Where(recipe => recipe.StationType == access.Table.Properties.GetValueOrDefault("objectType"))
            .Select(recipe =>
            {
                var ingredients = recipe.Ingredients.Select(item => new CraftingIngredientState(item.ItemType,
                    InventoryDefinition(item.ItemType).DisplayName, item.Quantity, supplies.GetValueOrDefault(item.ItemType), CraftingIngredientQuality(playerId,access.AccountId,item))).ToArray();
                var learned = _learnedRecipes.ContainsKey((playerId, recipe.Id));
                var study = GetRecipeStudy(playerId, recipe.Id) ?? new RecipeStudy(recipe.Id, 1, .01);
                return new CraftingRecipeState(recipe.Id, recipe.Name, recipe.OutputItemType, recipe.OutputQuantity, ingredients,
                    CraftableBatches(playerId, recipe, supplies), recipe.RequiredLevel, recipe.Difficulty, learned ? CraftChance(playerId, recipe, study) : 0, learned ? study.Count : 0, study.BaseChance, study.NextBonus, CraftBonuses(playerId, recipe), Learned: learned);
            }).OrderByDescending(recipe => recipe.MaximumCraftable > 0).ThenBy(recipe => recipe.RequiredLevel).ThenBy(recipe => recipe.Name).ToArray();
        return new CraftingState(furnitureId, recipes, skill, StationType: access.Table.Properties["objectType"]);
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
                    var recipe = CraftingCatalog.Recipes.FirstOrDefault(item => item.Id == request.RecipeId && item.StationType == access.Table.Properties.GetValueOrDefault("objectType"))
                        ?? throw new InvalidOperationException("This recipe requires a different crafting station: food and water use the stove, vehicles use the garage workbench, weapons and ammo use the weapons bench, and other items use the crafting table.");
                    if (!_learnedRecipes.ContainsKey((playerId, recipe.Id))) throw new InvalidOperationException("Find and collect this recipe in a dungeon treasure chest first.");
                    if (GetCraftingSkill(playerId).Level < recipe.RequiredLevel)
                        throw new InvalidOperationException($"{recipe.Name} requires crafting level {recipe.RequiredLevel}.");
                    var backpack = GetInventoryState(playerId);
                    var nextBackpack = backpack.Items.ToDictionary(i=>i.ItemType,i=>i.Quantity,StringComparer.OrdinalIgnoreCase);
                    var current = _homeItemStorage[access.AccountId];
                    Dictionary<string, int> next;
                    lock (current) next = new(current, StringComparer.OrdinalIgnoreCase);
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        var required = checked(ingredient.Quantity * request.Quantity);
                        if ((long)next.GetValueOrDefault(ingredient.ItemType) + nextBackpack.GetValueOrDefault(ingredient.ItemType) < required)
                            throw new InvalidOperationException($"Your backpack and Home storage need {required} × {InventoryDefinition(ingredient.ItemType).DisplayName}.");
                    }
                    var study = GetRecipeStudy(playerId, recipe.Id) ?? new RecipeStudy(recipe.Id, 1, .01);
                    var bonuses = CraftBonuses(playerId, recipe);
                    var bonusOutput = 0; var savedMaterials = 0;
                    var succeeded = 0;
                    var failed = false;
                    for (var batch = 0; batch < request.Quantity; batch++)
                    {
                        var chance=ProgressionRules.CraftSuccess(StatsFor(playerId),study.BaseChance+bonuses.Success+IngredientQualityBonus(playerId,recipe,next,nextBackpack));
                        var usedHome=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
                        foreach (var ingredient in recipe.Ingredients)
                        {
                            var fromHome=Math.Min(ingredient.Quantity,next.GetValueOrDefault(ingredient.ItemType));
                            usedHome[ingredient.ItemType]=fromHome;
                            next[ingredient.ItemType]=next.GetValueOrDefault(ingredient.ItemType)-fromHome;
                            nextBackpack[ingredient.ItemType]=nextBackpack.GetValueOrDefault(ingredient.ItemType)-(ingredient.Quantity-fromHome);
                        }
                        if (ProgressionRoll() >= chance) { failed = true; break; }
                        succeeded++;
                        if (bonuses.Quantity > 0 && ProgressionRoll() < bonuses.Quantity) bonusOutput++;
                        if (bonuses.Materials > 0 && ProgressionRoll() < bonuses.Materials)
                            foreach(var ingredient in recipe.Ingredients.Where(i=>i.Quantity>1))
                            {
                                var savedQuantity=Math.Max(1,(int)Math.Floor(ingredient.Quantity*.2));
                                var returnHome=Math.Min(savedQuantity,usedHome[ingredient.ItemType]);
                                next[ingredient.ItemType]+=returnHome;nextBackpack[ingredient.ItemType]+=savedQuantity-returnHome;savedMaterials+=savedQuantity;
                            }
                    }
                    var output = checked(recipe.OutputQuantity * succeeded + bonusOutput);
                    if (output > 0) next[recipe.OutputItemType] = checked(next.GetValueOrDefault(recipe.OutputItemType) + output);
                    var saved = new InventoryState(HomeItemStorageOwnerId(access.AccountId), next.Where(item => item.Value > 0).Select(item => InventoryStack(item.Key, item.Value, HomeItemStorageOwnerId(access.AccountId))).ToArray());
                    var experience = checked(_craftingExperience.GetValueOrDefault(playerId) + (succeeded + (failed ? 1 : 0)) * CraftingCatalog.ExperiencePerBatch);
                    var furniture = failed ? _homeFurniture[access.AccountId].Where(item => item.Id != request.FurnitureId).ToList() : null;
                    var savedBackpack=backpack with {Items=backpack.Items.Where(i=>nextBackpack.GetValueOrDefault(i.ItemType)>0).Select(i=>i with {Quantity=nextBackpack[i.ItemType]}).ToArray()};
                    await _store.SaveCraftAttemptAsync(Configuration.Id, playerId, saved, experience, access.AccountId, furniture, cancellationToken, savedBackpack);
                    _inventories[playerId]=nextBackpack;
                    foreach(var ingredient in recipe.Ingredients.Where(i=>nextBackpack.GetValueOrDefault(i.ItemType)<=0)) _weaponQualities.TryRemove((playerId,ingredient.ItemType),out _);
                    _homeItemStorage[access.AccountId] = next;
                    foreach(var ingredient in recipe.Ingredients.Where(i=>next.GetValueOrDefault(i.ItemType)<=0)) _weaponQualities.TryRemove((HomeItemStorageOwnerId(access.AccountId),ingredient.ItemType),out _);
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
                            "Crafting failed! The station exploded for 1 damage.", damaged.HealthHearts);
                    }
                    if (!failed)
                    {
                        var currentPlayer=_players[playerId];var normalized=NormalizeEquipmentAfterInventoryChange(currentPlayer);
                        if (normalized!=currentPlayer){await SavePlayerAsync(normalized,cancellationToken);damaged=_players[playerId];}
                    }
                    await AwardExperienceAsync(playerId, succeeded * (4 + recipe.RequiredLevel / 10d) + (failed ? 1 : 0),
                        failed ? "Crafting practice and a failed experiment" : "Crafted " + recipe.Name, cancellationToken: cancellationToken);
                    var message = failed
                        ? $"Craft failed! The station exploded and dealt 1 damage. Lost the failed batch's materials; {succeeded} earlier batch(es) succeeded. Unattempted materials remain in your backpack and Home storage."
                        : $"Crafted {output} × {recipe.Name} into Home storage. +{succeeded} crafting XP; level {GetCraftingSkill(playerId).Level}.";
                    if(bonusOutput>0) message += $" Upgrades added {bonusOutput} free bonus item(s).";
                    if(savedMaterials>0) message += $" Garage upgrades saved {savedMaterials} ingredient item(s).";
                    return new CraftingResult(failed ? new CraftingState(request.FurnitureId, Array.Empty<CraftingRecipeState>(), GetCraftingSkill(playerId), true, access.Table.Properties["objectType"]) : RequestCrafting(playerId, request.FurnitureId),
                        GetPrivateState(playerId), message, damaged, explosion, recipe.OutputItemType is "water" or "purifiedWater" ? "pour" : recipe.StationType is "garageWorkbench" or "weaponsBench" ? "craftMetal" : "craft");

                }
                finally { _homeItemStorageLock.Release(); }
            }
            finally { _homeFurnitureLock.Release(); }
        }
        finally { _craftingProgressLock.Release(); }
    }
}

public sealed record CraftingResult(CraftingState Crafting, PlayerPrivateState PrivateState, string Message, PlayerState? Player = null, CombatEvent? Explosion = null, string? Sound = null);
