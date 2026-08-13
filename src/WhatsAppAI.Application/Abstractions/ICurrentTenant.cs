namespace WhatsAppAI.Application.Abstractions;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    string? UserRole { get; }
    bool IsPlatformAdmin { get; }
    bool IsAuthenticated { get; }

    void SetContext(Guid tenantId, Guid userId, string role, bool isPlatformAdmin);
    void Clear();
}
