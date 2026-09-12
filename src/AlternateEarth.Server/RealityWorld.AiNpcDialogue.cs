using System.Collections.Concurrent;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    public const double AiNpcRefusalRating = -5;
    private readonly ConcurrentDictionary<(string Player, string Actor), string> _aiDialogueSessions = new();

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

        var delta = proposed.UsedAi ? Math.Clamp(proposed.RelationshipDelta, -.05, .05) : 0;
        var effective = Math.Clamp(Relationship(playerId, actorId) + delta, -10, 10);
        var raw = effective - FirstImpressionAdjustment(playerId, actorId);
        _relationships[(playerId, actorId)] = raw;
        await _store.SaveRelationshipAsync(Configuration.Id, new RelationshipState(playerId, actorId, raw), cancellationToken);
        var end = proposed.EndConversation || effective <= AiNpcRefusalRating || !proposed.UsedAi;
        var dialogue = effective <= AiNpcRefusalRating && proposed.UsedAi ? "Buzz off." : proposed.Dialogue.Trim();
        if (end) EndAiNpcDialogue(playerId, actorId); else BeginConversation(playerId, actorId);
        return new(new NpcDialogueModelResult(dialogue, proposed.PlayerIntent, proposed.NpcIntent, delta, end, proposed.ReasonCode, proposed.MemoryFacts, proposed.UsedAi),
            new RelationshipState(playerId, actorId, Relationship(playerId, actorId)));
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
            schema_version = 1,
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
public sealed record AiNpcDialogueApplied(NpcDialogueModelResult Result, RelationshipState Relationship);
