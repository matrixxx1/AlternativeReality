using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public LootDropState OpenLoot(string playerId, string lootId)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_loot.TryGetValue(lootId, out var loot) ||
            loot.LocationId != player.LocationId || loot.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Treasure is no longer available.");
        if (player.Position.Distance2D(loot.Position) > 4)
            throw new InvalidOperationException("Move closer to the treasure.");
        if (loot.OwnerId is not null && loot.DropKind == "eventReward" && loot.OwnerId != playerId) throw new InvalidOperationException("This is another player’s event reward.");
        var leveled = loot with { Items = loot.Items.Select(item => LevelLoot(playerId, item, loot.DropKind == "eventReward")).ToArray() };
        _loot[lootId] = leveled;
        return leveled;
    }

    public async Task<LootTakeResult> TakeLootItemsAsync(string playerId, TakeLootItemsRequest request, CancellationToken cancellationToken = default)
    {
        await _treasureInteractionLock.WaitAsync(cancellationToken);
        try
        {
            var loot = OpenLoot(playerId, request.LootId);
            if (request.Items is null || request.Items.Any(line => string.IsNullOrWhiteSpace(line.ItemType) || line.Quantity is < 1 or > 100_000))
                throw new InvalidOperationException("Choose a valid item and quantity.");
            var requested = request.Items.GroupBy(line => line.ItemType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Sum(line => (long)line.Quantity), StringComparer.OrdinalIgnoreCase);
            foreach (var line in requested)
                if (line.Value > loot.Items.Where(item => item.ItemType.Equals(line.Key, StringComparison.OrdinalIgnoreCase)).Sum(item => (long)item.Quantity))
                    throw new InvalidOperationException($"That treasure no longer contains {line.Value} × {DisplayItem(line.Key)}. Reopen it to see what remains.");

            var rewards = new List<ItemStack>();
            var remaining = new List<ItemStack>();
            foreach (var item in loot.Items)
            {
                var quantity = (int)Math.Min(item.Quantity, requested.GetValueOrDefault(item.ItemType));
                if (quantity > 0) rewards.Add(item with { Quantity = quantity });
                if (quantity < item.Quantity) remaining.Add(item with { Quantity = item.Quantity - quantity });
                requested[item.ItemType] = requested.GetValueOrDefault(item.ItemType) - quantity;
            }
            if (rewards.Count > 0 && !CanAddToBackpack(playerId, rewards, out var capacityMessage))
                throw new InvalidOperationException(capacityMessage + " Drop carried items or select fewer items.");

            foreach (var reward in rewards) AddInventory(playerId, reward.ItemType, reward.Quantity, reward.Quality, reward.Gear);
            var player = _players[playerId];
            var updated = player with { WalletCents = player.WalletCents + loot.MoneyCents, Version = player.Version + 1 };
            var remainder = remaining.Count == 0 ? null : loot with { Items = remaining.ToArray(), MoneyCents = 0 };
            if (remainder is null) _loot.TryRemove(loot.Id, out _);
            else _loot[loot.Id] = remainder;
            await SaveInventoryAsync(playerId, cancellationToken);
            await SavePlayerAsync(updated, cancellationToken);
            if (loot.DropKind is "tombstone" or "eventReward")
            {
                if (remainder is null) await _store.RemovePersistentLootAsync(loot.Id, cancellationToken);
                else await _store.SavePersistentLootAsync(Configuration.Id, remainder, cancellationToken);
            }
            var collected = rewards.Select(item => $"{item.Quantity} × {InventoryDefinition(item.ItemType).DisplayName}").ToList();
            if (loot.MoneyCents > 0) collected.Insert(0, $"{loot.MoneyCents / 100m:C}");
            return new LootTakeResult(updated, remainder, collected.Count > 0 ? $"Collected {string.Join(", ", collected)}." : "Left all items in the treasure.");
        }
        finally { _treasureInteractionLock.Release(); }
    }
}

public sealed record LootTakeResult(PlayerState Player, LootDropState? Remaining, string Message);
