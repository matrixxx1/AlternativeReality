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
    private readonly LocalAiNpcDialogueService _localAi;
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();

    public RealitySocketHub(RealityWorld world, AccountService accounts, LocalAiNpcDialogueService localAi) { _world = world; _accounts = accounts; _localAi = localAi; }

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
        PlayerState? connectedPlayer = null;
        try
        {
            if (_clients.TryRemove(characterId, out var previous)) await previous.CloseAsync("Reconnected from another client.");
            await connection.SendAsync(new { type = "sessionLoading", message = "Loading your character and surroundings…" }, context.RequestAborted);
            await _accounts.MarkSeenAsync(identity.AccountId, context.RequestAborted);
            var player = await _world.JoinAsync(characterId, name, identity.AccountId, context.RequestAborted);
            connectedPlayer = player;
            _clients[characterId] = connection;
            await connection.SendAsync(new { type = "sessionLoading", message = "Preparing the world view…" }, context.RequestAborted);
            await connection.SendAsync(new { type = "welcome", protocolVersion = Protocol.Version, playerId = characterId, snapshot = _world.CreateClientSnapshot(characterId, connection.MapView), privateState = _world.GetPrivateState(characterId), homeNotice = _world.TakeHomeNotice(characterId) }, context.RequestAborted);
            await BroadcastAsync(new { type = "playerJoined", player }, characterId, context.RequestAborted);
            await BroadcastAsync(new { type = "chatSaid", chat = new ChatMessage($"presence:{Guid.NewGuid():N}", characterId, "Server", $"{player.Name} entered the reality.", DateTimeOffset.UtcNow) }, null, context.RequestAborted);
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
                await _world.LeaveAsync(characterId, CancellationToken.None);
                await BroadcastAsync(new { type = "playerLeft", playerId = characterId }, characterId, CancellationToken.None);
                if (connectedPlayer is not null)
                    await BroadcastAsync(new { type = "chatSaid", chat = new ChatMessage($"presence:{Guid.NewGuid():N}", characterId, "Server", $"{connectedPlayer.Name} left the reality.", DateTimeOffset.UtcNow) }, characterId, CancellationToken.None);
                foreach (var flag in hiddenFlags)
                    await BroadcastAsync(new { type = "objectRemoved", entityId = flag.Id }, characterId, CancellationToken.None);
            }
        }
    }

    private async Task ReceiveLoopAsync(string characterId, ClientConnection connection, CancellationToken cancellationToken)
    {
        await using var routeWorker = new LatestCommandWorker();
        await using var areaWorker = new LatestCommandWorker();
        await using var mapWorker = new LatestCommandWorker();
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
            // Only known command names become metric keys; untrusted input cannot grow the metrics table.
            using var commandTiming = _world.Timings.Measure(type is "ping" or "moveRequest" or "requestArea" or "requestMapWindow" or "requestChunk"
                ? "command." + type : "command.other");
            try
            {
                if ((_world.IsProbulatorAbducted(characterId) || _world.IsGasAsleep(characterId)) && type is not ("ping" or "say" or "requestPrivateState" or "requestArea" or "requestMapWindow" or "prefetchArea" or "requestChunk" or "cancelCommand" or "setActionMode"))
                    throw new InvalidOperationException("You cannot act while abducted or asleep.");
                switch (type)
                {
                    case "taunt":
                        var taunt = await _world.TauntAsync(characterId, root.GetProperty("targetId").GetString() ?? string.Empty, cancellationToken);
                        await BroadcastChatAsync(new[] { taunt.Chat }, cancellationToken);
                        await SendRelationshipsAsync(taunt.Relationships, cancellationToken);
                        break;
                    case "setActionMode":
                        var actionMode = _world.SetActionMode(characterId, root.GetProperty("mode").GetString() ?? string.Empty);
                        await connection.SendAsync(new { type = "actionModeChanged", mode = actionMode }, cancellationToken);
                        break;
                    case "cancelCommand":
                        routeWorker.Cancel();
                        var stoppedAttack = _world.CancelPlayerCommand(characterId);
                        if (stoppedAttack is not null) await BroadcastAsync(new { type = "combatEvent", combat = stoppedAttack }, null, cancellationToken);
                        break;
                    case "moveRequest":
                        var moveRequest = root.Deserialize<MoveRequest>(SharedJson.Options)!;
                        var movement = await _world.MoveAsync(characterId, moveRequest, cancellationToken);
                        if (movement is not null)
                        {
                            if (movement.Moved || movement.Drowned || movement.Fell || movement.Died || movement.Message is not null) await BroadcastAsync(new { type = "playerMoved", player = movement.Player }, null, cancellationToken);
                            else await connection.SendAsync(new { type = "playerMoved", player = movement.Player }, cancellationToken);
                            if (movement.Blocked) await connection.SendAsync(new { type = "movementBlocked", sequence = moveRequest.Sequence, message = movement.Message ?? "Something is blocking the way." }, cancellationToken);
                            if (movement.Fell) await connection.SendAsync(new { type = "playerFell", message = movement.Message, player = movement.Player }, cancellationToken);
                            if (movement.Message is not null && !movement.Blocked && !movement.Fell && !movement.Died) await connection.SendAsync(new { type = "movementNotice", message = movement.Message, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                            if (movement.Player.LocationId != "outdoor") await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        }
                        break;
                    case "waitForBus":
                        var waitingPlayer = await _world.WaitForBusAsync(characterId, root.Deserialize<WaitForBusRequest>(SharedJson.Options)!.StopId, cancellationToken);
                        await BroadcastPlayersAsync(new[] { waitingPlayer }, cancellationToken);
                        break;
                    case "cancelBusWait":
                        await BroadcastPlayersAsync(new[] { await _world.CancelBusWaitAsync(characterId, cancellationToken) }, cancellationToken);
                        break;
                    case "boardBus":
                        await BroadcastPlayersAsync(new[] { await _world.BoardBusAsync(characterId, root.GetProperty("busId").GetString()!, cancellationToken) }, cancellationToken);
                        await BroadcastAsync(new { type = "busesMoved", buses = _world.GetTransitSnapshot().Buses }, null, cancellationToken);
                        break;
                    case "getOffBus":
                        await BroadcastPlayersAsync(new[] { await _world.GetOffBusAsync(characterId, cancellationToken) }, cancellationToken);
                        await BroadcastAsync(new { type = "busesMoved", buses = _world.GetTransitSnapshot().Buses }, null, cancellationToken);
                        break;
                    case "requestBusRoute":
                        var routeId = root.GetProperty("routeId").GetString();
                        var routeSnapshot = _world.GetTransitSnapshot();
                        var requestedRoute = routeSnapshot.Routes.FirstOrDefault(r => r.Id == routeId) ?? throw new InvalidOperationException("This bus route is no longer available.");
                        var requestedStops = requestedRoute.StopIds.ToHashSet();
                        var routeBuses = routeSnapshot.Buses.Where(b => b.RouteId == requestedRoute.Id).ToArray();
                        if (root.TryGetProperty("positionsOnly", out var positionsOnly) && positionsOnly.ValueKind == JsonValueKind.True)
                            await connection.SendAsync(new { type = "busRoutePositions", routeId = requestedRoute.Id, buses = routeBuses }, cancellationToken);
                        else
                            await connection.SendAsync(new { type = "busRoute", route = requestedRoute, stops = routeSnapshot.Stops.Where(s => requestedStops.Contains(s.Id)).ToArray(), buses = routeBuses }, cancellationToken);
                        break;
                    case "setTravelMode":
                        var travelRequest = root.Deserialize<SetTravelModeRequest>(SharedJson.Options)!;
                        var travelPlayer = await _world.SetTravelModeAsync(characterId, travelRequest.Mode, cancellationToken, connection.MapView);
                        await BroadcastAsync(new { type = "playerUpdated", player = travelPlayer }, null, cancellationToken);
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "assignStats":
                        var statPlayer = await _world.AssignStatsAsync(characterId, root.Deserialize<AssignStatsRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = statPlayer }, null, cancellationToken);
                        await connection.SendAsync(new { type = "statsAssigned", privateState = _world.GetPrivateState(characterId), notices = new[] { new ProgressionNotice(characterId, "Stat points saved.") } }, cancellationToken);
                        break;
                    case "setGodMode":
                        var godRequest = root.Deserialize<SetGodModeRequest>(SharedJson.Options)!;
                        var godPlayer = await _world.SetGodModeAsync(characterId, godRequest.Enabled, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = godPlayer }, null, cancellationToken);
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "updatePlayerTesting":
                        var testingRequest = root.Deserialize<UpdatePlayerTestingRequest>(SharedJson.Options)!;
                        await _world.UpdatePlayerTestingAsync(characterId, testingRequest.Settings, cancellationToken);
                        await connection.SendAsync(new { type = "playerTestingUpdated", privateState = _world.GetPrivateState(characterId), message = "Player testing rules saved." }, cancellationToken);
                        break;
                    case "placeTestCharacter":
                        var placedTest = _world.PlaceTestCharacter(characterId, root.Deserialize<PlaceTestCharacterRequest>(SharedJson.Options)!);
                        if (placedTest.Player is not null) await BroadcastPlayersAsync(new[] { placedTest.Player }, cancellationToken);
                        if (placedTest.Actor is not null) await BroadcastActorsAsync(new[] { placedTest.Actor }, cancellationToken);
                        await connection.SendAsync(new { type = "testCharacterPlaced", message = placedTest.Message }, cancellationToken);
                        break;
                    case "clearTestCharacters":
                        var clearedTests = _world.ClearTestCharacters(characterId);
                        await BroadcastAsync(new { type = "testCharactersCleared", ids = clearedTests, ownerId = characterId }, null, cancellationToken);
                        break;
                    case "triggerWorldEvent":
                    case "startServerVote":
                        _world.StartServerVote(characterId);
                        await BroadcastInversionsAsync(cancellationToken);
                        break;
                    case "cancelServerVote":
                        _world.CancelServerVote(characterId);
                        await BroadcastInversionsAsync(cancellationToken);
                        break;
                    case "castServerVote":
                        _world.CastServerVote(characterId, root.GetProperty("option").GetString()!);
                        await BroadcastInversionsAsync(cancellationToken);
                        break;
                    case "reflectEventMissile":
                        _world.ReflectEventMissile(characterId, root.GetProperty("missileId").GetString()!);
                        break;
                    case "setAchievementTitle":
                        await _world.SetAchievementTitleAsync(characterId, root.GetProperty("title").GetString()!, cancellationToken);
                        break;
                    case "photograph":
                        await _world.PhotographAsync(characterId, root.TryGetProperty("subjectId", out var photoSubject) ? photoSubject.GetString() : null, cancellationToken,
                            root.TryGetProperty("thumbnailDataUrl", out var photoThumbnail) ? photoThumbnail.GetString() : null);
                        await connection.SendAsync(new { type = "questUpdated", privateState = _world.GetPrivateState(characterId), message = "Photograph taken." }, cancellationToken);
                        break;
                    case "adventureAction":
                        var adventureResult = await _world.AdventureActionAsync(characterId, root.GetProperty("questId").GetString()!, root.GetProperty("choice").GetString()!, cancellationToken);
                        await connection.SendAsync(new { type = "questUpdated", player = adventureResult.Player, privateState = adventureResult.PrivateState, message = adventureResult.Message }, cancellationToken);
                        break;
                    case "enterEventDungeon":
                        await _world.EnterEventDungeonAsync(characterId, cancellationToken);
                        await connection.SendAsync(new { type = "dungeonEntered", player = _world.GetRetroBattleUpdate(characterId).Player, privateState = _world.GetPrivateState(characterId), dungeon = _world.GetPrivateState(characterId).Dungeon }, cancellationToken);
                        break;
                    case "eventBattleAction":
                        var eventBattleDungeon = await _world.PlayEventBattleAsync(characterId, root.GetProperty("action").GetString()!, cancellationToken);
                        await connection.SendAsync(new { type = "eventBattleUpdated", player = _world.GetRetroBattleUpdate(characterId).Player, dungeon = eventBattleDungeon, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "configureEventDeck":
                        _world.ConfigureEventDeck(characterId, root.GetProperty("cards").Deserialize<string[]>(SharedJson.Options)!);
                        await connection.SendAsync(new { type = "eventBattleUpdated", dungeon = _world.GetPrivateState(characterId).Dungeon, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "attackDungeonBarrier":
                        var barrierDungeon = await _world.AttackDungeonBarrierAsync(characterId, root.GetProperty("barrierId").GetString()!, cancellationToken);
                        await connection.SendAsync(new { type = "eventBattleUpdated", player = _world.GetRetroBattleUpdate(characterId).Player, dungeon = barrierDungeon, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "retroJump":
                        _world.JumpRetroBattle(characterId);
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
                    case "setWeaponMode":
                        var modePlayer = await _world.SetWeaponModeAsync(characterId, root.Deserialize<SetWeaponModeRequest>(SharedJson.Options)!.Mode, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = modePlayer }, null, cancellationToken);
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
                        await BroadcastDoorLocksAsync(_world.GetDoorLockSchedule(), cancellationToken);
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
                        await connection.SendAsync(new { type = "dungeonExited", player = exited, snapshot = _world.CreateClientSnapshot(characterId, connection.MapView), privateState = _world.GetPrivateState(characterId) }, cancellationToken);
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
                    case "transferHomeMoney":
                        var moneyState = await _world.TransferHomeMoneyAsync(characterId, root.Deserialize<TransferHomeMoneyRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = moneyState.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "homeStorageUpdated", privateState = moneyState.PrivateState, message = moneyState.Message }, cancellationToken);
                        break;
                    case "transferPostOfficeItem":
                        var postalState = await _world.TransferPostOfficeItemAsync(characterId, root.Deserialize<TransferPostOfficeItemRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = postalState.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "postalTransferCompleted", privateState = postalState.PrivateState, message = postalState.Message }, cancellationToken);
                        break;
                    case "purchaseBase":
                        var basePurchase = await _world.PurchaseBaseAsync(characterId, root.Deserialize<PurchaseBaseRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = basePurchase.Player }, null, cancellationToken);
                        await BroadcastAsync(new { type = "publicBasesChanged", publicBases = _world.CreateSnapshot().PublicBases }, null, cancellationToken);
                        await BroadcastDoorLocksAsync(_world.GetDoorLockSchedule(), cancellationToken);
                        await connection.SendAsync(new { type = "basePurchased", player = basePurchase.Player, priceCents = basePurchase.PriceCents, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "conversationStatus":
                        var conversationActorId = root.GetProperty("actorId").GetString() ?? string.Empty;
                        var conversationActive = root.GetProperty("active").GetBoolean();
                        _world.UpdateConversation(characterId, conversationActorId, conversationActive);
                        if (!conversationActive) _world.EndAiNpcDialogue(characterId, conversationActorId);
                        break;
                    case "npcDialogue":
                        var npcDialogue = root.Deserialize<NpcDialogueRequest>(SharedJson.Options)!;
                        _ = HandleNpcDialogueAsync(characterId, connection, npcDialogue, cancellationToken);
                        break;
                    case "requestTrade":
                        var tradeRequest = root.Deserialize<RequestTradeRequest>(SharedJson.Options)!;
                        await connection.SendAsync(new { type = "tradeQuote", quote = _world.RequestTrade(characterId, tradeRequest.MerchantId) }, cancellationToken);
                        break;
                    case "requestHomeShop":
                        var homeShop = await _world.RequestHomeShopAsync(characterId, root.Deserialize<RequestHomeShopRequest>(SharedJson.Options)!.FurnitureId, cancellationToken);
                        await connection.SendAsync(new { type = "homeShopOpened", shop = homeShop }, cancellationToken);
                        break;
                    case "setHomeShopListing":
                        var listed = await _world.SetHomeShopListingAsync(characterId, root.Deserialize<SetHomeShopListingRequest>(SharedJson.Options)!, cancellationToken);
                        await connection.SendAsync(new { type = "homeShopUpdated", shop = listed.Shop, player = listed.Player, privateState = listed.PrivateState, message = listed.Message }, cancellationToken);
                        break;
                    case "purchaseHomeShop":
                        var homePurchase = await _world.PurchaseHomeShopAsync(characterId, root.Deserialize<PurchaseHomeShopRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = homePurchase.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "homeShopUpdated", shop = homePurchase.Shop, player = homePurchase.Player, privateState = homePurchase.PrivateState, message = homePurchase.Message }, cancellationToken);
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
                        if (acceptedQuest.Quest.DeliveryRecipient is { } recipient) await BroadcastActorsAsync(new[] { recipient }, cancellationToken);
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
                    case "useFarmAnimal":
                        var farmAction=await _world.UseFarmAnimalAsync(characterId,root.GetProperty("entityId").GetString()!,root.GetProperty("action").GetString()!,cancellationToken);
                        if(farmAction.Result.Entity is not null)await BroadcastAsync(new {type="worldObjectUpdated",entity=farmAction.Result.Entity},null,cancellationToken);
                        if(farmAction.Remark is not null)await BroadcastAsync(new {type="chatSaid",chat=farmAction.Remark},null,cancellationToken);
                        await connection.SendAsync(new {type="gardenResult",privateState=farmAction.Result.PrivateState,message=farmAction.Result.Message},cancellationToken);
                        break;
                    case "drinkHose":
                        var hoseDrink=await _world.DrinkHoseAsync(characterId,root.GetProperty("entityId").GetString()!,cancellationToken);
                        await BroadcastAsync(new {type="playerUpdated",player=hoseDrink.Player},null,cancellationToken);
                        if(hoseDrink.Remark is not null)await BroadcastAsync(new {type="chatSaid",chat=hoseDrink.Remark},null,cancellationToken);
                        await connection.SendAsync(new {type="gardenResult",privateState=_world.GetPrivateState(characterId),message=hoseDrink.Remark is null?"The hose water was clean this time.":hoseDrink.Remark.Message+" You have parasites."},cancellationToken);
                        break;
                    case "useKitchenSink":
                        var sinkAction=root.GetProperty("action").GetString()!;
                        var sinkPlayer=await _world.UseKitchenSinkAsync(characterId,root.GetProperty("sinkId").GetString()!,sinkAction,cancellationToken);
                        await BroadcastAsync(new {type="playerUpdated",player=sinkPlayer},null,cancellationToken);
                        await connection.SendAsync(new {type="gardenResult",privateState=_world.GetPrivateState(characterId),message=sinkAction=="drink"?"Drank purified water from the kitchen sink.":"Filled your container with purified water."},cancellationToken);
                        break;
                    case "requestGardenBuild":
                        await connection.SendAsync(new {type="gardenBuildOptions",garden=_world.RequestGardenBuild(characterId)},cancellationToken);
                        break;
                    case "buildGarden":
                        var gardenBuilt=await _world.BuildGardenAsync(characterId,root.Deserialize<BuildGardenRequest>(SharedJson.Options)!,cancellationToken);
                        if(gardenBuilt.Entity is not null)await BroadcastAsync(new {type="worldObjectUpdated",entity=gardenBuilt.Entity},null,cancellationToken);
                        await connection.SendAsync(new {type="gardenResult",privateState=gardenBuilt.PrivateState,message=gardenBuilt.Message},cancellationToken);
                        break;
                    case "harvestGarden":
                        var harvest=await _world.HarvestGardenAsync(characterId,root.GetProperty("entityId").GetString()!,cancellationToken);
                        await BroadcastAsync(new {type="worldObjectUpdated",entity=harvest.Entity},null,cancellationToken);
                        await connection.SendAsync(new {type="gardenResult",privateState=harvest.PrivateState,message=harvest.Message},cancellationToken);
                        break;
                    case "gatherWild":
                        var resourceId = root.GetProperty("entityId").GetString()!;
                        var gathered = await _world.GatherWildAsync(characterId,resourceId,cancellationToken);
                        await BroadcastAsync(new { type = "vegetationChopped", entityId = resourceId },null,cancellationToken);
                        await connection.SendAsync(new {type="questUpdated",privateState=gathered.PrivateState,message=gathered.Message},cancellationToken);
                        break;
                    case "chopVegetation":
                        var chopped = await _world.ChopVegetationAsync(characterId, root.Deserialize<ChopVegetationRequest>(SharedJson.Options)!.EntityId, cancellationToken);
                        await BroadcastAsync(new { type = "vegetationChopped", entityId = chopped.EntityId }, null, cancellationToken);
                        if (chopped.WitnessMessage is not null) await BroadcastAsync(new { type = "chatSaid", chat = chopped.WitnessMessage }, null, cancellationToken);
                        await connection.SendAsync(new { type = "questUpdated", privateState = chopped.PrivateState, message = chopped.Message }, cancellationToken);
                        break;
                    case "attackWorldObject":
                        var worldCrime = await _world.AttackWorldObjectAsync(characterId, root.Deserialize<AttackWorldObjectRequest>(SharedJson.Options)!.EntityId, cancellationToken);
                        if (worldCrime.Combat is not null) await BroadcastAsync(new { type = "combatEvent", combat = worldCrime.Combat }, null, cancellationToken);
                        await BroadcastAsync(new { type = "worldObjectUpdated", entity = worldCrime.Entity }, null, cancellationToken);
                        if (worldCrime.WitnessMessage is not null) await BroadcastAsync(new { type = "chatSaid", chat = worldCrime.WitnessMessage }, null, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = worldCrime.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "crimeReported", privateState = worldCrime.PrivateState, message = worldCrime.Message }, cancellationToken);
                        break;
                    case "sprayPaintVehicle":
                        var paintedCar = await _world.SprayPaintVehicleAsync(characterId, root.Deserialize<SprayPaintVehicleRequest>(SharedJson.Options)!.EntityId, cancellationToken);
                        await BroadcastAsync(new { type = "worldObjectUpdated", entity = paintedCar.Entity }, null, cancellationToken);
                        if (paintedCar.WitnessMessage is not null) await BroadcastAsync(new { type = "chatSaid", chat = paintedCar.WitnessMessage }, null, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = paintedCar.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "crimeReported", privateState = paintedCar.PrivateState, message = paintedCar.Message }, cancellationToken);
                        break;
                    case "pickLock":
                        var lockResult = await _world.PickLockAsync(characterId, root.Deserialize<PickLockRequest>(SharedJson.Options)!.DoorId, cancellationToken);
                        if (lockResult.WitnessMessage is not null) await BroadcastAsync(new { type = "chatSaid", chat = lockResult.WitnessMessage }, null, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = lockResult.Player }, null, cancellationToken);
                        await connection.SendAsync(new { type = "lockPickResult", doorId = root.Deserialize<PickLockRequest>(SharedJson.Options)!.DoorId, success = lockResult.Success, policeCalled = lockResult.PoliceCalled, privateState = lockResult.PrivateState, message = lockResult.Message }, cancellationToken);
                        break;
                    case "attack":
                        var attack = await _world.AttackAsync(characterId, root.Deserialize<CombatRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastCombatAsync(new[] { attack.Event }, cancellationToken);
                        if (attack.Consequences is not null) await BroadcastCombatAsync(attack.Consequences, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = attack.Attacker }, null, cancellationToken);
                        if (attack.TargetPlayer is not null)
                        {
                            await BroadcastAsync(new { type = "playerUpdated", player = attack.TargetPlayer }, null, cancellationToken);
                        }
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        if (attack.Dungeon is not null) await connection.SendAsync(new { type = "dungeonUpdated", dungeon = attack.Dungeon }, cancellationToken);
                        break;
                    case "toggleProbulator":
                        var probulator = _world.ToggleProbulator(characterId, root.Deserialize<ToggleProbulatorRequest>(SharedJson.Options)!);
                        await BroadcastAsync(new { type = "combatEvent", combat = probulator.Event }, null, cancellationToken);
                        break;
                    case "throwHazard":
                        var hazardThrow = await _world.ThrowHazardAsync(characterId, root.Deserialize<ThrowHazardRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = hazardThrow.Attacker }, null, cancellationToken);
                        await BroadcastAsync(new { type = "combatEvent", combat = hazardThrow.Event }, null, cancellationToken);
                        await BroadcastAreaHazardsAsync(cancellationToken);
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "buyHomeUpgrade":
                        var upgraded = await _world.BuyHomeUpgradeAsync(characterId, root.GetProperty("upgradeId").GetString() ?? "", cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = upgraded }, null, cancellationToken);
                        await connection.SendAsync(new { type = "homeWorkshopUpdated", privateState = _world.GetPrivateState(characterId), message = "Home upgrade permanently installed." }, cancellationToken);
                        break;
                    case "useWaterPurifier":
                        var replacing = root.TryGetProperty("replaceFilter", out var filterReplacement) && filterReplacement.GetBoolean();
                        await _world.UseWaterPurifierAsync(characterId, replacing, cancellationToken);
                        await connection.SendAsync(new { type = "homeWorkshopUpdated", privateState = _world.GetPrivateState(characterId), message = replacing ? "New filter installed: 50 uses remaining." : "Purified water added to Home storage.", sound = replacing ? "metal" : "pour" }, cancellationToken);
                        break;
                    case "collectDirtyWater":
                        await _world.CollectDirtyWaterAsync(characterId, cancellationToken);
                        await connection.SendAsync(new { type = "dirtyWaterCollected", privateState = _world.GetPrivateState(characterId), message = "Collected dirty water." }, cancellationToken);
                        break;
                    case "requestCrafting":
                        var craftingRequest = root.Deserialize<RequestCraftingRequest>(SharedJson.Options)!;
                        await connection.SendAsync(new { type = "craftingOpened", crafting = _world.RequestCrafting(characterId, craftingRequest.FurnitureId), privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "craftItem":
                        var crafted = await _world.CraftItemAsync(characterId, root.Deserialize<CraftItemRequest>(SharedJson.Options)!, cancellationToken);
                        if (crafted.Player is not null) await BroadcastAsync(new { type = "playerUpdated", player = crafted.Player }, null, cancellationToken);
                        if (crafted.Explosion is not null) await BroadcastCombatAsync(new[] { crafted.Explosion }, cancellationToken);
                        await connection.SendAsync(new { type = "craftingUpdated", crafting = crafted.Crafting, privateState = crafted.PrivateState, message = crafted.Message, sound = crafted.Sound }, cancellationToken);
                        break;
                    case "openChest":
                        var chestRequest = root.Deserialize<OpenChestRequest>(SharedJson.Options)!;
                        var nearbyChest = await _world.OpenNearbyTreasureAsync(characterId, chestRequest.ChestId, true, cancellationToken);
                        await BroadcastNearbyTreasureAsync(nearbyChest, cancellationToken);
                        await connection.SendAsync(new { type = "nearbyTreasureOpened", contents = nearbyChest, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "takeNearbyTreasure":
                        var nearbyTaken = await _world.TakeNearbyTreasureAsync(characterId, root.Deserialize<TakeNearbyTreasureRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastNearbyTreasureAsync(nearbyTaken, cancellationToken);
                        await connection.SendAsync(new { type = "nearbyTreasureUpdated", contents = nearbyTaken, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "autoTakeNearbyTreasure":
                        NearbyTreasureState? automaticTreasure = null;
                        try
                        {
                            automaticTreasure = await _world.AutoTakeNearbyTreasureAsync(characterId,
                                root.GetProperty("sourceId").GetString() ?? string.Empty, root.GetProperty("chest").GetBoolean(), cancellationToken,
                                root.TryGetProperty("upgradesOnly", out var upgradesOnly) && upgradesOnly.GetBoolean());
                            await BroadcastNearbyTreasureAsync(automaticTreasure, cancellationToken);
                        }
                        catch (InvalidOperationException) { } // Moving away or another player collecting first is normal.
                        await connection.SendAsync(new { type = "autoTreasureUpdated", contents = automaticTreasure,
                            privateState = _world.GetPrivateState(characterId) }, cancellationToken);
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
                    case "openLoot":
                        var lootId = root.GetProperty("lootId").GetString() ?? string.Empty;
                        var openedLoot = await _world.OpenNearbyTreasureAsync(characterId, lootId, false, cancellationToken);
                        await BroadcastNearbyTreasureAsync(openedLoot, cancellationToken);
                        await connection.SendAsync(new { type = "nearbyTreasureOpened", contents = openedLoot, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "takeLootItems":
                        var lootRequest = root.Deserialize<TakeLootItemsRequest>(SharedJson.Options)!;
                        var lootTaken = await _world.TakeLootItemsAsync(characterId, lootRequest, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = lootTaken.Player }, null, cancellationToken);
                        if (lootTaken.Remaining is null) await BroadcastAsync(new { type = "lootRemoved", lootId = lootRequest.LootId }, null, cancellationToken);
                        else await BroadcastAsync(new { type = "lootCreated", loot = lootTaken.Remaining }, null, cancellationToken);
                        await connection.SendAsync(new { type = "lootItemsTaken", lootId = lootRequest.LootId, loot = lootTaken.Remaining, message = lootTaken.Message, privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "rebuildArea":
                        var rebuildRequest = root.Deserialize<RebuildAreaRequest>(SharedJson.Options)!;
                        var rebuilt = await _world.RebuildAsync(characterId, rebuildRequest.GodMode, cancellationToken, rebuildRequest.FromScratch);
                        await BroadcastWorldRebuiltAsync(rebuilt, cancellationToken);
                        break;
                    case "teleport":
                        var teleportRequest = root.Deserialize<TeleportRequest>(SharedJson.Options)!;
                        var needsTeleportArea = !_world.IsAreaLoaded(teleportRequest.X, teleportRequest.Y);
                        if (needsTeleportArea) await connection.SendAsync(new { type = "taskStatus", task = "Loading and generating teleport destination…" }, cancellationToken);
                        var teleport = await _world.TeleportWithAreaAsync(characterId, teleportRequest, cancellationToken);
                        if (teleport.Expanded) await connection.SendAsync(new { type = "worldExpanded", expanded = true, snapshot = _world.CreateClientSnapshot(characterId, connection.MapView) }, cancellationToken);
                        await BroadcastAsync(new { type = "playerTeleported", player = teleport.Player }, null, cancellationToken);
                        break;
                    case "mapFastTravel":
                        await connection.SendAsync(new { type = "taskStatus", task = "Preparing mini-map fast travel…" }, cancellationToken);
                        var fastTravel = await _world.MapFastTravelAsync(characterId, root.Deserialize<MapFastTravelRequest>(SharedJson.Options)!, cancellationToken);
                        if (fastTravel.Expanded) await connection.SendAsync(new { type = "worldExpanded", expanded = true, snapshot = _world.CreateClientSnapshot(characterId, connection.MapView) }, cancellationToken);
                        await BroadcastAsync(new { type = "playerTeleported", player = fastTravel.Player }, null, cancellationToken);
                        break;
                    case "say":
                        var chat = _world.Say(characterId, root.Deserialize<SayRequest>(SharedJson.Options)!);
                        await BroadcastAsync(new { type = "chatSaid", chat }, null, cancellationToken);
                        break;
                    case "pathRequest":
                        var pathRequest = root.Deserialize<PathRequest>(SharedJson.Options)!;
                        routeWorker.Replace(async routeToken =>
                        {
                            try
                            {
                                await connection.SendAsync(new { type = "taskStatus", sequence = pathRequest.Sequence, task = _world.IsAreaLoadRequiredForPath(characterId, pathRequest.X, pathRequest.Y) ? "Loading area and finding route…" : "Finding route…" }, cancellationToken);
                                var pathResult = await _world.FindPathAsync(characterId, pathRequest, routeToken);
                                routeToken.ThrowIfCancellationRequested();
                                if (pathResult.Expanded || pathRequest.IncludeSnapshot) await connection.SendAsync(new { type = "worldExpanded", sequence = pathRequest.Sequence, snapshot = _world.CreateClientSnapshot(characterId, connection.MapView) }, cancellationToken);
                                routeToken.ThrowIfCancellationRequested();
                                if (pathResult.Result.Success)
                                    await connection.SendAsync(new { type = "pathResult", sequence = pathRequest.Sequence, waypoints = pathResult.Result.Waypoints }, cancellationToken);
                                else
                                    await connection.SendAsync(new { type = "pathUnavailable", sequence = pathRequest.Sequence, message = pathResult.Result.Message }, cancellationToken);
                            }
                            catch (OperationCanceledException) when (routeToken.IsCancellationRequested) { }
                            catch (WebSocketException) { }
                            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
                            {
                                if (!routeToken.IsCancellationRequested)
                                    await connection.SendAsync(new { type = "pathUnavailable", sequence = pathRequest.Sequence, message = exception.Message }, cancellationToken);
                            }
                        }, cancellationToken);
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
                    case "requestMapWindow":
                        var mapRequest = root.Deserialize<RequestMapWindowRequest>(SharedJson.Options)!;
                        var mapView = new WorldBounds(mapRequest.MinimumX, mapRequest.MinimumY, mapRequest.MaximumX, mapRequest.MaximumY);
                        RealityWorld.ValidateMapView(mapView);
                        connection.MapView = mapView;
                        mapWorker.Replace(async token =>
                        {
                            using var timing = _world.Timings.Measure("map.window");
                            try
                            {
                                var mapWindow = _world.CreateMapWindow(characterId, mapView);
                                token.ThrowIfCancellationRequested();
                                await connection.SendAsync(new { type = "mapWindow", sequence = mapRequest.Sequence, map = mapWindow }, cancellationToken);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException && connection.Socket.State == WebSocketState.Open)
                            { await connection.SendAsync(new { type = "error", message = ex.Message }, token); }
                        }, cancellationToken);
                        break;
                    case "requestChunk":
                        await connection.SendAsync(new { type = "chunkSnapshot", snapshot = _world.CreateClientSnapshot(characterId, connection.MapView) }, cancellationToken);
                        break;
                    case "requestArea":
                        var requestedArea = root.Deserialize<RequestAreaRequest>(SharedJson.Options)!;
                        await connection.SendAsync(new { type = "taskStatus", task = "Loading and generating visible area…", blocksMovement = false }, cancellationToken);
                        areaWorker.Replace(async token =>
                        {
                            using var timing = _world.Timings.Measure("map.area");
                            try
                            {
                                var expanded = await _world.LoadAreaAsync(requestedArea.X, requestedArea.Y, token);
                                token.ThrowIfCancellationRequested();
                                await connection.SendAsync(new { type = "worldExpanded", expanded, snapshot = _world.CreateClientSnapshot(characterId, connection.MapView) }, cancellationToken);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException && connection.Socket.State == WebSocketState.Open)
                            { await connection.SendAsync(new { type = "error", message = ex.Message }, token); }
                        }, cancellationToken);
                        break;
                    case "requestCasinoState":
                    case "casinoBet":
                    case "casinoAction":
                    {
                        var casino = await _world.CasinoAsync(characterId,
                            type == "casinoBet" ? root.Deserialize<CasinoBetRequest>(SharedJson.Options) : null,
                            type == "casinoAction" ? root.Deserialize<CasinoActionRequest>(SharedJson.Options) : null,
                            cancellationToken);
                        await connection.SendAsync(new { type = "casinoUpdated", player = casino.Player,
                            privateState = _world.GetPrivateState(characterId), round = casino.Round,
                            stations = CasinoRules.Stations }, cancellationToken);
                        break;
                    }
                    case "requestPrivateState":
                        await connection.SendAsync(new { type = "privateState", privateState = _world.GetPrivateState(characterId) }, cancellationToken);
                        break;
                    case "ping":
                        await connection.SendAsync(new { type = "pong", id = root.TryGetProperty("id", out var probeId) && probeId.TryGetInt64(out var probeNumber) ? (long?)probeNumber : null, serverTime = DateTimeOffset.UtcNow }, cancellationToken);
                        break;
                    default:
                        await connection.SendAsync(new { type = "error", message = "Unknown message type." }, cancellationToken);
                        break;
                }
            }
            catch (CombatTargetUnavailableException exception)
            {
                await connection.SendAsync(new { type = "combatTargetUnavailable", targetId = exception.TargetId }, cancellationToken);
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException or HttpRequestException)
            {
                await connection.SendAsync(new { type = "error", commandSequence = root.TryGetProperty("commandSequence", out var commandSequence) && commandSequence.TryGetInt64(out var commandNumber) ? (long?)commandNumber : null, message = exception.Message }, cancellationToken);
            }
        }
    }

    private async Task HandleNpcDialogueAsync(string characterId, ClientConnection connection, NpcDialogueRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var prepared = _world.BeginAiNpcDialogue(characterId, request);
            await connection.SendAsync(new { type = "npcDialoguePending", actorId = prepared.ActorId, interactionId = prepared.InteractionId }, cancellationToken);
            var proposed = prepared.ImmediateResult ?? await _localAi.CompleteAsync(prepared.Turn!, cancellationToken);
            var applied = await _world.ApplyAiNpcDialogueAsync(characterId, prepared.ActorId, prepared.InteractionId, proposed, cancellationToken);
            await connection.SendAsync(new
            {
                type = "npcDialogueResult", actorId = prepared.ActorId, interactionId = prepared.InteractionId,
                dialogue = applied.Result.Dialogue, playerIntent = applied.Result.PlayerIntent, npcIntent = applied.Result.NpcIntent,
                relationshipDelta = applied.Result.RelationshipDelta, endConversation = applied.Result.EndConversation,
                reasonCode = applied.Result.ReasonCode, usedAi = applied.Result.UsedAi, relationship = applied.Relationship
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or WebSocketException)
        {
            try { await connection.SendAsync(new { type = "npcDialogueError", actorId = request.ActorId, interactionId = request.InteractionId, message = exception.Message }, cancellationToken); }
            catch (Exception sendException) when (sendException is OperationCanceledException or WebSocketException) { }
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
            try { await pair.Value.SendAsync(new { type = "worldRebuilt", snapshot = _world.CreateClientSnapshot(pair.Key), privateState = _world.GetPrivateState(pair.Key) }, cancellationToken); }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
        });
        await Task.WhenAll(sends);
    }

    public async Task SendRetroBattlesAsync(IReadOnlyList<string> playerIds, CancellationToken token = default)
    {
        foreach (var id in playerIds)
        {
            if (!_clients.TryGetValue(id, out var connection)) continue;
            var (player, dungeon) = _world.GetRetroBattleUpdate(id);
            if (player is null) continue;
            try
            {
                if (dungeon?.RetroBattle is null)
                    await connection.SendAsync(new { type = "dungeonExited", player, snapshot = _world.CreateClientSnapshot(id, connection.MapView), privateState = _world.GetPrivateState(id) }, token);
                else
                {
                    await connection.SendAsync(new { type = "retroBattleUpdated", player, dungeon }, token);
                    if (dungeon.IsCompleted && dungeon.RetroBattle.RewardChestId is { } chestId)
                    {
                        var reward = await _world.OpenChestAsync(id, chestId, token);
                        await connection.SendAsync(new { type = "chestOpened", player = reward.Player, contents = reward.Contents, message = reward.Message, privateState = _world.GetPrivateState(id) }, token);
                    }
                }
            }
            catch (WebSocketException) { }
            catch (InvalidOperationException) { /* The player may have left while this update was being sent. */ }
        }
    }

    public async Task SendProgressionNoticesAsync(IReadOnlyList<ProgressionNotice> notices, CancellationToken token = default)
    {
        foreach (var group in notices.GroupBy(notice => notice.PlayerId))
            if (_clients.TryGetValue(group.Key, out var connection))
            {
                try { await connection.SendAsync(new { type = "progressionUpdated", privateState = _world.GetPrivateState(group.Key), notices = group.ToArray() }, token); }
                catch (WebSocketException) { }
            }
    }

    public async Task SendQuestNoticesAsync(IReadOnlyList<(string PlayerId, string Message)> notices, CancellationToken token = default)
    {
        foreach (var notice in notices)
            if (_clients.TryGetValue(notice.PlayerId, out var connection))
            {
                try { await connection.SendAsync(new { type = "questUpdated", privateState = _world.GetPrivateState(notice.PlayerId), message = notice.Message }, token); }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
            }
    }

    public async Task SendRelationshipsAsync(IReadOnlyList<RelationshipState> relationships, CancellationToken cancellationToken = default)
    {
        foreach (var group in relationships.GroupBy(relationship => relationship.PlayerId))
            if (_clients.TryGetValue(group.Key, out var connection))
            {
                try { await connection.SendAsync(new { type = "relationshipsChanged", relationships = group.ToArray() }, cancellationToken); }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
            }
    }

    public Task BroadcastWeatherAsync(CancellationToken cancellationToken = default) =>
        BroadcastAsync(new { type = "weatherChanged", weather = _world.Weather }, null, cancellationToken);

    public async Task BroadcastTransitAsync(TransitTick tick, CancellationToken token)
    {
        foreach (var (playerId, connection) in _clients)
        {
            try
            {
                if (tick.NetworkChanged || DateTimeOffset.UtcNow - connection.LastTransitView > TimeSpan.FromSeconds(3))
                {
                    connection.LastTransitView = DateTimeOffset.UtcNow;
                    await connection.SendAsync(new { type = "transitChanged", transit = _world.CreateTransitView(playerId, connection.MapView) }, token);
                }
                else
                {
                    var buses = _world.NearbyBuses(playerId, connection.MapView);
                    var signature = string.Join(';', buses.Select(b => $"{b.Id}:{b.Version}:{b.Status}"));
                    if (signature == connection.LastBusSignature) continue;
                    connection.LastBusSignature = signature;
                    await connection.SendAsync(new { type = "busesMoved", buses }, token);
                }
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
        }
        foreach (var sound in tick.Sounds ?? []) await BroadcastAsync(new { type = "worldSound", sound }, null, token);
        await BroadcastPlayersAsync(tick.Players, token);
        await BroadcastActorsAsync(tick.Actors, token);
        await BroadcastRemovedActorsAsync(tick.RemovedActors, token);
        await BroadcastCombatAsync(tick.Combat, token);
        foreach (var entity in tick.Objects) await BroadcastAsync(new { type = "worldObjectUpdated", entity }, null, token);
        await BroadcastLootAsync(_world.TakeDeathDropAnnouncements(), token);
    }

    public Task BroadcastActorsAsync(IReadOnlyList<ActorState> actors, CancellationToken cancellationToken = default) =>
        actors.Count == 0 ? Task.CompletedTask : BroadcastAsync(new { type = "actorsMoved", actors }, null, cancellationToken);

    public Task BroadcastRemovedActorsAsync(IReadOnlyList<string> ids, CancellationToken token = default) =>
        ids.Count == 0 ? Task.CompletedTask : BroadcastAsync(new { type = "actorsRemoved", ids }, null, token);

    public Task BroadcastPlayersAsync(IReadOnlyList<PlayerState> players, CancellationToken cancellationToken = default) =>
        players.Count == 0 ? Task.CompletedTask : BroadcastAsync(new { type = "playersUpdated", players }, null, cancellationToken);

    public Task BroadcastAreaHazardsAsync(CancellationToken cancellationToken = default) =>
        BroadcastAsync(new { type = "areaHazardsChanged", areaHazards = _world.GetAreaHazards() }, null, cancellationToken);

    public Task BroadcastDoorLocksAsync(DoorLockSchedule schedule, CancellationToken cancellationToken = default) =>
        BroadcastAsync(new { type = "doorLocksChanged", doorLocks = schedule.Doors, doorLockCycleEndsAtUtc = schedule.EndsAtUtc }, null, cancellationToken);

    private async Task BroadcastNearbyTreasureAsync(NearbyTreasureState treasure, CancellationToken token)
    {
        await BroadcastAsync(new { type = "playerUpdated", player = treasure.Player }, null, token);
        foreach (var id in treasure.RemovedLoot) await BroadcastAsync(new { type = "lootRemoved", lootId = id }, null, token);
        foreach (var loot in treasure.ChangedLoot) await BroadcastAsync(new { type = "lootCreated", loot }, null, token);
    }

    public async Task BroadcastCombatAsync(IReadOnlyList<CombatEvent> combat, CancellationToken cancellationToken = default)
    {
        foreach (var item in combat) await BroadcastAsync(new { type = "combatEvent", combat = _world.ResolveCombatFear(item) }, null, cancellationToken);
    }

    public async Task BroadcastChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        foreach (var chat in messages)
            await BroadcastAsync(new { type = "chatSaid", chat }, null, cancellationToken);
    }

    private readonly ConcurrentDictionary<string,long> _inversionPlayerVersions = new();
    public async Task BroadcastInversionsAsync(CancellationToken token = default)
    {
        foreach (var id in _world.TakeInversionDungeonExits())
            if (_clients.TryGetValue(id, out var connection) && _world.GetRetroBattleUpdate(id).Player is { } player)
                await connection.SendAsync(new { type = "dungeonExited", player, snapshot = _world.CreateClientSnapshot(id, connection.MapView), privateState = _world.GetPrivateState(id) }, token);
        foreach (var pair in _clients)
            await pair.Value.SendAsync(new { type = "inversionsUpdated", inversions = _world.GetInversionView(pair.Key), progression = _world.GetProgression(pair.Key) }, token);
        await BroadcastPlayersAsync(_world.InversionPlayers().Where(p => !_inversionPlayerVersions.TryGetValue(p.Id, out var version) || version != p.Version).Select(p => { _inversionPlayerVersions[p.Id] = p.Version; return p; }).ToArray(), token);
        await BroadcastActorsAsync(_world.InversionActors().Concat(_world.TakeInversionActorUpdates()).ToArray(), token);
        await BroadcastRemovedActorsAsync(_world.TakeInversionRemovals(), token);
        await BroadcastRemovedWorldObjectsAsync(_world.TakeHaneyTruckRemovals(), token);
        foreach (var car in _world.TakeNpcCarUpdates()) await BroadcastAsync(new { type = "worldObjectUpdated", entity = car }, null, token);
        await BroadcastCombatAsync(_world.TakeInversionCombat(), token);
        await BroadcastChatAsync(_world.TakeInversionChat(), token);
    }

    public async Task BroadcastLootAsync(IReadOnlyList<LootDropState> drops, CancellationToken cancellationToken = default)
    {
        foreach (var loot in drops)
        {
            await BroadcastAsync(new { type = "lootCreated", loot }, null, cancellationToken);
            if (loot.DropKind == "tombstone" && loot.OwnerId is not null && _clients.TryGetValue(loot.OwnerId, out var defeatedConnection))
            {
                var player = _world.CreateSnapshot().Players.FirstOrDefault(item => item.Id == loot.OwnerId);
                if (player is not null) await defeatedConnection.SendAsync(new { type = "playerDied", reason = $"You died. Everything you carried and {loot.MoneyCents / 100m:C} were left in your tombstone.", player, privateState = _world.GetPrivateState(loot.OwnerId) }, cancellationToken);
            }
        }
    }

    public async Task BroadcastGardensAsync(IReadOnlyList<CanonicalEntity> entities,CancellationToken token) { foreach(var entity in entities)await BroadcastAsync(new {type="worldObjectUpdated",entity},null,token); }

    public async Task BroadcastRemovedWorldObjectsAsync(IReadOnlyList<string> entityIds, CancellationToken cancellationToken = default)
    {
        foreach (var entityId in entityIds) await BroadcastAsync(new { type = "objectRemoved", entityId }, null, cancellationToken);
    }

    private static string NormalizeCharacterId(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed.ToString("N") : Guid.NewGuid().ToString("N");

    private sealed class ClientConnection
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        public DateTimeOffset LastTransitView { get; set; }
        public string? LastBusSignature { get; set; }
        public ClientConnection(WebSocket socket) => Socket = socket;
        public WebSocket Socket { get; }
        public WorldBounds? MapView { get; set; }

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
