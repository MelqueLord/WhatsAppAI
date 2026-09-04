using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Messaging;

[Collection("IntegrationTests")]
public sealed class ServiceQueueTransferNoticeEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task TransferNotice_IsPersistedReturnedAndTenantScoped()
    {
        var first = await CreateTenantOwnerAsync();
        var second = await CreateTenantOwnerAsync();

        var create = await first.Client.PostAsJsonAsync("/api/service-queues", new
        {
            name = "Suporte",
            description = "Dúvidas técnicas",
            color = "#4F46E5",
            sortOrder = 0,
            keywords = "suporte, técnico",
            transferNotice = "Você será atendido pela nossa equipe de suporte."
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var queueId = created.GetProperty("id").GetGuid();

        var firstList = await first.Client.GetFromJsonAsync<JsonElement[]>("/api/service-queues");
        var crossTenantUpdate = await second.Client.PutAsJsonAsync($"/api/service-queues/{queueId}", new
        {
            name = "Alteração indevida",
            description = "",
            color = "#4F46E5",
            sortOrder = 0,
            keywords = "",
            transferNotice = "Não deve ser gravada."
        });
        var secondList = await second.Client.GetFromJsonAsync<JsonElement[]>("/api/service-queues");

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.NotNull(firstList);
        Assert.Single(firstList!);
        Assert.Equal("Você será atendido pela nossa equipe de suporte.",
            firstList[0].GetProperty("transferNotice").GetString());
        Assert.Equal(HttpStatusCode.NotFound, crossTenantUpdate.StatusCode);
        Assert.NotNull(secondList);
        Assert.Empty(secondList!);
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantOwnerAsync()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Queue notice {suffix}", $"queue-notice-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"queue-notice-{suffix}@test.example", "Queue Notice Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("QueueNotice@123"));
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
            password = "QueueNotice@123"
        });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }
}
