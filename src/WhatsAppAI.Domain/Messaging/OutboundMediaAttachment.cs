namespace WhatsAppAI.Domain.Messaging;

/// <summary>
/// Encrypted, short-lived media retained only while the durable Outbox needs it.
/// </summary>
public sealed class OutboundMediaAttachment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid MessageId { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long Length { get; private set; }
    public string EncryptedContent { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? PurgedAt { get; private set; }

    private OutboundMediaAttachment() { }

    public static OutboundMediaAttachment Create(
        Guid tenantId,
        Guid messageId,
        string contentType,
        long length,
        string encryptedContent)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (messageId == Guid.Empty) throw new ArgumentException("Message is required.", nameof(messageId));
        if (contentType is not ("image/jpeg" or "image/png")) throw new ArgumentException("Only JPEG and PNG images are supported.", nameof(contentType));
        if (length is <= 0 or > 5 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(length));
        if (string.IsNullOrWhiteSpace(encryptedContent)) throw new ArgumentException("Encrypted content is required.", nameof(encryptedContent));

        return new OutboundMediaAttachment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, MessageId = messageId,
            ContentType = contentType, Length = length, EncryptedContent = encryptedContent,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Purge()
    {
        EncryptedContent = string.Empty;
        PurgedAt = DateTime.UtcNow;
    }
}
