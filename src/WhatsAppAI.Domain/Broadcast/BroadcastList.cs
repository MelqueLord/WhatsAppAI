namespace WhatsAppAI.Domain.Broadcast;

public sealed class BroadcastList
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public BroadcastStatus Status { get; private set; }
    public string LinePhoneNumberId { get; private set; } = string.Empty;
    public int TotalCount { get; private set; }
    public int SentCount { get; private set; }
    public int FailedCount { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    private BroadcastList() { }

    public static BroadcastList Create(
        Guid tenantId,
        string name,
        string message,
        Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));
        if (message.Length > 4096)
            throw new ArgumentException("Message must be at most 4096 characters.", nameof(message));

        return new BroadcastList
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Message = message,
            Status = BroadcastStatus.Draft,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void StartDispatch(string linePhoneNumberId, int totalCount)
    {
        if (Status != BroadcastStatus.Draft)
            throw new InvalidOperationException("Only draft broadcasts can be dispatched.");
        if (string.IsNullOrWhiteSpace(linePhoneNumberId))
            throw new ArgumentException("Line is required.", nameof(linePhoneNumberId));
        if (totalCount < 1)
            throw new ArgumentException("At least one recipient required.", nameof(totalCount));

        LinePhoneNumberId = linePhoneNumberId;
        TotalCount = totalCount;
        Status = BroadcastStatus.Sending;
        StartedAt = DateTime.UtcNow;
    }

    public void RecordSent()
    {
        SentCount++;
        CheckCompletion();
    }

    public void RecordFailed()
    {
        FailedCount++;
        CheckCompletion();
    }

    public void Cancel()
    {
        if (Status == BroadcastStatus.Completed || Status == BroadcastStatus.Cancelled)
            return;

        Status = BroadcastStatus.Cancelled;
        FinishedAt = DateTime.UtcNow;
    }

    private void CheckCompletion()
    {
        if (Status != BroadcastStatus.Sending)
            return;

        if (SentCount + FailedCount >= TotalCount)
        {
            Status = BroadcastStatus.Completed;
            FinishedAt = DateTime.UtcNow;
        }
    }
}

public enum BroadcastStatus
{
    Draft = 0,
    Sending = 1,
    Completed = 2,
    Cancelled = 3,
}
