using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Integrations;

[Collection("IntegrationTests")]
public sealed class WhatsAppTemplateEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Tenant_owner_creates_one_durable_submission_per_idempotency_key()
    {
        var owner = await CreateTenantUserAsync(MembershipRole.TenantOwner);
        await AddOfficialLineAsync(owner.TenantId, 1, $"waba-{Guid.NewGuid():N}");
        const string key = "template-idempotency-key";

        var first = await CreateTemplateAsync(owner.Client, 1, key);
        var repeated = await CreateTemplateAsync(owner.Client, 1, key);

        await using var context = await factory.GetDbContextAsync();
        var submissions = await context.WhatsAppTemplateSubmissions.IgnoreQueryFilters()
            .Where(x => x.TenantId == owner.TenantId && x.IdempotencyKey == key).ToListAsync();

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        Assert.Single(submissions);
    }

    [Fact]
    public async Task Operator_cannot_create_a_template()
    {
        var user = await CreateTenantUserAsync(MembershipRole.Operator);
        await AddOfficialLineAsync(user.TenantId, 1, $"waba-{Guid.NewGuid():N}");

        var response = await CreateTemplateAsync(user.Client, 1, "operator-template-key");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Catalog_is_not_visible_from_another_tenant()
    {
        var first = await CreateTenantUserAsync(MembershipRole.TenantOwner);
        var second = await CreateTenantUserAsync(MembershipRole.TenantOwner);
        await AddOfficialLineAsync(first.TenantId, 1, $"waba-first-{Guid.NewGuid():N}");
        await AddOfficialLineAsync(second.TenantId, 1, $"waba-second-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Accepted, (await CreateTemplateAsync(first.Client, 1, "first-tenant-template-key")).StatusCode);

        var catalog = await second.Client.GetFromJsonAsync<JsonElement>("/api/integrations/whatsapp/official/1/templates");

        Assert.Equal(JsonValueKind.Array, catalog.GetProperty("templates").ValueKind);
        Assert.Equal(0, catalog.GetProperty("templates").GetArrayLength());
    }

    private static async Task<HttpResponseMessage> CreateTemplateAsync(HttpClient client, int lineNumber, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/integrations/whatsapp/official/{lineNumber}/templates")
        {
            Content = JsonContent.Create(new
            {
                name = "atualizacao_atendimento",
                language = "pt_BR",
                category = "UTILITY",
                bodyText = "Olá, {{1}}.",
                bodyExamples = new[] { "Maria" }
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantUserAsync(MembershipRole role)
    {
        await using var context = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await context.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Templates {suffix}", $"templates-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"templates-{suffix}@test.example", "Template User");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("Templates@123"));
        var membership = TenantMembership.Create(tenant.Id, user, role);
        membership.Activate();
        context.AddRange(tenant, user, membership);
        await context.SaveChangesAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Templates@123" });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }

    private async Task AddOfficialLineAsync(Guid tenantId, int lineNumber, string wabaId)
    {
        await using var context = await factory.GetDbContextAsync();
        context.WhatsAppAccounts.Add(WhatsAppAccount.Create(tenantId, wabaId, $"phone-{Guid.NewGuid():N}", "secret-ref",
            WhatsAppConnectionType.OfficialApi, lineNumber));
        await context.SaveChangesAsync();
    }
}
