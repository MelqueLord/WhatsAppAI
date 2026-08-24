using WhatsAppAI.Domain.Broadcast;

namespace WhatsAppAI.Application.Abstractions;

public interface IBroadcastRepository
{
    Task<BroadcastList?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<BroadcastList>> GetByTenantAsync(Guid tenantId, int limit = 50);
    Task<BroadcastList?> GetActiveSendingAsync(Guid tenantId);
    Task AddAsync(BroadcastList broadcast);
    Task UpdateAsync(BroadcastList broadcast);

    Task AddRecipientsAsync(IEnumerable<BroadcastRecipient> recipients);
    Task<IReadOnlyList<BroadcastRecipient>> GetPendingRecipientsAsync(Guid broadcastListId, int batchSize = 10);
    Task<BroadcastRecipient?> GetRecipientByIdAsync(Guid id);
    Task UpdateRecipientAsync(BroadcastRecipient recipient);
}
