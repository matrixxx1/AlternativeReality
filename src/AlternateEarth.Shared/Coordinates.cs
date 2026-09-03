namespace AlternateEarth.Shared;

public readonly record struct GeoCoordinate(double Latitude, double Longitude, double ElevationMeters = 0);

public readonly record struct RegionId(int LatitudeBand, int LongitudeBand)
{
    public static RegionId FromGeo(GeoCoordinate coordinate) =>
        new((int)Math.Floor(coordinate.Latitude), (int)Math.Floor(coordinate.Longitude));

    public GeoCoordinate Origin => new(LatitudeBand + 0.5, LongitudeBand + 0.5);

    public override string ToString() => $"{(LatitudeBand >= 0 ? "N" : "S")}{Math.Abs(LatitudeBand):00}_{(LongitudeBand >= 0 ? "E" : "W")}{Math.Abs(LongitudeBand):000}";
}

public readonly record struct WorldPosition(RegionId Region, double X, double Y, double Z = 0)
{
    public double Distance2D(WorldPosition other)
    {
        if (Region != other.Region)
        {
            throw new InvalidOperationException("Positions in different regions require an ECEF conversion before measuring distance.");
        }

        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public readonly record struct ChunkCoordinate(RegionId Region, int X, int Y)
{
    public const int DefaultSizeMeters = 256;

    public static ChunkCoordinate FromPosition(WorldPosition position, int chunkSizeMeters = DefaultSizeMeters) =>
        new(position.Region, FloorDivide(position.X, chunkSizeMeters), FloorDivide(position.Y, chunkSizeMeters));

    private static int FloorDivide(double value, int divisor) => (int)Math.Floor(value / divisor);
}

/// <summary>
/// Deterministic WGS84-to-local-meter projection for a one-degree geographic region.
/// Local X is east, Y is north, and Z is elevation. Gameplay never uses latitude/longitude.
/// </summary>
public sealed class LocalTangentProjection
{
    private const double EquatorialRadius = 6_378_137.0;
    private const double EccentricitySquared = 6.69437999014e-3;
    private readonly double _originLatitudeRadians;
    private readonly double _originLongitudeRadians;
    private readonly double _metersPerRadianLatitude;
    private readonly double _metersPerRadianLongitude;

    public LocalTangentProjection(RegionId region)
    {
        Region = region;
        var origin = region.Origin;
        _originLatitudeRadians = DegreesToRadians(origin.Latitude);
        _originLongitudeRadians = DegreesToRadians(origin.Longitude);

        var sinLatitude = Math.Sin(_originLatitudeRadians);
        var denominator = Math.Sqrt(1 - (EccentricitySquared * sinLatitude * sinLatitude));
        _metersPerRadianLongitude = EquatorialRadius * Math.Cos(_originLatitudeRadians) / denominator;
        _metersPerRadianLatitude = EquatorialRadius * (1 - EccentricitySquared) / Math.Pow(1 - (EccentricitySquared * sinLatitude * sinLatitude), 1.5);
    }

    public RegionId Region { get; }

    public WorldPosition Project(GeoCoordinate coordinate)
    {
        if (RegionId.FromGeo(coordinate) != Region)
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Coordinate is outside this projection's geographic region.");
        }

        var latitude = DegreesToRadians(coordinate.Latitude);
        var longitude = DegreesToRadians(coordinate.Longitude);
        return new WorldPosition(
            Region,
            (longitude - _originLongitudeRadians) * _metersPerRadianLongitude,
            (latitude - _originLatitudeRadians) * _metersPerRadianLatitude,
            coordinate.ElevationMeters);
    }

    public GeoCoordinate Unproject(WorldPosition position)
    {
        if (position.Region != Region)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is outside this projection's geographic region.");
        }

        return new GeoCoordinate(
            RadiansToDegrees(_originLatitudeRadians + (position.Y / _metersPerRadianLatitude)),
            RadiansToDegrees(_originLongitudeRadians + (position.X / _metersPerRadianLongitude)),
            position.Z);
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180.0;
    private static double RadiansToDegrees(double value) => value * 180.0 / Math.PI;
}
