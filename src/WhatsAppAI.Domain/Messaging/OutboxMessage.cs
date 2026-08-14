namespace WhatsAppAI.Domain.Messaging;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid MessageId { get; private set; }
    public OutboxStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(Guid tenantId, Guid messageId)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = messageId,
            Status = OutboxStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessing()
    {
        Status = OutboxStatus.Processing;
    }

    public void MarkCompleted()
    {
        Status = OutboxStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error, TimeSpan retryDelay)
    {
        Status = OutboxStatus.Pending;
        RetryCount++;
        LastError = error;
        NextRetryAt = DateTime.UtcNow.Add(retryDelay);
    }

    public void MarkDead(string error)
    {
        Status = OutboxStatus.Dead;
        LastError = error;
    }

    public bool IsReadyForProcessing(DateTime utcNow) =>
        Status == OutboxStatus.Pending && (NextRetryAt is null || NextRetryAt <= utcNow);
}

public enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Dead = 3
}
