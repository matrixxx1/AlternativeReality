using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private string HomeItemStorageOwnerId(string accountId) => $"home-items:{Configuration.Id}:{accountId}";

    private async Task EnsureHomeItemStorageAsync(string accountId, CancellationToken cancellationToken)
    {
        if (_homeItemStorage.ContainsKey(accountId)) return;
        await _homeItemStorageLock.WaitAsync(cancellationToken);
        try
        {
            if (_homeItemStorage.ContainsKey(accountId)) return;
            var stored = await _store.LoadInventoryAsync(HomeItemStorageOwnerId(accountId), cancellationToken);
            _homeItemStorage[accountId] = stored.Items.Where(item => item.Quantity > 0)
                .ToDictionary(item => item.ItemType, item => item.Quantity, StringComparer.OrdinalIgnoreCase);
        }
        finally { _homeItemStorageLock.Release(); }
    }

    private InventoryState GetHomeItemStorage(string accountId)
    {
        var items = Array.Empty<ItemStack>();
        if (_homeItemStorage.TryGetValue(accountId, out var storage))
        {
            lock (storage) items = storage.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key)
                .Select(pair => InventoryStack(pair.Key, pair.Value)).ToArray();
        }
        return new InventoryState(HomeItemStorageOwnerId(accountId), items, WeightPounds: Math.Round(items.Sum(item => item.UnitWeightPounds * item.Quantity), 3), Unlimited: true);
    }

    private (PlayerState Player, string AccountId, CanonicalEntity Chest) ValidateHomeStorageAccess(string playerId, string chestId)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_dungeons.TryGetValue(player.LocationId, out var home) || !home.IsHome)
            throw new InvalidOperationException("Your storage chest is only available inside your Home.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId)) throw new InvalidOperationException("Home storage is unavailable.");
        var chest = home.Furnishings?.FirstOrDefault(item => item.Id == chestId && item.Properties.GetValueOrDefault("objectType") == "storageChest")
            ?? throw new InvalidOperationException("Storage chest not found.");
        return (player, accountId, chest);
    }

    public InventoryState OpenHomeItemStorage(string playerId, string chestId)
    {
        var access = ValidateHomeStorageAccess(playerId, chestId);
        return GetHomeItemStorage(access.AccountId);
    }

    public async Task<(PlayerState Player, PlayerPrivateState PrivateState)> TransferHomeItemAsync(string playerId, TransferHomeStorageRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity is < 1 or > 100_000) throw new InvalidOperationException("Choose a quantity between 1 and 100,000.");
        var itemType = (request.ItemType ?? string.Empty).Trim();
        if (itemType.Length == 0 || itemType.Equals("fist", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("That item cannot be transferred.");
        if (!InventoryDefinition(itemType).CarriedInBackpack) throw new InvalidOperationException("Parked vehicles are kept outside the backpack and storage chest.");
        var access = ValidateHomeStorageAccess(playerId, request.ChestId);
        await EnsureHomeItemStorageAsync(access.AccountId, cancellationToken);
        await _homeItemStorageLock.WaitAsync(cancellationToken);
        try
        {
            var storage = _homeItemStorage[access.AccountId];
            if (request.ToStorage)
            {
                if (!RemoveInventory(playerId, itemType, request.Quantity)) throw new InvalidOperationException($"You do not have that many {DisplayItem(itemType)}.");
                lock (storage) storage[itemType] = storage.GetValueOrDefault(itemType) + request.Quantity;
            }
            else
            {
                lock (storage)
                {
                    if (storage.GetValueOrDefault(itemType) < request.Quantity) throw new InvalidOperationException($"The chest does not contain that many {DisplayItem(itemType)}.");
                }
                if (!CanAddToBackpack(playerId, new[] { InventoryStack(itemType, request.Quantity) }, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);
                lock (storage)
                {
                    storage[itemType] -= request.Quantity;
                    if (storage[itemType] <= 0) storage.Remove(itemType);
                }
                AddInventory(playerId, itemType, request.Quantity);
            }

            var player = NormalizeEquipmentAfterInventoryChange(access.Player);
            await SaveInventoryAsync(playerId, cancellationToken);
            await _store.SaveInventoryAsync(GetHomeItemStorage(access.AccountId), cancellationToken);
            await SavePlayerAsync(player, cancellationToken);
            return (player, GetPrivateState(playerId));
        }
        finally { _homeItemStorageLock.Release(); }
    }

    public async Task<ConfiguredInventoryAdjustment> ConfigureInventoryItemAsync(string playerId, ConfigureInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        if (!playerIsGod(playerId)) throw new InvalidOperationException("God Mode must be enabled to adjust inventory from server configuration.");
        if (!_players.ContainsKey(playerId)) throw new InvalidOperationException("Unknown player.");
        var itemType = (request.ItemType ?? string.Empty).Trim();
        if (!_itemConfigurations.TryGetValue(itemType, out var definition)) throw new InvalidOperationException("Unknown inventory item.");
        if (!_playerAccounts.TryGetValue(playerId, out var accountId)) throw new InvalidOperationException("An account is required to use Home inventory.");
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action is not ("take" or "give")) throw new InvalidOperationException("Choose Take 1 or Give 1.");
        await EnsureHomeItemStorageAsync(accountId, cancellationToken);

        PlayerState? updatedPlayer = null;
        string message;
        await _homeItemStorageLock.WaitAsync(cancellationToken);
        try
        {
            var storage = _homeItemStorage[accountId];
            if (action == "take")
            {
                lock (storage) storage[itemType] = storage.GetValueOrDefault(itemType) + 1;
                await _store.SaveInventoryAsync(GetHomeItemStorage(accountId), cancellationToken);
                message = $"Took 1 {definition.DisplayName} into Home inventory.";
            }
            else
            {
                var removedFromHome = false;
                lock (storage)
                {
                    var quantity = storage.GetValueOrDefault(itemType);
                    if (quantity > 0)
                    {
                        removedFromHome = true;
                        if (quantity == 1) storage.Remove(itemType); else storage[itemType] = quantity - 1;
                    }
                }
                if (removedFromHome)
                {
                    await _store.SaveInventoryAsync(GetHomeItemStorage(accountId), cancellationToken);
                    message = $"Gave 1 {definition.DisplayName} from Home inventory.";
                }
                else
                {
                    if (!RemoveInventory(playerId, itemType, 1)) throw new InvalidOperationException($"There is no {definition.DisplayName} in Home inventory or the backpack.");
                    await SaveInventoryAsync(playerId, cancellationToken);
                    for (var attempt = 0; attempt < 5; attempt++)
                    {
                        var candidate = NormalizeEquipmentAfterInventoryChange(_players[playerId]);
                        if (!await SavePlayerAsync(candidate, cancellationToken)) continue;
                        updatedPlayer = candidate;
                        break;
                    }
                    updatedPlayer ??= _players[playerId];
                    message = $"Gave 1 {definition.DisplayName} from the backpack.";
                }
            }
        }
        finally { _homeItemStorageLock.Release(); }

        return new ConfiguredInventoryAdjustment(GetPrivateState(playerId), updatedPlayer, message);
    }

    public async Task<DroppedInventoryItem> DropInventoryItemAsync(string playerId, DropItemRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var currentPlayer)) throw new InvalidOperationException("Unknown player.");
        var itemType = (request.ItemType ?? string.Empty).Trim();
        if (itemType.Length == 0 || itemType.Equals("fist", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("That item cannot be dropped.");
        if (request.Quantity is < 1 or > 100_000) throw new InvalidOperationException("Choose a quantity between 1 and 100,000.");
        if (!_itemConfigurations.ContainsKey(itemType)) throw new InvalidOperationException("Unknown inventory item.");
        if (!RemoveInventory(playerId, itemType, request.Quantity))
            throw new InvalidOperationException($"You do not have that many {DisplayItem(itemType)}.");

        var droppedStack = InventoryStack(itemType, request.Quantity);
        var drop = new LootDropState(
            $"loot:{Guid.NewGuid():N}",
            currentPlayer.Position,
            currentPlayer.LocationId,
            0,
            new[] { droppedStack },
            DateTimeOffset.MaxValue);
        _loot[drop.Id] = drop;

        var player = NormalizeEquipmentAfterInventoryChange(currentPlayer);
        await SaveInventoryAsync(playerId, cancellationToken);
        await SavePlayerAsync(player, cancellationToken);
        return new DroppedInventoryItem(player, GetPrivateState(playerId), drop, $"Dropped {request.Quantity} {DisplayItem(itemType)}.");
    }

    private PlayerState NormalizeEquipmentAfterInventoryChange(PlayerState player)
    {
        var godMode = player.GodMode;
        var travelMode = !godMode && TravelModeUnavailable(player.Id, player) ? TravelMode.Walk : player.TravelMode;
        var offhand = ActiveOffhand(player);
        if (!godMode && offhand != "none" && InventoryQuantity(player.Id, offhand) <= 0) offhand = "none";
        return player with
        {
            TravelMode = travelMode,
            FlashlightOn = offhand == "flashlight",
            LanternOn = offhand == "lantern",
            LaserOn = offhand == "laser",
            MagicHikingShoesOn = godMode ? player.MagicHikingShoesOn : player.MagicHikingShoesOn && InventoryQuantity(player.Id, "magicHikingShoes") > 0,
            MagicRunningShoesOn = godMode ? player.MagicRunningShoesOn : player.MagicRunningShoesOn && InventoryQuantity(player.Id, "magicRunningShoes") > 0,
            EquippedHat = RetainedEquipment(player.Id, player.EquippedHat, HatItems, godMode),
            EquippedShirt = RetainedEquipment(player.Id, player.EquippedShirt, ShirtItems, godMode),
            EquippedPants = RetainedEquipment(player.Id, player.EquippedPants, PantsItems, godMode),
            HatOn = RetainedEquipment(player.Id, player.EquippedHat, HatItems, godMode).Equals("hat", StringComparison.OrdinalIgnoreCase),
            EquippedWeapon = godMode ? player.EquippedWeapon : BestUsableWeapon(player.Id, player.EquippedWeapon, false),
            SpeedMetersPerSecond = 0,
            Version = player.Version + 1
        };
    }
}

public sealed record ConfiguredInventoryAdjustment(PlayerPrivateState PrivateState, PlayerState? Player, string Message);
public sealed record DroppedInventoryItem(PlayerState Player, PlayerPrivateState PrivateState, LootDropState Drop, string Message);
