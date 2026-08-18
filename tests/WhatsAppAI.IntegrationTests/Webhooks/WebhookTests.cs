using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;
using Xunit;

namespace WhatsAppAI.IntegrationTests.Webhooks;

[Collection("IntegrationTests")]
public class WebhookTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly string _appSecret = "test-app-secret-12345";
    private readonly string _verifyToken = "test-verify-token";

    public WebhookTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Requires Meta webhook configuration")]
    public async Task VerifyChallenge_ValidToken_ReturnsChallenge()
    {
        // Arrange
        var challenge = "test-challenge-12345";

        // Act
        var response = await _client.GetAsync(
            $"/api/webhooks/meta?hub_mode=subscribe&hub_verify_token={_verifyToken}&hub_challenge={challenge}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(challenge, content);
    }

    [Fact(Skip = "Requires Meta webhook configuration")]
    public async Task VerifyChallenge_InvalidToken_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/webhooks/meta?hub_mode=subscribe&hub_verify_token=wrong-token&hub_challenge=test");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(Skip = "Requires Meta webhook configuration")]
    public async Task ReceiveEvent_ValidSignature_ReturnsOk()
    {
        // Arrange
        var payload = CreateTestPayload("+5511999887665", "123456789", "Hello World");
        var signature = ComputeSignature(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/meta")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Skip = "Requires Meta webhook configuration")]
    public async Task ReceiveEvent_InvalidSignature_ReturnsBadRequest()
    {
        // Arrange
        var payload = CreateTestPayload("+5511999887665", "123456789", "Hello World");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/meta")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", "sha256=invalid-signature");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(Skip = "Requires Meta webhook configuration")]
    public async Task ReceiveEvent_MissingSignature_ReturnsBadRequest()
    {
        // Arrange
        var payload = CreateTestPayload("+5511999887665", "123456789", "Hello World");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/meta")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(Skip = "Requires Meta webhook configuration")]
    public async Task ReceiveEvent_DuplicatePayload_ReturnsOkAndDoesNotDuplicate()
    {
        // Arrange
        var payload = CreateTestPayload("+5511999887665", "123456789", "Hello World", "entry-123");
        var signature = ComputeSignature(payload);

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/meta")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request1.Headers.Add("X-Hub-Signature-256", signature);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/meta")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request2.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response1 = await _client.SendAsync(request1);
        var response2 = await _client.SendAsync(request2);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // Verify only one event was created
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventCount = context.WebhookEvents.Count();
        Assert.Equal(1, eventCount);
    }

    [Fact(Skip = "Requires Meta webhook configuration")]
    public async Task ReceiveEvent_LargePayload_HandlesCorrectly()
    {
        // Arrange - Create a large message content
        var largeContent = new string('A', 3000);
        var payload = CreateTestPayload("+5511999887665", "123456789", largeContent);
        var signature = ComputeSignature(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/meta")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", signature);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory(Skip = "Requires Meta webhook configuration")]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task ReceiveEvent_MassRetries_NoDuplicates(int count)
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < count; i++)
        {
            var payload = CreateTestPayload($"+5511999887{i:D4}", "123456789", $"Message {i}", $"entry-{i}");
            var signature = ComputeSignature(payload);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/meta")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Hub-Signature-256", signature);

            tasks.Add(_client.SendAsync(request));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Verify event count matches (each unique entry)
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventCount = context.WebhookEvents.Count();
        Assert.Equal(count, eventCount);
    }

    private string CreateTestPayload(string from, string phoneNumberId, string text, string? entryId = null)
    {
        var payload = new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = entryId ?? Guid.NewGuid().ToString(),
                    time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    changes = new[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                messaging_product = "whatsapp",
                                metadata = new
                                {
                                    display_phone_number = "5511999999999",
                                    phone_number_id = phoneNumberId
                                },
                                contacts = new[]
                                {
                                    new
                                    {
                                        wa_id = from,
                                        profile = new { name = "Test User" }
                                    }
                                },
                                messages = new[]
                                {
                                    new
                                    {
                                        from = from,
                                        id = $"msg-{Guid.NewGuid()}",
                                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                        type = "text",
                                        text = new { body = text }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

