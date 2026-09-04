using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Messaging;

[Collection("IntegrationTests")]
public sealed class ConversationLifecycleEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task CloseMovesConversationToClosedFilterAndKeepsHistory()
    {
        var setup = await CreateTenantOwnerAsync();
        var contact = Contact.Create(setup.TenantId, "5511999999999", "Lifecycle Contact");
        var conversation = Conversation.Create(setup.TenantId, contact.Id, "manual");
        var inbound = Message.CreateInbound(
            setup.TenantId,
            conversation.Id,
            contact.Id,
            "lifecycle-inbound",
            MessageType.Text,
            "Olá, preciso de ajuda");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.Add(contact);
            db.Conversations.Add(conversation);
            db.Messages.Add(inbound);
            await db.SaveChangesAsync();
        }

        using var closeRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/conversations/{conversation.Id}/close");
        closeRequest.Headers.TryAddWithoutValidation("If-Match", "1");
        using var close = await setup.Client.SendAsync(closeRequest);

        var openList = await setup.Client.GetFromJsonAsync<JsonElement>(
            "/api/conversations?status=Open");
        var closedList = await setup.Client.GetFromJsonAsync<JsonElement>(
            "/api/conversations?status=Closed");
        var history = await setup.Client.GetFromJsonAsync<JsonElement>(
            $"/api/conversations/{conversation.Id}/messages");

        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Empty(openList.GetProperty("items").EnumerateArray());
        Assert.Single(closedList.GetProperty("items").EnumerateArray());
        Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal("Olá, preciso de ajuda",
            history.GetProperty("items")[0].GetProperty("content").GetString());
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantOwnerAsync()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Conversation lifecycle {suffix}", $"conversation-lifecycle-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"conversation-lifecycle-{suffix}@test.example", "Lifecycle Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("ConversationLifecycle@123"));
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
            password = "ConversationLifecycle@123"
        });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }
}
