using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private long DoorLockCycleSeconds => Math.Max(1, _eventConfiguration.DoorLockRefreshMinutes) * 60L;

    public long CurrentDoorLockCycle => _probulatorClock.GetUtcNow().ToUnixTimeSeconds() / DoorLockCycleSeconds;

    public long CurrentStoreHour => CurrentServerTime.ToUnixTimeSeconds() / 3600;

    public DoorLockSchedule GetDoorLockSchedule()
    {
        var cycle = CurrentDoorLockCycle;
        var doors = _baseEntities.Values
            .Where(entity => entity.Kind == EntityKind.Door)
            .Select(door =>
            {
                var buildingId = door.Properties.GetValueOrDefault("buildingId") ?? string.Empty;
                var building = _baseEntities.GetValueOrDefault(buildingId);
                return new DoorLockState(door.Id, buildingId, building is not null && IsBuildingLocked(building, cycle),
                    building is not null ? StoreHoursForBuilding(building) : null);
            })
            .OrderBy(state => state.DoorId)
            .ToArray();
        return new DoorLockSchedule(cycle, DateTimeOffset.FromUnixTimeSeconds((cycle + 1) * DoorLockCycleSeconds), doors);
    }

    private bool IsBuildingLocked(CanonicalEntity building, long? cycle = null)
    {
        if (IsCasino(building)) return false;
        if (_publicBaseClaims.ContainsKey(building.Id) || _baseBuildings.Values.Contains(building.Id)) return false;
        if (StoreHoursForBuilding(building) is { } hours) return !hours.IsOpen(CurrentServerTime);
        if (IsQuestBuilding(building)) return false;
        var lockCycle = cycle ?? CurrentDoorLockCycle;
        var roll = unchecked((uint)StableInt($"door-lock:{Configuration.Seed}:{building.Id}:{lockCycle}")) % 100;
        return roll < 90;
    }

    private StoreOpeningHours? StoreHoursForBuilding(CanonicalEntity building)
    {
        if (IsCasino(building)) return null;
        if (_publicBaseClaims.ContainsKey(building.Id) || _baseBuildings.Values.Contains(building.Id) || StoreProfileForBuilding(building) is null) return null;
        // A seeded assignment keeps each store's daily hours stable across visits and restarts.
        return new StoreOpeningHours((int)(unchecked((uint)StableInt($"store-hours:{Configuration.Seed}:{building.Id}")) % 24));
    }

    private static string StoreClosedMessage(StoreOpeningHours hours) =>
        $"This store is closed. Open daily {hours.OpenHour:00}:00–{hours.CloseHour:00}:00 server time{(hours.CloseHour < hours.OpenHour ? " (overnight)" : "")}.";

    private static bool IsQuestBuilding(CanonicalEntity building) =>
        IsTrue(building.Properties.GetValueOrDefault("questItem")) || IsTrue(building.Properties.GetValueOrDefault("quest:item"));

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

public sealed record DoorLockSchedule(long Cycle, DateTimeOffset EndsAtUtc, IReadOnlyList<DoorLockState> Doors);
