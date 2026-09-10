using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<string, PlayerTestingSettings> _playerTesting = new();

    private PlayerTestingSettings TestingSettings(string playerId) =>
        _playerTesting.GetValueOrDefault(playerId) ?? new PlayerTestingSettings();

    private bool LegacyGodMode(string playerId) => _players.GetValueOrDefault(playerId)?.GodMode == true;
    private bool PlayerCanDie(string playerId) => !LegacyGodMode(playerId) && TestingSettings(playerId).CanDie;
    private bool PlayerConsumesAmmo(string playerId) => !LegacyGodMode(playerId) && TestingSettings(playerId).ConsumesAmmo;
    private bool PlayerConsumesCraftingMaterials(string playerId) => TestingSettings(playerId).ConsumesCraftingMaterials;
    private bool PlayerMustMeetCraftingMaterials(string playerId) => TestingSettings(playerId).MustMeetCraftingMaterialRequirements;
    private bool PlayerCanFailCrafting(string playerId) => TestingSettings(playerId).CanFailWhenCrafting;
    private bool PlayerConsumesAir(string playerId) => !LegacyGodMode(playerId) && TestingSettings(playerId).ConsumesAirWhenSwimming;
    private bool PlayerConsumesStamina(string playerId) => !LegacyGodMode(playerId) && TestingSettings(playerId).ConsumesStaminaWhenMoving;
    private bool PlayerConsumesStamina(PlayerState player) => !player.GodMode && TestingSettings(player.Id).ConsumesStaminaWhenMoving;
    private bool PlayerConsumesVehicleFuel(string playerId) => !LegacyGodMode(playerId) && TestingSettings(playerId).ConsumesVehicleFuel;
    private bool PlayerObeysBackpackWeight(string playerId) => !LegacyGodMode(playerId) && TestingSettings(playerId).ObeysBackpackWeightLimit;
    private bool PlayerObeysBackpackWeight(PlayerState player) => !player.GodMode && TestingSettings(player.Id).ObeysBackpackWeightLimit;
    private double PlayerDamageMultiplier(string playerId) => TestingSettings(playerId).DoesNormalDamage ? 1 : 100;
    private double PlayerTestingSpeedMultiplier(PlayerState player) => player.GodMode || !TestingSettings(player.Id).GetsNormalMovementSpeed ? 5 : 1;
    public bool CanUseWorldTesting(string playerId) => _players.TryGetValue(playerId, out var player) && !player.IsTestCharacter;

    public async Task<PlayerTestingSettings> UpdatePlayerTestingAsync(string playerId, PlayerTestingSettings settings, CancellationToken cancellationToken = default)
    {
        if (!_players.ContainsKey(playerId)) throw new InvalidOperationException("Unknown player.");
        _playerTesting[playerId] = settings;
        await _store.SavePlayerTestingSettingsAsync(Configuration.Id, playerId, settings, cancellationToken);
        return settings;
    }
}
