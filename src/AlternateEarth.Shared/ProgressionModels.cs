namespace AlternateEarth.Shared;

public sealed record CharacterStats(int Strength = 1, int Perception = 1, int Endurance = 1,
    int Charisma = 1, int Intelligence = 1, int Agility = 1, int Luck = 1)
{
    public int Total => Strength + Perception + Endurance + Charisma + Intelligence + Agility + Luck;
    public bool IsValid => new[] { Strength, Perception, Endurance, Charisma, Intelligence, Agility, Luck }.All(value => value is >= 0 and <= 10_000);
}

public sealed record ProgressionProfile(double Experience, CharacterStats Stats, IReadOnlyList<string> Rewards,
    double OnlineSeconds = 0, double EventSeconds = 0, double Alignment = 0);
public sealed record ProgressionState(int Level, double Experience, double EarnedTowardNextLevel, long RequiredForNextLevel,
    int AvailablePoints, CharacterStats Stats, double DamageMultiplier, double CarryingCapacity, double AccuracyMultiplier,
    double VisionMultiplier, double MaximumStamina, double FirstEncounterBonus, double ExperienceMultiplier,
    double StaminaDrainMultiplier, double NpcSightMultiplier, double WitnessChance, double LockpickChance,
    double Alignment = 0, double AlignmentFirstEncounterBonus = 0);
public sealed record AssignStatsRequest(CharacterStats Stats);
public sealed record ProgressionNotice(string PlayerId, string Message, double Experience = 0, bool LevelGained = false);

public static class ProgressionRules
{
    public static long ExperienceForLevel(int level) => 100L + 75L * (level - 1) + 25L * (level - 1) * (level - 1);
    public static (int Level, double Progress, long Required) LevelAt(double experience)
    {
        var level = 1;
        while (experience >= ExperienceForLevel(level)) experience -= ExperienceForLevel(level++);
        return (level, experience, ExperienceForLevel(level));
    }
    public static double Damage(CharacterStats stats) => 1 + .1 * (stats.Strength - 1);
    public static double Capacity(CharacterStats stats) => 50 + 5 * (stats.Strength - 1);
    public static double Accuracy(CharacterStats stats) => 1 + .04 * (stats.Perception - 1);
    public static double Vision(CharacterStats stats) => 1 + .05 * (stats.Perception - 1);
    public static double Stamina(CharacterStats stats) => 10 + 2 * (stats.Endurance - 1);
    public static double Charisma(CharacterStats stats) => .02 * (stats.Charisma - 1);
    public static double Experience(CharacterStats stats) => 1 + .08 * (stats.Intelligence - 1);
    public static double Drain(CharacterStats stats) => Math.Max(.2, 1 / (1 + .06 * (stats.Agility - 1)));
    public static double NpcSight(CharacterStats stats) => Math.Max(.25, 1 / (1 + .06 * (stats.Agility - 1)));
    public static double Witness(CharacterStats stats) => Math.Clamp(.9 - .035 * (stats.Luck - 1), .1, .95);
    public static double Lockpick(CharacterStats stats) => Math.Clamp(.15 + .035 * (stats.Perception - 1) + .025 * (stats.Luck - 1), .03, .95);
    public static double CraftSuccess(CharacterStats stats, double recipeChance) =>
        Math.Clamp(recipeChance + .025 * (stats.Intelligence - 1) + .02 * (stats.Luck - 1), .01, .98);
}
