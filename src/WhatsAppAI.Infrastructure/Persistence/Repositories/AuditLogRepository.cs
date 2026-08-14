using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Audit;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(AppDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        context.Set<AuditLog>().Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByTenantAsync(
        Guid tenantId, DateTime from, DateTime to, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await context.Set<AuditLog>()
            .Where(a => a.TenantId == tenantId && a.OccurredAt >= from && a.OccurredAt <= to)
            .OrderByDescending(a => a.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
