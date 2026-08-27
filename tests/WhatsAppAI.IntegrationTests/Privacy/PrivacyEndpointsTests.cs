using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Privacy;

[Collection("IntegrationTests")]
public sealed class PrivacyEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Notice_WithoutInstitutionalConfiguration_IsAvailableAndIncomplete()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/privacy/notice");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("configurationComplete").GetBoolean());
    }

    [Fact]
    public async Task ConsentEvidence_IsTenantScoped_AndRequiresConsentPurpose()
    {
        var first = await CreateTenantOwnerAsync();
        var second = await CreateTenantOwnerAsync();
        var firstContact = await CreateContactAsync(first.TenantId, "+5511999990001", "First");

        var contractResponse = await first.Client.PostAsJsonAsync("/api/privacy/purposes", new
        {
            name = "Support",
            description = "Customer support",
            legalBasis = "Contract",
            retentionDays = 365
        });
        var contractPurpose = await ReadIdAsync(contractResponse);
        var invalidConsent = await first.Client.PostAsJsonAsync("/api/privacy/consents", new
        {
            contactId = firstContact,
            processingPurposeId = contractPurpose,
            source = "WhatsApp"
        });

        var consentResponse = await first.Client.PostAsJsonAsync("/api/privacy/purposes", new
        {
            name = "Marketing",
            description = "Optional marketing",
            legalBasis = "Consent",
            retentionDays = 180
        });
        var consentPurpose = await ReadIdAsync(consentResponse);
        var evidenceResponse = await first.Client.PostAsJsonAsync("/api/privacy/consents", new
        {
            contactId = firstContact,
            processingPurposeId = consentPurpose,
            source = "WhatsApp",
            evidenceReference = "message-reference"
        });
        var evidenceId = await ReadIdAsync(evidenceResponse);
        var crossTenantRevocation = await second.Client.PostAsync(
            $"/api/privacy/consents/{evidenceId}/revoke", null);

        Assert.Equal(HttpStatusCode.BadRequest, invalidConsent.StatusCode);
        Assert.Equal(HttpStatusCode.Created, evidenceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantRevocation.StatusCode);
    }

    [Fact]
    public async Task Erasure_IsIdempotent_AndDoesNotAffectAnotherTenant()
    {
        var first = await CreateTenantOwnerAsync();
        var second = await CreateTenantOwnerAsync();
        const string sharedPhone = "+5511999990002";
        var firstContact = await CreateContactWithMessageAsync(first.TenantId, sharedPhone, "First", "first personal text");
        var secondContact = await CreateContactWithMessageAsync(second.TenantId, sharedPhone, "Second", "second personal text");

        var requestResponse = await first.Client.PostAsJsonAsync("/api/privacy/requests", new
        {
            contactId = firstContact,
            type = "Erasure"
        });
        var requestId = await ReadIdAsync(requestResponse);
        var firstErase = await first.Client.PostAsync($"/api/privacy/requests/{requestId}/erase", null);
        var secondErase = await first.Client.PostAsync($"/api/privacy/requests/{requestId}/erase", null);

        await using var db = await factory.GetDbContextAsync();
        var erasedContact = await db.Contacts.IgnoreQueryFilters().SingleAsync(x => x.Id == firstContact);
        var retainedContact = await db.Contacts.IgnoreQueryFilters().SingleAsync(x => x.Id == secondContact);
        var erasedMessage = await db.Messages.IgnoreQueryFilters().SingleAsync(x => x.ContactId == firstContact);
        var retainedMessage = await db.Messages.IgnoreQueryFilters().SingleAsync(x => x.ContactId == secondContact);

        Assert.Equal(HttpStatusCode.OK, firstErase.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondErase.StatusCode);
        Assert.StartsWith("anon-", erasedContact.PhoneNumber);
        Assert.Null(erasedContact.Name);
        Assert.Null(erasedMessage.Content);
        Assert.Equal(sharedPhone, retainedContact.PhoneNumber);
        Assert.Equal("Second", retainedContact.Name);
        Assert.Equal("second personal text", retainedMessage.Content);
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantOwnerAsync()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Privacy {suffix}", $"privacy-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"privacy-{suffix}@test.example", "Privacy Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("Privacy@123"));
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
            password = "Privacy@123"
        });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }

    private async Task<Guid> CreateContactAsync(Guid tenantId, string phone, string name)
    {
        await using var db = await factory.GetDbContextAsync();
        var contact = Contact.Create(tenantId, phone, name);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact.Id;
    }

    private async Task<Guid> CreateContactWithMessageAsync(
        Guid tenantId,
        string phone,
        string name,
        string content)
    {
        await using var db = await factory.GetDbContextAsync();
        var contact = Contact.Create(tenantId, phone, name);
        var conversation = Conversation.Create(tenantId, contact.Id, $"line-{tenantId:N}");
        var message = Message.CreateInbound(
            tenantId,
            conversation.Id,
            contact.Id,
            Guid.NewGuid().ToString("N"),
            MessageType.Text,
            content);
        db.Contacts.Add(contact);
        db.Conversations.Add(conversation);
        db.Messages.Add(message);
        await db.SaveChangesAsync();
        return contact.Id;
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
