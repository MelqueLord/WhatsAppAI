using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.IntegrationTests.Automation;

[Collection("IntegrationTests")]
public sealed class AiMessageClaimTests(TestWebApplicationFactory factory)
{
    [Fact]
    public async Task TryClaimInboundForAi_AllowsOnlyOneConcurrentWorker()
    {
        var db = await factory.GetDbContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var plan = await db.SubscriptionPlans.FirstAsync();

        var tenant = Tenant.Create($"Claim Tenant {suffix}", $"claim-tenant-{suffix}", plan.Id);
        var contact = Contact.Create(tenant.Id, $"55119999{suffix}");
        var conversation = Conversation.Create(tenant.Id, contact.Id, "manual");
        var inbound = Message.CreateInbound(
            tenant.Id, conversation.Id, contact.Id, $"claim-{suffix}", MessageType.Text, "Olá");
        db.Tenants.Add(tenant);
        db.Contacts.Add(contact);
        db.Conversations.Add(conversation);
        db.Messages.Add(inbound);
        await db.SaveChangesAsync();

        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var repo1 = scope1.ServiceProvider.GetRequiredService<IMessageRepository>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IMessageRepository>();
        var leaseUntil = DateTime.UtcNow.AddMinutes(5);

        var results = await Task.WhenAll(
            repo1.TryClaimInboundForAiAsync(tenant.Id, inbound.Id, leaseUntil),
            repo2.TryClaimInboundForAiAsync(tenant.Id, inbound.Id, leaseUntil));

        Assert.Single(results, result => result);
    }
}
