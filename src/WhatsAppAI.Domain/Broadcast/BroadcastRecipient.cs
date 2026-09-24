namespace WhatsAppAI.Domain.Broadcast;

public sealed class BroadcastRecipient
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BroadcastListId { get; private set; }
    public Guid ContactId { get; private set; }
    public BroadcastRecipientStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? OutboundMessageId { get; private set; }
    public int DispatchAttempt { get; private set; } = 1;
    public DateTime? QueuedAt { get; private set; }
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

    public void MarkQueued(Guid messageId)
    {
        if (Status != BroadcastRecipientStatus.Pending)
            throw new InvalidOperationException("Only pending recipients can be queued.");
        OutboundMessageId = messageId;
        QueuedAt = DateTime.UtcNow;
        Status = BroadcastRecipientStatus.Queued;
    }

    public void Retry()
    {
        if (Status != BroadcastRecipientStatus.Failed)
            throw new InvalidOperationException("Only failed recipients can be retried.");

        Status = BroadcastRecipientStatus.Pending;
        ErrorMessage = null;
        SentAt = null;
        QueuedAt = null;
        OutboundMessageId = null;
        DispatchAttempt++;
    }
}

public enum BroadcastRecipientStatus
{
    Pending = 0,
    Queued = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4,
}
