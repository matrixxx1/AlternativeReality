using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private readonly SemaphoreSlim _adventureLock = new(1, 1);
    private readonly ConcurrentDictionary<string,string> _adventureFollowers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _adventureCalm = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cameraShots = new();
    private QuestState GenerateAdventure(string playerId, ActorState giver, int sequence)
    {
        var definition = AdventureCatalog.All[(StableInt(giver.Id) & int.MaxValue) % AdventureCatalog.All.Count];
        definition = AdventureCatalog.All[(AdventureCatalog.All.ToList().IndexOf(definition) + sequence) % AdventureCatalog.All.Count];
        var id = $"adventure:{playerId}:{giver.Id}:{sequence}";
        var goal = Navigation.FindNearestWalkable(giver.Position with { X = giver.Position.X + 35 + sequence % 3 * 10, Y = giver.Position.Y + 25 });
        if (definition.Id is "deliveries" or "rescue" && _activeInversion is { } active) goal=Navigation.FindNearestWalkable(active.Center with {X=active.Center.X+25});
        return new(id, playerId, giver.Id, giver.Name, "adventure:" + definition.Id, "offered", definition.Title,
            definition.Story, 10000, TargetActorId: id + ":subject", TargetName: definition.Subject,
            NextStagePosition: goal, NextStageName: definition.Subject, NextStageLocationId: "outdoor");
    }
    private static string AdventureSupply(QuestState q) => "quest:supplies:"+q.Id;
    private void EnsureAdventureSubject(QuestState quest)
    {
        if (quest.Status != "active" || quest.NextStagePosition is not { } point || quest.TargetActorId is null) return;
        var definition = AdventureCatalog.All.First(a => quest.Kind == "adventure:" + a.Id);
        _actors.TryAdd(quest.TargetActorId, new(quest.TargetActorId, EntityKind.Npc, "adventure:" + definition.Id, definition.Subject, point, HealthHearts: definition.Id == "bait" ? 80 : 15, MaximumHealthHearts: definition.Id == "bait" ? 80 : 15));
    }
    public async Task<QuestActionResult> AdventureActionAsync(string playerId, string questId, string choice, CancellationToken token = default)
    {
        await _adventureLock.WaitAsync(token);
        try
        {
            if (!_players.TryGetValue(playerId, out var player) || !_quests.TryGetValue((playerId, questId), out var quest) || quest.Status != "active" || !quest.Kind.StartsWith("adventure:")) throw new InvalidOperationException("That adventure is not active.");
            var definition = AdventureCatalog.All.First(a => quest.Kind == "adventure:" + a.Id);
            if (!definition.Choices.Contains(choice)) throw new InvalidOperationException("Choose one of this quest's actions.");
            if (quest.DeadlineUtc <= _probulatorClock.GetUtcNow()) throw new InvalidOperationException("This quest has expired.");
            if (definition.Id == "race" && player.LocationId.StartsWith("race:")) return await CheckAdventureRaceAsync(player, quest, token);
            if (!_actors.ContainsKey(quest.TargetActorId!)) throw new InvalidOperationException("That quest character is no longer here.");
            var subject = _actors.GetValueOrDefault(quest.TargetActorId!);
            var goal = subject?.Position ?? quest.NextStagePosition!.Value;
            if (player.LocationId != "outdoor" || player.Position.Distance2D(goal) > 5) throw new InvalidOperationException("Move within five meters of the quest marker.");
            if (definition.Id == "race") return await StartAdventureRaceAsync(player, quest, token);
            if (definition.Id == "gnomes" && choice != (quest.Progress == 0 ? "Inspect footprints" : quest.Progress == 1 ? "Question neighbor" : choice is "Return gnomes" or "Support gnome army" ? choice : "")) throw new InvalidOperationException("Follow the footprints, question the neighbor, then decide the gnomes’ fate.");
            if (definition.Id == "ufo" && choice != (quest.Progress < 2 ? "Ask for clue" : "Inspect vehicle")) throw new InvalidOperationException("Collect two clues before inspecting the vehicle.");
            if (definition.Id == "lost" && (quest.Progress < 2 ? choice != "Search for property" : choice == "Search for property")) throw new InvalidOperationException("Search twice, then choose whether to return or keep the property.");
            if (definition.Id == "rescue" && quest.Progress == 0 && !InsideInversion(player)) throw new InvalidOperationException("Rescue the mug enthusiast from inside an active inversion.");
            if (definition.Id == "roll" || definition.Id == "deliveries" || definition.Id == "ghost" && choice == "Return mower")
            { if(!RemoveInventory(playerId,AdventureSupply(quest),1))throw new InvalidOperationException("Bring the supplies given to you for this quest."); }
            if (choice == "Offer food")
            { if (!RemoveInventory(playerId, "food", 1)) throw new InvalidOperationException("Bring food for this approach."); _adventureCalm[quest.Id] = _probulatorClock.GetUtcNow().AddSeconds(30); await SaveInventoryAsync(playerId, token); }
            if (choice is "Offer coins" or "Buy replacement" or "Support gnome army")
            { var cost = choice == "Buy replacement" ? 2500 : 500; if (player.WalletCents < cost) throw new InvalidOperationException($"This choice costs ${cost / 100}."); await SavePlayerAsync(player with { WalletCents = player.WalletCents - cost, Version = player.Version + 1 }, token); }
            if (definition.Id == "deliveries" && !InsideInversion(player)) throw new InvalidOperationException("Deliver during a Reality inversion, inside its ring.");
            if (definition.Id is "photo" or "inspection") quest = RedeemPhotographs(playerId, quest);
            if (definition.Id == "watch" && _probulatorClock.GetUtcNow().ToUnixTimeSeconds() % 8 < 3)
            { quest = quest with { Progress = Math.Max(0, quest.Progress - 1) }; EventSay(subject!.Id, subject.Name, "I SAW THAT! Please respect the neighborhood's arbitrary standards."); }
            else if (definition.Escort && (choice is "Begin escort" or "Offer food" or "Offer coins" or "Subdue chest"))
            {
                if (definition.Id == "rescue" && quest.Progress == 0) throw new InvalidOperationException("Retrieve the favorite mug first.");
                _adventureFollowers[quest.Id] = playerId;
                quest = quest with { Progress = 1, Description = "Keep your companion alive and within 10 meters. Lead them back to the quest giver.", NextStagePosition = definition.Id == "bait" ? Navigation.FindNearestWalkable(goal with {X=goal.X+100}) : FindActor(playerId, quest.GiverId)?.Position ?? goal };
            }
            else if (definition.Id == "refund" && choice == "Fight bandit")
            { _relationships[(playerId, subject!.Id)] = -10; _actors[subject.Id] = subject with { EquippedWeapon = "fist", Version = subject.Version + 1 }; quest = quest with { Description = "Defeat the dissatisfied bandit, then return to the quest giver." }; }
            else
            {
                if (definition.Id == "refund" && choice == "Exchange weapon" && !RemoveInventory(playerId, "knife", 1)) throw new InvalidOperationException("Bring a knife to exchange.");
                var steps = definition.Id is "gnomes" or "ufo" or "deliveries" or "lost" or "watch" ? 3 : 1;
                var progress = definition.Id is "photo" or "inspection" ? 3 : quest.Progress + 1;
                var ready = progress >= steps || definition.Id is "photo" or "inspection";
                if (definition.Id == "rescue" && choice == "Collect favorite mug") ready = false;
                var next = ready ? FindActor(playerId, quest.GiverId)?.Position ?? goal : Navigation.FindNearestWalkable(goal with { X = goal.X + 18, Y = goal.Y - 12 });
                quest = quest with { Progress = progress, Status = ready ? "ready" : "active", NextStagePosition = next, NextStageName = ready ? quest.GiverName : definition.Subject,
                    Description = ready ? "Objective complete. Return to " + quest.GiverName + "." : $"Clue/checkpoint {progress} found. Continue to the next marker." };
                if (subject is not null) _actors[subject.Id] = subject with { Position = next, Version = subject.Version + 1 };
                if (choice is "Keep property" or "Support gnome army") { quest = quest with { RewardCents = quest.RewardCents / 2 }; await AdjustAlignmentAsync(playerId, -.25, token); }
                else if (choice is "Split supplies" or "Return property" or "Return gnomes") await AdjustAlignmentAsync(playerId, .25, token);
            }
            if (definition.Id is "photo" or "inspection")
            {
                try { await _store.SaveQuestAsync(Configuration.Id, quest, token, GetInventoryState(playerId)); }
                catch { foreach (var item in quest.ObjectiveActorIds!) AddInventory(playerId, item, 1); throw; }
            }
            else { await _store.SaveQuestAsync(Configuration.Id, quest, token); await SaveInventoryAsync(playerId, token); }
            _quests[(playerId, questId)] = quest;
            return new(GetPrivateState(playerId), _players[playerId], quest, quest.Description);
        }
        finally { _adventureLock.Release(); }
    }
    public async Task PhotographAsync(string playerId, string? subjectId, CancellationToken token = default, string? thumbnailDataUrl = null)
    {
        await _adventureLock.WaitAsync(token);
        try
        {
            if (!_players.TryGetValue(playerId, out var player) || player.EquippedWeapon != "camera") throw new InvalidOperationException("Equip your camera in the weapon slot first.");
            ValidatePhotographThumbnail(thumbnailDataUrl);
            var now = _probulatorClock.GetUtcNow();
            if (_cameraShots.TryGetValue(playerId, out var last) && now - last < TimeSpan.FromSeconds(1)) throw new InvalidOperationException("Let the film advance.");
            var actor = subjectId is not null ? FindActor(playerId, subjectId) : ActorsAtLocation(player.LocationId).Where(a => a.EventName is not null).OrderBy(a => a.Position.Distance2D(player.Position)).FirstOrDefault();
            string evidence;
            var visibleActor = actor is not null && actor.LocationId == player.LocationId && actor.Position.Distance2D(player.Position) <= 40 && (player.LocationId != "outdoor" || Navigation.CanTraverse(player.Position, actor.Position));
            if (subjectId is not null && !visibleActor) throw new InvalidOperationException("That subject is out of camera range or behind an obstacle.");
            if (visibleActor) evidence = "actor:" + actor!.Id;
            else if (_dungeons.TryGetValue(player.LocationId, out var d))
            {
                evidence = d.Barriers?.Any(b => !b.Destroyed && player.Position.Distance2D(player.Position with { X = b.X, Y = b.Y }) < 8) == true ? "inspection:barrier"
                    : IsWater(DungeonTerrainAt(d, player.Position)) ? "inspection:water" : player.Position.Distance2D(d.Exit) < 5 ? "inspection:exit" : "scenery";
            }
            else evidence = "scenery";
            var kind = visibleActor && actor!.EventName is not null ? "event" : evidence.StartsWith("inspection:") ? "inspection" : "scenery";
            var subject = visibleActor ? actor!.Name : evidence switch { "inspection:barrier" => "Dungeon barrier", "inspection:water" => "Water crossing", "inspection:exit" => "Dungeon exit", _ => "Scenery" };
            var itemType = "photograph:" + Guid.NewGuid().ToString("N");
            _photographs[itemType] = new(evidence, subject, kind, visibleActor ? actor!.Subtype : evidence, player.LocationId, now,
                thumbnailDataUrl is null ? null : "/api/photographs/" + itemType[11..]);
            var inventory = _inventories[playerId];
            lock (inventory)
            {
                if (!RemoveInventory(playerId, "film", 1))
                { _photographs.TryRemove(itemType, out _); throw new InvalidOperationException("You need film. Buy it from vendors or find it in treasure chests."); }
                if (!CanAddToBackpack(playerId, [InventoryStack(itemType, 1)], out var capacityMessage))
                { AddInventory(playerId, "film", 1); _photographs.TryRemove(itemType, out _); throw new InvalidOperationException(capacityMessage); }
                AddInventory(playerId, itemType, 1);
            }
            try { await _store.SaveCameraExposureAsync(GetInventoryState(playerId), itemType, thumbnailDataUrl, token); }
            catch { RemoveInventory(playerId, itemType, 1); AddInventory(playerId, "film", 1); _photographs.TryRemove(itemType, out _); throw; }
            _cameraShots[playerId] = now;
            foreach (var key in _tradeQuotes.Keys.Where(k => k.Player == playerId).ToArray()) _tradeQuotes.TryRemove(key, out _);
            _progressionNotices.Enqueue(new(playerId, "Photograph added to your backpack: " + subject + ". Submit it as quest proof or sell it to a vendor."));
        }
        finally { _adventureLock.Release(); }
    }
    private async Task AdvanceAdventuresAsync(double seconds, CancellationToken token)
    {
        foreach (var quest in _quests.Values.Where(q => q.Kind.StartsWith("adventure:") && q.Status == "active").ToArray())
        {
            if (!_players.TryGetValue(quest.PlayerId, out var player)) continue;
            if (quest.DeadlineUtc <= _probulatorClock.GetUtcNow() || !_actors.ContainsKey(quest.TargetActorId!))
            {
                var success=quest.Kind=="adventure:refund"&&quest.Description.StartsWith("Defeat");
                var ended=quest with {Status=success?"ready":"failed",Description=success?"Defective weapon retrieved. Return to the quest giver.":"The quest expired or its companion was defeated.",FailedAtUtc=success?null:_probulatorClock.GetUtcNow()};
                _quests[(quest.PlayerId,quest.Id)]=ended;await _store.SaveQuestAsync(Configuration.Id,ended,token);_adventureFollowers.TryRemove(quest.Id,out _);continue;
            }
            if (!_adventureFollowers.ContainsKey(quest.Id) && quest.Kind is "adventure:goose" or "adventure:mimic" && _actors.TryGetValue(quest.TargetActorId!,out var runner))
            {
                var delta=runner.Position.Distance2D(player.Position);
                if(delta>1&&delta<12){var point=runner.Position with{X=runner.Position.X+(runner.Position.X-player.Position.X)/delta*seconds*1.2,Y=runner.Position.Y+(runner.Position.Y-player.Position.Y)/delta*seconds*1.2};if(Navigation.CanTraverse(runner.Position,point)){var moving=runner with{Position=point,IsMoving=true,Version=runner.Version+1};_actors[runner.Id]=moving;_inversionActorUpdates.Enqueue(moving);}}
            }
            if (!_adventureFollowers.ContainsKey(quest.Id) || !_actors.TryGetValue(quest.TargetActorId!, out var follower)) continue;
            if (follower.HealthHearts <= 0) continue;
            var distance = follower.Position.Distance2D(player.Position);
            if (distance > 2 && distance <= 30)
            {
                var next = follower.Position with { X = follower.Position.X + (player.Position.X - follower.Position.X) / distance * seconds * 2, Y = follower.Position.Y + (player.Position.Y - follower.Position.Y) / distance * seconds * 2 };
                if (Navigation.CanTraverse(follower.Position, next)) { _actors[follower.Id] = follower with { Position = next, IsMoving = true, Version = follower.Version + 1 }; _inversionActorUpdates.Enqueue(_actors[follower.Id]); }
            }
            if (quest.Kind == "adventure:scream" && _adventureCalm.GetValueOrDefault(quest.Id) <= _probulatorClock.GetUtcNow())
            { _adventureCalm[quest.Id] = _probulatorClock.GetUtcNow().AddSeconds(8); EventSay(follower.Id, follower.Name, "AAAAH! ANOTHER COMPLETELY ORDINARY THING!"); foreach (var enemy in _actors.Values.Where(a => a.FriendRating < 0 && a.Position.Distance2D(follower.Position) < 30)) _relationships[(player.Id, enemy.Id)] = -5; }
            if (quest.NextStagePosition is { } goal && follower.Position.Distance2D(goal) < 5 && player.Position.Distance2D(goal) < 8)
            {
                var ready = quest with { Status = "ready", Description = "Companion delivered safely. Speak to " + quest.GiverName + "." }; _quests[(quest.PlayerId, quest.Id)] = ready;
                await _store.SaveQuestAsync(Configuration.Id, ready, token); _progressionNotices.Enqueue(new(player.Id,ready.Description)); _adventureFollowers.TryRemove(quest.Id, out _);
            }
        }
    }
}
