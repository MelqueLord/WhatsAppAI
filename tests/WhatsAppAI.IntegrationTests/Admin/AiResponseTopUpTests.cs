using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.IntegrationTests.Admin;

[Collection("IntegrationTests")]
public sealed class AiResponseTopUpTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task TopUpAddsFiveHundredOnceAndReleasesQuotaSuspension()
    {
        Guid tenantId;
        await using (var db = await factory.GetDbContextAsync())
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var plan = await db.SubscriptionPlans.FirstAsync(item => item.Code == "IA_BOT");
            var tenant = Tenant.Create(
                $"Top-up {suffix}", $"top-up-{suffix}", plan.Id, monthlyAiResponseLimit: 1_500);
            tenant.Activate();
            tenantId = tenant.Id;
            db.Tenants.Add(tenant);
            db.UsageLedger.Add(UsageLedger.Create(
                tenantId, "openai", UsageMetricNames.AiResponses, $"used:{suffix}", 1_500, "responses"));
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "admin@test.com",
            Password = "Admin@12345!"
        })).EnsureSuccessStatusCode();

        const string idempotencyKey = "topup-regression-500";
        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/tenants/{tenantId}/ai-response-topups");
        firstRequest.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(firstResult.GetProperty("added").GetBoolean());
        Assert.Equal(500, firstResult.GetProperty("quantity").GetInt32());
        Assert.Equal(2_000, firstResult.GetProperty("limit").GetInt32());
        Assert.False(firstResult.GetProperty("aiSuspended").GetBoolean());

        using var repeatedRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/tenants/{tenantId}/ai-response-topups");
        repeatedRequest.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        var repeatedResponse = await client.SendAsync(repeatedRequest);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        var repeatedResult = await repeatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(repeatedResult.GetProperty("added").GetBoolean());
        Assert.Equal(500, repeatedResult.GetProperty("topUps").GetInt64());

        await using var verifyDb = await factory.GetDbContextAsync();
        Assert.Equal(1, await verifyDb.UsageLedger.IgnoreQueryFilters().CountAsync(entry =>
            entry.TenantId == tenantId && entry.Metric == UsageMetricNames.AiResponseTopUps));
        Assert.Equal(1, await verifyDb.AuditLogs.IgnoreQueryFilters().CountAsync(entry =>
            entry.TenantId == tenantId && entry.Action == "Tenant.AiQuotaTopUpAdded"));
    }
}
