using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private async Task<PlayerState> CreditTreasureMoneyAsync(string playerId, long cents, CancellationToken token)
    {
        // Damage and movement can advance the player revision while a wallet save waits.
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var player = _players[playerId];
            if (cents == 0) return player;
            var updated = player with { WalletCents = checked(player.WalletCents + cents), Version = player.Version + 1 };
            if (await SavePlayerAsync(updated, token)) return updated;
        }
    }

    public async Task<NearbyTreasureState> OpenNearbyTreasureAsync(string playerId, string sourceId, bool chest, CancellationToken token = default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            if (chest) ValidateTreasureChest(playerId, sourceId); else OpenLoot(playerId, sourceId);
            return await ReadNearbyTreasureAsync(playerId, sourceId, token);
        }
        finally { _treasureInteractionLock.Release(); }
    }

    private async Task<NearbyTreasureState> ReadNearbyTreasureAsync(string playerId, string anchorId, CancellationToken token, LootDropState? rewardAnchor = null)
    {
        var player = _players[playerId];
        var anchor = _loot.GetValueOrDefault(anchorId) ?? rewardAnchor;
        var sources = new List<TreasureSource>();
        var cash = 0L;
        var removed = new List<string>();
        var changed = new List<LootDropState>();
        foreach (var loot in _loot.Values.Where(l => l.ExpiresAtUtc > DateTimeOffset.UtcNow &&
            (l.DropKind != "eventReward" || l.OwnerId == playerId) &&
            ((l.LocationId == player.LocationId && l.Position.Distance2D(player.Position) <= 4) ||
                (l.DropKind == "eventReward" && l.OwnerId == playerId && anchor is { DropKind: "eventReward" } &&
                    l.LocationId == anchor.LocationId && l.Position.Distance2D(anchor.Position) <= 4))).OrderBy(l => l.Id).ToArray())
        {
            cash += loot.MoneyCents;
            var taken = loot.MoneyCents == 0 && loot.Items.Count > 0
                ? new LootTakeResult(_players[playerId], OpenLoot(playerId, loot.Id), "")
                : await TakeLootItemsCoreAsync(playerId, new(loot.Id, Array.Empty<PurchaseLine>()), token);
            if (taken.Remaining is { } remaining) sources.Add(new(remaining.Id, false, remaining.Items));
            if (taken.Remaining is null) removed.Add(loot.Id);
            else if (loot.DropKind != "eventReward") changed.Add(taken.Remaining);
        }
        var chests = player.LocationId == "outdoor" ? _outdoorChests.Values.ToArray() :
            _dungeons.GetValueOrDefault(player.LocationId)?.Chests.ToArray() ?? [];
        foreach (var item in chests.Where(c => c.Position.Distance2D(player.Position) <= 4 &&
            (c.ExpiresAtUtc is null || c.ExpiresAtUtc > DateTimeOffset.UtcNow)).OrderBy(c => c.Id))
        {
            if (_dungeons.GetValueOrDefault(player.LocationId) is { Underwater: not null } dive &&
                dive.Actors.Any(actor => actor.FactionId == item.Id)) continue;
            var walletBefore = _players[playerId].WalletCents;
            var opened = await OpenChestCoreAsync(playerId, item.Id, token);
            cash += opened.Player.WalletCents - walletBefore;
            // OpenChest already credits cash. Keep its items in the same selection.
            sources.Add(new(item.Id, true, opened.Contents.Items));
        }
        return new(anchorId, sources, sources.SelectMany(s => s.Items).GroupBy(i => i.ItemType, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First() with { Quantity = g.Sum(i => i.Quantity), Quality = g.Select(i => i.Quality).Distinct().Count() == 1 ? g.First().Quality : null }).ToArray(),
            cash > 0 ? $"Collected {cash / 100m:C} cash. Choose items from nearby treasure (within 4 meters)." : "Money collected automatically. Choose items from nearby treasure (within 4 meters).",
            _players[playerId], changed, removed, anchor?.DropKind == "eventReward" || sources.Any(s => _loot.GetValueOrDefault(s.Id)?.DropKind == "eventReward"));
    }

    public async Task<NearbyTreasureState> TakeNearbyTreasureAsync(string playerId, TakeNearbyTreasureRequest request, CancellationToken token = default)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try { return await TakeNearbyTreasureCoreAsync(playerId, request, token); }
        finally { _treasureInteractionLock.Release(); }
    }

    private async Task<NearbyTreasureState> TakeNearbyTreasureCoreAsync(string playerId, TakeNearbyTreasureRequest request, CancellationToken token)
    {
            if (request.Sources is null || request.Items is null || request.Sources.Length == 0 ||
                request.Items.Any(i => string.IsNullOrWhiteSpace(i.ItemType) || i.Quantity is < 1 or > 100_000))
                throw new InvalidOperationException("Choose valid treasure and quantities.");
            var sources = new List<TreasureSource>();
            var rewardAnchor = _loot.GetValueOrDefault(request.AnchorId);
            foreach (var source in request.Sources.DistinctBy(s => (s.Id, s.Chest)))
            {
                if (source.Chest)
                {
                    ValidateTreasureChest(playerId, source.Id);
                    if (!_chestContents.TryGetValue(source.Id, out var contents)) throw new InvalidOperationException("Reopen the treasure to refresh its contents.");
                    sources.Add(new(source.Id, true, contents.Items));
                }
                else sources.Add(new(source.Id, false, OpenLoot(playerId, source.Id).Items));
            }
            rewardAnchor ??= sources.Select(s => _loot.GetValueOrDefault(s.Id)).FirstOrDefault(l => l?.DropKind == "eventReward");
            var requested = request.Items.GroupBy(i => i.ItemType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity), StringComparer.OrdinalIgnoreCase);
            foreach (var line in requested)
                if (line.Value > sources.SelectMany(s => s.Items).Where(i => i.ItemType.Equals(line.Key, StringComparison.OrdinalIgnoreCase)).Sum(i => (long)i.Quantity))
                    throw new InvalidOperationException("Treasure changed. Reopen it to refresh the available quantities.");
            var additions = requested.Select(i => InventoryStack(i.Key, checked((int)i.Value))).ToArray();
            if (additions.Length > 0 && !CanAddToBackpack(playerId, additions, out var message)) throw new InvalidOperationException(message);
            foreach (var source in sources)
            {
                var lines = new List<PurchaseLine>();
                foreach (var group in source.Items.GroupBy(i => i.ItemType, StringComparer.OrdinalIgnoreCase))
                {
                    var count = (int)Math.Min(group.Sum(i => (long)i.Quantity), requested.GetValueOrDefault(group.Key));
                    if (count > 0) lines.Add(new(group.Key, count));
                    requested[group.Key] = requested.GetValueOrDefault(group.Key) - count;
                }
                if (source.Chest && lines.Count > 0) await TakeChestItemsCoreAsync(playerId, new(source.Id, lines), token);
                else if (!source.Chest) await TakeLootItemsCoreAsync(playerId, new(source.Id, lines), token);
            }
            var result = await ReadNearbyTreasureAsync(playerId, request.AnchorId, token, rewardAnchor);
            result = result with { Message = result.Items.Count == 0 ? "Everything collected. Your treasure is in your inventory." : "Selected items collected. Choose any remaining treasure." };
            return result with { RemovedLoot = result.RemovedLoot.Concat(sources.Where(s => !s.Chest && !_loot.ContainsKey(s.Id)).Select(s => s.Id)).Distinct().ToArray() };
    }

    private static int TreasureQualityRank(string? quality) => Array.IndexOf(WeaponQualityNames, NormalizeWeaponQuality(quality) ?? "Common");

    private bool IsTreasureGear(string item) => InventoryDefinition(item).Category == InventoryCategory.Weapon ||
        HatItems.Contains(item) || GloveItems.Contains(item) || ShirtItems.Contains(item) || PantsItems.Contains(item) || OffhandItems.Contains(item) ||
        VehicleItems.Contains(item) || item is "magicHikingShoes" or "magicRunningShoes" or "scubaGear" or "lockPickSet";

    private static readonly HashSet<string> AutomaticCraftingItems = CraftingCatalog.Materials.Concat(HazardCatalog.Materials)
        .Select(item => item.ItemType).Concat(CraftingCatalog.Recipes.SelectMany(recipe => recipe.Ingredients).Select(item => item.ItemType))
        .Concat(["kindling", "canadianMoney"]).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async Task<NearbyTreasureState> TakeNearbyUpgradesCoreAsync(string playerId, NearbyTreasureState opened, CancellationToken token)
    {
        var removed = opened.RemovedLoot.ToHashSet();
        var changed = opened.ChangedLoot.ToDictionary(item => item.Id);
        var homeItems = _playerAccounts.TryGetValue(playerId, out var accountId) ? GetHomeItemStorage(accountId).Items : [];
        // Best quality first across all sources, so a lesser copy cannot claim the available space first.
        var candidates = opened.Sources.SelectMany(source => source.Items.Select(item => (Source: source, Item: item)))
            .OrderByDescending(candidate => TreasureQualityRank(candidate.Item.Quality)).ToArray();
        foreach (var (source, item) in candidates)
        {
            var gear = IsTreasureGear(item.ItemType);
            if (gear)
            {
                var owned = GetInventoryItems(playerId).Concat(homeItems).Where(owned => owned.Quantity > 0 &&
                    owned.ItemType.Equals(item.ItemType, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (owned.Length > 0 && owned.Max(owned => TreasureQualityRank(owned.Quality)) >= TreasureQualityRank(item.Quality)) continue;
                if (VehicleItems.Contains(item.ItemType) && InventoryQuantity(playerId, item.ItemType) > 0) continue;
            }
            else if (!AutomaticCraftingItems.Contains(item.ItemType) && CraftingCatalog.RecipeFromItem(item.ItemType) is null) continue;
            var quantity = gear ? 1 : item.Quantity;
            // Take only the quantity that fits, leaving the rest for a later visit.
            var low = 0;
            var high = quantity;
            while (low < high)
            {
                var middle = low + (high - low + 1) / 2;
                if (CanAddToBackpack(playerId, [item with { Quantity = middle }], out _)) low = middle;
                else high = middle - 1;
            }
            if (low == 0) continue;
            if (source.Chest) await TakeChestItemsCoreAsync(playerId, new(source.Id, [new(item.ItemType, low)]), token);
            else
            {
                var taken = await TakeLootItemsCoreAsync(playerId, new(source.Id, [new(item.ItemType, low)]), token);
                if (taken.Remaining is null) removed.Add(source.Id);
                else if (taken.Remaining.DropKind != "eventReward") changed[source.Id] = taken.Remaining;
            }
        }
        var result = await ReadNearbyTreasureAsync(playerId, opened.AnchorId, token);
        removed.UnionWith(result.RemovedLoot);
        foreach (var loot in result.ChangedLoot) changed[loot.Id] = loot;
        return result with { IsEventReward = opened.IsEventReward, RemovedLoot = removed.ToArray(), ChangedLoot = changed.Values.Where(loot => !removed.Contains(loot.Id)).ToArray(),
            Message = "Collected money, crafting items, and available gear upgrades that fit in your backpack." };
    }

    public async Task<NearbyTreasureState> AutoTakeNearbyTreasureAsync(string playerId, string sourceId, bool chest, CancellationToken token = default, bool upgradesOnly = false)
    {
        await _treasureInteractionLock.WaitAsync(token);
        try
        {
            if (chest) ValidateTreasureChest(playerId, sourceId);
            else
            {
                var loot = OpenLoot(playerId, sourceId);
                var player = _players[playerId];
                if (loot.LocationId != player.LocationId || loot.Position.Distance2D(player.Position) > 4)
                    throw new InvalidOperationException("Move within 4 meters to collect treasure.");
            }
            var opened = await ReadNearbyTreasureAsync(playerId, sourceId, token);
            if (upgradesOnly) return await TakeNearbyUpgradesCoreAsync(playerId, opened, token);
            if (opened.Items.Count == 0 || !CanAddToBackpack(playerId, opened.Items, out _)) return opened;
            var taken = await TakeNearbyTreasureCoreAsync(playerId, new(sourceId,
                opened.Sources.Select(source => new TreasureSourceRequest(source.Id, source.Chest)).ToArray(),
                opened.Items.Select(item => new PurchaseLine(item.ItemType, item.Quantity)).ToArray()), token);
            var removed = opened.RemovedLoot.Concat(taken.RemovedLoot).Distinct().ToArray();
            return taken with { RemovedLoot = removed, ChangedLoot = opened.ChangedLoot.Concat(taken.ChangedLoot)
                .Where(loot => !removed.Contains(loot.Id)).GroupBy(loot => loot.Id).Select(group => group.Last()).ToArray() };
        }
        finally { _treasureInteractionLock.Release(); }
    }
}

public sealed record TreasureSource(string Id, bool Chest, IReadOnlyList<ItemStack> Items);
public sealed record TreasureSourceRequest(string Id, bool Chest);
public sealed record TakeNearbyTreasureRequest(string AnchorId, TreasureSourceRequest[] Sources, PurchaseLine[] Items);
public sealed record NearbyTreasureState(string AnchorId, IReadOnlyList<TreasureSource> Sources, IReadOnlyList<ItemStack> Items, string Message,
    PlayerState Player, IReadOnlyList<LootDropState> ChangedLoot, IReadOnlyList<string> RemovedLoot, bool IsEventReward = false);
