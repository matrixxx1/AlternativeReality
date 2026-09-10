using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private async Task<(RealityWorld World, SqliteRealityStore Store, string Id, CanonicalEntity Door)> CasinoFixture()
    {
        var config = new RealityConfiguration("casino-tests", "Casino", 93, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        CanonicalEntity House(string id, double x) => new(id, EntityKind.Building, new WorldPosition(config.Area.Region, x, 20),
            [new(x-5,15),new(x+5,15),new(x+5,25),new(x-5,25),new(x-5,15)], new Dictionary<string,string> { ["building"]="yes" });
        var store = new SqliteRealityStore(Path.Combine(_directory,"casino.db"));
        await store.InitializeAsync(config);
        await store.CreateAccountAsync(new AccountRecord("casino-account","Casino","hash","salt","token","casino-player"),"Casino");
        var world = new RealityWorld(config, new DeterministicWorldGenerator(new FixedGeographicProvider(House("casino-a",20),House("casino-b",60),House("casino-c",100))),new FixedWeatherProvider(),store);
        await world.InitializeAsync();
        var player = await world.JoinAsync("casino-player","Casino","casino-account");
        var casino = Assert.Single(world.CreateSnapshot().BaseEntities,e=>e.Kind==EntityKind.Building && e.Properties.GetValueOrDefault("merchantCategory")=="casino");
        Assert.NotEqual(world.GetPrivateState(player.Id).Base!.BuildingId,casino.Id);
        var mapLocation = world.GetPrivateState(player.Id).Casino;
        Assert.NotNull(mapLocation);
        Assert.Equal(casino.Id,mapLocation.BuildingId);
        Assert.Equal(casino.Position,mapLocation.Position);
        var door = world.CreateSnapshot().BaseEntities.Single(e=>e.Kind==EntityKind.Door && e.Properties["buildingId"]==casino.Id);
        await world.SetGodModeAsync(player.Id,true);
        await world.TeleportAsync(player.Id,new(door.Position.X,door.Position.Y,true));
        var entered = await world.EnterDungeonAsync(player.Id,door.Id);
        Assert.Equal("casino",entered.Dungeon.StoreCategory);
        Assert.Single(entered.Dungeon.Rooms); Assert.Empty(entered.Dungeon.Walls); Assert.Null(entered.Dungeon.Stairs);
        return (world,store,player.Id,door);
    }

    private static async Task<PlayerState> SeatCasinoPlayer(RealityWorld world, SqliteRealityStore store, string id, double x, long wallet)
    {
        var players = (ConcurrentDictionary<string,PlayerState>)typeof(RealityWorld).GetField("_players",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(world)!;
        var p = players[id] with { Position=players[id].Position with { X=x,Y=4 },WalletCents=wallet,Version=players[id].Version+1 };
        await store.SaveCharacterAsync(world.Configuration.Id,p);
        players[id]=p;
        return p;
    }

    [Fact]
    public async Task CasinoDeductsOnceAndCannotOverdrawEvenInGodMode()
    {
        var (world,store,id,_) = await CasinoFixture();
        await SeatCasinoPlayer(world,store,id,35.432,10000);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.CasinoAsync(id,new(Guid.NewGuid().ToString(),"slots",10001)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.CasinoAsync(id,new(Guid.NewGuid().ToString(),"slots",0)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.CasinoAsync(id,new(Guid.NewGuid().ToString(),"slots",-1)));
        var request = new CasinoBetRequest(Guid.NewGuid().ToString(),"slots",10000);
        var results = await Task.WhenAll(world.CasinoAsync(id,request),world.CasinoAsync(id,request));
        Assert.All(results,r=>Assert.Equal(0,r.Player.WalletCents));
        var accepted = results[0].Round!;
        Assert.Equal("resolve",accepted.Phase);
        var result = await world.CasinoAsync(id,action:new(accepted.RoundId,accepted.Revision,"resolve"));
        Assert.Equal(result.Round!.PayoutCents,result.Player.WalletCents);
        var replay = await world.CasinoAsync(id,action:new(accepted.RoundId,accepted.Revision,"resolve"));
        Assert.Equal(result.Player.WalletCents,replay.Player.WalletCents);
        Assert.Equivalent(result.Round,replay.Round);
        Assert.Equal(result.Player.WalletCents,(await store.LoadCharacterAsync(world.Configuration.Id,id))!.WalletCents);
        var persisted = await new SqliteRealityStore(Path.Combine(_directory,"casino.db")).LoadCasinoRoundAsync(world.Configuration.Id,id,null,CancellationToken.None);
        Assert.Equal(result.Round.PayoutCents,persisted!.PayoutCents);
        Assert.Equal("complete",persisted.Phase);
    }

    [Fact]
    public async Task CasinoBigWinCreatesTwoPrivatePersistentItemsExactlyOnce()
    {
        var (world,store,id,_) = await CasinoFixture();
        await SeatCasinoPlayer(world,store,id,8,200000);
        var accepted = await world.CasinoAsync(id,new(Guid.NewGuid().ToString(),"blackjack",100000));
        Assert.Equal(100000,accepted.Player.WalletCents);
        var saved = (await store.LoadCasinoRoundAsync(world.Configuration.Id,id,null,CancellationToken.None))!;
        saved = saved with { Cards=[12,8],DealerCards=[8,5],Phase="resolve" };
        await store.SaveCasinoTransitionAsync(world.Configuration.Id,accepted.Player,saved,null,CancellationToken.None);
        var won = await world.CasinoAsync(id,action:new(saved.RoundId,saved.Revision,"resolve"));
        Assert.Equal(350000,won.Player.WalletCents);
        var reward = world.OpenLoot(id,won.Round!.RewardLootId!);
        Assert.Equal(2,reward.Items.Sum(i=>i.Quantity));
        Assert.Throws<InvalidOperationException>(()=>world.OpenLoot("other-player",reward.Id));
        await world.CasinoAsync(id,action:new(saved.RoundId,saved.Revision,"resolve"));
        Assert.Single(await store.LoadPersistentLootAsync(world.Configuration.Id),l=>l.Id==reward.Id);
        await world.ExitDungeonAsync(id);
        Assert.Equal(reward.Id,world.OpenLoot(id,reward.Id).Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.CasinoAsync(id,new(Guid.NewGuid().ToString(),"slots",1)));
    }

    [Fact]
    public async Task CasinoRejectsDistantTablesAndHomePurchaseAndRestrictsMovementToHorizontal()
    {
        var (world,store,id,door) = await CasinoFixture();
        await SeatCasinoPlayer(world,store,id,8,10000);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.CasinoAsync(id,new(Guid.NewGuid().ToString(),"slots",100)));
        var moved = await world.MoveAsync(id,new(0,1,1));
        Assert.Equal(4,moved!.Player.Position.Y);
        await world.ExitDungeonAsync(id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.PurchaseBaseAsync(id,new(door.Id)));
    }
}

public sealed class CasinoRulesTests
{
    private static CasinoSavedRound Blackjack(int[] cards,int[] dealer,int[] deck) =>
        CasinoRules.Start(Guid.NewGuid().ToString(),"p","b","blackjack",1000,null) with { Cards=cards,DealerCards=dealer,Deck=deck,Phase="playing" };

    [Fact]
    public void BlackjackHandlesAcesNaturalsPushesAndBusts()
    {
        Assert.Equal(12,CasinoRules.BlackjackTotal([12,25]));
        var natural=Blackjack([12,8],[8,5],[]) with { Phase="resolve" };
        Assert.Equal(2500,CasinoRules.Act(natural,new(natural.RoundId,0,"resolve")).PayoutCents);
        var push=natural with { DealerCards=[25,21] };
        Assert.Equal(1000,CasinoRules.Act(push,new(push.RoundId,0,"resolve")).PayoutCents);
        var bust=Blackjack([8,9],[8,5],[10]);
        Assert.Equal(0,CasinoRules.Act(bust,new(bust.RoundId,0,"hit")).PayoutCents);
        var view=CasinoRules.View(bust);
        Assert.Equal("?",view.DealerCards[1]);
    }

    [Theory]
    [InlineData(new int[]{8,9,10,11,12},250)]
    [InlineData(new int[]{12,0,1,2,3},50)]
    [InlineData(new int[]{9,22,0,15,30},1)]
    [InlineData(new int[]{0,13,2,16,32},0)]
    public void PokerRecognizesRoyalWheelAndHighPair(int[] cards,int multiplier) => Assert.Equal(multiplier,CasinoRules.PokerPayout(cards).Multiplier);

    [Fact]
    public void PokerOnlyDrawsUnheldCardsAndRejectsInvalidHolds()
    {
        var poker=CasinoRules.Start(Guid.NewGuid().ToString(),"p","b","poker",100,null);
        Assert.Equal(52,poker.Deck.Concat(poker.Cards).Distinct().Count());
        var result=CasinoRules.Act(poker,new(poker.RoundId,0,"draw",[0,2,4]));
        Assert.Equal(poker.Cards[0],result.Cards[0]); Assert.Equal(poker.Cards[2],result.Cards[2]);
        Assert.Equal(poker.Deck[0],result.Cards[1]); Assert.Equal(poker.Deck[1],result.Cards[3]);
        Assert.Throws<InvalidOperationException>(()=>CasinoRules.Act(poker,new(poker.RoundId,0,"draw",[0,0])));
        Assert.Throws<InvalidOperationException>(()=>CasinoRules.Act(poker,new(poker.RoundId,0,"draw",[5])));
        Assert.Same(result,CasinoRules.Act(result,new(result.RoundId,0,"draw",[])));
    }
}
