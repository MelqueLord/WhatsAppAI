using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.IntegrationTests.Automation;

[Collection("IntegrationTests")]
public sealed class AiResponseExampleEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Examples_ArePersistedAndTenantScoped()
    {
        var first = await CreateTenantOwnerAsync();
        var second = await CreateTenantOwnerAsync();
        var create = await first.PostAsJsonAsync("/api/ai-response-examples", new
        {
            customerMessage = "Quero agendar uma consulta",
            idealResponse = "Claro! Vou ajudar com o agendamento."
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var exampleId = created.GetProperty("id").GetGuid();

        var firstList = await first.GetFromJsonAsync<JsonElement[]>("/api/ai-response-examples");
        using var crossTenantUpdate = new HttpRequestMessage(HttpMethod.Put, $"/api/ai-response-examples/{exampleId}")
        {
            Content = JsonContent.Create(new { customerMessage = "Outra", idealResponse = "Outra resposta" })
        };
        crossTenantUpdate.Headers.TryAddWithoutValidation("If-Match", "1");
        var crossTenantResponse = await second.SendAsync(crossTenantUpdate);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(firstList);
        Assert.Single(firstList!);
        Assert.Equal("Quero agendar uma consulta", firstList[0].GetProperty("customerMessage").GetString());
        Assert.Equal("Manual", firstList[0].GetProperty("source").GetString());
        Assert.False(firstList[0].GetProperty("learnedFromOperator").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, crossTenantResponse.StatusCode);
    }

    private async Task<HttpClient> CreateTenantOwnerAsync()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Examples {suffix}", $"examples-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"examples-{suffix}@test.example", "Examples Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("Examples@123"));
        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();
        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Examples@123" });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
