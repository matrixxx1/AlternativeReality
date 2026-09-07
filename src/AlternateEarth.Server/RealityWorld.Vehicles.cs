using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static readonly HashSet<string> VehicleItems = new(
        ["skateboard", "bike", "eBike", "dirtBike", "motorcycle", "inflatableRaft", "ufo"], StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, int>? VehicleHomeStorage(string playerId) =>
        _playerAccounts.TryGetValue(playerId, out var accountId) && _homeItemStorage.TryGetValue(accountId, out var storage) ? storage : null;

    private async Task ParkCarriedVehiclesAsync(string playerId, string accountId, CancellationToken cancellationToken)
    {
        await EnsureHomeItemStorageAsync(accountId, cancellationToken);
        if (!_inventories.TryGetValue(playerId, out var inventory)) return;
        var storage = _homeItemStorage[accountId];
        var changed = false;
        lock (storage)
        lock (inventory)
        {
            foreach (var item in inventory.Where(pair => VehicleItems.Contains(pair.Key) && pair.Value > 0).ToArray())
            {
                storage[item.Key] = storage.GetValueOrDefault(item.Key) + item.Value;
                inventory.Remove(item.Key);
                changed = true;
            }
        }
        if (changed) await SaveInventoryAsync(playerId, cancellationToken);
    }

    // Migrate offline characters too. Each account's transfers commit together so
    // restarting halfway through cannot duplicate vehicles or lose a backpack.
    private async Task ParkExistingVehiclesAsync(CancellationToken cancellationToken)
    {
        foreach (var account in await _store.LoadAccountRosterAsync(cancellationToken))
        {
            var updates = new List<InventoryState>();
            InventoryState? home = null;
            foreach (var character in account.Characters)
            {
                if (await _store.LoadCharacterAsync(Configuration.Id, character.Id, cancellationToken) is null) continue;
                var inventory = await _store.LoadInventoryAsync(character.Id, cancellationToken);
                var vehicles = inventory.Items.Where(item => VehicleItems.Contains(item.ItemType) && item.Quantity > 0).ToArray();
                if (vehicles.Length == 0) continue;
                home ??= await _store.LoadInventoryAsync(HomeItemStorageOwnerId(account.AccountId), cancellationToken);
                var merged = home.Items.ToDictionary(item => item.ItemType, StringComparer.OrdinalIgnoreCase);
                foreach (var item in vehicles)
                    merged[item.ItemType] = InventoryStack(item.ItemType, merged.GetValueOrDefault(item.ItemType)?.Quantity + item.Quantity ?? item.Quantity);
                home = home with { Items = merged.Values.ToArray() };
                updates.Add(inventory with { Items = inventory.Items.Where(item => !VehicleItems.Contains(item.ItemType)).ToArray() });
            }
            if (home is null) continue;
            updates.Add(home);
            await _store.SaveInventoriesAsync(updates, cancellationToken);
        }
    }
}
