using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Application.Abstractions;

public interface IUsageLedgerRepository
{
    Task AddAsync(UsageLedger entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsageLedger>> GetByTenantAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<long> GetTotalQuantityAsync(
        Guid tenantId,
        string metric,
        DateTime from,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, long>> GetTotalQuantityByTenantAsync(
        string metric,
        DateTime from,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);
}
