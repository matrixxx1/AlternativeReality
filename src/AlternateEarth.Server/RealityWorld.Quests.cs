using System.Security.Cryptography;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static readonly string[] QuestItemTypes =
    [
        "rock", "arrow", "gallonOfGas", "bike", "skateboard", "pencil", "pen", "marker",
        "sprayPaint", "book", "calculator", "cellPhone"
    ];

    public QuestInteraction RequestQuest(string playerId, string actorId)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var actor = FindActor(playerId, actorId) ?? throw new InvalidOperationException("That character is not here.");
        if (actor.Kind != EntityKind.Npc) throw new InvalidOperationException("Animals do not offer quests.");
        if (player.LocationId != actor.LocationId || player.Position.Distance2D(actor.Position) > 5) throw new InvalidOperationException("Move within 5 meters to talk about a quest.");

        var current = _quests.Where(pair => pair.Key.Player == playerId)
            .Select(pair => pair.Value)
            .FirstOrDefault(quest => quest.Status is "active" or "ready" &&
                (quest.GiverId == actorId || quest.DestinationActorId == actorId));
        if (current is not null) return new QuestInteraction(current, false, QuestCanComplete(playerId, current, actorId), actorId);
        if (!actor.IsQuestGiver) throw new InvalidOperationException($"{actor.Name} does not have a quest for you.");

        var sequence = _quests.Count(pair => pair.Key.Player == playerId && pair.Value.GiverId == actorId);
        var offer = GenerateQuest(playerId, actor, sequence);
        _questOffers[(playerId, offer.Id)] = offer;
        return new QuestInteraction(offer, true, false, actorId);
    }

    public async Task<QuestActionResult> AcceptQuestAsync(string playerId, string questId, CancellationToken cancellationToken = default)
    {
        if (!_players.ContainsKey(playerId)) throw new InvalidOperationException("Unknown player.");
        if (!_questOffers.TryRemove((playerId, questId), out var offered)) throw new InvalidOperationException("That quest offer expired. Talk to the quest giver again.");
        if (offered.Kind == "courier")
        {
            var package = QuestPackageItem(offered);
            if (!CanAddToBackpack(playerId, new[] { InventoryStack(package, 1) }, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);
            AddInventory(playerId, package, 1);
            await SaveInventoryAsync(playerId, cancellationToken);
        }
        var accepted = offered with { Status = "active" };
        _quests[(playerId, accepted.Id)] = accepted;
        await _store.SaveQuestAsync(Configuration.Id, accepted, cancellationToken);
        return new QuestActionResult(GetPrivateState(playerId), _players[playerId], accepted, $"Quest accepted: {accepted.Title}");
    }

    public async Task<QuestActionResult> CompleteQuestAsync(string playerId, CompleteQuestRequest request, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || !_quests.TryGetValue((playerId, request.QuestId), out var quest)) throw new InvalidOperationException("Quest not found.");
        var actor = FindActor(playerId, request.ActorId) ?? throw new InvalidOperationException("That character is not here.");
        if (player.LocationId != actor.LocationId || player.Position.Distance2D(actor.Position) > 5) throw new InvalidOperationException("Move within 5 meters to complete the quest.");
        if (!QuestCanComplete(playerId, quest, actor.Id)) throw new InvalidOperationException("That quest objective is not ready to turn in.");

        if (quest.Kind == "item" && !RemoveInventory(playerId, quest.RequiredItemType!, quest.RequiredQuantity)) throw new InvalidOperationException("The requested item is no longer in your backpack.");
        if (quest.Kind is "courier" or "missingPet")
        {
            var carried = quest.Kind == "courier" ? QuestPackageItem(quest) : QuestPetItem(quest);
            if (!RemoveInventory(playerId, carried, 1)) throw new InvalidOperationException("The quest item is no longer in your backpack.");
        }

        var completed = quest with { Status = "completed" };
        _quests[(playerId, quest.Id)] = completed;
        var updated = player with { WalletCents = player.WalletCents + quest.RewardCents, Version = player.Version + 1 };
        var friend = Relationship(playerId, quest.GiverId) + 1;
        _relationships[(playerId, quest.GiverId)] = friend;
        await SaveInventoryAsync(playerId, cancellationToken);
        await SavePlayerAsync(updated, cancellationToken);
        await _store.SaveQuestAsync(Configuration.Id, completed, cancellationToken);
        await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(playerId, quest.GiverId, friend), cancellationToken);
        return new QuestActionResult(GetPrivateState(playerId), updated, completed, $"Quest complete: {quest.Title}. Reward: {quest.RewardCents / 100m:C}.");
    }

    public async Task<QuestActionResult> AbandonQuestAsync(string playerId, string questId, CancellationToken cancellationToken = default)
    {
        if (!_quests.TryGetValue((playerId, questId), out var quest) || quest.Status is "completed" or "failed" or "abandoned") throw new InvalidOperationException("Active quest not found.");
        if (quest.Kind == "courier") RemoveInventory(playerId, QuestPackageItem(quest), 1);
        if (quest.Kind == "missingPet") RemoveInventory(playerId, QuestPetItem(quest), 1);
        var abandoned = quest with { Status = "abandoned" };
        _quests[(playerId, quest.Id)] = abandoned;
        await SaveInventoryAsync(playerId, cancellationToken);
        await _store.SaveQuestAsync(Configuration.Id, abandoned, cancellationToken);
        return new QuestActionResult(GetPrivateState(playerId), _players[playerId], abandoned, $"Abandoned: {quest.Title}");
    }

    public async Task<QuestActionResult> CaptureQuestPetAsync(string playerId, string actorId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var quest = _quests.Where(pair => pair.Key.Player == playerId).Select(pair => pair.Value)
            .FirstOrDefault(item => item.Kind == "missingPet" && item.Status == "active" && item.TargetActorId == actorId)
            ?? throw new InvalidOperationException("That animal is not a pet from one of your active quests.");
        var pet = FindActor(playerId, actorId) ?? throw new InvalidOperationException("The missing pet is no longer here.");
        if (player.Position.Distance2D(pet.Position) > 3) throw new InvalidOperationException("Move within 3 meters to safely capture the pet.");
        var petItem = QuestPetItem(quest);
        if (!CanAddToBackpack(playerId, new[] { InventoryStack(petItem, 1) }, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);
        _actors.TryRemove(actorId, out _);
        AddInventory(playerId, petItem, 1);
        var ready = quest with { Status = "ready", Description = $"Return {quest.TargetName} safely to {quest.GiverName}." };
        _quests[(playerId, quest.Id)] = ready;
        await SaveInventoryAsync(playerId, cancellationToken);
        await _store.SaveQuestAsync(Configuration.Id, ready, cancellationToken);
        return new QuestActionResult(GetPrivateState(playerId), player, ready, $"You safely captured {quest.TargetName}. Return to {quest.GiverName}.");
    }

    public async Task<VegetationChopResult> ChopVegetationAsync(string playerId, string entityId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId != "outdoor") throw new InvalidOperationException("You can only cut vegetation in the outdoor world.");
        if (!player.EquippedWeapon.Equals("sword", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Equip a sword to cut down a tree or bush.");
        if (!_baseEntities.TryGetValue(entityId, out var vegetation) || vegetation.Kind is not (EntityKind.Tree or EntityKind.Bush or EntityKind.ResourceNode)) throw new InvalidOperationException("That object is no longer there.");
        var isMailbox = vegetation.Kind == EntityKind.ResourceNode && vegetation.Properties.GetValueOrDefault("subtype") == "mailbox";
        if (vegetation.Kind == EntityKind.ResourceNode && !isMailbox) throw new InvalidOperationException("That object cannot be cut down.");
        var swordRange = InventoryDefinition("sword").RangeMeters;
        if (player.Position.Distance2D(vegetation.Position) > swordRange) throw new InvalidOperationException($"Move within {swordRange:0.#} meters to use the sword.");

        var quest = _quests.Where(pair => pair.Key.Player == playerId).Select(pair => pair.Value)
            .FirstOrDefault(item => item.Kind == "chop" && item.Status == "active" && item.TargetActorId == entityId);
        string message; ChatMessage? witnessMessage = null;
        if (quest is not null)
        {
            var ready = quest with { Status = "ready", Description = $"The target {vegetation.Kind.ToString().ToLowerInvariant()} is down. Return to {quest.GiverName}." };
            _quests[(playerId, quest.Id)] = ready;
            await _store.SaveQuestAsync(Configuration.Id, ready, cancellationToken);
            message = $"Quest target cut down. Return to {quest.GiverName}.";
        }
        else
        {
            var rewards = isMailbox ? new[] { InventoryStack("wood", 1), InventoryStack("metal", 2) } : new[] { InventoryStack(vegetation.Kind == EntityKind.Tree ? "wood" : "kindling", vegetation.Kind == EntityKind.Tree ? 3 : 2) };
            if (!CanAddToBackpack(playerId, rewards, out var capacityMessage)) throw new InvalidOperationException(capacityMessage);
            foreach (var reward in rewards) AddInventory(playerId, reward.ItemType, reward.Quantity);
            await SaveInventoryAsync(playerId, cancellationToken);
            message = isMailbox ? "Cut down the mailbox and collected 1 × Wood and 2 × Scrap metal." : vegetation.Kind == EntityKind.Tree ? "Cut down the tree and collected 3 × Wood." : "Cut down the bush and collected 2 × Kindling.";
            if (isMailbox)
            {
                var witness = _actors.Values.Where(actor => actor.Kind == EntityKind.Npc && actor.LocationId == "outdoor" && actor.Position.Distance2D(vegetation.Position) <= 30).OrderBy(actor => actor.Position.Distance2D(vegetation.Position)).FirstOrDefault();
                if (witness is not null)
                {
                    witnessMessage = new ChatMessage($"chat:{Guid.NewGuid():N}", witness.Id, witness.Name, "I'm calling the cops!", DateTimeOffset.UtcNow);
                    player = await ReportCrimeAsync(playerId, witness.Position, cancellationToken);
                    message += $" {witness.Name} witnessed it and called the cops.";
                }
            }
        }

        if (!isMailbox)
        {
            await _store.RemoveEntityAsync(Configuration.Id, vegetation, cancellationToken);
            _removedBaseEntityIds[entityId] = 0;
        }
        _baseEntities.TryRemove(entityId, out _);
        _navigation = new WorldNavigation(_loadedBounds ?? Configuration.Area.Bounds, _baseEntities.Values.Concat(_realityEntities.Values).ToArray(), _elevationSamples.Values.ToArray());
        return new VegetationChopResult(entityId, player, GetPrivateState(playerId), message, witnessMessage);
    }

    public async Task<WorldCrimeResult> AttackWorldObjectAsync(string playerId, string entityId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId != "outdoor") throw new InvalidOperationException("You can only attack outdoor world objects.");
        if (!_baseEntities.TryGetValue(entityId, out var entity) || entity.Kind != EntityKind.Vehicle) throw new InvalidOperationException("That vehicle is no longer there.");
        if (player.EquippedWeapon == "none") throw new InvalidOperationException("Equip a weapon before attacking a vehicle.");
        if (player.Position.Distance2D(entity.Position) > InventoryDefinition(player.EquippedWeapon).RangeMeters) throw new InvalidOperationException("That vehicle is out of weapon range.");
        var updated = await ReportCrimeAsync(playerId, entity.Position, cancellationToken);
        return new WorldCrimeResult(updated, GetPrivateState(playerId), "The vehicle alarm is sounding. Police are on the way.");
    }

    public async Task<LockPickResult> PickLockAsync(string playerId, string doorId, CancellationToken cancellationToken = default)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.LocationId != "outdoor") throw new InvalidOperationException("You can only pick an exterior door lock.");
        if (!player.GodMode && InventoryQuantity(playerId, "lockPickSet") <= 0) throw new InvalidOperationException("You need a lock pick set.");
        var door = _baseEntities.Values.FirstOrDefault(entity => entity.Id == doorId && entity.Kind == EntityKind.Door) ?? throw new InvalidOperationException("That door does not exist.");
        if (player.Position.Distance2D(door.Position) > 3) throw new InvalidOperationException("Move within 3 meters to pick the lock.");
        var buildingId = door.Properties.GetValueOrDefault("buildingId") ?? string.Empty;
        var building = _baseEntities.GetValueOrDefault(buildingId) ?? throw new InvalidOperationException("That building does not exist.");
        if (!IsBuildingLocked(building)) return new LockPickResult(player, GetPrivateState(playerId), true, false, "The door is already unlocked.");
        var success = RandomNumberGenerator.GetInt32(100) < 15;
        if (success) _pickedLocks[$"{playerId}:{doorId}:{CurrentDoorLockCycle}"] = 0;
        var witnessed = _actors.Values.Any(actor => actor.Kind == EntityKind.Npc && actor.LocationId == "outdoor" && actor.Position.Distance2D(door.Position) <= 30);
        var policeCalled = witnessed && success && RandomNumberGenerator.GetInt32(100) < 10;
        if (witnessed) player = await ReportCrimeAsync(playerId, door.Position, cancellationToken, policeCalled);
        var message = success ? policeCalled ? "Lock opened, but a witness called the police." : witnessed ? "Lock opened. A witness saw the crime." : "Lock opened successfully." : witnessed ? "The lock resisted the attempt. A witness saw the crime." : "The lock resisted the attempt.";
        return new LockPickResult(player, GetPrivateState(playerId), success, policeCalled, message);
    }

    private async Task<PlayerState> ReportCrimeAsync(string playerId, WorldPosition scene, CancellationToken cancellationToken, bool dispatchPolice = true)
    {
        var player = _players[playerId];
        var updated = player with { WantedLevel = player.WantedLevel + 1, Version = player.Version + 1 };
        await SavePlayerAsync(updated, cancellationToken);
        _lastWantedDecay[playerId] = DateTimeOffset.UtcNow;
        if (dispatchPolice) _pendingPolice[$"{playerId}:{Guid.NewGuid():N}"] = new PendingPoliceResponse(playerId, scene, DateTimeOffset.UtcNow.AddSeconds(20));
        return updated;
    }

    private async Task<QuestState?> RecordQuestKillAsync(string playerId, ActorState actor, CancellationToken cancellationToken)
    {
        var quest = _quests.Where(pair => pair.Key.Player == playerId).Select(pair => pair.Value)
            .FirstOrDefault(item => item.Status == "active" && item.TargetActorId == actor.Id && item.Kind is "hunt" or "missingPet");
        if (quest is null) return null;
        var updated = quest.Kind == "missingPet"
            ? quest with { Status = "failed", Description = $"You killed {quest.TargetName}. {quest.GiverName} is furious." }
            : quest with { Status = "ready", Description = $"You defeated {quest.TargetName}. Return to {quest.GiverName} for your reward." };
        _quests[(playerId, quest.Id)] = updated;
        await _store.SaveQuestAsync(Configuration.Id, updated, cancellationToken);
        return updated;
    }

    private QuestState GenerateQuest(string playerId, ActorState giver, int sequence)
    {
        var id = $"quest:{giver.Id}:{sequence}";
        var random = new Random(StableInt($"{Configuration.Seed}:{playerId}:{id}"));
        var kind = (StableInt(giver.Id) & int.MaxValue) % 5;
        if (kind == 1)
        {
            var animals = _actors.Values.Where(actor => actor.Kind == EntityKind.Animal && actor.Subtype is not ("dog" or "cat")).OrderBy(actor => actor.Id).ToArray();
            if (animals.Length > 0)
            {
                var target = animals[random.Next(animals.Length)]; var clue = DirectionClue(giver.Position, target.Position, target.Name);
                return new QuestState(id, playerId, giver.Id, giver.Name, "hunt", "offered", $"Hunt {target.Name} the {target.Subtype}", $"Defeat the specific {target.Subtype} named {target.Name}. {clue}", random.Next(25_000, 100_001), TargetActorId: target.Id, TargetName: target.Name, DestinationClue: clue);
            }
        }
        if (kind == 2)
        {
            var destinations = _actors.Values.Where(actor => actor.Kind == EntityKind.Npc && actor.Id != giver.Id).OrderBy(actor => actor.Id).ToArray();
            if (destinations.Length > 0)
            {
                var target = destinations[random.Next(destinations.Length)]; var clue = DirectionClue(giver.Position, target.Position, target.Name); var distance = giver.Position.Distance2D(target.Position);
                return new QuestState(id, playerId, giver.Id, giver.Name, "courier", "offered", $"Package for {target.Name}", $"Carry a sealed package to {target.Name}. {clue}", Math.Max(15_000, (long)(distance * 125)), DestinationActorId: target.Id, DestinationName: target.Name, DestinationClue: clue);
            }
        }
        if (kind == 3)
        {
            var pets = _actors.Values.Where(actor => actor.Kind == EntityKind.Animal && actor.Subtype is "dog" or "cat").OrderBy(actor => actor.Id).ToArray();
            if (pets.Length > 0)
            {
                var pet = pets[random.Next(pets.Length)]; var clue = DirectionClue(giver.Position, pet.Position, pet.Name);
                return new QuestState(id, playerId, giver.Id, giver.Name, "missingPet", "offered", $"Find {pet.Name}", $"Find and safely capture the missing {pet.Subtype} {pet.Name}; do not hurt them. {clue}", random.Next(30_000, 80_001), TargetActorId: pet.Id, TargetName: pet.Name, DestinationClue: clue);
            }
        }
        if (kind == 4)
        {
            var vegetation = _baseEntities.Values.Where(entity => entity.Kind is EntityKind.Tree or EntityKind.Bush).OrderBy(entity => entity.Id).ToArray();
            if (vegetation.Length > 0)
            {
                var target = vegetation[random.Next(vegetation.Length)]; var name = target.Kind == EntityKind.Tree ? "marked tree" : "marked bush"; var clue = DirectionClue(giver.Position, target.Position, name);
                return new QuestState(id, playerId, giver.Id, giver.Name, "chop", "offered", $"Cut down a specific {target.Kind.ToString().ToLowerInvariant()}", $"Equip a sword and cut down the marked {target.Kind.ToString().ToLowerInvariant()}. {clue}", random.Next(25_000, 70_001), TargetActorId: target.Id, TargetName: name, DestinationClue: clue);
            }
        }
        var itemType = QuestItemTypes[(StableInt(id) & int.MaxValue) % QuestItemTypes.Length]; var definition = InventoryDefinition(itemType);
        var reward = Math.Max(10_000, checked(definition.MaximumPriceCents * 4 + 10_000));
        return new QuestState(id, playerId, giver.Id, giver.Name, "item", "offered", $"Find {definition.DisplayName}", $"Find 1 {definition.DisplayName} and bring it back to {giver.Name}. Look for loose items, treasure, defeated enemies, or merchants.", reward, itemType, 1);
    }

    private bool QuestCanComplete(string playerId, QuestState quest, string actorId)
    {
        if (quest.Status is not ("active" or "ready")) return false;
        return quest.Kind switch
        {
            "item" => actorId == quest.GiverId && InventoryQuantity(playerId, quest.RequiredItemType!) >= quest.RequiredQuantity,
            "hunt" => actorId == quest.GiverId && quest.Status == "ready",
            "courier" => actorId == quest.DestinationActorId && InventoryQuantity(playerId, QuestPackageItem(quest)) > 0,
            "missingPet" => actorId == quest.GiverId && quest.Status == "ready" && InventoryQuantity(playerId, QuestPetItem(quest)) > 0,
            "chop" => actorId == quest.GiverId && quest.Status == "ready",
            _ => false
        };
    }

    private static string QuestPackageItem(QuestState quest) => $"quest:package:{quest.Id}";
    private static string QuestPetItem(QuestState quest) => $"quest:pet:{quest.Id}";
    private static string DirectionClue(WorldPosition from, WorldPosition to, string name)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y; var distance = Math.Sqrt(dx * dx + dy * dy);
        var angle = (Math.Atan2(dx, dy) * 180 / Math.PI + 360) % 360;
        string[] directions = ["north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"];
        return $"{name} was last seen about {distance:0} meters {directions[(int)Math.Round(angle / 45) % 8]}.";
    }
}

public sealed record QuestActionResult(PlayerPrivateState PrivateState, PlayerState Player, QuestState Quest, string Message);
public sealed record VegetationChopResult(string EntityId, PlayerState Player, PlayerPrivateState PrivateState, string Message, ChatMessage? WitnessMessage = null);
public sealed record PendingPoliceResponse(string PlayerId, WorldPosition WitnessPosition, DateTimeOffset DueAtUtc);
public sealed record WorldCrimeResult(PlayerState Player, PlayerPrivateState PrivateState, string Message);
public sealed record LockPickResult(PlayerState Player, PlayerPrivateState PrivateState, bool Success, bool PoliceCalled, string Message);
