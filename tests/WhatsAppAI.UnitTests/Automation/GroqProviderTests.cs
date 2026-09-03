using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure.Groq;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class GroqProviderTests : IDisposable
{
    private readonly RecordingHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly GroqProvider _provider;

    public GroqProviderTests()
    {
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://api.groq.com/") };
        _provider = new GroqProvider(_httpClient, NullLogger<GroqProvider>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task GetResponseAsync_RequestsJsonObjectFormat()
    {
        _handler.Response = CreateResponse("""
        { "id": "groq-1", "choices": [{ "message": { "content": "{\"action\":\"reply\",\"text\":\"Olá!\",\"confidence\":0.9,\"handoff_reason\":null,\"queue\":null,\"tags\":[]}" } }], "usage": { "prompt_tokens": 10, "completion_tokens": 5 } }
        """);

        var result = await _provider.GetResponseAsync(CreateRequest());

        using var requestBody = JsonDocument.Parse(_handler.LastRequestBody!);
        var responseFormat = requestBody.RootElement.GetProperty("response_format");

        Assert.Equal("json_object", responseFormat.GetProperty("type").GetString());
        Assert.False(responseFormat.TryGetProperty("json_schema", out _));
        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal("Olá!", result.Decision.Text);
    }

    private static AiRequest CreateRequest() => new()
    {
        ModelId = "openai/gpt-oss-120b",
        ApiKey = "test-key",
        Messages = [new AiMessage { Role = "user", Content = "Olá" }],
        SystemPrompt = "You are helpful.",
        MaxTokens = 240
    };

    private static HttpResponseMessage CreateResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
