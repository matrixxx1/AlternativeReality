using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed class RealitySetupState
{
    private readonly string _locationPath;
    private readonly string _markerPath;

    public RealitySetupState(bool required, string locationPath, string markerPath)
    {
        Required = required;
        _locationPath = locationPath;
        _markerPath = markerPath;
    }

    public bool Required { get; private set; }

    public async Task CompleteAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var temporaryPath = _locationPath + ".tmp";
        var json = JsonSerializer.Serialize(new SavedRealityLocation(coordinate.Latitude, coordinate.Longitude), SharedJson.Options);
        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
        File.Move(temporaryPath, _locationPath, true);
        if (File.Exists(_markerPath)) File.Delete(_markerPath);
        Required = false;
    }

    public static SavedRealityLocation? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<SavedRealityLocation>(File.ReadAllText(path), SharedJson.Options); }
        catch (JsonException exception) { throw new InvalidOperationException($"The saved reality location is invalid: {path}", exception); }
    }
}

public sealed record SavedRealityLocation(double Latitude, double Longitude);
