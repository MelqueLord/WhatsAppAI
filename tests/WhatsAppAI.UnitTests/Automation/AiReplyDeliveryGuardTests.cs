using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiReplyDeliveryGuardTests
{
    [Fact]
    public void CanSend_AllowsUnchangedAutomaticConversationWithOpenWindow()
    {
        var conversation = CreateConversationWithOpenWindow();

        Assert.True(AiReplyDeliveryGuard.CanSend(
            conversation, conversation.Version, DateTime.UtcNow));
    }

    [Fact]
    public void CanSend_BlocksChangedVersionEvenWhenModeReturnedToAutomatic()
    {
        var conversation = CreateConversationWithOpenWindow();
        var expectedVersion = conversation.Version;
        conversation.SwitchMode(ConversationMode.Human, conversation.Version);
        conversation.SwitchMode(ConversationMode.Automatic, conversation.Version);

        Assert.False(AiReplyDeliveryGuard.CanSend(
            conversation, expectedVersion, DateTime.UtcNow));
    }

    [Fact]
    public void CanSend_BlocksConcurrentHumanHandoff()
    {
        var conversation = CreateConversationWithOpenWindow();
        var expectedVersion = conversation.Version;
        conversation.SwitchMode(ConversationMode.Human, expectedVersion);

        Assert.False(AiReplyDeliveryGuard.CanSend(
            conversation, expectedVersion, DateTime.UtcNow));
    }

    [Fact]
    public void CanSend_BlocksClosedWindow()
    {
        var conversation = Conversation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "phone-number-id");

        Assert.False(AiReplyDeliveryGuard.CanSend(
            conversation, conversation.Version, DateTime.UtcNow));
    }

    [Fact]
    public void VersionedIdempotencyKey_RoundTripsExpectedVersion()
    {
        var key = AiReplyDeliveryGuard.CreateIdempotencyKey(Guid.NewGuid(), 42);

        Assert.True(AiReplyDeliveryGuard.IsAiReply(key));
        Assert.True(AiReplyDeliveryGuard.TryGetExpectedVersion(key, out var version));
        Assert.Equal(42U, version);
    }

    [Fact]
    public void IsAiReply_DoesNotMatchHandoffFallback()
    {
        Assert.False(AiReplyDeliveryGuard.IsAiReply(
            $"ai-unavailable:{Guid.NewGuid()}"));
    }

    private static Conversation CreateConversationWithOpenWindow()
    {
        var conversation = Conversation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "phone-number-id");
        conversation.RenewWindow();
        return conversation;
    }
}
