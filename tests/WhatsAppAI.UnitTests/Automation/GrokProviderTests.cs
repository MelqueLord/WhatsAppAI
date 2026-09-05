using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Grok;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class GrokProviderTests : IDisposable
{
    private readonly RecordingHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly GrokProvider _provider;

    public GrokProviderTests()
    {
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://api.x.ai/") };
        _provider = new GrokProvider(_httpClient, NullLogger<GrokProvider>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task GetResponseAsync_UsesResponsesWebSearchForGenericQuestion()
    {
        _handler.Response = CreateResponse("""
        { "id": "grok-response-1", "output": [{ "type": "message", "content": [{ "type": "output_text", "text": "{\"action\":\"reply\",\"text\":\"É uma solução de atendimento.\",\"confidence\":0.9}" }] }], "usage": { "input_tokens": 12, "output_tokens": 7 } }
        """);

        var result = await _provider.GetResponseAsync(CreateRequest() with { AllowPublicWebSearch = true });

        Assert.Equal("v1/responses", _handler.LastRequestUri);
        using var body = JsonDocument.Parse(_handler.LastRequestBody!);
        Assert.Equal("web_search", body.RootElement.GetProperty("tools")[0].GetProperty("type").GetString());
        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal("É uma solução de atendimento.", result.Content);
        Assert.Equal(12, result.InputTokens);
    }

    [Fact]
    public async Task GetResponseAsync_KeepsChatCompletionsWithoutWebPermission()
    {
        _handler.Response = CreateResponse("""
        { "id": "grok-chat-1", "choices": [{ "message": { "content": "{\"action\":\"reply\",\"text\":\"Olá!\",\"confidence\":0.9}" } }], "usage": { "prompt_tokens": 10, "completion_tokens": 5 } }
        """);

        await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal("v1/chat/completions", _handler.LastRequestUri);
        using var body = JsonDocument.Parse(_handler.LastRequestBody!);
        Assert.False(body.RootElement.TryGetProperty("tools", out _));
    }

    private static AiRequest CreateRequest() => new()
    {
        ModelId = "grok-4-fast",
        ApiKey = "test-key",
        Messages = [new AiMessage { Role = "user", Content = "É uma solução para WhatsApp?" }],
        SystemPrompt = "Responda em JSON.",
        MaxTokens = 120
    };

    private static HttpResponseMessage CreateResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public string? LastRequestBody { get; private set; }
        public string? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.IsAbsoluteUri == true
                ? request.RequestUri.PathAndQuery.TrimStart('/')
                : request.RequestUri?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
