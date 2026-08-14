using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Audit;

namespace WhatsAppAI.Application.Audit;

public sealed class AuditService(IAuditLogRepository repository)
{
    public async Task LogAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var entry = AuditLog.Create(tenantId, userId, action, entityType, entityId, details, ipAddress);
        await repository.AddAsync(entry, cancellationToken);
    }
}
