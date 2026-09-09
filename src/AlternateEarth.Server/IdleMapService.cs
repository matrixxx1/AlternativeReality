using System.Diagnostics;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private WorldPosition[] _idleMapAnchors = [];
    private readonly Queue<RealityConfiguration> _idleCandidates = new();
    private string _idleCandidateSignature = "";
    private DateTimeOffset _idleAnchorsRefreshed;
    internal const double IdleMapRadius = 5 * 1609.344;
    internal static bool WithinIdleMapCap(WorldBounds bounds, WorldPosition anchor) =>
        new[] { (bounds.MinimumX, bounds.MinimumY), (bounds.MinimumX, bounds.MaximumY), (bounds.MaximumX, bounds.MinimumY), (bounds.MaximumX, bounds.MaximumY) }
            .All(p => Math.Sqrt(Math.Pow(p.Item1 - anchor.X, 2) + Math.Pow(p.Item2 - anchor.Y, 2)) <= IdleMapRadius);
    public async Task<bool> PrepareOneIdleMapAsync(double cpuFraction, CancellationToken token)
    {
        if (_activeMapOperations.Count != 0 || _players.Count > 0 && (cpuFraction > .08 || _activeInversion is not null)) return false;
        if (GC.GetGCMemoryInfo().MemoryLoadBytes > GC.GetGCMemoryInfo().HighMemoryLoadThresholdBytes * .7) return false;
        var now = _probulatorClock.GetUtcNow();
        if (_idleAnchorsRefreshed.AddMinutes(5) < now)
        {
            var anchors = new List<WorldPosition>();
            foreach (var account in await _store.LoadAccountRosterAsync(token))
            {
                var home = await _store.LoadBaseAssignmentAsync(account.AccountId, Configuration.Id, token);
                if (home?.Position is { } point) anchors.Add(point);
                foreach (var character in account.Characters)
                {
                    var saved = await _store.LoadCharacterAsync(Configuration.Id, character.Id, token);
                    if (saved is { LocationId: "outdoor" }) anchors.Add(saved.Position);
                }
            }
            _idleMapAnchors = anchors.Where(p => p.Region == Configuration.Area.Region).Distinct().ToArray(); _idleAnchorsRefreshed = now;
        }
        var all = _idleMapAnchors.Concat(_players.Values.Where(p => p.LocationId == "outdoor").Select(p => p.Position)).Distinct().ToArray();
        if (all.Length == 0 || !await _areaPrefetchLock.WaitAsync(0, token)) return false;
        try
        {
            var signature = string.Join(";",all.Select(p=>AreaCellFor(p.X,p.Y)).Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y));
            if (_idleCandidateSignature != signature)
            {
                _idleCandidateSignature = signature;_idleCandidates.Clear();var seen = new HashSet<(int,int)>();
                var radius = (int)Math.Ceiling(IdleMapRadius / Configuration.Area.SizeMeters);
                for (var ring = 0; ring <= radius; ring++) foreach(var anchor in all)
                {
                    var cell=AreaCellFor(anchor.X,anchor.Y);
                    for(var x=-ring;x<=ring;x++)for(var y=-ring;y<=ring;y++)
                    {
                        if(Math.Max(Math.Abs(x),Math.Abs(y))!=ring)continue;
                        RealityConfiguration config;try{config=AreaConfiguration(cell.X+x,cell.Y+y);}catch(InvalidOperationException){continue;}
                        if(WithinIdleMapCap(config.Area.Bounds,anchor)&&seen.Add((cell.X+x,cell.Y+y)))_idleCandidates.Enqueue(config);
                    }
                }
            }
            // Bound filesystem probes, including when most of the five-mile disk is already cached.
            for(var checkedCount=0;checkedCount<128&&_idleCandidates.TryPeek(out var config);checkedCount++)
            {
                token.ThrowIfCancellationRequested();
                if(!all.Any(anchor=>WithinIdleMapCap(config.Area.Bounds,anchor))||_generator.IsGeneratedWorldCached(config)){_idleCandidates.Dequeue();continue;}
                if(_activeMapOperations.Count!=0)return false;
                _activeMapOperations["idle-prefetch"]="Preparing one idle map block within five miles of a player or base";
                await _generator.GenerateAsync(config,token);_idleCandidates.Dequeue();Interlocked.Increment(ref _preparedAreaCount);return true;
            }
            return false;
        }
        finally { _activeMapOperations.TryRemove("idle-prefetch", out _); _areaPrefetchLock.Release(); }
    }
}

public sealed class IdleMapService(RealityWorld world, ILogger<IdleMapService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime; var last = Stopwatch.GetTimestamp();
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var current = process.TotalProcessorTime; var now = Stopwatch.GetTimestamp();
            var usage = (current - cpu).TotalSeconds / Math.Max(1, Stopwatch.GetElapsedTime(last, now).TotalSeconds * Environment.ProcessorCount);
            cpu = current; last = now;
            try { await world.PrepareOneIdleMapAsync(usage, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Idle map preparation paused; it will retry later"); }
        }
    }
}
