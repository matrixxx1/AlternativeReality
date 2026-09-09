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
    PlayerStructure,
    PointOfInterest,
    Airport,
    StateBoundary,
    PropertyBoundary
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TravelMode
{
    Walk,
    Run,
    Skateboard,
    Bike,
    Raft,
    DirtBike,
    Motorcycle,
    EBike,
    Ufo,
    Swim
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
    bool IsBaseEntity = true,
    IReadOnlyList<IReadOnlyList<GeometryPoint>>? InteriorRings = null);

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
    bool LaserOn = false,
    bool MagicHikingShoesOn = false,
    bool MagicRunningShoesOn = false,
    bool HatOn = false,
    double DirtBikeGasGallons = 0,
    double MotorcycleGasGallons = 0,
    string EquippedWeapon = "fist",
    double BodyHeat = 50,
    double MaximumBodyHeat = 100,
    string EquippedHat = "none",
    string EquippedShirt = "none",
    string EquippedPants = "none",
    int WantedLevel = 0,
    double EBikeRemainingMeters = 1609.344,
    DateTimeOffset? EnergyDrinkBoostUntilUtc = null,
    DateTimeOffset? EnergyDrinkCrashUntilUtc = null,
    DateTimeOffset? ProbedUntilUtc = null,
    DateTimeOffset? CandleUntilUtc = null,
    bool ShieldOn = false,
    string Ar15FireMode = "single",
    double FlamethrowerGasGallons = 0,
    bool IsTestCharacter = false,
    ProbulatorAbductionState? Abduction = null,
    DateTimeOffset? AsleepUntilUtc = null,
    double UfoRemainingMeters = 0,
    string? WaitingAtBusStopId = null,
    string? RidingBusId = null, double Air = 10, double MaximumAir = 10, bool SwimExhausted = false, string? DisplayTitle = null);

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
    string LocationId = "outdoor",
    string? MerchantCategory = null,
    string EquippedWeapon = "none",
    string? FactionId = null,
    bool IsQuestGiver = false,
    DateTimeOffset? EventStartedAtUtc = null,
    DateTimeOffset? EventEndsAtUtc = null,
    string? EventName = null,
    string WeaponQuality = "Common",
    bool IsTestCharacter = false,
    ProbulatorAbductionState? Abduction = null,
    DateTimeOffset? AsleepUntilUtc = null,
    bool OffersFoodDelivery = false,
    DateTimeOffset? FartUntilUtc = null, string? FartPose = null)
{
    public const int PortalSeconds = 6;
    public int EventPortalDurationSeconds => EventStartedAtUtc is null ? 0 : PortalSeconds;

    public bool IsPassingThroughPortal(DateTimeOffset now) =>
        EventStartedAtUtc is { } start && EventEndsAtUtc is { } end &&
        (now < start.AddSeconds(PortalSeconds) || now >= end.AddSeconds(-PortalSeconds));
}

public enum InventoryCategory { Weapon, Quest, Other }
public sealed record ItemStack(
    string ItemType,
    int Quantity,
    InventoryCategory Category = InventoryCategory.Other,
    double UnitWeightPounds = 1,
    bool CarriedInBackpack = true,
    string? Quality = null,
    PhotographState? Photograph = null);
public sealed record PhotographState(string Evidence, string Subject, string Kind, string Subtype, string LocationId, DateTimeOffset TakenAtUtc, string? ThumbnailUrl = null);
public sealed record InventoryState(
    string PlayerId,
    IReadOnlyList<ItemStack> Items,
    double WeightPounds = 0,
    double? MaximumWeightPounds = 50,
    int WeaponSlotsUsed = 0,
    int? MaximumWeaponSlots = null,
    int QuestSlotsUsed = 0,
    int? MaximumQuestSlots = null,
    int OtherSlotsUsed = 0,
    int? MaximumOtherSlots = null,
    bool Unlimited = false);
public sealed record ItemConfiguration(string ItemType, string DisplayName, string Effect, double Damage, double RangeMeters, long MinimumPriceCents, long MaximumPriceCents, bool ForSale = true, bool Single = false, string? AmmoType = null, double? SpeedModifierMph = null, double? VisibilityModifierMeters = null, double WeightPounds = 1, InventoryCategory Category = InventoryCategory.Other, bool CarriedInBackpack = true, double Accuracy = 1, double AttackIntervalSeconds = .5);
public sealed record MovementConfiguration(
    double BaseSpeedMph,
    double BaseVisibilityMeters,
    IReadOnlyDictionary<TerrainType, double> TerrainSpeedModifiersMph,
    IReadOnlyDictionary<TravelMode, double> TravelModeSpeedModifiersMph);
