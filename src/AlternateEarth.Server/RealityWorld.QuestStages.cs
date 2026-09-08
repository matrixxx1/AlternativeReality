using System.Security.Cryptography;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private QuestState WithQuestStage(QuestState quest)
    {
        var id = quest.Status == "ready" ? quest.GiverId : quest.ObjectiveActorIds?.FirstOrDefault(_actors.ContainsKey) ?? quest.DestinationActorId ?? quest.TargetActorId ?? quest.GiverId;
        var actor = FindActor(quest.PlayerId, id);
        var entity = _baseEntities.GetValueOrDefault(id);
        var position = actor?.Position ?? entity?.Position ?? quest.DeliveryRecipient?.Position ?? quest.NextStagePosition;
        var location = actor?.LocationId ?? "outdoor";
        var name = actor?.Name ?? (id == quest.GiverId ? quest.GiverName : quest.TargetName ?? quest.DestinationName) ?? "Next objective";
        if (quest.Kind == "item" && InventoryQuantity(quest.PlayerId, quest.RequiredItemType!) < quest.RequiredQuantity)
        {
            var merchant = _actors.Values.Where(a => a.IsMerchant && a.LocationId == "outdoor")
                .OrderBy(a => a.Position.Distance2D(_players[quest.PlayerId].Position)).FirstOrDefault();
            position = merchant?.Position; location = "outdoor"; name = "Search merchants and treasure for " + DisplayItem(quest.RequiredItemType!);
        }
        if (quest.Kind == "vandalizeMailboxes" && quest.Status == "active")
        {
            var mailbox = _baseEntities.Values.Where(e => e.Properties.GetValueOrDefault("subtype") == "mailbox")
                .OrderBy(e => e.Position.Distance2D(_players[quest.PlayerId].Position)).FirstOrDefault();
            position = mailbox?.Position; name = "Next mailbox";
        }
        if (location != "outdoor" && _players[quest.PlayerId].LocationId != location && _dungeons.TryGetValue(location, out var interior))
        {
            var door = _baseEntities.Values.FirstOrDefault(e => e.Kind == EntityKind.Door && e.Properties.GetValueOrDefault("buildingId") == interior.BuildingId);
            position = door?.Position ?? _baseEntities.GetValueOrDefault(interior.BuildingId)?.Position ?? position;
            name = "Entrance for " + name; location = "outdoor";
        }
        return quest with { NextStagePosition = position, NextStageName = name, NextStageLocationId = location };
    }

    private QuestState PrepareQuestOffer(QuestState quest)
    {
        quest = WithQuestStage(quest);
        var distance = quest.NextStagePosition?.Distance2D(_players[quest.PlayerId].Position) ?? 0;
        // Allow a slow round trip, searching/combat, and substantial detours. Time starts on acceptance.
        var minutes = quest.Kind == "foodDelivery" ? quest.DeliveryMinutes!.Value : Math.Max(45, (int)Math.Ceiling(distance * 4 / 60 + 30));
        string[] supplies = ["wood", "metal", "kindling"];
        string[] consumables = ["food", "water", "energyDrink"];
        var rewards = new[] { InventoryStack(RandomNumberGenerator.GetInt32(2) == 0 ? "bullet" : "arrow", RandomNumberGenerator.GetInt32(5, 16)),
            InventoryStack(supplies[RandomNumberGenerator.GetInt32(supplies.Length)], RandomNumberGenerator.GetInt32(1, 4)),
            InventoryStack(consumables[RandomNumberGenerator.GetInt32(consumables.Length)], 1) };
        return quest with { DeliveryMinutes = minutes, RewardItems = rewards };
    }

    private async Task GrantQuestItemsAsync(QuestState quest, CancellationToken token)
    {
        var rewards = quest.RewardItems ?? Array.Empty<ItemStack>();
        if (rewards.Count == 0) return;
        if (CanAddToBackpack(quest.PlayerId, rewards, out _))
        {
            foreach (var item in rewards) AddInventory(quest.PlayerId, item.ItemType, item.Quantity);
        }
        else
        {
            var player = _players[quest.PlayerId];
            var drop = new LootDropState($"quest-reward:{Guid.NewGuid():N}", player.Position, player.LocationId, 0, rewards, DateTimeOffset.UtcNow.AddHours(1));
            _loot[drop.Id] = drop; _deathDropAnnouncements.Enqueue(drop);
            _questNotices.Enqueue((quest.PlayerId, "Your quest reward items are beside you: make room and collect them within one hour."));
        }
        await SaveInventoryAsync(quest.PlayerId, token);
    }

    private static string InversionQuestId(ActorState actor)
    {
        if (actor.Id.Contains(":manual:")) return "inversion:" + actor.Id.Split(':')[^1];
        if (actor.Subtype == "raptor") return "inversion:" + string.Join(":", actor.Id.Split(':').Take(2));
        return "inversion:" + actor.Id;
    }

    private async Task AdvanceQuestStagesAsync(string playerId, CancellationToken token)
    {
        var now = _probulatorClock.GetUtcNow();
        var player = _players[playerId];
        foreach (var actor in _actors.Values.Where(a => !IsIncursionActor(a) && a.EventEndsAtUtc > now && a.Position.Distance2D(player.Position) <= 500 && player.LocationId == "outdoor"))
        {
            var id = InversionQuestId(actor);
            if (_quests.ContainsKey((playerId, id))) continue;
            var members = _actors.Values.Where(a => a.EventEndsAtUtc > now && InversionQuestId(a) == id).Select(a => a.Id).ToArray();
            var quest = PrepareQuestOffer(new QuestState(id, playerId, actor.Id, "Reality inversion", "inversion", "active",
                actor.EventName ?? "Reality inversion", "Survive until this inversion ends, or defeat its creature. Stay alive; you may retreat to safety.", 10000,
                TargetActorId: actor.Id, TargetName: actor.Name, DeadlineUtc: actor.EventEndsAtUtc, NextStagePosition: actor.Position, ObjectiveActorIds: members));
            _quests[(playerId, id)] = quest;
            await _store.SaveQuestAsync(Configuration.Id, quest, token);
            _questNotices.Enqueue((playerId, "New quest: " + quest.Title));
        }
        foreach (var quest in _quests.Values.Where(q => q.PlayerId == playerId && q.Status is "active" or "ready" && q.Kind != "foodDelivery").ToArray())
        {
            if (quest.Kind == "inversion" && (quest.DeadlineUtc <= now || !(quest.ObjectiveActorIds ?? new[] { quest.TargetActorId! }).Any(_actors.ContainsKey)))
            {
                var completed = quest with { Status = "completed" };
                _quests[(playerId, quest.Id)] = completed;
                await GrantQuestItemsAsync(quest, token);
                var current = _players[playerId];
                await SavePlayerAsync(current with { WalletCents = current.WalletCents + quest.RewardCents, Version = current.Version + 1 }, token);
                await _store.SaveQuestAsync(Configuration.Id, completed, token);
                await AwardExperienceAsync(playerId, 75, "Survived " + quest.Title, "quest:" + quest.Id, cancellationToken: token);
                _questNotices.Enqueue((playerId, "Quest complete: " + quest.Title + ". Cash and item rewards awarded."));
            }
            else if (quest.DeadlineUtc is null)
            {
                var migrated = PrepareQuestOffer(quest);
                migrated = migrated with { DeadlineUtc = now.AddMinutes(migrated.DeliveryMinutes!.Value) };
                _quests[(playerId, quest.Id)] = migrated;
                await _store.SaveQuestAsync(Configuration.Id, migrated, token);
            }
            else if (quest.DeadlineUtc <= now)
            {
                if (quest.Kind == "courier") RemoveInventory(playerId, QuestPackageItem(quest), 1);
                if (quest.Kind == "drugDelivery") RemoveInventory(playerId, QuestDrugItem(quest), 1);
                if (quest.Kind == "missingPet") RemoveInventory(playerId, QuestPetItem(quest), 1);
                var failed = quest with { Status = "failed", FailedAtUtc = now };
                _quests[(playerId, quest.Id)] = failed;
                await SaveInventoryAsync(playerId, token);
                await _store.SaveQuestAsync(Configuration.Id, failed, token);
                _questNotices.Enqueue((playerId, "Quest expired: " + quest.Title));
            }
        }
    }
}
