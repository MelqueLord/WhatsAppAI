using WhatsAppAI.Domain.Broadcast;
using Xunit;

namespace WhatsAppAI.UnitTests.Broadcast;

public sealed class BroadcastRecipientTests
{
    [Fact]
    public void MarkQueued_AssociatesAnOutboundMessage()
    {
        var recipient = BroadcastRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var messageId = Guid.NewGuid();

        recipient.MarkQueued(messageId);

        Assert.Equal(BroadcastRecipientStatus.Queued, recipient.Status);
        Assert.Equal(messageId, recipient.OutboundMessageId);
    }
    [Fact]
    public void Retry_RejectsRecipientAlreadySent()
    {
        var recipient = BroadcastRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        recipient.MarkSent();

        Assert.Throws<InvalidOperationException>(() => recipient.Retry());
        Assert.Equal(BroadcastRecipientStatus.Sent, recipient.Status);
    }

    [Fact]
    public void Retry_ClearsPreviousFailureAndReturnsToPending()
    {
        var recipient = BroadcastRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        recipient.MarkFailed("Temporary error");

        recipient.Retry();

        Assert.Equal(BroadcastRecipientStatus.Pending, recipient.Status);
        Assert.Null(recipient.ErrorMessage);
        Assert.Null(recipient.SentAt);
    }

    [Fact]
    public void Retry_ClearsQueuedMessageAndIncrementsDispatchAttempt()
    {
        var recipient = BroadcastRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        recipient.MarkQueued(Guid.NewGuid());
        recipient.MarkFailed("Provider failure");

        recipient.Retry();

        Assert.Equal(BroadcastRecipientStatus.Pending, recipient.Status);
        Assert.Null(recipient.OutboundMessageId);
        Assert.Null(recipient.QueuedAt);
        Assert.Equal(2, recipient.DispatchAttempt);
    }
}
