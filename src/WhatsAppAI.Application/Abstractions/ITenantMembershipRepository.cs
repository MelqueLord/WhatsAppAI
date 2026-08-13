using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Application.Abstractions;

public interface ITenantMembershipRepository
{
    Task<TenantMembership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantMembership?> GetByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantMembership>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantMembership>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(TenantMembership membership, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantMembership membership, CancellationToken cancellationToken = default);
}
