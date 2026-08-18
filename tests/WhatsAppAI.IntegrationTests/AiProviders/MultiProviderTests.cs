using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.AiProviders;

[Collection("IntegrationTests")]
public class MultiProviderTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MultiProviderTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient client, Guid tenantId)> CreateTenantWithAiPlanAsync()
    {
        var db = await _factory.GetDbContextAsync();
        var plan = await db.SubscriptionPlans.FirstAsync(p => p.Code == "IA+BOT");
        var tenant = Tenant.Create("AI Tenant", "ai-tenant", plan.Id);
        tenant.Activate();
        db.Tenants.Add(tenant);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test@123");
        var user = User.Create("owner-ai@test.com", "AI Owner");
        user.Activate(passwordHash);
        db.Users.Add(user);

        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        db.TenantMemberships.Add(membership);

        await db.SaveChangesAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.PostAsJsonAsync("/api/auth/login", new { Email = "owner-ai@test.com", Password = "Test@123" });
        return (client, tenant.Id);
    }

    [Fact]
    public async Task GetProviders_ReturnsAllFour()
    {
        var (client, _) = await CreateTenantWithAiPlanAsync();

        var response = await client.GetAsync("/api/integrations/ai/providers");
        response.EnsureSuccessStatusCode();

        var providers = await response.Content.ReadFromJsonAsync<ProviderDto[]>();
        Assert.NotNull(providers);
        Assert.Equal(4, providers.Length);
        Assert.Contains(providers, p => p.Id == "openai");
        Assert.Contains(providers, p => p.Id == "gemini");
        Assert.Contains(providers, p => p.Id == "anthropic");
        Assert.Contains(providers, p => p.Id == "xiaomi");
    }

    [Fact]
    public async Task SaveProvider_SwitchingPreservesOldCredential()
    {
        var (client, tenantId) = await CreateTenantWithAiPlanAsync();

        // Save OpenAI credential
        var r1 = await client.PostAsJsonAsync("/api/integrations/ai", new
        {
            provider = "openai", modelId = "gpt-4o-mini", apiKey = "sk-test-openai"
        });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Save Gemini credential
        var r2 = await client.PostAsJsonAsync("/api/integrations/ai", new
        {
            provider = "gemini", modelId = "gemini-2.5-flash", apiKey = "AIza-test-gemini"
        });
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // Verify: old credential deactivated, new one active
        var db = await _factory.GetDbContextAsync();
        var credentials = await db.AiProviderCredentials.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId).ToListAsync();

        Assert.Equal(2, credentials.Count);

        var openaiCred = credentials.First(c => c.Provider == "openai");
        var geminiCred = credentials.First(c => c.Provider == "gemini");

        Assert.False(openaiCred.IsActive);
        Assert.True(geminiCred.IsActive);
        Assert.Equal("gemini-2.5-flash", geminiCred.ModelId);
    }

    [Fact]
    public async Task SaveProvider_UnsupportedProvider_ReturnsBadRequest()
    {
        var (client, _) = await CreateTenantWithAiPlanAsync();

        var response = await client.PostAsJsonAsync("/api/integrations/ai", new
        {
            provider = "cohere", modelId = "command-r", apiKey = "test-key"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetConfig_ReturnsBotConfigAlongsideProvider()
    {
        var (client, _) = await CreateTenantWithAiPlanAsync();

        // Save provider with bot config
        await client.PostAsJsonAsync("/api/integrations/ai", new
        {
            provider = "xiaomi", modelId = "mimo-v2.5-pro", apiKey = "sk-xiaomi-test",
            botConfig = new { mode = "AiPowered", welcomeMessage = "Olá!", maxTokensPerResponse = 800 }
        });

        var response = await client.GetAsync("/api/integrations/ai");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<ConfigResponseDto>();
        Assert.NotNull(json);
        Assert.True(json.Configured);
        Assert.Equal("xiaomi", json.Provider);
        Assert.NotNull(json.BotConfig);
        Assert.Equal("AiPowered", json.BotConfig.Mode);
        Assert.Equal("Olá!", json.BotConfig.WelcomeMessage);
    }

    private sealed record ProviderDto(string Id, string Name, object[] Models);
    private sealed record ConfigResponseDto(bool Configured, string? Provider, string? ModelId, bool? IsActive, BotConfigDto? BotConfig);
    private sealed record BotConfigDto(string Mode, string? WelcomeMessage, string? FallbackMessage, int MaxTokensPerResponse, bool Enabled);
}
