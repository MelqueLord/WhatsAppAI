using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Application.Abstractions;

public interface IAiResponseExampleRepository
{
    Task<AiResponseExample?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiResponseExample>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiResponseExample>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(AiResponseExample example, CancellationToken cancellationToken = default);
    Task UpdateAsync(AiResponseExample example, CancellationToken cancellationToken = default);
}
