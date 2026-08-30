using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.IntegrationTests.Admin;

[Collection("IntegrationTests")]
public sealed class AiUsageVisibilityTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task TenantUsageHidesTokensWhilePlatformAdminSeesActualTenantUsage()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"usage-visibility-{suffix}@test.com";
        Guid tenantId;

        await using (var db = await factory.GetDbContextAsync())
        {
            var plan = await db.SubscriptionPlans.FirstAsync(item => item.Code == "STAR");
            var tenant = Tenant.Create($"Usage {suffix}", $"usage-{suffix}", plan.Id, 1, 0, 2, 1_500);
            tenant.Activate();
            var user = User.Create(email, "Usage Owner");
            user.Activate(BCrypt.Net.BCrypt.HashPassword("Usage@123"));
            var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
            membership.Activate();

            tenantId = tenant.Id;
            db.Tenants.Add(tenant);
            db.Users.Add(user);
            db.TenantMemberships.Add(membership);
            db.UsageLedger.AddRange(
                UsageLedger.Create(tenantId, "openai", "input_tokens", $"input-{suffix}", 70, "tokens", 10, "USD", 1),
                UsageLedger.Create(tenantId, "openai", "output_tokens", $"output-{suffix}", 30, "tokens", 20, "USD", 1),
                UsageLedger.Create(tenantId, "openai", UsageMetricNames.AiResponses, $"reply-{suffix}", 1, "responses"));
            await db.SaveChangesAsync();
        }

        var tenantClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await tenantClient.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Usage@123" }))
            .EnsureSuccessStatusCode();
        var tenantResponse = await tenantClient.GetAsync("/api/usage");

        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenantPayload = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(tenantPayload.TryGetProperty("entries", out _));
        Assert.False(tenantPayload.TryGetProperty("tokens", out _));
        Assert.False(tenantPayload.TryGetProperty("estimatedCostMinorUnits", out _));
        Assert.Equal(1, tenantPayload.GetProperty("aiResponseQuota").GetProperty("used").GetInt64());

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await adminClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "admin@test.com",
            Password = "Admin@12345!"
        })).EnsureSuccessStatusCode();
        var adminResponse = await adminClient.GetAsync($"/api/admin/tenants/{tenantId}/ai-usage");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var adminPayload = await adminResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(70, adminPayload.GetProperty("tokens").GetProperty("input").GetInt64());
        Assert.Equal(30, adminPayload.GetProperty("tokens").GetProperty("output").GetInt64());
        Assert.Equal(100, adminPayload.GetProperty("tokens").GetProperty("total").GetInt64());
        Assert.Equal("openai", adminPayload.GetProperty("byProvider")[0].GetProperty("provider").GetString());
    }
}
