using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _swimAttempts = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _drowningUntil = new();
    internal static bool IsWater(TerrainType terrain) => terrain is TerrainType.ShallowWater or TerrainType.DeepWater;
    private TerrainType TerrainFor(PlayerState player) => player.LocationId == "outdoor"
        ? EventTerrainAt(player.Position, Navigation.TerrainAt(player.Position.X, player.Position.Y)) : _dungeons.TryGetValue(player.LocationId, out var d) ? DungeonTerrainAt(d, player.Position) : player.Terrain;

    internal static PlayerState StepAir(PlayerState player, double seconds, bool gas, bool submerged, bool struggling, bool scubaGear = false, bool consumesAir = true, bool canDie = true)
    {
        seconds = Math.Clamp(seconds, 0, 2);
        if (!consumesAir) return player with { Air = player.MaximumAir, SwimExhausted = false };
        var exhausted = player.TravelMode == TravelMode.Swim &&
            (player.Stamina <= .025 || player.SwimExhausted && (player.Stamina < 2 || struggling));
        var drowning = submerged && player.TravelMode is not (TravelMode.Raft or TravelMode.Ufo) &&
            (player.TravelMode != TravelMode.Swim || exhausted);
        var drain = gas || drowning || exhausted && struggling || player.TravelMode == TravelMode.Scuba;
        var drainRate = !gas && (scubaGear || player.TravelMode == TravelMode.Scuba) ? .025 : 2;
        var air = Math.Clamp(player.Air + (drain ? -drainRate : 2) * seconds, 0, player.MaximumAir);
        // Only the part of this tick actually spent without air inflicts suffocation damage.
        var withoutAir = drain ? Math.Max(0, seconds - player.Air / drainRate) : 0;
        return player with { Air = air, SwimExhausted = exhausted,
            HealthHearts = Math.Max(canDie ? 0 : 1, player.HealthHearts - player.MaximumHealthHearts / 5 * withoutAir) };
    }

    private async Task<PlayerState> ApplyAirAsync(PlayerState player, double seconds, CancellationToken token)
    {
        var now = _probulatorClock.GetUtcNow();
        var terrain = TerrainFor(player);
        if (player.TravelMode == TravelMode.Swim && !IsWater(terrain))
            player = player with { TravelMode = TravelMode.Walk, SwimExhausted = false };
        var gas = _areaHazards.Values.Any(h => h.State.EndsAtUtc > now && h.State.LocationId == player.LocationId &&
            h.State.Effect != "napalm" && HazardTouches(h.State, player.Position));
        gas |= InsideInversion(player) && _activeInversion!.Patches.Any(p => p.Kind == "stink" && p.ChangesAtUtc <= now && p.EndsAtUtc > now && p.Position.Distance2D(player.Position) <= p.Radius);
        var submerged = terrain == TerrainType.DeepWater || _drowningUntil.GetValueOrDefault(player.Id) > now;
        var updated = StepAir(player with { Terrain = terrain }, seconds, gas, submerged,
            _swimAttempts.TryGetValue(player.Id, out var attempt) && now - attempt < TimeSpan.FromSeconds(1), InventoryQuantity(player.Id, "scubaGear") > 0,
            PlayerConsumesAir(player.Id), PlayerCanDie(player.Id));
        return updated.HealthHearts <= 0 && PlayerCanDie(player.Id) ? await DieAndResetPlayerAsync(updated, token) : updated;
    }
}
