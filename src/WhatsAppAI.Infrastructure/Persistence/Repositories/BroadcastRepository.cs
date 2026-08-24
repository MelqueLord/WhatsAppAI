using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Broadcast;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class BroadcastRepository(AppDbContext context) : IBroadcastRepository
{
    public async Task<BroadcastList?> GetByIdAsync(Guid id)
    {
        return await context.Set<BroadcastList>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IReadOnlyList<BroadcastList>> GetByTenantAsync(Guid tenantId, int limit = 50)
    {
        return await context.Set<BroadcastList>()
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<BroadcastList?> GetActiveSendingAsync(Guid tenantId)
    {
        return await context.Set<BroadcastList>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Status == BroadcastStatus.Sending);
    }

    public async Task AddAsync(BroadcastList broadcast)
    {
        context.Set<BroadcastList>().Add(broadcast);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BroadcastList broadcast)
    {
        context.Set<BroadcastList>().Update(broadcast);
        await context.SaveChangesAsync();
    }

    public async Task AddRecipientsAsync(IEnumerable<BroadcastRecipient> recipients)
    {
        context.Set<BroadcastRecipient>().AddRange(recipients);
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<BroadcastRecipient>> GetPendingRecipientsAsync(
        Guid broadcastListId, int batchSize = 10)
    {
        return await context.Set<BroadcastRecipient>()
            .IgnoreQueryFilters()
            .Where(r => r.BroadcastListId == broadcastListId
                     && r.Status == BroadcastRecipientStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<BroadcastRecipient?> GetRecipientByIdAsync(Guid id)
    {
        return await context.Set<BroadcastRecipient>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task UpdateRecipientAsync(BroadcastRecipient recipient)
    {
        context.Set<BroadcastRecipient>().Update(recipient);
        await context.SaveChangesAsync();
    }
}
