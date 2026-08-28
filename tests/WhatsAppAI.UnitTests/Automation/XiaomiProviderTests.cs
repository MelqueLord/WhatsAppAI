using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Xiaomi;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class XiaomiProviderTests : IDisposable
{
    private readonly XiaomiProvider _provider;
    private readonly TestHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public XiaomiProviderTests()
    {
        _handler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://api.xiaomi.com/") };
        _provider = new XiaomiProvider(_httpClient, NullLogger<XiaomiProvider>.Instance);
    }

    public void Dispose() { _httpClient.Dispose(); _handler.Dispose(); }

    [Fact]
    public async Task GetResponseAsync_ParsesReplyDecision()
    {
        _handler.Response = CreateResponse("""
        { "id": "cmpl-1", "choices": [{ "message": { "role": "assistant", "content": "{\"action\":\"reply\",\"text\":\"Olá!\",\"confidence\":0.9}" }, "finish_reason": "stop" }],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 } }
        """);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal("Olá!", result.Decision.Text);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
        Assert.Equal("cmpl-1", result.RawResponseId);
    }

    [Fact]
    public async Task GetResponseAsync_ParsesHandoffDecision()
    {
        _handler.Response = CreateResponse("""
        { "id": "cmpl-2", "choices": [{ "message": { "role": "assistant", "content": "{\"action\":\"handoff\",\"text\":\"\",\"handoff_reason\":\"Complex\",\"confidence\":0.2}" }, "finish_reason": "stop" }],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5 } }
        """);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("Complex", result.Decision.HandoffReason);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task GetResponseAsync_RejectsPlainTextResponse()
    {
        _handler.Response = CreateResponse("""
        { "id": "cmpl-3", "choices": [{ "message": { "role": "assistant", "content": "Plain answer" }, "finish_reason": "stop" }],
          "usage": { "prompt_tokens": 5, "completion_tokens": 3 } }
        """);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("invalid_response", result.Decision.HandoffReason);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsOnApiError()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        { Content = new StringContent("{\"error\":\"Invalid API key\"}") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.GetResponseAsync(CreateRequest()));
    }

    [Fact]
    public async Task GetResponseAsync_SendsBearerAuthAndCorrectUrl()
    {
        _handler.Response = CreateResponse("""
        { "id": "cmpl-4", "choices": [{ "message": { "role": "assistant", "content": "ok" }, "finish_reason": "stop" }], "usage": {} }
        """);

        await _provider.GetResponseAsync(CreateRequest(modelId: "mimo-v2.5-pro", apiKey: "sk-xiaomi-test"));

        Assert.Contains("v1/chat/completions", _handler.LastRequestUri!.ToString());
        Assert.Contains("Bearer sk-xiaomi-test", _handler.LastRequestHeaders!.Authorization!.ToString());
    }

    [Fact]
    public async Task GetResponseAsync_SendsSystemAsFirstMessage()
    {
        _handler.Response = CreateResponse("""
        { "id": "cmpl-5", "choices": [{ "message": { "role": "assistant", "content": "ok" }, "finish_reason": "stop" }], "usage": {} }
        """);

        await _provider.GetResponseAsync(CreateRequest());

        var body = _handler.LastRequestBody!;
        Assert.Contains("\"role\":\"system\"", body);
        Assert.Contains("You are helpful.", body);
    }

    private static AiRequest CreateRequest(string? modelId = null, string? apiKey = null) => new()
    {
        ModelId = modelId ?? "mimo-v2.5",
        ApiKey = apiKey ?? "test-key",
        Messages = [new AiMessage { Role = "user", Content = "Hello" }],
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
        public System.Net.Http.Headers.HttpRequestHeaders? LastRequestHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestBody = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            LastRequestHeaders = request.Headers;
            return Response;
        }
    }
}
