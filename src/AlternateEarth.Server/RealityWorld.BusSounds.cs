using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static void WarnBusPeople(SimulatedBus bus, double speed, PlayerState[] people, ActorState[] actors, List<WorldSoundEvent> sounds)
    {
        if (bus.HornCooldown > 0 || bus.State.HealthHearts <= 0 || speed <= .1) return;
        var danger = people.Any(p => p.HealthHearts > 0 && p.RidingBusId is null && p.Abduction is null && p.TravelMode != TravelMode.Ufo &&
                TransitGeometry.InBusWarningPath(bus.State, p.Position, speed)) ||
            actors.Any(a => a.HealthHearts > 0 && a.Abduction is null && a.Subtype != "ufo" &&
                TransitGeometry.InBusWarningPath(bus.State, a.Position, speed));
        if (!danger) return;
        var (sound, text) = (bus.HornCount++ % 3) switch
        {
            1 => ("beep", "BEEP!"),
            2 => ("ahooga", "AhOoooooGa!"),
            _ => ("honk", "HONK!")
        };
        bus.HornCooldown = 3;
        sounds.Add(new($"sound:{Guid.NewGuid():N}", sound, bus.State.Position, SpeakerId: bus.State.Id, Text: text));
    }
}
