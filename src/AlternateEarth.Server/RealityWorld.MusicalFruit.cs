using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    internal Func<double> MusicalFruitRoll { get; set; } = Random.Shared.NextDouble;
    private double FruitRoll() => Math.Clamp(MusicalFruitRoll(), 0, 1);
    private double MusicalFruitInterval() => 10 + FruitRoll() * 10;
    private static bool IsMusicalFruit(AreaHazardState zone) => zone.Effect is "musicalFruit" or "musicalFruitFinale";
    private static readonly string[] FruitPlayerLines = ["Ewwww!", "My bad.", "'Scuse me!", "Wow, I feel better now!", "That one had a bass solo.", "Beans: the gift that keeps on giving.", "Who put a tuba in my trousers?", "I thought that was going to be a silent one."];
    private static readonly string[] FruitEnemyLines = ["Ewwww!", "Good god, what are you eating?", "Maybe you should go to the doctor.", "Hey! Chemical warfare is outlawed by the Geneva convention!", "My nose just filed a complaint!", "That's not a battle cry. That's a battle WHY!", "Somebody open a window!", "I can taste the trousers!"];

    private void FruitSay(string id, string name, string[] lines)
    {
        if (FruitRoll() >= .4) return;
        _questDialogue.Enqueue(new(Guid.NewGuid().ToString("N"), id, name, lines[Math.Min(lines.Length - 1, (int)(FruitRoll() * lines.Length))], _probulatorClock.GetUtcNow()));
    }

    // Use the same hostility threshold as ordinary NPC combat; passive animals and friends are safe.
    private bool FruitEnemy(string ownerId, ActorState actor) => Relationship(ownerId, actor.Id) < 0 || IsEventPredator(actor.Subtype);

    internal async Task AdvanceMusicalFruitAsync(DateTimeOffset now, List<PlayerState> changed, CancellationToken token)
    {
        foreach (var player in _players.Values.Where(p => p.Survival?.MusicalFruit is not null).ToArray())
        {
            var fruit = player.Survival!.MusicalFruit!;
            var finale = now >= fruit.EndsAtUtc;
            var finalId = $"musical-fruit:{player.Id}:{fruit.EndsAtUtc.UtcTicks}";
            if (now >= fruit.EndsAtUtc.AddSeconds(20))
            {
                var cleared = player with { Survival = player.Survival with { MusicalFruit = null }, Version = player.Version + 1 };
                if (await SavePlayerAsync(cleared, token)) changed.Add(cleared);
                continue;
            }
            if (!finale && now < fruit.NextEmissionAtUtc) continue;
            if (finale && _areaHazards.TryGetValue(finalId, out var ongoing))
            {
                if (ongoing.State.Position != player.Position || ongoing.State.LocationId != player.LocationId)
                {
                    ongoing.State = ongoing.State with { Position = player.Position, LocationId = player.LocationId };
                    Interlocked.Increment(ref _areaHazardRevision);
                }
                continue;
            }
            var duration = finale ? 20 : 5 + Math.Min(5, (int)(FruitRoll() * 6));
            var radius = finale ? 5 : 1 + FruitRoll() * 1.25;
            var start = finale ? fruit.EndsAtUtc : now;
            var state = new AreaHazardState(finale ? finalId : $"musical-fruit:{Guid.NewGuid():N}", player.Id, player.LocationId, player.Position,
                finale ? "Musical Fruit: Grand toot finale" : "Musical Fruit", finale ? "musicalFruitFinale" : "musicalFruit", radius, start, start.AddSeconds(duration));
            var updated = player with { Survival = player.Survival with { MusicalFruit = fruit with { NextEmissionAtUtc = now.AddSeconds(MusicalFruitInterval()), FinaleEmitted = finale } }, Version = player.Version + 1 };
            if (!await SavePlayerAsync(updated, token)) continue;
            changed.Add(updated);
            var pending = new PendingAreaHazard(state, new("musicalFruit", state.Name, state.Effect, duration, finale ? 1 : .5, 0, "", ""));
            // Reconnecting during a finale resumes it without retroactive damage.
            pending.AppliedPulses = (int)Math.Max(0, (now - start).TotalSeconds);
            _areaHazards[state.Id] = pending;
            Interlocked.Increment(ref _areaHazardRevision);
            if (!finale || !fruit.FinaleEmitted) FruitSay(player.Id, player.Name, FruitPlayerLines);
        }
    }
}
