using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static async Task<AiNpcDialogueApplied> AskForAiQuest(RealityWorld world, PlayerState player, ActorState npc, string action, string? subject = null)
    {
        var id = Guid.NewGuid().ToString();
        world.BeginAiNpcDialogue(player.Id, new(npc.Id, "Can you give me a quest?", id));
        return await world.ApplyAiNpcDialogueAsync(player.Id, npc.Id, id,
            new("Try {quest_title}! {objective} {location} Pays {reward} within {time_limit}.", "request_quest", "offer_quest", 0, false, "quest", [], QuestRequest: new(true, "The Great Missing Thing", action, subject)));
    }

    [Theory]
    [InlineData("find_item")]
    [InlineData("collect")]
    public async Task AiQuestSearchRequiresLocationAndAwardsOnlyOnce(string action)
    {
        var (world, player, store) = await CreateProbulatorTestWorld();
        var npc = world.PlaceTestCharacter(player.Id, new("npc", 1, 0)).Actor! with { AiDialogueEnabled = true };
        ImpressionActors(world)[npc.Id] = npc;
        var reply = await AskForAiQuest(world, player, npc, action, "metal");
        var offered = Assert.IsType<QuestInteraction>(reply.QuestInteraction).Quest;
        Assert.DoesNotContain("{", reply.Result.Dialogue);
        Assert.Equal(10, offered.DeliveryMinutes);
        Assert.Equal(3000, offered.RewardCents);
        var accepted = await world.AcceptQuestAsync(player.Id, offered.Id);
        Assert.Equal(TimeSpan.FromMinutes(10), accepted.Quest.DeadlineUtc - accepted.Quest.AiQuest!.AcceptedAtUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SearchAiQuestAsync(player.Id, offered.Id));
        var spot = offered.AiQuest!.Position;
        await world.TeleportAsync(player.Id, new(spot.X, spot.Y, true));
        var found = await world.SearchAiQuestAsync(player.Id, offered.Id);
        Assert.Equal("ready", found.Quest.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SearchAiQuestAsync(player.Id, offered.Id));
        await world.TeleportAsync(player.Id, new(npc.Position.X, npc.Position.Y, true));
        var before = world.GetPrivateState(player.Id).Inventory.Items.First(i => i.ItemType == offered.RequiredItemType).Quantity;
        Assert.Equal(offered.RequiredQuantity, before);
        var completed = await world.CompleteQuestAsync(player.Id, new(offered.Id, npc.Id));
        Assert.Equal("completed", completed.Quest.Status);
        Assert.DoesNotContain(world.GetPrivateState(player.Id).Inventory.Items, i => i.ItemType == offered.RequiredItemType);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteQuestAsync(player.Id, new(offered.Id, npc.Id)));
        Assert.Equal("completed", (await store.LoadQuestsAsync(world.Configuration.Id, player.Id)).Single(q => q.Id == offered.Id).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AiQuestExpiresAtTenMinutesEvenWhenReady(bool ready)
    {
        var clock = new ProbulatorTestClock();
        var (world, player, store) = await CreateProbulatorTestWorld(clock: clock);
        var npc = world.PlaceTestCharacter(player.Id, new("npc", 1, 0)).Actor! with { AiDialogueEnabled = true };
        ImpressionActors(world)[npc.Id] = npc;
        var offer = (await AskForAiQuest(world, player, npc, "find_item")).QuestInteraction!.Quest;
        await world.AcceptQuestAsync(player.Id, offer.Id);
        if (ready)
        {
            await world.TeleportAsync(player.Id, new(offer.AiQuest!.Position.X, offer.AiQuest.Position.Y, true));
            await world.SearchAiQuestAsync(player.Id, offer.Id);
        }
        clock.Advance(599);
        await world.AdvanceFoodDeliveriesAsync();
        Assert.NotEqual("failed", (await store.LoadQuestsAsync(world.Configuration.Id, player.Id)).Single(q => q.Id == offer.Id).Status);
        clock.Advance(1);
        await world.AdvanceFoodDeliveriesAsync();
        Assert.Equal("failed", (await store.LoadQuestsAsync(world.Configuration.Id, player.Id)).Single(q => q.Id == offer.Id).Status);
        Assert.DoesNotContain(world.GetPrivateState(player.Id).Inventory.Items, i => i.ItemType == offer.RequiredItemType);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.SearchAiQuestAsync(player.Id, offer.Id));
    }

    [Fact]
    public async Task AiQuestRejectsUnknownActionsAndDoesNotDuplicateAnActiveQuest()
    {
        var (world, player, _) = await CreateProbulatorTestWorld();
        var npc = world.PlaceTestCharacter(player.Id, new("npc", 1, 0)).Actor! with { AiDialogueEnabled = true };
        ImpressionActors(world)[npc.Id] = npc;
        Assert.Null((await AskForAiQuest(world, player, npc, "give_money")).QuestInteraction);
        var offer = (await AskForAiQuest(world, player, npc, "collect")).QuestInteraction!.Quest;
        await world.AcceptQuestAsync(player.Id, offer.Id);
        var again = (await AskForAiQuest(world, player, npc, "collect")).QuestInteraction!;
        Assert.Equal(offer.Id, again.Quest.Id);
        Assert.False(again.IsOffer);
    }

    [Fact]
    public async Task AiHistoryPersistsPaginatesDeduplicatesAndIsolatesPlayerNpcPairs()
    {
        var (world, player, store) = await CreateProbulatorTestWorld();
        for (var i = 0; i < 65; i++)
            await store.SaveNpcDialogueMessageAsync(world.Configuration.Id, player.Id, "npc", i.ToString(), i % 2 == 0 ? "player" : "npc", "Message " + i);
        await store.SaveNpcDialogueMessageAsync(world.Configuration.Id, player.Id, "npc", "64", "player", "duplicate");
        await store.SaveNpcDialogueMessageAsync(world.Configuration.Id, "other-player", "npc", "1", "player", "private");
        await store.SaveNpcDialogueMessageAsync(world.Configuration.Id, player.Id, "other-npc", "1", "npc", "private");
        var latest = await store.LoadNpcDialogueHistoryAsync(world.Configuration.Id, player.Id, "npc");
        Assert.Equal(50, latest.Messages.Count);
        Assert.Equal("Message 15", latest.Messages[0].Text);
        Assert.Equal("Message 64", latest.Messages[^1].Text);
        var older = await store.LoadNpcDialogueHistoryAsync(world.Configuration.Id, player.Id, "npc", latest.BeforeSequence);
        Assert.Equal(15, older.Messages.Count);
        Assert.Null(older.BeforeSequence);
        Assert.Equal("Message 0", older.Messages[0].Text);
    }

    [Fact]
    public async Task AiHistoryStoresFilledQuestTextAndRejectsReplayedReply()
    {
        var (world, player, _) = await CreateProbulatorTestWorld();
        var npc = world.PlaceTestCharacter(player.Id, new("npc", 1, 0)).Actor! with { AiDialogueEnabled = true };
        ImpressionActors(world)[npc.Id] = npc;
        var id = Guid.NewGuid().ToString();
        world.BeginAiNpcDialogue(player.Id, new(npc.Id, "A job please", id));
        await world.SaveAiNpcPlayerMessageAsync(player.Id, npc.Id, id, "A job please");
        var proposed = new NpcDialogueModelResult("{objective}", "request_quest", "offer_quest", .01, false, "quest", [], QuestRequest: new(true, "Lost property", "find_item"));
        var result = await world.ApplyAiNpcDialogueAsync(player.Id, npc.Id, id, proposed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ApplyAiNpcDialogueAsync(player.Id, npc.Id, id, proposed));
        world.EndAiNpcDialogue(player.Id, npc.Id);
        var page = await world.LoadAiNpcHistoryAsync(player.Id, npc.Id);
        Assert.Equal(2, page.Messages.Count);
        Assert.Equal("A job please", page.Messages[0].Text);
        Assert.Equal(result.Result.Dialogue, page.Messages[1].Text);
        Assert.DoesNotContain("{objective}", page.Messages[1].Text);
    }
    [Theory]
    [InlineData("find_pet")]
    [InlineData("taunt")]
    [InlineData("photograph")]
    [InlineData("hunt")]
    public async Task AiQuestUsesSpecificActorAndExistingObjectiveAction(string action)
    {
        var clock = new ProbulatorTestClock();
        var (world, player, _) = await CreateProbulatorTestWorld(clock: clock);
        await world.SetTravelModeAsync(player.Id, TravelMode.Walk);
        var actors = ImpressionActors(world); actors.Clear();
        var npc = new ActorState("giver", EntityKind.Npc, "resident", "Quest giver", player.Position with { X = 1 }, AiDialogueEnabled: true);
        var animal = action is "find_pet" or "hunt";
        var target = new ActorState("target", animal ? EntityKind.Animal : EntityKind.Npc, action == "find_pet" ? "dog" : action == "hunt" ? "rabbit" : "resident", "Target", player.Position with { X = 2 });
        actors[npc.Id] = npc; actors[target.Id] = target;
        if (action == "photograph")
        {
            typeof(RealityWorld).GetMethod("AddInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [player.Id, "camera", 1, null]);
            await world.SetEquipmentAsync(player.Id, "weapon", "camera");
            await world.PhotographAsync(player.Id, target.Id);
            clock.Advance(1);
        }
        var offer = (await AskForAiQuest(world, player, npc, action)).QuestInteraction!.Quest;
        Assert.Equal(target.Id, offer.TargetActorId);
        await world.AcceptQuestAsync(player.Id, offer.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteQuestAsync(player.Id, new(offer.Id, npc.Id)));
        switch (action)
        {
            case "find_pet": await world.CaptureQuestPetAsync(player.Id, target.Id); break;
            case "taunt": await world.TauntAsync(player.Id, target.Id); break;
            case "photograph": await world.PhotographAsync(player.Id, target.Id); break;
            case "hunt": await (Task)typeof(RealityWorld).GetMethod("RecordQuestKillAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [player.Id, target, CancellationToken.None])!; break;
        }
        Assert.True(world.RequestQuest(player.Id, npc.Id).CanComplete);
        Assert.Equal("completed", (await world.CompleteQuestAsync(player.Id, new(offer.Id, npc.Id))).Quest.Status);
    }

    [Theory]
    [InlineData("chop")]
    [InlineData("vandalize")]
    public async Task AiQuestUsesSpecificWorldObject(string action)
    {
        var (world, player, _) = await CreateProbulatorTestWorld();
        await world.SetTravelModeAsync(player.Id, TravelMode.Walk);
        var actors = ImpressionActors(world); actors.Clear();
        var npc = new ActorState("giver", EntityKind.Npc, "resident", "Quest giver", player.Position with { X = 1 }, AiDialogueEnabled: true);
        actors[npc.Id] = npc;
        var entities = PhotoField<System.Collections.Concurrent.ConcurrentDictionary<string, CanonicalEntity>>(world, "_baseEntities");
        entities.Clear();
        entities["objective"] = new("objective", action == "chop" ? EntityKind.Tree : EntityKind.Vehicle, player.Position with { X = 2 }, [], new Dictionary<string, string>());
        typeof(RealityWorld).GetMethod("AddInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [player.Id, "sword", 1, null]);
        await world.SetEquipmentAsync(player.Id, "weapon", "sword");
        var offer = (await AskForAiQuest(world, player, npc, action)).QuestInteraction!.Quest;
        Assert.Equal("objective", offer.TargetActorId);
        await world.AcceptQuestAsync(player.Id, offer.Id);
        if (action == "chop") await world.ChopVegetationAsync(player.Id, "objective");
        else await world.AttackWorldObjectAsync(player.Id, "objective");
        Assert.True(world.RequestQuest(player.Id, npc.Id).CanComplete);
        Assert.Equal("completed", (await world.CompleteQuestAsync(player.Id, new(offer.Id, npc.Id))).Quest.Status);
    }

}
