namespace WhatsAppAI.Domain.Usage;

public sealed class AiResponseTopUpRequest
{
    public const int TopUpQuantity = 500;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime PeriodStartUtc { get; private set; }
    public int Quantity { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public AiResponseTopUpRequestStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private AiResponseTopUpRequest() { }

    public static AiResponseTopUpRequest Create(
        Guid tenantId,
        DateTime periodStartUtc,
        string idempotencyKey,
        Guid requestedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new AiResponseTopUpRequest
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PeriodStartUtc = periodStartUtc,
            Quantity = TopUpQuantity,
            IdempotencyKey = idempotencyKey.Trim(),
            Status = AiResponseTopUpRequestStatus.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid reviewedByUserId)
    {
        EnsurePending();
        Status = AiResponseTopUpRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewedByUserId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsurePending();
        Status = AiResponseTopUpRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTime.UtcNow;
        RejectionReason = reason.Trim();
    }

    private void EnsurePending()
    {
        if (Status != AiResponseTopUpRequestStatus.Pending)
            throw new InvalidOperationException("The AI response top-up request is already reviewed.");
    }
}

public enum AiResponseTopUpRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
