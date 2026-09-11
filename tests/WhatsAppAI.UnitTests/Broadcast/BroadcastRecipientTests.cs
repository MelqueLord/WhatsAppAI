using WhatsAppAI.Domain.Broadcast;
using Xunit;

namespace WhatsAppAI.UnitTests.Broadcast;

public sealed class BroadcastRecipientTests
{
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
}
