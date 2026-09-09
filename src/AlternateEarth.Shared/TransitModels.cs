using System.Globalization;

namespace AlternateEarth.Shared;

public static class RoadClassification
{
    public static string Classify(IReadOnlyDictionary<string, string> tags) => tags.GetValueOrDefault("highway") switch
    {
        "motorway" or "motorway_link" => "freeway",
        "trunk" or "trunk_link" => "highway",
        "primary" or "primary_link" or "secondary" or "secondary_link" or "tertiary" or "tertiary_link" => "mainRoad",
        "residential" or "unclassified" or "living_street" or "service" => "surfaceStreet",
        _ => "path"
    };

    public static int OneWay(IReadOnlyDictionary<string, string> tags) => tags.GetValueOrDefault("oneway") switch
    {
        "-1" or "reverse" => -1,
        "yes" or "1" or "true" => 1,
        "no" or "0" or "false" => 0,
        _ => tags.GetValueOrDefault("junction") == "roundabout" || tags.GetValueOrDefault("highway") is "motorway" or "motorway_link" ? 1 : 0
    };

    public static double Width(IReadOnlyDictionary<string, string> tags) => double.TryParse(tags.GetValueOrDefault("widthMeters"), NumberStyles.Float, CultureInfo.InvariantCulture, out var width) && double.IsFinite(width) ? Math.Clamp(width, 1, 40) : 7;
    public static bool AllowsBus(IReadOnlyDictionary<string, string> tags) => Classify(tags) != "path" &&
        Width(tags) >= 5.5 && tags.GetValueOrDefault("area") != "yes" &&
        (tags.GetValueOrDefault("bus") is "yes" or "designated" ||
            !new[] { "no", "private" }.Contains(tags.GetValueOrDefault("access")) &&
            !new[] { "no", "private" }.Contains(tags.GetValueOrDefault("motor_vehicle")) &&
            !new[] { "no", "private" }.Contains(tags.GetValueOrDefault("vehicle")) && tags.GetValueOrDefault("bus") != "no");
    public static bool AllowsStop(IReadOnlyDictionary<string, string> tags) => AllowsBus(tags) &&
        Classify(tags) is "highway" or "mainRoad" or "surfaceStreet" && tags.GetValueOrDefault("motorroad") != "yes" &&
        !(tags.GetValueOrDefault("highway") ?? "").EndsWith("_link", StringComparison.Ordinal) &&
        tags.GetValueOrDefault("tunnel") is not ("yes" or "building_passage") && tags.GetValueOrDefault("bridge") != "yes";
}

public sealed record BusStopState(string Id, string Name, WorldPosition Position, string EdgeId, double DistanceMeters, string Direction,
    WorldPosition? BenchPosition = null, double HeadingRadians = 0);
public sealed record BusState(string Id, string RouteId, string RouteName, WorldPosition Position, double HeadingRadians,
    double SpeedMetersPerSecond = 0, double HealthHearts = 100, string Status = "driving", long Version = 1);
public sealed record BusRouteState(string Id, string Name, IReadOnlyList<WorldPosition> Path, IReadOnlyList<string> StopIds);
public sealed record TransitSnapshot(IReadOnlyList<BusStopState> Stops, IReadOnlyList<BusState> Buses, IReadOnlyList<BusRouteState> Routes);
public sealed record WaitForBusRequest(string StopId);
public sealed record RoadTurnRestriction(string FromWayId, string ToWayId, string ViaNodeId, string Restriction);
