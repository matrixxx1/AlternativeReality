using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public static void ValidateMapView(WorldBounds view)
    {
        if (!double.IsFinite(view.MinimumX) || !double.IsFinite(view.MinimumY) || !double.IsFinite(view.MaximumX) || !double.IsFinite(view.MaximumY)
            || view.MaximumX <= view.MinimumX || view.MaximumY <= view.MinimumY || view.MaximumX - view.MinimumX > 8000 || view.MaximumY - view.MinimumY > 8000)
            throw new InvalidOperationException("Invalid map view bounds.");
    }
    public WorldSnapshot CreateClientSnapshot(string playerId, WorldBounds? view = null)
    {
        var map = CreateMapWindow(playerId, view);
        return CreateSnapshot(map) with { Transit = CreateTransitView(playerId, view) };
    }

    public WorldMapWindow CreateMapWindow(string playerId, WorldBounds? view = null)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var origin = player.LocationId == "outdoor" ? player.Position :
            _returnPositions.GetValueOrDefault(playerId, new LocalTangentProjection(Configuration.Area.Region).Project(Configuration.Area.Center));
        // Keep a 100 m buffer beyond the mini-map's 500 m radius, including when
        // the camera is elsewhere. Camera buffers avoid requests on every frame.
        var coverage = new List<WorldBounds> { new(origin.X - 600, origin.Y - 600, origin.X + 600, origin.Y + 600) };
        if (view is not null)
        {
            ValidateMapView(view);
            coverage.Add(new(view.MinimumX - 192, view.MinimumY - 192, view.MaximumX + 192, view.MaximumY + 192));
        }
        var entities = _baseEntities.Values.Where(entity => coverage.Any(area => MapEntityOverlaps(entity, area))).OrderBy(entity => entity.Id).ToArray();
        // Retain the sparse elevation grid so interpolation stays continuous.
        return new(entities, _elevationSamples.Values.ToArray(), coverage);
    }

    private static bool MapEntityOverlaps(CanonicalEntity entity, WorldBounds area)
    {
        var minX = entity.Position.X; var maxX = minX;
        var minY = entity.Position.Y; var maxY = minY;
        foreach (var point in entity.Geometry)
        {
            minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
        }
        // Preserve crossing geometry, roof overhangs, and wide road strokes.
        return minX <= area.MaximumX + 50 && maxX >= area.MinimumX - 50 && minY <= area.MaximumY + 50 && maxY >= area.MinimumY - 50;
    }
}
