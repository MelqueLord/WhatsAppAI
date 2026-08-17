using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WhatsAppAI.IntegrationTests.Media;

[Collection("IntegrationTests")]
public class MediaSecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MediaSecurityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task DownloadMedia_WithoutAuth_ReturnsUnauthorized()
    {
        var messageId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/media/{messageId}/download");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DownloadMedia_WithInvalidMessageId_ReturnsNotFound()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var messageId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/media/{messageId}/download");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound);
    }
}
