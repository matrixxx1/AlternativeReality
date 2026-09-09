using System.Collections.Immutable;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    // Persistent turn queues let the bounded recovery history share curve geometry rather than copy it every step.
    private sealed class BusTurnQueue
    {
        public ImmutableQueue<(WorldPosition Position, double Heading)> Items = ImmutableQueue<(WorldPosition, double)>.Empty;
        public int Count;
        public void Enqueue((WorldPosition Position, double Heading) point) { Items = Items.Enqueue(point); Count++; }
        public void Dequeue() { Items = Items.Dequeue(); Count--; }
        public bool TryPeek(out (WorldPosition Position, double Heading) point)
        { point = Count > 0 ? Items.Peek() : default; return Count > 0; }
    }
    private sealed record BusBreadcrumb(WorldPosition Position, double Heading, int EdgeIndex, double Distance,
        ImmutableQueue<(WorldPosition Position, double Heading)> Turn, int TurnCount, double TurnExitDistance, bool TurningAround);

    private static void RememberBusStep(SimulatedBus bus)
    {
        if (bus.History.Count >= 160) bus.History.RemoveAt(0);
        bus.History.Add(new(bus.State.Position, bus.State.HeadingRadians, bus.EdgeIndex, bus.Distance,
            bus.Turn.Items, bus.Turn.Count, bus.TurnExitDistance, bus.TurningAround));
    }

    private bool ReverseBusToYield(SimulatedBus bus, double seconds, PlayerState[] people, ActorState[] actors)
    {
        var budget = Math.Min(bus.ReverseMeters, seconds * 1.2);
        while (budget > .001 && bus.History.Count > 0)
        {
            var previous = bus.History[^1];
            var distance = bus.State.Position.Distance2D(previous.Position);
            var amount = distance <= .001 ? 1 : Math.Min(1, budget / distance);
            var position = bus.State.Position with { X = bus.State.Position.X + (previous.Position.X - bus.State.Position.X) * amount,
                Y = bus.State.Position.Y + (previous.Position.Y - bus.State.Position.Y) * amount,
                Z = bus.State.Position.Z + (previous.Position.Z - bus.State.Position.Z) * amount };
            var angle = Math.Atan2(Math.Sin(previous.Heading - bus.State.HeadingRadians), Math.Cos(previous.Heading - bus.State.HeadingRadians));
            var heading = bus.State.HeadingRadians + angle * amount;
            var footprint = TransitGeometry.Footprint(position, heading, 9.4, 2.9);
            if (HaneyBlocks(footprint) || _transitObstacles!.Hit(footprint) is not null ||
                _buses.Values.Any(other => other != bus && other.State.Position.Distance2D(position) < 14 &&
                    TransitGeometry.Overlaps(footprint, TransitGeometry.Footprint(other.State.Position, other.State.HeadingRadians))) ||
                people.Any(p => p.RidingBusId != bus.State.Id && p.TravelMode != TravelMode.Ufo && TransitGeometry.Contains(footprint, p.Position)) ||
                actors.Any(a => TransitGeometry.Contains(footprint, a.Position)))
            { bus.State = bus.State with { Status = "yielding", SpeedMetersPerSecond = 0 }; return false; }
            var travelled = distance * amount;
            bus.State = bus.State with { Position = position, HeadingRadians = heading, SpeedMetersPerSecond = 1.2,
                Status = "reversing to yield", Version = bus.State.Version + 1 };
            budget -= Math.Max(.001, travelled); bus.ReverseMeters -= travelled;
            if (amount >= 1)
            {
                bus.History.RemoveAt(bus.History.Count - 1);bus.EdgeIndex = previous.EdgeIndex;bus.Distance = previous.Distance;
                bus.Turn.Items = previous.Turn;bus.Turn.Count = previous.TurnCount;
                bus.TurnExitDistance = previous.TurnExitDistance;bus.TurningAround = previous.TurningAround;
            }
        }
        if (bus.ReverseMeters <= .001 || bus.History.Count == 0)
        { bus.ReverseMeters = 0; bus.Dwell = 2; bus.LastObstacle = null; }
        return true;
    }

    private bool BusBlocksMovement(WorldPosition from, WorldPosition to)
    {
        foreach (var bus in _transitSnapshot.Buses)
        {
            if (bus.Position.Region != from.Region || bus.Position.Distance2D(from) > from.Distance2D(to) + 7) continue;
            if (TransitGeometry.SegmentHitsBus(from, to, bus, WorldNavigation.PlayerRadiusMeters)) return true;
        }
        return false;
    }
}
