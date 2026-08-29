using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class OutboxMessageRepository(AppDbContext context) : IOutboxMessageRepository
{
    public async Task AddAsync(OutboxMessage outboxMessage)
    {
        context.Set<OutboxMessage>().Add(outboxMessage);
        await context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<OutboxMessage> outboxMessages)
    {
        context.Set<OutboxMessage>().AddRange(outboxMessages);
        await context.SaveChangesAsync();
    }

    public async Task<OutboxMessage?> GetByIdAsync(Guid id)
    {
        return await context.Set<OutboxMessage>().FindAsync(id);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50)
    {
        var now = DateTime.UtcNow;
        return await context.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(o => o.Status == OutboxStatus.Pending && (o.NextRetryAt == null || o.NextRetryAt <= now))
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<bool> TryClaimAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var affected = await context.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(o => o.Id == id && o.Status == OutboxStatus.Pending &&
                (o.NextRetryAt == null || o.NextRetryAt <= utcNow))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OutboxStatus.Processing), cancellationToken);
        return affected == 1;
    }

    public async Task UpdateAsync(OutboxMessage outboxMessage)
    {
        context.Set<OutboxMessage>().Update(outboxMessage);
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteCompletedBeforeAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken = default)
    {
        var toDelete = await context.Set<OutboxMessage>()
            .Where(o => o.Status == OutboxStatus.Completed && o.ProcessedAt < cutoff)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (toDelete.Count == 0) return 0;

        context.Set<OutboxMessage>().RemoveRange(toDelete);
        await context.SaveChangesAsync(cancellationToken);
        return toDelete.Count;
    }
}
