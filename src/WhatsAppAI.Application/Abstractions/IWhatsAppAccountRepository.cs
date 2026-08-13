using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Application.Abstractions;

public interface IWhatsAppAccountRepository
{
    Task<WhatsAppAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WhatsAppAccount?> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<WhatsAppAccount?> GetByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default);
    Task AddAsync(WhatsAppAccount account, CancellationToken cancellationToken = default);
    Task UpdateAsync(WhatsAppAccount account, CancellationToken cancellationToken = default);
}
