using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Knowledge;

[Collection("IntegrationTests")]
public sealed class KnowledgeCategoryEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task KnowledgeCategories_ArePersistedReturnedAndTenantScoped()
    {
        var first = await CreateTenantOwnerAsync();
        var second = await CreateTenantOwnerAsync();

        var create = await first.Client.PostAsJsonAsync("/api/knowledge", new
        {
            title = "Valor da avaliação",
            content = "A avaliação inicial custa R$ 100.",
            category = "Pricing",
            priority = 100
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = created.GetProperty("id").GetGuid();

        var firstList = await first.Client.GetFromJsonAsync<JsonElement[]>("/api/knowledge");
        var crossTenantUpdate = await second.Client.PutAsJsonAsync($"/api/knowledge/{itemId}", new
        {
            title = "Outro valor",
            content = "Não deve alterar.",
            category = "Pricing",
            priority = 100
        });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.NotNull(firstList);
        Assert.Single(firstList!);
        Assert.Equal("Pricing", firstList[0].GetProperty("category").GetString());
        Assert.Equal(HttpStatusCode.NotFound, crossTenantUpdate.StatusCode);
    }

    [Fact]
    public async Task CreateKnowledge_RejectsUnsupportedCategory()
    {
        var owner = await CreateTenantOwnerAsync();

        var response = await owner.Client.PostAsJsonAsync("/api/knowledge", new
        {
            title = "Item inválido",
            content = "Conteúdo.",
            category = "Unsupported",
            priority = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantOwnerAsync()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Knowledge {suffix}", $"knowledge-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"knowledge-{suffix}@test.example", "Knowledge Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("Knowledge@123"));
        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "Knowledge@123"
        });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }
}
