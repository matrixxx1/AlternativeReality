using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlternateEarth.Shared;

public static class Protocol
{
    public const int Version = 3;
}

public sealed record ClientEnvelope(string Type, JsonElement Payload);
public sealed record MoveRequest(double X, double Y, long Sequence);
public sealed record PathRequest(double X, double Y, long Sequence);
public sealed record SetTravelModeRequest(TravelMode Mode);
public sealed record RebuildAreaRequest(bool GodMode);
public sealed record PlaceObjectRequest(string ObjectType, double X, double Y, double RotationDegrees = 0);
public sealed record RemoveObjectRequest(string EntityId);
public sealed record RequestChunkRequest(int X, int Y);

public static class SharedJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
