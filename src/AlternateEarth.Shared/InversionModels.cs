namespace AlternateEarth.Shared;

public sealed record InversionDefinition(string Id, string Name, string Boss, string Mode = "outdoor", int DurationMinutes = 10);
public static class InversionCatalog
{
    public static readonly IReadOnlyList<InversionDefinition> All = new InversionDefinition[]
    {
        new("ufo", "UFO Flyover", "The Mothership"), new("trex", "T-Rex Portal", "Rex"),
        new("brontosaurus", "Brontosaurus Portal", "Bronto"), new("stegosaurus", "Stegosaurus Portal", "Steggy"),
        new("raptors", "Raptor Pack", "Raptor Alpha"), new("giants", "Land of the Giants", "The Giant"), new("bear", "The Great Bear", "The Great Bear"),
        new("retro", "Jump Now, Regret Later!", "Lord of the Last Jump", "retro"),
        new("turns", "Wait Your Turn, You Goblin!", "The Queue Goblin", "turns"),
        new("cards", "Deal With It!", "The House Always Whines", "cards"),
        new("mech", "Mechanized Warrior", "Mechanized Warrior"),
        new("plants", "Zombie vs plants", "The Lawnfather"),
        new("fruit", "Musical fruit", "The Bean Counter"),
        new("lava", "The Floor Is Literally Lava", "The Hot Property Manager"),
        new("office", "Mandatory Team-Building Exercise", "The Middle Manager"),
        new("geese", "Attack of the Mildly Inconvenient Geese", "Honk Wick"),
        new("normal", "A Very Normal Tuesday", "The Ottoman Empire"),
        new("sender", "Return to Sender", "Postmaster Disaster"),
        new("bosses", "Oops! All Bosses!", "Tiny Rex, Huge Ego"),
        new("hoa", "The HOA Has Arrived", "Karen, President for Life"),
        new("barrel", "Emotional Support Barrel", "Codependent Cooper"),
        new("flood", "The Great Floods", "Noah More Mr. Nice Guy"),
        new("budget", "Budget Cuts", "The Executive Producer"),
        new("hold", "Please Hold", "Your Estimated Wait Time"),
        new("northern", "Northern exposure", "Mecha Terry and Mecha Phil", DurationMinutes: 20),
        new("smug", "Smug alert", "Bald Alex Wins")
    };
}
public sealed record VoteOption(string Id, string Name, IReadOnlyList<string> Voters);
public sealed record ServerVoteState(DateTimeOffset EndsAtUtc, int Round, IReadOnlyList<VoteOption> Options);
public sealed record EventPatch(string Id, WorldPosition Position, double Radius, string Kind, DateTimeOffset ChangesAtUtc, DateTimeOffset EndsAtUtc);
public sealed record EventMissile(string Id, string OwnerId, WorldPosition Position, WorldPosition Target, string Kind, DateTimeOffset ArrivesAtUtc);
public sealed record InversionState(string Id, string Type, string Name, WorldPosition Center, double Radius,
    DateTimeOffset EndsAtUtc, string BossId, int Kills, string Message, IReadOnlyList<EventPatch> Patches, IReadOnlyList<EventMissile> Missiles);
public sealed record InversionView(ServerVoteState? Vote, InversionState? Active, IReadOnlyList<string> Queued, int CompletedDungeons = 0);
public sealed record EventBattleState(string EventId, string Mode, int DungeonNumber, string EnemyName, double EnemyHealth,
    double MaximumEnemyHealth, int Turn, int Guard, IReadOnlyList<string> Hand, string Message, bool Won = false, int Poison = 0, int Focus = 0);
public sealed record DungeonBarrier(string Id, double X, double Y, double Width, double Height, string Kind, bool Destroyed = false);
public sealed record DungeonWater(double X, double Y, double Width, double Height, bool Deep);