public sealed record ServerEventConfiguration(
    int WeatherRefreshMinutes = 60,
    int StreetLightsOnHour = 19,
    int StreetLightsOffHour = 7,
    int BuildingLightsRefreshMinutes = 15,
    int MerchantRefreshMinutes = 240,
    int DoorLockRefreshMinutes = 240,
    int UfoIntervalHours = 24,
    int UfoDurationMinutes = 2,
    int TrexIntervalHours = 24,
    int TrexDurationMinutes = 10,
    int BearIntervalHours = 24,
    int BearDurationMinutes = 10,
    int ServerTimeOffsetMinutes = 0,
    string WeatherMode = "live",
    double? TemperatureCelsius = null,
    int BrontosaurusIntervalHours = 24,
    int BrontosaurusDurationMinutes = 10,
    int StegosaurusIntervalHours = 24,
    int StegosaurusDurationMinutes = 10,
    int RaptorIntervalHours = 24,
    int RaptorDurationMinutes = 10,
    int LandOfGiantsIntervalHours = 24,
    int LandOfGiantsDurationMinutes = 10,
    string UfoEventName = "UFO Flyover",
    string TrexEventName = "T-Rex Portal",
    string BrontosaurusEventName = "Brontosaurus Portal",
    string StegosaurusEventName = "Stegosaurus Portal",
    string RaptorEventName = "Raptor Pack",
    string LandOfGiantsEventName = "Land of the Giants",
    string BearEventName = "The Great Bear",
    string ServerTimeMode = "auto",
    int ServerUtcOffsetMinutes = 0,
    int WantedSwatThreshold = 5,
    int RetroBattlesIntervalHours = 24, int RetroBattlesDurationMinutes = 10,
    DateTimeOffset? RetroBattlesEndsAtUtc = null);
public sealed record ServerConfigurationState(IReadOnlyList<ItemConfiguration> Items, MovementConfiguration Movement, ServerEventConfiguration Events);
public sealed record MerchantOffer(
    string ItemType,
    int Quantity,
    long UnitPriceCents,
    string? DisplayName = null,
    string? ImageKey = null,
    IReadOnlyDictionary<string, string>? Properties = null);
public sealed record TradeQuote(string MerchantId, string MerchantName, double FriendRating, IReadOnlyList<MerchantOffer> Offers, IReadOnlyList<MerchantOffer>? BuyOffers = null);
public sealed record PurchaseLine(string ItemType, int Quantity);
public sealed record HomeShopListing(string ItemType, int Quantity, long UnitPriceCents, string DisplayName, double UnitWeightPounds, string? Quality = null);
public sealed record HomeShopState(string FurnitureId, string OwnerName, bool IsOwner, IReadOnlyList<HomeShopListing> Listings, InventoryState? OwnerInventory = null);
public sealed record QuestState(
    string Id, string PlayerId, string GiverId, string GiverName, string Kind, string Status,
    string Title, string Description, long RewardCents,
    string? RequiredItemType = null, int RequiredQuantity = 0,
    string? TargetActorId = null, string? TargetName = null,
    string? DestinationActorId = null, string? DestinationName = null,
    string? DestinationClue = null, int Progress = 0,
    int? DeliveryMinutes = null, DateTimeOffset? DeadlineUtc = null, DateTimeOffset? FailedAtUtc = null,
    ActorState? DeliveryRecipient = null, bool FoodDamaged = false, string? FoodDamageReason = null, bool DeliveryRefused = false,
    IReadOnlyList<ItemStack>? RewardItems = null, WorldPosition? NextStagePosition = null, string? NextStageName = null,
    string? NextStageLocationId = null, IReadOnlyList<string>? ObjectiveActorIds = null);
public sealed record QuestInteraction(QuestState Quest, bool IsOffer, bool CanComplete, string InteractionActorId);
public sealed record RelationshipState(string PlayerId, string ActorId, double FriendRating);
public sealed record DungeonRoom(double X, double Y, double Width, double Height);
public sealed record DungeonWall(double X1, double Y1, double X2, double Y2, double DoorStart = -1, double DoorEnd = -1);
public sealed record TreasureChestState(string Id, WorldPosition Position, string LocationId, DateTimeOffset? ExpiresAtUtc = null, bool IsOpened = false, bool IsGrand = false);
public sealed record ChestContentsState(string ChestId, long MoneyCents, IReadOnlyList<ItemStack> Items);
public sealed record LootDropState(string Id, WorldPosition Position, string LocationId, long MoneyCents, IReadOnlyList<ItemStack> Items, DateTimeOffset ExpiresAtUtc,
    string DropKind = "loot", string? OwnerName = null, string? OwnerId = null);
public sealed record DungeonState(
    string Id, string BuildingId, double Width, double Height,
    IReadOnlyList<DungeonRoom> Rooms, IReadOnlyList<DungeonWall> Walls,
    WorldPosition Exit, IReadOnlyList<ActorState> Actors,
    IReadOnlyList<TreasureChestState> Chests, IReadOnlyList<string> RevealedCells,
    bool IsHome = false, IReadOnlyList<CanonicalEntity>? Furnishings = null,
    IReadOnlyList<GeometryPoint>? Footprint = null, int ExteriorWallCount = 4,
    int Level = 1, int LevelCount = 1, WorldPosition? Stairs = null,
    WorldPosition? Doorway = null, string? SessionId = null,
    bool IsStore = false, string? StoreCategory = null,
    int Difficulty = 1, bool IsCompleted = false, RetroBattleState? RetroBattle = null, EventBattleState? EventBattle = null, IReadOnlyList<DungeonWater>? WaterAreas = null, IReadOnlyList<DungeonBarrier>? Barriers = null);
