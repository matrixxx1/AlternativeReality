using AlternateEarth.Geo;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class RoadContinuityTests
{
    [Fact]
    public async Task AdjacentBlocksKeepIdenticalCompleteRoadsIncludingOutsideEndpoints()
    {
        const string response="""
        {"elements":[
        {"type":"node","id":1,"lat":45.5,"lon":-122.508},
        {"type":"node","id":2,"lat":45.5,"lon":-122.500},
        {"type":"node","id":3,"lat":45.5,"lon":-122.492},
        {"type":"way","id":10,"nodes":[1,2,3],"tags":{"highway":"primary","oneway":"-1","name":"Continuous Road","bridge":"yes","layer":"1","maxspeed":"35 mph"}}
        ]}
        """;
        var path=Path.Combine(Path.GetTempPath(),"road-seams-"+Guid.NewGuid().ToString("N"));
        try
        {
            using var client=new HttpClient(new Handler(response)){BaseAddress=new Uri("https://example.test/")};
            var provider=new OverpassGeographicProvider(client,path,new FlatElevationProvider());
            var west=await provider.GetAreaAsync(new(new(45.5,-122.502),400));
            var east=await provider.GetAreaAsync(new(new(45.5,-122.497),400));
            var a=Assert.Single(west.Features);var b=Assert.Single(east.Features);
            Assert.Equal(a.Id,b.Id);Assert.Equal(a.Geometry,b.Geometry);Assert.Equal(3,a.Geometry.Count);
            Assert.Equal("1,2,3",a.Properties["osmNodeIds"]);Assert.Equal("mainRoad",a.Properties["roadClass"]);
            Assert.Equal("-1",a.Properties["oneway"]);Assert.Equal("1",a.Properties["layer"]);Assert.Equal("35 mph",a.Properties["maxspeed"]);
            Assert.False(west.Area.Bounds.Contains(a.Geometry[^1].X,a.Geometry[^1].Y));
        }
        finally {Directory.Delete(path,true);}
    }
    private sealed class Handler(string json):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(json)});
    }
}
