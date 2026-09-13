using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlternateEarth.Server;

public sealed class LocalAiSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://mmac.local:80";
    public string ProjectGuid { get; set; } = "";
    public string ProjectName { get; set; } = "AlternativeReality";
    public int RequestTimeoutSeconds { get; set; } = 300;
}

public sealed record NpcDialogueModelResult(
    string Dialogue,
    string PlayerIntent,
    string NpcIntent,
    double RelationshipDelta,
    bool EndConversation,
    string ReasonCode,
    IReadOnlyList<string> MemoryFacts,
    bool UsedAi = true,
    string? Farewell = null, AiNpcQuestRequest? QuestRequest = null);

public sealed record AiNpcQuestRequest(
    [property: JsonPropertyName("requested")] bool Requested,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("subject")] string? Subject = null);

public sealed record NpcDialogueTurn(
    string ChatExternalKey,
    string ChatName,
    string PairingContext,
    IReadOnlyDictionary<string, object?> ChatMetadata,
    string Message,
    string InteractionId,
    object ContextSnapshot);

public sealed class LocalAiNpcDialogueService
{
    public const string OfflineMessage = "Sorry, I can't talk now";
    private readonly IHttpClientFactory _clients;
    private readonly LocalAiConfiguration _configuration;
    private readonly ILogger<LocalAiNpcDialogueService> _logger;

    public LocalAiNpcDialogueService(IHttpClientFactory clients, LocalAiConfiguration configuration, ILogger<LocalAiNpcDialogueService> logger)
    {
        _clients = clients;
        _configuration = configuration;
        _logger = logger;
    }

    public int RequestTimeoutSeconds => _configuration.Current.RequestTimeoutSeconds;

    public async Task<NpcDialogueModelResult> CompleteAsync(NpcDialogueTurn turn, CancellationToken cancellationToken, LocalAiSettings? settingsOverride = null)
    {
        var settings = settingsOverride ?? _configuration.Current;
        var callerCancellation = cancellationToken;
        string? requestGuid = null;
        if (!settings.Enabled) return Offline();
        try
        {
            using var client = _clients.CreateClient("localai");
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(client.Timeout);
            cancellationToken = timeout.Token;
            var projectGuid = await ResolveProjectGuidAsync(client, settings, cancellationToken);
            if (projectGuid is null) return Offline();
            using var upsert = await client.PostAsJsonAsync($"api/v1/projects/{projectGuid}/chats/upsert", new
            {
                name = turn.ChatName,
                external_key = turn.ChatExternalKey,
                request_type = "npc_chats",
                mode = "read",
                context = turn.PairingContext,
                metadata = turn.ChatMetadata
            }, cancellationToken);
            if (!upsert.IsSuccessStatusCode) return await FailedAsync("chat upsert", upsert, cancellationToken);
            var chat = await upsert.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(chat?.Guid)) return Offline();

            using var queuedResponse = await client.PostAsJsonAsync($"api/v1/chats/{chat.Guid}/requests", new
            {
                message = turn.Message,
                request_type = "npc_chats",
                external_request_id = turn.InteractionId,
                context_snapshot = turn.ContextSnapshot
            }, cancellationToken);
            if (!queuedResponse.IsSuccessStatusCode) return await FailedAsync("request queue", queuedResponse, cancellationToken);
            var request = await queuedResponse.Content.ReadFromJsonAsync<RequestResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(request?.Guid)) return Offline();
            requestGuid = request.Guid;

