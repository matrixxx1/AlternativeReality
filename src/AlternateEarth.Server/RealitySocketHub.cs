using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed class RealitySocketHub
{
    private readonly RealityWorld _world;
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();

    public RealitySocketHub(RealityWorld world) => _world = world;

    public async Task AcceptAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("A WebSocket connection is required.");
            return;
        }

        var characterId = NormalizeCharacterId(context.Request.Query["characterId"].FirstOrDefault());
        var name = context.Request.Query["name"].FirstOrDefault() ?? "Explorer";
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connection = new ClientConnection(socket);
        try
        {
            if (_clients.TryRemove(characterId, out var previous)) await previous.CloseAsync("Reconnected from another client.");
            var player = await _world.JoinAsync(characterId, name, context.RequestAborted);
            _clients[characterId] = connection;
            await connection.SendAsync(new { type = "welcome", protocolVersion = Protocol.Version, playerId = characterId, snapshot = _world.CreateSnapshot() }, context.RequestAborted);
            await BroadcastAsync(new { type = "playerJoined", player }, characterId, context.RequestAborted);
            await ReceiveLoopAsync(characterId, connection, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (WebSocketException) { }
        finally
        {
            _clients.TryRemove(characterId, out _);
            _world.Leave(characterId);
            await BroadcastAsync(new { type = "playerLeft", playerId = characterId }, characterId, CancellationToken.None);
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
                            if (movement.Blocked) await connection.SendAsync(new { type = "movementBlocked", message = "Something is blocking the way." }, cancellationToken);
                            if (movement.Fell) await connection.SendAsync(new { type = "playerFell", message = movement.Message, player = movement.Player }, cancellationToken);
                            if (movement.Died) await connection.SendAsync(new { type = "playerDied", reason = movement.Message, player = movement.Player }, cancellationToken);
                        }
                        break;
                    case "setTravelMode":
                        var travelRequest = root.Deserialize<SetTravelModeRequest>(SharedJson.Options)!;
                        var travelPlayer = await _world.SetTravelModeAsync(characterId, travelRequest.Mode, cancellationToken);
                        await BroadcastAsync(new { type = "playerUpdated", player = travelPlayer }, null, cancellationToken);
                        break;
                    case "rebuildArea":
                        var rebuildRequest = root.Deserialize<RebuildAreaRequest>(SharedJson.Options)!;
                        var rebuilt = await _world.RebuildAsync(characterId, rebuildRequest.GodMode, cancellationToken);
                        await BroadcastAsync(new { type = "worldRebuilt", snapshot = rebuilt }, null, cancellationToken);
                        break;
                    case "pathRequest":
                        var pathRequest = root.Deserialize<PathRequest>(SharedJson.Options)!;
                        var path = _world.FindPath(characterId, pathRequest);
                        if (path.Success)
                            await connection.SendAsync(new { type = "pathResult", sequence = pathRequest.Sequence, waypoints = path.Waypoints }, cancellationToken);
                        else
                            await connection.SendAsync(new { type = "pathUnavailable", sequence = pathRequest.Sequence, message = path.Message }, cancellationToken);
                        break;
                    case "placeObject":
                        var created = await _world.PlaceObjectAsync(characterId, root.Deserialize<PlaceObjectRequest>(SharedJson.Options)!, cancellationToken);
                        await BroadcastAsync(new { type = "objectCreated", entity = created }, null, cancellationToken);
                        break;
                    case "removeObject":
                        var removedRequest = root.Deserialize<RemoveObjectRequest>(SharedJson.Options)!;
                        var removed = await _world.RemoveObjectAsync(characterId, removedRequest.EntityId, cancellationToken);
                        await BroadcastAsync(new { type = "objectRemoved", entityId = removed.Id }, null, cancellationToken);
                        break;
                    case "requestChunk":
                        await connection.SendAsync(new { type = "chunkSnapshot", snapshot = _world.CreateSnapshot() }, cancellationToken);
                        break;
                    case "ping":
                        await connection.SendAsync(new { type = "pong", serverTime = DateTimeOffset.UtcNow }, cancellationToken);
                        break;
                    default:
                        await connection.SendAsync(new { type = "error", message = "Unknown message type." }, cancellationToken);
                        break;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException)
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

    public Task BroadcastWeatherAsync(CancellationToken cancellationToken = default) =>
        BroadcastAsync(new { type = "weatherChanged", weather = _world.Weather }, null, cancellationToken);

    public Task BroadcastActorsAsync(IReadOnlyList<ActorState> actors, CancellationToken cancellationToken = default) =>
        actors.Count == 0 ? Task.CompletedTask : BroadcastAsync(new { type = "actorsMoved", actors }, null, cancellationToken);

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
