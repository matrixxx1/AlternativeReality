namespace AlternateEarth.Shared;

public sealed record AreaHazardState(string Id, string OwnerId, string LocationId, WorldPosition Position,
    string Name, string Effect, double RadiusMeters, DateTimeOffset StartedAtUtc, DateTimeOffset EndsAtUtc);
public sealed record ThrowHazardRequest(double X, double Y);
