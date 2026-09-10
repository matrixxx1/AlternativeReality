using System.Collections.Concurrent;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World, PlayerState Player, ProbulatorTestClock Clock)> ScubaFixture(bool gear = true, bool raft = false, bool shallow = false, bool swimmies = false)
    {
        var config = new RealityConfiguration(Guid.NewGuid().ToString("N"), "Scuba", 23, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var center = new LocalTangentProjection(config.Area.Region).Project(config.Area.Center);
        var lake = new CanonicalEntity("dive-lake", EntityKind.Water, center,
            [new(center.X-90,center.Y-90),new(center.X+90,center.Y-90),new(center.X+90,center.Y+90),new(center.X-90,center.Y+90),new(center.X-90,center.Y-90)],
            new Dictionary<string,string> { ["natural"]="water", ["name"]="Test lake" });
        var store = new SqliteRealityStore(Path.Combine(_directory, config.Id + ".db")); await store.InitializeAsync(config);
        await store.SaveInventoryAsync(new("diver", [new("swimmies",swimmies?1:0),new("scubaGear",gear?1:0),new("inflatableRaft",raft?1:0),new("spearGun",1,Quality:"Common"),new("spear",30),new("fish",2),new("knife",1),new("sword",1),new("hockeyStick",1),new("iceSkate",1),new("rifle",1),new("bullet",10),new("grenade",1)]));
        var clock = new ProbulatorTestClock();
        var world = new RealityWorld(config,new DeterministicWorldGenerator(new FixedGeographicProvider(lake)),new FixedWeatherProvider(),store,clock);
        await world.InitializeAsync(); var player = await world.JoinAsync("diver","Diver");
        player = player with { Position = center with { X = center.X + (shallow ? 88 : 0) }, Terrain = shallow?TerrainType.ShallowWater:TerrainType.DeepWater, TravelMode=raft?TravelMode.Raft:TravelMode.Walk };
        ScubaPlayer(world,player); return(world,player,clock);
    }
    private static void ScubaPlayer(RealityWorld world,PlayerState player) => PhotoField<ConcurrentDictionary<string,PlayerState>>(world,"_players")[player.Id]=player;
    private static PlayerState ScubaPlayer(RealityWorld world,string id="diver") => PhotoField<ConcurrentDictionary<string,PlayerState>>(world,"_players")[id];
    private static void ScubaDungeon(RealityWorld world,DungeonState dungeon) => PhotoField<ConcurrentDictionary<string,DungeonState>>(world,"_dungeons")[dungeon.Id]=dungeon;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RaftCanBeDeployedInDeepOrShallowWater(bool shallow)
    {
        var (world, player, _) = await ScubaFixture(raft: true, shallow: shallow);
        ScubaPlayer(world, player with { TravelMode = TravelMode.Walk });
        var deployed = await world.SetTravelModeAsync(player.Id, TravelMode.Raft);
        Assert.Equal(TravelMode.Raft, deployed.TravelMode);
        Assert.Equal(shallow ? TerrainType.ShallowWater : TerrainType.DeepWater, deployed.Terrain);
        Assert.Equal(player.Position, deployed.Position);
        Assert.False(deployed.GodMode);
    }

    [Fact]
    public async Task RaftDeploymentStillRequiresOwnershipAndWater()
    {
        var (world, player, _) = await ScubaFixture();
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => world.SetTravelModeAsync(player.Id, TravelMode.Raft));
        Assert.Contains("need an inflatable raft", missing.Message);
        var (equipped, land, _) = await ScubaFixture(raft: true);
        ScubaPlayer(equipped, land with { Position = land.Position with { X = land.Position.X + 110 }, TravelMode = TravelMode.Walk });
        var dry = await Assert.ThrowsAsync<InvalidOperationException>(() => equipped.SetTravelModeAsync(land.Id, TravelMode.Raft));
        Assert.Contains("shallow or deep water", dry.Message);
    }

    [Fact]
    public async Task ScubaRequiresGearNormallyAndWaterEvenInGodMode()
    {
        var (world,p,_) = await ScubaFixture(false);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.SetTravelModeAsync(p.Id,TravelMode.Scuba));
        var (equipped,land,_) = await ScubaFixture();ScubaPlayer(equipped,land with {GodMode=true,Position=land.Position with{X=land.Position.X+110}});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>equipped.SetTravelModeAsync(land.Id,TravelMode.Scuba));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwimmiesClickRoutesAndMovementAllowDeepWater(bool shallow)
    {
        var (world, player, _) = await ScubaFixture(gear: false, shallow: shallow, swimmies: true);
        var navigation = PhotoField<WorldNavigation>(world, "_navigation");
        var target = player.Position with { X = player.Position.X - 10 };
        Assert.Equal(TerrainType.DeepWater, navigation.TerrainAt(target.X, target.Y));
        Assert.False((await world.FindPathAsync(player.Id, new(target.X, target.Y, 1))).Result.Success);
        await world.SetTravelModeAsync(player.Id, TravelMode.Swim);
        var route = (await world.FindPathAsync(player.Id, new(target.X, target.Y, 2))).Result;
        Assert.True(route.Success, route.Message);
        var moved = (await world.MoveAsync(player.Id, new(-1, 0, 3)))!;
        Assert.True(moved.Moved);Assert.Equal(TravelMode.Swim, moved.Player.TravelMode);
        Assert.False(moved.Player.GodMode);
    }

    [Fact]
    public async Task SwimmiesCanRouteToShoreAndAutomaticallyWalkOnLand()
    {
        var (world, player, _) = await ScubaFixture(gear: false, swimmies: true);
        await world.SetTravelModeAsync(player.Id, TravelMode.Swim);
        var shore = player.Position with { X = player.Position.X + 95 };
        var path = (await world.FindPathAsync(player.Id, new(shore.X, shore.Y, 1))).Result;
        Assert.True(path.Success, path.Message);
        ScubaPlayer(world, ScubaPlayer(world) with { Position = player.Position with { X = player.Position.X + 89.999 } });
        var moved = (await world.MoveAsync(player.Id, new(1, 0, 2)))!;
        Assert.True(moved.Moved);Assert.Equal(TravelMode.Walk, moved.Player.TravelMode);
    }

    [Theory]
    [InlineData(false,false,TravelMode.Swim)]
    [InlineData(false,true,TravelMode.Raft)]
    [InlineData(true,false,TravelMode.Walk)]
    [InlineData(true,true,TravelMode.Raft)]
    public async Task ScubaReturnsToCorrectSurfaceModeWithoutRequiringSwimmies(bool shallow,bool raft,TravelMode expected)
    {
        var (world,p,_) = await ScubaFixture(raft:raft,shallow:shallow);
        var diver=await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);
        Assert.Equal(TravelMode.Scuba,diver.TravelMode);Assert.StartsWith("dive:",diver.LocationId);
        var dungeon=world.GetPrivateState(p.Id).Dungeon!;Assert.Equal("Test lake",dungeon.Underwater!.Name);
        var surfaced=await world.SetTravelModeAsync(p.Id,TravelMode.Walk);
        Assert.Equal("outdoor",surfaced.LocationId);Assert.Equal(expected,surfaced.TravelMode);
        Assert.InRange(surfaced.Position.Distance2D(p.Position),0,1.1);Assert.Null(world.GetPrivateState(p.Id).Dungeon);
        if(expected==TravelMode.Swim) Assert.Equal(TravelMode.Swim,(await world.MoveAsync(p.Id,new(1,0,1)))!.Player.TravelMode);
    }

    [Fact]
    public async Task ScubaWaterlineAndShoreRequireExplicitSurfacing()
    {
        var (world,p,_) = await ScubaFixture();var diver=await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);var dungeon=world.GetPrivateState(p.Id).Dungeon!;
        ScubaPlayer(world,diver with{Position=diver.Position with{Y=dungeon.Height-.501}});
        var waterline=(await world.MoveAsync(p.Id,new(0,1,1)))!.Player;
        Assert.Equal(TravelMode.Scuba,waterline.TravelMode);Assert.Equal(dungeon.Id,waterline.LocationId);
        Assert.Equal(dungeon.Height-.5,waterline.Position.Y);
        Assert.True(dungeon.Underwater!.WestShore);
        ScubaPlayer(world,diver with{Position=diver.Position with{X=.501,Y=4}});
        var shore=(await world.MoveAsync(p.Id,new(-1,0,2)))!.Player;
        Assert.Equal(dungeon.Id,shore.LocationId);Assert.Equal(TravelMode.Scuba,shore.TravelMode);
        Assert.Equal(1,shore.Position.X);
        Assert.True(shore.Position.Y > 18);
        var surfaced=await world.ExitDungeonAsync(p.Id);
        Assert.Equal("outdoor",surfaced.LocationId);Assert.NotEqual(TravelMode.Scuba,surfaced.TravelMode);
    }

    [Fact]
    public async Task ScubaSlopingBanksKeepRoutesCreaturesAndTreasureInWater()
    {
        var (world,p,_) = await ScubaFixture();
        await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);
        var dungeon = world.GetPrivateState(p.Id).Dungeon!;
        foreach (var point in dungeon.Actors.Select(actor => actor.Position).Concat(dungeon.Chests.Select(chest => chest.Position)).Append(dungeon.Exit))
            Assert.True(point.Y >= ScubaGeometry.FloorHeight(dungeon.Width,dungeon.Height,dungeon.Underwater!,point.X)+.35-1e-9);
        foreach (var x in new[] {-10d,dungeon.Width+10})
        {
            var path = (await world.FindPathAsync(p.Id,new(x,1,1))).Result;
            Assert.True(path.Success);
            var target = path.Waypoints.Last();
            Assert.True(target.Y >= ScubaGeometry.FloorHeight(dungeon.Width,dungeon.Height,dungeon.Underwater!,target.X)+.35-1e-9);
        }
        var water = dungeon.Underwater! with { WestShore=true,EastShore=true };
        foreach (var (x,y) in new[] {(.5,19.5),(9.5,10d),(18.5,.5),(90d,.5),(161.5,.5),(170.5,10d),(179.5,19.5)})
            Assert.Equal(y,ScubaGeometry.FloorHeight(180,20,water,x),8);
        Assert.Equal(.5,ScubaGeometry.FloorHeight(180,20,water with { EastShore=false },200));
    }

    [Fact]
    public void ScubaAirLastsMuchLongerButStillRunsOutAndRecoversOnSurface()
    {
        var player=new PlayerState("diver","Diver",new(default,0,0),TravelMode:TravelMode.Scuba);
        var diver=RealityWorld.StepAir(player,2,false,true,false);
        var unprotected=RealityWorld.StepAir(player with{TravelMode=TravelMode.Walk},2,false,true,false);
        Assert.Equal(9.95,diver.Air,6);Assert.Equal(6,unprotected.Air);
        for(var i=0;i<199;i++)diver=RealityWorld.StepAir(diver,2,false,true,false);
        Assert.InRange(diver.Air,0,.00001);
        diver=RealityWorld.StepAir(diver,2,false,true,false);Assert.True(diver.HealthHearts<10);
        var surfaced=RealityWorld.StepAir(diver with{TravelMode=TravelMode.Swim,Stamina=10},2,false,true,false);
        Assert.Equal(4,surfaced.Air,5);
        Assert.Equal(9.95,RealityWorld.StepAir(player with{TravelMode=TravelMode.Swim,Stamina=0},2,false,true,true,true).Air,5);
        Assert.Equal(6,RealityWorld.StepAir(player,2,true,true,false,true).Air);
    }

    [Fact]
    public async Task ScubaClickAboveWaterRoutesToSurfaceAndRejoiningCannotRefillTheTank()
    {
        var (world,p,_) = await ScubaFixture();await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);
        await world.AdvanceVitalsAsync(TimeSpan.FromSeconds(2), default);
        var air=ScubaPlayer(world).Air;Assert.True(air<10);
        var rejoined=await world.JoinAsync(p.Id,p.Name);Assert.Equal(TravelMode.Scuba,rejoined.TravelMode);Assert.Equal(air,rejoined.Air);
        var d=world.GetPrivateState(p.Id).Dungeon!;
        var path=await world.FindPathAsync(p.Id,new(rejoined.Position.X,d.Height+10,1));
        Assert.True(path.Result.Success);Assert.Equal(d.Height-.5,path.Result.Waypoints.Last().Y);
    }

    [Fact]
    public async Task SlowSpearCanBeDodgedAndCannotHitAfterTheDiverLeaves()
    {
        var (world,p,clock) = await ScubaFixture();var diver=await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);var d=world.GetPrivateState(p.Id).Dungeon!;
        var fish=d.Actors.First(a=>a.Subtype=="fish");ScubaPlayer(world,diver with{Position=fish.Position,EquippedWeapon="spearGun"});
        await world.AttackAsync(p.Id,new(fish.Id,"spearGun"));
        ScubaDungeon(world,d with{Actors=[fish with{Position=fish.Position with{X=fish.Position.X+5}}]});
        clock.Advance(1);Assert.False(Assert.Single(await world.AdvanceSpearsAsync()).Hit);
        PhotoField<ConcurrentDictionary<(string Player,string Weapon),DateTimeOffset>>(world,"_lastPlayerAttack").Clear();
        await world.AttackAsync(p.Id,new(fish.Id,"spearGun"));await world.ExitDungeonAsync(p.Id);clock.Advance(10);
        Assert.Empty(await world.AdvanceSpearsAsync());
    }

    [Fact]
    public async Task ScubaDifficultyScalesGuardiansRewardsAndWaterLootOdds()
    {
        var (world,p,_) = await ScubaFixture();
        await world.SetTravelModeAsync(p.Id,TravelMode.Scuba,mapView:new(p.Position.X-10,p.Position.Y-10,p.Position.X+10,p.Position.Y+10));
        var small=world.GetPrivateState(p.Id).Dungeon!;await world.ExitDungeonAsync(p.Id);
        await world.SetTravelModeAsync(p.Id,TravelMode.Scuba,mapView:new(p.Position.X-100,p.Position.Y-100,p.Position.X+100,p.Position.Y+100));
        var large=world.GetPrivateState(p.Id).Dungeon!;Assert.True(large.Difficulty>small.Difficulty);Assert.True(large.Actors.Count>small.Actors.Count);
        Assert.Contains(large.Actors,a=>a.Subtype=="shark");Assert.Contains(large.Actors,a=>a.Subtype=="octopus");Assert.True(large.Actors.Count(a=>a.Subtype=="fish")>=12);
        foreach(var chest in large.Chests)Assert.Single(large.Actors,a=>a.FactionId==chest.Id && a.Subtype is "largeShark" or "largeOctopus");
        var guarded=large.Chests[0];ScubaPlayer(world,ScubaPlayer(world) with{Position=guarded.Position});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.OpenChestAsync(p.Id,guarded.Id));
        ScubaDungeon(world,large with{Actors=large.Actors.Where(a=>a.FactionId!=guarded.Id).ToArray()});
        await world.OpenChestAsync(p.Id,guarded.Id);
        var low=world.CreateChestContents("fixed",true,true,1);var high=world.CreateChestContents("fixed",true,true,70);Assert.True(high.MoneyCents>low.MoneyCents);
        Assert.Contains(high.Items,i=>i.ItemType=="spearGun"&&i.Quality=="Legendary");
        var inland=Enumerable.Range(0,500).Count(i=>world.CreateChestContents("odds"+i).Items.Any(s=>s.ItemType=="spear"));
        var waterside=Enumerable.Range(0,500).Count(i=>world.CreateChestContents("odds"+i,nearWater:true).Items.Any(s=>s.ItemType=="spear"));
        Assert.InRange(inland,50,180);Assert.True(waterside>inland*2);
    }

    [Theory]
    [InlineData("fist")][InlineData("knife")][InlineData("sword")][InlineData("hockeyStick")][InlineData("iceSkate")]
    public async Task AllMeleeWeaponsCanAttackSubmerged(string weapon)
    {
        var (world,p,_) = await ScubaFixture();var diver=await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);var d=world.GetPrivateState(p.Id).Dungeon!;
        var fish=d.Actors.First(a=>a.Subtype=="fish");ScubaPlayer(world,diver with{Position=fish.Position,EquippedWeapon=weapon});
        Assert.Equal(weapon,(await world.AttackAsync(p.Id,new(fish.Id,weapon))).Event.Weapon);
    }

    [Theory]
    [InlineData("rifle")][InlineData("grenade")][InlineData("flamethrower")][InlineData("napalmBottle")][InlineData("crossbow")]
    public async Task UnderwaterRejectsOtherRangedAndHazardWeaponsBeforeSpendingAmmo(string weapon)
    {
        var (world,p,_) = await ScubaFixture();var diver=await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);var fish=world.GetPrivateState(p.Id).Dungeon!.Actors[0];
        ScubaPlayer(world,diver with{Position=fish.Position,EquippedWeapon=weapon});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.AttackAsync(p.Id,new(fish.Id,weapon)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.ThrowHazardAsync(p.Id,new(fish.Position.X,fish.Position.Y)));
        Assert.Equal(10,world.GetPrivateState(p.Id).Inventory.Items.Single(i=>i.ItemType=="bullet").Quantity);
    }

    [Fact]
    public async Task SpearsConsumeAmmoEnforceReloadAndResolveOnlyAfterTravelThenFishCanBeCollectedAndEaten()
    {
        var (world,p,clock) = await ScubaFixture();var diver=await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);var d=world.GetPrivateState(p.Id).Dungeon!;
        var fish=d.Actors.First(a=>a.Subtype=="fish") with{HealthHearts=.01};ScubaDungeon(world,d with{Actors=[fish]});
        ScubaPlayer(world,diver with{Position=fish.Position,EquippedWeapon="spearGun",Stamina=2,HealthHearts=5});
        var config=world.GetPrivateState(p.Id).ServerConfiguration!;
        Assert.Equal(config.Items.Single(i=>i.ItemType=="rifle").Damage,config.Items.Single(i=>i.ItemType=="spearGun").Damage);
        Assert.True(config.Items.Single(i=>i.ItemType=="spearGun").AttackIntervalSeconds>config.Items.Single(i=>i.ItemType=="rifle").AttackIntervalSeconds);
        await world.AttackAsync(p.Id,new(fish.Id,"spearGun"));Assert.Single(world.GetPrivateState(p.Id).Dungeon!.Actors);
        Assert.Equal(29,world.GetPrivateState(p.Id).Inventory.Items.Single(i=>i.ItemType=="spear").Quantity);
        Assert.Empty(await world.AdvanceSpearsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.AttackAsync(p.Id,new(fish.Id,"spearGun")));
        clock.Advance(.36);var impact=Assert.Single(await world.AdvanceSpearsAsync());Assert.Equal("spearImpact",impact.Weapon);
        // Retry misses without real sleeps by clearing the attack timestamp in this fixture.
        for(var i=0;i<20&&!impact.TargetDied;i++)
        {
            PhotoField<ConcurrentDictionary<(string Player,string Weapon),DateTimeOffset>>(world,"_lastPlayerAttack").Clear();
            await world.AttackAsync(p.Id,new(fish.Id,"spearGun"));clock.Advance(.36);impact=Assert.Single(await world.AdvanceSpearsAsync());
        }
        Assert.True(impact.TargetDied);
        var loot=Assert.Single(world.GetPrivateState(p.Id).Loot!,l=>l.Items.Any(i=>i.ItemType=="fish"));Assert.Single(loot.Items);
        await world.TakeLootItemsAsync(p.Id,new(loot.Id,[new("fish",1)]));
        var fed=await world.ConsumeItemAsync(p.Id,"fish");Assert.Equal(5.25,fed.HealthHearts,5);Assert.Equal(2.75,fed.Stamina,5);
        Assert.Contains(CraftingCatalog.Recipes,r=>r.Id=="fishStew"&&r.Ingredients.Any(i=>i.ItemType=="fish"));
        Assert.Contains(CraftingCatalog.Recipes,r=>r.Id=="friedFish"&&r.Ingredients.Any(i=>i.ItemType=="fish"));
    }

    [Fact]
    public async Task ScubaEnemiesMoveAndGuardiansAttackWhileFishRemainHarmless()
    {
        var (world,p,clock) = await ScubaFixture();var diver=await world.SetTravelModeAsync(p.Id,TravelMode.Scuba);var d=world.GetPrivateState(p.Id).Dungeon!;
        var boss=d.Actors.First(a=>a.Subtype=="largeShark");ScubaPlayer(world,diver with{Position=boss.Position});
        var tick=await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));Assert.Contains(tick.Combat,c=>c.AttackerId==boss.Id&&c.Damage>0);
        Assert.DoesNotContain(tick.Combat,c=>d.Actors.Any(a=>a.Id==c.AttackerId&&a.Subtype=="fish"));
        Assert.Contains(tick.Actors,a=>a.Subtype=="fish"&&a.Position!=d.Actors.Single(b=>b.Id==a.Id).Position);
    }
}
