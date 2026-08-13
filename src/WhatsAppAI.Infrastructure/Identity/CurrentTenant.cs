using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Identity;

internal sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserRole { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public bool IsAuthenticated => TenantId.HasValue && UserId.HasValue;

    public void SetContext(Guid tenantId, Guid userId, string role, bool isPlatformAdmin)
    {
        TenantId = tenantId;
        UserId = userId;
        UserRole = role;
        IsPlatformAdmin = isPlatformAdmin;
    }

    public void Clear()
    {
        TenantId = null;
        UserId = null;
        UserRole = null;
        IsPlatformAdmin = false;
    }
}
