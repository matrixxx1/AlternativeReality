using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlternateEarth.Shared;

public static class Protocol
{
    public const int Version = 28;
}

public sealed record ClientEnvelope(string Type, JsonElement Payload);
public sealed record MoveRequest(double X, double Y, long Sequence, double? MaximumDistanceMeters = null, double? DestinationX = null, double? DestinationY = null);
public sealed record PathRequest(double X, double Y, long Sequence);
public sealed record SetTravelModeRequest(TravelMode Mode);
public sealed record RebuildAreaRequest(bool GodMode);
public sealed record TeleportRequest(double X, double Y, bool GodMode);
public sealed record SayRequest(string Message);
public sealed record SetGodModeRequest(bool Enabled);
public sealed record TriggerWorldEventRequest(string EventType);
public sealed record EnterDungeonRequest(string DoorId);
public sealed record ExitDungeonRequest();
public sealed record ChangeDungeonLevelRequest(int Direction);
public sealed record CombatRequest(string TargetId, string Weapon);
public sealed record RequestTradeRequest(string MerchantId);
public sealed record ConfirmTradeRequest(string MerchantId, IReadOnlyList<PurchaseLine> Purchases, IReadOnlyList<PurchaseLine>? Sales = null);
public sealed record RequestQuestRequest(string ActorId);
public sealed record AcceptQuestRequest(string QuestId);
public sealed record CompleteQuestRequest(string QuestId, string ActorId);
public sealed record AbandonQuestRequest(string QuestId);
public sealed record CaptureQuestPetRequest(string ActorId);
public sealed record ChopVegetationRequest(string EntityId);
public sealed record AttackWorldObjectRequest(string EntityId);
public sealed record PickLockRequest(string DoorId);
public sealed record ConsumeItemRequest(string ItemType);
public sealed record DropItemRequest(string ItemType, int Quantity = 1);
public sealed record OpenChestRequest(string ChestId);
public sealed record TakeChestItemsRequest(string ChestId, IReadOnlyList<PurchaseLine> Items);
public sealed record ChestSeenRequest(string ChestId);
public sealed record RestAtBedRequest(string BedId);
public sealed record MoveFurnitureRequest(string FurnitureId, double X, double Y);
public sealed record RotateFurnitureRequest(string FurnitureId);
public sealed record StoreFurnitureRequest(string FurnitureId);
public sealed record PlaceFurnitureRequest(string FurnitureId, double X, double Y, double RotationDegrees = 0);
public sealed record OpenHomeStorageRequest(string ChestId);
public sealed record TransferHomeStorageRequest(string ChestId, string ItemType, int Quantity, bool ToStorage);
public sealed record PurchaseBaseRequest(string DoorId);
public sealed record SetLightsRequest(bool FlashlightOn, bool LanternOn, bool LaserOn);
public sealed record SetMagicHikingShoesRequest(bool Enabled);
public sealed record SetMagicRunningShoesRequest(bool Enabled);
public sealed record SetEquipmentRequest(string Slot, string? ItemType);
public sealed record UpdateItemConfigurationRequest(string ItemType, double Damage, double RangeMeters, long MinimumPriceCents, long MaximumPriceCents, double SpeedModifierMph = 0, double VisibilityModifierMeters = 0);
public sealed record ConfigureInventoryItemRequest(string ItemType, string Action);
public sealed record UpdateMovementConfigurationRequest(double BaseSpeedMph, double BaseVisibilityMeters, IReadOnlyDictionary<TerrainType, double> TerrainSpeedModifiersMph, IReadOnlyDictionary<TravelMode, double> TravelModeSpeedModifiersMph);
public sealed record UpdateServerEventsRequest(
    int WeatherRefreshMinutes, int StreetLightsOnHour, int StreetLightsOffHour,
    int BuildingLightsRefreshMinutes, int MerchantRefreshMinutes, int DoorLockRefreshMinutes,
    int UfoIntervalHours, int UfoDurationMinutes,
    int TrexIntervalHours, int TrexDurationMinutes,
    int BearIntervalHours, int BearDurationMinutes,
    int ServerTimeOffsetMinutes = 0, string WeatherMode = "live", double? TemperatureCelsius = null);
public sealed record PlaceObjectRequest(string ObjectType, double X, double Y, double RotationDegrees = 0);
public sealed record RemoveObjectRequest(string EntityId);
public sealed record RequestChunkRequest(int X, int Y);
public sealed record RequestAreaRequest(double X,double Y);

public static class SharedJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
