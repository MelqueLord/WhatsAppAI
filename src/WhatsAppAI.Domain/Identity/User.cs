namespace WhatsAppAI.Domain.Identity;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public string? DisplayName { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public bool MustChangePassword { get; private set; }
    public string SecurityStamp { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    private readonly List<TenantMembership> _memberships = [];
    public IReadOnlyCollection<TenantMembership> Memberships => _memberships.AsReadOnly();

    private User() { }

    public static User Create(string email, string? displayName = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName?.Trim(),
            IsActive = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateWithTemporaryPassword(string email, string passwordHash, string? displayName = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName?.Trim(),
            PasswordHash = passwordHash,
            IsActive = true,
            MustChangePassword = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow
        };
    }

    public void Activate(string passwordHash)
    {
        if (IsActive)
            throw new InvalidOperationException("User is already active.");

        PasswordHash = passwordHash;
        IsActive = true;
        ActivatedAt = DateTime.UtcNow;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("User is already inactive.");

        IsActive = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void UpdatePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        MustChangePassword = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void UpdateEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim().ToLowerInvariant();
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void UpdateDisplayName(string? displayName)
    {
        DisplayName = displayName?.Trim();
    }

    public void GrantPlatformAdmin()
    {
        IsPlatformAdmin = true;
    }

    public void RevokePlatformAdmin()
    {
        IsPlatformAdmin = false;
    }

    public void SetMustChangePassword(bool mustChange)
    {
        MustChangePassword = mustChange;
    }
}
