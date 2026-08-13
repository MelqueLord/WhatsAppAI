using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Contact?> GetByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);
    Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default);
}
