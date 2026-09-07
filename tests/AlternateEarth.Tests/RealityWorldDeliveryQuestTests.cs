using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static void PositionCourierForQuestTest(RealityWorld world, WorldPosition position, string locationId = "outdoor")
    {
        var players = (ConcurrentDictionary<string, PlayerState>)typeof(RealityWorld).GetField("_players", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        players["pilot"] = players["pilot"] with { Position = position, LocationId = locationId, Version = players["pilot"].Version + 1 };
    }

    private async Task<(RealityWorld World, SqliteRealityStore Store, ActorState Giver)> DeliveryWorld(ProbulatorTestClock clock)
    {
        var (world, store, npc) = await ImpressionWorld(clock);
        var giver = npc with { IsMerchant = true, IsQuestGiver = true, OffersFoodDelivery = true, Subtype = "storeMerchant", MerchantCategory = "food" };
        ImpressionActors(world)[giver.Id] = giver;
        return (world, store, giver);
    }

    [Fact]
    public async Task FoodDeliveryStartsOnAcceptanceRequiresExactRecipientAndCanBeRepeatedAfterSuccess()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, giver) = await DeliveryWorld(clock);
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        Assert.Equal("foodDelivery", offer.Kind);
        Assert.Null(offer.DeadlineUtc);
        Assert.InRange(offer.DeliveryMinutes!.Value, 5, 30);
        clock.Advance(TimeSpan.FromMinutes(10));
        var accepted = await world.AcceptQuestAsync("pilot", offer.Id);
        Assert.Equal(clock.GetUtcNow().AddMinutes(offer.DeliveryMinutes.Value), accepted.Quest.DeadlineUtc);
        Assert.Single(accepted.PrivateState.Inventory.Items, i => i.ItemType == $"quest:food:{offer.Id}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteQuestAsync("pilot", new(offer.Id, giver.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AcceptQuestAsync("pilot", offer.Id));
        var recipient = accepted.Quest.DeliveryRecipient!;
        world.AdvanceActors(TimeSpan.FromSeconds(30));
        Assert.Equal(recipient.Position, ImpressionActors(world)[recipient.Id].Position);
        PositionCourierForQuestTest(world, recipient.Position);
        Assert.True(world.RequestQuest("pilot", recipient.Id).CanComplete);
        var balance = world.CreateSnapshot().Players.Single(p => p.Id == "pilot").WalletCents;
        var completed = await world.CompleteQuestAsync("pilot", new(offer.Id, recipient.Id));
        Assert.Equal("completed", completed.Quest.Status);
        Assert.Equal(balance + offer.RewardCents, completed.Player.WalletCents);
        Assert.Equal(.25, completed.PrivateState.Progression!.Alignment);
        Assert.True(completed.PrivateState.Progression.Experience > 0);
        var completionXp = completed.PrivateState.Progression.Experience;
        Assert.DoesNotContain(completed.PrivateState.Inventory.Items, i => i.ItemType == $"quest:food:{offer.Id}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteQuestAsync("pilot", new(offer.Id, recipient.Id)));
        Assert.Equal(completionXp, world.GetProgression("pilot").Experience);
        Assert.Equal(.25, world.GetProgression("pilot").Alignment);
        await world.TeleportAsync("pilot", new(giver.Position.X, giver.Position.Y, true));
        var next = world.RequestQuest("pilot", giver.Id);
        Assert.True(next.IsOffer);
        Assert.NotEqual(offer.Id, next.Quest.Id);
        Assert.Equal("completed", (await store.LoadQuestsAsync(world.Configuration.Id, "pilot")).Single().Status);
    }

    [Fact]
    public async Task EatingDeliveryFailsImmediatelyIsACrimeEvenInGodModeAndBlocksEveryFoodMerchantFor24Hours()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, giver) = await DeliveryWorld(clock);
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        await world.AcceptQuestAsync("pilot", offer.Id);
        var other = giver with { Id = "other-food-counter", Name = "Another Restaurant" };
        ImpressionActors(world)[other.Id] = other;
        Assert.Throws<InvalidOperationException>(() => world.RequestQuest("pilot", other.Id));
        var consumed = await world.ConsumeItemAsync("pilot", $"quest:food:{offer.Id}");
        Assert.True(consumed.GodMode);
        Assert.Equal(1, consumed.WantedLevel);
        var failed = world.GetPrivateState("pilot").Quests!.Single();
        Assert.Equal("failed", failed.Status);
        Assert.Equal(clock.GetUtcNow(), failed.FailedAtUtc);
        Assert.DoesNotContain(world.GetPrivateState("pilot").Inventory.Items, i => i.ItemType.StartsWith("quest:food:"));
        Assert.Throws<InvalidOperationException>(() => world.RequestQuest("pilot", giver.Id));
        Assert.Throws<InvalidOperationException>(() => world.RequestQuest("pilot", other.Id));
        Assert.Equal(2, world.TakeQuestDialogue().Where(c => c.Id.StartsWith("delivery-refusal:")).Select(c => c.Message).Distinct().Count());
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ConsumeItemAsync("pilot", $"quest:food:{offer.Id}"));
        clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1));
        Assert.Throws<InvalidOperationException>(() => world.RequestQuest("pilot", other.Id));
        clock.Advance(1);
        Assert.True(world.RequestQuest("pilot", other.Id).IsOffer);
        Assert.Equal("failed", (await store.LoadQuestsAsync(world.Configuration.Id, "pilot")).Single().Status);
    }

    [Fact]
    public async Task DeliveryDeadlineSurvivesRestartAndOfflineFailureUsesTheOriginalDeadline()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, giver) = await DeliveryWorld(clock);
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        var accepted = await world.AcceptQuestAsync("pilot", offer.Id);
        await world.LeaveAsync("pilot");
        var restarted = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store, clock);
        await restarted.InitializeAsync();
        await restarted.JoinAsync("pilot", "Pilot");
        Assert.Equal(accepted.Quest.DeliveryRecipient, ImpressionActors(restarted)[accepted.Quest.DestinationActorId!]);
        await restarted.LeaveAsync("pilot");
        clock.Advance(TimeSpan.FromMinutes(offer.DeliveryMinutes!.Value + 60));
        await restarted.JoinAsync("pilot", "Pilot");
        ImpressionActors(restarted)[giver.Id] = giver;
        var failed = restarted.GetPrivateState("pilot").Quests!.Single();
        Assert.Equal("failed", failed.Status);
        Assert.Equal(accepted.Quest.DeadlineUtc, failed.FailedAtUtc);
        Assert.Throws<InvalidOperationException>(() => restarted.RequestQuest("pilot", giver.Id));
        Assert.Equal(0, restarted.CreateSnapshot().Players.Single(p => p.Id == "pilot").WantedLevel);
        clock.Advance(TimeSpan.FromHours(23));
        Assert.True(restarted.RequestQuest("pilot", giver.Id).IsOffer);
    }

    [Fact]
    public async Task DeadlineAndAbandonmentCannotBeBypassedWithAnOldOfferOrLateTurnIn()
    {
        var clock = new ProbulatorTestClock();
        var (world, _, giver) = await DeliveryWorld(clock);
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        var accepted = await world.AcceptQuestAsync("pilot", offer.Id);
        var recipient = accepted.Quest.DeliveryRecipient!;
        await world.TeleportAsync("pilot", new(recipient.Position.X, recipient.Position.Y, true));
        clock.Advance(TimeSpan.FromMinutes(offer.DeliveryMinutes!.Value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteQuestAsync("pilot", new(offer.Id, recipient.Id)));
        Assert.Equal("failed", world.GetPrivateState("pilot").Quests!.Single().Status);
        clock.Advance(TimeSpan.FromDays(1));
        await world.TeleportAsync("pilot", new(giver.Position.X, giver.Position.Y, true));
        var next = world.RequestQuest("pilot", giver.Id).Quest;
        await world.AcceptQuestAsync("pilot", next.Id);
        var abandoned = await world.AbandonQuestAsync("pilot", next.Id);
        Assert.Equal("failed", abandoned.Quest.Status);
        Assert.Throws<InvalidOperationException>(() => world.RequestQuest("pilot", giver.Id));
    }

    [Fact]
    public async Task FoodDeliveryCannotBeAcceptedRemotelyAndOnlyOneConcurrentAcceptanceSucceeds()
    {
        var (world, _, giver) = await DeliveryWorld(new ProbulatorTestClock());
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        await world.TeleportAsync("pilot", new(30, 30, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AcceptQuestAsync("pilot", offer.Id));
        await world.TeleportAsync("pilot", new(0, 0, true));
        offer = world.RequestQuest("pilot", giver.Id).Quest;
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await world.AcceptQuestAsync("pilot", offer.Id); return true; }
            catch (InvalidOperationException) { return false; }
        }));
        Assert.Single(results, success => success);
        Assert.Single(world.GetPrivateState("pilot").Quests!);
        Assert.Single(world.GetPrivateState("pilot").Inventory.Items, i => i.ItemType.StartsWith("quest:food:"));
    }

    [Fact]
    public async Task ExQuestRequiresTauntingTheNamedNpcThenReturningToTheGiver()
    {
        var clock = new ProbulatorTestClock();
        var (world, _, npc) = await ImpressionWorld(clock);
        var ex = npc with { Subtype = "resident", IsTestCharacter = false };
        ImpressionActors(world)[ex.Id] = ex;
        QuestState? offer = null;
        ActorState? giver = null;
        for (var index = 0; index < 100; index++)
        {
            giver = ex with { Id = $"ex-quest-giver:{index}", Name = $"Quest Giver {index}", IsQuestGiver = true, Position = ex.Position with { X = 0 } };
            ImpressionActors(world)[giver.Id] = giver;
            var proposed = world.RequestQuest("pilot", giver.Id).Quest;
            if (proposed.Kind == "tauntEx") { offer = proposed; break; }
            ImpressionActors(world).TryRemove(giver.Id, out _);
        }
        Assert.NotNull(offer);
        Assert.Equal(ex.Id, offer.TargetActorId);
        Assert.Contains("ex-", offer.Description);
        await world.AcceptQuestAsync("pilot", offer.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteQuestAsync("pilot", new(offer.Id, giver!.Id)));
        await world.TauntAsync("pilot", giver!.Id);
        Assert.Equal("active", world.GetPrivateState("pilot").Quests!.Single().Status);
        clock.Advance(3);
        await world.TauntAsync("pilot", ex.Id);
        Assert.Equal("ready", world.GetPrivateState("pilot").Quests!.Single().Status);
        Assert.Contains(world.TakeQuestNotices(), n => n.Message.Contains("Return to"));
        var result = await world.CompleteQuestAsync("pilot", new(offer.Id, giver.Id));
        Assert.Equal("completed", result.Quest.Status);
    }

    [Fact]
    public async Task DeliveryDogsAmbushEveryTenSecondsPursueOnlyTheirCourierAndDisperseWhenTheJobEnds()
    {
        var clock = new ProbulatorTestClock();
        var (world, _, giver) = await DeliveryWorld(clock);
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        await world.AcceptQuestAsync("pilot", offer.Id);
        clock.Advance(9.9);
        await world.AdvanceFoodDeliveriesAsync();
        Assert.DoesNotContain(ImpressionActors(world).Keys, id => id.StartsWith("delivery-dog:"));
        clock.Advance(.1);
        await world.AdvanceFoodDeliveriesAsync();
        var dog = Assert.Single(ImpressionActors(world).Values, a => a.Id.StartsWith("delivery-dog:"));
        var pilot = world.CreateSnapshot().Players.Single(p => p.Id == "pilot");
        var before = dog.Position.Distance2D(pilot.Position);
        world.AdvanceDeliveryDogs(TimeSpan.FromSeconds(3));
        Assert.True(ImpressionActors(world)[dog.Id].Position.Distance2D(pilot.Position) < before);
        Assert.Equal(-2, ImpressionRating(world, "pilot", dog.Id));
        Assert.Contains(world.TakeDeliveryDogRelationships(), r => r.ActorId == dog.Id && r.PlayerId == "pilot" && r.FriendRating == -2);
        await world.JoinAsync("bystander", "Bystander");
        await world.SetGodModeAsync("bystander", true);
        await world.TeleportAsync("bystander", new(0, 0, true));
        var attacks = await world.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.Contains(attacks.Combat, c => c.AttackerId == dog.Id && c.TargetId == "pilot");
        Assert.DoesNotContain(attacks.Combat, c => c.AttackerId == dog.Id && c.TargetId == "bystander");
        for (var wave = 2; wave <= 8; wave++) { clock.Advance(10); await world.AdvanceFoodDeliveriesAsync(); }
        Assert.Equal(3, ImpressionActors(world).Keys.Count(id => id.StartsWith("delivery-dog:")));
        Assert.Contains(dog.Id, world.TakeDeliveryActorRemovals());
        var activeDogIds = ImpressionActors(world).Keys.Where(id => id.StartsWith("delivery-dog:")).ToArray();
        await world.AbandonQuestAsync("pilot", offer.Id);
        Assert.DoesNotContain(ImpressionActors(world).Keys, id => id.StartsWith("delivery-dog:"));
        Assert.Equal(activeDogIds.Order(), world.TakeDeliveryActorRemovals().Order());
        clock.Advance(20);
        await world.AdvanceFoodDeliveriesAsync();
        Assert.DoesNotContain(ImpressionActors(world).Keys, id => id.StartsWith("delivery-dog:"));
    }

    [Theory]
    [InlineData(4.99, false)]
    [InlineData(5, false)]
    [InlineData(5.01, true)]
    public async Task FoodHandlingUsesActualAuthoritativeSpeedAndDoesNotFailUntilHandoff(double mph, bool damaged)
    {
        var (world, _, giver) = await DeliveryWorld(new ProbulatorTestClock());
        await world.UpdateMovementConfigurationAsync("pilot", new(mph / 5, 50, new Dictionary<TerrainType, double>(), new Dictionary<TravelMode, double>()));
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        Assert.Contains("5 mph", offer.Description);
        Assert.Contains(world.TakeQuestDialogue(), c => c.Message.Contains("Don't shake it up"));
        await world.AcceptQuestAsync("pilot", offer.Id);
        var moved = await world.MoveAsync("pilot", new(1, 0, 1));
        Assert.True(moved!.Moved);
        Assert.Equal(mph * .44704, moved.Player.SpeedMetersPerSecond, 5);
        var active = world.GetPrivateState("pilot").Quests!.Single();
        Assert.Equal(damaged, active.FoodDamaged);
        Assert.Equal("active", active.Status);
        PositionCourierForQuestTest(world, active.DeliveryRecipient!.Position);
        var result = await world.CompleteQuestAsync("pilot", new(active.Id, active.DestinationActorId!));
        Assert.Equal(damaged ? "failed" : "completed", result.Quest.Status);
    }

    [Fact]
    public async Task TeleportDamagePersistsAndTheRecipientRefusesPaymentBecomesHostileAndAttacks()
    {
        var clock = new ProbulatorTestClock();
        var (world, store, giver) = await DeliveryWorld(clock);
        var offer = world.RequestQuest("pilot", giver.Id).Quest;
        var accepted = await world.AcceptQuestAsync("pilot", offer.Id);
        var recipient = accepted.Quest.DeliveryRecipient!;
        await world.TeleportAsync("pilot", new(recipient.Position.X, recipient.Position.Y, true));
        var damaged = world.GetPrivateState("pilot").Quests!.Single();
        Assert.True(damaged.FoodDamaged);
        Assert.Equal("active", damaged.Status);
        await world.LeaveAsync("pilot");
        var restarted = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store, clock);
        await restarted.InitializeAsync();
        await restarted.JoinAsync("pilot", "Pilot");
        Assert.True(restarted.GetPrivateState("pilot").Quests!.Single().FoodDamaged);
        var balance = restarted.CreateSnapshot().Players.Single(p => p.Id == "pilot").WalletCents;
        var refused = await restarted.CompleteQuestAsync("pilot", new(offer.Id, recipient.Id));
        Assert.Equal("failed", refused.Quest.Status);
        Assert.True(refused.Quest.DeliveryRefused);
        Assert.Equal(balance, refused.Player.WalletCents);
        Assert.True(ImpressionRating(restarted, "pilot", recipient.Id) <= -2);
        Assert.Contains(restarted.TakeQuestDialogue(), c => c.PlayerId == recipient.Id && (c.Message.Contains("food") || c.Message.Contains("mess")));
        var attack = await restarted.AdvanceHostilityAsync(TimeSpan.FromSeconds(.5));
        Assert.Contains(attack.Combat, c => c.AttackerId == recipient.Id && c.TargetId == "pilot");
        await restarted.LeaveAsync("pilot");
        var again = new RealityWorld(world.Configuration, new DeterministicWorldGenerator(new FixedGeographicProvider()), new FixedWeatherProvider(), store, clock);
        await again.InitializeAsync();
        await again.JoinAsync("pilot", "Pilot");
        Assert.True(ImpressionActors(again).ContainsKey(recipient.Id));
        Assert.True(ImpressionRating(again, "pilot", recipient.Id) <= -2);
    }

    [Theory]
    [InlineData("restaurant", "chinese")]
    [InlineData("fast_food", "pizza")]
    public async Task ImportedFoodBusinessesOfferTradeAndDeliveryAndDogsAlsoPursueIndoors(string amenity, string cuisine)
    {
        var configuration = new RealityConfiguration("food-store", "Food Store", 71, new GeographicArea(new GeoCoordinate(45.5, -122.5), 500));
        var building = Building("food-building", configuration.Area.Region, 20, 20) with
        {
            Properties = new Dictionary<string, string> { ["building"] = "retail", ["amenity"] = amenity, ["cuisine"] = cuisine, ["name"] = "Takeout Kitchen" }
        };
        var store = new SqliteRealityStore(Path.Combine(_directory, "food-business.db"));
        await store.InitializeAsync(configuration);
        var clock = new ProbulatorTestClock();
        var world = new RealityWorld(configuration, new DeterministicWorldGenerator(new FixedGeographicProvider(building)), new FixedWeatherProvider(), store, clock);
        await world.InitializeAsync();
        await world.JoinAsync("pilot", "Pilot");
        await world.SetGodModeAsync("pilot", true);
        var door = world.CreateSnapshot().BaseEntities.Single(e => e.Kind == EntityKind.Door && e.Properties["buildingId"] == building.Id);
        var hours = world.GetDoorLockSchedule().Doors.Single(d => d.DoorId == door.Id).StoreHours!;
        clock.Advance(world.CurrentServerTime.Date.AddDays(1).AddHours(hours.OpenHour) - world.CurrentServerTime.DateTime);
        await world.TeleportAsync("pilot", new(door.Position.X, door.Position.Y, true));
        var entered = await world.EnterDungeonAsync("pilot", door.Id);
        Assert.True(entered.Dungeon.IsStore);
        var merchant = Assert.Single(entered.Dungeon.Actors, a => a.IsMerchant);
        Assert.True(merchant.IsQuestGiver);
        Assert.True(merchant.OffersFoodDelivery);
        PositionCourierForQuestTest(world, merchant.Position, entered.Dungeon.Id);
        Assert.Contains(world.RequestTrade("pilot", merchant.Id).Offers, o => o.ItemType == "food");
        var offer = world.RequestQuest("pilot", merchant.Id).Quest;
        await world.AcceptQuestAsync("pilot", offer.Id);
        clock.Advance(10);
        await world.AdvanceFoodDeliveriesAsync();
        var dog = Assert.Single(world.GetPrivateState("pilot").Dungeon!.Actors, a => a.Id.StartsWith("delivery-dog:"));
        Assert.Equal(entered.Dungeon.Id, dog.LocationId);
        Assert.Contains(world.AdvanceDeliveryDogs(TimeSpan.FromSeconds(.5)), a => a.Id == dog.Id);
        await world.AbandonQuestAsync("pilot", offer.Id);
        Assert.DoesNotContain(world.GetPrivateState("pilot").Dungeon!.Actors, a => a.Id == dog.Id);
        Assert.Contains(dog.Id, world.TakeDeliveryActorRemovals());
    }

    [Theory]
    [InlineData("amenity", "restaurant", true)]
    [InlineData("amenity", "fast_food", true)]
    [InlineData("amenity", "food_court", true)]
    [InlineData("shop", "deli", true)]
    [InlineData("takeaway", "only", true)]
    [InlineData("delivery", "yes", true)]
    [InlineData("shop", "supermarket", false)]
    [InlineData("amenity", "fuel", false)]
    public void FoodBusinessTagsRecognizePreparedFoodCounters(string key, string value, bool expected) =>
        Assert.Equal(expected, FoodBusinesses.OffersDelivery(new Dictionary<string, string> { [key] = value }));
}
