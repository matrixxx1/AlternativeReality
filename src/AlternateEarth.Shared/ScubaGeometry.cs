namespace AlternateEarth.Shared;

public static class ScubaGeometry
{
    public static double SlopeWidth(double width) => Math.Min(18, width / 4);

    public static double FloorHeight(double width, double height, UnderwaterState water, double x)
    {
        var distance = Math.Min(water.WestShore ? x - .5 : double.MaxValue,
            water.EastShore ? width - .5 - x : double.MaxValue);
        return .5 + (height - 1) * Math.Clamp(1 - distance / SlopeWidth(width), 0, 1);
    }

    public static WorldPosition ClampPosition(double width, double height, UnderwaterState water, WorldPosition point)
    {
        var x = Math.Clamp(point.X, water.WestShore ? 1 : .5, width - (water.EastShore ? 1 : .5));
        return point with { X = x, Y = Math.Clamp(point.Y, Math.Min(height - .5, FloorHeight(width, height, water, x) + .35), height - .5) };
    }
}
