using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlternateEarth.Shared;

public static class Protocol
{
    public const int Version = 11;
}

public sealed record ClientEnvelope(string Type, JsonElement Payload);
public sealed record MoveRequest(double X, double Y, long Sequence);
public sealed record PathRequest(double X, double Y, long Sequence);
public sealed record SetTravelModeRequest(TravelMode Mode);
public sealed record RebuildAreaRequest(bool GodMode);
public sealed record TeleportRequest(double X, double Y, bool GodMode);
public sealed record SayRequest(string Message);
public sealed record SetGodModeRequest(bool Enabled);
public sealed record EnterDungeonRequest(string DoorId);
public sealed record ExitDungeonRequest();
public sealed record CombatRequest(string TargetId, string Weapon);
public sealed record RequestTradeRequest(string MerchantId);
public sealed record ConfirmTradeRequest(string MerchantId, IReadOnlyList<PurchaseLine> Purchases);
public sealed record ConsumeItemRequest(string ItemType);
public sealed record OpenChestRequest(string ChestId);
public sealed record ChestSeenRequest(string ChestId);
public sealed record RestAtBedRequest(string BedId);
public sealed record PurchaseBaseRequest(string DoorId);
public sealed record SetLightsRequest(bool FlashlightOn, bool LanternOn, bool LaserOn);
public sealed record SetMagicHikingShoesRequest(bool Enabled);
public sealed record SetMagicRunningShoesRequest(bool Enabled);
public sealed record SetEquipmentRequest(string Slot, string? ItemType);
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
