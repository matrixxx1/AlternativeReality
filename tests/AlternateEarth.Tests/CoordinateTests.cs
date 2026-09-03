using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed class CoordinateTests
{
    [Fact]
    public void ProjectionRoundTripsToWgs84()
    {
        var source = new GeoCoordinate(45.6387, -122.6615, 31.4);
        var projection = new LocalTangentProjection(RegionId.FromGeo(source));

        var world = projection.Project(source);
        var roundTrip = projection.Unproject(world);

        Assert.Equal(source.Latitude, roundTrip.Latitude, 8);
        Assert.Equal(source.Longitude, roundTrip.Longitude, 8);
        Assert.Equal(source.ElevationMeters, roundTrip.ElevationMeters, 8);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(255.999, 0)]
    [InlineData(256, 1)]
    [InlineData(-0.001, -1)]
    [InlineData(-256, -1)]
    [InlineData(-256.001, -2)]
    public void ChunkCoordinatesUseMathematicalFloor(double worldX, int expectedChunkX)
    {
        var position = new WorldPosition(new RegionId(45, -123), worldX, 0);
        Assert.Equal(expectedChunkX, ChunkCoordinate.FromPosition(position).X);
    }
}
