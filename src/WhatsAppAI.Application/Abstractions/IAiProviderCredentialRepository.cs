using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Application.Abstractions;

public interface IAiProviderCredentialRepository
{
    Task<AiProviderCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AiProviderCredential?> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<AiProviderCredential?> GetByTenantAndProviderAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default);
    Task AddAsync(AiProviderCredential credential, CancellationToken cancellationToken = default);
    Task UpdateAsync(AiProviderCredential credential, CancellationToken cancellationToken = default);
}
