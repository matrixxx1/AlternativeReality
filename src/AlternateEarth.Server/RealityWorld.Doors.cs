using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private const long DoorLockCycleSeconds = 4 * 60 * 60;

    public long CurrentDoorLockCycle => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / DoorLockCycleSeconds;

    public DoorLockSchedule GetDoorLockSchedule()
    {
        var cycle = CurrentDoorLockCycle;
        var doors = _baseEntities.Values
            .Where(entity => entity.Kind == EntityKind.Door)
            .Select(door =>
            {
                var buildingId = door.Properties.GetValueOrDefault("buildingId") ?? string.Empty;
                var building = _baseEntities.GetValueOrDefault(buildingId);
                return new DoorLockState(door.Id, buildingId, building is not null && IsBuildingLocked(building, cycle));
            })
            .OrderBy(state => state.DoorId)
            .ToArray();
        return new DoorLockSchedule(cycle, DateTimeOffset.FromUnixTimeSeconds((cycle + 1) * DoorLockCycleSeconds), doors);
    }

    private bool IsBuildingLocked(CanonicalEntity building, long? cycle = null)
    {
        if (IsQuestBuilding(building)) return false;
        var lockCycle = cycle ?? CurrentDoorLockCycle;
        var roll = unchecked((uint)StableInt($"door-lock:{Configuration.Seed}:{building.Id}:{lockCycle}")) % 100;
        return roll < 90;
    }

    private static bool IsQuestBuilding(CanonicalEntity building) =>
        IsTrue(building.Properties.GetValueOrDefault("questItem")) || IsTrue(building.Properties.GetValueOrDefault("quest:item"));

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

public sealed record DoorLockSchedule(long Cycle, DateTimeOffset EndsAtUtc, IReadOnlyList<DoorLockState> Doors);
