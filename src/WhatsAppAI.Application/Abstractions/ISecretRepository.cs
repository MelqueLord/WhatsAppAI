namespace WhatsAppAI.Application.Abstractions;

public interface ISecretRepository
{
    Task<Domain.Integrations.Secret?> GetByKeyAsync(string key, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Domain.Integrations.Secret secret, CancellationToken cancellationToken = default);
    Task UpdateAsync(Domain.Integrations.Secret secret, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, Guid? tenantId = null, CancellationToken cancellationToken = default);
}
