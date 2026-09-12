using System.Net;
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
    public int RequestTimeoutSeconds { get; set; } = 60;
}

public sealed record NpcDialogueModelResult(
    string Dialogue,
    string PlayerIntent,
    string NpcIntent,
    double RelationshipDelta,
    bool EndConversation,
    string ReasonCode,
    IReadOnlyList<string> MemoryFacts,
    bool UsedAi = true);

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
    private readonly LocalAiSettings _settings;
    private readonly ILogger<LocalAiNpcDialogueService> _logger;
    private readonly SemaphoreSlim _projectLock = new(1, 1);
    private string? _resolvedProjectGuid;

    public LocalAiNpcDialogueService(IHttpClientFactory clients, LocalAiSettings settings, ILogger<LocalAiNpcDialogueService> logger)
    {
        _clients = clients;
        _settings = settings;
        _logger = logger;
    }

    public async Task<NpcDialogueModelResult> CompleteAsync(NpcDialogueTurn turn, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) return Offline();
        try
        {
            var client = _clients.CreateClient("localai");
            var projectGuid = await ResolveProjectGuidAsync(client, cancellationToken);
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

            while (request.Status is "queued" or "running")
            {
                await Task.Delay(500, cancellationToken);
                request = await client.GetFromJsonAsync<RequestResponse>($"api/v1/requests/{request.Guid}", cancellationToken);
                if (request is null) return Offline();
            }
            if (request.Status != "complete" || request.NpcResult is null) return Offline();
            var result = request.NpcResult;
            if (string.IsNullOrWhiteSpace(result.Dialogue)) return Offline();
            return new NpcDialogueModelResult(
                result.Dialogue.Trim()[..Math.Min(500, result.Dialogue.Trim().Length)],
                result.PlayerIntent ?? "unknown", result.NpcIntent ?? "unknown",
                Math.Clamp(result.RelationshipDelta, -.05, .05), result.EndConversation,
                result.ReasonCode ?? "unknown", result.MemoryFacts?.Take(4).ToArray() ?? []);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (!cancellationToken.IsCancellationRequested)
                _logger.LogWarning(exception, "LocalAI NPC dialogue was unavailable.");
            return Offline();
        }
    }

    private async Task<string?> ResolveProjectGuidAsync(HttpClient client, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(_settings.ProjectGuid, out var configured)) return configured.ToString();
        if (_resolvedProjectGuid is not null) return _resolvedProjectGuid;
        await _projectLock.WaitAsync(cancellationToken);
        try
        {
            if (_resolvedProjectGuid is not null) return _resolvedProjectGuid;
            var response = await client.GetFromJsonAsync<ProjectsResponse>("api/v1/projects", cancellationToken);
            _resolvedProjectGuid = response?.Projects.FirstOrDefault(project =>
                project.Type == "npc_chats" && project.Name.Equals(_settings.ProjectName, StringComparison.OrdinalIgnoreCase))?.Guid;
            if (_resolvedProjectGuid is null)
                _logger.LogWarning("LocalAI has no NPC Chats project named {ProjectName}. Create it or set LocalAI:ProjectGuid.", _settings.ProjectName);
            return _resolvedProjectGuid;
        }
        finally { _projectLock.Release(); }
    }

    private async Task<NpcDialogueModelResult> FailedAsync(string operation, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("LocalAI NPC {Operation} failed with {StatusCode}: {Body}", operation, (int)response.StatusCode, body[..Math.Min(300, body.Length)]);
        if (response.StatusCode == HttpStatusCode.NotFound) _resolvedProjectGuid = null;
        return Offline();
    }

    private static NpcDialogueModelResult Offline() =>
        new(OfflineMessage, "unknown", "refuse", 0, true, "localai_unavailable", [], false);

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
        [property: JsonPropertyName("memory_facts")] string[]? MemoryFacts);
}
