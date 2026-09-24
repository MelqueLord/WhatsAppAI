using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.IntegrationTests.Messaging;

[Collection("IntegrationTests")]
public sealed class BroadcastQueueRecipientSelectionTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task SelectedQueueContacts_AreSnapshotted_AndOtherContactsAreRejected()
    {
        var owner = await CreateTenantOwnerAsync();
        var otherOwner = await CreateTenantOwnerAsync();
        var queue = ServiceLine.Create(owner.TenantId, "Vendas");
        var selected = Contact.Create(owner.TenantId, "5511999999999", "Selecionado", queue.Id);
        var otherQueueContact = Contact.Create(owner.TenantId, "5511888888888", "Outra fila");
        var otherTenantContact = Contact.Create(otherOwner.TenantId, "5511777777777", "Outro tenant");

        await using (var db = await factory.GetDbContextAsync())
        {
            db.ServiceLines.Add(queue);
            db.Contacts.AddRange(selected, otherQueueContact, otherTenantContact);
            await db.SaveChangesAsync();
        }

        var create = await owner.Client.PostAsJsonAsync("/api/broadcasts", new
        {
            name = "Aviso",
            message = "Mensagem",
            queueId = queue.Id,
            contactIds = new[] { selected.Id, selected.Id }
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var broadcastId = created.GetProperty("id").GetGuid();

        await using (var db = await factory.GetDbContextAsync())
        {
            var recipients = await db.BroadcastRecipients.IgnoreQueryFilters()
                .Where(recipient => recipient.BroadcastListId == broadcastId)
                .Select(recipient => recipient.ContactId)
                .ToListAsync();
            Assert.Equal(new[] { selected.Id }, recipients);
        }

        var wrongQueue = await owner.Client.PostAsJsonAsync("/api/broadcasts", new
        {
            name = "Aviso",
            message = "Mensagem",
            queueId = queue.Id,
            contactIds = new[] { otherQueueContact.Id }
        });
        var wrongTenant = await owner.Client.PostAsJsonAsync("/api/broadcasts", new
        {
            name = "Aviso",
            message = "Mensagem",
            queueId = queue.Id,
            contactIds = new[] { otherTenantContact.Id }
        });

        Assert.Equal(HttpStatusCode.BadRequest, wrongQueue.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongTenant.StatusCode);
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateTenantOwnerAsync()
    {
        await using var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var plan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstAsync();
        var tenant = Tenant.Create($"Broadcast {suffix}", $"broadcast-{suffix}", plan.Id);
        tenant.Activate();
        var user = User.Create($"broadcast-{suffix}@test.example", "Broadcast Owner");
        user.Activate(BCrypt.Net.BCrypt.HashPassword("Broadcast@123"));
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
            password = "Broadcast@123"
        });
        login.EnsureSuccessStatusCode();
        return (client, tenant.Id);
    }
}
