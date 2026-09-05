using WhatsAppAI.Domain.Automation;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public class AiInteractionTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var interaction = AiInteraction.Create(
            tenantId, conversationId, messageId,
            "gpt-4o-mini", "Reply", null, 0.95,
            100, 50, 250, "resp-123");

        Assert.Equal(tenantId, interaction.TenantId);
        Assert.Equal(conversationId, interaction.ConversationId);
        Assert.Equal(messageId, interaction.MessageId);
        Assert.Equal("gpt-4o-mini", interaction.ModelId);
        Assert.Equal("Reply", interaction.Decision);
        Assert.Null(interaction.HandoffReason);
        Assert.Equal(0.95, interaction.Confidence);
        Assert.Equal(100, interaction.InputTokens);
        Assert.Equal(50, interaction.OutputTokens);
        Assert.Equal(250, interaction.LatencyMs);
        Assert.Equal("resp-123", interaction.ResponseId);
    }

    [Fact]
    public void Create_SetsHandoffReason()
    {
        var interaction = AiInteraction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "gpt-4o-mini", "Handoff", "Low confidence", 0.3,
            100, 50, 250, null);

        Assert.Equal("Handoff", interaction.Decision);
        Assert.Equal("Low confidence", interaction.HandoffReason);
    }

    [Fact]
    public void Create_AllowsNullResponseId()
    {
        var interaction = AiInteraction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "gpt-4o-mini", "Reply", null, 0.95,
            100, 50, 250, null);

        Assert.Null(interaction.ResponseId);
    }

    [Fact]
    public void Create_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var interaction = AiInteraction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "gpt-4o-mini", "Reply", null, 0.95,
            100, 50, 250, null);

        Assert.True(interaction.CreatedAt >= before);
        Assert.True(interaction.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_SetsTokenCounts()
    {
        var interaction = AiInteraction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "gpt-4o-mini", "Reply", null, 0.95,
            1500, 800, 1200, null);

        Assert.Equal(1500, interaction.InputTokens);
        Assert.Equal(800, interaction.OutputTokens);
    }

    [Fact]
    public void Create_SetsLatency()
    {
        var interaction = AiInteraction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "gpt-4o-mini", "Reply", null, 0.95,
            100, 50, 3500, null);

        Assert.Equal(3500, interaction.LatencyMs);
    }

    [Fact]
    public void RecordFeedback_CannotBeRecordedTwice()
    {
        var interaction = AiInteraction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "gpt-4o-mini", "Reply", null, 0.95, 100, 50, 250, null);

        interaction.RecordFeedback(AiFeedbackRating.Helpful, null, null, Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            interaction.RecordFeedback(AiFeedbackRating.Helpful, null, null, Guid.NewGuid()));
    }
}
