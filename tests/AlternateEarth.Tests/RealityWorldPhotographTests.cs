using System.Collections.Concurrent;
using System.Reflection;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    private static T PhotoField<T>(RealityWorld world, string name) =>
        (T)typeof(RealityWorld).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;
    private static ItemStack[] Prints(RealityWorld world, string id) => world.GetPrivateState(id).Inventory.Items.Where(i => i.Photograph is not null).ToArray();
    private static void GiveFilm(RealityWorld world, string id, int quantity) =>
        typeof(RealityWorld).GetMethod("AddInventory", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, [id, "film", quantity, null]);
    private static QuestState PhotoQuest(RealityWorld world, string playerId, string suffix = "1", string kind = "photo")
    {
        var player = world.GetRetroBattleUpdate(playerId).Player!;
        var clerk = new ActorState("photo-clerk", EntityKind.Npc, "clerk", "Disaster Clerk", player.Position, IsQuestGiver: true, IsMerchant: true);
        ImpressionActors(world)[clerk.Id] = clerk;
        var quest = new QuestState("photo-quest-" + suffix, playerId, clerk.Id, clerk.Name, "adventure:" + kind, "active", "Paperwork", "Bring prints", 10000,
            TargetActorId: clerk.Id, NextStagePosition: clerk.Position);
        PhotoField<ConcurrentDictionary<(string Player, string Quest), QuestState>>(world, "_quests")[(playerId, quest.Id)] = quest;
        return quest;
    }
    private static string PhotoSubject(RealityWorld world, string playerId, int number)
    {
        var player = world.GetRetroBattleUpdate(playerId).Player!;
        var id = "photo-subject-" + number;
        ImpressionActors(world)[id] = new ActorState(id, EntityKind.Npc, "robot", "Robot " + number, player.Position, EventName: "Mechanized Warrior");
        return id;
    }

    [Fact]
    public async Task PhotographsAreUniquePersistentItemsAndSoldPrintCannotBeSubmitted()
    {
        var (world, id, _, clock) = await RetroFixture();
        await world.SetEquipmentAsync(id, "weapon", "camera"); GiveFilm(world, id, 5);
        for (var n = 0; n < 3; n++) { await world.PhotographAsync(id, PhotoSubject(world, id, n)); clock.Advance(1); }
        Assert.Equal(3, Prints(world, id).Select(p => p.ItemType).Distinct().Count());
        var store = PhotoField<SqliteRealityStore>(world, "_store");
        var loaded = await store.LoadInventoryAsync(id);
        Assert.Equal(Prints(world, id), loaded.Items.Where(i => i.Photograph is not null).Select(i => i with { UnitWeightPounds = .01 }).ToArray());
        var quest = PhotoQuest(world, id);
        var print = Prints(world, id)[0];
        var quote = world.RequestTrade(id, quest.GiverId);
        Assert.True(Assert.Single(quote.BuyOffers!, o => o.ItemType == print.ItemType).UnitPriceCents > 0);
        var sale = new ConfirmTradeRequest(quest.GiverId, [], [new(print.ItemType, 1)]);
        await world.ConfirmTradeAsync(id, sale);
        Assert.Equal(2, Prints(world, id).Length);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ConfirmTradeAsync(id, sale));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AdventureActionAsync(id, quest.Id, "Submit photographs"));
        await world.PhotographAsync(id, print.Photograph!.Evidence[6..]);
        var submitted = await world.AdventureActionAsync(id, quest.Id, "Submit photographs");
        Assert.Equal("ready", submitted.Quest.Status); Assert.Equal(3, submitted.Quest.Progress);
        Assert.Empty(Prints(world, id));
        Assert.DoesNotContain((await store.LoadInventoryAsync(id)).Items, i => i.Photograph is not null);
        Assert.Equal("ready", Assert.Single(await store.LoadQuestsAsync(world.Configuration.Id, id), q => q.Id == quest.Id).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AdventureActionAsync(id, quest.Id, "Submit photographs"));
    }

    [Fact]
    public async Task DuplicateShotsDoNotReplaceDistinctSubjectsAndDuplicateSaleLinesCannotPayTwice()
    {
        var (world, id, _, clock) = await RetroFixture();
        await world.SetEquipmentAsync(id, "weapon", "camera"); GiveFilm(world, id, 3);
        var subject = PhotoSubject(world, id, 1);
        for (var n = 0; n < 3; n++) { await world.PhotographAsync(id, subject); clock.Advance(1); }
        var quest = PhotoQuest(world, id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AdventureActionAsync(id, quest.Id, "Submit photographs"));
        var print = Prints(world, id)[0];
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.ConfirmTradeAsync(id, new(quest.GiverId, [], [new(print.ItemType, 1), new(print.ItemType, 1)])));
        Assert.Equal(3, Prints(world, id).Length);
    }

    [Fact]
    public async Task ConcurrentRedemptionAndSaleCannotSpendTheSamePrint()
    {
        var (world, id, _, clock) = await RetroFixture();
        await world.SetEquipmentAsync(id, "weapon", "camera"); GiveFilm(world, id, 3);
        for (var n = 0; n < 3; n++) { await world.PhotographAsync(id, PhotoSubject(world, id, n)); clock.Advance(1); }
        var quest = PhotoQuest(world, id); var item = Prints(world, id)[0].ItemType;
        async Task<bool> Attempt(Func<Task> action) { try { await action(); return true; } catch (InvalidOperationException) { return false; } }
        var outcomes = await Task.WhenAll(Attempt(() => world.AdventureActionAsync(id, quest.Id, "Submit photographs")),
            Attempt(() => world.ConfirmTradeAsync(id, new(quest.GiverId, [], [new(item, 1)]))));
        Assert.Single(outcomes, success => success);
    }

    [Fact]
    public async Task InvalidOrOversizedThumbnailAndOutOfRangeSubjectDoNotConsumeFilm()
    {
        var (world, id, _, _) = await RetroFixture();
        await world.SetEquipmentAsync(id, "weapon", "camera"); GiveFilm(world, id, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.PhotographAsync(id, null, thumbnailDataUrl: "data:image/svg+xml,<svg/>"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.PhotographAsync(id, null, thumbnailDataUrl: "data:image/png;base64," + new string('A', 44000)));
        var subject = PhotoSubject(world, id, 1); var actor = ImpressionActors(world)[subject];
        ImpressionActors(world)[subject] = actor with { Position = actor.Position with { X = actor.Position.X + 100 } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.PhotographAsync(id, subject));
        Assert.Single(world.GetPrivateState(id).Inventory.Items, i => i.ItemType == "film" && i.Quantity == 1);
        Assert.Empty(Prints(world, id));
    }

    [Fact]
    public async Task PhotographThumbnailIsStoredSeparatelyFromRoutineInventoryUpdates()
    {
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAIAAAABgCAIAAABaGO0eAAAAxElEQVR4nO3RQQ0AIAzAwClBBJrwrwEZe/SSCmhyc97VYrN+EA8AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2gEA0A4AgHYAALQDAKAdAADtAABoBwBAOwAA2n2ZccHwKLyDoQAAAABJRU5ErkJggg==";
        var (world, id, _, _) = await RetroFixture();
        await world.SetEquipmentAsync(id, "weapon", "camera"); GiveFilm(world, id, 1);
        await world.PhotographAsync(id, null, thumbnailDataUrl: "data:image/png;base64," + png);
        var print = Assert.Single(Prints(world, id));
        Assert.Equal("/api/photographs/" + print.ItemType[11..], print.Photograph!.ThumbnailUrl);
        var store = PhotoField<SqliteRealityStore>(world, "_store");
        Assert.Equal(Convert.FromBase64String(png), await store.LoadPhotographImageAsync(print.ItemType));
        Assert.DoesNotContain(png, System.Text.Json.JsonSerializer.Serialize(world.GetPrivateState(id), SharedJson.Options));
    }

    [Fact]
    public async Task InspectionNeedsBarrierWaterAndExitPrintsInsteadOfEventPictures()
    {
        var (world, id, _, clock) = await RetroFixture();
        await world.SetEquipmentAsync(id, "weapon", "camera"); GiveFilm(world, id, 3);
        var players = PhotoField<ConcurrentDictionary<string, PlayerState>>(world, "_players");
        var outdoor = players[id]; var position = outdoor.Position with { X = 3, Y = 3 };
        var dungeon = new DungeonState("photo-dungeon", "test-building", 100, 100, [], [], position with { X = 80, Y = 80 }, [], [], [],
            Barriers: [new("barrier", 3, 3, 1, 1, "fire")], WaterAreas: [new(30, 30, 10, 10, true)]);
        PhotoField<ConcurrentDictionary<string, DungeonState>>(world, "_dungeons")[dungeon.Id] = dungeon;
        foreach (var point in new[] { position, position with { X = 35, Y = 35 }, dungeon.Exit })
        {
            players[id] = outdoor with { LocationId = dungeon.Id, Position = point };
            await world.PhotographAsync(id, null); clock.Advance(1);
        }
        Assert.Equal(new[] { "inspection:barrier", "inspection:exit", "inspection:water" }, Prints(world, id).Select(p => p.Photograph!.Evidence).Order().ToArray());
        players[id] = outdoor;
        var eventQuest = PhotoQuest(world, id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AdventureActionAsync(id, eventQuest.Id, "Submit photographs"));
        var quest = PhotoQuest(world, id, "inspection", "inspection");
        Assert.Equal("ready", (await world.AdventureActionAsync(id, quest.Id, "Submit inspection")).Quest.Status);
        Assert.Empty(Prints(world, id));
    }

    [Fact]
    public async Task PhotographMetadataSurvivesStorageLootAndShopPersistence()
    {
        var (world, id, _, _) = await RetroFixture();
        await world.SetEquipmentAsync(id, "weapon", "camera"); GiveFilm(world, id, 1);
        await world.PhotographAsync(id, PhotoSubject(world, id, 1));
        var print = Assert.Single(Prints(world, id)); var store = PhotoField<SqliteRealityStore>(world, "_store");
        await store.SaveInventoryAsync(new("photo-storage", [print]));
        Assert.Equal(print.Photograph, Assert.Single((await store.LoadInventoryAsync("photo-storage")).Items).Photograph);
        var loot = new LootDropState("photo-loot", world.GetRetroBattleUpdate(id).Player!.Position, "outdoor", 0, [print], DateTimeOffset.UtcNow.AddDays(1));
        await store.SavePersistentLootAsync(world.Configuration.Id, loot);
        Assert.Equal(print.Photograph, Assert.Single(Assert.Single(await store.LoadPersistentLootAsync(world.Configuration.Id), l => l.Id == loot.Id).Items).Photograph);
        await store.SaveHomeShopListingAsync("retro-account", world.Configuration.Id, new(print.ItemType, 1, 100, null, print.Photograph));
        Assert.Equal(print.Photograph, Assert.Single(await store.LoadHomeShopListingsAsync("retro-account", world.Configuration.Id)).Photograph);
        PhotoField<ConcurrentDictionary<string, PhotographState>>(world, "_photographs").Clear();
        await world.JoinAsync(id, "Retro", "retro-account");
        Assert.Equal(print.Photograph, Assert.Single(Prints(world, id)).Photograph);
    }
}
