using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Conversations;
using WhatsAppAI.Infrastructure.Meta.Models;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Persistence.Repositories;
using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.UnitTests.Workers;

public sealed class WebhookProcessingWorkerTests
{
    [Fact]
    public async Task ProcessStatusUpdateAsync_PersistsDeliveryAndNotifiesInbox()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var message = Message.CreateOutbound(
            tenantId, conversationId, Guid.NewGuid(), MessageType.Text, "Hello", "idempotency-key");
        message.MarkSent("wamid.123");
        var repository = new MessageRepositoryFake(message);
        var notifier = new RealtimeNotifierFake();
        using var services = new ServiceCollection().BuildServiceProvider();
        var worker = new WebhookProcessingWorker(services, NullLogger<WebhookProcessingWorker>.Instance);

        var processed = await worker.ProcessStatusUpdateAsync(
            tenantId,
            new WebhookStatus { Id = "wamid.123", Status = "delivered" },
            repository,
            notifier,
            CancellationToken.None);

        Assert.Equal(MessageStatus.Delivered, message.Status);
        Assert.True(processed);
        Assert.Same(message, repository.UpdatedMessage);
        Assert.Equal(tenantId, notifier.TenantId);
        Assert.Equal("MessageStatusUpdated", notifier.EventName);
        Assert.Equal(conversationId, notifier.Payload?.GetType().GetProperty("conversationId")?.GetValue(notifier.Payload));
    }

    [Fact]
    public async Task ProcessStatusUpdateAsync_ReturnsFalseWhenMessageHasNotBeenPersistedYet()
    {
        var tenantId = Guid.NewGuid();
        var message = Message.CreateOutbound(
            tenantId, Guid.NewGuid(), Guid.NewGuid(), MessageType.Template, "Hello", "idempotency-key");
        var repository = new MessageRepositoryFake(message);
        var notifier = new RealtimeNotifierFake();
        using var services = new ServiceCollection().BuildServiceProvider();
        var worker = new WebhookProcessingWorker(services, NullLogger<WebhookProcessingWorker>.Instance);

        var processed = await worker.ProcessStatusUpdateAsync(
            tenantId,
            new WebhookStatus { Id = "wamid.123", Status = "delivered" },
            repository,
            notifier,
            CancellationToken.None);

        Assert.False(processed);
        Assert.Null(repository.UpdatedMessage);
        Assert.Null(notifier.EventName);
    }

    [Fact]
    public async Task CustomerRevocation_MarksOnlyTheMatchingReceivedMessageAndKeepsItsText()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var context = new AppDbContext(options, new TenantContext(tenantId));
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var contact = Contact.Create(tenantId, "5511999990031", "Customer");
        var conversation = Conversation.Create(tenantId, contact.Id, $"qr:{tenantId:D}:1");
        var original = Message.CreateInbound(tenantId, conversation.Id, contact.Id,
            "original-id", MessageType.Text, "Conteúdo preservado");
        context.AddRange(contact, conversation, original);
        await context.SaveChangesAsync();

        using var services = new ServiceCollection().BuildServiceProvider();
        var worker = new WebhookProcessingWorker(services, NullLogger<WebhookProcessingWorker>.Instance);
        var notifier = new RealtimeNotifierFake();
        var revocation = new WebhookMessage
        {
            From = contact.PhoneNumber,
            Type = "revoked",
            Revoked = new WebhookRevocation { MessageId = "original-id" }
        };
        var messageRepository = new MessageRepository(context);
        var contactRepository = new ContactRepository(context);
        var conversationRepository = new ConversationRepository(context);

        var wrongLine = await worker.ProcessCustomerRevocationAsync(tenantId, $"qr:{tenantId:D}:2",
            revocation, contactRepository, conversationRepository, messageRepository, notifier, CancellationToken.None);
        var otherTenant = await worker.ProcessCustomerRevocationAsync(Guid.NewGuid(), conversation.PhoneNumberId,
            revocation, contactRepository, conversationRepository, messageRepository, notifier, CancellationToken.None);
        Assert.True(wrongLine);
        Assert.False(otherTenant);
        Assert.Null(original.CustomerDeletedAt);

        revocation.From = "5511999990032";
        var wrongContact = await worker.ProcessCustomerRevocationAsync(tenantId, conversation.PhoneNumberId,
            revocation, contactRepository, conversationRepository, messageRepository, notifier, CancellationToken.None);
        Assert.True(wrongContact);
        Assert.Null(original.CustomerDeletedAt);
        revocation.From = contact.PhoneNumber;

        var processed = await worker.ProcessCustomerRevocationAsync(tenantId, conversation.PhoneNumberId,
            revocation, contactRepository, conversationRepository, messageRepository, notifier, CancellationToken.None);
        var repeated = await worker.ProcessCustomerRevocationAsync(tenantId, conversation.PhoneNumberId,
            revocation, contactRepository, conversationRepository, messageRepository, notifier, CancellationToken.None);

        Assert.True(processed);
        Assert.True(repeated);
        Assert.NotNull(original.CustomerDeletedAt);
        Assert.Equal("Conteúdo preservado", original.Content);
        Assert.Null(original.HiddenFromInboxAt);
        Assert.Equal("MessageStatusUpdated", notifier.EventName);
        Assert.Single(await context.Messages.ToListAsync());

        var visible = await new ConversationQueries(context).GetMessagesAsync(tenantId, conversation.Id,
            new CursorPaginationRequest { Limit = 10 });
        Assert.True(visible.Items[0].IsDeletedByCustomer);
        Assert.Equal("Conteúdo preservado", visible.Items[0].Content);

    }

    [Fact]
    public async Task CustomerRevocation_RetriesWhenTheOriginalHasNotArrived()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var worker = new WebhookProcessingWorker(services, NullLogger<WebhookProcessingWorker>.Instance);
        var message = Message.CreateInbound(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "different-id", MessageType.Text, "Unrelated");

        var resolved = await worker.ProcessCustomerRevocationAsync(message.TenantId,
            "qr:unavailable:1",
            new WebhookMessage { From = "5511999990031", Revoked = new WebhookRevocation { MessageId = "missing-id" } },
            null!, null!, new MessageRepositoryFake(message), new RealtimeNotifierFake(), CancellationToken.None);

        Assert.False(resolved);
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

    private sealed class MessageRepositoryFake(Message message) : IMessageRepository
    {
        public Message? UpdatedMessage { get; private set; }

        public Task<Message?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Message?>(message.TenantId == tenantId && message.ExternalId == externalId ? message : null);

        public Task UpdateAsync(Message message, CancellationToken cancellationToken = default)
        {
            UpdatedMessage = message;
            return Task.CompletedTask;
        }

        public Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Message?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Message>> GetUnprocessedInboundAsync(int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryClaimInboundForAiAsync(Guid tenantId, Guid messageId, DateTime leaseUntil, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Message>> GetByConversationAsync(Guid conversationId, int limit = 50, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Message>> GetByTenantAsync(Guid tenantId, int limit = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Message message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RealtimeNotifierFake : IRealtimeNotifier
    {
        public Guid TenantId { get; private set; }
        public string? EventName { get; private set; }
        public object? Payload { get; private set; }

        public Task NotifyTenantAsync(Guid tenantId, string eventName, object payload, CancellationToken cancellationToken = default)
        {
            TenantId = tenantId;
            EventName = eventName;
            Payload = payload;
            return Task.CompletedTask;
        }
    }
}
