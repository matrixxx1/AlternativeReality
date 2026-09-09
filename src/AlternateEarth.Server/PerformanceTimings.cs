using System.Collections.Concurrent;
using System.Diagnostics;

namespace AlternateEarth.Server;

public sealed class PerformanceTimings
{
    private readonly ConcurrentDictionary<string, Timing> _timings = new();
    public IDisposable Measure(string name) => new Measurement(this, name);
    public IReadOnlyDictionary<string, Timing> Snapshot() => new Dictionary<string, Timing>(_timings);
    public sealed record Timing(long Count, double LastMilliseconds, double MaximumMilliseconds, double AverageMilliseconds, long SlowCount);
    private sealed class Measurement(PerformanceTimings owner, string name) : IDisposable
    {
        private readonly long _start = Stopwatch.GetTimestamp();
        public void Dispose()
        {
            var ms = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
            owner._timings.AddOrUpdate(name, new Timing(1, ms, ms, ms, ms >= 100 ? 1 : 0), (_, old) =>
                new(old.Count + 1, ms, Math.Max(old.MaximumMilliseconds, ms), old.AverageMilliseconds * .9 + ms * .1, old.SlowCount + (ms >= 100 ? 1 : 0)));
        }
    }
}
