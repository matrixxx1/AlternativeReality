using System.Collections.Concurrent;
using System.Security.Cryptography;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly SemaphoreSlim _deliveryLock = new(1, 1);
    private readonly ConcurrentQueue<(string PlayerId, string Message)> _questNotices = new();
    private readonly ConcurrentQueue<ChatMessage> _questDialogue = new();
    private readonly ConcurrentDictionary<string, int> _deliveryRefusals = new();
    private static string QuestFoodItem(QuestState quest) => $"quest:food:{quest.Id}";

    private void CheckDeliveryAvailability(string playerId, ActorState giver, bool allowCurrentGiver = false)
    {
        if (_dungeons.TryGetValue(giver.LocationId, out var interior) && interior.IsStore &&
            _baseEntities.TryGetValue(interior.BuildingId, out var building) && StoreHoursForBuilding(building) is { } hours && !hours.IsOpen(CurrentServerTime))
            throw new InvalidOperationException(StoreClosedMessage(hours));
        var now = _probulatorClock.GetUtcNow();
        var jobs = _quests.Values.Where(q => q.PlayerId == playerId && q.Kind == "foodDelivery").ToArray();
        // Expired jobs count even before the simulation tick or after an offline interval.
        var blockedUntil = jobs.Select(q => q.Status == "failed" ? q.FailedAtUtc : q.Status == "active" && q.DeadlineUtc <= now ? q.DeadlineUtc : null)
            .Where(at => at.HasValue).Select(at => at!.Value.AddDays(1)).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        if (blockedUntil > now)
        {
            string[] lines = ["I heard about you. You suck at delivering stuff. Try again tomorrow.",
                "Your last delivery was a disaster. Come back tomorrow.", "No deliveries for you today. We heard what happened. Try again tomorrow."];
            var index = _deliveryRefusals.AddOrUpdate(playerId, 0, (_, prior) => (prior + 1) % lines.Length);
            _questDialogue.Enqueue(new($"delivery-refusal:{Guid.NewGuid():N}", giver.Id, giver.Name, lines[index], now));
            throw new InvalidOperationException($"{giver.Name}: {lines[index]} Available again at {blockedUntil:u}.");
        }
        if (jobs.Any(q => q.Status == "active" && q.DeadlineUtc > now && (!allowCurrentGiver || q.GiverId != giver.Id)))
            throw new InvalidOperationException("Finish your current food delivery before taking another one.");
    }

    private QuestState GenerateFoodDelivery(string playerId, ActorState giver)
    {
        foreach (var pair in _questOffers.Where(p => p.Key.Player == playerId && p.Value.Kind == "foodDelivery").ToArray()) _questOffers.TryRemove(pair.Key, out _);
        var origin = giver.Position;
        if (_dungeons.TryGetValue(giver.LocationId, out var interior) && _baseEntities.TryGetValue(interior.BuildingId, out var storeBuilding))
            origin = Navigation.FindNearestWalkable(storeBuilding.Position);
        var minutes = RandomNumberGenerator.GetInt32(20, 31);
        var destinations = _baseEntities.Values.Where(e => e.Kind == EntityKind.Door && e.Position.Region == origin.Region &&
            e.Position.Distance2D(origin) is >= 30 and <= 600).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).Take(16).ToArray();
        WorldPosition? meetingPoint = null;
        CanonicalEntity? destination = null;
        // Check the walking route, including obstacles, so even five-minute jobs are feasible.
        bool Reachable(WorldPosition candidate)
        {
            var path = Navigation.FindPath(origin, candidate.X, candidate.Y);
            if (!path.Success) return false;
            var length = 0d; var previous = origin;
            foreach (var point in path.Waypoints) { length += previous.Distance2D(point); previous = point; }
            return length >= 10 && length <= minutes * 45;
        }
        foreach (var door in destinations)
        {
            var candidate = Navigation.FindNearestWalkable(door.Position);
            if (!Reachable(candidate)) continue;
            meetingPoint = candidate; destination = door; break;
        }
        for (var index = 0; meetingPoint is null && index < 16; index++)
        {
            var angle = index * Math.PI / 8;
            var candidate = Navigation.FindNearestWalkable(origin with { X = origin.X + Math.Cos(angle) * 60, Y = origin.Y + Math.Sin(angle) * 60 });
            if (Reachable(candidate)) meetingPoint = candidate;
        }
        var position = meetingPoint ?? throw new InvalidOperationException("No reachable delivery destination is available from this counter right now.");
        var place = "the marked outdoor meeting point";
        if (destination?.Properties.GetValueOrDefault("buildingId") is { } buildingId && _baseEntities.TryGetValue(buildingId, out var building))
            place = "outside " + (building.Properties.GetValueOrDefault("name") ?? building.Properties.GetValueOrDefault("address") ?? "the marked building");
        var id = $"food:{Guid.NewGuid():N}";
        var actorId = $"delivery-recipient:{Guid.NewGuid():N}";
        var name = UniqueNpcName(FriendlyHumanName(actorId, 0), actorId);
        var recipient = new ActorState(actorId, EntityKind.Npc, "resident", name, position, IsQuestGiver: false);
        var clue = $"{name} will wait {place} ({position.X:0}, {position.Y:0}). {DirectionClue(origin, position, name)}";
        return new(id, playerId, giver.Id, giver.Name, "foodDelivery", "offered", $"Food delivery for {name}",
            $"Deliver this food order to {name} within {minutes} minutes of accepting. {clue} Be careful: do not shake up the food, exceed 5 mph, or teleport. Damaged food will be refused on delivery. Hungry dogs will ambush you every 10 seconds while you deliver. Eating the order is a crime and fails the delivery. Failure or abandonment blocks delivery jobs for 24 hours.",
            RandomNumberGenerator.GetInt32(1_000, 5_001), DestinationActorId: actorId, DestinationName: name, DestinationClue: clue,
            DeliveryMinutes: minutes, DeliveryRecipient: recipient);
    }

    private async Task<QuestActionResult> AcceptFoodDeliveryCoreAsync(string playerId, QuestState offer, CancellationToken token)
    {
        var player = _players[playerId];
        var giver = FindActor(playerId, offer.GiverId);
        if (giver is null || !giver.OffersFoodDelivery || giver.HealthHearts <= 0 || player.LocationId != giver.LocationId || player.Position.Distance2D(giver.Position) > 5)
            throw new InvalidOperationException("Return to the food counter to accept this delivery.");
        CheckDeliveryAvailability(playerId, giver);
        var item = QuestFoodItem(offer);
        if (!CanAddToBackpack(playerId, [InventoryStack(item, 1)], out var message)) throw new InvalidOperationException(message);
        var accepted = offer with { Status = "active", DeadlineUtc = _probulatorClock.GetUtcNow().AddMinutes(offer.DeliveryMinutes!.Value) };
        AddInventory(playerId, item, 1);
        _quests[(playerId, accepted.Id)] = accepted;
        _actors[accepted.DeliveryRecipient!.Id] = accepted.DeliveryRecipient;
        await SaveInventoryAsync(playerId, token);
        await _store.SaveQuestAsync(Configuration.Id, accepted, token);
        return new(GetPrivateState(playerId), _players[playerId], accepted, $"Delivery accepted. Reach {accepted.DestinationName} within {accepted.DeliveryMinutes} minutes.");
    }

    private async Task<QuestActionResult> FailFoodDeliveryCoreAsync(QuestState quest, DateTimeOffset failedAt, string reason, CancellationToken token)
    {
        var failed = quest with { Status = "failed", FailedAtUtc = failedAt, Description = $"{reason} No more delivery jobs until {failedAt.AddDays(1):u}." };
        _quests[(quest.PlayerId, quest.Id)] = failed;
        ClearDeliveryDogs(quest.PlayerId);
        _deliveryAmbushWaves.TryRemove(quest.Id, out _);
        RemoveInventory(quest.PlayerId, QuestFoodItem(quest), 1);
        await SaveInventoryAsync(quest.PlayerId, token);
        await _store.SaveQuestAsync(Configuration.Id, failed, token);
        _questNotices.Enqueue((quest.PlayerId, failed.Description));
        return new(GetPrivateState(quest.PlayerId), _players[quest.PlayerId], failed, failed.Description);
    }

    private async Task ExpireFoodDeliveriesCoreAsync(string playerId, CancellationToken token)
    {
        await AdvanceQuestStagesAsync(playerId, token);
        var now = _probulatorClock.GetUtcNow();
        foreach (var quest in _quests.Values.Where(q => q.PlayerId == playerId && q.Kind == "foodDelivery" && q.Status == "active" && q.DeadlineUtc <= now).ToArray())
            await FailFoodDeliveryCoreAsync(quest, quest.DeadlineUtc!.Value, "Delivery failed: the food did not arrive in time.", token);
    }

    public async Task AdvanceFoodDeliveriesAsync(CancellationToken token = default)
    {
        await _deliveryLock.WaitAsync(token);
        try
        {
            foreach (var playerId in _players.Keys) await ExpireFoodDeliveriesCoreAsync(playerId, token);
            AdvanceDeliveryAmbushes();
        }
        finally { _deliveryLock.Release(); }
    }

    private async Task RestoreFoodDeliveriesAsync(string playerId, CancellationToken token)
    {
        await _deliveryLock.WaitAsync(token);
        try
        {
            await ExpireFoodDeliveriesCoreAsync(playerId, token);
            foreach (var q in _quests.Values.Where(q => q.PlayerId == playerId && q.Kind == "foodDelivery" && (q.Status == "active" || q.Status == "failed" && q.DeliveryRefused) && q.DeliveryRecipient is not null))
                _actors.TryAdd(q.DeliveryRecipient!.Id, q.DeliveryRecipient);
        }
        finally { _deliveryLock.Release(); }
    }

    private async Task<PlayerState> ConsumeDeliveryFoodAsync(string playerId, string itemType, CancellationToken token)
    {
        await _deliveryLock.WaitAsync(token);
        try
        {
            await ExpireFoodDeliveriesCoreAsync(playerId, token);
            var quest = _quests.Values.FirstOrDefault(q => q.PlayerId == playerId && q.Kind == "foodDelivery" && q.Status == "active" && QuestFoodItem(q) == itemType)
                ?? throw new InvalidOperationException("That food order is no longer available.");
            if (InventoryQuantity(playerId, itemType) < 1) throw new InvalidOperationException("The food order is not in your backpack.");
            await FailFoodDeliveryCoreAsync(quest, _probulatorClock.GetUtcNow(), "Delivery failed: you ate the customer's food. Food theft is a crime.", token);
            var player = _players[playerId];
            await SavePlayerAsync(player with { Stamina = player.MaximumStamina, HealthHearts = Math.Min(player.MaximumHealthHearts, player.HealthHearts + 2),
                FoodProtectedUntilUtc = _probulatorClock.GetUtcNow().AddMinutes(5), Version = player.Version + 1 }, token);
            var scene = player.Position;
            if (_dungeons.TryGetValue(player.LocationId, out var interior) && _baseEntities.TryGetValue(interior.BuildingId, out var building)) scene = building.Position;
            return await ReportCrimeAsync(playerId, scene, token);
        }
        finally { _deliveryLock.Release(); }
    }

    public const double MaximumFoodDeliverySpeedMetersPerSecond = 5 * .44704;

    private async Task RecordDeliveryHandlingAsync(string playerId, double speed, bool teleported, CancellationToken token)
    {
        if (!teleported && speed <= MaximumFoodDeliverySpeedMetersPerSecond + .000001) return;
        await _deliveryLock.WaitAsync(token);
        try
        {
            foreach (var quest in _quests.Values.Where(q => q.PlayerId == playerId && q.Kind == "foodDelivery" && q.Status == "active" && !q.FoodDamaged && q.DeadlineUtc > _probulatorClock.GetUtcNow()).ToArray())
            {
                var reason = teleported ? "Teleporting shook up the food." : "Traveling over 5 mph shook up the food.";
                var damaged = quest with { FoodDamaged = true, FoodDamageReason = reason };
                _quests[(playerId, quest.Id)] = damaged;
                await _store.SaveQuestAsync(Configuration.Id, damaged, token);
                _questNotices.Enqueue((playerId, reason + " The customer will refuse this order."));
            }
        }
        finally { _deliveryLock.Release(); }
    }

    private async Task RecordTauntQuestAsync(string playerId, string targetId, CancellationToken token)
    {
        await _deliveryLock.WaitAsync(token);
        try
        {
            foreach (var quest in _quests.Values.Where(q => q.PlayerId == playerId && q.Kind == "tauntEx" && q.Status == "active" && q.TargetActorId == targetId).ToArray())
            {
                var ready = quest with { Status = "ready", Progress = 1, Description = $"You taunted {quest.TargetName}. Return to {quest.GiverName} for your reward." };
                _quests[(playerId, quest.Id)] = ready;
                await _store.SaveQuestAsync(Configuration.Id, ready, token);
                _questNotices.Enqueue((playerId, ready.Description));
            }
        }
        finally { _deliveryLock.Release(); }
    }

    public IReadOnlyList<(string PlayerId, string Message)> TakeQuestNotices()
    {
        var notices = new List<(string, string)>();
        while (_questNotices.TryDequeue(out var notice)) notices.Add(notice);
        return notices;
    }

    public IReadOnlyList<ChatMessage> TakeQuestDialogue()
    {
        var messages = new List<ChatMessage>();
        while (_questDialogue.TryDequeue(out var message)) messages.Add(message);
        return messages;
    }
}
