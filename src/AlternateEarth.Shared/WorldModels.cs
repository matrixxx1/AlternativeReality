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
    Bush,
    Fence,
    Vehicle,
    StreetLight,
    TreasureChest,
    Tombstone,
    Animal,
    Npc,
    ResourceNode,
    PlayerStructure
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TravelMode
{
    Walk,
    Run,
    Skateboard,
    Bike,
    Raft
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
    double SpeedMetersPerSecond = 0,
    double HealthHearts = 10,
    double MaximumHealthHearts = 10,
    TravelMode TravelMode = TravelMode.Walk,
    double Stamina = 10,
    double MaximumStamina = 10,
    double Water = 10,
    double MaximumWater = 10,
    long WalletCents = 0,
    bool GodMode = false,
    DateTimeOffset? FoodProtectedUntilUtc = null,
    DateTimeOffset? WaterProtectedUntilUtc = null,
    string LocationId = "outdoor",
    bool FlashlightOn = false,
    bool LanternOn = false,
    bool LaserOn = false);

public sealed record ActorState(
    string Id,
    EntityKind Kind,
    string Subtype,
    string Name,
    WorldPosition Position,
    string Facing = "south",
    bool IsMoving = false,
    long Version = 1,
    double HealthHearts = 5,
    double MaximumHealthHearts = 5,
    double FriendRating = 0,
    bool IsMerchant = false,
    TravelMode TravelMode = TravelMode.Walk,
    string LocationId = "outdoor");

public sealed record ItemStack(string ItemType, int Quantity);
public sealed record InventoryState(string PlayerId, IReadOnlyList<ItemStack> Items);
public sealed record MerchantOffer(string ItemType, int Quantity, long UnitPriceCents);
public sealed record TradeQuote(string MerchantId, string MerchantName, double FriendRating, IReadOnlyList<MerchantOffer> Offers);
public sealed record PurchaseLine(string ItemType, int Quantity);
public sealed record RelationshipState(string PlayerId, string ActorId, double FriendRating);
public sealed record DungeonRoom(double X, double Y, double Width, double Height);
public sealed record DungeonWall(double X1, double Y1, double X2, double Y2, double DoorStart = -1, double DoorEnd = -1);
public sealed record TreasureChestState(string Id, WorldPosition Position, string LocationId, DateTimeOffset? ExpiresAtUtc = null, bool IsOpened = false);
public sealed record LootDropState(string Id, WorldPosition Position, string LocationId, long MoneyCents, IReadOnlyList<ItemStack> Items, DateTimeOffset ExpiresAtUtc);
public sealed record DungeonState(
    string Id, string BuildingId, double Width, double Height,
    IReadOnlyList<DungeonRoom> Rooms, IReadOnlyList<DungeonWall> Walls,
    WorldPosition Exit, IReadOnlyList<ActorState> Actors,
    IReadOnlyList<TreasureChestState> Chests, IReadOnlyList<string> RevealedCells,
    bool IsHome = false, IReadOnlyList<CanonicalEntity>? Furnishings = null);
public sealed record BaseState(string BuildingId, string DoorId, WorldPosition Position, string OwnerName);
public sealed record PlayerPrivateState(
    InventoryState Inventory,
    DungeonState? Dungeon,
    IReadOnlyList<RelationshipState> Relationships,
    IReadOnlyList<TreasureChestState>? Chests = null,
    IReadOnlyList<LootDropState>? Loot = null,
    BaseState? Base = null);
public sealed record CombatEvent(string AttackerId, string TargetId, string Weapon, WorldPosition Start, WorldPosition End, bool Hit, double Damage, bool TargetDied, string Message, double? TargetHealth = null);

public sealed record ChatMessage(
    string Id,
    string PlayerId,
    string Username,
    string Message,
    DateTimeOffset SaidAtUtc);

public sealed record WeatherState(
    string Condition,
    int WeatherCode,
    double TemperatureCelsius,
    double PrecipitationMillimeters,
    double WindSpeedKilometersPerHour,
    bool IsDay,
    DateTimeOffset ObservedAtUtc,
    string Source,
    DateTimeOffset? SunriseUtc = null,
    DateTimeOffset? SunsetUtc = null,
    string MoonPhase = "Unknown",
    double MoonIllumination = 0,
    bool IsAvailable = true)
{
    public static WeatherState Unavailable { get; } = new(
        "Weather unavailable", -1, 0, 0, 0, true, DateTimeOffset.MinValue, "none", IsAvailable: false);
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
    WeatherState? Weather = null,
    IReadOnlyList<ActorState>? Actors = null);
