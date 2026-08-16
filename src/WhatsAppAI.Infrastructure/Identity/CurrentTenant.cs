using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Identity;

internal sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserRole { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public bool IsAuthenticated => UserId.HasValue;
    public SupportSessionInfo? SupportSession { get; private set; }

    public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin)
    {
        TenantId = isPlatformAdmin ? null : tenantId;
        UserId = userId;
        UserRole = isPlatformAdmin ? "PlatformAdmin" : role;
        IsPlatformAdmin = isPlatformAdmin;
        SupportSession = null;
    }

    public void EnterSupportSession(Guid tenantId, string reason)
    {
        if (!IsPlatformAdmin)
            throw new InvalidOperationException("Only platform administrators can enter support sessions.");

        TenantId = tenantId;
        UserRole = "PlatformAdmin";
        SupportSession = new SupportSessionInfo(tenantId, reason, DateTime.UtcNow);
    }

    public void ExitSupportSession()
    {
        if (SupportSession is null)
            return;

        TenantId = null;
        UserRole = "PlatformAdmin";
        SupportSession = null;
    }

    public void Clear()
    {
        TenantId = null;
        UserId = null;
        UserRole = null;
        IsPlatformAdmin = false;
        SupportSession = null;
    }
}
