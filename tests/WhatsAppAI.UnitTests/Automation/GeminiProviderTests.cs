using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Gemini;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class GeminiProviderTests : IDisposable
{
    private readonly GeminiProvider _provider;
    private readonly TestHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public GeminiProviderTests()
    {
        _handler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
        _provider = new GeminiProvider(_httpClient, NullLogger<GeminiProvider>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task GetResponseAsync_ParsesReplyDecision()
    {
        var geminiResponse = """
        {
            "candidates": [{
                "content": { "parts": [{ "text": "{\"action\":\"reply\",\"text\":\"Hello!\",\"confidence\":0.9}" }], "role": "model" },
                "finishReason": "STOP"
            }],
            "usageMetadata": { "promptTokenCount": 10, "candidatesTokenCount": 5, "totalTokenCount": 15 }
        }
        """;
        _handler.Response = CreateResponse(geminiResponse);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal("Hello!", result.Decision.Text);
        Assert.Equal(0.9, result.Decision.Confidence);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
    }

    [Fact]
    public async Task GetResponseAsync_ParsesHandoffDecision()
    {
        var geminiResponse = """
        {
            "candidates": [{
                "content": { "parts": [{ "text": "{\"action\":\"handoff\",\"text\":\"\",\"handoff_reason\":\"Low confidence\",\"confidence\":0.3}" }], "role": "model" }
            }],
            "usageMetadata": { "promptTokenCount": 10, "candidatesTokenCount": 5, "totalTokenCount": 15 }
        }
        """;
        _handler.Response = CreateResponse(geminiResponse);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("Low confidence", result.Decision.HandoffReason);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task GetResponseAsync_HandlesPlainTextResponse()
    {
        var geminiResponse = """
        {
            "candidates": [{
                "content": { "parts": [{ "text": "Just a plain answer" }], "role": "model" }
            }],
            "usageMetadata": { "promptTokenCount": 10, "candidatesTokenCount": 5, "totalTokenCount": 15 }
        }
        """;
        _handler.Response = CreateResponse(geminiResponse);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal("Just a plain answer", result.Decision.Text);
        Assert.Equal(0.5, result.Decision.Confidence);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsOnApiError()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        { Content = new StringContent("{\"error\":\"Invalid API key\"}") };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _provider.GetResponseAsync(CreateRequest()));
    }

    [Fact]
    public async Task GetResponseAsync_SendsCorrectUrl()
    {
        var geminiResponse = """
        { "candidates": [{ "content": { "parts": [{ "text": "ok" }], "role": "model" } }], "usageMetadata": { "promptTokenCount": 1, "candidatesTokenCount": 1, "totalTokenCount": 2 } }
        """;
        _handler.Response = CreateResponse(geminiResponse);

        await _provider.GetResponseAsync(CreateRequest(modelId: "gemini-3.6-flash"));

        Assert.Contains("v1beta/models/gemini-3.6-flash:generateContent", _handler.LastRequestUri!.ToString());
        Assert.Contains("key=test-key", _handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task GetResponseAsync_MapsAssistantRoleToModel()
    {
        var geminiResponse = """
        { "candidates": [{ "content": { "parts": [{ "text": "ok" }], "role": "model" } }], "usageMetadata": {} }
        """;
        _handler.Response = CreateResponse(geminiResponse);

        var request = CreateRequest(messages: [
            new AiMessage { Role = "user", Content = "Hi" },
            new AiMessage { Role = "assistant", Content = "Hello" },
            new AiMessage { Role = "user", Content = "Help" }
        ]);

        await _provider.GetResponseAsync(request);

        var body = _handler.LastRequestBody!;
        Assert.Contains("\"role\":\"user\"", body);
        Assert.Contains("\"role\":\"model\"", body);
        Assert.DoesNotContain("\"role\":\"assistant\"", body);
    }

    private static AiRequest CreateRequest(string? modelId = null, AiMessage[]? messages = null) => new()
    {
            ModelId = modelId ?? "gemini-3.1-pro-preview",
        ApiKey = "test-key",
        Messages = messages ?? [new AiMessage { Role = "user", Content = "Hello" }],
        SystemPrompt = "You are helpful.",
        MaxTokens = 1024
    };

    private static HttpResponseMessage CreateResponse(string json) => new(HttpStatusCode.OK)
    { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public Uri? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestBody = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            return Response;
        }
    }
}
