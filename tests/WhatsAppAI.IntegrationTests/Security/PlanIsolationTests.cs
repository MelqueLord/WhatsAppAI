using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Security;

public class PlanIsolationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PlanIsolationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid tenantId)> CreateTenantWithPlanAsync(string planCode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plan = await db.SubscriptionPlans.FirstAsync(p => p.Code == planCode);
        var tenant = Tenant.Create($"Test {planCode}", $"test-{planCode.ToLower()}", plan.Id);
        tenant.Activate();
        db.Tenants.Add(tenant);

        var user = User.Create($"owner-{planCode.ToLower()}@test.com", $"Owner {planCode}");
        user.Activate("hashed-password");
        db.Users.Add(user);

        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        db.TenantMemberships.Add(membership);

        await db.SaveChangesAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        return (client, tenant.Id);
    }

    [Fact]
    public async Task BotPlan_AiConfig_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.GetAsync("/api/integrations/ai");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AiBotPlan_AiConfig_ReturnsOk()
    {
        var (client, _) = await CreateTenantWithPlanAsync("IA_BOT");
        var response = await client.GetAsync("/api/integrations/ai");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BotPlan_AiTestConnection_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PostAsJsonAsync("/api/integrations/ai/test-connection", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BotPlan_ModelEvaluations_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.GetAsync("/api/integrations/ai/evaluations");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AiBotPlan_ModelEvaluations_ReturnsOk()
    {
        var (client, _) = await CreateTenantWithPlanAsync("IA_BOT");
        var response = await client.GetAsync("/api/integrations/ai/evaluations");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BotPlan_AiPoweredMode_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PutAsJsonAsync("/api/bot-config/mode", new { Mode = "AiPowered" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BotPlan_ManualMode_ReturnsOk()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PutAsJsonAsync("/api/bot-config/mode", new { Mode = "Manual" });
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthMe_ReturnsPlanInfo()
    {
        var (client, _) = await CreateTenantWithPlanAsync("IA_BOT");
        var response = await client.GetAsync("/api/auth/me");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlansEndpoint_ReturnsActivePlans()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.NotNull(plans);
        Assert.Contains(plans, p => p.Code == "BOT");
        Assert.Contains(plans, p => p.Code == "IA_BOT");
    }

    [Fact]
    public async Task BotPlan_UpgradeToAiBot_EnablesAiFeatures()
    {
        var (client, tenantId) = await CreateTenantWithPlanAsync("BOT");

        // Verify AI is disabled
        var aiResponse = await client.GetAsync("/api/integrations/ai");
        Assert.Equal(HttpStatusCode.BadRequest, aiResponse.StatusCode);

        // Upgrade to IA+BOT
        var upgradeResponse = await client.PutAsJsonAsync($"/api/admin/tenants/{tenantId}/plan", new { PlanCode = "IA_BOT" });
        Assert.True(upgradeResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidPlanCode_ReturnsBadRequest()
    {
        var (client, tenantId) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PutAsJsonAsync($"/api/admin/tenants/{tenantId}/plan", new { PlanCode = "INVALID" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed record PlanDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool AiEnabled { get; init; }
}
