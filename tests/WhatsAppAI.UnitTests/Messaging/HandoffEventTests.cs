using WhatsAppAI.Domain.Messaging;
using Xunit;

namespace WhatsAppAI.UnitTests.Messaging;

public class HandoffEventTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var operatorUserId = Guid.NewGuid();

        var handoff = HandoffEvent.Create(
            tenantId, conversationId,
            ConversationMode.Automatic, ConversationMode.Human,
            operatorUserId, "Low confidence");

        Assert.Equal(tenantId, handoff.TenantId);
        Assert.Equal(conversationId, handoff.ConversationId);
        Assert.Equal(ConversationMode.Automatic, handoff.FromMode);
        Assert.Equal(ConversationMode.Human, handoff.ToMode);
        Assert.Equal(operatorUserId, handoff.OperatorUserId);
        Assert.Equal("Low confidence", handoff.Reason);
    }

    [Fact]
    public void Create_AllowsNullOperatorUserId()
    {
        var handoff = HandoffEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            ConversationMode.Automatic, ConversationMode.Human,
            null, "System handoff");

        Assert.Null(handoff.OperatorUserId);
    }

    [Fact]
    public void Create_SetsOccurredAt()
    {
        var before = DateTime.UtcNow;
        var handoff = HandoffEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            ConversationMode.Automatic, ConversationMode.Human,
            null, "Test");

        Assert.True(handoff.OccurredAt >= before);
        Assert.True(handoff.OccurredAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_SetsModeTransition()
    {
        var handoff = HandoffEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            ConversationMode.Human, ConversationMode.Automatic,
            Guid.NewGuid(), "Operator released");

        Assert.Equal(ConversationMode.Human, handoff.FromMode);
        Assert.Equal(ConversationMode.Automatic, handoff.ToMode);
    }
}
