namespace AlternateEarth.Shared;

public static class ProbulatorGeometry
{
    // About 15% beyond the UFO shadow. Match the client's ground projection.
    public const double DefaultRadiusMeters = 1.7;
    public const double GroundPitch = .69;
    public const double GroundShear = .14;
    public const double FootprintAspect = .38 / 1.35;

    public static bool Touches(WorldPosition start, WorldPosition end, WorldPosition target, double radius)
    {
        if (radius <= 0 || start.Region != target.Region || end.Region != target.Region) return false;
        var dx = end.X - start.X; var dy = end.Y - start.Y;
        var tx = target.X - start.X; var ty = target.Y - start.Y;
        var vx = (dx + dy * GroundShear) / radius;
        var vy = dy * GroundPitch / (radius * FootprintAspect);
        var px = (tx + ty * GroundShear) / radius;
        var py = ty * GroundPitch / (radius * FootprintAspect);
        var lengthSquared = vx * vx + vy * vy;
        var t = lengthSquared > 0 ? Math.Clamp((px * vx + py * vy) / lengthSquared, 0, 1) : 0;
        return Math.Pow(px - t * vx, 2) + Math.Pow(py - t * vy, 2) <= 1;
    }
}
