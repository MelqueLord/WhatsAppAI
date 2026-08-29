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

    public async Task<long> GetTotalQuantityAsync(
        Guid tenantId,
        string metric,
        DateTime from,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<UsageLedger>()
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId &&
                u.Metric == metric &&
                u.RecordedAt >= from &&
                u.RecordedAt < toExclusive)
            .SumAsync(u => (long?)u.Quantity, cancellationToken) ?? 0;
    }

    public async Task<IReadOnlyDictionary<Guid, long>> GetTotalQuantityByTenantAsync(
        string metric,
        DateTime from,
        DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<UsageLedger>()
            .IgnoreQueryFilters()
            .Where(u => u.Metric == metric &&
                u.RecordedAt >= from &&
                u.RecordedAt < toExclusive)
            .GroupBy(u => u.TenantId)
            .Select(group => new { TenantId = group.Key, Quantity = group.Sum(u => u.Quantity) })
            .ToDictionaryAsync(item => item.TenantId, item => item.Quantity, cancellationToken);
    }
}
