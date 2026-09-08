using System.Text.Json.Serialization;

namespace AlternateEarth.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DamageType { Physical, Bleeding, Fire, Acid, Gas, Poison }
public sealed record DamageComponent(DamageType Type, double InitialMultiplier = 1, double PerSecondMultiplier = 0, int Seconds = 0);
public sealed record CombatEffect(string SourceId, DamageType Type, double DamagePerSecond, DateTimeOffset NextTickUtc, DateTimeOffset EndsAtUtc, string LocationId);
public sealed record GearStats(int Level, string Quality, IReadOnlyDictionary<DamageType, double> Resistances);
public sealed record ActorInspection(ActorState Actor, int Level, double FriendRating, string Allegiance, IReadOnlyList<CombatEffect> Effects, IReadOnlyDictionary<DamageType, double> Resistances, IReadOnlyList<DamageComponent> Attack);
public sealed record IncursionState(string Id, string Type, string Name, WorldPosition Center, double Radius, DateTimeOffset EndsAtUtc, int Kills, int Goal, string Objective, string BossId);
public sealed record CelebrityQuote(string Name, string Quote, string Source);
public sealed record PersistentCombatState(DateTimeOffset? AlcoholUntilUtc, int AlcoholNutUp, DateTimeOffset? FearedUntilUtc, string? FearSourceId, IReadOnlyList<CombatEffect>? Effects);

public static class CombatRules
{
    public static readonly string[] Qualities = ["Crude", "Poor", "Worn", "Common", "Fine", "Superior", "Masterwork", "Epic", "Legendary", "Godly"];
    public static IReadOnlyList<DamageComponent> Attack(string weapon)
    {
        var name = weapon.ToLowerInvariant();
        if (name.Contains("acid")) return [new(DamageType.Acid, 1, .15, 4)];
        if (name.Contains("gas") || name.Contains("stink")) return [new(DamageType.Gas, 1, .12, 3)];
        if (name.Contains("zombiebite")) return [new(DamageType.Physical), new(DamageType.Poison, 0, .15, 5)];
        if (name.Contains("poison")) return [new(DamageType.Poison, 1, .15, 5)];
        if (name.Contains("knife") || name.Contains("sword")) return [new(DamageType.Physical), new(DamageType.Bleeding, 0, .1, name.Contains("sword") ? 5 : 3)];
        if (name.Contains("flame") || name.Contains("fire") || name.Contains("napalm") || name.Contains("molotov") || name.Contains("lava")) return [new(DamageType.Fire)];
        if (name.Contains("rocket") || name.Contains("missile") || name.Contains("grenade") || name.Contains("explosion")) return [new(DamageType.Physical), new(DamageType.Fire, 0, .15, 4)];
        return [new(DamageType.Physical)];
    }
    public static int EquipLimit(int level, int nutUp) => Math.Max(1, level) + Math.Max(0, nutUp);
    public static int LootLevel(int playerLevel, bool exceptional, Random random) => random.Next(1, Math.Max(1, playerLevel) + (exceptional ? 20 : 0) + 1);
    public static GearStats RollGear(int level, string quality, Random random, bool clothing = true)
    {
        var tier = Math.Max(0, Array.FindIndex(Qualities, q => q.Equals(quality, StringComparison.OrdinalIgnoreCase)));
        var resistances = new Dictionary<DamageType, double>();
        if (clothing) foreach (var type in Enum.GetValues<DamageType>())
            resistances[type] = Math.Round(Math.Min(.6, (.005 + tier * .018 + random.NextDouble() * .015) * (1 + Math.Max(0, level - 1) * .025)), 4);
        return new(Math.Max(1, level), Qualities[tier], resistances);
    }
    public static IReadOnlyDictionary<DamageType, double> Combine(IEnumerable<GearStats> gear) => Enum.GetValues<DamageType>().ToDictionary(type => type,
        type => Math.Min(.9, 1 - gear.Aggregate(1d, (remaining, item) => remaining * (1 - Math.Clamp(item.Resistances.GetValueOrDefault(type), 0, .9)))));
    public static double Reduce(double damage, DamageType type, IReadOnlyDictionary<DamageType, double> resistance) => Math.Max(0, damage) * (1 - Math.Clamp(resistance.GetValueOrDefault(type), 0, .9));
    public static double FearSeconds(int nutUp, CharacterStats stats, int activeHarmfulEffects) => Math.Clamp(10 * (1 + activeHarmfulEffects * .05) / (1 + Math.Max(0, nutUp - 1) * .05 + Math.Max(0, stats.Endurance - 1) * .025), 1, 15);
}
