using WhatsAppAI.Domain.Audit;

namespace WhatsAppAI.Application.Abstractions;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetByTenantAsync(Guid tenantId, DateTime from, DateTime to, int limit = 100, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid tenantId, string action, string entityId, CancellationToken cancellationToken = default);
}
