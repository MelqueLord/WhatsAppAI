using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Anthropic;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AnthropicProviderTests : IDisposable
{
    private readonly AnthropicProvider _provider;
    private readonly TestHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public AnthropicProviderTests()
    {
        _handler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.anthropic.com/")
        };
        _provider = new AnthropicProvider(_httpClient, NullLogger<AnthropicProvider>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task GetResponseAsync_ParsesReplyDecision()
    {
        var response = """
        {
            "id": "msg_123", "type": "message", "role": "assistant",
            "content": [{ "type": "text", "text": "{\"action\":\"reply\",\"text\":\"Olá!\",\"confidence\":0.95}" }],
            "model": "claude-sonnet-4-20250514", "stop_reason": "end_turn",
            "usage": { "input_tokens": 15, "output_tokens": 8 }
        }
        """;
        _handler.Response = CreateResponse(response);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal("Olá!", result.Decision.Text);
        Assert.Equal(0.95, result.Decision.Confidence);
        Assert.Equal(15, result.InputTokens);
        Assert.Equal(8, result.OutputTokens);
        Assert.Equal("msg_123", result.RawResponseId);
    }

    [Fact]
    public async Task GetResponseAsync_ParsesHandoffDecision()
    {
        var response = """
        {
            "id": "msg_456", "type": "message", "role": "assistant",
            "content": [{ "type": "text", "text": "{\"action\":\"handoff\",\"text\":\"\",\"handoff_reason\":\"Complex request\",\"confidence\":0.2}" }],
            "model": "claude-haiku-3-5-20241022", "stop_reason": "end_turn",
            "usage": { "input_tokens": 10, "output_tokens": 5 }
        }
        """;
        _handler.Response = CreateResponse(response);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("Complex request", result.Decision.HandoffReason);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task GetResponseAsync_RejectsPlainTextResponse()
    {
        var response = """
        {
            "id": "msg_789", "type": "message", "role": "assistant",
            "content": [{ "type": "text", "text": "Just plain text" }],
            "model": "claude-sonnet-4-20250514", "stop_reason": "end_turn",
            "usage": { "input_tokens": 10, "output_tokens": 5 }
        }
        """;
        _handler.Response = CreateResponse(response);

        var result = await _provider.GetResponseAsync(CreateRequest());

        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("invalid_response", result.Decision.HandoffReason);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsOnApiError()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        { Content = new StringContent("{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\"}}") };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _provider.GetResponseAsync(CreateRequest()));
    }

    [Fact]
    public async Task GetResponseAsync_SendsCorrectHeaders()
    {
        var response = """
        { "id": "msg_1", "type": "message", "role": "assistant", "content": [{ "type": "text", "text": "ok" }], "stop_reason": "end_turn", "usage": { "input_tokens": 1, "output_tokens": 1 } }
        """;
        _handler.Response = CreateResponse(response);

        await _provider.GetResponseAsync(CreateRequest(apiKey: "sk-ant-test123"));

        Assert.Contains("v1/messages", _handler.LastRequestUri!.ToString());
        Assert.True(_handler.LastRequestHeaders!.Contains("x-api-key"));
        Assert.Contains("sk-ant-test123", _handler.LastRequestHeaders.GetValues("x-api-key"));
        Assert.True(_handler.LastRequestHeaders.Contains("anthropic-version"));
        Assert.Contains("2023-06-01", _handler.LastRequestHeaders.GetValues("anthropic-version"));
    }

    [Fact]
    public async Task GetResponseAsync_SendsSystemAsTopLevelField()
    {
        var response = """
        { "id": "msg_1", "type": "message", "role": "assistant", "content": [{ "type": "text", "text": "ok" }], "stop_reason": "end_turn", "usage": {} }
        """;
        _handler.Response = CreateResponse(response);

        await _provider.GetResponseAsync(CreateRequest());

        var body = _handler.LastRequestBody!;
        Assert.Contains("\"system\":", body);
        Assert.Contains("You are helpful.", body);
    }

    private static AiRequest CreateRequest(string? apiKey = null) => new()
    {
        ModelId = "claude-sonnet-4-20250514",
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
