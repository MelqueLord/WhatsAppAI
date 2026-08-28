using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var plan = await db.SubscriptionPlans.FirstAsync(p => p.Code == "IA_BOT");
        var tenant = Tenant.Create($"AI Tenant {suffix}", $"ai-tenant-{suffix}", plan.Id);
        tenant.Activate();
        db.Tenants.Add(tenant);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test@123");
        var email = $"owner-ai-{suffix}@test.com";
        var user = User.Create(email, "AI Owner");
        user.Activate(passwordHash);
        db.Users.Add(user);

        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        db.TenantMemberships.Add(membership);

        await db.SaveChangesAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Test@123" });
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
        Assert.Equal(6, providers.Length);
        Assert.Contains(providers, p => p.Id == "openai");
        Assert.Contains(providers, p => p.Id == "gemini");
        Assert.Contains(providers, p => p.Id == "anthropic");
        Assert.Contains(providers, p => p.Id == "xiaomi");
        Assert.Contains(providers, p => p.Id == "grok");
        Assert.Contains(providers, p => p.Id == "groq");
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
            provider = "gemini", modelId = "gemini-3.6-flash", apiKey = "AIza-test-gemini"
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
        Assert.Equal("gemini-3.6-flash", geminiCred.ModelId);
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

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.True(root.GetProperty("configured").GetBoolean());
        Assert.Equal("xiaomi", root.GetProperty("provider").GetString());
        Assert.Equal("mimo-v2.5-pro", root.GetProperty("modelId").GetString());
        Assert.False(root.GetProperty("aiActive").GetBoolean());
    }

    [Fact]
    public async Task UpdateInstructions_RequiresIfMatchAndRejectsStaleVersion()
    {
        var (client, _) = await CreateTenantWithAiPlanAsync();
        await client.PostAsJsonAsync("/api/integrations/ai", new
        {
            provider = "openai", modelId = "gpt-4o-mini", apiKey = "sk-test-openai"
        });

        var payload = new
        {
            systemPrompt = "Versão atual",
            maxTokensPerResponse = 180,
            confidenceThreshold = 0.5,
            routingQueueIds = Array.Empty<Guid>(),
            routingTagIds = Array.Empty<Guid>()
        };

        var missingHeader = await client.PutAsJsonAsync(
            "/api/integrations/ai/instructions", payload);
        Assert.Equal(HttpStatusCode.BadRequest, missingHeader.StatusCode);

        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Put, "/api/integrations/ai/instructions")
        {
            Content = JsonContent.Create(payload)
        };
        firstRequest.Headers.TryAddWithoutValidation("If-Match", "0");
        var firstUpdate = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        using var staleRequest = new HttpRequestMessage(
            HttpMethod.Put, "/api/integrations/ai/instructions")
        {
            Content = JsonContent.Create(payload with { systemPrompt = "Versão obsoleta" })
        };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", "0");
        var staleUpdate = await client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        var config = await client.GetFromJsonAsync<JsonElement>("/api/integrations/ai");
        Assert.Equal("Versão atual", config.GetProperty("systemPrompt").GetString());
        Assert.Equal(1U, config.GetProperty("version").GetUInt32());
    }

    private sealed record ProviderDto(string Id, string Name, object[] Models);
}
