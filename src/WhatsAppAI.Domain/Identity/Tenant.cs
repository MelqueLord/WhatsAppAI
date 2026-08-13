namespace WhatsAppAI.Domain.Identity;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public DateTime CreatedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public DateTime? ReactivatedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public uint Version { get; private set; }

    private readonly List<TenantMembership> _memberships = [];
    public IReadOnlyCollection<TenantMembership> Memberships => _memberships.AsReadOnly();

    private Tenant() { }

    public static Tenant Create(string name)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Suspend(string reason)
    {
        if (Status == TenantStatus.Suspended)
            throw new InvalidOperationException("Tenant is already suspended.");

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
}

public enum TenantStatus
{
    Active = 0,
    Suspended = 1
}
