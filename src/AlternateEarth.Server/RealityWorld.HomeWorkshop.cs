using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<string, HomeWorkshopProgress> _homeWorkshops = new();

    private HomeWorkshopProgress WorkshopFor(string account) => _homeWorkshops.GetValueOrDefault(account) ?? new([]);
    private HomeWorkshopView? WorkshopView(string playerId) => _playerAccounts.TryGetValue(playerId, out var account)
        ? new(HomeUpgradeCatalog.All, WorkshopFor(account), GetHomeItemStorage(account).Items.Where(i=>i.ItemType=="waterFilter").Sum(i=>i.Quantity)) : null;

    private string ValidateWorkshopAccess(string playerId)
    {
        EnsureNotProbulatorAbducted(playerId);
        if (IsGasAsleep(playerId)) throw new InvalidOperationException("You cannot use Home upgrades while asleep.");
        if (!_players.TryGetValue(playerId,out var player) || !_dungeons.TryGetValue(player.LocationId,out var home) || !home.IsHome ||
            !_playerAccounts.TryGetValue(playerId,out var account) || _baseBuildings.GetValueOrDefault(account)!=home.BuildingId)
            throw new InvalidOperationException("Use upgrades inside your own Home.");
        return account;
    }

    private bool IsCraftingAmmo(string itemType) => _itemConfigurations.Values.Any(item=>item.AmmoType==itemType && item.ItemType!=itemType);
    private CraftingBonuses CraftBonuses(string playerId, CraftingRecipe recipe)
    {
        if (!_playerAccounts.TryGetValue(playerId,out var account)) return new();
        var installed = WorkshopFor(account).Installed;
        var upgrades = HomeUpgradeCatalog.All.Where(u=>u.StationType==recipe.StationType && installed.Contains(u.Id)).ToArray();
        return new(upgrades.Sum(u=>u.SuccessBonus), recipe.StationType=="stove" || IsCraftingAmmo(recipe.OutputItemType) ? upgrades.Sum(u=>u.BonusQuantityChance) : 0,
            upgrades.Sum(u=>u.MaterialSavingChance), IngredientQualityBonus(playerId,recipe));
    }

    internal static double IngredientQualityModifier(string? quality) => NormalizeWeaponQuality(quality) switch
    {
        "Crude" => -.15, "Poor" => -.10, "Worn" => -.05, "Fine" => .05, "Superior" => .10,
        "Masterwork" => .15, "Epic" => .20, "Legendary" => .25, "Godly" => .30, _ => 0
    };

    private double CraftChance(string playerId, CraftingRecipe recipe, RecipeStudy study)
    {
        if(NutritionCatalog.IsBasicCook(recipe))return 1;
        var bonuses=CraftBonuses(playerId,recipe);
        return ProgressionRules.CraftSuccess(StatsFor(playerId),study.BaseChance+bonuses.Success+bonuses.IngredientQuality);
    }

    public async Task<PlayerState> BuyHomeUpgradeAsync(string playerId, string upgradeId, CancellationToken token = default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            await _craftingProgressLock.WaitAsync(token);
            try
            {
                var account=ValidateWorkshopAccess(playerId);var current=WorkshopFor(account);
                var upgrade=HomeUpgradeCatalog.All.SingleOrDefault(u=>u.Id==upgradeId) ?? throw new InvalidOperationException("Unknown Home upgrade.");
                if(current.Installed.Contains(upgradeId))throw new InvalidOperationException("This permanent upgrade is already installed.");
                var player=_players[playerId];var cost=player.GodMode?0:upgrade.PriceCents;
                if(player.WalletCents<cost)throw new InvalidOperationException("You do not have enough money for this Home upgrade.");
                var next=current with{Installed=[..current.Installed,upgradeId],FilterUsesRemaining=upgradeId=="waterPurifier"?50:current.FilterUsesRemaining};
                var paid=player with{WalletCents=player.WalletCents-cost,Version=player.Version+1};
                await _store.SaveHomeWorkshopAsync(Configuration.Id,account,next,purchase:paid,token:token);
                _homeWorkshops[account]=next;_players[playerId]=paid;return paid;
            }
            finally{_craftingProgressLock.Release();}
        }
        finally{_treasureInteractionLock.Release();}
    }

    public async Task UseWaterPurifierAsync(string playerId, bool replaceFilter, CancellationToken token = default)
    {
        await _craftingProgressLock.WaitAsync(token);
        try
        {
            var account=ValidateWorkshopAccess(playerId);var current=WorkshopFor(account);
            if(!current.Installed.Contains("waterPurifier"))throw new InvalidOperationException("Buy a water purifier for your Home first.");
            await _homeItemStorageLock.WaitAsync(token);
            try
            {
                var stored=_homeItemStorage[account];Dictionary<string,int> next;
                lock(stored)next=new(stored,StringComparer.OrdinalIgnoreCase);
                if(replaceFilter)
                {
                    if(current.FilterUsesRemaining>0)throw new InvalidOperationException("Use the current filter before replacing it.");
                    if(next.GetValueOrDefault("waterFilter")<1)throw new InvalidOperationException("Put a water filter in Home storage first.");
                    next["waterFilter"]--;current=current with{FilterUsesRemaining=50};
                }
                else
                {
                    if(current.FilterUsesRemaining<1)throw new InvalidOperationException("The purifier needs a new filter after 50 uses.");
                    next["purifiedWater"]=checked(next.GetValueOrDefault("purifiedWater")+1);current=current with{FilterUsesRemaining=current.FilterUsesRemaining-1};
                }
                var saved=new InventoryState(HomeItemStorageOwnerId(account),next.Where(i=>i.Value>0).Select(i=>InventoryStack(i.Key,i.Value,HomeItemStorageOwnerId(account))).ToArray());
                await _store.SaveHomeWorkshopAsync(Configuration.Id,account,current,inventory:saved,token:token);
                _homeItemStorage[account]=next;_homeWorkshops[account]=current;
            }
            finally{_homeItemStorageLock.Release();}
        }
        finally{_craftingProgressLock.Release();}
    }
}
