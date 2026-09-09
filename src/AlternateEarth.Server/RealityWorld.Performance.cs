using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public PerformanceTimings Timings { get; } = new();
    private readonly ActorRoutePlanner _routePlanner = new();
    private readonly Dictionary<string, DateTimeOffset> _actorRouteRetry = new();
    private int _actorRouteCursor;
    public int ActorRouteWorkers => _routePlanner.Capacity;

    private void ApplyCompletedActorRoutes(DateTimeOffset now)
    {
        foreach (var result in _routePlanner.TakeCompleted())
        {
            if (!_actors.TryGetValue(result.Actor.Id, out var actor) || !result.Matches(actor, Navigation)) continue;
            _actorRoutes[actor.Id] = new Queue<WorldPosition>(result.Waypoints);
            if (result.Waypoints.Count == 0) _actorRouteRetry[actor.Id] = now.AddSeconds(3);
        }
        foreach (var id in _actorRouteRetry.Where(p => p.Value <= now).Select(p => p.Key).ToArray()) _actorRouteRetry.Remove(id);
    }
}
