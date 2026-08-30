using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Infrastructure.Meta;
using WhatsAppAI.Infrastructure.WhatsApp;

namespace WhatsAppAI.UnitTests.Meta;

public sealed class MetaClientAuthorizationTests : IDisposable
{
    private readonly RecordingHandler handler = new();
    private readonly HttpClient httpClient;

    public MetaClientAuthorizationTests()
    {
        httpClient = new HttpClient(handler);
    }

    [Fact]
    public async Task WhatsAppClient_UsesRequestScopedAuthorization()
    {
        var client = new WhatsAppClient(httpClient, NullLogger<WhatsAppClient>.Instance);

        await Task.WhenAll(
            client.TestConnectionAsync("phone-a", "token-a"),
            client.TestConnectionAsync("phone-b", "token-b"),
            client.SendTextMessageAsync("phone-c", "token-c", "recipient", "message"));

        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Contains("Bearer token-a", handler.Authorizations);
        Assert.Contains("Bearer token-b", handler.Authorizations);
        Assert.Contains("Bearer token-c", handler.Authorizations);
    }

    [Fact]
    public async Task WhatsAppClient_SendsOfficialTemplatePayload()
    {
        var client = new WhatsAppClient(httpClient, NullLogger<WhatsAppClient>.Instance);

        var result = await client.SendTemplateMessageAsync(
            "phone-template", "token-template", "recipient", "welcome_customer", "pt_BR", ["Maria"]);

        Assert.True(result.IsSuccess);
        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = document.RootElement;
        Assert.Equal("template", root.GetProperty("type").GetString());
        Assert.Equal("welcome_customer", root.GetProperty("template").GetProperty("name").GetString());
        Assert.Equal("pt_BR", root.GetProperty("template").GetProperty("language").GetProperty("code").GetString());
        Assert.Equal("Maria", root.GetProperty("template").GetProperty("components")[0]
            .GetProperty("parameters")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task WhatsAppWebClient_RejectsTemplates()
    {
        var client = new WhatsAppWebClient(new HttpClient(), new ConfigurationBuilder().Build());

        var result = await client.SendTemplateMessageAsync(
            "qr:tenant:1", "whatsapp-web", "recipient", "welcome_customer", "pt_BR", []);

        Assert.False(result.IsSuccess);
        Assert.Contains("official WhatsApp API", result.ErrorMessage);
    }

    [Fact]
    public async Task MediaGateway_UsesRequestScopedAuthorizationForBothRequests()
    {
        var gateway = new MediaGateway(httpClient, NullLogger<MediaGateway>.Instance);

        var result = await gateway.DownloadAsync("media-id", "media-token");

        Assert.True(result.IsSuccess);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Equal(2, handler.Authorizations.Count);
        Assert.All(handler.Authorizations, value => Assert.Equal("Bearer media-token", value));
    }

    public void Dispose()
    {
        httpClient.Dispose();
        handler.Dispose();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public ConcurrentBag<string> Authorizations { get; } = [];
        public ConcurrentBag<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            if (request.Content is not null)
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));

            HttpResponseMessage response;
            if (request.RequestUri?.Host == "media.example")
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                };
            }
            else
            {
                var content = request.RequestUri?.AbsolutePath.Contains(
                    "media-id",
                    StringComparison.Ordinal) == true
                    ? "{\"url\":\"https://media.example/content\"}"
                    : "{\"display_phone_number\":\"123\",\"quality_rating\":\"GREEN\"}";
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content)
                };
            }

            return response;
        }
    }
}
