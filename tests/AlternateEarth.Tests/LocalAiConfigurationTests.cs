using AlternateEarth.Server;

namespace AlternateEarth.Tests;

public sealed class LocalAiConfigurationTests
{
    [Fact]
    public void SavedSettingsReplaceDefaultsAndSurviveReload()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var config = new LocalAiConfiguration(path, new LocalAiSettings());
            var old = config.Current;
            config.Save(new LocalAiSettings { BaseUrl = "http://localhost:8080/", ProjectName = "Other", Enabled = false, RequestTimeoutSeconds = 300 });
            Assert.Equal("http://mmac.local:80", old.BaseUrl);
            Assert.Equal("http://localhost:8080", config.Current.BaseUrl);
            var restored = new LocalAiConfiguration(path, new LocalAiSettings()).Current;
            Assert.Equal("Other", restored.ProjectName);
            Assert.False(restored.Enabled);
            Assert.Equal(300, restored.RequestTimeoutSeconds);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("file:///tmp/test", 60)]
    [InlineData("http://localhost/?foo=bar", 60)]
    [InlineData("http://localhost", 0)]
    [InlineData("http://localhost", 301)]
    public void InvalidSettingsDoNotReplaceCurrentOrWriteFile(string url, int timeout)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var config = new LocalAiConfiguration(path, new LocalAiSettings());
        Assert.Throws<ArgumentException>(() => config.Save(new LocalAiSettings { BaseUrl = url, RequestTimeoutSeconds = timeout }));
        Assert.Equal("http://mmac.local:80", config.Current.BaseUrl);
        Assert.False(File.Exists(path));
    }
}
