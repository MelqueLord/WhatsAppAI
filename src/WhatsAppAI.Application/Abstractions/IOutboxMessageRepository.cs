using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage outboxMessage);
    Task AddRangeAsync(IEnumerable<OutboxMessage> outboxMessages);
    Task<OutboxMessage?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50);
    Task UpdateAsync(OutboxMessage outboxMessage);
    Task<int> DeleteCompletedBeforeAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken = default);
}
