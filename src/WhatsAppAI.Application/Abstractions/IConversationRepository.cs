using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByContactAndPhoneAsync(Guid tenantId, Guid contactId, string phoneNumberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetByTenantAsync(Guid tenantId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetOpenByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
}
