using WhatsAppAI.Domain.Messaging;
using Xunit;

namespace WhatsAppAI.UnitTests.Messaging;

public class MessageTests
{
    [Fact]
    public void CreateInbound_SetsDirectionInbound()
    {
        var message = Message.CreateInbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "ext123", MessageType.Text, "Hello");

        Assert.Equal(MessageDirection.Inbound, message.Direction);
        Assert.Equal(MessageStatus.Received, message.Status);
    }

    [Fact]
    public void CreateOutbound_SetsDirectionOutbound()
    {
        var message = Message.CreateOutbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MessageType.Text, "Reply", "idem123");

        Assert.Equal(MessageDirection.Outbound, message.Direction);
        Assert.Equal(MessageStatus.Queued, message.Status);
    }

    [Fact]
    public void CreateOutboundTemplate_SetsTemplateMetadata()
    {
        var message = Message.CreateOutboundTemplate(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "welcome_customer", "pt_BR", "[\"Maria\"]", "idem-template");

        Assert.Equal(MessageType.Template, message.Type);
        Assert.Equal("welcome_customer", message.TemplateName);
        Assert.Equal("pt_BR", message.TemplateLanguage);
        Assert.Equal("[\"Maria\"]", message.TemplateParametersJson);
    }

    [Fact]
    public void RedactPersonalData_ClearsTemplateMetadata()
    {
        var message = Message.CreateOutboundTemplate(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "welcome_customer", "pt_BR", "[\"Maria\"]", "idem-template");

        message.RedactPersonalData();

        Assert.Null(message.TemplateName);
        Assert.Null(message.TemplateLanguage);
        Assert.Null(message.TemplateParametersJson);
    }

    [Fact]
    public void CreateInbound_SetsContent()
    {
        var message = Message.CreateInbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "ext123", MessageType.Text, "Hello World");

        Assert.Equal("Hello World", message.Content);
    }

    [Fact]
    public void CreateInbound_SetsMediaId()
    {
        var message = Message.CreateInbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "ext123", MessageType.Image, null, "media123");

        Assert.Equal("media123", message.MediaId);
    }

    [Fact]
    public void CreateOutbound_SetsIdempotencyKey()
    {
        var message = Message.CreateOutbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MessageType.Text, "Reply", "idem-key-123");

        Assert.Equal("idem-key-123", message.IdempotencyKey);
    }

    [Fact]
    public void MarkSent_SetsExternalIdAndStatus()
    {
        var message = Message.CreateOutbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MessageType.Text, "Reply", "idem123");
        message.MarkSent("wa-msg-123");

        Assert.Equal("wa-msg-123", message.ExternalId);
        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.NotNull(message.SentAt);
    }

    [Fact]
    public void MarkDelivered_SetsStatus()
    {
        var message = Message.CreateOutbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MessageType.Text, "Reply", "idem123");
        message.MarkSent("wa-msg-123");
        message.MarkDelivered();

        Assert.Equal(MessageStatus.Delivered, message.Status);
        Assert.NotNull(message.DeliveredAt);
    }

    [Fact]
    public void MarkRead_SetsStatus()
    {
        var message = Message.CreateOutbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MessageType.Text, "Reply", "idem123");
        message.MarkSent("wa-msg-123");
        message.MarkRead();

        Assert.Equal(MessageStatus.Read, message.Status);
        Assert.NotNull(message.ReadAt);
    }

    [Fact]
    public void MarkFailed_SetsStatusAndReason()
    {
        var message = Message.CreateOutbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            MessageType.Text, "Reply", "idem123");
        message.MarkFailed("Rate limit exceeded");

        Assert.Equal(MessageStatus.Failed, message.Status);
        Assert.Equal("Rate limit exceeded", message.FailureReason);
        Assert.NotNull(message.FailedAt);
    }

    [Fact]
    public void MarkProcessedByAi_SetsFlag()
    {
        var message = Message.CreateInbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "ext123", MessageType.Text, "Hello");
        message.MarkProcessedByAi();

        Assert.True(message.ProcessedByAi);
    }

    [Fact]
    public void RegisterAiFailure_SchedulesExponentialRetriesUntilLimit()
    {
        var message = Message.CreateInbound(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "ext123", MessageType.Text, "Hello");

        Assert.True(message.RegisterAiFailure(3, TimeSpan.FromSeconds(10)));
        Assert.Equal(1, message.AiRetryCount);
        Assert.NotNull(message.NextAiRetryAt);

        Assert.True(message.RegisterAiFailure(3, TimeSpan.FromSeconds(20)));
        Assert.False(message.RegisterAiFailure(3, TimeSpan.FromSeconds(40)));
        Assert.Equal(3, message.AiRetryCount);
        Assert.Null(message.NextAiRetryAt);
    }
}
