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
        Assert.True(navigation.SpeedFor(TerrainType.Grass, TravelMode.Bike) > navigation.SpeedFor(TerrainType.Grass));
        Assert.Equal(40.0, navigation.SpeedFor(TerrainType.Road, TravelMode.DirtBike) * 2.236936, 3);
        Assert.Equal(90.0, navigation.SpeedFor(TerrainType.Road, TravelMode.Motorcycle) * 2.236936, 3);
        Assert.Equal(300.0, navigation.SpeedFor(TerrainType.Road, TravelMode.Ufo) * 2.236936, 3);
        Assert.True(navigation.SpeedFor(TerrainType.Grass, TravelMode.DirtBike) > navigation.SpeedFor(TerrainType.Grass, TravelMode.Motorcycle));
        Assert.False(WorldNavigation.SupportsTravelMode(TerrainType.DeepWater, TravelMode.DirtBike));
        Assert.False(WorldNavigation.SupportsTravelMode(TerrainType.DeepWater, TravelMode.Motorcycle));
        Assert.Equal(3.0, navigation.SpeedFor(TerrainType.DeepWater, TravelMode.Raft) * 2.236936, 3);
        Assert.True(WorldNavigation.SupportsTravelMode(TerrainType.DeepWater, TravelMode.Raft));
    }

    [Fact]
    public void MagicHikingShoesBoostWalkingAndRunningWhileIgnoringSelectedTerrainPenalties()
    {
        var navigation = CreateNavigation();
        var pavedWalkingSpeed = navigation.SpeedFor(TerrainType.Pavement);

        Assert.Equal(pavedWalkingSpeed * 2, navigation.SpeedFor(TerrainType.Grass, TravelMode.Walk, magicHikingShoes: true));
        Assert.Equal(pavedWalkingSpeed * 2, navigation.SpeedFor(TerrainType.Mud, TravelMode.Walk, magicHikingShoes: true));
        Assert.Equal(pavedWalkingSpeed * 2, navigation.SpeedFor(TerrainType.ShallowWater, TravelMode.Walk, magicHikingShoes: true));
        Assert.Equal(pavedWalkingSpeed * 4, navigation.SpeedFor(TerrainType.Grass, TravelMode.Run, 1, magicHikingShoes: true));
        Assert.Equal(WorldNavigation.RunningStaminaDrain(1, false) / 2, WorldNavigation.RunningStaminaDrain(1, true));
        Assert.Equal(navigation.SpeedFor(TerrainType.Grass, TravelMode.Bike), navigation.SpeedFor(TerrainType.Grass, TravelMode.Bike, magicHikingShoes: true));
    }

    [Fact]
    public void MagicRunningShoesTripleWalkingAndRunningWithoutChangingTerrainRules()
    {
        var navigation = CreateNavigation();

        Assert.Equal(navigation.SpeedFor(TerrainType.Road) * 3, navigation.SpeedFor(TerrainType.Road, TravelMode.Walk, magicRunningShoes: true));
        Assert.Equal(navigation.SpeedFor(TerrainType.Grass) * 6, navigation.SpeedFor(TerrainType.Grass, TravelMode.Run, 1, magicRunningShoes: true));
        Assert.Equal(navigation.SpeedFor(TerrainType.Mud) * 3, navigation.SpeedFor(TerrainType.Mud, TravelMode.Walk, magicRunningShoes: true));
        Assert.False(WorldNavigation.MagicRunningShoesReduceStaminaOn(TerrainType.Road));
        Assert.False(WorldNavigation.MagicRunningShoesReduceStaminaOn(TerrainType.Sidewalk));
        Assert.True(WorldNavigation.MagicRunningShoesReduceStaminaOn(TerrainType.Pavement));
        Assert.True(WorldNavigation.MagicRunningShoesReduceStaminaOn(TerrainType.Grass));
        Assert.Equal(WorldNavigation.WaterDrain(1, false) / 2, WorldNavigation.WaterDrain(1, true));
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
    public void OpenWaterwayKeepsItsThreeMeterWidthAcrossSpatialCells()
    {
        var stream = new CanonicalEntity("stream", EntityKind.Water, new WorldPosition(Region, 31, 0),
            new GeometryPoint[] { new(31, -10), new(31, 10) }, new Dictionary<string, string> { ["waterway"] = "stream" });
        var navigation = CreateNavigation(stream);

        Assert.Equal(TerrainType.ShallowWater, navigation.TerrainAt(32.4, 0));
        Assert.Equal(TerrainType.Grass, navigation.TerrainAt(32.6, 0));
    }

    [Fact]
    public void OpenWaterwayWithManyPointsDoesNotBecomeADeepWaterPolygon()
    {
        var stream = new CanonicalEntity("bending-stream", EntityKind.Water, new WorldPosition(Region, 30, 30),
            new GeometryPoint[] { new(20, 20), new(40, 20), new(40, 40), new(20, 40) },
            new Dictionary<string, string> { ["waterway"] = "stream" });
        var navigation = CreateNavigation(stream);

        Assert.Equal(TerrainType.Grass, navigation.TerrainAt(30, 30));
        Assert.Equal(TerrainType.ShallowWater, navigation.TerrainAt(30, 20.5));
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

    [Fact]
    public void RaftRouteStaysInWaterAndRejectsLand()
    {
        var water = Polygon("water", EntityKind.Water, -20, -20, 20, 20);
        var navigation = CreateNavigation(water);
        var start = new WorldPosition(Region, -15, 0);
        bool WaterOnly(TerrainType terrain) => WorldNavigation.SupportsTravelMode(terrain, TravelMode.Raft);

        var acrossWater = navigation.FindPath(start, 15, 0, terrain => navigation.SpeedFor(terrain, TravelMode.Raft), WaterOnly);
        var ontoLand = navigation.FindPath(start, 25, 0, terrain => navigation.SpeedFor(terrain, TravelMode.Raft), WaterOnly);

        Assert.True(acrossWater.Success, acrossWater.Message);
        Assert.False(ontoLand.Success);
        var previous = start;
        foreach (var waypoint in acrossWater.Waypoints)
        {
            Assert.True(navigation.CanTraverse(previous, waypoint, WaterOnly));
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
