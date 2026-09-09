using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class RealityMaintenanceTests
{
    [Fact]
    public async Task FreshRebuildBypassesCacheAndRemovesOtherBlocksOnlyAfterSuccessfulFetch()
    {
        var path=Path.Combine(Path.GetTempPath(),$"maintenance-{Guid.NewGuid():N}");
        try
        {
            var provider=new Provider();var generator=new DeterministicWorldGenerator(provider,path);
            var reality=new RealityConfiguration("test","Test",42,new(new(45.5,-122.5),500));
            await generator.GenerateAsync(reality);
            await generator.GenerateAsync(reality with { Area=new(new(45.51,-122.5),500) });
            var before=Directory.GetFiles(path,"world-*.json").ToDictionary(f=>f,File.ReadAllText);
            provider.Fail=true;
            await Assert.ThrowsAsync<HttpRequestException>(()=>generator.RebuildFromScratchAsync(reality));
            foreach(var (file,contents) in before)Assert.Equal(contents,File.ReadAllText(file));
            provider.Fail=false;
            await generator.RebuildFromScratchAsync(reality);
            Assert.Single(Directory.GetFiles(path,"world-*.json"));
            Assert.Equal(2,provider.FreshCalls);
            Assert.Equal(1,provider.Clears);
            Assert.Equal(2,provider.CachedCalls);
        }
        finally { if(Directory.Exists(path))Directory.Delete(path,true); }
    }

    [Fact]
    public void StorageSeparatesMapDatabaseAndOtherFiles()
    {
        var path=Path.Combine(Path.GetTempPath(),$"storage-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(path,"world-cache"));
            File.WriteAllBytes(Path.Combine(path,"world-cache","world-a.json"),new byte[100]);
            File.WriteAllBytes(Path.Combine(path,"world-cache","incomplete.tmp"),new byte[5]);
            File.WriteAllBytes(Path.Combine(path,"reality.db"),new byte[50]);
            File.WriteAllBytes(Path.Combine(path,"reality.db-wal"),new byte[10]);
            var info=RealityStorageInfo.Read(path);
            Assert.Equal(165,info.TotalBytes);Assert.Equal(100,info.MapBytes);
            Assert.Equal(1,info.MapBlocks);Assert.Equal(60,info.DatabaseBytes);Assert.True(info.Complete);
        }
        finally { if(Directory.Exists(path))Directory.Delete(path,true); }
    }
    private sealed class Provider:IGeographicProvider
    {
        public string Name=>"test";
        public bool Fail;public int FreshCalls,CachedCalls,Clears;
        public void ClearLegacyCache()=>Clears++;
        public Task<GeographicDataset> GetAreaAsync(GeographicArea area,CancellationToken cancellationToken=default)
        {CachedCalls++;return Task.FromResult(new GeographicDataset(Name,area,[],[],DateTimeOffset.UtcNow));}
        public Task<GeographicDataset> GetFreshAreaAsync(GeographicArea area,CancellationToken cancellationToken=default)
        {FreshCalls++;if(Fail)throw new HttpRequestException("Offline");return Task.FromResult(new GeographicDataset(Name,area,[],[],DateTimeOffset.UtcNow));}
    }
}
