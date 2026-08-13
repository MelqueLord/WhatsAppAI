using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Message?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> GetByConversationAsync(Guid conversationId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> GetByTenantAsync(Guid tenantId, int limit = 100, CancellationToken cancellationToken = default);
    Task AddAsync(Message message, CancellationToken cancellationToken = default);
    Task UpdateAsync(Message message, CancellationToken cancellationToken = default);
}
