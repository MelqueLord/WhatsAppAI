namespace WhatsAppAI.Domain.Identity;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public int OfficialApiLineCount { get; private set; }
    public int QrCodeLineCount { get; private set; }
    public int OperatorLimit { get; private set; }
    public int? MonthlyAiResponseLimit { get; private set; }
    public long? MonthlyAiTokenLimit { get; private set; }
    public long? MonthlyAiCostLimitMinorUnits { get; private set; }
    public TenantStatus Status { get; private set; } = TenantStatus.Pending;
    public DateTime CreatedAt { get; private set; }
    public DateTime DueDate { get; private set; }
    public DateTime? LastPaymentAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public DateTime? ReactivatedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public uint Version { get; private set; }

    private readonly List<TenantMembership> _memberships = [];
    public IReadOnlyCollection<TenantMembership> Memberships => _memberships.AsReadOnly();

    private Tenant() { }

    public static Tenant Create(
        string name,
        string slug,
        Guid planId,
        int officialApiLineCount = 0,
        int qrCodeLineCount = 0,
        int operatorLimit = 0,
        int? monthlyAiResponseLimit = null,
        long? monthlyAiTokenLimit = null,
        long? monthlyAiCostLimitMinorUnits = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(officialApiLineCount);
        ArgumentOutOfRangeException.ThrowIfNegative(qrCodeLineCount);
        ArgumentOutOfRangeException.ThrowIfNegative(operatorLimit);
        if (monthlyAiResponseLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiResponseLimit));
        if (monthlyAiTokenLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiTokenLimit));
        if (monthlyAiCostLimitMinorUnits is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiCostLimitMinorUnits));

        var createdAt = DateTime.UtcNow;
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            PlanId = planId,
            OfficialApiLineCount = officialApiLineCount,
            QrCodeLineCount = qrCodeLineCount,
            OperatorLimit = operatorLimit,
            MonthlyAiResponseLimit = monthlyAiResponseLimit,
            MonthlyAiTokenLimit = monthlyAiTokenLimit,
            MonthlyAiCostLimitMinorUnits = monthlyAiCostLimitMinorUnits,
            Status = TenantStatus.Pending,
            CreatedAt = createdAt,
            DueDate = createdAt.AddDays(30)
        };
    }

    public void Activate()
    {
        if (Status == TenantStatus.Active)
            throw new InvalidOperationException("Tenant is already active.");

        if (Status == TenantStatus.Closed)
            throw new InvalidOperationException("Closed tenants cannot be reactivated.");

        Status = TenantStatus.Active;
        ActivatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Suspend(string reason)
    {
        if (Status != TenantStatus.Active)
            throw new InvalidOperationException("Only active tenants can be suspended.");

        Status = TenantStatus.Suspended;
        SuspendedAt = DateTime.UtcNow;
        SuspensionReason = reason;
        Version++;
    }

    public void Reactivate()
    {
        if (Status != TenantStatus.Suspended)
            throw new InvalidOperationException("Only suspended tenants can be reactivated.");

        Status = TenantStatus.Active;
        ReactivatedAt = DateTime.UtcNow;
        SuspensionReason = null;
        Version++;
    }

    public void Close()
    {
        if (Status == TenantStatus.Closed)
            throw new InvalidOperationException("Tenant is already closed.");

        Status = TenantStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        Version++;
    }

    public void ChangePlan(Guid planId)
    {
        PlanId = planId;
        Version++;
    }

    public void ChangePlan(
        Guid planId,
        int officialApiLineCount,
        int qrCodeLineCount,
        int operatorLimit,
        int? monthlyAiResponseLimit,
        long? monthlyAiTokenLimit = null,
        long? monthlyAiCostLimitMinorUnits = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(officialApiLineCount);
        ArgumentOutOfRangeException.ThrowIfNegative(qrCodeLineCount);
        ArgumentOutOfRangeException.ThrowIfNegative(operatorLimit);
        if (monthlyAiResponseLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiResponseLimit));
        if (monthlyAiTokenLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiTokenLimit));
        if (monthlyAiCostLimitMinorUnits is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiCostLimitMinorUnits));

        PlanId = planId;
        OfficialApiLineCount = officialApiLineCount;
        QrCodeLineCount = qrCodeLineCount;
        OperatorLimit = operatorLimit;
        MonthlyAiResponseLimit = monthlyAiResponseLimit;
        MonthlyAiTokenLimit = monthlyAiTokenLimit;
        MonthlyAiCostLimitMinorUnits = monthlyAiCostLimitMinorUnits;
        Version++;
    }

    public void RegisterPayment(DateTime paidAt)
    {
        LastPaymentAt = paidAt.ToUniversalTime();
        DueDate = LastPaymentAt.Value.AddDays(30);
        if (Status == TenantStatus.Suspended)
            Reactivate();
        else
            Version++;
    }

    public void UpdateDetails(
        string name,
        string slug,
        Guid planId,
        int officialApiLineCount,
        int qrCodeLineCount,
        int operatorLimit,
        int? monthlyAiResponseLimit = null,
        long? monthlyAiTokenLimit = null,
        long? monthlyAiCostLimitMinorUnits = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentOutOfRangeException.ThrowIfNegative(officialApiLineCount);
        ArgumentOutOfRangeException.ThrowIfNegative(qrCodeLineCount);
        ArgumentOutOfRangeException.ThrowIfNegative(operatorLimit);
        if (monthlyAiResponseLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiResponseLimit));
        if (monthlyAiTokenLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiTokenLimit));
        if (monthlyAiCostLimitMinorUnits is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyAiCostLimitMinorUnits));

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        PlanId = planId;
        OfficialApiLineCount = officialApiLineCount;
        QrCodeLineCount = qrCodeLineCount;
        OperatorLimit = operatorLimit;
        MonthlyAiResponseLimit = monthlyAiResponseLimit;
        MonthlyAiTokenLimit = monthlyAiTokenLimit;
        MonthlyAiCostLimitMinorUnits = monthlyAiCostLimitMinorUnits;
        Version++;
    }

}

public enum TenantStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Closed = 3
}
