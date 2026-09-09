using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public const string RetroBattlesName = "Jump Now, Regret Later!";
    private readonly object _retroLock = new();
    private DateTimeOffset? _retroEndsAt;
    private long? _lastRetroCycle;
    public DateTimeOffset? RetroBattlesEndsAtUtc => _retroEndsAt;
    public bool RetroBattlesActive => _retroEndsAt > _probulatorClock.GetUtcNow();

    private void StartRetroBattles() => _retroEndsAt = _probulatorClock.GetUtcNow().AddMinutes(_eventConfiguration.RetroBattlesDurationMinutes);

    internal static RetroBattleState CreateRetroBattle(int difficulty, DateTimeOffset now)
    {
        difficulty = Math.Clamp(difficulty, 1, 100);
        var count = 4 + (int)Math.Ceiling(difficulty / 4d);
        var speed = 3.5 + difficulty * .065;
        // Every enemy has a full jump's worth of space, including across the loop boundary.
        var spacing = speed * 1.8;
        return new(speed, (count + 2) * spacing, 0, 0, 0,
            Enumerable.Range(0, count).Select(i => new RetroEnemyState(i, 12 + i * spacing)).ToArray(), now);
    }

    private DungeonState CreateRetroDungeon(string playerId, DungeonState original)
    {
        var id = $"retro:{playerId}:{Guid.NewGuid():N}";
        var exit = original.Exit with { X = 6, Y = 2, Z = 0 };
        var dungeon = new DungeonState(id, original.BuildingId, 32, 12,
            [new DungeonRoom(0, 0, 32, 12)], [], exit, [], [], [],
            SessionId: id, Difficulty: original.Difficulty,
            RetroBattle: CreateRetroBattle(original.Difficulty, _probulatorClock.GetUtcNow()));
        _dungeons[id] = dungeon;
        return dungeon;
    }

    private DungeonState CreateRetroDungeon(string playerId, CanonicalEntity building) => CreateRetroDungeon(playerId,
        new DungeonState("", building.Id, 32, 12, [], [], building.Position, [], [], [], Difficulty: DungeonDifficulty(building)));

    public bool IsInRetroBattle(string playerId) => _players.TryGetValue(playerId, out var player)
        && _dungeons.TryGetValue(player.LocationId, out var dungeon) && dungeon.RetroBattle is not null;

    public (PlayerState? Player, DungeonState? Dungeon) GetRetroBattleUpdate(string playerId)
    {
        var player = _players.GetValueOrDefault(playerId);
        return (player, player is null ? null : _dungeons.GetValueOrDefault(player.LocationId));
    }

    public void JumpRetroBattle(string playerId)
    {
        lock (_retroLock)
        {
            if (!RetroBattlesActive || !_players.TryGetValue(playerId, out var player)
                || !_dungeons.TryGetValue(player.LocationId, out var dungeon) || dungeon.RetroBattle is not { } run)
                throw new InvalidOperationException("You are not in an active Retro battle.");
            if (dungeon.IsCompleted || run.JumpHeight > 0 || run.JumpVelocity > 0) return;
            _dungeons[dungeon.Id] = dungeon with { RetroBattle = run with { JumpVelocity = 9, UpdatedAtUtc = _probulatorClock.GetUtcNow() } };
        }
    }

    internal static RetroBattleState StepRetroBattle(RetroBattleState run, double seconds, DateTimeOffset now)
    {
        // Small fixed substeps prevent tunneling at the highest difficulty.
        var remaining = Math.Clamp(seconds, 0, .25);
        while (remaining > .000001)
        {
            var dt = Math.Min(remaining, 1d / 120); remaining -= dt;
            var velocity = run.JumpVelocity - 20 * dt;
            var height = Math.Max(0, run.JumpHeight + velocity * dt);
            var scroll = (run.Scroll + run.ScrollSpeed * dt) % run.TrackLength;
            var enemies = run.Enemies;
            if (velocity < 0 && run.JumpHeight >= .85 && height <= .85)
            {
                enemies = enemies.Select(enemy => !enemy.Defeated && Math.Abs(RetroEnemyX(enemy.X, scroll, run.TrackLength) - 6) <= .9
                    ? enemy with { Defeated = enemy.Hearts <= 1, Hearts = Math.Max(0, enemy.Hearts - 1) } : enemy).ToArray();
            }
            run = run with { Scroll = scroll, JumpHeight = height, JumpVelocity = height == 0 ? 0 : velocity, Enemies = enemies };
        }
        return run with { UpdatedAtUtc = now };
    }

    internal static double RetroEnemyX(double x, double scroll, double length) => ((x - scroll + 3) % length + length) % length - 3;

    public async Task<IReadOnlyList<string>> AdvanceRetroBattlesAsync(TimeSpan elapsed, CancellationToken token = default)
    {
        var now = _probulatorClock.GetUtcNow();
        var cycle = ScheduledEventCycle(now, "retro-battles", _eventConfiguration.RetroBattlesIntervalHours);
        // Hourly Server Vote owns event scheduling.
        _lastRetroCycle = cycle;
        var changed = new List<string>();
        foreach (var player in _players.Values.ToArray())
        {
            if (!_dungeons.TryGetValue(player.LocationId, out var dungeon) || dungeon.IsHome || dungeon.IsStore) continue;
            if (dungeon.RetroBattle is null) continue;
            if (!RetroBattlesActive && !dungeon.IsCompleted)
            {
                await ExitDungeonAsync(player.Id, token);
                changed.Add(player.Id);
                continue;
            }
            if (dungeon.IsCompleted) continue;
            lock (_retroLock)
            {
                if (!_dungeons.TryGetValue(player.LocationId, out dungeon) || dungeon.RetroBattle is not { } run) continue;
                run = StepRetroBattle(run, elapsed.TotalSeconds, now);
                var complete = run.Enemies.All(enemy => enemy.Defeated);
                if (complete && dungeon.EventBattle is null)
                {
                    var chestId = $"{dungeon.Id}:chest:0";
                    run = run with { RewardChestId = chestId, JumpHeight = 0, JumpVelocity = 0 };
                    dungeon = dungeon with { Chests = [new TreasureChestState(chestId, dungeon.Exit, dungeon.Id, IsGrand: true)] };
                }
                _dungeons[dungeon.Id] = dungeon with { RetroBattle = run, IsCompleted = complete };
            }
            if (_dungeons.TryGetValue(player.LocationId, out var completed) && completed.IsCompleted && completed.EventBattle is not null)
                await CompleteEventDungeonAsync(_players.GetValueOrDefault(player.Id, player), completed, token);
            changed.Add(player.Id);
        }
        return changed;
    }
}

public sealed class RetroBattleSimulationService(RealityWorld world, RealitySocketHub hub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        var previous = System.Diagnostics.Stopwatch.GetTimestamp();
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(previous, now); previous = now;
            var players = await world.AdvanceRetroBattlesAsync(elapsed, stoppingToken);
            await hub.SendRetroBattlesAsync(players, stoppingToken);
        }
    }
}
