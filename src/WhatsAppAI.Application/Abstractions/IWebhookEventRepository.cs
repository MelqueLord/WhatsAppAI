using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IWebhookEventRepository
{
    Task<WebhookEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WebhookEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEvent>> GetPendingEventsAsync(int batchSize = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEvent>> GetRetryableEventsAsync(int batchSize = 10, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task UpdateAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task<int> DeleteProcessedBeforeAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken = default);
}
