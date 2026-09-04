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

    [Theory]
    [InlineData("simple-auto-reply")]
    [InlineData("ai-unavailable")]
    [InlineData("ai-handoff")]
    [InlineData("ai-quota")]
    [InlineData("consent-request")]
    [InlineData("consent-confirmation")]
    public void AutomatedNotice_RequiresVersionedKey(string kind)
    {
        var id = Guid.NewGuid();
        var key = AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(kind, id, 7);

        Assert.True(AiReplyDeliveryGuard.IsAutomated(key));
        Assert.True(AiReplyDeliveryGuard.TryGetExpectedVersion(key, out var version));
        Assert.Equal(7U, version);
        Assert.True(AiReplyDeliveryGuard.IsAutomated($"{kind}:{id}"));
        Assert.False(AiReplyDeliveryGuard.TryGetExpectedVersion($"{kind}:{id}", out _));
    }

    [Fact]
    public void CanSendAutomatedNotice_AllowsIntentionalHumanHandoffWithinWindow()
    {
        var conversation = CreateConversationWithOpenWindow();
        conversation.SwitchMode(ConversationMode.Human, conversation.Version);

        Assert.True(AiReplyDeliveryGuard.CanSendAutomatedNotice(
            conversation, conversation.Version, DateTime.UtcNow));
    }

    private static Conversation CreateConversationWithOpenWindow()
    {
        var conversation = Conversation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "phone-number-id");
        conversation.RenewWindow();
        return conversation;
    }
}
