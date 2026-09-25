using WhatsAppAI.Domain.Messaging;
using Xunit;

namespace WhatsAppAI.UnitTests.Messaging;

public class OutboundMediaAttachmentTests
{
    [Fact]
    public void Create_StoresTenantScopedEncryptedImageUntilPurged()
    {
        var attachment = OutboundMediaAttachment.Create(Guid.NewGuid(), Guid.NewGuid(), "image/png", 12, "ciphertext");

        Assert.Equal("image/png", attachment.ContentType);
        Assert.Null(attachment.PurgedAt);
        attachment.Purge();
        Assert.Empty(attachment.EncryptedContent);
        Assert.NotNull(attachment.PurgedAt);
    }

    [Theory]
    [InlineData("image/gif", 10)]
    [InlineData("image/jpeg", 0)]
    [InlineData("image/png", 5242881)]
    public void Create_RejectsUnsupportedOrOversizedImage(string contentType, long length) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            OutboundMediaAttachment.Create(Guid.NewGuid(), Guid.NewGuid(), contentType, length, "ciphertext"));
}
