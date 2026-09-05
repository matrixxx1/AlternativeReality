using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly SemaphoreSlim _homeShopLock = new(1, 1);

    private (PlayerState Player, DungeonState Home, CanonicalEntity Shop, string OwnerAccountId, string OwnerName, bool IsOwner) ValidateHomeShop(string playerId, string furnitureId)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_dungeons.TryGetValue(player.LocationId, out var home) || !home.IsHome)
            throw new InvalidOperationException("Home shops can only be used inside a Home.");
        var shop = home.Furnishings?.FirstOrDefault(item => item.Id == furnitureId && item.Properties.GetValueOrDefault("objectType") == "homeShopCounter" && !IsStoredFurniture(item))
            ?? throw new InvalidOperationException("That Home shop is not available.");
        var ownerAccountId = home.Id.StartsWith("home:", StringComparison.Ordinal) ? home.Id[5..].Split(':')[0] : string.Empty;
        if (ownerAccountId.Length == 0) throw new InvalidOperationException("The Home owner could not be determined.");
        var claim = _publicBaseClaims.GetValueOrDefault(home.BuildingId);
        var ownerName = claim?.OwnerName ?? "Player shop";
        var isOwner = _playerAccounts.GetValueOrDefault(playerId) == ownerAccountId;
        return (player, home, shop, ownerAccountId, ownerName, isOwner);
    }

    public async Task<HomeShopState> RequestHomeShopAsync(string playerId, string furnitureId, CancellationToken cancellationToken = default)
    {
        var access = ValidateHomeShop(playerId, furnitureId);
        var stored = await _store.LoadHomeShopListingsAsync(access.OwnerAccountId, Configuration.Id, cancellationToken);
        var listings = stored.Select(item =>
        {
            var definition = InventoryDefinition(item.ItemType);
            return new HomeShopListing(item.ItemType, item.Quantity, item.UnitPriceCents, definition.DisplayName, definition.WeightPounds, item.Quality);
        }).ToArray();
        return new HomeShopState(furnitureId, access.OwnerName, access.IsOwner, listings, access.IsOwner ? GetInventoryState(playerId) : null);
    }

    public async Task<HomeShopResult> SetHomeShopListingAsync(string playerId, SetHomeShopListingRequest request, CancellationToken cancellationToken = default)
    {
        var access = ValidateHomeShop(playerId, request.FurnitureId);
        if (!access.IsOwner) throw new InvalidOperationException("Only the Home owner can change this shop.");
        if (request.Quantity is < 0 or > 100_000) throw new InvalidOperationException("Choose a quantity from 0 to 100,000.");
        if (request.UnitPriceCents is < 1 or > 100_000_000_00) throw new InvalidOperationException("Choose a price from $0.01 to $100,000,000.");
        var itemType = (request.ItemType ?? string.Empty).Trim();
        var definition = InventoryDefinition(itemType);
        if (itemType.Length == 0 || itemType.Equals("fist", StringComparison.OrdinalIgnoreCase) || itemType.Equals("personalFlag", StringComparison.OrdinalIgnoreCase) || definition.Category == InventoryCategory.Quest)
            throw new InvalidOperationException("That item cannot be listed for sale.");

        await _homeShopLock.WaitAsync(cancellationToken);
        try
        {
            var current = (await _store.LoadHomeShopListingsAsync(access.OwnerAccountId, Configuration.Id, cancellationToken)).FirstOrDefault(item => item.ItemType.Equals(itemType, StringComparison.OrdinalIgnoreCase));
            var priorQuantity = current?.Quantity ?? 0;
            var difference = request.Quantity - priorQuantity;
            var quality = current?.Quality ?? _weaponQualities.GetValueOrDefault((playerId, itemType));
            if (difference > 0 && !RemoveInventory(playerId, itemType, difference)) throw new InvalidOperationException($"You need {difference} more {definition.DisplayName} in your inventory.");
            if (difference < 0)
            {
                var returned = InventoryStack(itemType, -difference, quality: quality);
                if (!CanAddToBackpack(playerId, new[] { returned }, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);
                AddInventory(playerId, itemType, -difference, quality);
            }
            await _store.SaveHomeShopListingAsync(access.OwnerAccountId, Configuration.Id, new HomeShopListingRecord(itemType, request.Quantity, request.UnitPriceCents, quality), cancellationToken);
            await SaveInventoryAsync(playerId, cancellationToken);
            var shop = await RequestHomeShopAsync(playerId, request.FurnitureId, cancellationToken);
            return new HomeShopResult(access.Player, GetPrivateState(playerId), shop, request.Quantity == 0 ? $"Removed {definition.DisplayName} from the shop." : $"Listed {request.Quantity} {definition.DisplayName} at {request.UnitPriceCents / 100d:C} each.");
        }
        finally { _homeShopLock.Release(); }
    }

    public async Task<HomeShopResult> PurchaseHomeShopAsync(string playerId, PurchaseHomeShopRequest request, CancellationToken cancellationToken = default)
    {
        var access = ValidateHomeShop(playerId, request.FurnitureId);
        if (access.IsOwner) throw new InvalidOperationException("You cannot buy from your own shop.");
        if (request.Quantity is < 1 or > 100_000) throw new InvalidOperationException("Choose a quantity from 1 to 100,000.");
        await _homeShopLock.WaitAsync(cancellationToken);
        try
        {
            var listing = (await _store.LoadHomeShopListingsAsync(access.OwnerAccountId, Configuration.Id, cancellationToken)).FirstOrDefault(item => item.ItemType.Equals(request.ItemType, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("That item is no longer for sale.");
            if (listing.Quantity < request.Quantity) throw new InvalidOperationException("The shop no longer has that many available.");
            var total = checked(listing.UnitPriceCents * request.Quantity);
            var buyer = _players[playerId];
            if (!buyer.GodMode && buyer.WalletCents < total) throw new InvalidOperationException("You do not have enough money.");
            if (!CanAddToBackpack(playerId, new[] { InventoryStack(listing.ItemType, request.Quantity, quality: listing.Quality) }, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);

            var remaining = listing.Quantity - request.Quantity;
            await _store.SaveHomeShopListingAsync(access.OwnerAccountId, Configuration.Id, listing with { Quantity = remaining }, cancellationToken);
            AddInventory(playerId, listing.ItemType, request.Quantity, listing.Quality);
            buyer = buyer with { WalletCents = buyer.GodMode ? buyer.WalletCents : buyer.WalletCents - total, Version = buyer.Version + 1 };
            await SaveInventoryAsync(playerId, cancellationToken);
            await SavePlayerAsync(buyer, cancellationToken);

            var sellerCharacterId = _playerAccounts.FirstOrDefault(pair => pair.Value == access.OwnerAccountId).Key;
            if (!string.IsNullOrWhiteSpace(sellerCharacterId) && _players.TryGetValue(sellerCharacterId, out var seller))
            {
                await SavePlayerAsync(seller with { WalletCents = seller.WalletCents + total, Version = seller.Version + 1 }, cancellationToken);
            }
            else await _store.CreditActiveCharacterAsync(access.OwnerAccountId, Configuration.Id, total, cancellationToken);
            var definition = InventoryDefinition(listing.ItemType);
            await _store.AddAccountNoticeAsync(access.OwnerAccountId, Configuration.Id, $"Your Home shop sold {request.Quantity} {definition.DisplayName} to {buyer.Name} for {total / 100d:C} while you were away.", cancellationToken);
            var shop = await RequestHomeShopAsync(playerId, request.FurnitureId, cancellationToken);
            return new HomeShopResult(buyer, GetPrivateState(playerId), shop, $"Bought {request.Quantity} {definition.DisplayName} for {total / 100d:C}.");
        }
        finally { _homeShopLock.Release(); }
    }
}

public sealed record HomeShopResult(PlayerState Player, PlayerPrivateState PrivateState, HomeShopState Shop, string Message);
