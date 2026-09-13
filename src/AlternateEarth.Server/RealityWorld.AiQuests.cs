using System.Globalization;
using System.Text.RegularExpressions;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static readonly string[] AiQuestActions = ["find_pet", "find_item", "collect", "photograph", "taunt", "vandalize", "hunt", "chop"];

    private (string Dialogue, QuestInteraction? Offer) CreateAiQuest(string playerId, ActorState giver, string interactionId, NpcDialogueModelResult result)
    {
        if (!result.UsedAi || result.QuestRequest is not { Requested: true } request) return (result.Dialogue, null);
        var existing = _quests.Values.FirstOrDefault(q => q.PlayerId == playerId && q.GiverId == giver.Id && q.Status is "active" or "ready");
        if (existing is not null) return ($"Please finish {existing.Title} first. {existing.Description}", new(existing, false, QuestCanComplete(playerId, existing, giver.Id), giver.Id));
        if (giver.LocationId != "outdoor" || !AiQuestActions.Contains(request.Action) ||
            _quests.Values.Count(q => q.PlayerId == playerId && q.AiQuest is not null && q.Status is "active" or "ready") >= 3)
            return ("I don't have a suitable mini quest for you right now. Try another request outside, or finish your current quests.", null);

        var action = request.Action!;
        var title = (request.Title ?? "A small favor").Trim();
        if (title.Length is < 1 or > 80 || title.Any(char.IsControl) || title.Contains('{') || title.Contains('}')) title = "A small favor";
        var now = _probulatorClock.GetUtcNow();
        string? targetId = null, item = null;
        var quantity = 1;
        var targetName = "marked search spot";
        var position = giver.Position;
        var kind = action;
        var reserved = _quests.Values.Where(q => q.Status is "active" or "ready").Select(q => q.TargetActorId).ToHashSet();
        bool Reachable(WorldPosition point) => point.Distance2D(giver.Position) <= 120 && Navigation.CanTraverse(giver.Position, point);
        if (action is "find_pet" or "hunt" or "taunt" or "photograph")
        {
            var target = _actors.Values.Where(a => a.Id != giver.Id && a.HealthHearts > 0 && a.LocationId == "outdoor" && !reserved.Contains(a.Id) &&
                (action == "find_pet" ? a.Kind == EntityKind.Animal && a.Subtype is "dog" or "cat" :
                 action == "hunt" ? a.Kind == EntityKind.Animal && a.Subtype is "rabbit" or "chicken" or "deer" :
                 action == "taunt" ? a.Kind == EntityKind.Npc && a.Subtype == "resident" : a.Kind is EntityKind.Npc or EntityKind.Animal))
                .OrderBy(a => a.Position.Distance2D(giver.Position)).Take(30).FirstOrDefault(a => Reachable(a.Position));
            if (target is null) return ("I can't find a suitable nearby target for that job. Ask me for a different kind of quest.", null);
            targetId = target.Id; targetName = target.Name; position = target.Position;
            kind = action switch { "find_pet" => "missingPet", "taunt" => "tauntEx", "photograph" => "aiPhoto", _ => "hunt" };
            if (action == "photograph" && !(_players[playerId].GodMode || InventoryQuantity(playerId, "camera") > 0 && InventoryQuantity(playerId, "film") > 0))
                return ("Bring a camera and film, then ask me for a photography quest.", null);
        }
        else if (action is "vandalize" or "chop")
        {
            if (action == "chop" && InventoryQuantity(playerId, "sword") == 0) return ("Bring a sword before asking for a chopping job.", null);
            var target = _baseEntities.Values.Where(e => !reserved.Contains(e.Id) && (action == "chop" ? e.Kind is EntityKind.Tree or EntityKind.Bush : e.Kind == EntityKind.Vehicle && e.Properties.GetValueOrDefault("occupied") != "true"))
                .OrderBy(e => e.Position.Distance2D(giver.Position)).Take(30).FirstOrDefault(e =>
                    e.Position.Distance2D(giver.Position) <= 120 && Enumerable.Range(0, 8).Any(i => Reachable(e.Position with { X = e.Position.X + Math.Cos(i * Math.PI / 4) * 2, Y = e.Position.Y + Math.Sin(i * Math.PI / 4) * 2 })));
            if (target is null) return ("There isn't a suitable nearby target. Ask me for another kind of quest.", null);
            targetId = target.Id; targetName = target.Kind.ToString().ToLowerInvariant(); position = target.Position;
            kind = action == "chop" ? "chop" : "vandalizeCar";
        }
        else
        {
            var point = Enumerable.Range(0, 48).Select(i => giver.Position with { X = giver.Position.X + Math.Cos(i * Math.PI / 12) * (15 + i / 24 * 15), Y = giver.Position.Y + Math.Sin(i * Math.PI / 12) * (15 + i / 24 * 15) }).Where(Reachable).Select(p => (WorldPosition?)p).FirstOrDefault();
            if (point is null) return ("I don't have an accessible search spot nearby. Try asking another resident.", null);
            position = point.Value;
            kind = "aiSearch";
            item = action == "collect" ? new[] { "wood", "metal", "kindling" }.Contains(request.Subject) ? request.Subject! : "wood" : $"quest:ai:{interactionId}";
            quantity = action == "collect" ? 3 : 1;
            targetName = action == "collect" ? DisplayItem(item) : "lost keepsake";
        }
        var clue = DirectionClue(giver.Position, position, targetName);
        var objective = action switch
        {
            "find_pet" => $"Find {targetName}, use Capture pet, and bring it back to {giver.Name}.",
            "find_item" or "collect" => $"Go to the marked spot and use Search marked spot in your quest log. Bring {quantity} × {targetName} to {giver.Name}.",
            "photograph" => $"Equip your camera and photograph {targetName}. Bring the new photograph to {giver.Name}.",
            "taunt" => $"Taunt {targetName}, then return to {giver.Name}.",
            "vandalize" => $"Damage the marked parked car, then return to {giver.Name}. Normal police consequences apply.",
            "chop" => $"Cut down the marked {targetName} with a sword, then return to {giver.Name}.",
            _ => $"Defeat {targetName}, then return to {giver.Name}."
        };
        var reward = action is "hunt" or "vandalize" ? 6000L : 3000L;
        var tokens = new Dictionary<string, string> { ["quest_title"] = title, ["target_name"] = targetName, ["item_name"] = targetName,
            ["quantity"] = quantity.ToString(), ["location"] = clue, ["reward"] = "$" + (reward / 100m).ToString("0.00", CultureInfo.InvariantCulture),
            ["time_limit"] = "10 minutes after accepting", ["objective"] = objective, ["giver_name"] = giver.Name };
        var text = Regex.Replace(result.Dialogue, @"\{([a-z_]+)\}", m => tokens.GetValueOrDefault(m.Groups[1].Value, ""));
        // Always include the authoritative objective even when the model omits placeholders.
        var description = $"{objective} {clue} Return before the ten-minute deadline to earn the reward.";
        var offer = new QuestState("ai:" + interactionId, playerId, giver.Id, giver.Name, kind, "offered", title, description, reward,
            RequiredItemType: item, RequiredQuantity: quantity, TargetActorId: targetId, TargetName: targetName, DestinationClue: clue,
            DeliveryMinutes: 10, NextStagePosition: position, NextStageName: targetName, NextStageLocationId: "outdoor", AiQuest: new(action, now, position));
        foreach (var old in _questOffers.Where(p => p.Key.Player == playerId && p.Value.AiQuest is not null && (p.Value.GiverId == giver.Id || p.Value.AiQuest.OfferedAtUtc.AddMinutes(10) <= now)).ToArray()) _questOffers.TryRemove(old.Key, out _);
        _questOffers[(playerId, offer.Id)] = offer;
        return ($"{text}\n{description} Reward: {tokens["reward"]}. Review the quest to accept or decline.", new(offer, true, false, giver.Id));
    }

    public async Task<QuestActionResult> SearchAiQuestAsync(string playerId, string questId, CancellationToken token = default)
    {
        await _deliveryLock.WaitAsync(token);
        try
        {
            await ExpireFoodDeliveriesCoreAsync(playerId, token);
            if (!_quests.TryGetValue((playerId, questId), out var quest) || quest.Kind != "aiSearch" || quest.Status != "active" || quest.AiQuest is null)
                throw new InvalidOperationException("That search is no longer active.");
            var player = _players[playerId];
            if (player.LocationId != "outdoor" || player.Position.Distance2D(quest.AiQuest.Position) > 4 || !Navigation.CanTraverse(player.Position, quest.AiQuest.Position))
                throw new InvalidOperationException("Move within four meters of the marked search spot.");
            if (!CanAddToBackpack(playerId, [InventoryStack(quest.RequiredItemType!, quest.RequiredQuantity)], out var error)) throw new InvalidOperationException(error);
            AddInventory(playerId, quest.RequiredItemType!, quest.RequiredQuantity);
            var ready = quest with { Status = "ready", Progress = quest.RequiredQuantity };
            try { await _store.SaveQuestAsync(Configuration.Id, ready, token, GetInventoryState(playerId)); }
            catch { RemoveInventory(playerId, quest.RequiredItemType!, quest.RequiredQuantity); throw; }
            _quests[(playerId, questId)] = ready;
            return new(GetPrivateState(playerId), player, ready, $"Found {quest.TargetName}. Return to {quest.GiverName} before time runs out.");
        }
        finally { _deliveryLock.Release(); }
    }

    private string? AiQuestPhotograph(string playerId, QuestState quest) => _photographs.FirstOrDefault(p => p.Value.TakenByPlayerId == playerId && p.Value.Evidence == "actor:" + quest.TargetActorId &&
        p.Value.TakenAtUtc >= quest.AiQuest?.AcceptedAtUtc && p.Value.TakenAtUtc < quest.DeadlineUtc && InventoryQuantity(playerId, p.Key) > 0).Key;
}
