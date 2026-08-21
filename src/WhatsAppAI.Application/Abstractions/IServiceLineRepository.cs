using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IServiceLineRepository
{
    Task<ServiceLine?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceLine>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceLine>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(ServiceLine queue, CancellationToken ct = default);
    Task UpdateAsync(ServiceLine queue, CancellationToken ct = default);
}
