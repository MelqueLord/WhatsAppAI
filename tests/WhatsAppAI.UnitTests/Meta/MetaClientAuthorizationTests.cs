using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Infrastructure.Meta;

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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);

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

            return Task.FromResult(response);
        }
    }
}
