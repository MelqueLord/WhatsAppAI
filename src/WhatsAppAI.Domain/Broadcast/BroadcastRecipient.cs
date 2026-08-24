namespace WhatsAppAI.Domain.Broadcast;

public sealed class BroadcastRecipient
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BroadcastListId { get; private set; }
    public Guid ContactId { get; private set; }
    public BroadcastRecipientStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    // Navigation
    public BroadcastList? BroadcastList { get; private set; }

    private BroadcastRecipient() { }

    public static BroadcastRecipient Create(Guid tenantId, Guid broadcastListId, Guid contactId)
    {
        return new BroadcastRecipient
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BroadcastListId = broadcastListId,
            ContactId = contactId,
            Status = BroadcastRecipientStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void MarkSent()
    {
        Status = BroadcastRecipientStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = BroadcastRecipientStatus.Failed;
        ErrorMessage = error;
    }
}

public enum BroadcastRecipientStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3,
}
