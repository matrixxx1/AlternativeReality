using AlternateEarth.Shared;
using AlternateEarth.Geo;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private void PopulateWaterLife(GeographicDataset generated)
    {
        foreach (var water in generated.Features.Where(e => e.Kind == EntityKind.Water && e.Geometry.Count >= 4))
        {
            var points = water.Geometry;
            if (Math.Abs(points[0].X - points[^1].X) + Math.Abs(points[0].Y - points[^1].Y) > .1) continue;
            var area = Math.Abs(points.Zip(points.Skip(1), (a, b) => a.X * b.Y - b.X * a.Y).Sum()) / 2;
            if (area < 900) continue;
            var random = new Random(StableInt("water-life:" + water.Id));
            var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
            for (var index = 0; index < Math.Clamp((int)(area / 900), 4, 12); index++)
            {
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    var x = minX + random.NextDouble() * (maxX - minX); var y = minY + random.NextDouble() * (maxY - minY);
                    if (Navigation.TerrainAt(x, y) != TerrainType.DeepWater) continue;
                    var monster = index == 0;
                    var id = $"water-life:{water.Id}:{index}";
                    _actors.TryAdd(id, new ActorState(id, EntityKind.Animal, monster ? "waterMonster" : "fish",
                        monster ? "Lake monster" : "Fish", water.Position with { X = x, Y = y },
                        HealthHearts: monster ? 20 : 2, MaximumHealthHearts: monster ? 20 : 2));
                    break;
                }
            }
        }
        var anchor = generated.Features.Where(e => e.Kind is EntityKind.Door or EntityKind.Road).OrderBy(e => e.Id).FirstOrDefault();
        var bounds = generated.Area.Bounds;
        var flagAnchor = anchor?.Position ?? new WorldPosition(generated.Area.Region, (bounds.MinimumX + bounds.MaximumX) / 2, (bounds.MinimumY + bounds.MaximumY) / 2);
        var point = Navigation.FindNearestWalkable(flagAnchor with { X = flagAnchor.X + 3, Y = flagAnchor.Y + 2 });
        var flagId = $"wind-flag:{generated.Area.Center.Latitude:F5}:{generated.Area.Center.Longitude:F5}";
        _baseEntities.TryAdd(flagId, new CanonicalEntity(flagId, EntityKind.ResourceNode, point, Array.Empty<GeometryPoint>(),
            new Dictionary<string, string> { ["subtype"] = "windFlag" }));
    }

    private ActorState AdvanceWaterActor(ActorState actor, TimeSpan elapsed)
    {
        var angle = _probulatorClock.GetUtcNow().ToUnixTimeMilliseconds() / 8000d + (StableInt(actor.Id) & int.MaxValue) % 360;
        var speed = actor.Subtype == "waterMonster" ? .8 : .5;
        var candidate = actor.Position with { X = actor.Position.X + Math.Cos(angle) * speed * elapsed.TotalSeconds,
            Y = actor.Position.Y + Math.Sin(angle) * speed * elapsed.TotalSeconds };
        var moved = Navigation.CanTraverse(actor.Position, candidate, t => t is TerrainType.DeepWater or TerrainType.ShallowWater);
        return actor with { Position = moved ? candidate : actor.Position, IsMoving = moved,
            Facing = Math.Cos(angle) < 0 ? "west" : "east", Version = actor.Version + 1 };
    }
}
