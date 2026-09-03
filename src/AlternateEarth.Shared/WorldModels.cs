using System.Text.Json.Serialization;

namespace AlternateEarth.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntityKind
{
    Terrain,
    Road,
    Sidewalk,
    Building,
    Door,
    Water,
    Tree,
    Fence,
    Vehicle,
    ResourceNode,
    PlayerStructure
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TerrainType
{
    Grass,
    Forest,
    Sand,
    Pavement,
    Road,
    ShallowWater,
    DeepWater,
    Mud,
    Sidewalk
}

public readonly record struct GeometryPoint(double X, double Y, double Z = 0);

public sealed record CanonicalEntity(
    string Id,
    EntityKind Kind,
    WorldPosition Position,
    IReadOnlyList<GeometryPoint> Geometry,
    IReadOnlyDictionary<string, string> Properties,
    long Version = 1,
    bool IsBaseEntity = true);

public sealed record PlayerState(
    string Id,
    string Name,
    WorldPosition Position,
    long Version = 1,
    TerrainType Terrain = TerrainType.Grass,
    double SpeedMetersPerSecond = 0);

public sealed record WeatherState(
    string Condition,
    int WeatherCode,
    double TemperatureCelsius,
    double PrecipitationMillimeters,
    double WindSpeedKilometersPerHour,
    bool IsDay,
    DateTimeOffset ObservedAtUtc,
    string Source,
    bool IsAvailable = true)
{
    public static WeatherState Unavailable { get; } = new(
        "Weather unavailable", -1, 0, 0, 0, true, DateTimeOffset.MinValue, "none", false);
}

public sealed record ElevationSample(double X, double Y, double ElevationMeters);

public sealed record WorldBounds(double MinimumX, double MinimumY, double MaximumX, double MaximumY)
{
    public WorldPosition Clamp(WorldPosition position) => position with
    {
        X = Math.Clamp(position.X, MinimumX, MaximumX),
        Y = Math.Clamp(position.Y, MinimumY, MaximumY)
    };

    public bool Contains(double x, double y) => x >= MinimumX && x <= MaximumX && y >= MinimumY && y <= MaximumY;
}

public sealed record GeographicArea(GeoCoordinate Center, int SizeMeters)
{
    public RegionId Region => RegionId.FromGeo(Center);
    public WorldBounds Bounds
    {
        get
        {
            var center = new LocalTangentProjection(Region).Project(Center);
            var half = SizeMeters / 2.0;
            return new WorldBounds(center.X - half, center.Y - half, center.X + half, center.Y + half);
        }
    }
}

public sealed record RealityConfiguration(
    string Id,
    string Name,
    long Seed,
    GeographicArea Area,
    bool IsPublic = false,
    int MaximumPlayers = 32,
    bool PvpEnabled = false,
    bool PermanentDeath = false,
    bool BuildingDestruction = true,
    bool FriendlyFire = false,
    double GameSpeed = 1.0,
    bool ObjectPlacementEnabled = false);

public sealed record GeographicDataset(
    string Provider,
    GeographicArea Area,
    IReadOnlyList<CanonicalEntity> Features,
    IReadOnlyList<ElevationSample> Elevation,
    DateTimeOffset CachedAtUtc);

public sealed record WorldSnapshot(
    RealityConfiguration Reality,
    WorldBounds Bounds,
    IReadOnlyList<CanonicalEntity> BaseEntities,
    IReadOnlyList<CanonicalEntity> RealityEntities,
    IReadOnlyList<PlayerState> Players,
    IReadOnlyList<ElevationSample> Elevation,
    WeatherState? Weather = null);
