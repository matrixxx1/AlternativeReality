namespace AlternateEarth.Shared;

public sealed record CasinoBetRequest(string RoundId, string Game, long WagerCents, string? Choice = null);
public sealed record CasinoActionRequest(string RoundId, int Revision, string Action, IReadOnlyList<int>? Holds = null);
public sealed record CasinoStation(string Game, string Name, double X);
public sealed record CasinoMapLocation(string BuildingId, string Name, WorldPosition Position);
public sealed record CasinoRoundView(string RoundId, int Revision, string Game, long WagerCents,
    string Phase, IReadOnlyList<string> Cards, IReadOnlyList<string> DealerCards,
    IReadOnlyList<string> Symbols, string Message, long PayoutCents, string? RewardLootId);
