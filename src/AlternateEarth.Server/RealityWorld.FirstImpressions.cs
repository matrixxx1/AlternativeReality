using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed record NpcFirstImpression(string CharacterName, string NpcName, int LastDay, double Adjustment);

public sealed partial class RealityWorld
{
    public const double DailyFirstImpressionAdjustment = .05;
    private readonly ConcurrentDictionary<string, string> _actionModes = new();
    private readonly ConcurrentDictionary<(string Character, string Npc), NpcFirstImpression> _firstImpressions = new();
    private readonly SemaphoreSlim _firstImpressionLock = new(1, 1);

    public string SetActionMode(string playerId, string mode)
    {
        if (!_players.ContainsKey(playerId)) throw new InvalidOperationException("Unknown player.");
        if (mode == "friendly") mode = "defensive"; // Compatibility with clients before Friendly was removed.
        if (mode == "attack") mode = "attackReady"; // Existing clients before Posturing.
        if (mode is not ("neutral" or "attackReady" or "aggressive" or "defensive" or "timid")) throw new InvalidOperationException("Choose Neutral, Offensive, Aggressive, Defensive, or Timid posturing.");
        _actionModes[playerId] = mode;
        return mode;
    }

    private static string ImpressionName(string name) => name.Trim().ToUpperInvariant();
    private static bool CanFormFirstImpression(ActorState actor) => actor.Kind == EntityKind.Npc &&
        actor.LocationId == "outdoor" && !actor.IsMerchant && !actor.IsQuestGiver && string.IsNullOrEmpty(actor.MerchantCategory) &&
        actor.Subtype is not ("merchant" or "storeMerchant" or "storeEmployee" or "ufo") && actor.EventStartedAtUtc is null;

    private double FirstImpressionAdjustment(string playerId, string actorId)
    {
        if (!_players.TryGetValue(playerId, out var player) || FindActor(playerId, actorId) is not { } actor) return 0;
        return _firstImpressions.GetValueOrDefault((ImpressionName(player.Name), ImpressionName(actor.Name)))?.Adjustment ?? 0;
    }

    public async Task<IReadOnlyList<RelationshipState>> AdvanceFirstImpressionsAsync(CancellationToken cancellationToken = default)
    {
        var changed = new List<RelationshipState>();
        await _firstImpressionLock.WaitAsync(cancellationToken);
        try
        {
            var day = DateOnly.FromDateTime(CurrentServerTime.DateTime).DayNumber;
            foreach (var player in _players.Values.Where(p => !p.IsTestCharacter && p.HealthHearts > 0 && p.TravelMode != TravelMode.Ufo))
            {
                if (IsProbulatorAbducted(player.Id) || IsGasAsleep(player.Id)) continue;
                var candidates = ActorsAtLocation(player.LocationId).Where(actor => actor.LocationId == player.LocationId && actor.Kind == EntityKind.Npc && actor.Subtype != "ufo").ToArray();
                foreach (var actor in candidates)
                {
                    var key = (ImpressionName(player.Name), ImpressionName(actor.Name));
                    var prior = _firstImpressions.GetValueOrDefault(key);
                    if (prior?.LastDay >= day || actor.HealthHearts <= 0 || IsGasAsleep(actor.Id) || IsProbulatorAbducted(actor.Id)) continue;
                    // A meeting means being nearby and visible, not merely sharing a loaded map block.
                    var distance = player.Position.Distance2D(actor.Position);
                    if (distance > 10 || distance > NpcSightRange(actor, player.Position, player.Id)) continue;
                    if (player.LocationId == "outdoor" ? !Navigation.CanTraverse(actor.Position, player.Position) :
                        !_dungeons.TryGetValue(player.LocationId, out var interior) || interior.Walls.Any(wall => CrossesDungeonWall(actor.Position, player.Position, wall))) continue;
                    var adjustment = _actionModes.GetValueOrDefault(player.Id, "neutral") switch
                    {
                        "attackReady" => -DailyFirstImpressionAdjustment,
                        "aggressive" => -2 * DailyFirstImpressionAdjustment,
                        "defensive" => DailyFirstImpressionAdjustment,
                        "timid" => 2 * DailyFirstImpressionAdjustment,
                        _ => 0
                    };
                    if (!CanFormFirstImpression(actor)) adjustment = 0;
                    var socialBonus = prior is null ? ProgressionRules.Charisma(StatsFor(player.Id)) + GetProgression(player.Id).AlignmentFirstEncounterBonus : 0;
                    if (!CanFormFirstImpression(actor) && socialBonus == 0)
                    {
                        // Remember neutral meetings too, so a later respec cannot change a first encounter.
                        if (prior is null)
                        {
                            var neutral = new NpcFirstImpression(key.Item1, key.Item2, day, 0);
                            await _store.SaveFirstImpressionAsync(Configuration.Id, neutral, cancellationToken);
                            _firstImpressions[key] = neutral;
                        }
                        continue;
                    }
                    adjustment += socialBonus;
                    var impression = new NpcFirstImpression(key.Item1, key.Item2, day, Math.Round((prior?.Adjustment ?? 0) + adjustment, 5));
                    await _store.SaveFirstImpressionAsync(Configuration.Id, impression, cancellationToken);
                    _firstImpressions[key] = impression;
                    foreach (var sameName in candidates.Where(a => ImpressionName(a.Name) == key.Item2))
                        changed.Add(new(player.Id, sameName.Id, Relationship(player.Id, sameName.Id)));
                }
            }
        }
        finally { _firstImpressionLock.Release(); }
        return changed;
    }
}
