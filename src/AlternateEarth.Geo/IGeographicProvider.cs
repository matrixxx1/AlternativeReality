using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public interface IGeographicProvider
{
    string Name { get; }
    Task<GeographicDataset> GetAreaAsync(GeographicArea area, CancellationToken cancellationToken = default);
}

public interface IElevationProvider
{
    Task<IReadOnlyList<ElevationSample>> GetElevationGridAsync(
        GeographicArea area,
        int samplesPerAxis,
        CancellationToken cancellationToken = default);
}

public interface IWeatherProvider
{
    Task<WeatherState> GetCurrentAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default);
}
