namespace WhatsAppAI.Domain.Identity;

public sealed class TenantMembership
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public MembershipRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeactivatedAt { get; private set; }
    public DateTime? ReactivatedAt { get; private set; }
    public uint Version { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private TenantMembership() { }

    public static TenantMembership Create(Guid tenantId, User user, MembershipRole role)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsPlatformAdmin)
            throw new InvalidOperationException("Platform administrators cannot belong to a tenant.");

        return new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            User = user,
            Role = role,
            Status = MembershipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Activate()
    {
        if (Status == MembershipStatus.Active)
            throw new InvalidOperationException("Membership is already active.");

        Status = MembershipStatus.Active;
        Version++;
    }

    public void Deactivate()
    {
        if (Status == MembershipStatus.Inactive)
            throw new InvalidOperationException("Membership is already inactive.");

        Status = MembershipStatus.Inactive;
        DeactivatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Reactivate()
    {
        if (Status != MembershipStatus.Inactive)
            throw new InvalidOperationException("Only inactive memberships can be reactivated.");

        Status = MembershipStatus.Active;
        ReactivatedAt = DateTime.UtcNow;
        Version++;
    }
}

public enum MembershipRole
{
    TenantOwner = 0,
    Operator = 1
}

public enum MembershipStatus
{
    Pending = 0,
    Active = 1,
    Inactive = 2
}
