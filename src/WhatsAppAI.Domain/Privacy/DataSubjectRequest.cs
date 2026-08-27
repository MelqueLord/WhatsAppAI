namespace WhatsAppAI.Domain.Privacy;

public sealed class DataSubjectRequest
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public DataSubjectRequestType Type { get; private set; }
    public DataSubjectRequestStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime DueAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTime? ReviewAt { get; private set; }

    private DataSubjectRequest() { }

    public static DataSubjectRequest Create(
        Guid tenantId,
        Guid contactId,
        DataSubjectRequestType type,
        Guid requestedByUserId)
    {
        var now = DateTime.UtcNow;
        return new DataSubjectRequest
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ContactId = contactId,
            Type = type,
            Status = DataSubjectRequestStatus.Open,
            RequestedByUserId = requestedByUserId,
            RequestedAt = now,
            DueAt = now.AddDays(15)
        };
    }

    public void Complete(Guid resolvedByUserId)
    {
        EnsureOpen();
        Status = DataSubjectRequestStatus.Completed;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Deny(Guid resolvedByUserId, string reason, DateTime reviewAt)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reviewAt, DateTime.UtcNow);

        Status = DataSubjectRequestStatus.Denied;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = DateTime.UtcNow;
        DecisionReason = reason.Trim();
        ReviewAt = reviewAt;
    }

    private void EnsureOpen()
    {
        if (Status != DataSubjectRequestStatus.Open)
            throw new InvalidOperationException("The request is already resolved.");
    }
}

public enum DataSubjectRequestType
{
    Access = 0,
    Portability = 1,
    Correction = 2,
    Anonymization = 3,
    Blocking = 4,
    Erasure = 5
}

public enum DataSubjectRequestStatus
{
    Open = 0,
    Completed = 1,
    Denied = 2
}