public sealed record RetroEnemyState(int Id, double X, bool Defeated = false, int Hearts = 1, bool IsBoss = false);
public sealed record RetroBattleState(double ScrollSpeed, double TrackLength, double Scroll,
    double JumpHeight, double JumpVelocity, IReadOnlyList<RetroEnemyState> Enemies,
    DateTimeOffset UpdatedAtUtc, string? RewardChestId = null);
public sealed record BaseState(
    string BuildingId,
    string DoorId,
    WorldPosition Position,
    string OwnerName,
    double SquareFeet = 0,
    long PurchasePriceCents = 35_000_000);
public sealed record PublicBaseState(string BuildingId, string OwnerName);
public sealed record PlayerPrivateState(
    InventoryState Inventory,
    DungeonState? Dungeon,
    IReadOnlyList<RelationshipState> Relationships,
    IReadOnlyList<TreasureChestState>? Chests = null,
    IReadOnlyList<LootDropState>? Loot = null,
    BaseState? Base = null,
    long BasePurchasePriceCents = 35_000_000,
    ServerConfigurationState? ServerConfiguration = null,
    IReadOnlyList<string>? RevealedWorldAreas = null,
    IReadOnlyList<CanonicalEntity>? HomeStorage = null,
    InventoryState? HomeItemStorage = null,
    IReadOnlyList<QuestState>? Quests = null,
    bool CanEditHome = false,
    long HomeStorageMoneyCents = 0,
    IReadOnlyList<string>? LearnedRecipes = null, CraftingSkillState? CraftingSkill = null,
    IReadOnlyList<string>? OwnedVehicles = null, ProgressionState? Progression = null, InversionView? Inversions = null, IReadOnlyList<string>? Achievements = null, DateTimeOffset? MapleSyrupUntilUtc = null);
public sealed record CombatEvent(string AttackerId, string TargetId, string Weapon, WorldPosition Start, WorldPosition End, bool Hit, double Damage, bool TargetDied, string Message, double? TargetHealth = null,
    WorldPosition? RelocatedTo = null, string? StatusEffect = null, DateTimeOffset? StatusEffectUntilUtc = null, string? Dialogue = null, bool FleeInFear = false);

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
    bool IsAvailable = true,
    double WindDirectionDegrees = 0)
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

public sealed record StoreOpeningHours(int OpenHour)
{
    public int CloseHour => (OpenHour + 12) % 24;
    public bool IsOpen(DateTimeOffset serverTime) => (serverTime.TimeOfDay.TotalHours - OpenHour + 24) % 24 < 12;
}

public sealed record DoorLockState(string DoorId, string BuildingId, bool Locked, StoreOpeningHours? StoreHours = null);

public sealed record WorldSnapshot(
    RealityConfiguration Reality,
    WorldBounds Bounds,
    IReadOnlyList<CanonicalEntity> BaseEntities,
    IReadOnlyList<CanonicalEntity> RealityEntities,
    IReadOnlyList<PlayerState> Players,
    IReadOnlyList<ElevationSample> Elevation,
    WeatherState? Weather = null,
    IReadOnlyList<ActorState>? Actors = null,
    IReadOnlyList<WorldBounds>? LoadedAreas = null,
    IReadOnlyList<DoorLockState>? DoorLocks = null,
    DateTimeOffset? DoorLockCycleEndsAtUtc = null,
    IReadOnlyList<PublicBaseState>? PublicBases = null,
    IReadOnlyList<LootDropState>? Graves = null,
    IReadOnlyList<AreaHazardState>? AreaHazards = null,
    IReadOnlyList<WorldBounds>? MapCoverage = null,
    TransitSnapshot? Transit = null);

public sealed record WorldMapWindow(IReadOnlyList<CanonicalEntity> BaseEntities,
    IReadOnlyList<ElevationSample> Elevation, IReadOnlyList<WorldBounds> Coverage);

public sealed record ProbulatorAbductionState(string PilotId, DateTimeOffset StartedAtUtc, WorldPosition Origin,
    WorldPosition ShipPosition, WorldPosition DropPosition)
{
    public const int LiftSeconds = 10;
    public const int AboardSeconds = 15;
    public const int LowerSeconds = 2;
    public const int TotalSeconds = LiftSeconds + AboardSeconds + LowerSeconds;
    public DateTimeOffset LoweringAtUtc => StartedAtUtc.AddSeconds(LiftSeconds + AboardSeconds);
    public DateTimeOffset EndsAtUtc => LoweringAtUtc.AddSeconds(LowerSeconds);
}
