namespace WhatsAppAI.Domain.Messaging;

/// <summary>
/// Protected QR media retained by the platform for authenticated Inbox retrieval.
/// The QR bridge never writes this data to its session volume.
/// </summary>
public sealed class InboundMediaAttachment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ExternalMessageId { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Length { get; private set; }
    public string EncryptedContent { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private InboundMediaAttachment() { }

    public static InboundMediaAttachment Create(
        Guid tenantId, string externalMessageId, string contentType, long length, string encryptedContent) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalMessageId = !string.IsNullOrWhiteSpace(externalMessageId) && externalMessageId.Length <= 200
                ? externalMessageId : throw new ArgumentException("External message ID is required.", nameof(externalMessageId)),
            ContentType = contentType is "image/jpeg" or "image/png"
                ? contentType : throw new ArgumentException("Only JPEG and PNG images are supported.", nameof(contentType)),
            Length = length is > 0 and <= 5 * 1024 * 1024
                ? length : throw new ArgumentOutOfRangeException(nameof(length)),
            EncryptedContent = !string.IsNullOrWhiteSpace(encryptedContent)
                ? encryptedContent : throw new ArgumentException("Encrypted content is required.", nameof(encryptedContent)),
            CreatedAt = DateTime.UtcNow
        };
}
