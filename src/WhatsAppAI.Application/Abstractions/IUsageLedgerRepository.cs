using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Application.Abstractions;

public interface IUsageLedgerRepository
{
    Task AddAsync(UsageLedger entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsageLedger>> GetByTenantAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
