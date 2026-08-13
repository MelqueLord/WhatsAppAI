namespace WhatsAppAI.Domain.Identity;

public sealed class Invitation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public InvitationPurpose Purpose { get; private set; }
    public InvitationStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public uint Version { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User? User { get; private set; }

    private Invitation() { }

    public static Invitation Create(
        Guid tenantId,
        string email,
        string tokenHash,
        InvitationPurpose purpose,
        Guid createdByUserId,
        Guid? userId = null)
    {
        return new Invitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Email = email.Trim().ToLowerInvariant(),
            TokenHash = tokenHash,
            Purpose = purpose,
            Status = InvitationStatus.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
    }

    public bool IsUsable => Status == InvitationStatus.Pending
        && ExpiresAt > DateTime.UtcNow
        && RevokedAt is null;

    public void Consume()
    {
        if (!IsUsable)
            throw new InvalidOperationException("Invitation is not usable.");

        Status = InvitationStatus.Consumed;
        ConsumedAt = DateTime.UtcNow;
        Version++;
    }

    public void Revoke(Guid revokedByUserId)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be revoked.");

        Status = InvitationStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
        RevokedByUserId = revokedByUserId;
        Version++;
    }
}

public enum InvitationPurpose
{
    TenantOwner = 0,
    Operator = 1
}

public enum InvitationStatus
{
    Pending = 0,
    Consumed = 1,
    Revoked = 2,
    Expired = 3
}
