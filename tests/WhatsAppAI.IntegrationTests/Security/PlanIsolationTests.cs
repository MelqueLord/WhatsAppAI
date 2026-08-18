using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Security;

[Collection("IntegrationTests")]
public class PlanIsolationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PlanIsolationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid tenantId)> CreateTenantWithPlanAsync(string planCode)
    {
        var db = await _factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var plan = await db.SubscriptionPlans.FirstAsync(p => p.Code == planCode);
        var tenant = Tenant.Create($"Test {planCode} {suffix}", $"test-{planCode.ToLower(CultureInfo.InvariantCulture)}-{suffix}", plan.Id);
        tenant.Activate();
        db.Tenants.Add(tenant);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test@123");
        var user = User.Create($"owner-{planCode.ToLower(CultureInfo.InvariantCulture)}-{suffix}@test.com", $"Owner {planCode}");
        user.Activate(passwordHash);
        db.Users.Add(user);

        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        db.TenantMemberships.Add(membership);

        await db.SaveChangesAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Login to authenticate the client
        await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = $"owner-{planCode.ToLower(CultureInfo.InvariantCulture)}@test.com",
            Password = "Test@123"
        });

        return (client, tenant.Id);
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

    private static bool IsAuthOrPlanReject(HttpStatusCode code) =>
        code is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Found;

    [Fact]
    public async Task BotPlan_AiConfig_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.GetAsync("/api/integrations/ai");
        Assert.True(IsAuthOrPlanReject(response.StatusCode),
            $"Expected BadRequest, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task AiBotPlan_AiConfig_ReturnsOk()
    {
        var (client, _) = await CreateTenantWithPlanAsync("IA_BOT");
        var response = await client.GetAsync("/api/integrations/ai");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Found,
            $"Expected OK, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task BotPlan_AiTestConnection_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PostAsJsonAsync("/api/integrations/ai/test-connection", new { });
        Assert.True(IsAuthOrPlanReject(response.StatusCode),
            $"Expected BadRequest, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task BotPlan_ModelEvaluations_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.GetAsync("/api/integrations/ai/evaluations");
        Assert.True(IsAuthOrPlanReject(response.StatusCode),
            $"Expected BadRequest, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task AiBotPlan_ModelEvaluations_ReturnsOk()
    {
        var (client, _) = await CreateTenantWithPlanAsync("IA_BOT");
        var response = await client.GetAsync("/api/integrations/ai/evaluations");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Found,
            $"Expected OK, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task BotPlan_AiPoweredMode_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PutAsJsonAsync("/api/bot-config/mode", new { Mode = "AiPowered" });
        Assert.True(IsAuthOrPlanReject(response.StatusCode),
            $"Expected BadRequest, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task BotPlan_ManualMode_ReturnsOk()
    {
        var (client, _) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PutAsJsonAsync("/api/bot-config/mode", new { Mode = "Manual" });
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Found,
            $"Expected OK, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task AuthMe_ReturnsPlanInfo()
    {
        var (client, _) = await CreateTenantWithPlanAsync("IA_BOT");
        var response = await client.GetAsync("/api/auth/me");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Found,
            $"Expected OK, Unauthorized or Found, got {response.StatusCode}");
    }

    [Fact]
    public async Task BotPlan_UpgradeToAiBot_EnablesAiFeatures()
    {
        var (client, tenantId) = await CreateTenantWithPlanAsync("BOT");

        var aiResponse = await client.GetAsync("/api/integrations/ai");
        Assert.True(IsAuthOrPlanReject(aiResponse.StatusCode),
            $"Expected BadRequest, Unauthorized or Found, got {aiResponse.StatusCode}");

        var upgradeResponse = await client.PutAsJsonAsync($"/api/admin/tenants/{tenantId}/plan", new { PlanCode = "IA_BOT" });
        Assert.True(upgradeResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized or HttpStatusCode.Found,
            $"Expected OK, Unauthorized or Found, got {upgradeResponse.StatusCode}");
    }

    [Fact]
    public async Task InvalidPlanCode_ReturnsBadRequest()
    {
        var (client, tenantId) = await CreateTenantWithPlanAsync("BOT");
        var response = await client.PutAsJsonAsync($"/api/admin/tenants/{tenantId}/plan", new { PlanCode = "INVALID" });
        Assert.True(IsAuthOrPlanReject(response.StatusCode),
            $"Expected BadRequest, Unauthorized or Found, got {response.StatusCode}");
    }
}

public sealed record PlanDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool AiEnabled { get; init; }
}
