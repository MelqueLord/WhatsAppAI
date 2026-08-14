using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class UsageLedgerRepository(AppDbContext context) : IUsageLedgerRepository
{
    public async Task AddAsync(UsageLedger entry, CancellationToken cancellationToken = default)
    {
        context.Set<UsageLedger>().Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UsageLedger>> GetByTenantAsync(
        Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await context.Set<UsageLedger>()
            .Where(u => u.TenantId == tenantId && u.RecordedAt >= from && u.RecordedAt <= to)
            .OrderByDescending(u => u.RecordedAt)
            .ToListAsync(cancellationToken);
    }
}
