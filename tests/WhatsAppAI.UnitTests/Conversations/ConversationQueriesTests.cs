using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Conversations;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.UnitTests.Conversations;

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
        var assignedConversation = Conversation.Create(tenantId, firstContact.Id, "phone-1");
        var otherConversation = Conversation.Create(tenantId, secondContact.Id, "phone-1");
        assignedConversation.AssignQueue(assignedQueueId);
        otherConversation.AssignQueue(otherQueueId);
        context.AddRange(firstContact, secondContact, assignedConversation, otherConversation);
        await context.SaveChangesAsync();

        var queries = new ConversationQueries(context);
        var result = await queries.GetConversationsAsync(
            tenantId,
            new CursorPaginationRequest { Limit = 50 },
            queueId: assignedQueueId);
        var generalResult = await queries.GetConversationsAsync(
            tenantId,
            new CursorPaginationRequest { Limit = 50 });

        Assert.Collection(result.Items, item => Assert.Equal(assignedConversation.Id, item.Id));
        Assert.Equal(2, generalResult.Items.Count);
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
