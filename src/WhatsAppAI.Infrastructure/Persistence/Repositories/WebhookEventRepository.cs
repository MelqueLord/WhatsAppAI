using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Secrets;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class WebhookEventRepository(
    AppDbContext context,
    IEncryptionService encryptionService) : IWebhookEventRepository
{
    public async Task<WebhookEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<WebhookEvent>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<WebhookEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await context.Set<WebhookEvent>()
            .FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookEvent>> GetPendingEventsAsync(int batchSize = 10, CancellationToken cancellationToken = default)
    {
        return await context.Set<WebhookEvent>()
            .Where(e => e.Status == WebhookEventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookEvent>> GetRetryableEventsAsync(int batchSize = 10, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await context.Set<WebhookEvent>()
            .Where(e => e.Status == WebhookEventStatus.Failed
                && e.NextRetryAt.HasValue
                && e.NextRetryAt.Value <= now
                && e.RetryCount < 5)
            .OrderBy(e => e.NextRetryAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        // Encrypt the payload before storing
        webhookEvent.SetEncryptedPayload(encryptionService.Encrypt(webhookEvent.EncryptedPayload));

        await context.Set<WebhookEvent>().AddAsync(webhookEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        context.Set<WebhookEvent>().Update(webhookEvent);
        await context.SaveChangesAsync(cancellationToken);
    }
}
