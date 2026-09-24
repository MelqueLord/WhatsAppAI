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
  public async Task QueueContacts_IncludeOpenConversationsWithoutImport_AndRemainTenantScoped()
    {
        var owner = await CreateTenantOwnerAsync();
        var otherOwner = await CreateTenantOwnerAsync();
        var salesQueue = ServiceLine.Create(owner.TenantId, "Vendas");
        var supportQueue = ServiceLine.Create(owner.TenantId, "Suporte");
        var imported = Contact.Create(owner.TenantId, "5511999999801", "Importado", salesQueue.Id);
        var inSales = Contact.Create(owner.TenantId, "5511999999802", "Na conversa de vendas");
        var inSupport = Contact.Create(owner.TenantId, "5511999999803", "Na conversa de suporte");
        var outside = Contact.Create(owner.TenantId, "5511999999804", "Sem fila");
        var otherTenant = Contact.Create(otherOwner.TenantId, "5511999999805", "Outra empresa");
        var importedConversation = Conversation.Create(owner.TenantId, imported.Id, "manual");
        importedConversation.AssignQueue(salesQueue.Id);
        var salesConversation = Conversation.Create(owner.TenantId, inSales.Id, "manual");
        salesConversation.AssignQueue(salesQueue.Id);
        var supportConversation = Conversation.Create(owner.TenantId, inSupport.Id, "manual");
        supportConversation.AssignQueue(supportQueue.Id);
        var otherTenantConversation = Conversation.Create(otherOwner.TenantId, otherTenant.Id, "manual");
        otherTenantConversation.AssignQueue(salesQueue.Id);

        await using (var db = await factory.GetDbContextAsync())
        {
            db.ServiceLines.AddRange(salesQueue, supportQueue);
            db.Contacts.AddRange(imported, inSales, inSupport, outside, otherTenant);
            db.Conversations.AddRange(importedConversation, salesConversation, supportConversation, otherTenantConversation);
            await db.SaveChangesAsync();
        }

        var salesContacts = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/contacts?queueId={salesQueue.Id}&limit=500");
        Assert.NotNull(salesContacts);
        Assert.Equal(new[] { imported.Id, inSales.Id }.Order(),
            salesContacts.Select(item => item.GetProperty("id").GetGuid()).Order().ToArray());

        var supportContacts = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/contacts?queueId={supportQueue.Id}&limit=500");
        Assert.NotNull(supportContacts);
        Assert.Equal(inSupport.Id, Assert.Single(supportContacts).GetProperty("id").GetGuid());

        var allSales = await owner.Client.PostAsJsonAsync("/api/broadcasts", new
        {
            name = "Aviso para vendas",
            message = "Mensagem",
            queueId = salesQueue.Id,
            contactIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.Created, allSales.StatusCode);
        var created = await allSales.Content.ReadFromJsonAsync<JsonElement>();
        var broadcastId = created.GetProperty("id").GetGuid();
        await using (var db = await factory.GetDbContextAsync())
        {
            var recipientIds = await db.BroadcastRecipients.IgnoreQueryFilters()
                .Where(recipient => recipient.TenantId == owner.TenantId && recipient.BroadcastListId == broadcastId)
                .Select(recipient => recipient.ContactId)
                .ToListAsync();
            Assert.Equal(new[] { imported.Id, inSales.Id }.Order(), recipientIds.Order());
        }

        var selectedSales = await owner.Client.PostAsJsonAsync("/api/broadcasts", new
        {
            name = "Contato sem importação",
            message = "Mensagem",
            queueId = salesQueue.Id,
            contactIds = new[] { inSales.Id }
        });
        Assert.Equal(HttpStatusCode.Created, selectedSales.StatusCode);

        var wrongQueue = await owner.Client.PostAsJsonAsync("/api/broadcasts", new
        {
            name = "Fila errada",
            message = "Mensagem",
            queueId = salesQueue.Id,
            contactIds = new[] { inSupport.Id }
        });
        var wrongTenant = await owner.Client.PostAsJsonAsync("/api/broadcasts", new
        {
            name = "Outra empresa",
            message = "Mensagem",
            queueId = salesQueue.Id,
            contactIds = new[] { otherTenant.Id }
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongQueue.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongTenant.StatusCode);

        await using (var db = await factory.GetDbContextAsync())
        {
            var conversation = await db.Conversations.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == salesConversation.Id);
            conversation.Close();
            await db.SaveChangesAsync();
        }

        var contactsAfterClose = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/contacts?queueId={salesQueue.Id}&limit=500");
        Assert.NotNull(contactsAfterClose);
        Assert.Equal(imported.Id, Assert.Single(contactsAfterClose).GetProperty("id").GetGuid());
    }

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
