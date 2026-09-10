using AlternateEarth.Geo;
using AlternateEarth.Shared;
namespace AlternateEarth.Tests;
public sealed class GardenGenerationTests
{
    private static readonly RegionId Region=new GeographicArea(new GeoCoordinate(45.5,-122.5),500).Region;
    private static CanonicalEntity House(string id,double x,double y)=>new(id,EntityKind.Building,new(Region,x,y),[new(x-5,y-5),new(x+5,y-5),new(x+5,y+5),new(x-5,y+5),new(x-5,y-5)],new Dictionary<string,string>{["building"]="house"});
    private static CanonicalEntity Door(CanonicalEntity h)=>new("door:"+h.Id,EntityKind.Door,h.Position with {Y=h.Position.Y-5},[],new Dictionary<string,string>{["buildingId"]=h.Id,["facingDegrees"]="270"});
    private static CanonicalEntity[] Neighborhood(int n)=>Enumerable.Range(0,n).SelectMany(i=>{var h=House("house"+i,100+i%10*40,100+i/10*40);return new[]{h,Door(h)};}).ToArray();
    [Fact] public void GardensAreCappedDeterministicAndBehindOppositeWall()
    {
        var features=Neighborhood(100);var bounds=new WorldBounds(0,0,1000,1000);var plots=GardenGenerator.Generate(features,bounds,32);
        Assert.Equal(5,plots.Count);Assert.Equal(plots.Select(e=>e.Id),GardenGenerator.Generate(features,bounds,32).Select(e=>e.Id));
        foreach(var plot in plots){var h=features.Single(e=>e.Id==plot.Properties["buildingId"]);Assert.True(plot.Geometry.Min(p=>p.Y)>h.Geometry.Max(p=>p.Y));Assert.True(GardenGenerator.Clear(plot.Position,bounds,features));}
    }
    [Fact] public void ClearRejectsWholeFootprintOverlapAndThinFeatures()
    {
        var p=new WorldPosition(Region,0,0);var bounds=new WorldBounds(-100,-100,100,100);
        foreach(var kind in new[]{EntityKind.Building,EntityKind.Road,EntityKind.Sidewalk,EntityKind.Water,EntityKind.Fence,EntityKind.Tree,EntityKind.Bush}){
            var obstacle=new CanonicalEntity("obstacle",kind,p with{X=2},[new(1.99,-5),new(2.01,5)],new Dictionary<string,string>());
            Assert.False(GardenGenerator.Clear(p,bounds,[obstacle]));
        }
        var forest=new CanonicalEntity("forest",EntityKind.Terrain,p,[new(-5,-5),new(5,-5),new(5,5),new(-5,5)],new Dictionary<string,string>{["terrain"]="forest"});
        Assert.False(GardenGenerator.Clear(p,bounds,[forest]));Assert.True(GardenGenerator.Clear(p,bounds,[]));Assert.False(GardenGenerator.Clear(p with{X=99},bounds,[]));
    }
    [Fact] public void IsolatedFarmHasTenDistinctPlotsAndNoPartialFarm()
    {
        var features=Neighborhood(100).Concat(new[]{House("farm",1500,1500)}).ToArray();var bounds=new WorldBounds(0,0,3000,3000);
        var plots=GardenGenerator.Generate(features,bounds,4).Where(p=>p.Properties["farm"]=="true").ToArray();Assert.Equal(10,plots.Length);Assert.Equal(10,plots.Select(p=>p.Properties["itemType"]).Distinct().Count());Assert.All(plots,p=>Assert.Equal("farm",p.Properties["buildingId"]));
        var crowded=features.Concat(new[]{House("neighbor",1700,1500)}).ToArray();Assert.DoesNotContain(GardenGenerator.Generate(crowded,bounds,4),p=>p.Properties["farm"]=="true");
    }
    [Fact] public void HosesAreCappedAndClearOfDoorAndDriveways()
    {
        var features=Neighborhood(100);var bounds=new WorldBounds(0,0,1000,1000);var hoses=GardenGenerator.GenerateHoses(features,bounds,42);Assert.Equal(5,hoses.Count);
        foreach(var hose in hoses){Assert.True(GardenGenerator.Clear(hose.Position,bounds,features,1,1.2));var door=features.Single(e=>e.Id==hose.Properties["doorId"]);Assert.True(hose.Position.Y<door.Position.Y);Assert.True(hose.Position.Distance2D(door.Position)>2);}
    }
    [Fact] public void FarmLivestockIsDeterministicClearAndDoesNotDuplicate()
    {
        var house=House("farm",1500,1500);var bounds=new WorldBounds(0,0,3000,3000);var features=new[]{house,GardenRules.Create("plot",house.Position with{X=1520},"corn",house.Id,true)};
        var animals=GardenGenerator.GenerateLivestock(features,bounds);Assert.Equal(2,animals.Count(FarmRules.IsCow));Assert.Equal(3,animals.Count(e=>!FarmRules.IsCow(e)));Assert.All(animals,e=>Assert.True(GardenGenerator.Clear(e.Position,bounds,features, FarmRules.IsCow(e)?3:1.6,2)));
        Assert.Equal(animals.Select(e=>e.Position),GardenGenerator.GenerateLivestock(features,bounds).Select(e=>e.Position));Assert.Empty(GardenGenerator.GenerateLivestock(features.Concat(animals).ToArray(),bounds));
    }
    [Fact] public void GardenStatsAndBookImproveChancesWithoutExceedingYieldLimits()
    {
        Assert.Equal(0,GardenRules.BuildChance(4,new(),true));Assert.True(GardenRules.BuildChance(10,new(),false)>GardenRules.BuildChance(5,new(),false));Assert.Equal(.05,GardenRules.BuildChance(5,new(),true)-GardenRules.BuildChance(5,new(),false),6);
        Assert.True(GardenRules.BuildChance(5,new(Intelligence:10,Luck:10),false)>GardenRules.BuildChance(5,new(),false));
        var rng=new Random(5);for(var i=0;i<1000;i++){var harvest=GardenRules.Yield(rng,new(Luck:100,Perception:100));Assert.InRange(harvest.Produce,1,10);Assert.InRange(harvest.Seeds,0,5);}
    }
}
