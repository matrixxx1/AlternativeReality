using System.Text.Json;
using AlternateEarth.Geo;
using AlternateEarth.Shared;

static class BuildingConsistency
{
    public static async Task<int> NormalizeAsync(string snapshotPath, string stage)
    {
        using var snapshot = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath));
        var reality = snapshot.RootElement.GetProperty("reality").Deserialize<RealityConfiguration>(SharedJson.Options)!;
        var files = Directory.GetFiles(stage, "world-*.json");
        var buildings = new Dictionary<(RegionId Region, string Id), (CanonicalEntity Entity, DateTimeOffset Imported)>();
        var conflicts = new HashSet<string>();
        foreach (var file in files)
        {
            var world = await ReadAsync(file);
            foreach (var entity in world.Features.Where(e => e.Kind == EntityKind.Building))
            {
                var key = (entity.Position.Region, entity.Id);
                if (buildings.TryGetValue(key, out var previous))
                {
                    if (!previous.Entity.Geometry.SequenceEqual(entity.Geometry)) conflicts.Add(entity.Id);
                    if (previous.Imported >= world.CachedAtUtc) continue;
                }
                buildings[key] = (entity, world.CachedAtUtc);
            }
        }
        // Public replicas can serve different revisions during a long import.
        // Use the last imported complete footprint consistently, then regenerate
        // doors, driveways, and obstacles from it instead of moving roofs alone.
        var regenerated = new List<string>();
        foreach (var file in files)
        {
            var world = await ReadAsync(file);
            if (!world.Features.Any(e => e.Kind == EntityKind.Building &&
                !e.Geometry.SequenceEqual(buildings[(e.Position.Region, e.Id)].Entity.Geometry))) continue;
            var source = world with
            {
                Provider = "OpenStreetMap/Overpass",
                Features = world.Features.Where(e => e.Id.StartsWith("geo:osm:", StringComparison.Ordinal))
                    .Select(e => e.Kind == EntityKind.Building ? buildings[(e.Position.Region, e.Id)].Entity : e).ToArray()
            };
            var generator = new DeterministicWorldGenerator(new ImportedSource(source), stage);
            await generator.GenerateAsync(reality with { Area = world.Area }, fresh: true);
            regenerated.Add(Path.GetFileName(file));
            Console.WriteLine($"NORMALIZED {Path.GetFileName(file)}");
        }
        await File.WriteAllTextAsync(Path.Combine(stage, "building-consistency-report.json"),
            JsonSerializer.Serialize(new { ConflictingBuildingIds = conflicts.Order().ToArray(), RegeneratedAreas = regenerated }, SharedJson.Options));
        Console.WriteLine($"CONSISTENT buildings={buildings.Count} differingSourceRevisions={conflicts.Count} regeneratedAreas={regenerated.Count}");
        return 0;
    }

    private static async Task<GeographicDataset> ReadAsync(string file) =>
        JsonSerializer.Deserialize<GeographicDataset>(await File.ReadAllTextAsync(file), SharedJson.Options)!;

    private sealed class ImportedSource(GeographicDataset source) : IGeographicProvider
    {
        public string Name => "Reimported OpenStreetMap";
        public Task<GeographicDataset> GetAreaAsync(GeographicArea area, CancellationToken cancellationToken = default) => Task.FromResult(source);
    }
}
