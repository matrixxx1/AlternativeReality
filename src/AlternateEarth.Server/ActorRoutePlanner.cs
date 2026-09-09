using AlternateEarth.Shared;

namespace AlternateEarth.Server;

// Called only by the actor tick. Workers receive immutable snapshots and never mutate the world.
public sealed class ActorRoutePlanner
{
    public sealed record Result(ActorState Actor, WorldNavigation Navigation, IReadOnlyList<WorldPosition> Waypoints)
    {
        public bool Matches(ActorState current, WorldNavigation navigation) => current.Id == Actor.Id &&
            current.Position == Actor.Position && current.Version == Actor.Version && ReferenceEquals(navigation, Navigation);
    }
    private readonly Dictionary<string, Task<Result>> _pending = new();
    public int Capacity { get; } = Math.Clamp(Environment.ProcessorCount - 1, 1, 2);
    public int PendingCount => _pending.Count;

    public bool TrySchedule(ActorState actor, WorldNavigation navigation, int seed, PerformanceTimings timings)
    {
        if (_pending.Count >= Capacity || _pending.ContainsKey(actor.Id)) return false;
        _pending.Add(actor.Id, Task.Run(() =>
        {
            using var timing = timings.Measure("actors.pathfinding");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));
            var random = new Random(seed);
            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    var distance = 6 + random.NextDouble() * 24;
                    var angle = random.NextDouble() * Math.PI * 2;
                    var path = navigation.FindPath(actor.Position, actor.Position.X + Math.Cos(angle) * distance,
                        actor.Position.Y + Math.Sin(angle) * distance, cancellationToken: timeout.Token);
                    if (path.Success) return new Result(actor, navigation, path.Waypoints);
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
            return new Result(actor, navigation, Array.Empty<WorldPosition>());
        }));
        return true;
    }

    public IReadOnlyList<Result> TakeCompleted()
    {
        var results = new List<Result>();
        foreach (var pair in _pending.ToArray())
        {
            if (!pair.Value.IsCompleted) continue;
            _pending.Remove(pair.Key);
            results.Add(pair.Value.GetAwaiter().GetResult());
        }
        return results;
    }
}
