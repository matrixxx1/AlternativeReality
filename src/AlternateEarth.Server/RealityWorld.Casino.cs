using System.Collections.Concurrent;
using System.Security.Cryptography;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly object _casinoAssignmentLock = new();
    private string? _casinoBuildingId;
    private readonly ConcurrentDictionary<string, CasinoSavedRound> _testCasinoRounds = new();
    private readonly ConcurrentDictionary<string, string> _testCasinoLatest = new();
    private static bool IsCasino(CanonicalEntity building) => building.Properties.GetValueOrDefault("merchantCategory") == "casino";
    private CasinoMapLocation? GetCasinoMapLocation()
    {
        if (_casinoBuildingId is null) return null;
        var building = _baseEntities.GetValueOrDefault(_casinoBuildingId) ?? _realityEntities.GetValueOrDefault(_casinoBuildingId);
        return building is null || building.Properties.GetValueOrDefault("state") == "rubble" ? null :
            new(building.Id, building.Properties.GetValueOrDefault("name") ?? "Casino", building.Position);
    }

    private void EnsureCasinoBuilding()
    {
        lock (_casinoAssignmentLock)
        {
            _casinoBuildingId ??= _baseEntities.Values.Concat(_realityEntities.Values).FirstOrDefault(IsCasino)?.Id;
            if (_casinoBuildingId is not null && _baseEntities.TryGetValue(_casinoBuildingId, out var existing))
            {
                if (!_publicBaseClaims.ContainsKey(existing.Id) && existing.Properties.GetValueOrDefault("state") != "rubble")
                { _baseEntities[existing.Id] = CasinoBuilding(existing); return; }
                _casinoBuildingId = null;
            }
            if (_casinoBuildingId is not null) return;
            var doorBuildings = _baseEntities.Values.Where(e => e.Kind == EntityKind.Door).Select(e => e.Properties.GetValueOrDefault("buildingId")).ToHashSet();
            var candidates = _baseEntities.Values.Where(e => e.Kind == EntityKind.Building && doorBuildings.Contains(e.Id) &&
                !_publicBaseClaims.ContainsKey(e.Id) && !_baseBuildings.Values.Contains(e.Id) && !IsQuestBuilding(e) &&
                e.Properties.GetValueOrDefault("state") != "rubble" && StoreProfileForBuilding(e) is null).OrderBy(e => e.Id).ToArray();
            // Reserve a first Home only when the reality has no existing player bases.
            if (candidates.Length == 0 || candidates.Length == 1 && _publicBaseClaims.IsEmpty && _baseBuildings.IsEmpty) return;
            var selected = CasinoBuilding(candidates[RandomNumberGenerator.GetInt32(candidates.Length)]);
            _store.SaveEntityAsync(Configuration.Id, selected).GetAwaiter().GetResult();
            _casinoBuildingId = selected.Id; _baseEntities[selected.Id] = selected;
        }
    }

    private static CanonicalEntity CasinoBuilding(CanonicalEntity building) => building with
    { Properties = new Dictionary<string, string>(building.Properties) { ["merchantCategory"] = "casino", ["name"] = "Lucky Lantern Casino" } };

    private static DungeonState GenerateCasino(string id, CanonicalEntity building) => new(id, building.Id, 56, 8,
        [new(0, 0, 56, 8)], [], new WorldPosition(building.Position.Region, 2, 4), [], [], [],
        IsStore: true, StoreCategory: "casino", SessionId: id);

    private Task<CasinoSavedRound?> LoadCasinoRoundAsync(PlayerState player, string? id, CancellationToken token) => player.IsTestCharacter
        ? Task.FromResult(_testCasinoRounds.GetValueOrDefault($"{player.Id}:{id ?? _testCasinoLatest.GetValueOrDefault(player.Id)}"))
        : _store.LoadCasinoRoundAsync(Configuration.Id, player.Id, id, token);

    public async Task<(PlayerState Player, CasinoRoundView? Round)> CasinoAsync(string playerId, CasinoBetRequest? bet = null,
        CasinoActionRequest? action = null, CancellationToken cancellationToken = default)
    {
        var gate = _playerSaveLocks.GetOrAdd(playerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!_players.TryGetValue(playerId, out var player) || !_dungeons.TryGetValue(player.LocationId, out var dungeon) || dungeon.StoreCategory != "casino")
                throw new InvalidOperationException("Enter the casino to play.");
            var current = await LoadCasinoRoundAsync(player, null, cancellationToken);
            if (bet is null && action is null) return (player, current is null ? null : CasinoRules.View(current));
            CasinoSavedRound next;
            long delta;
            if (bet is not null)
            {
                if (!Guid.TryParse(bet.RoundId, out _)) throw new InvalidOperationException("Invalid wager identifier.");
                var replay = await LoadCasinoRoundAsync(player, bet.RoundId, cancellationToken);
                if (replay is not null) return (player, CasinoRules.View(current ?? replay));
                if (current is { Phase: not "complete" }) throw new InvalidOperationException("Finish your existing hand before making another wager.");
                var station = CasinoRules.Stations.FirstOrDefault(s => s.Game == bet.Game) ?? throw new InvalidOperationException("Choose a casino game.");
                if (Math.Abs(player.Position.X - station.X) > 2.8) throw new InvalidOperationException("Walk closer to that game first.");
                if (bet.WagerCents <= 0 || bet.WagerCents > player.WalletCents) throw new InvalidOperationException("You cannot wager more than your wallet balance.");
                if (bet.WagerCents > (long.MaxValue - player.WalletCents) / 500) throw new InvalidOperationException("That wager exceeds the supported payout limit.");
                next = CasinoRules.Start(bet.RoundId, playerId, dungeon.BuildingId, bet.Game, bet.WagerCents, bet.Choice);
                delta = -bet.WagerCents;
            }
            else
            {
                if (current is null || action!.RoundId != current.RoundId) throw new InvalidOperationException("Reopen your current casino hand.");
                next = CasinoRules.Act(current, action);
                if (next == current) return (player, CasinoRules.View(current));
                delta = next.Phase == "complete" ? next.PayoutCents : 0;
            }
            LootDropState? reward = null;
            if (next.Phase == "complete" && next.WagerCents >= 100_000 && next.PayoutCents > next.WagerCents)
            {
                string[] items = ["knife", "pistol", "hat", "lightJacket", "food", "water", "wood", "metal",
                    GloveCatalog.Items[RandomNumberGenerator.GetInt32(GloveCatalog.Items.Length)].ItemType];
                var first = RandomNumberGenerator.GetInt32(items.Length);
                var second = (first + RandomNumberGenerator.GetInt32(1, items.Length)) % items.Length;
                reward = new($"casino:{Configuration.Id}:{playerId}:{next.RoundId}:reward", player.Position, player.LocationId, 0,
                    [InventoryStack(items[first], 1, quality: "Common"), InventoryStack(items[second], 1, quality: "Common")],
                    DateTimeOffset.MaxValue, "eventReward", player.Name, playerId);
                next = next with { RewardLootId = reward.Id, Message = next.Message + " Two bonus items await in your private reward chest." };
            }
            // Invalidate already-captured movement/exit snapshots, which may advance by two versions.
            var updated = player with { WalletCents = checked(player.WalletCents + delta), Version = player.Version + 2 };
            if (!player.IsTestCharacter) await _store.SaveCasinoTransitionAsync(Configuration.Id, updated, next, reward, cancellationToken);
            else { _testCasinoRounds[$"{playerId}:{next.RoundId}"] = next; _testCasinoLatest[playerId] = next.RoundId; }
            _players[playerId] = updated;
            if (reward is not null) _loot[reward.Id] = reward;
            return (updated, CasinoRules.View(next));
        }
        finally { gate.Release(); }
    }
}
