using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Messaging;

[Collection("IntegrationTests")]
public sealed class ConversationLifecycleEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task SendImage_DoesNotAllowAnotherTenantConversation()
    {
        var requestingTenant = await CreateTenantOwnerAsync();
        var otherTenant = await CreateTenantOwnerAsync();
        var contact = Contact.Create(otherTenant.TenantId, "5511999999910", "Other Tenant");
        var conversation = Conversation.Create(otherTenant.TenantId, contact.Id, "official-phone-other");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.Add(contact);
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        using var form = new MultipartFormDataContent();
        using var image = new ByteArrayContent([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(image, "file", "tenant-isolation.png");

        var response = await requestingTenant.Client.PostAsync(
            $"/api/conversations/{conversation.Id}/media", form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendImage_AcceptsMultipartRequestForOwnOpenConversation()
    {
        var setup = await CreateTenantOwnerAsync();
        var contact = Contact.Create(setup.TenantId, "5511999999911", "Image Recipient");
        var conversation = Conversation.Create(setup.TenantId, contact.Id, "manual");
        conversation.RenewWindow();

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.Add(contact);
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        using var form = new MultipartFormDataContent();
        using var image = new ByteArrayContent([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(image, "file", "own-conversation.png");

        var response = await setup.Client.PostAsync(
            $"/api/conversations/{conversation.Id}/media", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TemplateAvailability_FollowsTheConversationOfficialLine()
    {
        var setup = await CreateTenantOwnerAsync();
        var manualContact = Contact.Create(setup.TenantId, "5511999999901", "Legado QR");
        var officialContact = Contact.Create(setup.TenantId, "5511999999902", "Oficial");
        var manualConversation = Conversation.Create(setup.TenantId, manualContact.Id, "manual");
        var officialConversation = Conversation.Create(setup.TenantId, officialContact.Id, "official-phone-1");
        var officialAccount = WhatsAppAccount.Create(setup.TenantId, "waba-1", "official-phone-1", "secret-ref");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.Contacts.AddRange(manualContact, officialContact);
            db.Conversations.AddRange(manualConversation, officialConversation);
            db.WhatsAppAccounts.Add(officialAccount);
            await db.SaveChangesAsync();
        }

        var list = await setup.Client.GetFromJsonAsync<JsonElement>("/api/conversations");
        var items = list.GetProperty("items").EnumerateArray().ToList();
        var manualItem = items.Single(item => item.GetProperty("id").GetGuid() == manualConversation.Id);
        var officialItem = items.Single(item => item.GetProperty("id").GetGuid() == officialConversation.Id);
        Assert.False(manualItem.GetProperty("canUseTemplates").GetBoolean());
        Assert.True(officialItem.GetProperty("canUseTemplates").GetBoolean());

        var manualDetail = await setup.Client.GetFromJsonAsync<JsonElement>($"/api/conversations/{manualConversation.Id}");
        Assert.False(manualDetail.GetProperty("canUseTemplates").GetBoolean());

        await using (var db = await factory.GetDbContextAsync())
        {
            var account = await db.WhatsAppAccounts.SingleAsync(item => item.Id == officialAccount.Id);
            account.Deactivate();
            await db.SaveChangesAsync();
        }

        var inactiveDetail = await setup.Client.GetFromJsonAsync<JsonElement>($"/api/conversations/{officialConversation.Id}");
        Assert.False(inactiveDetail.GetProperty("canUseTemplates").GetBoolean());
    }

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
