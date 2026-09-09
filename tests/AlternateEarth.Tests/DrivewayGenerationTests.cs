using System.Text.Json;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class DrivewayGenerationTests
{
    private static readonly RealityConfiguration Reality = new("driveways", "Driveways", 42,
        new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
    private static CanonicalEntity Entity(string id, EntityKind kind, GeometryPoint[] geometry, Dictionary<string,string>? properties = null) =>
        new(id, kind, new WorldPosition(Reality.Area.Region, geometry.Average(p => p.X), geometry.Average(p => p.Y)), geometry, properties ?? new());
    private static GeometryPoint[] Rectangle(double x, double y, double w, double h) =>
        [new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h),new(x,y)];
    private static CanonicalEntity[] Features()
    {
        var house = Entity("house", EntityKind.Building, Rectangle(0,0,10,10), new() { ["building"]="house" });
        var road = Entity("road", EntityKind.Road, [new(-50,-15),new(50,-15)], new() { ["highway"]="residential",["widthMeters"]="6" });
        return [house, road, ..DeterministicWorldGenerator.GenerateDoors([house,road])];
    }

    [Fact]
    public void ConnectsDoorToRoadWithOneStablePavementPolygon()
    {
        var features = Features();
        var driveway = Assert.Single(DrivewayGenerator.Generate(features));
        Assert.Equal("generated:driveway:house", driveway.Id);
        Assert.Equal(EntityKind.Terrain, driveway.Kind);
        Assert.Equal("pavement", driveway.Properties["terrain"]);
        Assert.Equal("house", driveway.Properties["buildingId"]);
        Assert.Equal(0, driveway.Geometry.Max(p=>p.Y));
        Assert.Equal(-12.15, driveway.Geometry.Min(p=>p.Y), 6);
        Assert.Equal(3, driveway.Geometry.Max(p=>p.X)-driveway.Geometry.Min(p=>p.X), 6);
        Assert.Equal(driveway.Geometry[0], driveway.Geometry[^1]);
        Assert.Equal(driveway.Geometry, Assert.Single(DrivewayGenerator.Generate(features.Reverse().ToArray())).Geometry);
        Assert.Empty(DrivewayGenerator.Generate([..features,driveway]));
        var secondDoor = features[2] with { Id="other-door" };
        Assert.Single(DrivewayGenerator.Generate([..features,secondDoor]));
    }

    [Theory]
    [InlineData(EntityKind.Building)]
    [InlineData(EntityKind.Water)]
    [InlineData(EntityKind.Fence)]
    [InlineData(EntityKind.Tree)]
    [InlineData(EntityKind.Vehicle)]
    public void RejectsBlockedSpace(EntityKind kind)
    {
        var obstacle = Entity("obstacle", kind, (kind is EntityKind.Tree or EntityKind.Vehicle)
            ? [new(5,-6)] : kind == EntityKind.Fence ? [new(0,-6),new(10,-6)] : Rectangle(3,-8,4,4));
        Assert.Empty(DrivewayGenerator.Generate([..Features(),obstacle]));
    }

    [Fact]
    public void IslandDrivewayRemainsDryInsideWaterMultipolygon()
    {
        var river = Entity("river", EntityKind.Water, Rectangle(-100,-100,200,200)) with
        { InteriorRings = [Rectangle(-60,-30,120,60)] };
        Assert.Single(DrivewayGenerator.Generate([..Features(),river]));
        Assert.Empty(DrivewayGenerator.Generate([..Features(),river with { InteriorRings = null }]));
    }

    [Fact]
    public void RequiresHouseDoorNearbyAccessibleRoadAndOutwardPath()
    {
        var f=Features();
        Assert.Empty(DrivewayGenerator.Generate(f[..2]));
        Assert.Empty(DrivewayGenerator.Generate([f[0],f[2]]));
        Assert.Empty(DrivewayGenerator.Generate([f[0] with { Properties=new Dictionary<string,string>{{"building","commercial"}} }, f[1],f[2]]));
        Assert.Empty(DrivewayGenerator.Generate([f[0], f[1] with { Geometry=[new(-50,-80),new(50,-80)] }, f[2]]));
        Assert.Empty(DrivewayGenerator.Generate([f[0], f[1] with { Properties=new Dictionary<string,string>{{"highway","motorway"}} }, f[2]]));
        Assert.Empty(DrivewayGenerator.Generate([f[0], f[1], f[2] with { Position=f[2].Position with { Y=10 } }]));
    }

    [Fact]
    public void HouseBelongsToOnlyOneAdjacentImport()
    {
        var f=Features(); var x=f[0].Position.X;
        Assert.Empty(DrivewayGenerator.Generate(f,new(-100,-100,x,100)));
        Assert.Single(DrivewayGenerator.Generate(f,new(x,-100,100,100)));
    }

    [Fact]
    public void ExistingMappedDrivewaySuppressesGeneratedDuplicate()
    {
        var mapped=Entity("mapped",EntityKind.Road,[new(5,0),new(5,-15)],new(){{"highway","service"},{"service","driveway"}});
        Assert.Empty(DrivewayGenerator.Generate([..Features(),mapped]));
    }

    [Fact]
    public void PavementOverridesUnderlyingGrassInEitherOrder()
    {
        var f=Features(); var driveway=Assert.Single(DrivewayGenerator.Generate(f));
        var grass=Entity("grass",EntityKind.Terrain,Rectangle(-100,-100,200,200),new(){{"terrain","grass"}});
        foreach(var entities in new CanonicalEntity[][] { [grass,driveway,..f], [driveway,grass,..f] })
        {
            var nav=new WorldNavigation(new(-100,-100,100,100),entities,[]);
            Assert.Equal(TerrainType.Pavement,nav.TerrainAt(5,-6));
            Assert.Equal(TerrainType.Grass,nav.TerrainAt(10,-6));
        }
    }

    [Fact]
    public async Task FreshImportPersistsDrivewayAndOldCacheIsNeverRetrofitted()
    {
        var directory=Path.Combine(Path.GetTempPath(),$"driveways-{Guid.NewGuid():N}");
        try
        {
            var provider=new Provider();
            var first=await new DeterministicWorldGenerator(provider,directory).GenerateAsync(Reality);
            var driveway=Assert.Single(first.Features,e=>e.Properties.GetValueOrDefault("subtype")=="driveway");
            Assert.DoesNotContain(first.Features,e=>e.Kind is EntityKind.Tree or EntityKind.Bush or EntityKind.Vehicle or EntityKind.StreetLight &&
                Math.Abs(e.Position.X-5)<1.5 && e.Position.Y<0 && e.Position.Y> -12.15);
            var path=Assert.Single(Directory.GetFiles(directory,"world-*.json"));
            var saved=await File.ReadAllTextAsync(path);
            var reloaded=await new DeterministicWorldGenerator(provider,directory).GenerateAsync(Reality);
            Assert.Single(reloaded.Features,e=>e.Id==driveway.Id);
            Assert.Equal(saved,await File.ReadAllTextAsync(path));
            // Simulate an existing pre-feature cache at the same version/path.
            var old=JsonSerializer.Serialize(first with { Features=first.Features.Where(e=>e.Id!=driveway.Id).ToArray() },SharedJson.Options);
            await File.WriteAllTextAsync(path,old);
            var unchanged=await new DeterministicWorldGenerator(provider,directory).GenerateAsync(Reality);
            Assert.DoesNotContain(unchanged.Features,e=>e.Id==driveway.Id);
            Assert.Equal(old,await File.ReadAllTextAsync(path));
            Assert.Equal(1,provider.Calls);
        }
        finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
    }
    private sealed class Provider : IGeographicProvider
    {
        public string Name=>"OpenStreetMap/Overpass";
        public int Calls;
        public Task<GeographicDataset> GetAreaAsync(GeographicArea area,CancellationToken cancellationToken=default)
        { Calls++;return Task.FromResult(new GeographicDataset(Name,area,Features()[..2],[],DateTimeOffset.UtcNow)); }
    }
}
