using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.IntegrationTests.Bot;

[Collection("IntegrationTests")]
public sealed class BotConfigurationAuthorizationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Operator_CannotMutateBotConfiguration()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync(p => p.Code == "BOT");
        var tenant = Tenant.Create($"BOT auth {suffix}", $"bot-auth-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"operator-{suffix}@test.example", "BOT Operator");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("Operator@123"));
        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.Operator);
        membership.Activate();
        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "Operator@123"
        });
        login.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/bot-config/toggle", new { enabled = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
