using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Webhooks;

[Collection("IntegrationTests")]
public sealed class WhatsAppWebSessionLeaseTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private const string ServiceToken = "integration-test-whatsapp-web-service-token-at-least-32-bytes";
    private const string PreviousServiceToken = "integration-test-whatsapp-web-previous-service-token";
    private readonly TestWebApplicationFactory _factory;

    public WhatsAppWebSessionLeaseTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.WhatsAppWebSessionLeases.RemoveRange(context.WhatsAppWebSessionLeases);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Internal_bridge_routes_require_service_identity_and_the_legacy_webhook_route_is_unavailable()
    {
        var sessionId = $"{Guid.NewGuid():D}-qr-1";
        var leaseRequest = new { instanceId = "bridge-a", instanceUrl = "http://whatsapp-web-a:3020" };

        using var unauthenticated = _factory.CreateClient();
        var noIdentity = await unauthenticated.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{sessionId}/lease", leaseRequest);

        using var incorrectIdentity = _factory.CreateClient();
        incorrectIdentity.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Id", "untrusted-service");
        incorrectIdentity.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Token", ServiceToken);
        var wrongService = await incorrectIdentity.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{sessionId}/lease", leaseRequest);

        var legacyRoute = await unauthenticated.PostAsync("/api/webhooks/whatsapp-web", null);

        Assert.Equal(HttpStatusCode.Unauthorized, noIdentity.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongService.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, legacyRoute.StatusCode);
    }

    [Fact]
    public async Task Internal_bridge_routes_accept_the_previous_token_during_rotation()
    {
        var sessionId = $"{Guid.NewGuid():D}-qr-1";
        using var bridge = _factory.CreateClient();
        bridge.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Id", "whatsapp-web");
        bridge.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Token", PreviousServiceToken);

        var response = await bridge.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{sessionId}/lease",
            new { instanceId = "bridge-rotation", instanceUrl = "http://whatsapp-web-rotation:3020" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Lease_allows_one_owner_blocks_competitor_and_transfers_after_expiration()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = $"{tenantId:D}-qr-1";

        using var firstBridge = CreateBridgeClient("bridge-a");
        using var secondBridge = CreateBridgeClient("bridge-b");

        var firstRequest = new { instanceId = "bridge-a", instanceUrl = "http://whatsapp-web-a:3020" };
        var secondRequest = new { instanceId = "bridge-b", instanceUrl = "http://whatsapp-web-b:3020" };

        var acquireResponses = await Task.WhenAll(
            firstBridge.PutAsJsonAsync($"/internal/whatsapp-web/sessions/{sessionId}/lease", firstRequest),
            secondBridge.PutAsJsonAsync($"/internal/whatsapp-web/sessions/{sessionId}/lease", secondRequest));

        Assert.Single(acquireResponses, response => response.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(acquireResponses, response => response.StatusCode == HttpStatusCode.Conflict);
        var owner = await conflict.Content.ReadFromJsonAsync<LeaseResponse>();
        Assert.NotNull(owner);
        Assert.True(owner!.OwnerUrl is "http://whatsapp-web-a:3020" or "http://whatsapp-web-b:3020");

        var winningBridge = acquireResponses[0].StatusCode == HttpStatusCode.OK ? firstBridge : secondBridge;
        var losingBridge = acquireResponses[0].StatusCode == HttpStatusCode.OK ? secondBridge : firstBridge;

        var rejectedSave = await losingBridge.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{sessionId}",
            new { payload = "encrypted-session-state" });
        Assert.Equal(HttpStatusCode.BadRequest, rejectedSave.StatusCode);

        var acceptedSave = await winningBridge.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{sessionId}",
            new { payload = "encrypted-session-state" });
        Assert.Equal(HttpStatusCode.NoContent, acceptedSave.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lease = await context.WhatsAppWebSessionLeases.SingleAsync(item => item.SessionId == sessionId);
            context.Entry(lease).Property(item => item.ExpiresAt).CurrentValue = DateTime.UtcNow.AddSeconds(-1);
            await context.SaveChangesAsync();
        }

        var takeover = await losingBridge.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{sessionId}/lease",
            acquireResponses[0].StatusCode == HttpStatusCode.OK ? secondRequest : firstRequest);
        Assert.Equal(HttpStatusCode.OK, takeover.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transferredLease = await verificationContext.WhatsAppWebSessionLeases.SingleAsync(item => item.SessionId == sessionId);
        Assert.Equal(
            acquireResponses[0].StatusCode == HttpStatusCode.OK ? "bridge-b" : "bridge-a",
            transferredLease.OwnerInstanceId);
    }

    [Fact]
    public async Task Webhook_from_bridge_without_current_lease_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = $"{tenantId:D}-qr-1";
        using var ownerBridge = CreateBridgeClient("bridge-a");
        using var formerBridge = CreateBridgeClient("bridge-b");

        var lease = await ownerBridge.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{sessionId}/lease",
            new { instanceId = "bridge-a", instanceUrl = "http://whatsapp-web-a:3020" });
        Assert.Equal(HttpStatusCode.OK, lease.StatusCode);

        var webhook = await formerBridge.PostAsJsonAsync("/internal/whatsapp-web/events", new
        {
            entry = new[]
            {
                new
                {
                    id = "bridge-event",
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                metadata = new { phone_number_id = $"qr:{tenantId:D}:1" },
                                messages = new[] { new { id = "message-from-former-owner" } }
                            }
                        }
                    }
                }
            }
        });

        Assert.Equal(HttpStatusCode.Conflict, webhook.StatusCode);
    }

    [Fact]
    public async Task Multiple_qr_channels_of_the_same_tenant_have_independent_leases()
    {
        var tenantId = Guid.NewGuid();
        using var bridge = CreateBridgeClient("bridge-a");

        var lineOne = await bridge.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{tenantId:D}-qr-1/lease",
            new { instanceId = "bridge-a", instanceUrl = "http://whatsapp-web-a:3020" });
        var lineTwo = await bridge.PutAsJsonAsync(
            $"/internal/whatsapp-web/sessions/{tenantId:D}-qr-2/lease",
            new { instanceId = "bridge-a", instanceUrl = "http://whatsapp-web-a:3020" });

        Assert.Equal(HttpStatusCode.OK, lineOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, lineTwo.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var leases = await context.WhatsAppWebSessionLeases
            .Where(item => item.TenantId == tenantId)
            .ToListAsync();
        Assert.Equal(2, leases.Count);
        Assert.Equal([1, 2], leases.Select(item => item.LineNumber).OrderBy(line => line).ToArray());
    }

    private HttpClient CreateBridgeClient(string instanceId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Id", "whatsapp-web");
        client.DefaultRequestHeaders.Add("X-WhatsApp-Web-Service-Token", ServiceToken);
        client.DefaultRequestHeaders.Add("X-WhatsApp-Web-Instance", instanceId);
        return client;
    }

    private sealed record LeaseResponse(string OwnerUrl);
}
