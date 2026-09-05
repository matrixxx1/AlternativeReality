using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed class RealitySocketHub
{
    private readonly RealityWorld _world;
    private readonly AccountService _accounts;
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();

    public RealitySocketHub(RealityWorld world, AccountService accounts) { _world = world; _accounts = accounts; }

    public async Task AcceptAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("A WebSocket connection is required.");
            return;
        }

        var identity = await _accounts.AuthenticateAsync(context.Request.Cookies[AccountService.CookieName] ?? context.Request.Query["session"].FirstOrDefault(), context.RequestAborted);
        if (identity is null) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; await context.Response.WriteAsync("Account setup is required."); return; }
        var characterId = identity.CharacterId;
        var name = identity.Username;
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connection = new ClientConnection(socket);
        try
        {
            if (_clients.TryRemove(characterId, out var previous)) await previous.CloseAsync("Reconnected from another client.");
            var player = await _world.JoinAsync(characterId, name, identity.AccountId, context.RequestAborted);
            _clients[characterId] = connection;
            await connection.SendAsync(new { type = "welcome", protocolVersion = Protocol.Version, playerId = characterId, snapshot = _world.CreateSnapshot(), privateState = _world.GetPrivateState(characterId) }, context.RequestAborted);
            await BroadcastAsync(new { type = "playerJoined", player }, characterId, context.RequestAborted);
            foreach (var flag in _world.PersonalFlagsForOwner(characterId))
                await BroadcastAsync(new { type = "objectCreated", entity = flag }, characterId, context.RequestAborted);
            await ReceiveLoopAsync(characterId, connection, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (WebSocketException) { }
        finally
        {
            if (_clients.TryGetValue(characterId, out var active) && ReferenceEquals(active, connection) && _clients.TryRemove(characterId, out _))
            {
                var hiddenFlags = _world.PersonalFlagsForOwner(characterId);
                _world.Leave(characterId);
                await BroadcastAsync(new { type = "playerLeft", playerId = characterId }, characterId, CancellationToken.None);
                foreach (var flag in hiddenFlags)
                    await BroadcastAsync(new { type = "objectRemoved", entityId = flag.Id }, characterId, CancellationToken.None);
            }
        }
    }

    private async Task ReceiveLoopAsync(string characterId, ClientConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        while (connection.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await connection.Socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                if (stream.Length + result.Count > 64 * 1024) throw new InvalidDataException("Client message exceeds 64 KiB.");
                stream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text) continue;
            using var document = JsonDocument.Parse(stream.ToArray());
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            try
            {
                switch (type)
                {
                    case "moveRequest":
                        var movement = await _world.MoveAsync(characterId, root.Deserialize<MoveRequest>(SharedJson.Options)!, cancellationToken);
                        if (movement is not null)
                        {
                            if (movement.Moved || movement.Drowned || movement.Fell || movement.Died) await BroadcastAsync(new { type = "playerMoved", player = movement.Player }, null, cancellationToken);
                            if (movement.Blocked) await connection.SendAsync(new { type = "movementBlocked", message = movement.Message ?? "Something is blocking the way." }, cancellationToken);
                            if (movement.Fell) await connection.SendAsync(new { type = "playerFell", message = movement.Message, player = movement.Player }, cancellationToken);
                            if (movement.Died) await connection.SendAsync(new { type = "playerDied", reason = movement.Message, player = movement.Player }, cancellationToken);
                            if (movement.Message is not null && !movement.Blocked && !movement.Fell && !movement.Died) await connection.SendAsync(new { type = "movementNotice", message = movement.Message, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                            if (movement.Player.LocationId != "outdoor") await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        }
                        break;
                    case "setTravelMode":
                        var travelRequest = root.Deserialize<SetTravelModeRequest>(SharedJson.Options)!;
                        var travelPlayer = await _world.SetTravelModeAsync(characterId, travelRequest.Mode, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = travelPlayer }, null, cancellationToken);
                        break;
                    case "setGodMode":
                        var godRequest = root.Deserialize<SetGodModeRequest>(SharedJson.Options)!;
                        var godPlayer = await _world.SetGodModeAsync(characterId, godRequest.Enabled, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = godPlayer }, null, cancellationToken);
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "triggerWorldEvent":
                        var eventRequest = root.Deserialize<TriggerWorldEventRequest>(SharedJson.Options)!;
                        var eventActors = _world.TriggerWorldEvent(characterId, eventRequest.EventType);
                        await BroadcastAsync(new { type = "worldEventTriggered", actor = eventActors[0], actors = eventActors, message = $"God Mode triggered {eventActors[0].EventName}." }, null, cancellationToken);
                        break;
                    case "setLights":
                        var lightRequest=root.Deserialize<SetLightsRequest>(SharedJson.Options)!;var litPlayer=await _world.SetLightsAsync(characterId,lightRequest.FlashlightOn,lightRequest.LanternOn,lightRequest.LaserOn,cancellationToken);await BroadcastAsync(new{type="playerUpdated",player=litPlayer},null,cancellationToken);break;
                    case "setMagicHikingShoes":
                        var shoesRequest = root.Deserialize<SetMagicHikingShoesRequest>(SharedJson.Options)!;
                        var shoesPlayer = await _world.SetMagicHikingShoesAsync(characterId, shoesRequest.Enabled, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = shoesPlayer }, null, cancellationToken);
                        break;
                    case "setMagicRunningShoes":
                        var runningShoesRequest = root.Deserialize<SetMagicRunningShoesRequest>(SharedJson.Options)!;
                        var runningShoesPlayer = await _world.SetMagicRunningShoesAsync(characterId, runningShoesRequest.Enabled, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = runningShoesPlayer }, null, cancellationToken);
                        break;
                    case "setEquipment":
                        var equipmentRequest = root.Deserialize<SetEquipmentRequest>(SharedJson.Options)!;
                        var equipmentPlayer = await _world.SetEquipmentAsync(characterId, equipmentRequest.Slot, equipmentRequest.ItemType, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = equipmentPlayer }, null, cancellationToken);
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "updateItemConfiguration":
                        var itemConfiguration = await _world.UpdateItemConfigurationAsync(characterId, root.Deserialize<UpdateItemConfigurationRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "itemConfigurationUpdated", item = itemConfiguration, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "configureInventoryItem":
                        var inventoryAdjustment = await _world.ConfigureInventoryItemAsync(characterId, root.Deserialize<ConfigureInventoryItemRequest>(SharedJson.Options)!, cancellationToken);
                        if (inventoryAdjustment.Player is not null) await BroadcastAsync(new { type = "playerUpdated", player = inventoryAdjustment.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "configuredInventoryAdjusted", privateState = inventoryAdjustment.PrivateState, message = inventoryAdjustment.Message }, cancellationToken);
                        break;
                    case "updateMovementConfiguration":
                        var movementConfiguration = await _world.UpdateMovementConfigurationAsync(characterId, root.Deserialize<UpdateMovementConfigurationRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "movementConfigurationUpdated", movement = movementConfiguration, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "updateServerEvents":
                        var eventConfiguration = await _world.UpdateServerEventConfigurationAsync(characterId, root.Deserialize<UpdateServerEventsRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "serverEventsUpdated", events = eventConfiguration, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        await BroadcastAsync(new { type = "serverEventsChanged", events = _world.ClientEventConfiguration }, characterId, cancellationToken);
                        await BroadcastWeatherAsync(cancellationToken);
                        break;
                    case "enterDungeon":
                        var enter = root.Deserialize<EnterDungeonRequest>(SharedJson.Options)!;
                        var entered = await _world.EnterDungeonAsync(characterId, enter.DoorId, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = entered.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "dungeonEntered", player = entered.Player, dungeon = entered.Dungeon, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "changeDungeonLevel":
                        var levelRequest = root.Deserialize<ChangeDungeonLevelRequest>(SharedJson.Options)!;
                        var changedLevel = await _world.ChangeDungeonLevelAsync(characterId, levelRequest.Direction, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = changedLevel.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "dungeonLevelChanged", player = changedLevel.Player, dungeon = changedLevel.Dungeon, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "exitDungeon":
                        var exited = await _world.ExitDungeonAsync(characterId, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = exited }, null, cancellationToken);
                        await connection.SendAsync(new { type = "dungeonExited", player = exited, snapshot = _world.CreateSnapshot(), privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "consumeItem":
                        var consume = root.Deserialize<ConsumeItemRequest>(SharedJson.Options)!;
                        var consumingPlayer = await _world.ConsumeItemAsync(characterId, consume.ItemType, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = consumingPlayer }, null, cancellationToken);
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "dropItem":
                        var droppedItem = await _world.DropInventoryItemAsync(characterId, root.Deserialize<DropItemRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = droppedItem.Player }, null, cancellationToken);
                        await BroadcastAsync(new { type = "lootCreated", loot = droppedItem.Drop }, null, cancellationToken);
                        await connection.SendAsync(new { type = "inventoryItemDropped", privateState = droppedItem.PrivateState, message = droppedItem.Message }, cancellationToken);
                        break;
                    case "restAtBed":
                        var rest = root.Deserialize<RestAtBedRequest>(SharedJson.Options)!;
                        var rested = await _world.RestAtBedAsync(characterId, rest.BedId, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = rested }, null, cancellationToken);
                        await connection.SendAsync(new { type = "rested", player = rested, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "moveFurniture":
                        var movedFurniture = await _world.MoveFurnitureAsync(characterId, root.Deserialize<MoveFurnitureRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "homeUpdated", dungeon = movedFurniture, privateState = _world.GetPrivateState(characterId), message = "Furniture moved." }, cancellationToken);
                        break;
                    case "placeFurniture":
                        var placedFurniture = await _world.PlaceFurnitureAsync(characterId, root.Deserialize<PlaceFurnitureRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "homeUpdated", dungeon = placedFurniture, privateState = _world.GetPrivateState(characterId), message = "Furniture placed." }, cancellationToken);
                        break;
                    case "rotateFurniture":
                        var rotatedFurniture = await _world.RotateFurnitureAsync(characterId, root.Deserialize<RotateFurnitureRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "homeUpdated", dungeon = rotatedFurniture, privateState = _world.GetPrivateState(characterId), message = "Furniture rotated 90 degrees." }, cancellationToken);
                        break;
                    case "storeFurniture":
                        var storedFurniture = await _world.StoreFurnitureAsync(characterId, root.Deserialize<StoreFurnitureRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "homeUpdated", dungeon = storedFurniture, privateState = _world.GetPrivateState(characterId), message = "Furniture moved to Home storage." }, cancellationToken);
                        break;
                    case "openHomeStorage":
                        var openedStorage = _world.OpenHomeItemStorage(characterId, root.Deserialize<OpenHomeStorageRequest>(SharedJson.Options)!.ChestId);
                        await connection.SendAsync(new { type = "homeStorageOpened", storage = openedStorage, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "transferHomeStorage":
                        var storageState = await _world.TransferHomeItemAsync(characterId, root.Deserialize<TransferHomeStorageRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = storageState.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "homeStorageUpdated", privateState = storageState.PrivateState, message = "Storage chest updated." }, cancellationToken);
                        break;
                    case "purchaseBase":
                        var basePurchase = await _world.PurchaseBaseAsync(characterId, root.Deserialize<PurchaseBaseRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = basePurchase.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "basePurchased", player = basePurchase.Player, priceCents = basePurchase.PriceCents, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "requestTrade":
                        var tradeRequest = root.Deserialize<RequestTradeRequest>(SharedJson.Options)!;
                        await connection.SendAsync(new { type = "tradeQuote", quote = _world.RequestTrade(characterId, tradeRequest.MerchantId) }, cancellationToken);
                        break;
                    case "confirmTrade":
                        var confirmation = root.Deserialize<ConfirmTradeRequest>(SharedJson.Options)!;
                        var purchase = await _world.ConfirmTradeAsync(characterId, confirmation, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = purchase.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "tradeCompleted", player = purchase.Player, inventory = purchase.Inventory, relationship = purchase.Relationship, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "requestQuest":
                        var requestedQuest = root.Deserialize<RequestQuestRequest>(SharedJson.Options)!;
                        await connection.SendAsync(new { type = "questInteraction", interaction = _world.RequestQuest(characterId, requestedQuest.ActorId) }, cancellationToken);
                        break;
                    case "acceptQuest":
                        var acceptedQuest = await _world.AcceptQuestAsync(characterId, root.Deserialize<AcceptQuestRequest>(SharedJson.Options)!.QuestId, cancellationToken);
                        await connection.SendAsync(new { type = "questUpdated", privateState = acceptedQuest.PrivateState, quest = acceptedQuest.Quest, message = acceptedQuest.Message }, cancellationToken);
                        break;
                    case "completeQuest":
                        var completedQuest = await _world.CompleteQuestAsync(characterId, root.Deserialize<CompleteQuestRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = completedQuest.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "questUpdated", privateState = completedQuest.PrivateState, quest = completedQuest.Quest, message = completedQuest.Message }, cancellationToken);
                        break;
                    case "abandonQuest":
                        var abandonedQuest = await _world.AbandonQuestAsync(characterId, root.Deserialize<AbandonQuestRequest>(SharedJson.Options)!.QuestId, cancellationToken);
                        await connection.SendAsync(new { type = "questUpdated", privateState = abandonedQuest.PrivateState, quest = abandonedQuest.Quest, message = abandonedQuest.Message }, cancellationToken);
                        break;
                    case "captureQuestPet":
                        var capturedPet = await _world.CaptureQuestPetAsync(characterId, root.Deserialize<CaptureQuestPetRequest>(SharedJson.Options)!.ActorId, cancellationToken);
                        await BroadcastAsync(new { type = "actorRemoved", actorId = root.Deserialize<CaptureQuestPetRequest>(SharedJson.Options)!.ActorId }, null, cancellationToken);
                        await connection.SendAsync(new { type = "questUpdated", privateState = capturedPet.PrivateState, quest = capturedPet.Quest, message = capturedPet.Message }, cancellationToken);
                        break;
                    case "chopVegetation":
                        var chopped = await _world.ChopVegetationAsync(characterId, root.Deserialize<ChopVegetationRequest>(SharedJson.Options)!.EntityId, cancellationToken);
                        await BroadcastAsync(new { type = "vegetationChopped", entityId = chopped.EntityId }, null, cancellationToken);
                        if (chopped.WitnessMessage is not null) await BroadcastAsync(new { type = "chatSaid", chat = chopped.WitnessMessage }, null, cancellationToken);
                        await connection.SendAsync(new { type = "questUpdated", privateState = chopped.PrivateState, message = chopped.Message }, cancellationToken);
                        break;
                    case "attackWorldObject":
                        var worldCrime = await _world.AttackWorldObjectAsync(characterId, root.Deserialize<AttackWorldObjectRequest>(SharedJson.Options)!.EntityId, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = worldCrime.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "crimeReported", privateState = worldCrime.PrivateState, message = worldCrime.Message }, cancellationToken);
                        break;
                    case "pickLock":
                        var lockResult = await _world.PickLockAsync(characterId, root.Deserialize<PickLockRequest>(SharedJson.Options)!.DoorId, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = lockResult.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "lockPickResult", doorId = root.Deserialize<PickLockRequest>(SharedJson.Options)!.DoorId, success = lockResult.Success, policeCalled = lockResult.PoliceCalled, privateState = lockResult.PrivateState, message = lockResult.Message }, cancellationToken);
                        break;
                    case "attack":
                        var attack = await _world.AttackAsync(characterId, root.Deserialize<CombatRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "combatEvent", combat = attack.Event }, null, cancellationToken);
                        if (attack.Consequences is not null) foreach (var consequence in attack.Consequences) await BroadcastAsync(new { type = "combatEvent", combat = consequence }, null, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = attack.Attacker }, null, cancellationToken);
                        if (attack.Consequences?.Count > 0) await connection.SendAsync(new { type = "playerDied", reason = "You killed a missing quest pet. Its owner retaliated.", player = attack.Attacker, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        if (attack.TargetPlayer is not null)
                        {
                            await BroadcastAsync(new { type = "playerUpdated", player = attack.TargetPlayer }, null, cancellationToken);
                            if (attack.Event.TargetDied && _clients.TryGetValue(attack.TargetPlayer.Id, out var defeatedConnection))
                                await defeatedConnection.SendAsync(new { type = "playerDied", reason = attack.Event.Message, player = attack.TargetPlayer, privateState = _world.GetPrivateState(attack.TargetPlayer.Id) }, cancellationToken);
                        }
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        if (attack.Dungeon is not null) await connection.SendAsync(new { type = "dungeonUpdated", dungeon = attack.Dungeon }, cancellationToken);
                        break;
                    case "openChest":
                        var chestRequest = root.Deserialize<OpenChestRequest>(SharedJson.Options)!;
                        var reward = await _world.OpenChestAsync(characterId, chestRequest.ChestId, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = reward.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "chestOpened", contents = reward.Contents, message = reward.Message, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "takeChestItems":
                        var chestTake = await _world.TakeChestItemsAsync(characterId, root.Deserialize<TakeChestItemsRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "chestItemsTaken", contents = chestTake.Contents, chestRemoved = chestTake.ChestRemoved, chestId = root.Deserialize<TakeChestItemsRequest>(SharedJson.Options)!.ChestId, message = chestTake.Message, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "chestSeen":
                        var seen = root.Deserialize<ChestSeenRequest>(SharedJson.Options)!;
                        await connection.SendAsync(new { type = "chestUpdated", chest = _world.MarkChestSeen(characterId, seen.ChestId) }, cancellationToken);
                        break;
                    case "collectLoot":
                        var lootId = root.GetProperty("lootId").GetString() ?? string.Empty;
                        var collected = await _world.CollectLootAsync(characterId, lootId, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = collected.Player }, null, cancellationToken);
                        await BroadcastAsync(new { type = "lootRemoved", lootId }, null, cancellationToken);
                        await connection.SendAsync(new { type = "lootCollected", lootId, message = collected.Message, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "rebuildArea":
                        var rebuildRequest = root.Deserialize<RebuildAreaRequest>(SharedJson.Options)!;
                        var rebuilt = await _world.RebuildAsync(characterId, rebuildRequest.GodMode, cancellationToken);
                        await BroadcastWorldRebuiltAsync(rebuilt, cancellationToken);
                        break;
                    case "teleport":
                        var teleportRequest = root.Deserialize<TeleportRequest>(SharedJson.Options)!;
                        var needsTeleportArea = !_world.IsAreaLoaded(teleportRequest.X, teleportRequest.Y);
                        if (needsTeleportArea) await connection.SendAsync(new { type = "taskStatus", task = "Loading and generating teleport destination…" }, cancellationToken);
                        var teleport = await _world.TeleportWithAreaAsync(characterId, teleportRequest, cancellationToken);
                        if (teleport.Expanded) await connection.SendAsync(new { type = "worldExpanded", expanded = true, snapshot = _world.CreateSnapshot() }, cancellationToken);
                        await BroadcastAsync(new { type = "playerTeleported", player = teleport.Player }, null, cancellationToken);
                        break;
                    case "mapFastTravel":
                        await connection.SendAsync(new { type = "taskStatus", task = "Preparing mini-map fast travel…" }, cancellationToken);
                        var fastTravel = await _world.MapFastTravelAsync(characterId, root.Deserialize<MapFastTravelRequest>(SharedJson.Options)!, cancellationToken);
                        if (fastTravel.Expanded) await connection.SendAsync(new { type = "worldExpanded", expanded = true, snapshot = _world.CreateSnapshot() }, cancellationToken);
                        await BroadcastAsync(new { type = "playerTeleported", player = fastTravel.Player }, null, cancellationToken);
                        break;
                    case "say":
                        var chat = _world.Say(characterId, root.Deserialize<SayRequest>(SharedJson.Options)!);
                        await BroadcastAsync(new { type = "chatSaid", chat }, null, cancellationToken);
                        break;
                    case "pathRequest":
                        var pathRequest = root.Deserialize<PathRequest>(SharedJson.Options)!;
                        await connection.SendAsync(new { type = "taskStatus", task = _world.IsAreaLoadRequiredForPath(characterId, pathRequest.X, pathRequest.Y) ? "Loading area and finding route…" : "Finding route…" }, cancellationToken);
                        var pathResult = await _world.FindPathAsync(characterId, pathRequest, cancellationToken);
                        if (pathResult.Expanded) await connection.SendAsync(new { type = "worldExpanded", snapshot = _world.CreateSnapshot() }, cancellationToken);
                        if (pathResult.Result.Success)
                            await connection.SendAsync(new { type = "pathResult", sequence = pathRequest.Sequence, waypoints = pathResult.Result.Waypoints }, cancellationToken);
                        else
                            await connection.SendAsync(new { type = "pathUnavailable", sequence = pathRequest.Sequence, message = pathResult.Result.Message }, cancellationToken);
                        break;
                    case "placeObject":
                        var created = await _world.PlaceObjectAsync(characterId, root.Deserialize<PlaceObjectRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "objectCreated", entity = created }, null, cancellationToken);
                        break;
                    case "placeFlag":
                        var placedFlag = await _world.PlaceFlagAsync(characterId, root.Deserialize<PlaceFlagRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "objectCreated", entity = placedFlag }, characterId, cancellationToken);
                        await connection.SendAsync(new { type = "flagPlaced", entity = placedFlag, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "removeObject":
                        var removedRequest = root.Deserialize<RemoveObjectRequest>(SharedJson.Options)!;
                        var removed = await _world.RemoveObjectAsync(characterId, removedRequest.EntityId, cancellationToken);
                        await BroadcastAsync(new { type = "objectRemoved", entityId = removed.Id }, null, cancellationToken);
                        break;
                    case "requestChunk":
                        await connection.SendAsync(new { type = "chunkSnapshot", snapshot = _world.CreateSnapshot() }, cancellationToken);
                        break;
                    case "requestArea":
                        var requestedArea=root.Deserialize<RequestAreaRequest>(SharedJson.Options)!;await connection.SendAsync(new{type="taskStatus",task="Loading and generating visible area…"},cancellationToken);var areaExpanded=await _world.LoadAreaAsync(requestedArea.X,requestedArea.Y,cancellationToken);await connection.SendAsync(new{type="worldExpanded",expanded=areaExpanded,snapshot=_world.CreateSnapshot()},cancellationToken);break;
                    case "requestPrivateState":
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "ping":
                        await connection.SendAsync(new { type = "pong", serverTime = DateTimeOffset.UtcNow }, cancellationToken);
                        break;
                    default:
                        await connection.SendAsync(new { type = "error", message = "Unknown message type." }, cancellationToken);
                        break;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException or HttpRequestException)
            {
                await connection.SendAsync(new { type = "error", message = exception.Message }, cancellationToken);
            }
        }
    }

    private async Task BroadcastAsync(object message, string? exceptCharacterId, CancellationToken cancellationToken)
    {
        var sends = _clients
            .Where(pair => pair.Key != exceptCharacterId)
            .Select(async pair =>
            {
                try { await pair.Value.SendAsync(message, cancellationToken); }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
            });
        await Task.WhenAll(sends);
    }

    private async Task BroadcastWorldRebuiltAsync(WorldSnapshot snapshot, CancellationToken cancellationToken)
    {
        var sends = _clients.Select(async pair =>
        {
            try { await pair.Value.SendAsync(new { type = "worldRebuilt", snapshot, privateState = _world.GetPrivateState(pair.Key) }, cancellationToken); }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
        });
        await Task.WhenAll(sends);
    }

    public Task BroadcastWeatherAsync(CancellationToken cancellationToken = default) =>
        BroadcastAsync(new { type = "weatherChanged", weather = _world.Weather }, null, cancellationToken);

    public Task BroadcastActorsAsync(IReadOnlyList<ActorState> actors, CancellationToken cancellationToken = default) =>
        actors.Count == 0 ? Task.CompletedTask : BroadcastAsync(new { type = "actorsMoved", actors }, null, cancellationToken);

    public Task BroadcastPlayersAsync(IReadOnlyList<PlayerState> players, CancellationToken cancellationToken = default) =>
        players.Count == 0 ? Task.CompletedTask : BroadcastAsync(new { type = "playersUpdated", players }, null, cancellationToken);

    public Task BroadcastDoorLocksAsync(DoorLockSchedule schedule, CancellationToken cancellationToken = default) =>
        BroadcastAsync(new { type = "doorLocksChanged", doorLocks = schedule.Doors, doorLockCycleEndsAtUtc = schedule.EndsAtUtc }, null, cancellationToken);

    public async Task BroadcastCombatAsync(IReadOnlyList<CombatEvent> combat, CancellationToken cancellationToken = default)
    {
        foreach (var item in combat) await BroadcastAsync(new { type = "combatEvent", combat = item }, null, cancellationToken);
    }

    public async Task BroadcastChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        foreach (var chat in messages)
            await BroadcastAsync(new { type = "chatSaid", chat }, null, cancellationToken);
    }

    private static string NormalizeCharacterId(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed.ToString("N") : Guid.NewGuid().ToString("N");

    private sealed class ClientConnection
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        public ClientConnection(WebSocket socket) => Socket = socket;
        public WebSocket Socket { get; }

        public async Task SendAsync(object message, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(message, SharedJson.Options);
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                if (Socket.State == WebSocketState.Open)
                    await Socket.SendAsync(json, WebSocketMessageType.Text, true, cancellationToken);
            }
            finally { _sendLock.Release(); }
        }

        public async Task CloseAsync(string reason)
        {
            if (Socket.State == WebSocketState.Open)
                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
        }
    }
}
