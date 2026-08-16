namespace WhatsAppAI.Application.Abstractions;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    string? UserRole { get; }
    bool IsPlatformAdmin { get; }
    bool IsAuthenticated { get; }
    SupportSessionInfo? SupportSession { get; }

    void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin);
    void EnterSupportSession(Guid tenantId, string reason);
    void ExitSupportSession();
    void Clear();
}

public sealed record SupportSessionInfo(
    Guid TenantId,
    string Reason,
    DateTime StartedAt);
