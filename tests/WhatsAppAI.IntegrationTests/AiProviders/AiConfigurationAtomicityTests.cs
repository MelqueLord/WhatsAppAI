using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.AiProviders;

[Collection("IntegrationTests")]
public sealed class AiConfigurationAtomicityTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task InstructionsConflictDoesNotPersistCredentialWhenBotVersionIsStale()
    {
        var (client, tenantId) = await CreateTenantAsync();

        using var providerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/ai")
        {
            Content = JsonContent.Create(new { provider = "openai", modelId = "gpt-4o-mini", apiKey = "key" })
        };
        providerRequest.Headers.TryAddWithoutValidation("If-Match", "0");
        (await client.SendAsync(providerRequest)).EnsureSuccessStatusCode();

        using var firstInstructions = new HttpRequestMessage(HttpMethod.Put, "/api/integrations/ai/instructions")
        {
            Content = JsonContent.Create(new
            {
                systemPrompt = "prompt-current",
                maxTokensPerResponse = 180,
                confidenceThreshold = 0.5,
                routingQueueIds = Array.Empty<Guid>(),
                routingTagIds = Array.Empty<Guid>()
            })
        };
        firstInstructions.Headers.TryAddWithoutValidation("If-Match", "0");
        (await client.SendAsync(firstInstructions)).EnsureSuccessStatusCode();

        using var staleInstructions = new HttpRequestMessage(HttpMethod.Put, "/api/integrations/ai/instructions")
        {
            Content = JsonContent.Create(new
            {
                systemPrompt = "prompt-must-not-persist",
                maxTokensPerResponse = 220,
                confidenceThreshold = 0.8,
                routingQueueIds = Array.Empty<Guid>(),
                routingTagIds = Array.Empty<Guid>()
            })
        };
        staleInstructions.Headers.TryAddWithoutValidation("If-Match", "1");
        staleInstructions.Headers.TryAddWithoutValidation("If-Match-Bot", "0");

        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(staleInstructions)).StatusCode);

        var config = await client.GetFromJsonAsync<JsonElement>("/api/integrations/ai");
        Assert.Equal("prompt-current", config.GetProperty("systemPrompt").GetString());
        Assert.Equal(1U, config.GetProperty("version").GetUInt32());
        Assert.Equal(0.5, config.GetProperty("confidenceThreshold").GetDouble());

        await using var db = await factory.GetDbContextAsync();
        Assert.Equal(1, await db.AuditLogs.IgnoreQueryFilters()
            .CountAsync(a => a.TenantId == tenantId && a.Action == "AI.InstructionsUpdated"));
    }

    [Fact]
    public async Task AiActivationRequiresApprovedModelEvaluation()
    {
        var (client, tenantId) = await CreateTenantAsync();

        using var providerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/ai")
        {
            Content = JsonContent.Create(new { provider = "openai", modelId = "gpt-4o-mini", apiKey = "key" })
        };
        providerRequest.Headers.TryAddWithoutValidation("If-Match", "0");
        (await client.SendAsync(providerRequest)).EnsureSuccessStatusCode();

        using var toggleRequest = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/ai/toggle")
        {
            Content = JsonContent.Create(new { enabled = true })
        };
        toggleRequest.Headers.TryAddWithoutValidation("If-Match-Bot", "0");
        var blocked = await client.SendAsync(toggleRequest);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("model_evaluation_required", await blocked.Content.ReadAsStringAsync());

        var db = await factory.GetDbContextAsync();
        var evaluation = ModelEvaluation.Create(
            tenantId, "gpt-4o-mini", "owner", 0.9, 0.1, 0.95, 0.2m, 500);
        evaluation.Approve();
        db.ModelEvaluations.Add(evaluation);
        await db.SaveChangesAsync();

        using var enabledRequest = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/ai/toggle")
        {
            Content = JsonContent.Create(new { enabled = true })
        };
        enabledRequest.Headers.TryAddWithoutValidation("If-Match-Bot", "0");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(enabledRequest)).StatusCode);
    }

    [Fact]
    public async Task AiActivationRequiresEvaluationForConfiguredProvider()
    {
        var (client, tenantId) = await CreateTenantAsync();

        using var providerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/ai")
        {
            Content = JsonContent.Create(new { provider = "openai", modelId = "gpt-4o-mini", apiKey = "key" })
        };
        providerRequest.Headers.TryAddWithoutValidation("If-Match", "0");
        (await client.SendAsync(providerRequest)).EnsureSuccessStatusCode();

        await using (var db = await factory.GetDbContextAsync())
        {
            var evaluation = ModelEvaluation.Create(
                tenantId, "gpt-4o-mini", "owner", 0.9, 0.1, 0.95, 0.2m, 500, provider: "gemini");
            evaluation.Approve();
            db.ModelEvaluations.Add(evaluation);
            await db.SaveChangesAsync();
        }

        using var toggleRequest = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/ai/toggle")
        {
            Content = JsonContent.Create(new { enabled = true })
        };
        toggleRequest.Headers.TryAddWithoutValidation("If-Match-Bot", "0");
        var blocked = await client.SendAsync(toggleRequest);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("model_evaluation_required", await blocked.Content.ReadAsStringAsync());
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantAsync()
    {
        var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var plan = await db.SubscriptionPlans.FirstAsync(p => p.Code == "IA_BOT");
        var tenant = Tenant.Create($"Atomic AI {suffix}", $"atomic-ai-{suffix}", plan.Id);
        tenant.Activate();
        db.Tenants.Add(tenant);

        var user = User.Create($"atomic-owner-{suffix}@test.com", "Atomic Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("Test@123"));
        db.Users.Add(user);
        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = $"atomic-owner-{suffix}@test.com",
            Password = "Test@123"
        });
        return (client, tenant.Id);
    }
}
