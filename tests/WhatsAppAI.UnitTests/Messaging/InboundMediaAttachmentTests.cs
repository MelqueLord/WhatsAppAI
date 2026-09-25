using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.UnitTests.Messaging;

public sealed class InboundMediaAttachmentTests
{
    [Fact]
    public void Create_RequiresTenantBoundPngOrJpegWithinLimit()
    {
        var tenantId = Guid.NewGuid();
        var attachment = InboundMediaAttachment.Create(tenantId, "qr-message-1", "image/png", 42, "encrypted");

        Assert.Equal(tenantId, attachment.TenantId);
        Assert.Equal("qr-message-1", attachment.ExternalMessageId);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Throws<ArgumentException>(() =>
            InboundMediaAttachment.Create(tenantId, "qr-message-2", "image/gif", 42, "encrypted"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InboundMediaAttachment.Create(tenantId, "qr-message-3", "image/jpeg", 0, "encrypted"));
    }
}
