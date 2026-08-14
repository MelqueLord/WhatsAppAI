using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Application.Abstractions;

public interface IClientTagRepository
{
    Task<ClientTag?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClientTag>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<ClientTag>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(ClientTag tag, CancellationToken ct = default);
    Task UpdateAsync(ClientTag tag, CancellationToken ct = default);
}
