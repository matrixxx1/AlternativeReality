using System.Text.Json;
using AlternateEarth.Geo;
using AlternateEarth.Shared;

// Stage a complete replacement cache without touching the live world or player database.
// Usage: ReimportWorld snapshot.json manifest.json source-cache staging-directory
if (args.Length == 3 && args[0] == "--normalize")
    return await BuildingConsistency.NormalizeAsync(args[1], args[2]);
if (args.Length != 4) throw new ArgumentException("Expected snapshot, manifest, source cache, staging directory.");
if (string.Equals(Path.GetFullPath(args[2]).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(args[3]).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Staging directory must be separate from the live source cache.");
using var snapshot = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var reality = snapshot.RootElement.GetProperty("reality").Deserialize<RealityConfiguration>(SharedJson.Options)!;
var manifest = JsonSerializer.Deserialize<List<CacheEntry>>(await File.ReadAllTextAsync(args[1]), SharedJson.Options)!;
var areas = manifest.GroupBy(e => FormattableString.Invariant($"{e.Area.Center.Latitude:F6}:{e.Area.Center.Longitude:F6}:{e.Area.SizeMeters}"))
    .Select(g => g.OrderByDescending(e => File.GetLastWriteTimeUtc(Path.Combine(args[2], e.File))).First())
    .OrderBy(e => e.Area.Center.Latitude).ThenBy(e => e.Area.Center.Longitude).ToArray();
Directory.CreateDirectory(args[3]);
using var http = new HttpClient(new RequestLogHandler(Path.Combine(args[3], "overpass-responses"))) { BaseAddress = new("https://maps.mail.ru/osm/tools/overpass/"), Timeout = TimeSpan.FromSeconds(60) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("AlternateEarth/1.0");
var completed = new List<object>();
var failures = new List<object>();
var reportLock = new SemaphoreSlim(1, 1);
await Parallel.ForEachAsync(Enumerable.Range(0, areas.Length), new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (i, cancellationToken) =>
{
    var entry = areas[i];
    Console.WriteLine($"START {i+1}/{areas.Length} {entry.Area.Center.Latitude:F6},{entry.Area.Center.Longitude:F6}");
    var old = JsonSerializer.Deserialize<GeographicDataset>(await File.ReadAllTextAsync(Path.Combine(args[2], entry.File)), SharedJson.Options)!;
    var provider = new OverpassGeographicProvider(http, Path.Combine(args[3], "source-cache"), new SavedElevation(old.Elevation));
    var generator = new DeterministicWorldGenerator(provider, args[3]);
    var configuration = reality with { Area = entry.Area };
    try
    {
        // The staging directory contains only results from this importer, allowing safe resume.
        var result = await generator.GenerateAsync(configuration, cancellationToken);
        var buildings = result.Features.Count(f => f.Kind == EntityKind.Building);
        lock (completed) completed.Add(new { entry.Area, Buildings = buildings, Features = result.Features.Count });
        Console.WriteLine($"DONE {i+1}/{areas.Length} buildings={buildings} features={result.Features.Count}");
    }
    catch (Exception ex)
    {
        lock (failures) failures.Add(new { entry.Area, Error = ex.Message });
        Console.WriteLine($"FAILED {i+1}/{areas.Length} {ex.Message}");
    }
    await reportLock.WaitAsync(cancellationToken);
    try
    {
        string report;
        lock (completed) lock (failures) report = JsonSerializer.Serialize(new { Total = areas.Length, Completed = completed, Failures = failures }, SharedJson.Options);
        await File.WriteAllTextAsync(Path.Combine(args[3], "import-report.json"), report, cancellationToken);
    }
    finally { reportLock.Release(); }
});
Console.WriteLine($"FINISHED completed={completed.Count} failed={failures.Count} total={areas.Length}");
return failures.Count == 0 ? 0 : 1;

sealed record CacheEntry(string File, GeographicArea Area);
sealed class SavedElevation(IReadOnlyList<ElevationSample> samples) : IElevationProvider
{
    public Task<IReadOnlyList<ElevationSample>> GetElevationGridAsync(GeographicArea area, int samplesPerAxis, CancellationToken cancellationToken = default) => Task.FromResult(samples);
}

sealed class RequestLogHandler(string directory) : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var query = await request.Content!.ReadAsStringAsync(cancellationToken);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(query)));
        var path = Path.Combine(directory, key + ".json");
        if (File.Exists(path))
        {
            Console.WriteLine("SOURCE CACHE HIT");
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(await File.ReadAllTextAsync(path, cancellationToken)) };
        }
        var started = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"FETCH {request.RequestUri!.Host}");
        var result = await base.SendAsync(request, cancellationToken);
        Console.WriteLine($"FETCHED {request.RequestUri.Host} {(int)result.StatusCode} {started.Elapsed.TotalSeconds:F1}s");
        if (result.IsSuccessStatusCode)
        {
            var json = await result.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("remark", out var remark) || string.IsNullOrWhiteSpace(remark.GetString()))
            {
                await File.WriteAllTextAsync(path + ".tmp", json, cancellationToken);
                File.Move(path + ".tmp", path, true);
            }
        }
        return result;
    }
}
