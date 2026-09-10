using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly ConcurrentDictionary<string, ProgressionProfile> _progression = new();
    private readonly SemaphoreSlim _progressionLock = new(1, 1);
    private readonly SemaphoreSlim _dungeonCompletionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _progressionLastTick = new();
    private readonly ConcurrentDictionary<string, byte> _creditedKills = new();
    private readonly ConcurrentQueue<ProgressionNotice> _progressionNotices = new();
    internal Func<double> ProgressionRoll { get; set; } = Random.Shared.NextDouble;
    private static ProgressionProfile NewProgression => new(0, new CharacterStats(), Array.Empty<string>());
    private CharacterStats StatsFor(string playerId) { var stats = FoodStats(playerId, _progression.GetValueOrDefault(playerId)?.Stats ?? new CharacterStats()); return MapleBoostActive(playerId) ? stats with { Perception = stats.Perception + 5 } : stats; }
    private readonly ConcurrentDictionary<(string Player, string Attacker), DateTimeOffset> _fearChecks = new();

    internal CombatEvent ResolveCombatFear(CombatEvent combat)
    {
        if (combat.Weapon is "canadianGas" or "areaHazard" or "molotovFire" ||
            combat.TargetDied || !combat.Hit || combat.Damage <= 0 || combat.AttackerId == combat.TargetId ||
            !_players.TryGetValue(combat.TargetId, out var player) || player.GodMode || player.HealthHearts <= 0)
            return combat;
        var actor = FindActor(player.Id, combat.AttackerId);
        var opponent = _players.GetValueOrDefault(combat.AttackerId);
        // Maximum hearts provide a stable strength comparison unaffected by damage from this hit.
        var strength = actor?.MaximumHealthHearts ?? opponent?.MaximumHealthHearts ?? 0;
        if (strength <= player.MaximumHealthHearts) return combat;
        var now = _probulatorClock.GetUtcNow();
        var key = (player.Id, combat.AttackerId);
        if (_fearChecks.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(5)) return combat;
        _fearChecks[key] = now;
        var chance = Math.Clamp(1 - player.MaximumHealthHearts / strength, 0, .8) *
            (1 - ProgressionRules.FearResistance(StatsFor(player.Id)));
        return combat with { FleeInFear = ProgressionRoll() < chance };
    }

    public ProgressionState GetProgression(string playerId)
    {
        var profile = _progression.GetValueOrDefault(playerId) ?? NewProgression;
        var (level, earned, required) = ProgressionRules.LevelAt(profile.Experience);
        var stats = StatsFor(playerId);
        return new(level, profile.Experience, earned, required, ProgressionRules.StartingPoints + level - 1 - profile.Stats.Total, profile.Stats,
            ProgressionRules.Damage(stats), ProgressionRules.Capacity(stats), ProgressionRules.Accuracy(stats),
            ProgressionRules.Vision(StatsFor(playerId)), ProgressionRules.Stamina(stats), ProgressionRules.Charisma(stats),
            ProgressionRules.Experience(stats), ProgressionRules.Drain(stats), ProgressionRules.NpcSight(stats),
            ProgressionRules.Witness(stats), ProgressionRules.Lockpick(stats), profile.Alignment, profile.Alignment * .003,
            ProgressionRules.FearResistance(stats), ProgressionRules.ExtraAttackChance(stats), ProgressionRules.ShootingInterval(stats));
    }

    internal async Task AdjustAlignmentAsync(string playerId, double amount, CancellationToken token = default)
    {
        if (!_progression.ContainsKey(playerId)) return;
        await _progressionLock.WaitAsync(token);
        try
        {
            var current = _progression[playerId];
            var next = current with { Alignment = Math.Clamp(Math.Round(current.Alignment + amount, 2), -100, 100) };
            await _store.SaveProgressionAsync(Configuration.Id, playerId, next, token);
            _progression[playerId] = next;
            _progressionNotices.Enqueue(new(playerId, amount > 0 ? $"Reputation: +{amount:0.##} Good" : $"Reputation: +{-amount:0.##} Evil"));
        }
        finally { _progressionLock.Release(); }
    }

    internal async Task<double> AwardExperienceAsync(string playerId, double baseExperience, string reason,
        string? once = null, bool randomize = true, CancellationToken cancellationToken = default)
    {
        if (!_progression.ContainsKey(playerId) || _players.GetValueOrDefault(playerId)?.IsTestCharacter == true) return 0;
        await _progressionLock.WaitAsync(cancellationToken);
        try
        {
            var current = _progression[playerId];
            if (once is not null && current.Rewards.Contains(once, StringComparer.Ordinal)) return 0;
            var awarded = Math.Round(Math.Max(0, baseExperience) * ProgressionRules.Experience(current.Stats) *
                (randomize ? .8 + ProgressionRoll() * .4 : 1) *
                (FailedCraftExperienceBoostActive(_players.GetValueOrDefault(playerId), _probulatorClock.GetUtcNow()) ? 1.5 : 1), 2);
            if (awarded <= 0) return 0;
            var next = current with { Experience = current.Experience + awarded,
                Rewards = once is null ? current.Rewards : current.Rewards.Append(once).ToArray() };
            await _store.SaveProgressionAsync(Configuration.Id, playerId, next, cancellationToken);
            _progression[playerId] = next;
            var previousLevel = ProgressionRules.LevelAt(current.Experience).Level;
            var level = ProgressionRules.LevelAt(next.Experience).Level;
            var message = $"+{awarded:0.##} XP · {reason}";
            if (level > previousLevel) message += $" · Level {level}! {level - previousLevel} new stat point(s).";
            if (reason != "Passive presence") _progressionNotices.Enqueue(new(playerId, message, awarded, level > previousLevel));
            return awarded;
        }
        finally { _progressionLock.Release(); }
    }

    public async Task<PlayerState> AssignStatsAsync(string playerId, AssignStatsRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.ContainsKey(playerId) || !_progression.ContainsKey(playerId)) throw new InvalidOperationException("Unknown player.");
        if (request.Stats is null || !request.Stats.IsValid) throw new InvalidOperationException("Stat points must be whole numbers from 0 to 10,000.");
        await _progressionLock.WaitAsync(cancellationToken);
        try
        {
            var current = _progression[playerId];
            var budget = ProgressionRules.StartingPoints + ProgressionRules.LevelAt(current.Experience).Level - 1;
            if (request.Stats.Total > budget) throw new InvalidOperationException($"You have {budget} total stat points. Remove a point from another stat or gain a level.");
            var next = current with { Stats = request.Stats };
            await _store.SaveProgressionAsync(Configuration.Id, playerId, next, cancellationToken);
            _progression[playerId] = next;
        }
        finally { _progressionLock.Release(); }
        // Preserve the stamina fraction so reallocating Endurance cannot refill it.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var player = _players[playerId];
            var maximum = ProgressionRules.Stamina(request.Stats);
            var updated = player with { MaximumStamina = maximum, Stamina = maximum * player.Stamina / Math.Max(1, player.MaximumStamina), Version = player.Version + 1 };
            if (await SavePlayerAsync(updated, cancellationToken)) return updated;
        }
        return _players[playerId];
    }

    public async Task AdvanceProgressionAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        foreach (var player in _players.Values.Where(player => !player.IsTestCharacter))
        {
            if (!_progressionLastTick.TryGetValue(player.Id, out var previous)) { _progressionLastTick[player.Id] = now; continue; }
            _progressionLastTick[player.Id] = now;
            var seconds = Math.Max(0, (now - previous).TotalSeconds);
            if (!_progression.TryGetValue(player.Id, out var profile)) continue;
            var inEvent = player.LocationId == "outdoor" && _actors.Values.Any(actor => actor.EventStartedAtUtc <= now && actor.EventEndsAtUtc > now &&
                actor.HealthHearts > 0 && actor.Position.Distance2D(player.Position) <= 100);
            int minutes, eventMinutes; double movementCredit;
            await _progressionLock.WaitAsync(cancellationToken);
            try
            {
                profile = _progression[player.Id];
                var onlineSeconds = profile.OnlineSeconds + seconds;
                var eventSeconds = profile.EventSeconds + (inEvent ? seconds : 0);
                var movingSeconds=profile.MovingSeconds+(_lastMovement.TryGetValue(player.Id,out var moved)&&now-moved<TimeSpan.FromSeconds(2)?seconds:0);
                minutes = (int)(onlineSeconds / 60); eventMinutes = (int)(eventSeconds / 60);
                movementCredit=minutes>0?Math.Min(minutes*60,movingSeconds):0;
                _progression[player.Id] = profile with { OnlineSeconds = onlineSeconds % 60, EventSeconds = eventSeconds % 60, MovingSeconds = movingSeconds-movementCredit };
            }
            finally { _progressionLock.Release(); }
            if (minutes > 0) await AwardExperienceAsync(player.Id, minutes*.05+movementCredit*.05/60, "Passive presence", randomize: false, cancellationToken: cancellationToken);
            if (eventMinutes > 0) await AwardExperienceAsync(player.Id, 3 * eventMinutes, "Time in a server event area", cancellationToken: cancellationToken);
            if (player.LocationId == "outdoor")
                await AwardExperienceAsync(player.Id, 75, "New map area explored", $"explore:{AreaKeyFor(player.Position.X, player.Position.Y)}", cancellationToken: cancellationToken);
        }
    }

    public IReadOnlyList<ProgressionNotice> TakeProgressionNotices()
    {
        var notices = new List<ProgressionNotice>();
        while (_progressionNotices.TryDequeue(out var notice)) notices.Add(notice);
        return notices;
    }

    private async Task RewardActorKillAsync(PlayerState player, ActorState actor, CancellationToken cancellationToken)
    {
        if (actor.IsTestCharacter || player.IsTestCharacter || !_creditedKills.TryAdd(actor.Id, 0)) return;
        await AwardExperienceAsync(player.Id, (12 + Math.Min(50, actor.MaximumHealthHearts * 2)) * (actor.EventStartedAtUtc is null ? 1 : 2),
            $"Defeated {actor.Name}", cancellationToken: cancellationToken);
        await CheckDungeonCompletionAsync(player, actor.Position, cancellationToken);
    }

    private async Task RewardPlayerKillAsync(string killerId, PlayerState victim, CancellationToken token)
    {
        if (killerId == victim.Id || victim.IsTestCharacter) return;
        await AwardExperienceAsync(killerId, 40, $"Defeated {victim.Name}", cancellationToken: token);
    }

    private async Task CheckDungeonCompletionAsync(PlayerState killer, WorldPosition rewardPosition, CancellationToken cancellationToken)
    {
        await _dungeonCompletionLock.WaitAsync(cancellationToken);
        try
        {
            if (!_dungeons.TryGetValue(killer.LocationId, out var floor) || floor.IsHome || floor.IsStore || floor.Underwater is not null || floor.IsCompleted || floor.Actors.Any(actor => actor.HealthHearts > 0)) return;
            var session = floor.SessionId ?? floor.Id;
            var floors = _dungeons.Values.Where(item => (item.SessionId ?? item.Id) == session).ToArray();
            if (floors.Length < floor.LevelCount || floors.Any(item => item.Actors.Any(actor => actor.HealthHearts > 0))) return;
            var chest = new TreasureChestState($"{floor.Id}:completion:{Guid.NewGuid():N}", rewardPosition, floor.Id, IsGrand: true);
            var rewards = new List<ItemStack> { InventoryStack("rock", 12), InventoryStack("ballBearing", 40), InventoryStack("bullet", 20),
                InventoryStack("sword", 1, quality: "Fine"), InventoryStack("craftingSkillBook", 1),
                InventoryStack(CraftingCatalog.RecipeItemType(CraftingCatalog.Recipes[Random.Shared.Next(CraftingCatalog.Recipes.Length)].Id), 1) };
            _chestContents[chest.Id] = new(chest.Id, (long)((2_500 + floor.Difficulty * 100) * (.8 + ProgressionRoll() * .4)), rewards);
            foreach (var item in floors) _dungeons[item.Id] = item with { IsCompleted = true,
                Chests = item.Id == floor.Id ? item.Chests.Append(chest).ToArray() : item.Chests };
            foreach (var player in _players.Values.Where(player => !player.IsTestCharacter && floors.Any(item => item.Id == player.LocationId)))
            {
                await AwardExperienceAsync(player.Id, 150 + floor.Difficulty * 5 + floor.LevelCount * 30, "Dungeon complete! Grand treasure chest unlocked", cancellationToken: cancellationToken);
            }
        }
        finally { _dungeonCompletionLock.Release(); }
    }
}