            while (request.Status is "queued" or "running")
            {
                await Task.Delay(500, cancellationToken);
                request = await client.GetFromJsonAsync<RequestResponse>($"api/v1/requests/{request.Guid}", cancellationToken);
                if (request is null) return Offline();
            }
            if (request.Status != "complete" || request.NpcResult is null)
            {
                _logger.LogWarning("LocalAI request {RequestGuid} ended with status {Status} or no NPC result.", requestGuid, request.Status);
                return Offline("localai_failed");
            }
            var result = request.NpcResult;
            if (string.IsNullOrWhiteSpace(result.Dialogue)) return Offline();
            return new NpcDialogueModelResult(
                result.Dialogue.Trim()[..Math.Min(500, result.Dialogue.Trim().Length)],
                result.PlayerIntent ?? "unknown", result.NpcIntent ?? "unknown",
                Math.Clamp(result.RelationshipDelta, -.05, .05), result.EndConversation,
                result.ReasonCode ?? "unknown", result.MemoryFacts?.Take(4).ToArray() ?? [], QuestRequest: result.QuestRequest);
        }
        catch (OperationCanceledException) when (!callerCancellation.IsCancellationRequested)
        {
            _logger.LogWarning("LocalAI request {RequestGuid} at {BaseUrl} timed out after {TimeoutSeconds} seconds.", requestGuid, settings.BaseUrl, settings.RequestTimeoutSeconds);
            return Offline("localai_timeout");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            if (!callerCancellation.IsCancellationRequested)
                _logger.LogWarning(exception, "LocalAI NPC dialogue was unavailable.");
            return Offline();
        }
    }

    public async Task<IReadOnlyList<ImportedNpcMessage>> ReadHistoryAsync(string externalKey, CancellationToken token)
    {
        var settings = _configuration.Current;
        if (!settings.Enabled) return [];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var client = _clients.CreateClient("LocalAI");
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
        var project = await ResolveProjectGuidAsync(client, settings, deadline.Token);
        if (project is null) return [];
        using var chats = await client.GetFromJsonAsync<JsonDocument>($"api/v1/projects/{project}/chats", deadline.Token);
        if (chats is null || !chats.RootElement.TryGetProperty("chats", out var list)) return [];
        var chat = list.EnumerateArray().FirstOrDefault(c => c.TryGetProperty("external_key", out var key) && key.GetString() == externalKey);
        if (chat.ValueKind == JsonValueKind.Undefined) return [];
        var id = chat.GetProperty("guid").GetString();
        using var document = await client.GetFromJsonAsync<JsonDocument>($"api/v1/chats/{Uri.EscapeDataString(id!)}/messages", deadline.Token);
        if (document is null || !document.RootElement.TryGetProperty("messages", out var messages)) return [];
        var imported = new List<ImportedNpcMessage>();
        foreach (var message in messages.EnumerateArray())
        {
            var role = message.GetProperty("role").GetString();
            if (role is not ("user" or "assistant")) continue;
            var text = message.TryGetProperty("npc_result", out var result) && result.ValueKind == JsonValueKind.Object && result.TryGetProperty("dialogue", out var dialogue)
                ? dialogue.GetString() : message.GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(text)) continue;
            imported.Add(new(message.GetProperty("guid").GetString()!, role == "user" ? "player" : "npc", text,
                message.GetProperty("created_at").GetString()!));
        }
        return imported.OrderBy(m => m.SaidAtUtc, StringComparer.Ordinal).ToArray();
    }

    private async Task<string?> ResolveProjectGuidAsync(HttpClient client, LocalAiSettings settings, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(settings.ProjectGuid, out var configured)) return configured.ToString();
        var response = await client.GetFromJsonAsync<ProjectsResponse>("api/v1/projects", cancellationToken);
        var projectGuid = response?.Projects.FirstOrDefault(project =>
            project.Type == "npc_chats" && project.Name.Equals(settings.ProjectName, StringComparison.OrdinalIgnoreCase))?.Guid;
        if (projectGuid is null)
            _logger.LogWarning("LocalAI has no NPC Chats project named {ProjectName}. Create it or set LocalAI:ProjectGuid.", settings.ProjectName);
        return projectGuid;
    }

    private async Task<NpcDialogueModelResult> FailedAsync(string operation, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("LocalAI NPC {Operation} failed with {StatusCode}: {Body}", operation, (int)response.StatusCode, body[..Math.Min(300, body.Length)]);
        return Offline();
    }

    private static NpcDialogueModelResult Offline(string reason = "localai_unavailable") =>
        new(OfflineMessage, "unknown", "refuse", 0, true, reason, [], false);

    private sealed record ProjectsResponse([property: JsonPropertyName("projects")] ProjectResponse[] Projects);
    private sealed record ProjectResponse(
        [property: JsonPropertyName("guid")] string Guid,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type);
    private sealed record ChatResponse([property: JsonPropertyName("guid")] string Guid);
    private sealed record RequestResponse(
        [property: JsonPropertyName("guid")] string Guid,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("npc_result")] NpcResultResponse? NpcResult);
    private sealed record NpcResultResponse(
        [property: JsonPropertyName("dialogue")] string Dialogue,
        [property: JsonPropertyName("player_intent")] string? PlayerIntent,
        [property: JsonPropertyName("npc_intent")] string? NpcIntent,
        [property: JsonPropertyName("relationship_delta")] double RelationshipDelta,
        [property: JsonPropertyName("end_conversation")] bool EndConversation,
        [property: JsonPropertyName("reason_code")] string? ReasonCode,
        [property: JsonPropertyName("memory_facts")] string[]? MemoryFacts,
        [property: JsonPropertyName("quest_request")] AiNpcQuestRequest? QuestRequest = null);
}

public sealed record ImportedNpcMessage(string Id, string Role, string Text, string SaidAtUtc);
