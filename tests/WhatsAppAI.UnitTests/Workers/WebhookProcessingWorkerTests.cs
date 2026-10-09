using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Meta.Models;
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
