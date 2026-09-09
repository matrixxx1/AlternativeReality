using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World, string PlayerId, CanonicalEntity Door, ProbulatorTestClock Clock)> RetroFixture()
    {
        var config = new RealityConfiguration("retro-tests", "Retro", 23, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var clock = new ProbulatorTestClock();
        CanonicalEntity House(string id, double x) => new(id, EntityKind.Building, new WorldPosition(config.Area.Region, x, 20),
            [new(x - 5, 15), new(x + 5, 15), new(x + 5, 25), new(x - 5, 25), new(x - 5, 15)],
            new Dictionary<string, string> { ["building"] = "yes", ["questItem"] = "true" });
        var store = new SqliteRealityStore(Path.Combine(_directory, "retro.db"));
        await store.InitializeAsync(config);
        await store.CreateAccountAsync(new AccountRecord("retro-account", "Retro", "hash", "salt", "token", "retro-player"), "Retro");
        var world = new RealityWorld(config, new DeterministicWorldGenerator(new FixedGeographicProvider(House("retro-a", 20), House("retro-b", 60))), new FixedWeatherProvider(), store, clock);
        await world.InitializeAsync();
        var player = await world.JoinAsync("retro-player", "Retro", "retro-account");
        var home = world.GetPrivateState(player.Id).Base!;
        var door = world.CreateSnapshot().BaseEntities.Single(e => e.Kind == EntityKind.Door && e.Properties["buildingId"] != home.BuildingId);
        await world.SetGodModeAsync(player.Id, true);
        await world.TeleportAsync(player.Id, new(door.Position.X, door.Position.Y, true));
        return (world, player.Id, door, clock);
    }

    [Fact]
    public void RetroDifficultyScalesAndOnlyDescendingHeadContactKills()
    {
        var now = DateTimeOffset.UtcNow;
        var easy = RealityWorld.CreateRetroBattle(1, now);
        var hard = RealityWorld.CreateRetroBattle(100, now);
        Assert.True(hard.ScrollSpeed > easy.ScrollSpeed);
        Assert.True(hard.Enemies.Count > easy.Enemies.Count);
        var contact = easy with { Enemies = [new(1, 6)], JumpHeight = .9, JumpVelocity = -4 };
        Assert.True(RealityWorld.StepRetroBattle(contact, .025, now).Enemies[0].Defeated);
        Assert.False(RealityWorld.StepRetroBattle(contact with { JumpHeight = 0, JumpVelocity = 0 }, .025, now).Enemies[0].Defeated);
        Assert.False(RealityWorld.StepRetroBattle(contact with { JumpHeight = .8, JumpVelocity = 4 }, .025, now).Enemies[0].Defeated);
        Assert.False(RealityWorld.StepRetroBattle(contact with { Enemies = [new(1, 9)] }, .025, now).Enemies[0].Defeated);
        var loop = easy;
        for (var i = 0; i < 1200; i++) loop = RealityWorld.StepRetroBattle(loop, .05, now);
        Assert.All(loop.Enemies, enemy => Assert.False(enemy.Defeated));
        Assert.InRange(loop.Scroll, 0, loop.TrackLength);
    }

    private static async Task ClearRetro(RealityWorld world, string id, ProbulatorTestClock clock)
    {
        for (var step=0;step<9000&&!world.GetRetroBattleUpdate(id).Dungeon!.IsCompleted;step++)
        {
            var run=world.GetRetroBattleUpdate(id).Dungeon!.RetroBattle!;
            var next=run.Enemies.Where(e=>!e.Defeated).Select(e=>RealityWorld.RetroEnemyX(e.X,run.Scroll,run.TrackLength)).Where(x=>x>6).DefaultIfEmpty(double.MaxValue).Min();
            if(run.JumpHeight==0&&next<=6+run.ScrollSpeed*.8)world.JumpRetroBattle(id);
            clock.Advance(.05);await world.AdvanceRetroBattlesAsync(TimeSpan.FromSeconds(.05));
        }
        Assert.True(world.GetRetroBattleUpdate(id).Dungeon!.IsCompleted);
    }
    [Fact]
    public async Task RetroNeedsTwoPersonalDungeonsAndRewardsRemainPrivateAfterExit()
    {
        var (world,id,door,clock)=await RetroFixture();
        var p=world.GetRetroBattleUpdate(id).Player!;world.StartInversion("retro",p,clock.GetUtcNow());
        var first=await world.EnterEventDungeonAsync(id);
        Assert.Equal(2,first.RetroBattle!.Enemies.Single(e=>e.IsBoss).Hearts);
        Assert.Empty(first.Chests);
        Assert.False((await world.MoveAsync(id,new(1,0,1)))!.Moved);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.AttackAsync(id,new("enemy","fist")));
        await ClearRetro(world,id,clock);
        Assert.Equal(1,world.GetInversionView(id).CompletedDungeons);
        Assert.DoesNotContain(world.GetPrivateState(id).Loot!,d=>d.DropKind=="eventReward");
        var second=await world.EnterEventDungeonAsync(id);
        Assert.Equal(5,second.RetroBattle!.Enemies.Single(e=>e.IsBoss).Hearts);
        await ClearRetro(world,id,clock);
        Assert.Equal(2,world.GetInversionView(id).CompletedDungeons);
        var reward=Assert.Single(world.GetPrivateState(id).Loot!,d=>d.DropKind=="eventReward");
        await world.JoinAsync("other","Other");
        Assert.DoesNotContain(world.GetPrivateState("other").Loot!,d=>d.Id==reward.Id);
        Assert.Throws<InvalidOperationException>(()=>world.OpenLoot("other",reward.Id));
        await world.ExitDungeonAsync(id);Assert.Equal(reward.Id,world.OpenLoot(id,reward.Id).Id);
        await world.AdvanceRetroBattlesAsync(TimeSpan.FromSeconds(.05));
        Assert.Single(world.GetPrivateState(id).Loot!,d=>d.DropKind=="eventReward");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.EnterEventDungeonAsync(id));
    }
    [Fact]
    public async Task EventDoesNotConvertUnrelatedDungeonAndExpiryEndsUnfinishedRunWithoutPrize()
    {
        var (world,id,door,clock)=await RetroFixture();
        await world.EnterDungeonAsync(id,door.Id);
        world.StartInversion("retro",world.GetRetroBattleUpdate(id).Player!,clock.GetUtcNow());
        await world.AdvanceRetroBattlesAsync(TimeSpan.FromSeconds(.05));Assert.Null(world.GetPrivateState(id).Dungeon!.RetroBattle);
        await world.ExitDungeonAsync(id);await world.EnterEventDungeonAsync(id);
        clock.Advance(601);await world.AdvanceInversionsAsync(TimeSpan.FromSeconds(.5));await world.AdvanceRetroBattlesAsync(TimeSpan.FromSeconds(.05));
        Assert.Null(world.GetInversionView(id).Active);Assert.Equal("outdoor",world.GetRetroBattleUpdate(id).Player!.LocationId);
        Assert.DoesNotContain(world.GetPrivateState(id).Loot!,d=>d.DropKind=="eventReward");
    }
    [Fact]
    public async Task RetroRunsAndProgressArePrivateToEachPlayer()
    {
        var (world,id,door,clock)=await RetroFixture();
        await world.JoinAsync("retro-visitor","Visitor");await world.SetGodModeAsync("retro-visitor",true);await world.TeleportAsync("retro-visitor",new(door.Position.X,door.Position.Y,true));
        world.StartInversion("retro",world.GetRetroBattleUpdate(id).Player!,clock.GetUtcNow());
        var first=await world.EnterEventDungeonAsync(id);var second=await world.EnterEventDungeonAsync("retro-visitor");Assert.NotEqual(first.Id,second.Id);
        world.JumpRetroBattle(id);Assert.Equal(0,world.GetPrivateState("retro-visitor").Dungeon!.RetroBattle!.JumpVelocity);
        await ClearRetro(world,id,clock);Assert.Equal(0,world.GetInversionView("retro-visitor").CompletedDungeons);Assert.Equal(1,world.GetInversionView(id).CompletedDungeons);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void RetroEveryDifficultyCanBeClearedUsingOnlyGroundedJumps(int difficulty)
    {
        var now = DateTimeOffset.UtcNow;
        var run = RealityWorld.CreateRetroBattle(difficulty, now);
        for (var i = 0; i < 5000 && run.Enemies.Any(e => !e.Defeated); i++)
        {
            var next = run.Enemies.Where(e => !e.Defeated).Select(e => RealityWorld.RetroEnemyX(e.X, run.Scroll, run.TrackLength)).Where(x => x > 6).DefaultIfEmpty(double.MaxValue).Min();
            if (run.JumpHeight == 0 && next <= 6 + run.ScrollSpeed * .8) run = run with { JumpVelocity = 9 };
            run = RealityWorld.StepRetroBattle(run, .05, now);
        }
        Assert.All(run.Enemies, e => Assert.True(e.Defeated));
    }

    [Fact]
    public async Task RetroAllowsEarlyExitAndNeverConvertsHome()
    {
        var (world, id, door, _) = await RetroFixture();
        world.StartInversion("retro",world.GetRetroBattleUpdate(id).Player!,DateTimeOffset.UtcNow);
        var first = await world.EnterEventDungeonAsync(id);
        var wallet = world.GetRetroBattleUpdate(id).Player!.WalletCents;
        Assert.Equal(wallet, (await world.ExitDungeonAsync(id)).WalletCents);
        var second = await world.EnterEventDungeonAsync(id);
        Assert.Equal(first.Id, second.Id);
        Assert.All(second.RetroBattle!.Enemies, e => Assert.False(e.Defeated));
        await world.ExitDungeonAsync(id);
        var home = world.GetPrivateState(id).Base!;
        var homeDoor = world.CreateSnapshot().BaseEntities.Single(e => e.Id == home.DoorId);
        await world.TeleportAsync(id, new(homeDoor.Position.X, homeDoor.Position.Y, true));
        var enteredHome = await world.EnterDungeonAsync(id, homeDoor.Id);
        Assert.True(enteredHome.Dungeon.IsHome);
        Assert.Null(enteredHome.Dungeon.RetroBattle);
        await world.AdvanceRetroBattlesAsync(TimeSpan.FromSeconds(.05));
        Assert.Null(world.GetPrivateState(id).Dungeon!.RetroBattle);
    }
}
