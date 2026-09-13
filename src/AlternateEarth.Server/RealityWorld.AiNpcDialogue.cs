using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public const double AiNpcRefusalRating = -5;
    private readonly ConcurrentDictionary<(string Player, string Actor), string> _aiDialogueSessions = new();

    public void OpenAiNpcDialogue(string playerId, string actorId)
    {
        var actor = FindActor(playerId, actorId);
        if (!_players.TryGetValue(playerId, out var player) || actor is null || actor.HealthHearts <= 0 || !actor.AiDialogueEnabled ||
            actor.LocationId != player.LocationId || actor.Position.Distance2D(player.Position) > 6)
            throw new InvalidOperationException("Move closer to an AI character to talk.");
        BeginConversation(playerId, actorId);
    }

    public Task<NpcDialogueHistoryPage> LoadAiNpcHistoryAsync(string playerId, string actorId, long? before = null, CancellationToken token = default)
    {
        var actor = FindActor(playerId, actorId);
        if (!_players.TryGetValue(playerId, out var player) || actor is null || !actor.AiDialogueEnabled || actor.HealthHearts <= 0 ||
            actor.LocationId != player.LocationId || actor.Position.Distance2D(player.Position) > 6)
            throw new InvalidOperationException("Move closer to that AI character to read your conversation.");
        return _store.LoadNpcDialogueHistoryAsync(Configuration.Id, playerId, actor.AiDialogueId ?? actor.Id, before, token);
    }

    public async Task ImportAiNpcHistoryAsync(string playerId, string actorId, LocalAiNpcDialogueService service, CancellationToken token)
    {
        var actor = FindActor(playerId, actorId) ?? throw new InvalidOperationException("That character is no longer here.");
        var npcId = actor.AiDialogueId ?? actor.Id;
        if ((await _store.LoadNpcDialogueHistoryAsync(Configuration.Id, playerId, npcId, token: token)).Messages.Count > 0) return;
        var messages = await service.ReadHistoryAsync($"{Configuration.Id}:npc:{npcId}:player:{playerId}", token);
        foreach (var message in messages)
            await _store.SaveNpcDialogueMessageAsync(Configuration.Id, playerId, npcId, "import:" + message.Id, message.Role, message.Text, token, message.SaidAtUtc);
    }

    public Task SaveAiNpcPlayerMessageAsync(string playerId, string actorId, string interactionId, string message, CancellationToken token = default)
    {
        var actor = FindActor(playerId, actorId) ?? throw new InvalidOperationException("That character is no longer here.");
        return _store.SaveNpcDialogueMessageAsync(Configuration.Id, playerId, actor.AiDialogueId ?? actor.Id, interactionId, "player", message.Trim(), token);
    }

    public AiNpcDialoguePreparation BeginAiNpcDialogue(string playerId, NpcDialogueRequest request)
    {
        if (!_players.TryGetValue(playerId, out var player)) throw new InvalidOperationException("Unknown player.");
        var actor = FindActor(playerId, request.ActorId);
        if (actor is null || actor.HealthHearts <= 0 || !actor.AiDialogueEnabled)
            throw new InvalidOperationException("That character cannot talk with LocalAI.");
        if (actor.LocationId != player.LocationId || actor.Position.Distance2D(player.Position) > 6)
            throw new InvalidOperationException("Move closer to talk with that character.");
        var message = (request.Message ?? string.Empty).Trim();
        if (message.Length is < 1 or > 180) throw new InvalidOperationException("NPC dialogue must contain 1 to 180 characters.");
        if (!Guid.TryParse(request.InteractionId, out var interaction)) throw new InvalidOperationException("NPC dialogue requires a valid interaction ID.");
        var interactionId = interaction.ToString("D");
        var relationship = Relationship(playerId, actor.Id);
        BeginConversation(playerId, actor.Id);
        _aiDialogueSessions[(playerId, actor.Id)] = interactionId;
        if (relationship <= AiNpcRefusalRating)
            return new(actor.Id, interactionId, null, new NpcDialogueModelResult("Buzz off.", "unknown", "end_conversation", 0, true, "relationship_refusal", [], false));

        var externalKey = $"{Configuration.Id}:npc:{actor.AiDialogueId ?? actor.Id}:player:{playerId}";
        var chatName = $"*{actor.Name} and {player.Name}";
        var turn = new NpcDialogueTurn(
            externalKey,
            chatName,
            $"NPC {actor.Name} ({actor.Subtype}) speaking with player {player.Name}. The NPC is an ordinary mortal resident of the game world. Never claim to perform game actions.",
            new Dictionary<string, object?>
            {
                ["game"] = "AlternativeReality", ["reality_id"] = Configuration.Id,
                ["npc_id"] = actor.AiDialogueId ?? actor.Id, ["actor_id"] = actor.Id, ["player_id"] = playerId,
                ["home_building_id"] = actor.HomeBuildingId
            },
            message,
            interactionId,
            CreateAiNpcContext(player, actor, relationship));
        return new(actor.Id, interactionId, turn, null);
    }

    public async Task<AiNpcDialogueApplied> ApplyAiNpcDialogueAsync(string playerId, string actorId, string interactionId, NpcDialogueModelResult proposed, CancellationToken cancellationToken = default)
    {
        var key = (playerId, actorId);
        if (!_aiDialogueSessions.TryGetValue(key, out var active) || active != interactionId)
            throw new InvalidOperationException("That NPC dialogue turn is no longer active.");
        var actor = FindActor(playerId, actorId);
        if (!_players.TryGetValue(playerId, out var player) || actor is null || actor.HealthHearts <= 0 || !actor.AiDialogueEnabled ||
            actor.LocationId != player.LocationId || actor.Position.Distance2D(player.Position) > 6)
        {
            EndAiNpcDialogue(playerId, actorId);
            throw new InvalidOperationException("That character is no longer available to talk.");
        }

        if (!((ICollection<KeyValuePair<(string Player, string Actor), string>>)_aiDialogueSessions).Remove(new(key, interactionId)))
            throw new InvalidOperationException("That NPC dialogue turn was already applied.");
        (string Dialogue, QuestInteraction? Offer) questReply;
        await _deliveryLock.WaitAsync(cancellationToken);
        try { questReply = CreateAiQuest(playerId, actor, interactionId, proposed); }
        finally { _deliveryLock.Release(); }
        var delta = proposed.UsedAi ? Math.Clamp(proposed.RelationshipDelta, -.05, .05) : 0;
        var effective = Math.Clamp(Relationship(playerId, actorId) + delta, -10, 10);
        var raw = effective - FirstImpressionAdjustment(playerId, actorId);
        _relationships[(playerId, actorId)] = raw;
        await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(playerId, actorId, raw), cancellationToken);
        var end = proposed.EndConversation || effective <= AiNpcRefusalRating || !proposed.UsedAi;
        var farewell = end && (proposed.UsedAi || proposed.ReasonCode == "relationship_refusal")
            ? NpcDialogueFarewells.Choose(effective) : null;
        var dialogue = questReply.Dialogue.Trim();
        // Refusal has no model response: use the farewell as its sole spoken line.
        if (proposed.ReasonCode == "relationship_refusal") { dialogue = farewell!; farewell = null; }
        await _store.SaveNpcDialogueMessageAsync(Configuration.Id, playerId, actor.AiDialogueId ?? actor.Id, interactionId, "npc", dialogue, cancellationToken);
        if (farewell is not null) await _store.SaveNpcDialogueMessageAsync(Configuration.Id, playerId, actor.AiDialogueId ?? actor.Id, interactionId, "farewell", farewell, cancellationToken);
        if (end) EndAiNpcDialogue(playerId, actorId); else BeginConversation(playerId, actorId);
        return new(new NpcDialogueModelResult(dialogue, proposed.PlayerIntent, proposed.NpcIntent, delta, end, proposed.ReasonCode, proposed.MemoryFacts, proposed.UsedAi, farewell),
            new RelationshipState(playerId, actorId, Relationship(playerId, actorId)), questReply.Offer);
    }

    public void EndAiNpcDialogue(string playerId, string actorId)
    {
        _aiDialogueSessions.TryRemove((playerId, actorId), out _);
        _conversations.TryRemove((playerId, actorId), out _);
    }

    private void EndAiNpcDialoguesForActor(string actorId)
    {
        foreach (var key in _aiDialogueSessions.Keys.Where(key => key.Actor == actorId).ToArray()) EndAiNpcDialogue(key.Player, key.Actor);
    }

    private object CreateAiNpcContext(PlayerState player, ActorState actor, double relationship)
    {
        var privateState = GetPrivateState(player.Id);
        var nearbyActors = ActorsAtLocation(player.LocationId).Where(candidate => candidate.Id != actor.Id && candidate.HealthHearts > 0 && candidate.Position.Distance2D(player.Position) <= 35)
            .OrderBy(candidate => candidate.Position.Distance2D(player.Position)).Take(16)
            .Select(candidate => new { candidate.Id, candidate.Name, kind = candidate.Kind.ToString(), candidate.Subtype, distance_meters = Math.Round(candidate.Position.Distance2D(player.Position), 1), friend_rating = Relationship(player.Id, candidate.Id) }).ToArray();
        var nearbyPlayers = _players.Values.Where(candidate => candidate.Id != player.Id && candidate.LocationId == player.LocationId && candidate.Position.Distance2D(player.Position) <= 35)
            .OrderBy(candidate => candidate.Position.Distance2D(player.Position)).Take(8)
            .Select(candidate => new { candidate.Id, candidate.Name, distance_meters = Math.Round(candidate.Position.Distance2D(player.Position), 1), health = candidate.HealthHearts }).ToArray();
        var buildings = player.LocationId == "outdoor"
            ? _baseEntities.Values.Where(entity => entity.Kind == EntityKind.Building && entity.Position.Distance2D(player.Position) <= 50)
                .OrderBy(entity => entity.Position.Distance2D(player.Position)).Take(12)
                .Select(entity => (object)new { entity.Id, name = entity.Properties.GetValueOrDefault("name") ?? entity.Properties.GetValueOrDefault("address") ?? "Building", distance_meters = Math.Round(entity.Position.Distance2D(player.Position), 1) }).ToArray()
            : Array.Empty<object>();
        return new
        {
            schema_version = 2,
            quest_contract = new {
                version = 1, single_response = true, supported_actions = AiQuestActions,
                instructions = "Detect requests for quests or jobs. Include npc_result.quest_request: {requested:true,title:short title,action:one supported action,subject:optional hint}. Otherwise omit it or set requested:false. The game chooses targets, items, locations, rewards, and a 10 minute deadline from acceptance. Never invent their values. Use only the listed placeholders in dialogue. Unsupported or unavailable requests may be declined by the game.",
                placeholders = new[] { "{quest_title}", "{target_name}", "{item_name}", "{quantity}", "{location}", "{reward}", "{time_limit}", "{objective}", "{giver_name}" },
                active_quests = (privateState.Quests ?? []).Where(q => q.Status is "active" or "ready").Select(q => new { q.Title, q.Description, q.Status, q.DeadlineUtc }).ToArray()
            },
            game = new { id = "AlternativeReality", reality_id = Configuration.Id, reality_name = Configuration.Name },
            world = new
            {
                server_time = CurrentServerTime,
                weather = new { Weather.Condition, Weather.TemperatureCelsius, Weather.PrecipitationMillimeters, Weather.WindSpeedKilometersPerHour, Weather.IsDay },
                location_id = player.LocationId,
                terrain = player.Terrain.ToString(),
                nearby_buildings = buildings,
                nearby_actors = nearbyActors,
                nearby_players = nearbyPlayers
            },
            npc = new
            {
                actor.Id, actor.Name, actor.Subtype, actor.HealthHearts, actor.MaximumHealthHearts, actor.EquippedWeapon,
                actor.FactionId, actor.HomeBuildingId, relationship,
                refusal_threshold = AiNpcRefusalRating
            },
            player = new
            {
                player.Id, player.Name, player.HealthHearts, player.MaximumHealthHearts, player.Stamina, player.MaximumStamina,
                player.Water, player.MaximumWater, hunger = player.Survival?.Hunger ?? 0, player.BodyHeat, player.MaximumBodyHeat,
                player.TravelMode, player.EquippedWeapon, player.EquippedHat, player.EquippedShirt, player.EquippedPants, player.EquippedGloves,
                player.WantedLevel, player.GodMode, progression = privateState.Progression,
                testing_cheats = privateState.PlayerTesting,
                inventory = privateState.Inventory.Items.Take(40).Select(item => new { item.ItemType, item.Quantity, item.Quality }).ToArray()
            }
        };
    }
}

public sealed record AiNpcDialoguePreparation(string ActorId, string InteractionId, NpcDialogueTurn? Turn, NpcDialogueModelResult? ImmediateResult);
public sealed record AiNpcDialogueApplied(NpcDialogueModelResult Result, RelationshipState Relationship, QuestInteraction? QuestInteraction = null);
