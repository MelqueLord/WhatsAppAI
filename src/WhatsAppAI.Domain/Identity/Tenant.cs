namespace WhatsAppAI.Domain.Identity;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public TenantStatus Status { get; private set; } = TenantStatus.Pending;
    public DateTime CreatedAt { get; private set; }
    public DateTime DueDate { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public DateTime? ReactivatedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public uint Version { get; private set; }

    private readonly List<TenantMembership> _memberships = [];
    public IReadOnlyCollection<TenantMembership> Memberships => _memberships.AsReadOnly();

    private Tenant() { }

    public static Tenant Create(string name, string slug, Guid planId)
    {
        var createdAt = DateTime.UtcNow;
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            PlanId = planId,
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
}

public enum TenantStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Closed = 3
}
