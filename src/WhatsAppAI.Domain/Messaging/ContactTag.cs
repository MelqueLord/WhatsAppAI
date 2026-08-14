namespace WhatsAppAI.Domain.Messaging;

public sealed class ContactTag
{
    public Guid Id { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid TagId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ContactTag() { }

    public static ContactTag Create(Guid contactId, Guid tagId, Guid tenantId)
    {
        return new ContactTag
        {
            Id = Guid.NewGuid(),
            ContactId = contactId,
            TagId = tagId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
