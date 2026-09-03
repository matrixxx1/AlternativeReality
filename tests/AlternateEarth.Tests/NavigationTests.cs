using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class NavigationTests
{
    private static readonly RegionId Region = new(45, -123);
    private static readonly WorldBounds Bounds = new(-100, -100, 100, 100);

    [Fact]
    public void UsesRepresentativeRealWorldWalkingSpeeds()
    {
        var navigation = CreateNavigation();

        Assert.Equal(3.5, navigation.SpeedFor(TerrainType.Sidewalk) * 2.236936, 3);
        Assert.Equal(3.5, navigation.SpeedFor(TerrainType.Road) * 2.236936, 3);
        Assert.Equal(2.0, navigation.SpeedFor(TerrainType.Forest) * 2.236936, 3);
        Assert.Equal(1.0, navigation.SpeedFor(TerrainType.Sand) * 2.236936, 3);
    }

    [Fact]
    public void TravelModesApplyAuthoritativeMultipliers()
    {
        var navigation = CreateNavigation();

        Assert.Equal(navigation.SpeedFor(TerrainType.Road) * 2, navigation.SpeedFor(TerrainType.Road, TravelMode.Run));
        Assert.Equal(navigation.SpeedFor(TerrainType.Road) * 1.5, navigation.SpeedFor(TerrainType.Road, TravelMode.Run, .5));
        Assert.Equal(navigation.SpeedFor(TerrainType.Road), navigation.SpeedFor(TerrainType.Road, TravelMode.Run, 0));
        Assert.Equal(navigation.SpeedFor(TerrainType.Road) * 4, navigation.SpeedFor(TerrainType.Road, TravelMode.Skateboard));
        Assert.Equal(navigation.SpeedFor(TerrainType.Sidewalk) * 3, navigation.SpeedFor(TerrainType.Sidewalk, TravelMode.Skateboard));
        Assert.False(WorldNavigation.SupportsTravelMode(TerrainType.Grass, TravelMode.Skateboard));
        Assert.True(WorldNavigation.SupportsTravelMode(TerrainType.Pavement, TravelMode.Skateboard));
    }

    [Fact]
    public void BuildingAndTreeAreSolid()
    {
        var building = Polygon("building", EntityKind.Building, 5, 5, 15, 15);
        var tree = new CanonicalEntity("tree", EntityKind.Tree, new WorldPosition(Region, -10, 0), Array.Empty<GeometryPoint>(),
            new Dictionary<string, string> { ["collisionRadius"] = "0.85" });
        var navigation = CreateNavigation(building, tree);

        Assert.True(navigation.IsBlocked(10, 10));
        Assert.True(navigation.IsBlocked(-10, 0));
        Assert.False(navigation.IsBlocked(0, 0));
    }

    [Fact]
    public void WaterHasShallowEdgesAndDeepInterior()
    {
        var water = Polygon("water", EntityKind.Water, 20, 20, 40, 40);
        var navigation = CreateNavigation(water);

        Assert.Equal(TerrainType.ShallowWater, navigation.TerrainAt(21, 30));
        Assert.Equal(TerrainType.DeepWater, navigation.TerrainAt(30, 30));
    }

    [Fact]
    public void ClickRouteGoesAroundBuilding()
    {
        var building = Polygon("building", EntityKind.Building, -2, -6, 2, 6);
        var navigation = CreateNavigation(building);
        var start = new WorldPosition(Region, -10, 0);

        var route = navigation.FindPath(start, 10, 0);

        Assert.True(route.Success, route.Message);
        Assert.True(route.Waypoints.Count >= 2);
        var previous = start;
        foreach (var waypoint in route.Waypoints)
        {
            Assert.True(navigation.CanTraverse(previous, waypoint, true));
            previous = waypoint;
        }
    }

    private static WorldNavigation CreateNavigation(params CanonicalEntity[] entities) =>
        new(Bounds, entities, new[] { new ElevationSample(0, 0, 12) });

    private static CanonicalEntity Polygon(string id, EntityKind kind, double minimumX, double minimumY, double maximumX, double maximumY) =>
        new(id, kind, new WorldPosition(Region, (minimumX + maximumX) / 2, (minimumY + maximumY) / 2),
            new GeometryPoint[]
            {
                new(minimumX, minimumY), new(maximumX, minimumY), new(maximumX, maximumY),
                new(minimumX, maximumY), new(minimumX, minimumY)
            },
            new Dictionary<string, string>());
}
