using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Conversations;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.UnitTests.Conversations;

[Collection("Persistence")]
public sealed class ConversationQueriesTests
{
    [Fact]
    public async Task GetConversations_FiltersByAssignedQueue()
    {
        var tenantId = Guid.NewGuid();
        var assignedQueueId = Guid.NewGuid();
        var otherQueueId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new AppDbContext(options, new TenantContext(tenantId));
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var firstContact = Contact.Create(tenantId, "5511999990001", "First");
        var secondContact = Contact.Create(tenantId, "5511999990002", "Second");
        var closedContact = Contact.Create(tenantId, "5511999990003", "Closed");
        var assignedConversation = Conversation.Create(tenantId, firstContact.Id, "phone-1");
        var otherConversation = Conversation.Create(tenantId, secondContact.Id, "phone-1");
        var closedConversation = Conversation.Create(tenantId, closedContact.Id, "phone-1");
        closedConversation.Close();
        assignedConversation.AssignQueue(assignedQueueId);
        otherConversation.AssignQueue(otherQueueId);
        context.AddRange(firstContact, secondContact, closedContact, assignedConversation, otherConversation, closedConversation);
        await context.SaveChangesAsync();
        Assert.Equal(3, await context.Conversations.CountAsync());
        Assert.Equal(assignedQueueId, await context.Conversations
            .Where(conversation => conversation.Id == assignedConversation.Id)
            .Select(conversation => conversation.QueueId)
            .SingleAsync());

        var queries = new ConversationQueries(context);
        var result = await queries.GetConversationsAsync(
            tenantId,
            new CursorPaginationRequest { Limit = 50 },
            queueId: assignedQueueId);
        var generalResult = await queries.GetConversationsAsync(
            tenantId,
            new CursorPaginationRequest { Limit = 50 });
        var closedResult = await queries.GetConversationsAsync(
            tenantId,
            new CursorPaginationRequest { Limit = 50 },
            status: ConversationStatus.Closed);

        Assert.Collection(result.Items, item => Assert.Equal(assignedConversation.Id, item.Id));
        Assert.Equal(2, generalResult.Items.Count);
        Assert.Single(closedResult.Items);
        Assert.Equal(closedConversation.Id, closedResult.Items[0].Id);
    }

    private sealed class TenantContext(Guid tenantId) : ICurrentTenant
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => Guid.NewGuid();
        public string? UserRole => "Operator";
        public bool IsPlatformAdmin => false;
        public bool IsAuthenticated => true;
        public SupportSessionInfo? SupportSession => null;
        public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin) { }
        public void EnterSupportSession(Guid tenantId, string reason) { }
        public void ExitSupportSession() { }
        public void Clear() { }
    }
}
