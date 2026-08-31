namespace WhatsAppAI.Domain.Usage;

public sealed class AiResponseQuotaReservation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime PeriodStartUtc { get; private set; }
    public Guid SourceMessageId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public AiResponseQuotaPackageType PackageType { get; private set; }
    public string PackageReference { get; private set; } = string.Empty;
    public AiResponseQuotaReservationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CommittedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public string? ReleaseReason { get; private set; }

    private AiResponseQuotaReservation() { }

    public static AiResponseQuotaReservation Create(
        Guid tenantId,
        DateTime periodStartUtc,
        Guid sourceMessageId,
        string idempotencyKey,
        AiResponseQuotaPackageType packageType = AiResponseQuotaPackageType.BasePackage,
        string packageReference = "legacy")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageReference);

        return new AiResponseQuotaReservation
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PeriodStartUtc = periodStartUtc,
            SourceMessageId = sourceMessageId,
            IdempotencyKey = idempotencyKey.Trim(),
            PackageType = packageType,
            PackageReference = packageReference.Trim(),
            Status = AiResponseQuotaReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Commit()
    {
        if (Status == AiResponseQuotaReservationStatus.Committed)
            return;
        EnsurePending();
        Status = AiResponseQuotaReservationStatus.Committed;
        CommittedAt = DateTime.UtcNow;
    }

    public void Release(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status == AiResponseQuotaReservationStatus.Released)
            return;
        EnsurePending();
        Status = AiResponseQuotaReservationStatus.Released;
        ReleasedAt = DateTime.UtcNow;
        ReleaseReason = reason.Trim();
    }

    private void EnsurePending()
    {
        if (Status != AiResponseQuotaReservationStatus.Pending)
            throw new InvalidOperationException("The AI response quota reservation is already finalized.");
    }
}

public enum AiResponseQuotaReservationStatus
{
    Pending = 0,
    Committed = 1,
    Released = 2
}

public enum AiResponseQuotaPackageType
{
    BasePackage = 0,
    TopUpPackage = 1
}
