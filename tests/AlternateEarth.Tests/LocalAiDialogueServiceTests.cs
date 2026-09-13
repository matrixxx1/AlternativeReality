using System.Net;
using System.Text;
using AlternateEarth.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlternateEarth.Tests;

public sealed class LocalAiDialogueServiceTests
{
    private static readonly NpcDialogueTurn Turn = new("test", "test", "test", new Dictionary<string, object?>(), "Hello", Guid.NewGuid().ToString(), new { });

    [Fact]
    public async Task QueuedReplyIsPolledAndNewSettingsChangeTheDestination()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var handler = new FakeHandler();
            var config = new LocalAiConfiguration(path, new LocalAiSettings { BaseUrl = "http://first", ProjectGuid = Guid.NewGuid().ToString() });
            var service = new LocalAiNpcDialogueService(new Factory(handler), config, NullLogger<LocalAiNpcDialogueService>.Instance);
            var result = await service.CompleteAsync(Turn, CancellationToken.None);
            Assert.True(result.UsedAi);
            Assert.Equal("Hello there", result.Dialogue);
            Assert.Contains(handler.Urls, url => url.Host == "first" && url.AbsolutePath == "/api/v1/requests/req");
            config.Save(new LocalAiSettings { BaseUrl = "http://second", ProjectGuid = Guid.NewGuid().ToString() });
            result = await service.CompleteAsync(Turn, CancellationToken.None);
            Assert.True(result.UsedAi);
            Assert.Equal("second", handler.Urls.Last().Host);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DeadlineReturnsTimeoutReasonButCallerCancellationPropagates()
    {
        var config = new LocalAiConfiguration("unused-" + Guid.NewGuid(), new LocalAiSettings { RequestTimeoutSeconds = 1, ProjectGuid = Guid.NewGuid().ToString() });
        var service = new LocalAiNpcDialogueService(new Factory(new FakeHandler { Hang = true }), config, NullLogger<LocalAiNpcDialogueService>.Instance);
        var result = await service.CompleteAsync(Turn, CancellationToken.None);
        Assert.False(result.UsedAi);
        Assert.Equal("localai_timeout", result.ReasonCode);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CompleteAsync(Turn, cancel.Token));
    }

    [Fact]
    public async Task QuestProposalIsReturnedInOneModelRequest()
    {
        var handler = new FakeHandler { Quest = true };
        var config = new LocalAiConfiguration("unused-" + Guid.NewGuid(), new LocalAiSettings { BaseUrl = "http://ai", ProjectGuid = Guid.NewGuid().ToString() });
        var service = new LocalAiNpcDialogueService(new Factory(handler), config, NullLogger<LocalAiNpcDialogueService>.Instance);
        var result = await service.CompleteAsync(Turn, CancellationToken.None);
        Assert.Equal("collect", result.QuestRequest!.Action);
        Assert.True(result.QuestRequest.Requested);
        Assert.Equal("metal", result.QuestRequest.Subject);
        Assert.Single(handler.Urls, u => u.AbsolutePath.EndsWith("/requests"));
    }

    [Fact]
    public async Task HistoryLookupUsesExactPairAndDoesNotSubmitAModelRequest()
    {
        var handler = new FakeHandler { History = true };
        var config = new LocalAiConfiguration("unused-" + Guid.NewGuid(), new LocalAiSettings { BaseUrl = "http://ai", ProjectGuid = Guid.NewGuid().ToString() });
        var service = new LocalAiNpcDialogueService(new Factory(handler), config, NullLogger<LocalAiNpcDialogueService>.Instance);
        var history = await service.ReadHistoryAsync("my-pair", CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.Equal("player", history[0].Role);
        Assert.Equal("Actual dialogue", history[1].Text);
        Assert.DoesNotContain(handler.Urls, u => u.AbsolutePath.EndsWith("/requests") || u.AbsolutePath.EndsWith("upsert"));
        Assert.Empty(await service.ReadHistoryAsync("someone-else", CancellationToken.None));
    }

    private sealed class Factory(FakeHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<Uri> Urls { get; } = [];
        public bool Hang { get; init; }
        public bool Quest { get; init; }
        public bool History { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Hang) await Task.Delay(Timeout.Infinite, cancellationToken);
            Urls.Add(request.RequestUri!);
            var path = request.RequestUri!.AbsolutePath;
            var json = path.EndsWith("upsert") ? "{\"guid\":\"chat\"}"
                : request.Method == HttpMethod.Post ? "{\"guid\":\"req\",\"status\":\"queued\"}"
                : "{\"guid\":\"req\",\"status\":\"complete\",\"npc_result\":{\"dialogue\":\"Hello there\",\"relationship_delta\":0.01}}";
            if (Quest && request.Method == HttpMethod.Get) json = """{"guid":"req","status":"complete","npc_result":{"dialogue":"{objective}","quest_request":{"requested":true,"title":"Scrap safari","action":"collect","subject":"metal"}}}""";
            if (History) json = path.EndsWith("/chats")
                ? """{"chats":[{"guid":"other","external_key":"private"},{"guid":"mine","external_key":"my-pair"}]}"""
                : """{"messages":[{"guid":"1","role":"user","content":"Hello!","created_at":"2026-09-12T00:00:00Z"},{"guid":"2","role":"assistant","content":"raw","npc_result":{"dialogue":"Actual dialogue"},"created_at":"2026-09-12T00:01:00Z"}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
